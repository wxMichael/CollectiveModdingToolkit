using CollectiveModdingToolkit.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using IniParser;
using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CollectiveModdingToolkit;

internal partial class ModOrganizer : ObservableObject, IModManager
{
	//[ObservableProperty] public partial string Name { get; set; }
	//[ObservableProperty] private string _name;
	//[ObservableProperty] private string _path;
	//[ObservableProperty] private Version _version;
	public string AppName { get; } = "Mod Organizer";
	public string AppPath { get; }
	public DirectoryInfo AppFolder { get; }
	public Version? AppVersion { get; }
	public string AppVersionString { get; private set; } = string.Empty;
	public string? GameName { get; private set; }
	public string? GamePath { get; private set; }
	public string Profile { get; private set; } = "Default";

	public string? StagePath { get; private set; }
	public string? PluginsTxt { get; private set; }
	public string? GameIniPath { get; private set; }

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
	private IniData? _appIniData;

	private string CurrentInstance
	{
		get
		{
			if (field != string.Empty)
				return field;
			using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Mod Organizer Team\Mod Organizer")
				?? throw new NullReferenceException();
			object value = key.GetValue("CurrentInstance") ?? throw new NullReferenceException();
			field = (string)value;
			return field;
		}

		set;
	} = string.Empty;

	private bool? _isPortable = null;
	private bool IsPortable
	{
		get
		{
			if (_isPortable != null)
				return _isPortable.Value;
			_isPortable = File.Exists(Path.Combine(AppFolder.FullName, "portable.txt")) || CurrentInstance == string.Empty;
			return _isPortable.Value;
		}
	}

	// TODO: Include in refresh
	private string _baseDirectory = string.Empty;
	private string _modDirectory = string.Empty;
	private string _overwriteDirectory = string.Empty;
	private string _profilesDirectory = string.Empty;
	private bool _profile_local_inis = true;
	private bool _profile_local_saves = false;
	private bool _profile_archive_invalidation = false;
	private string[] _skip_file_suffixes = [];
	private string[] _skip_directories = [];
	private string[] _executable_blacklist = [];
	private readonly bool _moddingLinked = false;

	private static string GetFileVersion(string filePath)
	{
		FileVersionInfo version = FileVersionInfo.GetVersionInfo(filePath);
		string versionString = version.FileVersion == null
			? string.Empty
			: $"{version.FileMajorPart}.{version.FileMinorPart}.{version.FileBuildPart}";
		return versionString;
	}

	public ModOrganizer()
	{
#if DEBUG
		AppPath = @"C:\Modding\MO2\ModOrganizer.exe";
		AppFolder = Directory.GetParent(AppPath)!;
		AppVersion = new Version("2.5.2");
		//ReadSettings();2.5.2-ML1.5
		RefreshSettings();
#else
		throw new NotImplementedException();
#endif
	}

	public ModOrganizer(ProcessModule? processModule)
	{
		ArgumentNullException.ThrowIfNull(processModule);
		AppPath = processModule.FileName;
		AppFolder = Directory.GetParent(AppPath)!;
		AppVersion = new(GetFileVersion(AppPath));
		string? ver = processModule.FileVersionInfo.FileVersion;
		if (ver != null && ver.Contains("-ML"))
		{
			_moddingLinked = true;
			AppVersionString = ver;
		}
		//ReadSettings();
		RefreshSettings();
	}

