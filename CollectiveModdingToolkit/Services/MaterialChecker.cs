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
using CollectiveModdingToolkit.Materials;
using CollectiveModdingToolkit.Models;

namespace CollectiveModdingToolkit.Services;

/// <summary>What the material checks need to know about the game's textures.</summary>
public interface ITextureLookup
{
	/// <summary>Whether a texture exists, loose or in a loaded archive. The path is relative to Data.</summary>
	bool Exists(string path);

	/// <summary>The texture's format. Null if it couldn't be read.</summary>
	TextureFacts? GetFacts(string path);
}

/// <summary>One problem found with a material.</summary>
public readonly record struct MaterialIssue(
	ProblemType Type,
	Severity Severity,
	string Summary,
	string? Solution = null);

/// <summary>
///     Checks BGSM/BGEM materials: that the file is what its name says, that it's in Fallout 4's format, and that the
///     textures in its slots exist and suit them.
/// </summary>
public static class MaterialChecker
{
	/// <summary>Problems that break rendering.</summary>
	private const Severity Error = Severity.Medium;

	/// <summary>Problems that work but are suboptimal or confusing.</summary>
	private const Severity Warning = Severity.Low;

	private const string ReportSolution = "[accent]Report it[/accent] to the mod author.";

	/// <summary>The most a material can reasonably be. Real ones are a few hundred bytes.</summary>
	public const int MaxSize = 1 << 20;

	/// <param name="relative">The material's path inside Data, e.g. "Materials\Foo\Bar.bgsm".</param>
	/// <param name="data">The whole file.</param>
	/// <param name="textures">Where to find the textures it names.</param>
	/// <param name="exists">Whether another file exists at a path relative to Data, in the same place as this one.</param>
	public static List<MaterialIssue> Check(string relative, ReadOnlySpan<byte> data, ITextureLookup textures,
		Func<string, bool>? exists = null)
	{
		List<MaterialIssue> issues = [];
		var fileName = Path.GetFileName(relative);
		var ext = Path.GetExtension(fileName).ToLowerInvariant();
		var header = MaterialLoader.Inspect(data);

		if (exists != null && header.Format != MaterialFormat.Empty && HasProperSibling(relative, exists))
		{
			var wrongContent = header.Format is MaterialFormat.Json or MaterialFormat.Unknown;
			var wrongExtension = !wrongContent && ext == ".json";
			if (wrongContent || wrongExtension)
			{
				issues.Add(new MaterialIssue(ProblemType.InvalidMaterial, Warning,
					"This file isn't a usable material, but a [accent]proper material with the same name[/accent] exists next to it. It's most likely an [warning]unused leftover[/warning] from development.",
					"[accent]Tell the mod author[/accent] they can remove it. It's harmless unless something points to this file."));
				return issues;
			}
		}

		switch (header.Format)
		{
			case MaterialFormat.Empty:
				issues.Add(new MaterialIssue(ProblemType.InvalidMaterial, Error,
					"This material is [bad]empty (0 bytes)[/bad].", ReportSolution));
				return issues;
			case MaterialFormat.Unknown:
				issues.Add(new MaterialIssue(ProblemType.InvalidMaterial, Error,
					"This file is [bad]not a material[/bad]. It doesn't have a [b]BGSM[/b]/[b]BGEM[/b] header and isn't JSON.",
					ReportSolution));
				return issues;
			case MaterialFormat.Json:
				issues.Add(JsonIssue(ext));
				break;
			default:
				CheckExtension(issues, ext, header.Kind);
				if (header.Version > MaterialFile.MaxFallout4Version)
				{
					issues.Add(WrongVersionIssue(header.Version));
					return issues;
				}

				break;
		}

		MaterialFile material;
		try
		{
			material = MaterialLoader.Parse(data, fileName);
		}
		catch (InvalidDataException e)
		{
			issues.Add(new MaterialIssue(ProblemType.InvalidMaterial, Error,
				$"This material might be corrupted. It [bad]could not be read[/bad]: {e.Message}",
				ReportSolution));
			return issues;
		}

		// A JSON material may still claim a newer game's version.
		if (!material.IsFallout4Format)
		{
			issues.Add(WrongVersionIssue(material.Version));
			return issues;
		}

		CheckTextures(issues, material, textures);
		return issues;
	}

