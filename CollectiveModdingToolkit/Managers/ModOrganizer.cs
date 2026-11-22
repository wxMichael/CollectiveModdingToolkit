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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;

namespace CollectiveModdingToolkit.Managers;

internal class ModOrganizer : ObservableObject, IModManager
{
	private const string MO2RegistryPath = @"Software\Mod Organizer Team\Mod Organizer";
	private const string SupportedGameName = "Fallout 4";
	private const string DefaultProfileName = "Default";

	/// <summary>
	///     The instance MO2 was told to use on its command line, or null if none. An empty string means the portable instance.
	/// </summary>
	private readonly string? _launchInstance;

	private readonly DateTime? _launchTimeUtc;

	private readonly List<string> _stagePaths = [];
	private string _modDirectory = string.Empty;

	private string? _modListTxtPath;
	private string _overwriteDirectory = string.Empty;
	private string _profilesDirectory = string.Empty;

	/// <param name="exePath">Full path of the running ModOrganizer.exe.</param>
	/// <param name="process">The running MO2 process, used to read the instance it was launched with.</param>
	public ModOrganizer(string exePath, Process? process = null)
	{
		Log.Debug($"MO2: starting setup for {exePath}");
		AppPath = exePath;
		if (process != null)
		{
			if (NtQueryInformationProcessWrapper.GetCommandLineArguments(process.Handle) is { } arguments)
				_launchInstance = GetInstanceArgument(arguments);
			else
				Log.Warning("Unable to read Mod Organizer's command line");

			try
			{
				_launchTimeUtc = process.StartTime.ToUniversalTime();
			}
			catch (Exception e) when (e is Win32Exception or InvalidOperationException)
			{
				Log.Warning($"Unable to read when Mod Organizer started: {e.Message}");
			}
		}

		AppFolder = Directory.GetParent(AppPath)!;
		AppVersionString = Utilities.GetFileVersion(AppPath, true);
		AppVersion = AppVersionString == null ? null : new Version(AppVersionString);

		// ModdingLinked builds add a "-ML" suffix to the version; show the full version for those.
		var fileVersion = FileVersionInfo.GetVersionInfo(AppPath).FileVersion;
		if (fileVersion != null && fileVersion.Contains("-ML")) AppVersionString = fileVersion;

		Log.Debug("MO2: reading settings");
		if (ReadSettings())
		{
			Log.Debug($"MO2: profile \"{Profile}\", game at {GamePath}; reading mod list");
			GetStagePaths();
			Log.Debug($"MO2: {_stagePaths.Count} mod folders");
		}
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

	public string? OverwritePath => _overwriteDirectory.Length > 0 ? _overwriteDirectory : null;
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
	///     forces the portable instance; otherwise the instance from MO2's command line, then the registry's current
	///     global instance, or the portable instance when no global instance is set.
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
		DateTime? registryWritten;
		using (var key = Registry.CurrentUser.OpenSubKey(MO2RegistryPath))
		{
			instance = key?.GetValue("CurrentInstance") as string;
			registryWritten = key == null ? null : RegistryKeyInfo.GetLastWriteTimeUtc(key);
		}

		// MO2 uses an instance from its command line instead of the registry's, but forgets it when it restarts
		// itself. Picking another instance writes the registry, so once the registry has been written since MO2
		// started, the registry is right. A restart to apply settings writes nothing and can't be detected.
		if (_launchInstance != null && !(registryWritten > _launchTimeUtc))
		{
			Log.Info($"Using the Mod Organizer instance from its command line: \"{_launchInstance}\"");
			instance = _launchInstance;
		}

		if (string.IsNullOrEmpty(instance))
		{
			if (File.Exists(portableIni)) return portableIni;

			ErrorReporter.Current.Critical(message:
				$"Unable to find ModOrganizer.ini for this Mod Organizer instance:\n{AppFolder.FullName}");
			return null;
		}

		// MO2 never falls back to the portable instance when a global one is named.
		var instanceIni = Path.Combine(Computer.AppDataLocal, "ModOrganizer", instance, "ModOrganizer.ini");
		if (File.Exists(instanceIni)) return instanceIni;

		ErrorReporter.Current.Critical(message:
			$"Unable to find ModOrganizer.ini for the Mod Organizer instance \"{instance}\":\n{instanceIni}");
		return null;
	}

