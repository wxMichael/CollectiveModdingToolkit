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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using CollectiveModdingToolkit.Managers;
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Models.FileTypes;

namespace CollectiveModdingToolkit.Services;

public sealed record ScanOptions(
	bool OverviewIssues,
	bool WrongFormat,
	bool LoosePrevis,
	bool JunkFiles,
	bool ProblemOverrides,
	bool RaceSubgraphs)
{
	/// <summary>True if any option needs the Data folder walked. Overview Issues and Race Subgraphs don't.</summary>
	public bool NeedsFileWalk => WrongFormat || LoosePrevis || JunkFiles || ProblemOverrides;

	public bool Any => OverviewIssues || NeedsFileWalk || RaceSubgraphs;

	public static ScanOptions FromSettings(AppSettings s) =>
		new(
			s.ScanOverviewIssues, s.ScanWrongFormat, s.ScanLoosePrevis, s.ScanJunkFiles, s.ScanProblemOverrides,
			s.ScanRaceSubgraphs);
}

public static class Scanner
{
	private const int RaceSubgraphThreshold = 100;

	public const string InfoRaceSubgraphs =
		"Counts race animation subgraph records (RACE \\ SADD).\n"
		+ "Depending on your PC, adding too many of these may result in stutter when loading Cells.\n"
		+ "This issue needs more investigation as this may be mere correlation and not causation.";

	private const string SummaryPrevis =
		"Loose previs files should be archived so they only win conflicts according to their plugin's load order.\n"
		+ "Loose previs files are also not supported by PJM's Previs Scripts.";

	/// <summary>Folders that are never scanned. Lowercase.</summary>
	private static readonly string[] IgnoreFolders = ["bodyslide", "fo4edit", "robco_patcher", "source"];

	/// <summary>
	///     Top-level Data folders that are scanned, with the file extensions allowed in them (null means any). Lowercase,
	///     no dot.
	/// </summary>
	private static readonly Dictionary<string, HashSet<string>?> DataWhitelist = new()
	{
		["f4se"] = null,
		["materials"] = ["bgem", "bgsm", "txt"],
		["meshes"] =
		[
			"bto", "btr", "hko", "hkx", "hkx_back", "hkx_backup", "lst", "max", "nif", "obj", "sclp", "ssf", "tri",
			"txt", "xml",
		],
		["music"] = ["wav", "xwm"],
		["textures"] = ["dds"],
		["scripts"] = ["pex", "psc", "txt", "zip"],
		["sound"] = ["cdf", "fuz", "hkx", "lip", "wav", "xwm"],
		["vis"] = ["uvd"],
	};

	private static readonly HashSet<string> JunkFiles =
		[with(StringComparer.OrdinalIgnoreCase), "thumbs.db", "desktop.ini", ".ds_store"];

	private static readonly string[] JunkFileSuffixes = [".tmp", ".bak"];

	/// <summary>Formats that are often used by mistake, and what the game expects instead.</summary>
	private static readonly Dictionary<string, string[]> ProperFormats = new()
	{
		["bmp"] = ["dds"],
		["jpeg"] = ["dds"],
		["jpg"] = ["dds"],
		["png"] = ["dds"],
		["psd"] = ["dds"],
		["tga"] = ["dds"],
		["mp3"] = ["wav", "xwm"],
	};

	// TODO: Check if these have been removed from INIs.
	/// <summary>Archives from the game or Creation Kit, which never need a plugin.</summary>
	private static readonly HashSet<string> ArchiveNameWhitelist =
	[
		with(StringComparer.OrdinalIgnoreCase),
		"creationkit - shaders.ba2", "creationkit - textures.ba2",
		"fallout4 - animations.ba2", "fallout4 - interface.ba2", "fallout4 - materials.ba2",
		"fallout4 - meshes.ba2",
		"fallout4 - meshesextra.ba2", "fallout4 - misc.ba2", "fallout4 - nvflex.ba2", "fallout4 - shaders.ba2",
		"fallout4 - sounds.ba2", "fallout4 - startup.ba2", "fallout4 - texturespatch.ba2", "fallout4 - voices.ba2",
		.. Enumerable.Range(1, 9).Select(i => $"fallout4 - textures{i}.ba2"),
		.. Enumerable.Range(1, 16).Select(i => $"dlcultrahighresolution - textures{i:00}.ba2"),
	];