	private static bool HasProperSibling(string relative, Func<string, bool> exists)
	{
		var stem = Path.ChangeExtension(relative, null);
		return (exists(stem + ".bgsm") && !relative.EndsWith(".bgsm", StringComparison.OrdinalIgnoreCase))
		       || (exists(stem + ".bgem") && !relative.EndsWith(".bgem", StringComparison.OrdinalIgnoreCase));
	}

	// Tested in-game; whatever the extension (.bgsm or .json), JSON text makes the mesh invisible or crashes the game.
	private static MaterialIssue JsonIssue(string ext) =>
		new(ProblemType.InvalidMaterial, Error,
			$"This [b]{ext}[/b] file contains [accent]JSON text[/accent] instead of the binary material format. Fallout 4 [bad]can't load it[/bad]. The mesh will be invisible or the game may crash.",
			"[accent]Open it in the Materials tab and save it[/accent] to convert it to a binary material. The file extension doesn't matter; the game reads the material from its contents, not its name.");

	// Tested in-game; version 3 makes the mesh invisible, and versions 10 and 20 crash the game.
	private static MaterialIssue WrongVersionIssue(uint? version) =>
		new(ProblemType.InvalidMaterial, Error,
			$"This material is [bad]version {version}[/bad], which is for [accent]Fallout 76 (or a newer game)[/accent]. Fallout 4 only supports versions up to [accent]{MaterialFile.MaxFallout4Version}[/accent], and will show an invisible mesh or crash.",
			"[accent]Convert it[/accent] to Fallout 4's format in the Materials tab, or [accent]report it[/accent] to the mod author.");

	private static void CheckExtension(List<MaterialIssue> issues, string ext, MaterialKind? kind)
	{
		var actual = kind == MaterialKind.Bgem ? "BGEM (effect material)" : "BGSM (shader material)";
		var correct = kind == MaterialKind.Bgem ? ".bgem" : ".bgsm";
		switch (ext)
		{
			case ".bgsm" when kind != MaterialKind.Bgsm:
			case ".bgem" when kind != MaterialKind.Bgem:
				issues.Add(new MaterialIssue(ProblemType.InvalidMaterial, Error,
					$"This file has a [bad]{ext}[/bad] extension but contains a [accent]{actual}[/accent]. The game picks how to read a material from its extension, so the mesh will be invisible or render incorrectly.",
					$"[accent]Rename[/accent] it to [b]{correct}[/b] and make sure whatever uses it points to the new name."));
				break;
			case ".json":
				// Tested in-game; a BGSM named .json works. A .bgem name makes the game read a BGSM as an effect material,
				// so the game seems to treat everything that isn't .bgem as a BGSM. A BGEM named .json is untested but
				// would be read the wrong way round.
				issues.Add(kind == MaterialKind.Bgsm
					? new MaterialIssue(ProblemType.InvalidMaterial, Warning,
						$"This file is named [warning].json[/warning] but contains a binary [accent]{actual}[/accent]. The game reads it anyway, but it's the wrong name.",
						$"[accent]Rename[/accent] it to [b]{correct}[/b] and make sure whatever uses it points to the new name.")
					: new MaterialIssue(ProblemType.InvalidMaterial, Error,
						$"This file is named [bad].json[/bad] but contains a binary [accent]{actual}[/accent], which the game won't read correctly under that name.",
						$"[accent]Rename[/accent] it to [b]{correct}[/b] and make sure whatever uses it points to the new name."));
				break;
		}
	}

