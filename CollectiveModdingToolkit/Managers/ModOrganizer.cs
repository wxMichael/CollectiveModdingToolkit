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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using IniParser;
using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Microsoft.Win32;

namespace CollectiveModdingToolkit.Managers;

internal class ModOrganizer : ObservableObject, IModManager
{
	private const string MO2RegistryPath = @"Software\Mod Organizer Team\Mod Organizer";

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

	private readonly List<string> _stagePaths = [];

	private IniData? _appIniData;

	private string _baseDirectory = string.Empty;

	private bool? _isPortable;
	private string _modDirectory = string.Empty;
	private string? _modListTxtPath;
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
		AppVersionString = Utilities.GetFileVersion(AppPath, true);
		AppVersion = AppVersionString == null ? null : new Version(AppVersionString);
		Refresh();
	}
#pragma warning restore CS0162 // Unreachable code detected
#pragma warning restore IDE0079 // Remove unnecessary suppression

	public ModOrganizer(ProcessModule? processModule)
	{
		ArgumentNullException.ThrowIfNull(processModule);
		AppPath = processModule.FileName;
		AppFolder = Directory.GetParent(AppPath)!;
		AppVersionString = Utilities.GetFileVersion(AppPath, true);
		AppVersion = AppVersionString == null ? null : new Version(AppVersionString);
		var ver = processModule.FileVersionInfo.FileVersion;
		if (ver != null && ver.Contains("-ML"))
			//_moddingLinked = true;
			AppVersionString = ver;

		Refresh();
	}

	public DirectoryInfo AppFolder { get; }

	private string CurrentInstance
	{
		get
		{
			// TODO: Handling for ERROR return value.
			if (field != string.Empty) return field;

			using var key = Registry.CurrentUser.OpenSubKey(MO2RegistryPath);
			if (key == null)
			{
				ErrorReporter.Current.Critical(typeof(NullReferenceException),
					$"Registry key missing:\n{MO2RegistryPath}");
				return "ERROR";
			}

			var value = key.GetValue("CurrentInstance");
			if (value == null)
			{
				ErrorReporter.Current.Critical(typeof(NullReferenceException),
					$"Registry key missing:\n{MO2RegistryPath}\\CurrentInstance");
				return "ERROR";
			}

			field = (string)value;
			return field;
		}

		set;
	} = string.Empty;

	private bool IsPortable
	{
		get
		{
			if (_isPortable != null) return _isPortable.Value;

			_isPortable = File.Exists(Path.Combine(AppFolder.FullName, "portable.txt")) ||
			              CurrentInstance == string.Empty;
			return _isPortable.Value;
		}
	}

	public string AppName { get; } = "Mod Organizer";
	public string AppPath { get; }
	public Version? AppVersion { get; }
	public string? AppVersionString { get; }
	public string? GamePath { get; private set; }

	public string? Profile { get; private set; }

	//public string? StagePath { get; private set; }
	public string GameIniFolderPath { get; private set; } = string.Empty;
	public string PluginsTxtPath { get; private set; } = string.Empty;
	public IReadOnlyList<string> StagePaths => _stagePaths;
	public IReadOnlyList<string> SkipFileSuffixes { get; private set; } = [".mohidden"];
	public IReadOnlyList<string> SkipDirectories { get; private set; } = [".git"];

	/// <summary>
	///     Rebuilds MO2's virtual file system view from disk, so whether the VFS is present isn't relevant.
	///     Everything in the game folder outside of Data is assumed to be real.
	/// </summary>
	public FileOrigin? Resolve(string path)
	{
		// TODO: Handle Root Builder
		if (GamePath == null ||
		    !Utilities.TryGetGameRelativePath(GamePath, path, out var fullPath, out var relativePath))
			return null;

		const string dataPrefix = @"Data\";
		if (!relativePath.StartsWith(dataPrefix, StringComparison.OrdinalIgnoreCase))
			return File.Exists(fullPath) ? new FileOrigin(FileOriginKind.GameFolder, fullPath) : null;

		var dataRelativePath = relativePath[dataPrefix.Length..];
		if (_overwriteDirectory.Length == 0)
			return File.Exists(fullPath) ? new FileOrigin(FileOriginKind.GameFolder, fullPath) : null;

		var overwriteFull = Path.GetFullPath(_overwriteDirectory);
		// StagePaths is lowest priority first with Overwrite last, so the highest priority mod is at the end.
		for (var i = StagePaths.Count - 1; i >= 0; i--)
		{
			var candidate = Path.Combine(StagePaths[i], dataRelativePath);
			if (!File.Exists(candidate)) continue;

			var isOverwrite = string.Equals(Path.GetFullPath(StagePaths[i]), overwriteFull,
				StringComparison.OrdinalIgnoreCase);
			return new FileOrigin(
				isOverwrite ? FileOriginKind.Overwrite : FileOriginKind.Mod,
				candidate,
				ModName: Path.GetFileName(StagePaths[i]),
				ModFolder: StagePaths[i],
				Priority: i);
		}

		return File.Exists(fullPath) ? new FileOrigin(FileOriginKind.GameFolder, fullPath) : null;
	}

	private void Refresh()
	{
		GamePath = null;
		Profile = null;
		//StagePath = null;
		GameIniFolderPath = string.Empty;
		PluginsTxtPath = string.Empty;
		_stagePaths.Clear();
		_modListTxtPath = null;
		_appIniData = null;
		//_profileIniData = null;
		CurrentInstance = string.Empty;
		_isPortable = null;

		_baseDirectory = string.Empty;
		_modDirectory = string.Empty;
		_overwriteDirectory = string.Empty;
		_profilesDirectory = string.Empty;
		SkipFileSuffixes = [".mohidden"];
		SkipDirectories = [".git"];
		//_profile_local_inis = true;
		//_profile_local_saves = false;
		//_profile_archive_invalidation = false;
		//_skip_file_suffixes = [];
		//_skip_directories = [];
		//_executable_blacklist = [];

		if (!ReadSettings()) return;

		GetStagePaths();
	}

	private bool ReadSettings()
	{
		var iniPath = IsPortable
			? Path.Combine(AppFolder.FullName, "ModOrganizer.ini")
			: Path.Combine(Computer.AppDataLocal, "ModOrganizer", CurrentInstance, "ModOrganizer.ini");

		// TODO: filenotfound
		FileIniDataParser parser = new(new IniDataParser(IniParserConfig));

		// TODO: Other encodings?
		_appIniData = parser.ReadFile(iniPath, Encoding.UTF8);
		// TODO: handle null. test if even actually possible?
		if (_appIniData == null)
		{
			ErrorReporter.Current.Critical(typeof(NullReferenceException), $"Unable to read file:\n{iniPath}");
			GameIniFolderPath = string.Empty;
			PluginsTxtPath = string.Empty;
			return false;
		}

		var general = _appIniData["General"];
		//selected_profile=@ByteArray(Default)
		Profile = FormatByteArray(general["selected_profile"] ?? "Default");

		//gamePath=@ByteArray(C:\\Program Files(x86)\\Steam\\steamapps\\common\\Fallout 4)
		var gamePath = general["gamePath"];
		GamePath = gamePath == null ? null : FormatByteArray(gamePath);

		var settings = _appIniData["Settings"];

		// TODO: Verify AppPath is correct here
		//base_directory=C:/Modding/MO2 Instances/FO4
		var baseDirectory = settings["base_directory"]?.Trim();
		if (baseDirectory == string.Empty) baseDirectory = null;

		_baseDirectory = baseDirectory ??
		                 (IsPortable ? AppPath : Path.Combine(Computer.AppDataLocal, "ModOrganizer", CurrentInstance));

		if (!Directory.Exists(_baseDirectory))
		{
			ErrorReporter.Current.Critical(typeof(DirectoryNotFoundException), _baseDirectory);
			GameIniFolderPath = string.Empty;
			PluginsTxtPath = string.Empty;
			return false;
		}

		_modDirectory = (settings["mod_directory"] ?? "%BASE_DIR%/mods").Replace("%BASE_DIR%", _baseDirectory);
		_overwriteDirectory =
			(settings["overwrite_directory"] ?? "%BASE_DIR%/overwrite").Replace("%BASE_DIR%", _baseDirectory);
		_profilesDirectory =
			(settings["profiles_directory"] ?? "%BASE_DIR%/profiles").Replace("%BASE_DIR%", _baseDirectory);


		// MO2's "Use profile-specific Game INI Files" (profile_local_inis, on by default).
		// When it's off, every profile shares the INIs in Documents instead of having its own copy.
		var profileLocalInis = !string.Equals(settings["profile_local_inis"]?.Trim(), "false",
			StringComparison.OrdinalIgnoreCase);
		GameIniFolderPath = profileLocalInis ? Path.Combine(_profilesDirectory, Profile) : Computer.DocumentsFallout4;
		PluginsTxtPath = Path.Combine(_profilesDirectory, Profile, "plugins.txt");

		// Comma separated. A missing key means MO2's defaults; an empty value means nothing is skipped.
		SkipFileSuffixes = ParseList(settings["skip_file_suffixes"], ".mohidden");
		SkipDirectories = ParseList(settings["skip_directories"], ".git");

		var modListTxtPath = Path.Combine(_profilesDirectory, Profile, "modlist.txt");
		if (!File.Exists(modListTxtPath))
		{
			ErrorReporter.Current.Critical(typeof(FileNotFoundException), modListTxtPath);
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

	private void GetStagePaths()
	{
		// TODO: Make slashes consistent?
		//Console.WriteLine(_modListTxtPath);
		// TODO: Encoding
		//StagePaths.Clear();
		foreach (var line in File.ReadLines(_modListTxtPath!, Encoding.UTF8).Reverse())
			if (line.StartsWith('+') && !line.EndsWith("_separator"))
			{
				var modFolder = line[1..];
				var modFolderPath = Path.Combine(_modDirectory, modFolder);
				if (Directory.Exists(modFolderPath)) _stagePaths.Add(modFolderPath);
			}

		_stagePaths.Add(_overwriteDirectory);
	}

	private static string[] ParseList(string? value, string fallback)
	{
		return
		[
			.. (value ?? fallback).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.Select(s => s.ToLowerInvariant()),
		];
	}

	private static string FormatByteArray(string path)
	{
		path = path.Trim('"');
		if (path.StartsWith("@ByteArray(") && path.EndsWith(')')) path = path[11..^1];

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
