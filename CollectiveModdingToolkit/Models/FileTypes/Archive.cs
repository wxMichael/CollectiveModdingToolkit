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
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectiveModdingToolkit.Models.FileTypes;

public partial class Archive : ObservableObject, IFile
{
	/// <summary>Why the last header read failed, or null if it succeeded.</summary>
	private FailReason? _readFailReason;

	// TODO: check if counted against limits (+ba2) when !IsReadable
	internal Archive(string name, string pathFull)
	{
		Name = name;
		PathFull = pathFull;
		ReadHeader();
	}

	[ObservableProperty] public partial byte? ArchiveVersion { get; private set; }

	/// <summary>"GNRL" or "DX10". Null if unreadable or the format is unrecognized.</summary>
	public string? ArchiveFormat { get; private set; }

	public string OriginText => Origin?.DisplayName ?? "Unknown";

	public bool? IsEnabled { get; set; }

	[ObservableProperty] public partial bool IsOG { get; private set; }
	[ObservableProperty] public partial bool IsNG { get; private set; }
	public bool IsGNRL => ArchiveFormat == "GNRL";
	public bool IsDX10 => ArchiveFormat == "DX10";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PatchFailed))]
	public partial bool? PatchSuccessful { get; private set; }

	public bool PatchFailed => PatchSuccessful == false;
	[ObservableProperty] public partial FailReason? PatchFailReason { get; private set; } = null;

	[MemberNotNullWhen(true, nameof(Mod))] public bool IsManaged => Mod != null;
	public string Name { get; }
	public string PathFull { get; }

	[MemberNotNullWhen(true, nameof(ArchiveVersion))]
	public bool? IsReadable { get; private set; }

	public string? Mod => Origin is { IsGameFolder: false } ? Origin.ModName : null;

	public FileOrigin? Origin { get; private set; }

	/// <summary>A user-facing explanation of a failed patch.</summary>
	public static string Describe(FailReason reason) => reason switch
	{
		FailReason.FileNotFound => "File not found.",
		FailReason.NotBA2 => "Not a BA2 archive (no BTDX header).",
		FailReason.UnknownVersion => "Unknown archive version. Only v1, v7, and v8 can be patched.",
		FailReason.InUse => "The file is in-use. Close the game, Creation Kit, or other apps and try again.",
		FailReason.AccessDenied => "Access denied. The file may be read-only or need administrator rights.",
		FailReason.ReadFailed => "Unable to read the file.",
		FailReason.WriteFailed => "Unable to write to the file.",
		_ => "Cause unknown.",
	};

	/// <summary>Sorts an I/O failure into in-use, access denied, or <paramref name="fallback" />.</summary>
	private static FailReason Classify(Exception e, FailReason fallback)
	{
		const int sharingViolation = 32;
		const int lockViolation = 33;
		return e switch
		{
			UnauthorizedAccessException => FailReason.AccessDenied,
			IOException when (e.HResult & 0xFFFF) is sharingViolation or lockViolation => FailReason.InUse,
			_ => fallback,
		};
	}

	protected override void OnPropertyChanged(PropertyChangedEventArgs e)
	{
		if (Dispatcher.UIThread.CheckAccess())
			base.OnPropertyChanged(e);
		else
			Dispatcher.UIThread.Post(() => base.OnPropertyChanged(e));
	}

	internal void SetOrigin(FileOrigin? origin)
	{
		Origin = origin;

		// The constructor read PathFull; if the real file is elsewhere (mod folder), read that instead.
		if (origin != null && !string.Equals(origin.SourcePath, PathFull, StringComparison.OrdinalIgnoreCase))
			ReadHeader();
	}

	/// <summary>Sets <see cref="ArchiveVersion" /> and the <see cref="IsOG" />/<see cref="IsNG" /> flags derived from it.</summary>
	private void SetArchiveVersion(byte? version)
	{
		ArchiveVersion = version;
		IsOG = version == 1;
		IsNG = version is 8 or 7;
	}

	[MemberNotNull(nameof(IsReadable))]
	private void ReadHeader()
	{
		_readFailReason = null;
		SetArchiveVersion(null);
		ArchiveFormat = null;
		var header = new byte[12];
		int bytesRead;
		try
		{
			using var file = File.OpenRead(this.RealPath);
			bytesRead = file.ReadAtLeast(header, header.Length, false);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			_readFailReason = e is FileNotFoundException or DirectoryNotFoundException
				? FailReason.FileNotFound
				: Classify(e, FailReason.ReadFailed);
			IsReadable = false;
			return;
		}

		if (bytesRead != header.Length || !header.AsSpan(0, 4).SequenceEqual(Magic.BTDX))
		{
			_readFailReason = FailReason.NotBA2;
			IsReadable = false;
			return;
		}

		SetArchiveVersion(header[4]);

		if (header.AsSpan(8, 4).SequenceEqual(Magic.GNRL))
			ArchiveFormat = "GNRL";
		else if (header.AsSpan(8, 4).SequenceEqual(Magic.DX10)) ArchiveFormat = "DX10";

		IsReadable = true;
	}

	private static void WriteVersion(string path, byte version)
	{
		using var file = File.OpenWrite(path);
		file.Position = 4;
		file.Write(new ReadOnlySpan<byte>(ref version));
	}

	private static int ReadVersionByte(string path)
	{
		using var file = File.OpenRead(path);
		file.Position = 4;
		return file.ReadByte();
	}

	/// <summary>
	///     Sets the header version to v1 (OG) or v8 (NG). An archive already usable by the target (v1 for v1; v7 or v8
	///     for v8) is left untouched. Only v1, v7, and v8 archives are changed.
	/// </summary>
	internal PatchOutcome SetVersion(byte version)
	{
		PatchSuccessful = null;
		PatchFailReason = null;

		if (!File.Exists(this.RealPath)) return Fail(FailReason.FileNotFound);

		ReadHeader();
		if (_readFailReason is { } readFailReason) return Fail(readFailReason);
		if (ArchiveVersion is not (1 or 7 or 8)) return Fail(FailReason.UnknownVersion);

		var alreadyPatched = version == 1 ? ArchiveVersion == 1 : ArchiveVersion is 7 or 8;
		if (alreadyPatched)
		{
			PatchSuccessful = true;
			return PatchOutcome.AlreadyPatched;
		}

		try
		{
			WriteVersion(this.RealPath, version);

			// Vortex: if deployment copied the file instead of hard-linking it, the game-folder copy is separate data and must match.
			var deployed = Origin?.DeployedPath;
			if (deployed != null && File.Exists(deployed) && ReadVersionByte(deployed) != version)
				WriteVersion(deployed, version);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return Fail(Classify(e, FailReason.WriteFailed));
		}

		SetArchiveVersion(version);
		PatchSuccessful = true;
		return PatchOutcome.Patched;
	}

	private PatchOutcome Fail(FailReason reason)
	{
		PatchSuccessful = false;
		PatchFailReason = reason;
		return PatchOutcome.Failed;
	}
}
