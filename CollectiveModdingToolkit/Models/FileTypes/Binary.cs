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

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GV = CollectiveModdingToolkit.Models.GameVersion;

namespace CollectiveModdingToolkit.Models.FileTypes;

public class Binary : ObservableObject, IFile
{
	internal const string MainExeName = "Fallout4.exe";
	private static readonly string Archive2Path = Path.Combine("Tools", "Archive2", "Archive2.exe");

	/// <summary>
	///     Known versions of each base game file, and which game version each belongs to.<br />
	///     CRC32 is used for files without usable version info.<br />
	///     Keys are paths relative to the game folder.
	/// </summary>
	internal static readonly Dictionary<string, Dictionary<string, string>> BaseFiles = new(System.StringComparer.OrdinalIgnoreCase)
	{
		["Fallout4.exe"] = new Dictionary<string, string>
		{
			["1.10.120.0"] = GV.Obsolete,
			["1.10.130.0"] = GV.Obsolete,
			["1.10.138.0"] = GV.Obsolete,
			["1.10.162.0"] = GV.Obsolete,
			["1.10.163.0"] = GV.OldGen,
			["1.10.980.0"] = GV.Obsolete,
			["1.10.984.0"] = GV.NextGen,
			["1.11.137.0"] = GV.Obsolete,
			["1.11.159.0"] = GV.Obsolete,
			["1.11.169.0"] = GV.Obsolete,
			["1.11.191.0"] = GV.Obsolete,
			["1.11.221.0"] = GV.Obsolete,
			["1.11.240.0"] = GV.Anniversary,
		},
		["Fallout4Launcher.exe"] = new Dictionary<string, string>
		{
			["02445570"] = GV.OldGen, // 1.10.120 to 1.10.163
			["F6A06FF5"] = GV.NextGen, // 1.10.980 to 1.10.984
			["0E696744"] = GV.Obsolete, // 1.11.137
			["D15C6A49"] = GV.Obsolete, // 1.11.159
			["8C52BE93"] = GV.Obsolete, // 1.11.169a
			["591009C9"] = GV.Obsolete, // 1.11.169b
			["720BB9C3"] = GV.Obsolete, // 1.11.191
			["CA61EDD7"] = GV.Obsolete, // 1.11.221
			["57D1C794"] = GV.Anniversary, // 1.11.240
		},
		["steam_api64.dll"] = new Dictionary<string, string>
		{
			["2.89.45.4"] = GV.OldGen, // 1.10.120 to 1.10.163
			["7.40.51.27"] = GV.NextGenAnniversary, // 1.10.980 to 1.11.240
			["BD3AA35F"] = GV.OldGen, // GOG
		},
		["f4se_loader.exe"] =
			new Dictionary<string, string>
			{
				["0.0.6.23"] = GV.OldGen,
				["0.0.7.2"] = GV.NextGen,
				["0.0.7.4"] = GV.Obsolete,
				["0.0.7.5"] = GV.Obsolete,
				["0.0.7.6"] = GV.Obsolete,
				["0.0.7.7"] = GV.Obsolete,
				["0.0.7.8"] = GV.Obsolete,
				["0.0.7.9"] = GV.Anniversary,
			},
		["f4se_steam_loader.dll"] = new Dictionary<string, string> { ["0.0.6.23"] = GV.OldGen },
		["CreationKit.exe"] = new Dictionary<string, string>
		{
			["1.10.162.0"] = GV.OldGen,
			["1.10.943.1"] = GV.Obsolete,
			["1.10.982.3"] = GV.NextGen,
			["1.11.137.0"] = GV.Obsolete,
			["1.11.240.0"] = GV.Obsolete,
			["1.11.246.0"] = GV.Anniversary,
		},
		[Archive2Path] = new Dictionary<string, string>
		{
			["4CDFC7B5"] = GV.OldGen,
			["71A5240B"] = GV.NextGen,
			["C867674F"] = GV.Obsolete,
			["9A059211"] = GV.Obsolete,
			["B2EEF34A"] = GV.Anniversary,
		},
	};