	private bool ReadSettings()
	{
		var iniPath = FindIniPath();
		if (iniPath == null) return false;
		Log.Debug($"MO2: using {iniPath}");

		QSettingsIni ini;
		try
		{
			ini = QSettingsIni.Read(iniPath);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			Log.Error($"Unable to read {iniPath}", e);
			ErrorReporter.Current.Critical(message: $"Unable to read ModOrganizer.ini:\n{iniPath}\n\n{e.Message}");
			return false;
		}

		//gameName=Fallout 4
		var gameName = ini.GetString("General", "gameName")?.Trim();
		if (!string.IsNullOrEmpty(gameName) && gameName != SupportedGameName)
		{
			ErrorReporter.Current.Critical(message:
				$"Only {SupportedGameName} is supported, but this Mod Organizer instance is for \"{gameName}\".\n\n{iniPath}");
			return false;
		}

		//selected_profile=@ByteArray(Default)
		// MO2 uses its default profile when none is selected.
		Profile = ini.GetString("General", "selected_profile");
		if (string.IsNullOrWhiteSpace(Profile)) Profile = DefaultProfileName;

		//gamePath=@ByteArray(C:\\Program Files (x86)\\Steam\\steamapps\\common\\Fallout 4)
		GamePath = ini.GetString("General", "gamePath");

		//base_directory=C:/Modding/MO2 Instances/FO4
		// Defaults to the folder holding ModOrganizer.ini: the MO2 folder when portable, the instance folder otherwise.
		var baseDirectory = ini.GetString("Settings", "base_directory");
		if (string.IsNullOrEmpty(baseDirectory)) baseDirectory = Path.GetDirectoryName(iniPath)!;

		if (!Directory.Exists(baseDirectory))
		{
			ErrorReporter.Current.Critical(typeof(DirectoryNotFoundException), baseDirectory);
			return false;
		}

		_modDirectory = GetPath(ini, "mod_directory", "mods", baseDirectory);
		_overwriteDirectory = GetPath(ini, "overwrite_directory", "overwrite", baseDirectory);
		_profilesDirectory = GetPath(ini, "profiles_directory", "profiles", baseDirectory);

		var profileDirectory = Path.Combine(_profilesDirectory, Profile);

		// MO2's "Use profile-specific Game INI Files" is stored per profile as LocalSettings in the profile's
		// settings.ini. The instance-wide profile_local_inis (on by default) only applies when the profile hasn't
		// set it. When it's off, the profile uses the INIs in Documents instead of its own copy.
		var defaultLocalInis = ini.GetBool("Settings", "profile_local_inis", true);
		var profileLocalInis = ReadProfileSettings(profileDirectory)?.GetBool("General", "LocalSettings",
			defaultLocalInis) ?? defaultLocalInis;
		GameIniFolderPath = profileLocalInis ? profileDirectory : Computer.DocumentsFallout4;
		PluginsTxtPath = Path.Combine(profileDirectory, "plugins.txt");

		// A missing key means MO2's defaults; an empty list means nothing is skipped.
		SkipFileSuffixes = GetList(ini, "skip_file_suffixes", ".mohidden");
		SkipDirectories = GetList(ini, "skip_directories", ".git");

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
			if (line.StartsWith('+') && !line.EndsWith("_separator", StringComparison.OrdinalIgnoreCase))
			{
				var modFolder = line[1..];
				var modFolderPath = Path.Combine(_modDirectory, modFolder);
				if (Directory.Exists(modFolderPath)) _stagePaths.Add(modFolderPath);
			}

		_stagePaths.Add(_overwriteDirectory);
	}

	/// <summary>
	///     Finds the instance in MO2's command line the way MO2 does (boost::program_options).
	///     "-i Name", "-iName", "--instance Name", or "--instance=Name", where a following option means no value was given.
	///     A "moshortcut://Instance:Executable" first argument overrides "-i".
	/// </summary>
	/// <param name="arguments">The command line, starting with the program.</param>
	/// <returns>The instance, an empty string for the portable instance, or null if none was given.</returns>
	internal static string? GetInstanceArgument(IReadOnlyList<string> arguments)
	{
		const string shortcutPrefix = "moshortcut://";

		string? instance = null;
		string? firstPositional = null;
		var onlyPositionals = false;

		for (var i = 1; i < arguments.Count; i++)
		{
			var argument = arguments[i];

			if (onlyPositionals || argument.Length < 2 || argument[0] != '-')
			{
				firstPositional ??= argument;
				continue;
			}

			if (argument == "--")
			{
				onlyPositionals = true;
			}
			else if (argument.StartsWith("--"))
			{
				var equals = argument.IndexOf('=');
				var name = equals < 0 ? argument[2..] : argument[2..equals];
				if (name == "instance")
				{
					if (equals >= 0) instance = argument[(equals + 1)..];
					else if (TakeValue(arguments, ref i) is { } value) instance = value;
				}
				else if (name == "profile" && equals < 0)
				{
					i++; // The profile's value is the next argument.
				}
			}
			else if (argument[1] == 'i')
			{
				if (argument.Length > 2) instance = argument[2..];
				else if (TakeValue(arguments, ref i) is { } value) instance = value;
			}
			else if (argument == "-p")
			{
				i++;
			}
		}

		if (firstPositional != null && firstPositional.StartsWith(shortcutPrefix))
		{
			var separator = firstPositional.IndexOf(':', shortcutPrefix.Length);
			if (separator >= 0) return firstPositional[shortcutPrefix.Length..separator];
		}

		return instance;

		// The optional value of -i/--instance may also be the next argument, unless that's another option.
		static string? TakeValue(IReadOnlyList<string> arguments, ref int i)
		{
			if (i + 1 >= arguments.Count) return null;

			var next = arguments[i + 1];
			if (next.Length > 1 && next[0] == '-') return null;

			i++;
			return next;
		}
	}

	/// <summary>Reads a profile's settings.ini, which MO2 creates with the profile.</summary>
	/// <returns>The settings, or null if the file is missing or unreadable.</returns>
	private static QSettingsIni? ReadProfileSettings(string profileDirectory)
	{
		var path = Path.Combine(profileDirectory, "settings.ini");
		if (!File.Exists(path)) return null;

		try
		{
			return QSettingsIni.Read(path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			Log.Warning($"Unable to read {path}: {e.Message}");
			return null;
		}
	}

	/// <summary>A configurable MO2 folder, defaulting to %BASE_DIR%/<paramref name="defaultName" />.</summary>
	private static string GetPath(QSettingsIni ini, string key, string defaultName, string baseDirectory) =>
		(ini.GetString("Settings", key) ?? $"%BASE_DIR%/{defaultName}").Replace("%BASE_DIR%", baseDirectory);

	private static string[] GetList(QSettingsIni ini, string key, string fallback) =>
	[
		.. (ini.GetStringList("Settings", key) ?? [fallback])
		.Select(s => s.Trim().ToLowerInvariant())
		.Where(s => s.Length != 0),
	];
}
