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
using IniParser.Exceptions;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Microsoft.Win32;

namespace CollectiveModdingToolkit.Managers;

internal class ModOrganizer : ObservableObject, IModManager
{
	private const string MO2RegistryPath = @"Software\Mod Organizer Team\Mod Organizer";
	private const string SupportedGameName = "Fallout 4";

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

	private string? _modListTxtPath;
	private string _modDirectory = string.Empty;
	private string _overwriteDirectory = string.Empty;
	private string _profilesDirectory = string.Empty;

	/// <param name="exePath">Full path of the running ModOrganizer.exe.</param>
	public ModOrganizer(string exePath)
	{
		AppPath = exePath;
		AppFolder = Directory.GetParent(AppPath)!;
		AppVersionString = Utilities.GetFileVersion(AppPath, true);
		AppVersion = AppVersionString == null ? null : new Version(AppVersionString);

		// ModdingLinked builds add a "-ML" suffix to the version; show the full version for those.
		var fileVersion = FileVersionInfo.GetVersionInfo(AppPath).FileVersion;
		if (fileVersion != null && fileVersion.Contains("-ML")) AppVersionString = fileVersion;

		if (ReadSettings()) GetStagePaths();
	}

	public DirectoryInfo AppFolder { get; }

	public string AppName { get; } = "Mod Organizer";
	public string AppPath { get; }
	public Version? AppVersion { get; }
	public string? AppVersionString { get; }
	public string? GamePath { get; private set; }

	public string? Profile { get; private set; }

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

	/// <summary>
	///     Finds the ModOrganizer.ini of the running instance, the same way MO2 does: a portable.txt next to the exe
	///     forces the portable instance; otherwise the registry's current global instance, falling back to portable.
	/// </summary>
	/// <returns>The ini path, or null after reporting why it couldn't be found.</returns>
	private string? FindIniPath()
	{
		var portableIni = Path.Combine(AppFolder.FullName, "ModOrganizer.ini");

		if (File.Exists(Path.Combine(AppFolder.FullName, "portable.txt")))
		{
			if (File.Exists(portableIni)) return portableIni;

			ErrorReporter.Current.Critical(message:
				$"Mod Organizer is set up as portable (portable.txt exists) but has no ModOrganizer.ini:\n{AppFolder.FullName}");
			return null;
		}

		string? instance;
		using (var key = Registry.CurrentUser.OpenSubKey(MO2RegistryPath))
			instance = key?.GetValue("CurrentInstance") as string;

		if (!string.IsNullOrEmpty(instance))
		{
			var instanceIni = Path.Combine(Computer.AppDataLocal, "ModOrganizer", instance, "ModOrganizer.ini");
			if (File.Exists(instanceIni)) return instanceIni;
		}

		if (File.Exists(portableIni)) return portableIni;

		ErrorReporter.Current.Critical(message: string.IsNullOrEmpty(instance)
			? $"Unable to find ModOrganizer.ini for this Mod Organizer instance:\n{AppFolder.FullName}"
			: $"Unable to find ModOrganizer.ini for the Mod Organizer instance \"{instance}\".");
		return null;
	}