	private static string SolutionText(SolutionType type)
	{
		return type switch
		{
			SolutionType.ArchiveFolder => "These folders should be packed into BA2 archives.",
			SolutionType.DeleteOrIgnoreFile or SolutionType.DeleteOrIgnoreFolder =>
				"It can either be deleted or ignored.",
			SolutionType.ConvertDeleteOrIgnoreFile =>
				"This file may need to be converted and relevant files updated for the new name.\nOtherwise it can likely be deleted or ignored.",
			SolutionType.RenameArchive =>
				"Archives must be named the same as a plugin with an added suffix or added to an INI.",
			SolutionType.DownloadMod => "Download the mod here:",
			SolutionType.VerifyFiles =>
				"Verify files with Steam or reinstall the game.\nIf you downgraded the game you will need to do so again afterward.",
			SolutionType.UnknownFormat => "If this file type is expected here, please report it.",
			_ => string.Empty,
		};
	}

	public static List<ProblemInfo> Scan(Game game, ScanOptions options, IProgress<Progress>? progress = null,
		CancellationToken token = default)
	{
		List<ProblemInfo> problems = [];

		if (options.OverviewIssues) problems.AddRange(game.Problems);

		try
		{
			ScanFiles(game, options, problems, progress, token);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception e)
		{
			Log.Error("Scanner: scan failed", e);
			problems.Add(new ProblemInfo(
				ProblemType.ScanError,
				Severity.Critical,
				$"The scan failed and results may be incomplete.\n{e.GetType().Name}: {e.Message}",
				solution: "Please report this error.") { Title = "Scanner" });
		}

		return problems;
	}

