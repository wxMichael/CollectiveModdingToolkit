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

//using Avalonia.Collections;
//using CollectiveModdingToolkit.Models.FileTypes;

using System;
using System.Collections.Generic;
using CollectiveModdingToolkit.Models;

namespace CollectiveModdingToolkit.Managers;

public interface IModManager
{
	string AppName { get; }
	string? AppPath { get; }
	Version? AppVersion { get; }
	string? AppVersionString { get; }
	string? GamePath { get; }

	string? Profile { get; }

	//string? StagePath { get; }
	string PluginsTxtPath { get; }

	//string? ModListTxtPath { get; }
	string GameIniFolderPath { get; }

	/// <summary>
	///     Mod folders. MO2: lowest priority first, Overwrite last. Vortex: unordered. Unmanaged: empty. Only
	///     meaningful as a priority order for MO2.
	/// </summary>
	IReadOnlyList<string> StagePaths { get; }

	/// <summary>Lowercase file name endings the manager ignores (MO2: <c>skip_file_suffixes</c>; Vortex: its backups).</summary>
	IReadOnlyList<string> SkipFileSuffixes { get; }

	/// <summary>Lowercase folder names the manager ignores anywhere in a mod (MO2: <c>skip_directories</c>).</summary>
	IReadOnlyList<string> SkipDirectories { get; }

	/// <summary>
	///     Determines whether a file in the game folder comes from a managed mod or from the game folder itself.
	/// </summary>
	/// <param name="path">Absolute path, or path relative to the game folder (e.g. <c>Data\Fallout4.esm</c>).</param>
	/// <returns>Null if the path is outside the game folder or the file does not exist.</returns>
	FileOrigin? Resolve(string path);
}
