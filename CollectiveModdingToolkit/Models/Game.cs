using CommunityToolkit.Mvvm.ComponentModel;
using IniParser;
using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text;

namespace CollectiveModdingToolkit.Models;

public partial class Game : ObservableObject
{
	[ObservableProperty] public partial string GamePath { get; set; } = "";
	[ObservableProperty] public partial string GameVersion { get; set; } = "";
	[ObservableProperty] public partial string Source { get; set; } = "";

	[ObservableProperty] public partial string Fallout4Version { get; set; } = "";
	[ObservableProperty] public partial string Fallout4LauncherVersion { get; set; } = "";
	[ObservableProperty] public partial string Steam_api64Version { get; set; } = "";
	[ObservableProperty] public partial string F4se_loaderVersion { get; set; } = "";
	[ObservableProperty] public partial string F4se_steam_loaderVersion { get; set; } = "";
	[ObservableProperty] public partial string CreationKitVersion { get; set; } = "";
	[ObservableProperty] public partial string Archive2Version { get; set; } = "";
	[ObservableProperty] public partial string Archive2InteropVersion { get; set; } = "";

	internal IniData? Config { get; private set; }
	internal IniData? Prefs { get; private set; }
	public string DataPath { get; private set; }
	public string F4SEPath { get; private set; }
	public string Language { get; private set; } = "en";
	public SortedSet<string> ArchivesINI { get; } = [];
	public SortedSet<string> ArchivesEnabled { get; } = [];
	public SortedSet<string> ArchivesGeneral { get; } = [];
	public SortedSet<string> ArchivesTexture { get; } = [];
	public SortedSet<string> ArchivesOG { get; } = [];
	public SortedSet<string> ArchivesNG { get; } = [];
	public SortedSet<string> ArchivesUnreadable { get; } = [];
	public SortedSet<string> ArchivesUnknown { get; } = [];

	public SortedSet<string> ModulesCCC { get; } = [];
	public SortedSet<string> ModulesEnabled { get; } = [];
	public SortedSet<string> Modules095 { get; } = [];
	public SortedSet<string> Modules100 { get; } = [];
	public SortedSet<string> ModulesFull { get; } = [];
	public SortedSet<string> ModulesLight { get; } = [];
	public SortedSet<string> ModulesUnreadable { get; } = [];
	public SortedSet<string> ModulesUnknown { get; } = [];

	public int TotalArchives => ArchivesGeneral.Count + ArchivesTexture.Count;
	public int TotalModules => ModulesFull.Count + ModulesLight.Count;

	public bool ModulesFullWarning => ModulesFull.Count >= MaxModulesFull * 0.95;
	public bool ModulesFullMaxed => ModulesFull.Count >= MaxModulesFull;
	public bool ModulesLightWarning => ModulesLight.Count >= MaxModulesLight * 0.95;
	public bool ModulesLightMaxed => ModulesLight.Count >= MaxModulesLight;
	public bool TotalModulesWarning => TotalModules >= TotalModulesMax * 0.95;
	public bool TotalModulesMaxed => TotalModules >= TotalModulesMax;

	public bool HasModulesUnreadable => ModulesUnreadable.Count > 0;
	public bool HasModulesUnknown => ModulesUnreadable.Count > 0;

	public bool ArchivesGeneralWarning => ArchivesGeneral.Count >= MaxArchives * 0.95;
	public bool ArchivesGeneralMaxed => ArchivesGeneral.Count >= MaxArchives;
	public bool ArchivesTextureWarning => ArchivesTexture.Count >= MaxArchives * 0.95;
	public bool ArchivesTextureMaxed => ArchivesTexture.Count >= MaxArchives;
	public bool TotalArchivesWarning => TotalArchives >= TotalArchivesMax * 0.95;
	public bool TotalArchivesMaxed => TotalArchives >= TotalArchivesMax;

	public bool HasArchivesUnreadable => ArchivesUnreadable.Count > 0;
	public bool HasArchivesUnknown => ArchivesUnreadable.Count > 0;

