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
using System.Text;
using Avalonia.Collections;
using CollectiveModdingToolkit.Controls;
using CollectiveModdingToolkit.Managers;
using CollectiveModdingToolkit.Models.FileTypes;
using CollectiveModdingToolkit.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using IniParser;
using IniParser.Exceptions;
using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Microsoft.Win32;

namespace CollectiveModdingToolkit.Models;

public partial class Game : ObservableObject
{
	private static readonly string[] DefaultBA2Suffixes = ["main", "textures", "voices_en"];

	private static readonly IniParserConfiguration IniParserConfig = new()
	{
		AllowCreateSectionsOnFly = true,
		AllowDuplicateKeys = true,
		AllowDuplicateSections = true,
		AllowKeysWithoutSection = true,
		AssigmentSpacer = string.Empty,
		CaseInsensitive = true,
		SkipInvalidLines = true,
	};

	private static readonly string[] ModuleExtensions = [".esm", ".esl", ".esp"];

	private static readonly string[] GameMasters =
	[
		"Fallout4.esm",
		"Fallout4_VR.esm",
		"DLCRobot.esm",
		"DLCworkshop01.esm",
		"DLCworkshop02.esm",
		"DLCworkshop03.esm",
		"DLCCoast.esm",
		"DLCNukaWorld.esm",
		"DLCUltraHighResolution.esm",
	];

	public Game(IModManager manager, string name)
	{
		Manager = manager;
		Name = name;
		Log.Debug($"Game: loading {name} via {manager.AppName}");

		BA2Suffixes = [.. DefaultBA2Suffixes]; // TODO: Remove
		var gamePath = FindGamePath(manager);
		if (gamePath == null)
		{
			ErrorReporter.Current.Critical(typeof(DirectoryNotFoundException), "Fallout 4 install folder.");
			return;
		}

		Log.Debug($"Game: install folder is {gamePath}");
		GamePath = gamePath;
		DataPath = Path.Combine(GamePath, "Data");
		F4SEPath = Path.Combine(DataPath, "F4SE", "Plugins");

		if (!Directory.Exists(GamePath))
		{
			ErrorReporter.Current.Critical(typeof(DirectoryNotFoundException), GamePath);
			return;
		}

		var mainExePath = Path.Combine(GamePath, Binary.MainExeName);
		if (!File.Exists(mainExePath))
		{
			ErrorReporter.Current.Critical(typeof(FileNotFoundException), mainExePath);
			return;
		}

		// From here on, problems are recorded and the app carries on with whatever it can still inspect.
		var hasData = Directory.Exists(DataPath);
		if (!hasData)
			AddProblem(ProblemType.FileNotFound, Severity.Critical,
				"The game's Data folder was not found, so plugins, archives and F4SE plugins can't be checked.",
				DataPath, solution: SolutionVerify, solutionType: SolutionType.VerifyFiles);

		if (Directory.Exists(manager.GameIniFolderPath))
			ReadGameINIs(manager.GameIniFolderPath);
		else
			AddProblem(ProblemType.Configuration, Severity.Medium,
				"The folder with the game's INI files was not found, so archive lists can't be read and archive counts will be inaccurate.",
				manager.GameIniFolderPath, solution: "Run the Fallout 4 Launcher once to create the INI files.");

		Log.Debug("Game: reading binaries");
		GetBinaryInfo();

		if (hasData)
		{
			Log.Debug("Game: listing modules");
			GetModuleLists(manager);
			Log.Debug("Game: listing archives");
			GetArchiveLists();

			Log.Debug("Game: reading module headers");
			GetModuleInfo();
			Log.Debug("Game: reading archive headers");
			GetArchiveInfo();

			Log.Debug("Game: listing DLLs");
			GetDllLists();
			Log.Debug("Game: reading DLL info");
			GetDllInfo();
		}

		Dlls = [.. DllsWinning.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];
		if (!hasData)
			F4SEMessage = "The game's Data folder was not found.";
		else if (F4SEMessage == null && Dlls.Count == 0)
			F4SEMessage = "No DLLs were found in Data/F4SE/Plugins.";

		Log.Debug("Game: checking for problems");
		FindProblems();
		Log.Info(
			$"Game: {GameVersion} at {GamePath} via {manager.AppName}; {ModulesEnabled.Count} modules, {ArchivesEnabled.Count} archives, {Dlls.Count} DLLs, {Problems.Count} problems.");

		//GameVersion = string.Empty;
		//Source = string.Empty;
	}