	private bool ReadSettings()
	{
		var iniPath = FindIniPath();
		if (iniPath == null) return false;

		FileIniDataParser parser = new(new IniDataParser(IniParserConfig));

		IniParser.Model.IniData iniData;
		try
		{
			// TODO: Other encodings?
			iniData = parser.ReadFile(iniPath, Encoding.UTF8);
		}
		catch (Exception e) when (e is ParsingException or IOException or UnauthorizedAccessException)
		{
			Log.Error($"Unable to read {iniPath}", e);
			ErrorReporter.Current.Critical(message: $"Unable to read ModOrganizer.ini:\n{iniPath}\n\n{e.Message}");
			return false;
		}

		var general = iniData["General"];

		//gameName=Fallout 4
		var gameName = general["gameName"]?.Trim();
		if (!string.IsNullOrEmpty(gameName) && gameName != SupportedGameName)
		{
			ErrorReporter.Current.Critical(message:
				$"Only {SupportedGameName} is supported, but this Mod Organizer instance is for \"{gameName}\".\n\n{iniPath}");
			return false;
		}

		//selected_profile=@ByteArray(Default)
		var selectedProfile = general["selected_profile"];
		Profile = selectedProfile == null ? null : FormatByteArray(selectedProfile);
		if (string.IsNullOrWhiteSpace(Profile))
		{
			Profile = null;
			ErrorReporter.Current.Critical(message:
				$"No profile is selected in ModOrganizer.ini. Open Mod Organizer, pick a profile, and try again.\n\n{iniPath}");
			return false;
		}

		//gamePath=@ByteArray(C:\\Program Files(x86)\\Steam\\steamapps\\common\\Fallout 4)
		var gamePath = general["gamePath"];
		GamePath = gamePath == null ? null : FormatByteArray(gamePath);

		var settings = iniData["Settings"];

		//base_directory=C:/Modding/MO2 Instances/FO4
		// Defaults to the folder holding ModOrganizer.ini: the MO2 folder when portable, the instance folder otherwise.
		var baseDirectory = settings["base_directory"]?.Trim();
		if (string.IsNullOrEmpty(baseDirectory)) baseDirectory = Path.GetDirectoryName(iniPath)!;

		if (!Directory.Exists(baseDirectory))
		{
			ErrorReporter.Current.Critical(typeof(DirectoryNotFoundException), baseDirectory);
			return false;
		}

		_modDirectory = (settings["mod_directory"] ?? "%BASE_DIR%/mods").Replace("%BASE_DIR%", baseDirectory);
		_overwriteDirectory =
			(settings["overwrite_directory"] ?? "%BASE_DIR%/overwrite").Replace("%BASE_DIR%", baseDirectory);
		_profilesDirectory =
			(settings["profiles_directory"] ?? "%BASE_DIR%/profiles").Replace("%BASE_DIR%", baseDirectory);

		var profileDirectory = Path.Combine(_profilesDirectory, Profile);

		// MO2's "Use profile-specific Game INI Files" is stored per profile as LocalSettings in the profile's
		// settings.ini. The instance-wide profile_local_inis (on by default) only applies when the profile hasn't
		// set it. When it's off, the profile uses the INIs in Documents instead of its own copy.
		var profileLocalInis = ParseBool(
			ReadProfileSetting(profileDirectory, "LocalSettings") ?? settings["profile_local_inis"], true);
		GameIniFolderPath = profileLocalInis ? profileDirectory : Computer.DocumentsFallout4;
		PluginsTxtPath = Path.Combine(profileDirectory, "plugins.txt");

		// Comma separated. A missing key means MO2's defaults; an empty value means nothing is skipped.
		SkipFileSuffixes = ParseList(settings["skip_file_suffixes"], ".mohidden");
		SkipDirectories = ParseList(settings["skip_directories"], ".git");

		var modListTxtPath = Path.Combine(profileDirectory, "modlist.txt");
		if (!File.Exists(modListTxtPath))
		{
			ErrorReporter.Current.Critical(typeof(FileNotFoundException), modListTxtPath);
			return false;
		}

		_modListTxtPath = modListTxtPath;
		return true;
	}

	private void GetStagePaths()
	{
		// TODO: Encoding
		foreach (var line in File.ReadLines(_modListTxtPath!, Encoding.UTF8).Reverse())
			if (line.StartsWith('+') && !line.EndsWith("_separator"))
			{
				var modFolder = line[1..];
				var modFolderPath = Path.Combine(_modDirectory, modFolder);
				if (Directory.Exists(modFolderPath)) _stagePaths.Add(modFolderPath);
			}

		_stagePaths.Add(_overwriteDirectory);
	}

	/// <summary>Reads a [General] value from a profile's settings.ini, which MO2 creates with the profile.</summary>
	/// <returns>The value, or null if the file or key is missing or unreadable.</returns>
	private static string? ReadProfileSetting(string profileDirectory, string key)
	{
		var path = Path.Combine(profileDirectory, "settings.ini");
		if (!File.Exists(path)) return null;

		try
		{
			return new FileIniDataParser(new IniDataParser(IniParserConfig)).ReadFile(path, Encoding.UTF8)["General"][key];
		}
		catch (Exception e) when (e is ParsingException or IOException or UnauthorizedAccessException)
		{
			Log.Warning($"Unable to read {path}: {e.Message}");
			return null;
		}
	}

	/// <summary>Converts a QSettings value to a bool the way QVariant does: "", "0", and "false" are false.</summary>
	private static bool ParseBool(string? value, bool fallback)
	{
		if (value == null) return fallback;

		value = value.Trim();
		return value.Length != 0 && value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
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