	private static string FormatByteArray(string path)
	{
		path = path.Trim('"');
		if (path.StartsWith("@ByteArray(") && path.EndsWith(')'))
		{
			path = path[11..^1];
		}
		return path.Replace(@"\\", @"\");
	}

	public void ReadSettings()
	{
		string iniPath = IsPortable
			? Path.Combine(AppFolder.FullName, "ModOrganizer.ini")
			: Path.Combine(Computer.AppDataLocal, "ModOrganizer", CurrentInstance, "ModOrganizer.ini");

		// TODO: filenotfound
		FileIniDataParser parser = new(new IniDataParser(IniParserConfig));

		// TODO: Other encodings?
		_appIniData = parser.ReadFile(iniPath, Encoding.UTF8);
		// TODO: handle null. test if even actually possible?
		if (_appIniData == null)
			return;

		KeyDataCollection? general = _appIniData["General"];
		if (general != null)
		{
			//gameName=Fallout 4
			GameName = general["gameName"];

			//selected_profile=@ByteArray(Default)
			Profile = FormatByteArray(general["selected_profile"] ?? "Default");

			//gamePath=@ByteArray(C:\\Program Files(x86)\\Steam\\steamapps\\common\\Fallout 4)
			string? gamePath = general["gamePath"];
			GamePath = gamePath == null ? null : FormatByteArray(gamePath);

		}

		KeyDataCollection? settings = _appIniData["Settings"];
		if (settings != null)
		{
			// TODO: Verify AppPath is correct here
			//base_directory=C:/Modding/MO2 Instances/FO4
			string? base_directory = settings["base_directory"]?.Trim();
			if (base_directory == string.Empty)
				base_directory = null;
			_baseDirectory = base_directory ?? (IsPortable ? AppPath : Path.Combine(Computer.AppDataLocal, "ModOrganizer", CurrentInstance));

			if (!Directory.Exists(_baseDirectory))
				throw new DirectoryNotFoundException(base_directory);

			_modDirectory = (settings["mod_directory"] ?? "%BASE_DIR%/mods").Replace("%BASE_DIR%", _baseDirectory);
			_overwriteDirectory = (settings["overwrite_directory"] ?? "%BASE_DIR%/overwrite").Replace("%BASE_DIR%", _baseDirectory);
			_profilesDirectory = (settings["profiles_directory"] ?? "%BASE_DIR%/profiles").Replace("%BASE_DIR%", _baseDirectory);

			//profile_local_inis=true
			try
			{
				_profile_local_inis = Convert.ToBoolean(settings["profile_local_inis"] ?? bool.TrueString);
			}
			catch (FormatException)
			{
				_profile_local_inis = true;
			}

			//profile_local_saves=false
			try
			{
				_profile_local_saves = Convert.ToBoolean(settings["profile_local_saves"] ?? bool.FalseString);
			}
			catch (FormatException)
			{
				_profile_local_saves = false;
			}

			//profile_archive_invalidation=false
			try
			{
				_profile_archive_invalidation = Convert.ToBoolean(settings["profile_archive_invalidation"] ?? bool.FalseString);
			}
			catch (FormatException)
			{
				_profile_archive_invalidation = false;
			}

			//skip_file_suffixes=.mohidden
			// Delimited with comma-space
			_skip_file_suffixes = (settings["skip_file_suffixes"] ?? ".mohidden").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

			//skip_directories=.git
			// Delimited with comma-space
			_skip_directories = (settings["skip_directories"] ?? ".git").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

			//executable_blacklist="Chrome.exe;Firefox.exe;TSVNCache.exe;TGitCache.exe;Steam.exe;GameOverlayUI.exe;Discord.exe;GalaxyClient.exe;Spotify.exe;Brave.exe"
			// Delimited with semicolon and quoted if more than one item.
			string default_exes = "Chrome.exe;Firefox.exe;TSVNCache.exe;TGitCache.exe;Steam.exe;GameOverlayUI.exe;Discord.exe;GalaxyClient.exe;Spotify.exe;Brave.exe";
			_executable_blacklist = (settings["executable_blacklist"] ?? default_exes).Trim('"').Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

			//filter_regex=false
			//regex_case_sensitive=false
			//regex_extended=false
			//force_enable_core_files=true
			//archive_parsing_experimental=false

			string pluginsTxt = Path.Combine(_profilesDirectory, Profile, "plugins.txt");
			if (File.Exists(pluginsTxt))
				PluginsTxt = pluginsTxt;

			// TODO: use settings.ini (and _profile_local_inis ??)
			string gameIniPath = Path.Combine(_profilesDirectory, Profile);
			if (Directory.Exists(gameIniPath))
				GameIniPath = gameIniPath;
		}

	}

	//[Settings]
	//base_directory=C:/Modding/MO2 Instances/FO4
	//profile_local_inis=true
	//profile_local_saves=true
	//profile_archive_invalidation=true
	//filter_regex=false
	//regex_case_sensitive=false
	//regex_extended=false
	//force_enable_core_files=true
	//archive_parsing_experimental=false
	//executable_blacklist="Firefox.exe;Steam.exe;GameOverlayUI.exe;Discord.exe"
	//skip_file_suffixes=.mohidden
	//skip_directories=.git

	//[customExecutables]
	//size=21
	//1\arguments=
	//1\binary=C:/Users/Michael/RiderProjects/AvaloniaApplication1/AvaloniaApplication1/bin/Release/net8.0-windows/AvaloniaApplication1.exe
	//1\hide=false
	//1\ownicon=false
	//1\steamAppID=
	//1\title=Ava Release
	//1\toolbar=false
	//1\workingDirectory=

	public void RefreshSettings()
	{
		GameName = null;
		GamePath = null;
		Profile = "Default";
		_appIniData = null;
		//_profileIniData = null;
		CurrentInstance = string.Empty;
		_isPortable = null;
		ReadSettings();
	}
}

//public IniParserConfiguration()
//{
//	CommentString = ";";
//	SectionStartChar = '[';
//	SectionEndChar = ']';
//	KeyValueAssigmentChar = '=';
//	AssigmentSpacer = " ";
//	NewLineStr = Environment.NewLine;
//	ConcatenateDuplicateKeys = false;
//	AllowKeysWithoutSection = true;
//	AllowDuplicateKeys = false;
//	AllowDuplicateSections = false;
//	AllowCreateSectionsOnFly = true;
//	ThrowExceptionsOnError = true;
//	SkipInvalidLines = false;
//}
