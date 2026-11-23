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
using System.Linq;
using System.Threading;
using CollectiveModdingToolkit.Materials;
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Models.FileTypes;

namespace CollectiveModdingToolkit.Services;

/// <summary>
///     Finds the textures the game would load: loose files from Data and mods, plus everything in the archives it loads
///     (including the base game's). Reads each texture's header at most once.
/// </summary>
internal sealed class TextureLookup : ITextureLookup
{
	private readonly Dictionary<string, (string Archive, Ba2Entry Entry)> _archived =
		new(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, TextureFacts?> _facts = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Scanner.Entry> _loose;
	private readonly Func<Scanner.Entry, string> _sourcePath;
	private readonly string _texturesPrefix = @"textures\";

	/// <param name="loose">Every loose file the game would see, keyed by path relative to Data.</param>
	/// <param name="sourcePath">Gets the real file for a loose file.</param>
	private TextureLookup(Dictionary<string, Scanner.Entry> loose, Func<Scanner.Entry, string> sourcePath)
	{
		_loose = loose;
		_sourcePath = sourcePath;
	}

	public bool Exists(string path) => _loose.ContainsKey(path) || _archived.ContainsKey(path);

	public TextureFacts? GetFacts(string path)
	{
		if (_facts.TryGetValue(path, out var cached)) return cached;

		var facts = Read(path);
		_facts[path] = facts;
		return facts;
	}

	/// <summary>Indexes the textures in every archive the game loads.</summary>
	public static TextureLookup Build(Game game, Dictionary<string, Scanner.Entry> loose,
		Func<Scanner.Entry, string> sourcePath, Action<string>? progress, CancellationToken token)
	{
		TextureLookup lookup = new(loose, sourcePath);
		// The archive loaded last wins, so go through them from the end.
		foreach (var name in Enumerable.Reverse(game.ArchivesLoadOrder))
		{
			token.ThrowIfCancellationRequested();
			if (!game.ArchivesWinning.TryGetValue(Game.ArchiveKey(name), out var archive)) continue;

			progress?.Invoke($"Indexing textures in {name}");
			try
			{
				foreach (var entry in Ba2Reader.Read(archive.RealPath, token))
				{
					var path = entry.Path.Replace('/', '\\');
					if (path.StartsWith(lookup._texturesPrefix, StringComparison.OrdinalIgnoreCase))
						lookup._archived.TryAdd(path, (archive.RealPath, entry));
				}
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
			{
				// Unsupported or damaged archives are reported by the archive checks, not here.
				Log.Warning($"Scanner: unable to index textures in {name}: {e.Message}");
			}
		}

		return lookup;
	}

	private TextureFacts? Read(string path)
	{
		try
		{
			// Loose files win over archives, as in the game.
			if (_loose.TryGetValue(path, out var loose))
			{
				var real = _sourcePath(loose);
				DDS dds = new(Path.GetFileName(real), real, null);
				dds.ReadHeader();
				return dds.IsReadable == true ? dds.Facts : null;
			}

			if (!_archived.TryGetValue(path, out var archived)) return null;

			var texture = archived.Entry;
			DDS header = new(Path.GetFileName(path), archived.Archive, null);
			header.ReadHeader(texture.IsTexture
				? Ba2Reader.BuildDdsHeader(texture)
				: Ba2Reader.ReadStart(archived.Archive, texture, 148));
			return header.IsReadable == true ? header.Facts : null;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
		{
			return null;
		}
	}
}