	private static void CheckTextures(List<MaterialIssue> issues, MaterialFile material, ITextureLookup textures)
	{
		List<(Severity Severity, string Line)> lines = [];
		foreach (var (field, slot, raw) in material.TextureSlots)
		{
			if (string.IsNullOrWhiteSpace(raw)) continue;

			var label = $"[b]{field.Label}[/b] ([i]{raw.Trim()}[/i])";
			var path = NormalizeTexturePath(raw);
			if (!path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
			{
				// Always an error as it's the wrong file type. Tested in-game; a .tga crashes, a .png loads with wrong colors.
				lines.Add((Error,
					$"{label}: [bad]isn't a DDS file[/bad]. The game can only load .dds textures, and may crash or show the wrong colors."));
				continue;
			}

			var exists = textures.Exists(path);
			if (!exists)
				// Always an error even where the game copes (a missing normal just looks flat, a missing diffuse is
				// magenta), a missing file means the author forgot it or used the wrong path.
				lines.Add((Error, $"{label}: [bad]texture not found[/bad] in Data, your mods, or any loaded archive."));

			// Tested in-game; wrong formats, slots, and cubemaps look odd but never crash or go invisible.
			foreach (var problem in TextureSlotRules.Evaluate(slot, path, exists ? textures.GetFacts(path) : null,
				         material.UsesAlpha))
				lines.Add((Warning, $"{label}: {Describe(problem)}"));
		}

		// Errors and warnings are separate results, so the ones that matter aren't buried among suggestions.
		AddTextureIssue(issues, ProblemType.MaterialTextures, Error, lines);
		AddTextureIssue(issues, ProblemType.MaterialTextureWarnings, Warning, lines);
	}

	private static void AddTextureIssue(List<MaterialIssue> issues, ProblemType type, Severity severity,
		List<(Severity Severity, string Line)> lines)
	{
		var matching = lines.Where(l => l.Severity == severity).Select(l => l.Line).ToList();
		if (matching.Count == 0) return;

		issues.Add(new MaterialIssue(type, severity, string.Join('\n', matching),
			type == ProblemType.MaterialTextures
				? "[accent]Report it[/accent] to the mod author. Check that the texture exists, is in the right slot, and is saved in the right format."
				: "These work in-game, but [accent]tell the mod author[/accent] if you think the texture is in the wrong slot or the wrong format."));
	}

	private static string Describe(TextureProblem problem) =>
		problem.Kind switch
		{
			TextureProblemKind.NotCubemap =>
				"an environment map must be a [accent]cubemap[/accent], but this [bad]isn't one[/bad].",
			TextureProblemKind.UnexpectedCubemap =>
				"this slot isn't for environment maps, but the texture [bad]is a cubemap[/bad].",
			TextureProblemKind.WrongFormat =>
				$"the texture is [bad]{problem.Detail}[/bad], which can't hold data this slot needs.",
			TextureProblemKind.NonStandardFormat =>
				$"the texture is [warning]{problem.Detail}[/warning] instead of [accent]BC5[/accent], which vanilla files use for this slot.",
			TextureProblemKind.UnneededAlpha =>
				$"the texture is [warning]{problem.Detail}[/warning] and has an [warning]alpha channel[/warning] this slot doesn't use. Vanilla files use [accent]BC5[/accent].",
			TextureProblemKind.MissingAlpha =>
				$"the material uses alpha, but the texture is [warning]{problem.Detail}[/warning] and has [warning]no alpha channel[/warning].",
			TextureProblemKind.SuffixMismatch =>
				$"the file name says it's a [warning]{problem.Detail}[/warning], which is a different kind of texture.",
			_ => "unknown problem.",
		};

	/// <summary>
	///     Turns a texture path from a material into a Data-relative path with backslashes. Fallout 4's materials leave
	///     out the leading "Textures\", and some tools add "Data\".
	/// </summary>
	public static string NormalizeTexturePath(string raw)
	{
		var path = raw.Trim().Replace('/', '\\').TrimStart('\\');
		if (path.StartsWith(@"data\", StringComparison.OrdinalIgnoreCase)) path = path[5..];

		return path.StartsWith(@"textures\", StringComparison.OrdinalIgnoreCase) ? path : @"textures\" + path;
	}
}