	private static void ScanFiles(Game game, ScanOptions options, List<ProblemInfo> problems,
		IProgress<Progress>? progress, CancellationToken token)
	{
		if (options.RaceSubgraphs)
		{
			progress?.Report(new Progress("Counting race subgraph records...", 1));
			ScanRaceSubgraphs(game, problems, token);
		}

		if (!options.NeedsFileWalk) return;

		if (!Directory.Exists(game.DataPath))
		{
			problems.Add(new ProblemInfo(
				ProblemType.FileNotFound,
				Severity.Critical,
				"The game's Data folder was not found, so its files can't be scanned.",
				game.DataPath,
				game.GamePath,
				solution: SolutionText(SolutionType.VerifyFiles),
				solutionType: SolutionType.VerifyFiles));
			return;
		}

		var manager = game.Manager;
		var skipDirectories = new HashSet<string>([.. IgnoreFolders, .. manager.SkipDirectories],
			StringComparer.OrdinalIgnoreCase);
		string[] skipSuffixes = [.. manager.SkipFileSuffixes];

		progress?.Report(new Progress("Building mod file index...", 1));
		var files = BuildIndex(game, manager, skipDirectories, skipSuffixes, token);
		string[] topFolders =
		[
			.. files.Values
				.Select(e => e.Relative.Split('\\'))
				.Where(p => p.Length > 1)
				.Select(p => p[0])
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Order(StringComparer.OrdinalIgnoreCase),
		];

		var archivesEnabled = new HashSet<string>(game.ArchivesEnabled, StringComparer.OrdinalIgnoreCase);
		var reportedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var currentTop = string.Empty;

		foreach (var entry in files.Values.OrderBy(e => e.Relative, StringComparer.OrdinalIgnoreCase))
		{
			token.ThrowIfCancellationRequested();
			var parts = entry.Relative.Split('\\');
			var fileName = parts[^1];
			var dirs = parts[..^1];
			var root = dirs.Length > 0 ? dirs[0].ToLowerInvariant() : null;

			if (root != null && !root.Equals(currentTop, StringComparison.Ordinal))
			{
				currentTop = root;
				var index = Array.FindIndex(topFolders, f => f.Equals(root, StringComparison.OrdinalIgnoreCase)) + 1;
				progress?.Report(new Progress($"Scanning... {index}/{topFolders.Length}: {dirs[0]}",
					100.0 * index / Math.Max(1, topFolders.Length)));
			}

			HashSet<string>? whitelist = null;
			if (root != null)
			{
				if (root == "fomod")
				{
					if (options.JunkFiles && reportedFolders.Add("fomod"))
						AddFolderProblem(problems, game, manager, entry, 1, ProblemType.JunkFile, Severity.Low,
							"This is a junk folder not used by the game or mod managers.",
							SolutionType.DeleteOrIgnoreFolder);

					continue;
				}

				if (!DataWhitelist.TryGetValue(root, out whitelist)) continue;

				switch (root)
				{
					case "vis" when options.LoosePrevis:
					{
						if (reportedFolders.Add("vis"))
							AddFolderProblem(problems, game, manager, entry, 1, ProblemType.LoosePrevis,
								Severity.Medium,
								SummaryPrevis, SolutionType.ArchiveFolder);

						continue;
					}
					case "meshes"
						when IsInProblemFolder(options, game, manager, problems, reportedFolders, entry, dirs):
						continue;
				}
			}

			if (options.JunkFiles && (JunkFiles.Contains(fileName) ||
			                          JunkFileSuffixes.Any(s =>
				                          fileName.EndsWith(s, StringComparison.OrdinalIgnoreCase))))
			{
				AddFileProblem(problems, game, manager, entry, ProblemType.JunkFile, Severity.Low,
					"This is a junk file not used by the game or mod managers.", SolutionType.DeleteOrIgnoreFile);
				continue;
			}

			// Only scripts directly in Data\Scripts.
			if (root == "scripts" && dirs.Length == 1 && options.ProblemOverrides)
				if (F4SEScripts.TryGetHash(game.GameVersion, fileName, out var f4SEScriptCRC))
				{
					var mod = GetMod(manager, entry);

					var scriptCRC = Utilities.GetFileHash(entry.FullPath);
					if (!scriptCRC.Equals(f4SEScriptCRC, StringComparison.OrdinalIgnoreCase))
						AddFileProblem(problems, game, manager, entry, ProblemType.F4SEOverride, Severity.Critical,
							"This is an override of an F4SE script which does not match your game's version.",
							null,
							mod == null || mod.Contains("Fallout 4 Script Extender")
								? $"Install F4SE for {game.GameVersion}."
								: "Check if this mod is supposed to override F4SE Scripts.\n"
								  + "If this is a script extender/library or requires one, this is likely intentional but it must support your game version explicitly.\n"
								  + "Otherwise, this mod or file may need to be deleted.");

					else if (mod != null && !mod.Contains("Fallout 4 Script Extender"))
						AddFileProblem(problems, game, manager, entry, ProblemType.F4SEOverride, Severity.Low,
							"This is an override of an F4SE script. It matches your game's version so won't "
							+ "cause issues now, but if this is unintentionally bundled with a mod it may cause "
							+ "issues if your game or F4SE version ever changes.",
							null,
							"If this file isn't expected in this mod, inform the author of the potential mistake");


					continue;
				}

			var dot = fileName.LastIndexOf('.');
			if (dot < 0) continue;

			var ext = fileName[(dot + 1)..].ToLowerInvariant();
			var stem = fileName[..dot];

			if (!options.WrongFormat) continue;

			var misplacedDll = ext == "dll" &&
			                   !entry.Relative.StartsWith(@"f4se\plugins", StringComparison.OrdinalIgnoreCase);
			if ((whitelist != null && !whitelist.Contains(ext)) || misplacedDll)
			{
				var location = root ?? "Data";
				string summary;
				SolutionType solution;
				if (ProperFormats.TryGetValue(ext, out var proper))
				{
					var folder = dirs.Length > 0 ? string.Join('\\', dirs) + '\\' : string.Empty;
					string[] found =
					[
						.. proper.Where(e => files.ContainsKey($"{folder}{stem}.{e}"))
							.Select(e => $"{stem}.{e}"),
					];
					if (found.Length > 0)
					{
						summary =
							$"Format not in whitelist for {location}.\nA file with the expected format was found ({string.Join(", ", found)}).";
						solution = SolutionType.DeleteOrIgnoreFile;
					}
					else
					{
						summary =
							$"Format not in whitelist for {location}.\nA file with the expected format was NOT found ({string.Join(", ", proper)}).";
						solution = SolutionType.ConvertDeleteOrIgnoreFile;
					}
				}
				else
				{
					summary =
						$"Format not in whitelist for {location}.\nUnable to determine whether the game will use this file.";
					solution = SolutionType.UnknownFormat;
				}

				AddFileProblem(problems, game, manager, entry, ProblemType.UnexpectedFormat, Severity.Medium, summary,
					solution);
				continue;
			}

			if (ext == "ba2" && !ArchiveNameWhitelist.Contains(fileName) && !archivesEnabled.Contains(fileName))
			{
				var split = stem.LastIndexOf(" - ", StringComparison.Ordinal);
				var suffix = split < 0 ? null : stem[(split + 3)..].ToLowerInvariant();
				if (suffix == null || !game.BA2Suffixes.Contains(suffix))
				{
					var baseName = split < 0 ? stem : stem[..split];
					AddFileProblem(problems, game, manager, entry, ProblemType.InvalidArchiveName, Severity.Medium,
						"This is not a valid archive name and won't be loaded by the game.",
						SolutionType.RenameArchive,
						$"{SolutionText(SolutionType.RenameArchive)}\n\nValid Suffixes: {string.Join(", ", game.BA2Suffixes)}\nExample: {baseName} - Main.ba2");
				}
			}
		}
	}