	/// <summary>The game chosen at app start (see <c>GameSession.GameName</c>).</summary>
	public string Name { get; }

	public IModManager Manager { get; }

	[ObservableProperty] public partial string GamePath { get; set; } = string.Empty;
	[ObservableProperty] public partial string GameVersion { get; set; } = string.Empty;
	[ObservableProperty] public partial string Source { get; set; } = string.Empty;

	public ObservableCollection<DLL> Dlls { get; set; } = [];
	public ObservableCollection<Binary> Binaries { get; set; } = [];
	public ObservableCollection<Binary> BinariesGame { get; set; } = [];
	public ObservableCollection<Binary> BinariesCK { get; set; } = [];

	/// <summary>Fallout4.exe's version packed the way F4SE packs it, or null if it couldn't be determined.</summary>
	internal uint? ExeVersion { get; private set; }

	/// <summary>Why the F4SE tab has nothing to show, or null if it has DLLs to list.</summary>
	public string? F4SEMessage { get; private set; }

	public string DataPath { get; } = string.Empty;
	public string F4SEPath { get; } = string.Empty;
	public string Language { get; private set; } = "en";

	public SortedSet<string> ArchivesINI { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> ArchivesEnabled { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The enabled archives in the order the game loads them. When two hold the same file, the later one wins.</summary>
	public List<string> ArchivesLoadOrder { get; } = [];

	/// <summary>The enabled plugins in load order.</summary>
	public List<string> ModulesLoadOrder { get; } = [];

	public SortedSet<string> ArchivesGeneral { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> ArchivesTexture { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> ArchivesOG { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> ArchivesNG { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> ArchivesUnreadable { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> ArchivesUnknown { get; } = new(StringComparer.OrdinalIgnoreCase);

	private List<Module> Modules { get; } = [];
	public Dictionary<string, Module> ModulesWinning { get; } = new(StringComparer.OrdinalIgnoreCase);
	private List<Archive> Archives { get; } = [];
	public AvaloniaDictionary<string, Archive> ArchivesWinning { get; } = new();

	// List<DLL> Dlls { get; }
	public Dictionary<string, DLL> DllsWinning { get; } = new(StringComparer.OrdinalIgnoreCase);

	public SortedSet<string> ModulesCCC { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> ModulesEnabled { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> Modules095 { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> Modules100 { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> ModulesFull { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> ModulesLight { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> ModulesUnreadable { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> ModulesUnknown { get; } = new(StringComparer.OrdinalIgnoreCase);
	public SortedSet<string> DLLsUnreadable { get; } = new(StringComparer.OrdinalIgnoreCase);

	// TODO: remove .DownGrade and just have IsDG?
	public bool IsOG => GameVersion is Models.GameVersion.DownGrade or Models.GameVersion.OldGen;
	public bool IsNG => GameVersion == Models.GameVersion.NextGen;
	public bool IsAE => GameVersion == Models.GameVersion.Anniversary;

	public int TotalArchives => ArchivesGeneral.Count + ArchivesTexture.Count;
	public int TotalModules => ModulesFull.Count + ModulesLight.Count;

	public StatusLevel ModulesFullStatus => Status.ForLimit(ModulesFull.Count, MaxModulesFull);
	public StatusLevel ModulesLightStatus => Status.ForLimit(ModulesLight.Count, MaxModulesLight);
	public StatusLevel TotalModulesStatus => Status.ForLimit(TotalModules, TotalModulesMax);

	public bool HasModulesUnreadable => ModulesUnreadable.Count > 0;
	public bool HasModulesUnknown => ModulesUnknown.Count > 0;

	public StatusLevel ArchivesGeneralStatus => Status.ForLimit(ArchivesGeneral.Count, MaxArchivesGeneral);
	public StatusLevel ArchivesTextureStatus => Status.ForLimit(ArchivesTexture.Count, MaxArchivesTexture);
	public StatusLevel TotalArchivesStatus => Status.ForLimit(TotalArchives, TotalArchivesMax);

	public bool HasArchivesUnreadable => ArchivesUnreadable.Count > 0;
	public bool HasArchivesUnknown => ArchivesUnknown.Count > 0;

	public SortedSet<string> BA2Suffixes { get; private set; }

	/// <summary>
	///     BA2 limits. 1.11.240 raised them; every earlier version (including older Anniversary releases, which are
	///     "Obsolete") keeps the original limits.
	/// </summary>
	public int MaxArchivesGeneral => IsAE ? 1024 : 256;

	public int MaxArchivesTexture => IsAE ? 1023 : 255;
	public int TotalArchivesMax => MaxArchivesGeneral + MaxArchivesTexture;
	public static int MaxModulesFull { get; } = 254;
	public static int MaxModulesLight { get; } = 4096;
	public static int TotalModulesMax { get; } = MaxModulesFull + MaxModulesLight;

	private IniData? Config { get; set; }
	private IniData? Prefs { get; set; }

	/// <summary>Whether a plugin (or archive base name) is one of the base game's masters.</summary>
	internal static bool IsBaseGameName(string name) =>
		GameMasters.Any(m => !m.StartsWith("DLC", StringComparison.OrdinalIgnoreCase) &&
		                     Path.GetFileNameWithoutExtension(m).Equals(Path.GetFileNameWithoutExtension(name),
			                     StringComparison.OrdinalIgnoreCase));

	/// <summary>Whether a plugin (or archive base name) is one of the game's DLC masters.</summary>
	internal static bool IsDLCName(string name) =>
		GameMasters.Any(m => m.StartsWith("DLC", StringComparison.OrdinalIgnoreCase) &&
		                     Path.GetFileNameWithoutExtension(m).Equals(Path.GetFileNameWithoutExtension(name),
			                     StringComparison.OrdinalIgnoreCase));

	/// <summary>Whether a plugin (or archive base name) is listed in Fallout4.ccc.</summary>
	internal bool IsCreationClubName(string name) =>
		ModulesCCC.Any(m => Path.GetFileNameWithoutExtension(m)
			.Equals(Path.GetFileNameWithoutExtension(name), StringComparison.OrdinalIgnoreCase));

	/// <summary>Keys are lower-cased file names, so lookups ignore case. Use <see cref="ArchiveKey" />.</summary>
	public static string ArchiveKey(string name) => name.ToLowerInvariant();

	// TODO: Detect steam library folders to find game
	// TODO: Prompt user to choose between all non-manager game install detections or manual entry
	// TODO: Game source
	private static string? FindGamePath(IModManager manager) =>
		manager.GamePath
		?? FindInstallPathFromRegistry();

	internal static string? FindInstallPathFromRegistry() =>
		GetRegistryInstallPath()
		?? GetRegistryInstallPathGOG();

	private static string? GetRegistryInstallPath()
	{
		try
		{
			using var key =
				Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Bethesda Softworks\Fallout4");
			return key?.GetValue("Installed Path")?.ToString();
		}
		catch
		{
			return null;
		}
		// TODO: Specific exceptions
	}

	private static string? GetRegistryInstallPathGOG()
	{
		try
		{
			using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games\1998527297");
			return key?.GetValue("path")?.ToString();
		}
		catch (Exception)
		{
			// TODO: Specific exceptions
			return null;
		}
	}

	private void ReadGameINIs(string path)
	{
		Config = null;
		Prefs = null;
		FileIniDataParser parser = new(new IniDataParser(IniParserConfig));
		var iniPath = Path.Combine(path, "Fallout4.ini");
		var iniCustomPath = Path.Combine(path, "Fallout4Custom.ini");
		var iniPrefsPath = Path.Combine(path, "Fallout4Prefs.ini");
		const string runLauncher = "Run the Fallout 4 Launcher or Bethini Pie to create it.";

		try
		{
			if (File.Exists(iniPath))
			{
				Config = parser.ReadFile(iniPath, Encoding.UTF8);
				if (File.Exists(iniCustomPath)) Config.Merge(parser.ReadFile(iniCustomPath));

				if (!Config.Sections.ContainsSection("Archive"))
					AddProblem(ProblemType.Configuration, Severity.Medium,
						"The [Archive] section is missing from your game INIs. Base game archives can't be detected, so archive counts will be inaccurate.",
						iniPath, solution: runLauncher);
			}
			else
			{
				AddProblem(ProblemType.FileNotFound, Severity.Medium,
					"Fallout4.ini was not found. Base game archives can't be detected, so archive counts will be inaccurate.",
					iniPath, solution: runLauncher);
			}

			if (File.Exists(iniPrefsPath))
				Prefs = parser.ReadFile(iniPrefsPath);
			else
				AddProblem(ProblemType.FileNotFound, Severity.Low,
					"Fallout4Prefs.ini was not found.",
					iniPrefsPath, solution: runLauncher);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or ParsingException)
		{
			Log.Error($"Unable to read the game INIs in {path}", e);
			AddProblem(ProblemType.Configuration, Severity.Medium,
				$"The game's INI files could not be read: {e.Message}",
				path,
				solution:
				"Check that the files aren't open in another program and that you have permission to read them.");
		}

		BA2Suffixes = [.. DefaultBA2Suffixes];
		var lang = Config?["General"]["sLanguage"];
		// TODO: Test if empty string is valid
		if (string.IsNullOrEmpty(lang))
		{
			lang = "en";
		}
		else
		{
			lang = lang.ToLowerInvariant();
			if (lang != "en") BA2Suffixes.Add($"voices_{lang}");
		}

		Language = lang;
	}

	private void GetBinaryInfo()
	{
		Binaries.Clear();
		BinariesGame.Clear();
		BinariesCK.Clear();
		foreach (var relativePath in Binary.BaseFiles.Keys)
		{
			var fullPath = Path.Combine(GamePath, relativePath);
			var fileName = relativePath.Contains(Path.DirectorySeparatorChar)
				? Path.GetFileName(relativePath)
				: relativePath;
			Binary binary = new(fileName, fullPath, relativePath, Manager.Resolve(fullPath));
			if (fileName.Equals(Binary.MainExeName, StringComparison.OrdinalIgnoreCase))
			{
				ExeVersion = F4SERuntime.Pack(binary.Version);
				GameVersion = binary.GameVersion;
				// An unknown version isn't fatal: it's reported, and the rest of the checks still run.
				binary.IsGood = GameVersion != Models.GameVersion.Unknown;
			}
			else
			{
				if (IsNG || IsAE) binary.ResolveSharedVersion(GameVersion);

				if (binary.GameVersion == Models.GameVersion.NotFound &&
				    !Binary.IsExpectedFor(relativePath, GameVersion))
					continue;

				binary.IsGood = binary.GameVersion == GameVersion;
			}

			Binaries.Add(binary);
			if (binary.Name.Contains("CreationKit", StringComparison.OrdinalIgnoreCase) ||
			    binary.Name.Contains("Archive2", StringComparison.OrdinalIgnoreCase))
				BinariesCK.Add(binary);
			else
				BinariesGame.Add(binary);
		}
	}

	private void GetModuleLists(IModManager manager)
	{
		ModulesEnabled.Clear();
		ModulesLoadOrder.Clear();

		foreach (var moduleName in GameMasters) EnableModule(moduleName);

		// Creation Club plugins listed in Fallout4.ccc are always enabled.
		ModulesCCC.Clear();
		var cccPath = Path.Combine(GamePath, "Fallout4.ccc");
		if (File.Exists(cccPath))
			// TODO: Encoding
			foreach (var line in File.ReadAllLines(cccPath, Encoding.UTF8))
			{
				var moduleName = line.Trim();
				if (moduleName.Length > 0 && EnableModule(moduleName)) ModulesCCC.Add(moduleName);
			}
		else
			AddProblem(ProblemType.FileNotFound, Severity.Medium,
				"Fallout4.ccc was not found. It lists the Creation Club plugins that are always enabled, so they can't be counted.",
				cccPath, solution: SolutionVerify, solutionType: SolutionType.VerifyFiles);

		if (File.Exists(manager.PluginsTxtPath))
		{
			// TODO: Encoding
			foreach (var line in File.ReadAllLines(manager.PluginsTxtPath, Encoding.UTF8))
				// Enabled plugins start with *. Disabled ones have no marker.
				// TODO: Store missing files
				// TODO: Check first line comment
				if (line.StartsWith('*'))
					EnableModule(line[1..].Trim());
		}
		else
		{
			AddProblem(ProblemType.FileNotFound, Severity.Medium,
				"plugins.txt was not found, so the app can't tell which plugins are enabled. Counts assume every plugin in the Data folder is enabled.",
				manager.PluginsTxtPath,
				solution: manager is Unmanaged
					? "Launch this app from your mod manager."
					: "Run the game from your mod manager once to create it.");

			foreach (var filePath in EnumerateDataFiles())
				if (ModuleExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase))
					EnableModule(Path.GetFileName(filePath));
		}

		return;

		// Adds the module (once) and marks it enabled. False if the file isn't there.
		bool EnableModule(string moduleName)
		{
			if (!ModulesWinning.TryGetValue(moduleName, out var module))
			{
				var modulePath = Path.Combine(DataPath, moduleName);
				if (!FileExistsInGame(modulePath)) return false;

				module = new Module(moduleName, modulePath, manager.Resolve(modulePath));
				Modules.Add(module);
				ModulesWinning[moduleName] = module;
			}

			module.IsEnabled = true;
			if (ModulesEnabled.Add(moduleName)) ModulesLoadOrder.Add(moduleName);
			return true;
		}
	}

	private void GetDllLists()
	{
		Dlls.Clear();
		List<string> pluginFiles = [.. EnumerateDataFiles(Path.Combine("F4SE", "Plugins"))];
		if (pluginFiles.Count == 0 && !Directory.Exists(F4SEPath))
		{
			// Under a mod manager the folder only exists if F4SE plugins are installed; with none, there's nothing to check.
			F4SEMessage = Manager is Unmanaged
				? "The Data/F4SE/Plugins folder was not found. If your F4SE plugins are in a mod manager, launch this app from it."
				: "The Data/F4SE/Plugins folder was not found, so no F4SE plugins are installed.";
			Log.Info(F4SEMessage);
			return;
		}

		foreach (var filePath in pluginFiles)
		{
			// TODO: Get mod name from meta.ini
			var fileName = Path.GetFileName(filePath);
			var fileExt = Path.GetExtension(fileName);
			if (fileExt == string.Empty) continue;

			var fileExtLower = fileExt.ToLowerInvariant();

			if (fileExtLower != ".dll") continue;

			// Microsoft debug interface access DLLs that some plugins ship with. They are never F4SE plugins.
			if (fileName.StartsWith("msdia", StringComparison.OrdinalIgnoreCase)) continue;

			DLL dll = new(fileName, filePath, Manager.Resolve(filePath));
			//Console.WriteLine(dll.PathRelative);
			Dlls.Add(dll);
			DllsWinning.TryAdd(fileName, dll);
			// TODO: Check if already present and add to DllsLosing
		}
	}

	private void GetDllInfo()
	{
		//DllsUnreadable.Clear();

		foreach (var (dllName, dll) in DllsWinning)
		{
			//if (dll.IsEnabled != true)
			//	continue;

			dll.Read(ExeVersion);
			if (dll.IsReadable != true) DLLsUnreadable.Add(dllName);
		}
	}

	private void GetModuleInfo()
	{
		ModulesUnreadable.Clear();
		ModulesUnknown.Clear();
		ModulesFull.Clear();
		ModulesLight.Clear();
		Modules100.Clear();
		Modules095.Clear();

		foreach (var (moduleName, module) in ModulesWinning)
		{
			if (module.IsEnabled != true) continue;

			module.ReadHeader();
			if (module.IsReadable != true)
			{
				ModulesUnreadable.Add(moduleName);
				continue;
			}

			switch (module.HEDRVersion)
			{
				case 1:
					Modules100.Add(moduleName);
					break;
				case 0.95:
					Modules095.Add(moduleName);
					break;
				default:
					ModulesUnknown.Add(moduleName);
					break;
			}

			if (module.IsLight == true)
				ModulesLight.Add(moduleName);
			else
				ModulesFull.Add(moduleName);
		}
	}

	/// <summary>
	///     Files in a Data subfolder as the game sees them. Under MO2, mod files only appear in the real Data folder while
	///     running inside its VFS,
	///     so the mod folders are walked too. Paths are returned as Data-folder paths (what <see cref="IModManager.Resolve" />
	///     expects); the highest priority file wins.
	/// </summary>
	private IEnumerable<string> EnumerateDataFiles(string relativeFolder = "")
	{
		var folder = Path.Combine(DataPath, relativeFolder);
		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

		if (Directory.Exists(folder))
			foreach (var filePath in Directory.EnumerateFiles(folder))
				if (seen.Add(Path.GetFileName(filePath)))
					yield return filePath;

		if (Manager is not ModOrganizer manager) yield break;
		// Stage paths are only meaningful as a priority order for MO2; Vortex deploys real files into Data.

		// StagePaths is lowest priority first, Overwrite last; later folders win.
		for (var i = manager.StagePaths.Count - 1; i >= 0; i--)
		{
			var stageFolder = Path.Combine(manager.StagePaths[i], relativeFolder);
			if (!Directory.Exists(stageFolder)) continue;

			foreach (var filePath in Directory.EnumerateFiles(stageFolder))
			{
				var name = Path.GetFileName(filePath);
				if (manager.SkipFileSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase))) continue;

				if (seen.Add(name)) yield return Path.Combine(folder, name);
			}
		}
	}

	private void GetArchiveLists()
	{
		ArchivesEnabled.Clear();
		ArchivesLoadOrder.Clear();
		ArchivesINI.Clear();

		// ArchivesINI is sorted, so keep the INI's own order for load order.
		List<string> iniOrder = [];

		// TODO: bInvalidateOlderFiles = 1
		// TODO: sResourceDataDirsFinal =
		foreach (var list in new[]
		         {
			         "SResourceArchiveList", "SResourceArchiveList2", "sResourceIndexFileList",
			         "sResourceStartUpArchiveList",
		         })
		{
			var archives = Config?["Archive"][list];
			if (archives == null || archives.Trim() == string.Empty) continue;

			foreach (var archiveName in archives.Split(',',
				         StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
				if (ArchivesINI.Add(archiveName))
					iniOrder.Add(archiveName);
		}

		// TODO: Test with custom Nvflex BA2
		// bNVFlexEnable lives in Fallout4Prefs.ini, not Fallout4.ini.
		if ((Prefs?["NVFlex"]["bNVFlexEnable"] ?? Config?["NVFlex"]["bNVFlexEnable"]) == "1")
			if (ArchivesINI.Add("Fallout4 - Nvflex.ba2"))
				iniOrder.Add("Fallout4 - Nvflex.ba2");

		foreach (var archiveName in iniOrder)
		{
			var archivePath = Path.Combine(DataPath, archiveName);
			Archive? archive;
			if (FileExistsInGame(archivePath))
			{
				archive = new Archive(archiveName, archivePath, Manager.Resolve(archivePath));
				Archives.Add(archive);
				ArchivesWinning.TryAdd(ArchiveKey(archiveName), archive);
			}

			if (ArchivesWinning.TryGetValue(ArchiveKey(archiveName), out archive))
			{
				archive.IsEnabled = true;
				if (ArchivesEnabled.Add(archiveName)) ArchivesLoadOrder.Add(archiveName);
			}

			// The game still loads without these, but its own base archives being absent means a broken install.
			if (archive == null && archiveName.Equals("Fallout4 - Nvflex.ba2", StringComparison.OrdinalIgnoreCase))
				AddProblem(ProblemType.FileNotFound, Severity.Medium,
					"NVIDIA Flex is enabled in your INI, but Fallout4 - Nvflex.ba2 was not found.",
					archivePath, solution: SolutionVerify, solutionType: SolutionType.VerifyFiles);
		}

		// The Anniversary Edition loads this archive without it being listed in the INI.
		if (IsAE)
		{
			const string texturesPatch = "Fallout4 - TexturesPatch.ba2";
			var texturesPatchPath = Path.Combine(DataPath, texturesPatch);
			if (!ArchivesWinning.TryGetValue(ArchiveKey(texturesPatch), out var patch) &&
			    FileExistsInGame(texturesPatchPath))
			{
				patch = new Archive(texturesPatch, texturesPatchPath, Manager.Resolve(texturesPatchPath));
				Archives.Add(patch);
				ArchivesWinning.TryAdd(ArchiveKey(texturesPatch), patch);
			}

			if (patch != null)
			{
				patch.IsEnabled = true;
				if (ArchivesEnabled.Add(texturesPatch)) ArchivesLoadOrder.Add(texturesPatch);
			}
			else
			{
				AddProblem(ProblemType.FileNotFound, Severity.Medium,
					"Fallout4 - TexturesPatch.ba2 is a base game file for the Anniversary Edition but was not found.",
					texturesPatchPath, solution: SolutionVerify, solutionType: SolutionType.VerifyFiles);
			}
		}

		foreach (var module in ModulesLoadOrder)
		{
			// TODO: Check if this does more than split a string
			var moduleBaseName = Path.GetFileNameWithoutExtension(module);

			foreach (var suffix in BA2Suffixes)
			{
				// TODO: Review this
				var archiveName = $"{moduleBaseName} - {suffix}.ba2";
				var archivePath = Path.Combine(DataPath, archiveName);
				Archive? archive;
				if (!ArchivesINI.Contains(archiveName) && FileExistsInGame(archivePath))
				{
					archive = new Archive(archiveName, archivePath, Manager.Resolve(archivePath));
					Archives.Add(archive);
					ArchivesWinning.TryAdd(ArchiveKey(archiveName), archive);
				}

				if (!ArchivesWinning.TryGetValue(ArchiveKey(archiveName), out archive)) continue;
				archive.IsEnabled = true;
				if (ArchivesEnabled.Add(archiveName)) ArchivesLoadOrder.Add(archiveName);
			}
		}
	}

	private void GetArchiveInfo()
	{
		ArchivesGeneral.Clear();
		ArchivesTexture.Clear();
		ArchivesOG.Clear();
		ArchivesNG.Clear();
		ArchivesUnreadable.Clear();
		ArchivesUnknown.Clear();

		if (GameVersion == Models.GameVersion.OldGen &&
		    ArchivesWinning.TryGetValue(ArchiveKey("Fallout4 - Startup.ba2"), out var startupArchive))
		{
			var hash = Utilities.GetFileHash(startupArchive.RealPath, true);
			if (hash == "A5808F5F") GameVersion = Models.GameVersion.DownGrade;
		}

		foreach (var archive in ArchivesWinning.Values)
		{
			if (archive.IsEnabled != true) continue;

			//archive.ReadHeader();
			if (archive.IsReadable != true)
			{
				ArchivesUnreadable.Add(archive.Name);
				continue;
			}

			switch (archive.ArchiveVersion)
			{
				case 1:
					ArchivesOG.Add(archive.Name);
					break;
				case 7 or 8:
					ArchivesNG.Add(archive.Name);
					break;
				default:
					ArchivesUnknown.Add(archive.Name);
					break;
			}

			switch (archive.ArchiveFormat)
			{
				case "GNRL":
					ArchivesGeneral.Add(archive.Name);
					break;
				case "DX10":
					ArchivesTexture.Add(archive.Name);
					break;
				default:
					ArchivesUnknown.Add(archive.Name);
					break;
			}
		}
	}
}
