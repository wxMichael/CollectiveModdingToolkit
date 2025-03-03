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

using CollectiveModdingToolkit.Models.FileTypes;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace CollectiveModdingToolkit;

public partial class Binary : ObservableObject, IFile
{
	internal const string NO_FILE = "NO_FILE";
	internal static readonly string mainExeName = "Fallout4.exe";
	private static readonly string _archive2Path = Path.Combine("Tools", "Archive2", "Archive2.exe");
	private static readonly string _archive2InteropPath = Path.Combine("Tools", "Archive2", "Archive2Interop.dll");
	internal static readonly Dictionary<string, string> binariesOG = new()
	{
		{ "Fallout4.exe", "1.10.163.0" },
		{ "Fallout4Launcher.exe", "02445570" },
		{ "steam_api64.dll", "2.89.45.4" },
		{ "f4se_loader.exe", "0.0.6.23" },
		{ "f4se_steam_loader.dll", "0.0.6.23" },
		{ "CreationKit.exe", "1.10.162.0" },
		{ _archive2Path, "1.1.0.4" },
		{ _archive2InteropPath, "850D36A9" },
	};
	internal static readonly Dictionary<string, string> binariesNG = new()
	{
		{ "Fallout4.exe", "1.10.984.0" },
		{ "Fallout4Launcher.exe", "F6A06FF5" },
		{ "steam_api64.dll", "7.40.51.27" },
		{ "f4se_loader.exe", "0.0.7.2" },
		{ "f4se_steam_loader.dll", NO_FILE },
		{ "CreationKit.exe", "1.10.982.3" },
		{ _archive2Path, "1.1.0.5" },
		{ _archive2InteropPath, "EFBE3622" },
	};

	public string Name { get; private set; }
	public string Ext { get; private set; }
	public string PathFull { get; private set; }
	public string PathRelative { get; private set; }

	[MemberNotNullWhen(true, nameof(Version), nameof(GameVersion))]
	public bool Found { get; private set; }
	public bool IsGood { get; internal set; }

	public bool WasRead => IsReadable != null;
	[MemberNotNullWhen(true, nameof(Version))]
	public bool? IsReadable { get; private set; }

	public int Priority { get; private set; }
	//[MemberNotNullWhen(true, nameof(Mod))]
	public bool IsManaged => Mod != null;
	public string? Mod { get; private set; }

	/// <summary>
	/// The file's version number, or CRC32 if version is missing or unused.
	/// </summary>
	public string? Version { get; private set; }

	/// <summary>
	/// The game version associated with <c>Version</c>.
	/// </summary>
	public string GameVersion { get; private set; }

	[ObservableProperty] public partial string DisplayText { get; set; }

	internal Binary(
	string name,
	string ext,
	string pathFull,
	string pathRelative,
	int priority = 0,
	string? modName = null)
	{
		Name = name;
		Ext = ext;
		PathFull = pathFull;
		PathRelative = pathRelative ?? name;
		Found = File.Exists(PathFull);
		if (Found)
		{
			Priority = priority == 0 ? 0 : priority + 1;
			Mod = modName;
			bool hasVersion = binariesOG.TryGetValue(PathRelative, out string? version);
			Version = Utilities.GetFileVersionOrHash(PathFull, hashOnly: hasVersion && version != null && !version.Contains('.'));
			GameVersion = hasVersion && Version == version
				? Models.GameVersion.OldGen
				: binariesNG.TryGetValue(PathRelative, out version) && Version == version
					? Models.GameVersion.NextGen
					: Models.GameVersion.Unknown;
		}
		else
		{
			GameVersion = Models.GameVersion.NotFound;
		}

		DisplayText = GameVersion;
	}
}