	[ObservableProperty] public partial bool? Fallout4Good { get; private set; } = null;
	[ObservableProperty] public partial bool? Fallout4LauncherGood { get; private set; } = null;
	[ObservableProperty] public partial bool? Steam_api64Good { get; private set; } = null;
	[ObservableProperty] public partial bool? F4se_loaderGood { get; private set; } = null;
	[ObservableProperty] public partial bool? F4se_steam_loaderGood { get; private set; } = null;
	[ObservableProperty] public partial bool? CreationKitGood { get; private set; } = null;
	[ObservableProperty] public partial bool? Archive2Good { get; private set; } = null;
	[ObservableProperty] public partial bool? Archive2InteropGood { get; private set; } = null;

	public SortedSet<string> BA2Suffixes { get; private set; }
	private static readonly string[] _default_ba2_suffixes = ["main", "textures", "voices_en"];

	public static int MaxArchives { get; } = 256;
	public static int MaxModulesFull { get; } = 254;
	public static int MaxModulesLight { get; } = 4096;
	public static int TotalArchivesMax { get; } = MaxArchives * 2;
	public static int TotalModulesMax { get; } = MaxModulesFull + MaxModulesLight;

	private static readonly IniParserConfiguration IniParserConfig = new()
	{
		AllowCreateSectionsOnFly = true,
		AllowDuplicateKeys = true,
		AllowDuplicateSections = true,
		AllowKeysWithoutSection = true,
		AssigmentSpacer = "",
		CaseInsensitive = true,
		SkipInvalidLines = true,
	};

