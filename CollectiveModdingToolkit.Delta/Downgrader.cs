/*
 *
 * Collective Modding Toolkit
 * Copyright (C) 2024, 2025, 2026  wxMichael
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, see <https://www.gnu.org/licenses/>.
 *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CollectiveModdingToolkit.Delta;

public enum MessageKind
{
	Info,
	Good,
	Bad,
}

/// <summary>One line for the Downgrader's log pane.</summary>
public readonly record struct DowngradeMessage(MessageKind Kind, string Text);

/// <summary>A tracked file as found on disk.</summary>
public sealed record FileState(TrackedFile File, Edition Edition, uint? Crc);

public sealed record DowngradeOptions(bool KeepBackups, bool DeletePatches);

public sealed record DowngradeResult(int Patched, int Failed, int Skipped);

/// <summary>Where patches come from. The app downloads them; tests read a folder.</summary>
public interface IPatchSource
{
	/// <summary>Gets <paramref name="patchName" /> and writes it to <paramref name="destination" />.</summary>
	/// <exception cref="IOException">The patch could not be fetched. The message is shown to the user.</exception>
	Task FetchAsync(string patchName, string destination, IProgress<double>? progress, CancellationToken cancel);
}

/// <summary>
///     Switches Fallout 4 and the Creation Kit between Old-Gen, Next-Gen and Anniversary by replacing six files.
///     <para>
///         For each file: if a backup of the wanted version exists it is restored; otherwise a delta patch is fetched and
///         applied to the installed file. The installed file is backed up under its own edition's name first
///         (<c>Fallout4_downgradeBackup.exe</c> etc., compatible with Simple Downgrader). The game files are never touched
///         until the replacement has been fully built and verified, so a failure leaves the install as it was.
///     </para>
/// </summary>
public sealed class Downgrader(
	string gamePath,
	IPatchSource patches,
	string patchFolder,
	IReadOnlyList<TrackedFile>? files = null)
{
	private static readonly Dictionary<Edition, string> BackupSuffixes = new()
	{
		[Edition.OldGen] = "_upgradeBackup",
		[Edition.NextGen] = "_downgradeBackup",
		[Edition.Anniversary] = "_anniversaryBackup",
	};

	private readonly IReadOnlyList<TrackedFile> _files = files ?? GameFiles.All;

	/// <summary>Identifies every tracked file by CRC32. Reads about 150 MB, so call it off the UI thread.</summary>
	public IReadOnlyList<FileState> Inspect() => [.. _files.Select(Inspect)];

	private FileState Inspect(TrackedFile file)
	{
		var path = Path.Combine(gamePath, file.RelativePath);
		if (!File.Exists(path)) return new FileState(file, Edition.NotFound, null);

		try
		{
			var crc = GameFiles.Crc32OfFile(path);
			return new FileState(file, file.Identify(crc), crc);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return new FileState(file, Edition.Unknown, null);
		}
	}

	public static string BackupPath(string path, Edition edition) =>
		Path.Combine(Path.GetDirectoryName(path)!,
			Path.GetFileNameWithoutExtension(path) + BackupSuffixes[edition] + Path.GetExtension(path));

	/// <summary>Patches every file that isn't already <paramref name="desired" />.</summary>
	/// <param name="progress">Overall progress, 0 to 100.</param>
	public async Task<DowngradeResult> RunAsync(
		Edition desired,
		DowngradeOptions options,
		IProgress<DowngradeMessage> log,
		IProgress<double> progress,
		CancellationToken cancel)
	{
		if (!GameFiles.Supported.Contains(desired)) throw new ArgumentOutOfRangeException(nameof(desired));

		int skipped = 0, patched = 0, failed = 0;
		List<(FileState State, Edition From)> work = [];
		foreach (var state in Inspect())
		{
			var name = state.File.Name;
			if (state.Edition == Edition.NotFound)
			{
				log.Report(new DowngradeMessage(MessageKind.Info, $"Skipped {name}: Not Found."));
				skipped++;
				continue;
			}

			if (state.Edition == desired
			    || (state.Edition == Edition.NextGenAnniversary && desired is Edition.NextGen or Edition.Anniversary))
			{
				log.Report(new DowngradeMessage(MessageKind.Info, $"Skipped {name}: Already {Describe(desired)}."));
				skipped++;
				continue;
			}

			// Next-Gen and Anniversary share this file, and its patches are named for Next-Gen.
			var from = state.Edition == Edition.NextGenAnniversary ? Edition.NextGen : state.Edition;
			if (!GameFiles.Supported.Contains(from) || state.Crc is null)
			{
				log.Report(new DowngradeMessage(MessageKind.Info, $"Skipped {name}: Unsupported Version."));
				skipped++;
				continue;
			}

			work.Add((state, from));
		}

		progress.Report(0);
		for (var i = 0; i < work.Count; i++)
		{
			cancel.ThrowIfCancellationRequested();
			var (state, from) = work[i];
			var done = i;
			Progress<double> fileProgress = new(f => progress.Report((done + f / 100) / work.Count * 100));
			try
			{
				if (await PatchFileAsync(state, from, desired, options, log, fileProgress, cancel)) patched++;
				else failed++;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or DeltaException)
			{
				failed++;
				log.Report(new DowngradeMessage(MessageKind.Bad, $"Failed patching {state.File.Name}: {e.Message}"));
			}

			progress.Report((i + 1.0) / work.Count * 100);
		}

		return new DowngradeResult(patched, failed, skipped);
	}

	private async Task<bool> PatchFileAsync(
		FileState state,
		Edition from,
		Edition desired,
		DowngradeOptions options,
		IProgress<DowngradeMessage> log,
		IProgress<double> progress,
		CancellationToken cancel)
	{
		var file = state.File;
		var currentCrc = state.Crc!.Value;
		var path = Path.Combine(gamePath, file.RelativePath);
		var backupCurrent = BackupPath(path, from);
		var temp = path + ".cmt-new";

		ClearReadOnly(path);
		try
		{
			var backupDesired = FindBackup(path, file, desired);
			if (backupDesired != null)
			{
				// A backup under the current edition's name that is really the wanted version (a Simple Downgrader
				// quirk, or a rename). Give it its own name so the current file can take over the old one.
				if (string.Equals(backupDesired, backupCurrent, StringComparison.OrdinalIgnoreCase))
				{
					var own = BackupPath(path, desired);
					File.Move(backupDesired, own, true);
					backupDesired = own;
				}

				File.Copy(backupDesired, temp, true);
				Commit(path, temp, backupCurrent, currentCrc, options.KeepBackups);
				if (!options.KeepBackups) DeleteQuietly(backupDesired);

				log.Report(new DowngradeMessage(MessageKind.Good,
					$"Patched {file.Name} (restored from {Path.GetFileName(backupDesired)})"));
				return true;
			}

			var patchName = GameFiles.PatchFileName(from, desired, file);
			var patchPath = Path.Combine(patchFolder, patchName);
			if (!IsPatchFor(patchPath, currentCrc, file, desired))
			{
				Directory.CreateDirectory(patchFolder);
				log.Report(new DowngradeMessage(MessageKind.Info, $"Downloading {patchName}..."));
				try
				{
					await patches.FetchAsync(patchName, patchPath, progress, cancel);
				}
				catch (IOException e)
				{
					log.Report(new DowngradeMessage(MessageKind.Bad,
						$"Failed downloading patch for {file.Name}: {e.Message}"));
					return false;
				}

				if (!IsPatchFor(patchPath, currentCrc, file, desired))
				{
					DeleteQuietly(patchPath);
					log.Report(new DowngradeMessage(MessageKind.Bad,
						$"Failed patching {file.Name}: the downloaded patch does not match your file."));
					return false;
				}
			}

			cancel.ThrowIfCancellationRequested();
			try
			{
				DeltaApplier.ApplyFile(patchPath, path, temp);
			}
			catch (DeltaException e)
			{
				DeleteQuietly(patchPath); // A corrupt download would fail the same way every time.
				log.Report(new DowngradeMessage(MessageKind.Bad, $"Failed patching {file.Name}: {e.Message}"));
				return false;
			}

			if (!GameFiles.IsEdition(file, GameFiles.Crc32OfFile(temp), desired))
			{
				log.Report(new DowngradeMessage(MessageKind.Bad,
					$"Failed patching {file.Name}: the result is not a known {Describe(desired)} file."));
				return false;
			}

			cancel.ThrowIfCancellationRequested();
			Commit(path, temp, backupCurrent, currentCrc, options.KeepBackups);
			if (options.DeletePatches) DeleteQuietly(patchPath);

			log.Report(new DowngradeMessage(MessageKind.Good, $"Patched {file.Name}"));
			return true;
		}
		finally
		{
			DeleteQuietly(temp);
		}
	}

	/// <summary>
	///     Backs up the installed file under its own edition's name unless a good backup is already there, then swaps in
	///     the finished replacement. Only the swap touches the installed file, and it is a single move.
	/// </summary>
	private static void Commit(string path, string temp, string backupCurrent, uint currentCrc, bool keepBackups)
	{
		var hasBackup = false;
		if (File.Exists(backupCurrent))
		{
			ClearReadOnly(backupCurrent);
			hasBackup = TryCrc(backupCurrent) == currentCrc;
		}

		if (!hasBackup) File.Copy(path, backupCurrent, true);

		File.Move(temp, path, true);
		if (!keepBackups) DeleteQuietly(backupCurrent);
	}

	/// <summary>A backup of <paramref name="edition" />, whatever it is named, as long as its CRC32 proves it.</summary>
	private static string? FindBackup(string path, TrackedFile file, Edition edition)
	{
		foreach (var candidate in new[] { edition }.Concat(GameFiles.Supported.Where(e => e != edition)))
		{
			var backup = BackupPath(path, candidate);
			if (File.Exists(backup) && TryCrc(backup) is { } crc && GameFiles.IsEdition(file, crc, edition))
				return backup;
		}

		return null;
	}

	/// <summary>Whether a patch file on disk turns this exact file into the wanted edition.</summary>
	private static bool IsPatchFor(string patchPath, uint currentCrc, TrackedFile file, Edition desired)
	{
		if (!File.Exists(patchPath)) return false;

		try
		{
			var info = DeltaFormat.ReadInfo(File.ReadAllBytes(patchPath));
			return info.OldCrc == currentCrc && GameFiles.IsEdition(file, info.NewCrc, desired);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or DeltaException)
		{
			return false;
		}
	}

	private static uint? TryCrc(string path)
	{
		try
		{
			return GameFiles.Crc32OfFile(path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	private static void ClearReadOnly(string path)
	{
		if (!File.Exists(path)) return;

		var attributes = File.GetAttributes(path);
		if ((attributes & FileAttributes.ReadOnly) != 0)
			File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
	}

	private static void DeleteQuietly(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			// A leftover temp or patch file is harmless.
		}
	}

	/// <summary>The name shown to users.</summary>
	public static string Describe(Edition edition) => edition switch
	{
		Edition.OldGen => "Old-Gen",
		Edition.NextGen => "Next-Gen",
		Edition.Anniversary => "Anniversary",
		Edition.NextGenAnniversary => "Next-Gen & Anniversary",
		Edition.Obsolete => "Obsolete",
		Edition.NotFound => "Not Found",
		_ => "Unknown",
	};
}
