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
using System.Globalization;
using System.IO;
using System.Linq;
using CollectiveModdingToolkit.Managers;
using CollectiveModdingToolkit.Models.FileTypes;
using CollectiveModdingToolkit.Services;

namespace CollectiveModdingToolkit.Models;

// Everything the Overview can tell the user is wrong. Counts and tables are in Game.cs; this turns them into problems.
public partial class Game
{
	private const string UrlAddressLibrary = "https://www.nexusmods.com/fallout4/mods/47327";
	private const string UrlEslGuide = "https://themidnightride.moddinglinked.com/esl.html";
	private const string UrlF4SE = "https://www.nexusmods.com/fallout4/mods/42147";

	private const string SolutionVerify = "Verify the game files in Steam (or reinstall) to restore it.";

	/// <summary>Games and the module (HEDR) versions they accept. A module with another version is for a different game.</summary>
	private static readonly Dictionary<string, string[]> ModuleVersionSupport = new()
	{
		["Morrowind"] = ["1.2", "1.3"],
		["Oblivion"] = ["0.8", "1.0"],
		["Skyrim LE"] = ["0.94", "1.70"],
		["Skyrim VR"] = ["0.94", "1.70"],
		["Skyrim SE"] = ["0.94", "1.70", "1.71"],
		["Fallout 3"] = ["0.85", "0.94"],
		["Fallout 4 VR"] = ["0.95"],
		["Fallout 4"] = ["0.95", "1.00"],
		["Fallout NV"] = ["0.94", "1.32", "1.33", "1.34"],
		["Fallout 76"] = ["68.0", "216.0", "223.0"],
		["Starfield"] = ["0.96"],
	};

	/// <summary>Things found while gathering Overview info, most serious first.</summary>
	public ObservableCollection<ProblemInfo> Problems { get; } = [];

	public bool HasProblems => Problems.Count > 0;

	/// <summary>Address Library's version file for the installed game, or null if the game's version couldn't be determined.</summary>
	public string? AddressLibraryPath { get; private set; }

	public bool AddressLibraryFound { get; private set; }
	public string AddressLibraryText => AddressLibraryFound ? "Installed" : "Not Found";
	public StatusLevel AddressLibraryStatus => AddressLibraryFound ? StatusLevel.Good : StatusLevel.Bad;

	/// <summary>Only shown when it's missing.</summary>
	public string? AddressLibraryTip => AddressLibraryFound ? null : "Address Library is required for many F4SE mods.";

	private void AddProblem(
		ProblemType type,
		Severity severity,
		string summary,
		string? path = null,
		string? mod = null,
		string? solution = null,
		SolutionType solutionType = SolutionType.None,
		string? url = null,
		Collection<string>? fileList = null)
	{
		Log.Info($"Problem: {ProblemInfo.Describe(type)}: {summary}{(path == null ? string.Empty : $" ({path})")}");
		Problems.Add(new ProblemInfo(type, severity, summary, path, GamePath, mod, solution, solutionType, url,
			fileList));
	}

	/// <summary>True if the file exists in the game folder as the game (and the mod manager) sees it.</summary>
	private bool FileExistsInGame(string path) => Manager.Resolve(path) != null || File.Exists(path);

	/// <summary>Turns what was gathered into problems. Each check is independent, so one failing doesn't hide the others.</summary>
	private void FindProblems()
	{
		foreach (var (name, check) in new (string, Action)[]
		         {
			         ("mod manager", CheckModManager), ("game version", CheckGameVersion),
			         ("binaries", CheckBinaries), ("address library", CheckAddressLibrary),
			         ("archives", CheckArchives), ("modules", CheckModules),
		         })
			try
			{
				check();
			}
			catch (Exception e)
			{
				Log.Error($"Checking {name} failed", e);
			}

		// Most serious first, keeping the order found within each severity.
		List<ProblemInfo> sorted = [.. Problems.OrderByDescending(p => p.Severity)];
		Problems.Clear();
		foreach (var problem in sorted) Problems.Add(problem);
	}