	/// <summary>
	///     Reports a loose precombined or AnimTextData folder under Meshes, once. True if the file is inside one and
	///     shouldn't be checked further.
	/// </summary>
	private static bool IsInProblemFolder(
		ScanOptions options, Game game, IModManager manager, List<ProblemInfo> problems, HashSet<string> reported,
		Entry entry, string[] dirs)
	{
		for (var i = 1; i < dirs.Length; i++)
		{
			var name = dirs[i];
			ProblemType type;
			string summary;
			Severity severity;
			if (options.LoosePrevis && name.Equals("precombined", StringComparison.OrdinalIgnoreCase))
			{
				type = ProblemType.LoosePrevis;
				summary = SummaryPrevis;
				severity = Severity.Medium;
			}
			else if (options.ProblemOverrides && name.Equals("animtextdata", StringComparison.OrdinalIgnoreCase))
			{
				type = ProblemType.AnimTextDataFolder;
				summary = "The existence of unpacked AnimTextData may cause the game to crash.";
				severity = Severity.Critical;
			}
			else
			{
				continue;
			}

			// Reported once per mod, as each mod has its own folder.
			if (reported.Add($"{entry.Root}|{string.Join('\\', dirs[..(i + 1)])}"))
				AddFolderProblem(problems, game, manager, entry, i + 1, type, severity, summary,
					SolutionType.ArchiveFolder);

			return true;
		}

		return false;
	}

