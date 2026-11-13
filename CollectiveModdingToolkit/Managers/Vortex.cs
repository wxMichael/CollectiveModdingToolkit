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
using System.Text.Json;
using System.Text.RegularExpressions;
using CollectiveModdingToolkit.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectiveModdingToolkit.Managers;

/// <summary>
///     Vortex writes a manifest (<c>vortex.deployment*.json</c>) into folders it deploys to, recording the source mod of
///     each deployed file.
/// </summary>
internal sealed partial class Vortex : ObservableObject, IModManager
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

	private readonly Dictionary<string, DeployedFile> _deployed = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<string> _stagePaths = [];

	/// <param name="exePath">Full path of the running Vortex.exe.</param>
	public Vortex(string exePath)
	{
		AppPath = exePath;
		AppVersionString = Utilities.GetFileVersion(AppPath, true);
		AppVersion = AppVersionString == null ? null : new Version(AppVersionString);
		Refresh();
	}

	public string AppName { get; } = "Vortex";
	public string? AppPath { get; }
	public Version? AppVersion { get; }
	public string? AppVersionString { get; }
	public string? GamePath { get; private set; }
	public string? Profile { get; } = null;
	public string PluginsTxtPath { get; } = Path.Combine(Computer.AppDataLocal, "Fallout4", "plugins.txt");
	public string GameIniFolderPath { get; } = Computer.DocumentsFallout4;

	/// <summary>
	///     Staging folders of every deployed mod. Order is not meaningful.
	/// </summary>
	public IReadOnlyList<string> StagePaths => _stagePaths;

	public IReadOnlyList<string> SkipFileSuffixes { get; } = [".vortex_backup"];

	public IReadOnlyList<string> SkipDirectories { get; } = [];

	public FileOrigin? Resolve(string path)
	{
		if (GamePath == null ||
		    !Utilities.TryGetGameRelativePath(GamePath, path, out var fullPath, out var relativePath))
			return null;

		if (!_deployed.TryGetValue(relativePath, out var deployed))
			return File.Exists(fullPath) ? new FileOrigin(FileOriginKind.GameFolder, fullPath) : null;

		var modFolder = Path.Combine(deployed.StagingPath, deployed.Source);
		var sourcePath = Path.Combine(modFolder, deployed.RelativePath);
		// A manifest entry whose staged file has vanished is stale; trust what's actually on disk.
		if (File.Exists(sourcePath))
			return new FileOrigin(
				FileOriginKind.Mod,
				sourcePath,
				File.Exists(fullPath) ? fullPath : null,
				GetModName(deployed.Source),
				modFolder);

		return File.Exists(fullPath) ? new FileOrigin(FileOriginKind.GameFolder, fullPath) : null;
	}

	private void Refresh()
	{
		_deployed.Clear();
		_stagePaths.Clear();

		GamePath = Game.FindInstallPathFromRegistry();
		if (GamePath == null || !Directory.Exists(GamePath)) return;

		// Game root and Data.
		// TODO: Other mod types?
		foreach (var folder in new[] { GamePath, Path.Combine(GamePath, "Data") })
		{
			if (!Directory.Exists(folder)) continue;

			foreach (var manifestPath in Directory.EnumerateFiles(folder, "vortex.deployment*.json"))
				LoadManifest(manifestPath);
		}

		_stagePaths.AddRange(_deployed.Values
			.Select(f => Path.Combine(f.StagingPath, f.Source))
			.Distinct(StringComparer.OrdinalIgnoreCase));
	}

	// Older Vortex staging folders are "<name>-<nexus id>-<version parts>-<timestamp>".
	[GeneratedRegex(@"^(?<name>.+?)-\d+(?:-[0-9A-Za-z]+)+$")]
	private static partial Regex StagingFolderNameRegex();

	// Newer Vortex staging folders are "<name> <nexus id> <version> <yyyy-MM-ddTHH-mmZ> <mod slug>".
	[GeneratedRegex(@"^(?<name>.+?) \d+ \S+ \d{4}-\d\d-\d\dT\d\d-\d\dZ \S+$")]
	private static partial Regex NewStagingFolderNameRegex();

	private void LoadManifest(string manifestPath)
	{
		Manifest? manifest;
		try
		{
			using var stream = File.OpenRead(manifestPath);
			manifest = JsonSerializer.Deserialize<Manifest>(stream, JsonOptions);
		}
		catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
		{
			// Manifest is mangled or Vortex is rewriting; treat as game-folder files.
			Debug.WriteLine($"Unable to read Vortex manifest {manifestPath}: {e.Message}");
			return;
		}

		if (manifest?.Files == null || string.IsNullOrEmpty(manifest.StagingPath) ||
		    string.IsNullOrEmpty(manifest.TargetPath))
			return;

		// Paths in a manifest are relative to its target, not the game root.
		// The Data manifest has "Foo.esp"; the root manifest has "Data\Scripts\Foo.pex".
		var prefix = Path.GetRelativePath(GamePath!, manifest.TargetPath);
		if (prefix == ".")
			prefix = string.Empty;
		else if (prefix == ".." || prefix.StartsWith(@"..\", StringComparison.Ordinal) ||
		         Path.IsPathRooted(prefix)) return;

		foreach (var file in manifest.Files)
		{
			if (string.IsNullOrEmpty(file.RelPath) || string.IsNullOrEmpty(file.Source)) continue;

			var relativePath = file.RelPath.Replace('/', '\\');
			_deployed[Path.Combine(prefix, relativePath)] =
				new DeployedFile(manifest.StagingPath, file.Source, relativePath);
		}
	}

	private static string GetModName(string stagingFolderName)
	{
		var match = NewStagingFolderNameRegex().Match(stagingFolderName);
		if (!match.Success)
			match = StagingFolderNameRegex().Match(stagingFolderName);
		return match.Success ? match.Groups["name"].Value : stagingFolderName;
	}

	private readonly record struct DeployedFile(string StagingPath, string Source, string RelativePath);

	private sealed record Manifest(string? StagingPath, string? TargetPath, List<ManifestFile>? Files);

	private sealed record ManifestFile(string? RelPath, string? Source);
}