	private void CheckModManager()
	{
		if (Manager is not Unmanaged) return;

		AddProblem(
			ProblemType.NoModManager,
			Severity.Low,
			"No mod manager was detected, so the app can't tell which mod a file comes from.",
			solution: "Add this app as an executable in Mod Organizer 2 or Vortex, and launch it from there.");
	}

	private void CheckGameVersion()
	{
		if (GameVersion != Models.GameVersion.Unknown) return;

		var exe = Binaries.FirstOrDefault(b => b.Name == Binary.MainExeName);
		AddProblem(
			ProblemType.UnknownGameVersion,
			Severity.Medium,
			$"{exe?.Version ?? "This version"} of {Binary.MainExeName} is not a known version. "
			+ "Possible causes: the game is an old version and should be updated, the file may be corrupted, "
			+ "or the game is newer than this app.",
			exe?.RealPath,
			exe?.Mod,
			"Update the game or verify its files in Steam. If neither helps, report this version.",
			SolutionType.VerifyFiles);
	}

	private void CheckBinaries()
	{
		foreach (var binary in Binaries)
		{
			if (binary.IsGood || binary.Name == Binary.MainExeName) continue;

			var optionalTool = BinariesCK.Contains(binary);
			var isF4SE = binary.Name.StartsWith("f4se_", StringComparison.OrdinalIgnoreCase);
			var isBaseGameFile = !isF4SE;

			if (binary.GameVersion == Models.GameVersion.NotFound)
			{
				if (optionalTool) continue;

				if (isF4SE)
					AddProblem(ProblemType.FileNotFound, Severity.Medium,
						$"{binary.Name} was not found, so F4SE is not installed.", binary.PathFull,
						solution: $"Install F4SE for {GameVersion}.", url: UrlF4SE);
				else
					AddProblem(ProblemType.FileNotFound, Severity.Critical,
						$"{binary.Name} is a base game file but was not found.", binary.PathFull,
						solution: SolutionVerify, solutionType: SolutionType.VerifyFiles);

				continue;
			}

			string what;
			var supportedByDowngrader = false;
			switch (binary.GameVersion)
			{
				case Models.GameVersion.Unknown:
					what = "an unrecognized version";
					break;

				case Models.GameVersion.Obsolete:
					what = "an outdated version";
					break;

				default:
					what = $"for {binary.GameVersion}";
					supportedByDowngrader = isBaseGameFile;
					break;
			}

			AddProblem(
				ProblemType.WrongVersion,
				optionalTool ? Severity.Low : Severity.Medium,
				$"{binary.Name} {binary.Version} is {what}, but your game is {GameVersion}.",
				binary.Origin?.SourcePath ?? binary.PathFull,
				binary.Mod,
				isF4SE
					? $"Install the F4SE version for {GameVersion} or use the Downgrader."
					: supportedByDowngrader
						? "Use the Downgrader to switch versions."
						: "Update your game or verify its files.",
				url: isF4SE ? UrlF4SE : null);
		}
	}

	private void CheckAddressLibrary()
	{
		AddressLibraryFound = false;
		AddressLibraryPath = null;
		if (!Directory.Exists(DataPath)) return;

		// Address Library ships one file per game version, e.g. version-1-11-240-0.bin
		var exe = Binaries.FirstOrDefault(b => b.Name == Binary.MainExeName);
		if (exe?.Version == null || !Version.TryParse(exe.Version, out var version)) return;

		var fileName =
			$"version-{version.Major}-{version.Minor}-{version.Build}-{Math.Max(version.Revision, 0)}.bin";
		AddressLibraryPath = Path.Combine(F4SEPath, fileName);
		AddressLibraryFound = FileExistsInGame(AddressLibraryPath);
		if (AddressLibraryFound) return;

		AddProblem(
			ProblemType.FileNotFound,
			Severity.Medium,
			$"Address Library ({fileName}) was not found. It is required by many F4SE mods and by playing a downgraded game.",
			AddressLibraryPath,
			solution: "Download and install Address Library for F4SE Plugins.",
			solutionType: SolutionType.DownloadMod,
			url: UrlAddressLibrary);
	}