	//private Computer _Computer;
	private readonly string[] _gameMasters = [
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
	private const string NO_FILE = "NO_FILE";
	private static readonly string Fallout4exeOG = "1.10.163.0";
	private static readonly string Fallout4exeNG = "1.10.984.0";
	//{ "Fallout4.exe", "1.10.980.0" },
	private static readonly string steam_api64_OG_GOG = "BD3AA35F";
	private static readonly Dictionary<string, string> binariesOG = new()
	{
		{ "Fallout4Launcher.exe", "02445570" },
		{ "steam_api64.dll", "2.89.45.4" },
		{ "f4se_loader.exe", "0.0.6.23" },
		{ "f4se_steam_loader.dll", "0.0.6.23" },
		{ "CreationKit.exe", "1.10.162.0" },
		{ Path.Combine("Tools", "Archive2", "Archive2.exe"), "1.1.0.4" },
		{ Path.Combine("Tools", "Archive2", "Archive2Interop.dll"), "850D36A9" },
	};
	private static readonly Dictionary<string, string> binariesNG = new()
	{
		{ "Fallout4Launcher.exe", "F6A06FF5" },
		{ "steam_api64.dll", "7.40.51.27" },
		{ "f4se_loader.exe", "0.0.7.2" },
		{ "f4se_steam_loader.dll", NO_FILE },
		{ "CreationKit.exe", "1.10.982.3" },
		{ Path.Combine("Tools", "Archive2", "Archive2.exe"), "1.1.0.5" },
		{ Path.Combine("Tools", "Archive2", "Archive2Interop.dll"), "EFBE3622" },
	};

	private readonly string _pluginsTxt;
	private readonly string _gameIniPath;

	public Game(IModManager? managerInfo)
	{
		//_Computer = computer;

		BA2Suffixes = new(_default_ba2_suffixes); // TODO: Remove
		GamePath = FindGamePath(managerInfo);
		DataPath = Path.Combine(GamePath, "Data");
		F4SEPath = Path.Combine(DataPath, "F4SE", "Plugins");


		_pluginsTxt = managerInfo?.PluginsTxt ?? Path.Combine(Computer.AppDataLocal, "Fallout4", "plugins.txt");
		if (!File.Exists(_pluginsTxt))
			throw new FileNotFoundException(_pluginsTxt);

		_gameIniPath = managerInfo?.GameIniPath ?? Computer.DocumentsFallout4;
		if (!Directory.Exists(_gameIniPath))
			throw new DirectoryNotFoundException(_gameIniPath);

		ReadGameINIs(_gameIniPath);

		GetBinaryInfo();
		GetModuleLists();
		GetArchiveLists();

		GetModuleInfo();
		GetArchiveInfo();

		//GameVersion = string.Empty;
		//Source = string.Empty;
	}

	private static string FindGamePath(IModManager? managerInfo)
	{
		if (managerInfo != null && managerInfo.GamePath != null)
			return managerInfo.GamePath;
		else
		{
			// TODO: Detect steam library folders and find game
			// TODO: Prompt user to choose between all non-manager game install detections or manual entry
			// TODO: Game source
			string? path = GetRegistryInstallPath() ?? GetRegistryInstallPathGOG();
			return path ?? throw new NotImplementedException();
		}
	}

	private static string? GetRegistryInstallPath()
	{
		try
		{
			using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Bethesda Softworks\Fallout4");
			return key?.GetValue("Installed Path")?.ToString();
		}
		catch { return null; }
		// TODO: Specific exceptions
	}

	private static string? GetRegistryInstallPathGOG()
	{
		try
		{
			using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games\1998527297");
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
		string iniPath = Path.Combine(path, "Fallout4.ini");
		string iniCustomPath = Path.Combine(path, "Fallout4Custom.ini");
		string iniPrefsPath = Path.Combine(path, "Fallout4Prefs.ini");

		if (File.Exists(iniPath))
		{
			Config = parser.ReadFile(iniPath, Encoding.UTF8);
			if (File.Exists(iniCustomPath))
			{
				IniData configCustom = parser.ReadFile(iniCustomPath);
				Config.Merge(configCustom);
			}
		}
		else
			throw new NotImplementedException("Fallout4.ini Not Present");

		Prefs = File.Exists(iniPrefsPath)
			? parser.ReadFile(iniPrefsPath)
			: throw new NotImplementedException("Fallout4Prefs.ini Not Present");

		if (Config == null)
			throw new NotImplementedException("Config is null");
		if (Prefs == null)
			throw new NotImplementedException("Prefs is null");

		BA2Suffixes = new(_default_ba2_suffixes);
		string? lang = Config["General"]["sLanguage"];
		// TODO: Test if empty string is valid
		if (string.IsNullOrEmpty(lang))
			lang = "en";
		else
		{
			lang = lang.ToLower();
			if (lang != "en")
				BA2Suffixes.Add($"voices_{lang}");
		}
		Language = lang;
	}

	private static string GetFileVersion(string filePath, bool hashOnly = false)
	{
		if (hashOnly)
			return $"{Crc32.HashToUInt32(File.ReadAllBytes(filePath).AsSpan()):X8}";
		FileVersionInfo version = FileVersionInfo.GetVersionInfo(filePath);
		string versionString = version.FileVersion == null
			? $"{Crc32.HashToUInt32(File.ReadAllBytes(filePath).AsSpan()):X8}"
			: $"{version.FileMajorPart}.{version.FileMinorPart}.{version.FileBuildPart}.{version.FilePrivatePart}";
		return versionString;
	}

	private void GetBinaryInfo()
	{
		//BinaryInfo binaryInfo = new();

		Dictionary<string, string> binariesGood;
		Dictionary<string, string> binariesBad;
		string gameVersionBad;
		string filePath = Path.Combine(GamePath, "Fallout4.exe");
		if (File.Exists(filePath))
		{
			string version = GetFileVersion(filePath);
			if (version == Fallout4exeOG)
			{
				GameVersion = Models.GameVersion.OldGen;
				Fallout4Version = Models.GameVersion.OldGen;
				gameVersionBad = Models.GameVersion.NextGen;
				binariesGood = binariesOG;
				binariesBad = binariesNG;
			}
			else if (version == Fallout4exeNG)
			{
				GameVersion = Models.GameVersion.NextGen;
				Fallout4Version = Models.GameVersion.NextGen;
				gameVersionBad = Models.GameVersion.OldGen;
				binariesGood = binariesNG;
				binariesBad = binariesOG;
			}
			else
			{
				GameVersion = Models.GameVersion.Unknown;
				throw new NotImplementedException("Unknown version: Fallout4.exe");
			}
		}
		else
		{
			throw new NotImplementedException("File missing: Fallout4.exe");
		}
		//OnPropertyChanged(nameof(Fallout4LauncherVersion));
		Fallout4Good = Fallout4Version == GameVersion;




		string fileName = "Fallout4Launcher.exe";
		filePath = Path.Combine(GamePath, fileName);
		if (File.Exists(filePath))
		{
			if (binariesGood[fileName] == NO_FILE)
				Fallout4LauncherVersion = gameVersionBad;
			else
			{
				bool hashOnly = !binariesGood[fileName].Contains('.');
				string version = GetFileVersion(filePath, hashOnly);
				Fallout4LauncherVersion = version == binariesGood[fileName]
					? GameVersion
					: version == binariesBad[fileName] ? gameVersionBad : Models.GameVersion.Unknown;
			}
			Fallout4LauncherGood = Fallout4LauncherVersion == GameVersion;
		}
		else if (binariesGood[fileName] == NO_FILE)
		{
			Fallout4LauncherVersion = Models.GameVersion.NotFoundGood;
			Fallout4LauncherGood = null;
		}
		else
		{
			Fallout4LauncherVersion = Models.GameVersion.NotFoundBad;
			Fallout4LauncherGood = false;
		}

		fileName = "steam_api64.dll";
		filePath = Path.Combine(GamePath, fileName);
		if (File.Exists(filePath))
		{
			if (binariesGood[fileName] == NO_FILE)
				Steam_api64Version = gameVersionBad;
			else
			{
				bool hashOnly = !binariesGood[fileName].Contains('.');
				string version = GetFileVersion(filePath, hashOnly);
				Steam_api64Version = version == binariesGood[fileName]
					? GameVersion
					: version == binariesBad[fileName] ? gameVersionBad : Models.GameVersion.Unknown;
			}
			Steam_api64Good = Steam_api64Version == GameVersion;
		}
		else if (binariesGood[fileName] == NO_FILE)
		{
			Steam_api64Version = Models.GameVersion.NotFoundGood;
			Steam_api64Good = null;
		}
		else
		{
			Steam_api64Version = Models.GameVersion.NotFoundBad;
			Steam_api64Good = false;
		}

		fileName = "f4se_loader.exe";
		filePath = Path.Combine(GamePath, fileName);
		if (File.Exists(filePath))
		{
			if (binariesGood[fileName] == NO_FILE)
				F4se_loaderVersion = gameVersionBad;
			else
			{
				bool hashOnly = !binariesGood[fileName].Contains('.');
				string version = GetFileVersion(filePath, hashOnly);
				F4se_loaderVersion = version == binariesGood[fileName]
					? GameVersion
					: version == binariesBad[fileName] ? gameVersionBad : Models.GameVersion.Unknown;
			}
			F4se_loaderGood = F4se_loaderVersion == GameVersion;
		}
		else if (binariesGood[fileName] == NO_FILE)
		{
			F4se_loaderVersion = Models.GameVersion.NotFoundGood;
			F4se_loaderGood = null;
		}
		else
		{
			F4se_loaderVersion = Models.GameVersion.NotFoundBad;
			F4se_loaderGood = false;
		}

		fileName = "f4se_steam_loader.dll";
		filePath = Path.Combine(GamePath, fileName);
		if (File.Exists(filePath))
		{
			if (binariesGood[fileName] == NO_FILE)
				F4se_steam_loaderVersion = gameVersionBad;
			else
			{
				bool hashOnly = !binariesGood[fileName].Contains('.');
				string version = GetFileVersion(filePath, hashOnly);
				F4se_steam_loaderVersion = version == binariesGood[fileName]
					? GameVersion
					: version == binariesBad[fileName] ? gameVersionBad : Models.GameVersion.Unknown;
			}
			F4se_steam_loaderGood = F4se_steam_loaderVersion == GameVersion;
		}
		else if (binariesGood[fileName] == NO_FILE)
		{
			F4se_steam_loaderVersion = Models.GameVersion.NotFoundGood;
			F4se_steam_loaderGood = null;
		}
		else
		{
			F4se_steam_loaderVersion = Models.GameVersion.NotFoundBad;
			F4se_steam_loaderGood = false;
		}

		fileName = "CreationKit.exe";
		filePath = Path.Combine(GamePath, fileName);
		if (File.Exists(filePath))
		{
			if (binariesGood[fileName] == NO_FILE)
				CreationKitVersion = gameVersionBad;
			else
			{
				bool hashOnly = !binariesGood[fileName].Contains('.');
				string version = GetFileVersion(filePath, hashOnly);
				CreationKitVersion = version == binariesGood[fileName]
					? GameVersion
					: version == binariesBad[fileName] ? gameVersionBad : Models.GameVersion.Unknown;
			}
			CreationKitGood = CreationKitVersion == GameVersion;
		}
		else if (binariesGood[fileName] == NO_FILE)
		{
			CreationKitVersion = Models.GameVersion.NotFoundGood;
			CreationKitGood = null;
		}
		else
		{
			CreationKitVersion = Models.GameVersion.NotFoundBad;
			CreationKitGood = false;
		}

		fileName = Path.Combine("Tools", "Archive2", "Archive2.exe");
		filePath = Path.Combine(GamePath, fileName);
		if (File.Exists(filePath))
		{
			if (binariesGood[fileName] == NO_FILE)
				Archive2Version = gameVersionBad;
			else
			{
				bool hashOnly = !binariesGood[fileName].Contains('.');
				string version = GetFileVersion(filePath, hashOnly);
				Archive2Version = version == binariesGood[fileName]
					? GameVersion
					: version == binariesBad[fileName] ? gameVersionBad : Models.GameVersion.Unknown;
			}
			Archive2Good = Archive2Version == GameVersion;
		}
		else if (binariesGood[fileName] == NO_FILE)
		{
			Archive2Version = Models.GameVersion.NotFoundGood;
			Archive2Good = null;
		}
		else
		{
			Archive2Version = Models.GameVersion.NotFoundBad;
			Archive2Good = false;
		}

		fileName = Path.Combine("Tools", "Archive2", "Archive2Interop.dll");
		filePath = Path.Combine(GamePath, fileName);
		if (File.Exists(filePath))
		{
			if (binariesGood[fileName] == NO_FILE)
				Archive2InteropVersion = gameVersionBad;
			else
			{
				bool hashOnly = !binariesGood[fileName].Contains('.');
				string version = GetFileVersion(filePath, hashOnly);
				Archive2InteropVersion = version == binariesGood[fileName]
					? GameVersion
					: version == binariesBad[fileName] ? gameVersionBad : Models.GameVersion.Unknown;
			}
			Archive2InteropGood = Archive2InteropVersion == GameVersion;
		}
		else if (binariesGood[fileName] == NO_FILE)
		{
			Archive2InteropVersion = Models.GameVersion.NotFoundGood;
			Archive2InteropGood = null;
		}
		else
		{
			Archive2InteropVersion = Models.GameVersion.NotFoundBad;
			Archive2InteropGood = false;
		}


		//foreach (string fileName in binariesGood.Keys)
		//{
		//	filePath = Path.Combine(GamePath, fileName);
		//	if (File.Exists(filePath))
		//	{
		//		//throw new NotImplementedException($"File shouldn't exist: ${fileName}");
		//		if (binariesGood[fileName] == NO_FILE) BinaryInfo[fileName] = gameVersionBad;
		//		else
		//		{
		//			bool hashOnly = !binariesGood[fileName].Contains('.');
		//			string version = GetFileVersion(filePath, hashOnly);
		//			if (version == binariesGood[fileName]) BinaryInfo[fileName] = GameVersion;
		//			else if (version == binariesBad[fileName]) BinaryInfo[fileName] = gameVersionBad;
		//			else BinaryInfo[fileName] = Models.GameVersion.Unknown;
		//		}

		//	}
		//	else if (binariesGood[fileName] == NO_FILE)
		//	{
		//		BinaryInfo[fileName] = Models.GameVersion.NotFoundGood;
		//	}
		//	else
		//	{
		//		BinaryInfo[fileName] = Models.GameVersion.NotFoundBad;
		//		//throw new NotImplementedException($"File missing: {fileName}");
		//	}
		//}
		//foreach (KeyValuePair<string, string> s in BinaryInfo) Console.WriteLine(s);
	}

	private void GetModuleLists()
	{
		ModulesEnabled.Clear();

		foreach (string module in _gameMasters)
		{
			string modulePath = Path.Combine(DataPath, module);
			if (File.Exists(modulePath))
				ModulesEnabled.Add(module);
			// TODO: store path?
		}

		// TODO: check if force enabled or if its even possible to disable
		ModulesCCC.Clear();
		string cccPath = Path.Combine(GamePath, "Fallout4.ccc");
		if (!File.Exists(cccPath))
			throw new NotImplementedException("Fallout4.ccc missing");
		string[] contents = File.ReadAllLines(cccPath, Encoding.UTF8);
		foreach (string module in contents)
		{
			string modulePath = Path.Combine(DataPath, module);
			if (File.Exists(modulePath))
				ModulesCCC.Add(module);
			// TODO: store path?
		}
		ModulesEnabled.UnionWith(ModulesCCC);

		//string pluginsPath = Path.Combine(Computer.AppDataLocal, "Fallout4", "plugins.txt");
		//if (!File.Exists(_pluginsTxt))
		//	throw new NotImplementedException("plugins.txt missing");
		contents = File.ReadAllLines(_pluginsTxt, Encoding.UTF8);
		foreach (string module in contents)
		{
			if (!module.StartsWith('*'))
				continue;
			string modulePath = Path.Combine(DataPath, module[1..]);
			if (File.Exists(modulePath))
				ModulesEnabled.Add(module[1..]);
			// TODO: store path?
			// TODO: store missing files
			// TODO: store disabled files
			// TODO: check first line comment
		}
	}

	private void GetModuleInfo()
	{
		ModulesUnreadable.Clear();
		ModulesFull.Clear();
		ModulesLight.Clear();
		Modules100.Clear();
		Modules095.Clear();

		foreach (string module in ModulesEnabled)
		{
			string modulePath = Path.Combine(DataPath, module);
			using FileStream file = File.OpenRead(modulePath);
			byte[] header = new byte[34];
			int bytesRead = file.Read(header, 0, header.Length);

			if (bytesRead != header.Length || !header[0..4].SequenceEqual(Magic.TES4) || !header[24..28].SequenceEqual(Magic.HEDR))
			{
				ModulesUnreadable.Add(module); // TOD: check if counted against limits (+ba2)
				continue;
			}
			double hedrVersion = Math.Round(BitConverter.ToSingle(header, 30), 2);
			if (hedrVersion == 1)
				Modules100.Add(module);
			else if (hedrVersion == 0.95)
				Modules095.Add(module);
			else
				ModulesUnknown.Add(module);
			if (module.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
			{
				ModulesLight.Add(module);
			}
			else
			{
				int flags = BitConverter.ToInt32(header, 8);
				if ((flags & Magic.Light) != 0)
					ModulesLight.Add(module);
				else
					ModulesFull.Add(module);
			}
		}
	}

	private void GetArchiveLists()
	{
		ArchivesEnabled.Clear();
		ArchivesINI.Clear();

		// TODO: bInvalidateOlderFiles = 1
		// TODO: sResourceDataDirsFinal =
		foreach (string list in new[] { "SResourceArchiveList", "SResourceArchiveList2", "sResourceIndexFileList", "sResourceStartUpArchiveList" })
		{
			string? archives = Config?["Archive"][list];
			if (archives == null || archives.Trim() == string.Empty)
				continue;
			ArchivesINI.UnionWith(archives.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
		}
		if (Config?["NVFlex"]["bNVFlexEnable"] == "1")
			ArchivesINI.Add("Fallout4 - Nvflex.ba2");

		foreach (string archive in ArchivesINI)
		{
			if (!File.Exists(Path.Combine(DataPath, archive)))
				ArchivesINI.Remove(archive);
		}

		ArchivesEnabled.UnionWith(ArchivesINI);

		foreach (string module in ModulesEnabled)
		{
			// TODO: Check if this does more than split a string
			string moduleBaseName = Path.GetFileNameWithoutExtension(module);

			foreach (string suffix in BA2Suffixes)
			{
				string archiveName = $"{moduleBaseName} - {suffix}.ba2";
				string archivePath = Path.Combine(DataPath, archiveName);
				if (File.Exists(archivePath))
					ArchivesEnabled.Add(archiveName);

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

		foreach (string archive in ArchivesEnabled)
		{
			string archivePath = Path.Combine(DataPath, archive);
			using FileStream file = File.OpenRead(archivePath);
			byte[] header = new byte[12];
			int bytesRead = file.Read(header, 0, header.Length);

			if (bytesRead != header.Length || !header[0..4].SequenceEqual(Magic.BTDX))
			{
				ArchivesUnreadable.Add(archive);
				continue;
			}
			byte archiveVersion = header[4];
			if (archiveVersion == 1)
				ArchivesOG.Add(archive);
			else if (archiveVersion is 7 or 8)
				ArchivesNG.Add(archive);
			else
			{
				ArchivesUnknown.Add(archive);
				continue;
			}

			byte[] archiveFormat = header[8..12];
			if (archiveFormat.SequenceEqual(Magic.GNRL))
				ArchivesGeneral.Add(archive);
			else if (archiveFormat.SequenceEqual(Magic.DX10))
				ArchivesTexture.Add(archive);
			else
				ArchivesUnknown.Add(archive);
		}
	}
}
