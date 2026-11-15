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

namespace CollectiveModdingToolkit.Models.FileTypes;

internal interface IFile
{
	/// <summary>The file name, including extension.</summary>
	string Name { get; }

	/// <summary>The path in the game folder (which may be virtual or linked when a mod manager is in use).</summary>
	string PathFull { get; }

	/// <summary>Null until read; then whether the file's contents were understood.</summary>
	bool? IsReadable { get; }

	/// <summary>The providing mod's name, or null if the file isn't from a mod.</summary>
	string? Mod { get; }

	/// <summary>Where this file really comes from (a mod, Overwrite, or the game folder). Null until resolved.</summary>
	FileOrigin? Origin { get; }
}

internal static class FileExtensions
{
	extension(IFile file)
	{
		/// <summary>
		///     The real file on disk: the manager's source file for mod files (correct with or without MO2's VFS),
		///     otherwise the game-folder path. Use for reading and editing.
		/// </summary>
		public string RealPath => file.Origin?.SourcePath ?? file.PathFull;
	}
}