	internal Binary(
		string name,
		string ext,
		string pathFull,
		string pathRelative,
		int priority = 0,
		string? modName = null,
		FileOrigin? origin = null)
	{
		Name = name;
		Ext = ext;
		PathFull = pathFull;
		PathRelative = pathRelative;
		// Known before reading, so the identified file is the one a mod manager actually provides.
		Origin = origin;
		Found = File.Exists(RealPath);
		if (Found)
		{
			Priority = priority == 0 ? 0 : priority + 1;
			Mod = modName;

			// Match on version first, then CRC32. Only hash the file if the version didn't identify it.
			var knownVersions = BaseFiles[PathRelative];
			var versionString = Utilities.GetFileVersion(RealPath);
			string? gameVersion = null;
			string? hash = null;
			if (versionString != null) knownVersions.TryGetValue(versionString, out gameVersion);

			if (gameVersion == null)
			{
				hash = Utilities.GetFileHash(RealPath);
				knownVersions.TryGetValue(hash, out gameVersion);
			}

			Version = versionString ?? hash;
			GameVersion = gameVersion ?? GV.Unknown;
		}
		else
		{
			GameVersion = GV.NotFound;
		}
	}

	[MemberNotNullWhen(true, nameof(Version), nameof(GameVersion))]
	private bool Found { get; }

	public bool IsGood { get; internal set; }
	public StatusLevel Level => IsGood ? StatusLevel.Good : StatusLevel.Bad;
	public string OriginText => Origin?.DisplayName ?? "Unknown";

	/// <summary>
	///     The real file on disk: the manager's source file when a mod replaced it (e.g. Vortex root mods), otherwise the
	///     game-folder path. Use for reading.
	/// </summary>
	internal string RealPath => Origin?.SourcePath ?? PathFull;

	/// <summary>
	///     The file's version number, or CRC32 if version is missing or unused.
	/// </summary>
	public string? Version { get; }

	/// <summary>
	///     The game version associated with <c>Version</c>.
	/// </summary>
	public string GameVersion { get; private set; }

	/// <summary>Shown while hovering, where <see cref="GameVersion" /> is shown otherwise.</summary>
	public string FullVersion => Version ?? GameVersion;

	public string Name { get; }
	public string Ext { get; }
	public string PathFull { get; }
	public string PathRelative { get; }

	public bool WasRead => IsReadable != null;

	[MemberNotNullWhen(true, nameof(Version))]
	public bool? IsReadable { get; private set; }

	public int Priority { get; private set; }

	//[MemberNotNullWhen(true, nameof(Mod))]
	public bool IsManaged => Mod != null;
	public string? Mod { get; private set; }

	public FileOrigin? Origin { get; private set; }

	/// <summary>
	///     Whether a base file is expected to exist for a given game version.
	///     e.g. <c>f4se_steam_loader.dll</c> only exists on Old-Gen, so its absence on Next-Gen is not a problem.
	/// </summary>
	internal static bool IsExpectedFor(string relativePath, string gameVersion)
	{
		var family = gameVersion == GV.DownGrade ? GV.OldGen : gameVersion;
		if (family is not (GV.OldGen or GV.NextGen or GV.Anniversary))
			return true; // Can't tell which files this game version should have, so report anything missing.

		return BaseFiles[relativePath].Values.Any(v =>
			v == family
			|| (v == GV.NextGenAnniversary && family is GV.NextGen or GV.Anniversary));
	}

	internal void SetOrigin(FileOrigin? origin)
	{
		Origin = origin;
		Mod = origin is { IsGameFolder: false } ? origin.ModName : null;
		Priority = origin?.Priority ?? 0;
	}

	/// <summary>
	///     Some files (e.g. steam_api64.dll) are shared by Next-Gen and Anniversary.<br />
	///     Once the game's own version is known, adopt it.
	/// </summary>
	internal void ResolveSharedVersion(string gameVersion)
	{
		if (GameVersion != GV.NextGenAnniversary) return;

		GameVersion = gameVersion;
		OnPropertyChanged(nameof(GameVersion));
		OnPropertyChanged(nameof(FullVersion));
	}
}