	private void CheckArchives()
	{
		if (ArchivesGeneral.Count > MaxArchivesGeneral)
			AddProblem(ProblemType.LimitExceeded, Severity.Critical,
				$"You have {ArchivesGeneral.Count} General archives. The limit is {MaxArchivesGeneral}.",
				solution: "Reduce the number of archives by unpacking or merging them.");

		if (ArchivesTexture.Count > MaxArchivesTexture)
			AddProblem(ProblemType.LimitExceeded, Severity.Critical,
				$"You have {ArchivesTexture.Count} Texture archives. The limit is {MaxArchivesTexture}.",
				solution: "Reduce the number of archives by unpacking or merging them.");

		foreach (var name in ArchivesUnreadable)
		{
			if (!ArchivesWinning.TryGetValue(name, out var archive)) continue;

			AddProblem(ProblemType.InvalidArchive, Severity.Critical,
				"This archive could not be read. The cause is a permissions problem, an unexpected format, or corruption.",
				archive.Origin?.SourcePath ?? archive.PathFull, archive.Mod,
				"Re-download or reinstall the mod it belongs to.");
		}

		foreach (var name in ArchivesUnknown)
		{
			if (!ArchivesWinning.TryGetValue(name, out var archive)) continue;

			var detail = archive.ArchiveVersion is 1 or 7 or 8
				? $"format {archive.ArchiveFormat ?? "unknown"}"
				: $"version {archive.ArchiveVersion}";
			AddProblem(ProblemType.InvalidArchive, Severity.Medium,
				$"This archive has an unsupported {detail}. Fallout 4 archives are General or Texture, version 1, 7, or 8.",
				archive.Origin?.SourcePath ?? archive.PathFull, archive.Mod,
				"It may be for another game, or corrupted. Re-download the mod it belongs to.");
		}
	}

	private void CheckModules()
	{
		if (ModulesFull.Count > MaxModulesFull)
			AddProblem(ProblemType.LimitExceeded, Severity.Critical,
				$"You have {ModulesFull.Count} Full modules enabled. The limit is {MaxModulesFull}.",
				solution: "Convert modules to Light (ESL) where possible, or remove some.",
				url: UrlEslGuide);

		if (ModulesLight.Count > MaxModulesLight)
			AddProblem(ProblemType.LimitExceeded, Severity.Critical,
				$"You have {ModulesLight.Count} Light modules enabled. The limit is {MaxModulesLight}.",
				solution: "Remove or merge modules. Merging with zMerge is risky and can break mods.");

		foreach (var name in ModulesUnreadable)
		{
			if (!ModulesWinning.TryGetValue(name, out var module)) continue;

			AddProblem(ProblemType.InvalidModule, Severity.Critical,
				"This module could not be read. The cause is a permissions problem, an unexpected format, or corruption.",
				module.Origin?.SourcePath ?? module.PathFull, module.Mod,
				"Re-download or reinstall the mod it belongs to.");
		}

		foreach (var name in ModulesUnknown)
		{
			if (!ModulesWinning.TryGetValue(name, out var module) || !module.WasRead ||
			    module.IsReadable != true) continue;

			var version = module.HEDRVersion.ToString("0.0#", CultureInfo.InvariantCulture);
			string[] games =
			[
				.. ModuleVersionSupport
					.Where(g => g.Value.Any(v =>
						Math.Abs(double.Parse(v, CultureInfo.InvariantCulture) - module.HEDRVersion) < 0.005))
					.Select(g => g.Key),
			];
			var forGames = games.Length > 0
				? $" Games that use this version: {string.Join(", ", games)}."
				: string.Empty;

			AddProblem(ProblemType.InvalidModule, Severity.Critical,
				$"The module version ({version}) is not valid for Fallout 4. Fallout 4 uses 0.95 and 1.00.{forGames}",
				module.Origin?.SourcePath ?? module.PathFull, module.Mod,
				"Open the module in the Creation Kit and save it, or replace it with a version made for Fallout 4. Files with 0.94 were likely made with FalloutSnip.");
		}
	}
}
