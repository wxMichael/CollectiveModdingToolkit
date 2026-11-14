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
using System.IO;
using CollectiveModdingToolkit.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectiveModdingToolkit.Managers;

internal class Unmanaged : ObservableObject, IModManager
{
	// Without a manager the game is always the registry install, which is also where Game looks; cached for this instance.
	private string? _gamePath;
	public string AppName { get; } = "Not Found";

	public string? AppPath { get; } = null;

	public Version? AppVersion { get; } = null;

	public string? AppVersionString { get; } = null;

	public string? GamePath { get; } = null;

	public string? Profile { get; } = null;

	//public string? StagePath { get; } = null;

	public string PluginsTxtPath { get; } = Path.Combine(Computer.AppDataLocal, "Fallout4", "plugins.txt");

	public string GameIniFolderPath { get; } = Computer.DocumentsFallout4;

	public IReadOnlyList<string> StagePaths { get; } = [];

	public IReadOnlyList<string> SkipFileSuffixes { get; } = [];

	public IReadOnlyList<string> SkipDirectories { get; } = [];

	/// <summary>Without a manager every file is assumed to be a game-folder file.</summary>
	public FileOrigin? Resolve(string path)
	{
		var gamePath = _gamePath ??= Game.FindInstallPathFromRegistry();
		if (gamePath == null || !Utilities.TryGetGameRelativePath(gamePath, path, out var fullPath, out _)) return null;

		return File.Exists(fullPath) ? new FileOrigin(FileOriginKind.GameFolder, fullPath) : null;
	}
}