	/// <summary>
	///     Merges the Data folder and every mod's files, later layers winning.
	/// </summary>
	private static Dictionary<string, Entry> BuildIndex(Game game, IModManager manager, HashSet<string> skipDirectories,
		string[] skipSuffixes, CancellationToken token)
	{
		Dictionary<string, Entry> files = new(StringComparer.OrdinalIgnoreCase);

		// The Data folder is the lowest layer. Under Vortex it holds hard links to mod files; under MO2 it only holds game files
		// (or everything, if this app is running inside MO2's virtual file system, which the mod layers below then agree with).
		Walk(game.DataPath, null);

		// MO2: StagePaths is lowest priority first with Overwrite last.
		if (manager is not ModOrganizer) return files;
		foreach (var stage in manager.StagePaths)
			Walk(stage, Path.GetFileName(stage.TrimEnd('\\', '/')));

		return files;

		void Walk(string root, string? mod)
		{
			if (!Directory.Exists(root)) return;

			Stack<string> pending = new();
			pending.Push(root);
			while (pending.Count > 0)
			{
				token.ThrowIfCancellationRequested();
				var folder = pending.Pop();
				IEnumerable<string> children;
				try
				{
					children = [.. Directory.EnumerateFileSystemEntries(folder)];
				}
				catch (Exception e) when (e is IOException or UnauthorizedAccessException)
				{
					Log.Warning($"Scanner: unable to read {folder}: {e.Message}");
					continue;
				}

				foreach (var child in children)
				{
					var name = Path.GetFileName(child);
					if (Directory.Exists(child))
					{
						if (!skipDirectories.Contains(name)) pending.Push(child);

						continue;
					}

					if (skipSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase))) continue;

					var relative = Path.GetRelativePath(root, child);
					files[relative] = new Entry(relative, child, mod, root);
				}
			}
		}
	}

	/// <summary>The mod a file comes from, or null if it's a game-folder file or the manager can't say.</summary>
	private static string? GetMod(IModManager manager, Entry entry)
	{
		if (entry.Mod != null) return entry.Mod;

		if (manager is ModOrganizer) return null;

		var origin = manager.Resolve(entry.FullPath);
		return origin is { IsGameFolder: false } ? origin.ModName ?? origin.DisplayName : null;
	}

	/// <summary>The path to show and open: the mod's real file, not the game-folder link.</summary>
	private static string GetSourcePath(IModManager manager, Entry entry) =>
		entry.Mod != null || manager is ModOrganizer
			? entry.FullPath
			: manager.Resolve(entry.FullPath)?.SourcePath ?? entry.FullPath;

	/// <summary>The folder the file's relative path starts from: the mod's folder, or Data.</summary>
	private static string GetBasePath(Game game, Entry entry, string sourcePath) =>
		sourcePath.EndsWith(entry.Relative, StringComparison.OrdinalIgnoreCase) &&
		sourcePath.Length > entry.Relative.Length
			? sourcePath[..^entry.Relative.Length].TrimEnd('\\', '/')
			: game.DataPath;

	private static void AddFileProblem(
		List<ProblemInfo> problems, Game game, IModManager manager, Entry entry,
		ProblemType type, Severity severity, string summary, SolutionType? solutionType, string? solution = null)
	{
		var path = GetSourcePath(manager, entry);
		var mod = GetMod(manager, entry);
		var basePath = GetBasePath(game, entry, path);
		problems.Add(new ProblemInfo(
			type, severity, summary, path, basePath, mod,
			solution ?? (solutionType is { } t ? SolutionText(t) : null),
			solutionType ?? SolutionType.None));
	}

	/// <summary>
	///     Reports a folder, <paramref name="depth" /> levels below Data, found as the parent of <paramref name="entry" />.
	/// </summary>
	private static void AddFolderProblem(
		List<ProblemInfo> problems, Game game, IModManager manager, Entry entry, int depth,
		ProblemType type, Severity severity, string summary, SolutionType solutionType)
	{
		var parts = entry.Relative.Split('\\');
		var sourceFile = GetSourcePath(manager, entry);

		// Walk up from the file by the number of levels below the folder to get the real folder.
		var folder = sourceFile;
		for (var i = parts.Length; i > depth; i--) folder = Path.GetDirectoryName(folder)!;

		var mod = GetMod(manager, entry);
		var basePath = GetBasePath(game, entry, sourceFile);
		problems.Add(new ProblemInfo(type, severity, summary, folder, basePath, mod, SolutionText(solutionType),
			solutionType));
	}

	private static void ScanRaceSubgraphs(Game game, List<ProblemInfo> problems, CancellationToken token)
	{
		// TODO: Determine whether overrides matter and ignore them if not.

		byte[] pattern = [0x00, 0x53, 0x41, 0x44, 0x44]; // .SADD
		List<(int Count, string Name, string? Mod)> modules = [];
		var total = 0;

		foreach (var name in game.ModulesEnabled)
		{
			token.ThrowIfCancellationRequested();
			if (!game.ModulesWinning.TryGetValue(name, out var module)) continue;

			int count;
			try
			{
				count = Count(module.RealPath, pattern, token);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				continue;
			}

			if (count <= 0) continue;
			modules.Add((count, name, module.Origin?.DisplayName));
			total += count;
		}

		if (total <= RaceSubgraphThreshold) return;

		Collection<string> list =
		[
			.. modules.OrderByDescending(m => m.Count).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
				.Select(m => $"{m.Count,5}   {m.Name}{(m.Mod == null ? string.Empty : $"  ({m.Mod})")}"),
		];

		problems.Add(new ProblemInfo(
			ProblemType.RaceSubgraphs,
			Severity.NoImpact,
			InfoRaceSubgraphs,
			solution:
			"IF you are experiencing stutter when moving between cells, removing some of these mods could alleviate performance issues.\nMerging them may also reduce stutter.",
			fileList: list) { Title = $"{total} SADD Records from {modules.Count} modules" });
	}

	/// <summary>Counts non-overlapping occurrences of <paramref name="pattern" /> in a file, without loading it all.</summary>
	private static int Count(string path, byte[] pattern, CancellationToken token)
	{
		using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920);
		var buffer = new byte[1 << 20];
		var keep = pattern.Length - 1;
		var count = 0;
		var filled = 0;
		int read;
		while ((read = stream.Read(buffer, filled, buffer.Length - filled)) > 0)
		{
			token.ThrowIfCancellationRequested();
			var length = filled + read;
			ReadOnlySpan<byte> span = buffer.AsSpan(0, length);
			var start = 0;
			int index;
			while ((index = span[start..].IndexOf(pattern)) >= 0)
			{
				count++;
				start += index + pattern.Length;
			}

			// Carry over the tail that could be the start of a match split across reads. Skip bytes already counted.
			var tail = Math.Min(keep, length - start);
			Buffer.BlockCopy(buffer, length - tail, buffer, 0, tail);
			filled = tail;
		}

		return count;
	}

	/// <summary>One file as the game would see it: the winning copy across the Data folder and every mod.</summary>
	private sealed record Entry(string Relative, string FullPath, string? Mod, string Root);

	public readonly record struct Progress(string Text, double Percent);
}
