/*
 * 
 * Collective Modding Toolkit
 * Copyright (C) 2024, 2025  wxMichael
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

using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Models.FileTypes;
using CollectiveModdingToolkit.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using IniParser;
using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace CollectiveModdingToolkit.Managers;

internal partial class ModOrganizer : ObservableObject, IModManager
{
	public string AppName { get; } = "Mod Organizer";
	public string AppPath { get; }
	public DirectoryInfo AppFolder { get; }
	public Version? AppVersion { get; }
	public string? AppVersionString { get; private set; } = string.Empty;
	public string? GameName { get; private set; }
	public string? GamePath { get; private set; }
	public string? Profile { get; private set; } = null;
	//public string? StagePath { get; private set; }
	public string GameIniFolderPath { get; private set; }
	public string PluginsTxtPath { get; private set; }
	public List<string> StagePaths { get; private set; } = [];
	public List<Module> Modules { get; private set; } = [];
	public Dictionary<string, Module> ModulesWinning { get; private set; } = [];
	public List<Archive> Archives { get; private set; } = [];
	public Dictionary<string, Archive> ArchivesWinning { get; private set; } = [];
	//public List<string> ModulesEnabled { get; private set; } = [];
	//public List<string> ArchivesEnabled { get; private set; } = [];
	public List<DLL> Dlls { get; private set; } = [];
	public Dictionary<string, DLL> DllsWinning { get; private set; } = [];

	private const string MO2RegistryPath = @"Software\Mod Organizer Team\Mod Organizer";
	private string? _modListTxtPath;
	private readonly string[] _moduleExtensions = [".esm", ".esl", ".esp"];

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
			// TODO: Handling for ERROR return value.
			if (field != string.Empty)
				return field;
			using RegistryKey? key = Registry.CurrentUser.OpenSubKey(MO2RegistryPath);
			if (key == null)
			{
				MainWindow.CriticalError(typeof(NullReferenceException), $"Registry key missing:\n{MO2RegistryPath}");
				return "ERROR";
			}

			object? value = key.GetValue("CurrentInstance");
			if (value == null)
			{
				MainWindow.CriticalError(typeof(NullReferenceException), $"Registry key missing:\n{MO2RegistryPath}\\CurrentInstance");
				return "ERROR";
			}
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

	private string _baseDirectory = string.Empty;
	private string _modDirectory = string.Empty;
	private string _overwriteDirectory = string.Empty;
	private string _profilesDirectory = string.Empty;
	//private bool _profile_local_inis = true;
	//private bool _profile_local_saves = false;
	//private bool _profile_archive_invalidation = false;
	//private string[] _skip_file_suffixes = [];
	//private string[] _skip_directories = [];
	//private string[] _executable_blacklist = [];
	//private readonly bool _moddingLinked = false;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS0162 // Unreachable code detected
	public ModOrganizer()
	{
#if !DEBUG
		throw new NotImplementedException();
#endif
		AppPath = @"C:\Modding\MO2\ModOrganizer.exe";
		AppFolder = Directory.GetParent(AppPath) ?? throw new NullReferenceException($"Parent of {AppPath}");
		AppVersionString = Utilities.GetFileVersion(AppPath, skipPrivate: true);
		AppVersion = AppVersionString == null ? null : new(AppVersionString);
		Refresh();
	}
#pragma warning restore CS0162 // Unreachable code detected
#pragma warning restore IDE0079 // Remove unnecessary suppression

	public ModOrganizer(ProcessModule? processModule)
	{
		ArgumentNullException.ThrowIfNull(processModule);
		AppPath = processModule.FileName;
		AppFolder = Directory.GetParent(AppPath)!;
		AppVersionString = Utilities.GetFileVersion(AppPath, skipPrivate: true);
		AppVersion = AppVersionString == null ? null : new(AppVersionString);
		string? ver = processModule.FileVersionInfo.FileVersion;
		if (ver != null && ver.Contains("-ML"))
		{
			//_moddingLinked = true;
			AppVersionString = ver;
		}
		Refresh();
	}

	[MemberNotNull(nameof(GameIniFolderPath), nameof(PluginsTxtPath))]
	public void Refresh()
	{
		GameName = null;
		GamePath = null;
		Profile = null;
		//StagePath = null;
		GameIniFolderPath = null!;
		PluginsTxtPath = null!;
		StagePaths.Clear();
		Modules.Clear();
		ModulesWinning.Clear();
		Archives.Clear();
		ArchivesWinning.Clear();
		Dlls.Clear();
		DllsWinning.Clear();
		_modListTxtPath = null;
		_appIniData = null;
		//_profileIniData = null;
		CurrentInstance = string.Empty;
		_isPortable = null;

		_baseDirectory = string.Empty;
		_modDirectory = string.Empty;
		_overwriteDirectory = string.Empty;
		_profilesDirectory = string.Empty;
		//_profile_local_inis = true;
		//_profile_local_saves = false;
		//_profile_archive_invalidation = false;
		//_skip_file_suffixes = [];
		//_skip_directories = [];
		//_executable_blacklist = [];

		if (!ReadSettings())
			return;
		GetOverviewInfo();
	}

	[MemberNotNull(nameof(GameIniFolderPath), nameof(PluginsTxtPath))]
	private bool ReadSettings()
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
		{
			MainWindow.CriticalError(typeof(NullReferenceException), $"Unable to read file:\n{iniPath}");
			GameIniFolderPath = string.Empty;
			PluginsTxtPath = string.Empty;
			return false;
		}

		KeyDataCollection general = _appIniData["General"];
		//gameName=Fallout 4
		GameName = general["gameName"];

		//selected_profile=@ByteArray(Default)
		Profile = FormatByteArray(general["selected_profile"] ?? "Default");

		//gamePath=@ByteArray(C:\\Program Files(x86)\\Steam\\steamapps\\common\\Fallout 4)
		string? gamePath = general["gamePath"];
		GamePath = gamePath == null ? null : FormatByteArray(gamePath);

		KeyDataCollection settings = _appIniData["Settings"];

		// TODO: Verify AppPath is correct here
		//base_directory=C:/Modding/MO2 Instances/FO4
		string? base_directory = settings["base_directory"]?.Trim();
		if (base_directory == string.Empty)
			base_directory = null;
		_baseDirectory = base_directory ?? (IsPortable ? AppPath : Path.Combine(Computer.AppDataLocal, "ModOrganizer", CurrentInstance));

		if (!Directory.Exists(_baseDirectory))
		{
			MainWindow.CriticalError(typeof(DirectoryNotFoundException), _baseDirectory);
			GameIniFolderPath = string.Empty;
			PluginsTxtPath = string.Empty;
			return false;
		}

		_modDirectory = (settings["mod_directory"] ?? "%BASE_DIR%/mods").Replace("%BASE_DIR%", _baseDirectory);
		_overwriteDirectory = (settings["overwrite_directory"] ?? "%BASE_DIR%/overwrite").Replace("%BASE_DIR%", _baseDirectory);
		_profilesDirectory = (settings["profiles_directory"] ?? "%BASE_DIR%/profiles").Replace("%BASE_DIR%", _baseDirectory);


		// TODO: use settings.ini (and _profile_local_inis ??)
		GameIniFolderPath = Path.Combine(_profilesDirectory, Profile);
		PluginsTxtPath = Path.Combine(_profilesDirectory, Profile, "plugins.txt");

		string modListTxtPath = Path.Combine(_profilesDirectory, Profile, "modlist.txt");
		if (!File.Exists(modListTxtPath))
		{
			MainWindow.CriticalError(typeof(FileNotFoundException), modListTxtPath);
			return false;
		}
		_modListTxtPath = modListTxtPath;


		////profile_local_inis=true
		//try
		//{
		//	_profile_local_inis = Convert.ToBoolean(settings["profile_local_inis"] ?? bool.TrueString);
		//}
		//catch (FormatException)
		//{
		//	_profile_local_inis = true;
		//}

		////profile_local_saves=false
		//try
		//{
		//	_profile_local_saves = Convert.ToBoolean(settings["profile_local_saves"] ?? bool.FalseString);
		//}
		//catch (FormatException)
		//{
		//	_profile_local_saves = false;
		//}

		////profile_archive_invalidation=false
		//try
		//{
		//	_profile_archive_invalidation = Convert.ToBoolean(settings["profile_archive_invalidation"] ?? bool.FalseString);
		//}
		//catch (FormatException)
		//{
		//	_profile_archive_invalidation = false;
		//}

		////skip_file_suffixes=.mohidden
		//// Delimited with comma-space
		//_skip_file_suffixes = (settings["skip_file_suffixes"] ?? ".mohidden").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

		////skip_directories=.git
		//// Delimited with comma-space
		//_skip_directories = (settings["skip_directories"] ?? ".git").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

		////executable_blacklist="Chrome.exe;Firefox.exe;TSVNCache.exe;TGitCache.exe;Steam.exe;GameOverlayUI.exe;Discord.exe;GalaxyClient.exe;Spotify.exe;Brave.exe"
		//// Delimited with semicolon and quoted if more than one item.
		//string default_exes = "Chrome.exe;Firefox.exe;TSVNCache.exe;TGitCache.exe;Steam.exe;GameOverlayUI.exe;Discord.exe;GalaxyClient.exe;Spotify.exe;Brave.exe";
		//_executable_blacklist = (settings["executable_blacklist"] ?? default_exes).Trim('"').Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

		//filter_regex=false
		//regex_case_sensitive=false
		//regex_extended=false
		//force_enable_core_files=true
		//archive_parsing_experimental=false

		return true;
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

	public void GetOverviewInfo()
	{
		//StagePaths.Clear();
		//Modules.Clear();
		//Archives.Clear();

		// TODO: Make slashes consistent?
		//Console.WriteLine(_modListTxtPath);
		// TODO: Encoding
		foreach (string line in File.ReadLines(_modListTxtPath!, Encoding.UTF8).Reverse())
		{
			if (line.StartsWith('+') && !line.EndsWith("_separator"))
			{
				string modFolder = line[1..];
				string modFolderPath = Path.Combine(_modDirectory, modFolder);
				if (Directory.Exists(modFolderPath))
					StagePaths.Add(modFolderPath);
			}
		}
		StagePaths.Add(_overwriteDirectory);
		// TODO: Scanner: Option to scan overridden files?
		foreach ((int index, string modFolder) in StagePaths.Index())
		{
			string modFolderPath = Path.Combine(_modDirectory, modFolder);
			foreach (string filePath in Directory.EnumerateFiles(modFolderPath))
			{
				// TODO: Get mod name from meta.ini
				string fileName = Path.GetFileName(filePath);
				string fileExt = Path.GetExtension(fileName);
				if (fileExt == string.Empty)
					continue;
				string fileExtLower = fileExt.ToLower();

				if (fileExtLower == ".ba2")
				{
					Archive archive = new(fileName, filePath, modFolder, index, modFolder);
					Archives.Add(archive);
					ArchivesWinning[fileName] = archive;
					// TODO: Check if already present and add to ArchivesLosing
					continue;
				}
				if (_moduleExtensions.Any(fileExtLower.Equals))
				{
					Module module = new(fileName, filePath, modFolder, fileExtLower, index, modFolder);
					Modules.Add(module);
					ModulesWinning[fileName] = module;
					continue;
				}
			}
			string f4sePluginsPath = Path.Combine(modFolderPath, "F4SE", "Plugins");
			if (Directory.Exists(f4sePluginsPath))
			{
				foreach (string filePath in Directory.EnumerateFiles(modFolderPath))
				{
					// TODO: Get mod name from meta.ini
					string fileName = Path.GetFileName(filePath);
					string fileExt = Path.GetExtension(fileName);
					if (fileExt == string.Empty)
						continue;
					string fileExtLower = fileExt.ToLower();

					if (fileExtLower == ".dll")
					{
						DLL dll = new(fileName, filePath, modFolder, index, modFolder);
						Dlls.Add(dll);
						DllsWinning[fileName] = dll;
						// TODO: Check if already present and add to DllsLosing
						continue;
					}
				}
			}
		}
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
}

// Defaults:
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
