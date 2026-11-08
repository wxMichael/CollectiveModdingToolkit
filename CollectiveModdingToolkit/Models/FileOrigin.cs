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

namespace CollectiveModdingToolkit.Models;

public enum FileOriginKind
{
	/// <summary>The file is in the game folder and is not deployed by a manager, or the manager cannot be determined.</summary>
	GameFolder,

	/// <summary>The file is deployed from a manager's mod staging area.</summary>
	Mod,

	/// <summary>(MO2-only) The file is in Mod Organizer 2's Overwrite folder.</summary>
	Overwrite,
}

/// <summary>
///     Where a file in the game's folder really comes from.
/// </summary>
/// <param name="Kind">Game folder, managed mod, or MO2 Overwrite.</param>
/// <param name="SourcePath">
///     The authoritative file on real disk. Use this for reading, editing and deleting.
///     For mods this is the staged file; for game-folder files it is the game-folder file itself.
/// </param>
/// <param name="DeployedPath">
///     (Vortex-only) The hard-linked file inside the game folder, if present.
///     It is the same data as <paramref name="SourcePath" />, so edits through either path change both.
///     Deleting this path only removes the link; Vortex will recreate it on the next deploy.
///     Always null for MO2, whose game-folder view is virtual.
/// </param>
/// <param name="ModName">Display name of the providing mod, or null for game-folder files.</param>
/// <param name="ModFolder">The mod's staging folder, or null for game-folder files.</param>
/// <param name="Priority">Load priority for MO2 (higher wins). Zero when not applicable.</param>
public sealed record FileOrigin(
	FileOriginKind Kind,
	string SourcePath,
	string? DeployedPath = null,
	string? ModName = null,
	string? ModFolder = null,
	int Priority = 0)
{
	/// <summary>Short label for UI: the mod's name, "Overwrite", or "Game Folder".</summary>
	public string DisplayName => Kind switch
	{
		FileOriginKind.Mod => ModName ?? "Mod",
		FileOriginKind.Overwrite => "Overwrite",
		_ => "Game Folder",
	};

	public bool IsGameFolder => Kind == FileOriginKind.GameFolder;
	public bool IsMod => Kind == FileOriginKind.Mod;
}
