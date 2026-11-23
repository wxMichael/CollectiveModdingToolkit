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

namespace CollectiveModdingToolkit.Materials;

/// <summary>The kinds of pixel data textures have, grouped by what matters for a material's slot.</summary>
public enum TextureFormatClass
{
	Unknown,
	Bc1,
	Bc2,
	Bc3,
	Bc4,
	Bc5,
	Bc6H,
	Bc7,

	/// <summary>Uncompressed, with color only.</summary>
	Rgb,

	/// <summary>Uncompressed, with color and alpha.</summary>
	Rgba,

	/// <summary>Uncompressed, one channel.</summary>
	SingleChannel,
}

/// <summary>What a material needs to know about the texture in one of its slots.</summary>
public readonly record struct TextureFacts(TextureFormatClass Format, bool IsCubemap)
{
	public bool HasAlpha => Format is TextureFormatClass.Bc2 or TextureFormatClass.Bc3 or TextureFormatClass.Bc7
		or TextureFormatClass.Rgba;

	public string FormatName => Format switch
	{
		TextureFormatClass.Bc1 => "BC1",
		TextureFormatClass.Bc2 => "BC2",
		TextureFormatClass.Bc3 => "BC3",
		TextureFormatClass.Bc4 => "BC4",
		TextureFormatClass.Bc5 => "BC5",
		TextureFormatClass.Bc6H => "BC6H",
		TextureFormatClass.Bc7 => "BC7",
		TextureFormatClass.Rgb => "uncompressed RGB",
		TextureFormatClass.Rgba => "uncompressed RGBA",
		TextureFormatClass.SingleChannel => "single-channel",
		_ => "unknown",
	};
}

public enum TextureProblemKind
{
	/// <summary>An environment map slot holds a texture that isn't a cubemap.</summary>
	NotCubemap,

	/// <summary>A slot that isn't for environment maps holds a cubemap.</summary>
	UnexpectedCubemap,

	/// <summary>The format can't hold what the slot needs, e.g. a single-channel normal map.</summary>
	WrongFormat,

	/// <summary>The format works but isn't the one vanilla textures use for this slot.</summary>
	NonStandardFormat,

	/// <summary>The texture has an alpha channel the slot doesn't use.</summary>
	UnneededAlpha,

	/// <summary>The material blends or tests alpha, but its diffuse texture has no alpha channel.</summary>
	MissingAlpha,

	/// <summary>The file name's suffix says it's for a different kind of slot.</summary>
	SuffixMismatch,
}

/// <summary>
///     A problem with a texture in a slot. <see cref="Detail" /> is the format name for format problems and the role
///     the suffix implies (e.g. "normal map") for <see cref="TextureProblemKind.SuffixMismatch" />.
/// </summary>
public readonly record struct TextureProblem(TextureProblemKind Kind, string? Detail = null);

/// <summary>What textures in each material slot should look like, based on vanilla textures.</summary>
public static class TextureSlotRules
{
	/// <summary>Maps a file name's suffix to the role Fallout 4 gives it. Null if it isn't a known suffix.</summary>
	public static (TextureSlotKind Slot, string Role)? RoleOfSuffix(string suffix) =>
		suffix switch
		{
			"d" => (TextureSlotKind.Diffuse, "diffuse map"),
			"n" or "msn" => (TextureSlotKind.Normal, "normal map"),
			"s" => (TextureSlotKind.SmoothSpec, "specular map"),
			"e" => (TextureSlotKind.Environment, "environment map"),
			_ => null,
		};

	/// <summary>The part of the file name after the last underscore, lowercase. Empty if there isn't one.</summary>
	public static string SuffixOf(string path)
	{
		var name = path[(path.LastIndexOfAny(['\\', '/']) + 1)..];
		var dot = name.LastIndexOf('.');
		if (dot >= 0) name = name[..dot];

		var underscore = name.LastIndexOf('_');
		return underscore >= 0 ? name[(underscore + 1)..].ToLowerInvariant() : string.Empty;
	}

	/// <summary>Finds what's wrong with a texture used in a slot.</summary>
	/// <param name="slot">The slot.</param>
	/// <param name="path">The path the material gives for the texture.</param>
	/// <param name="facts">The texture's format. Null if it couldn't be read, which skips the format checks.</param>
	/// <param name="materialUsesAlpha">Whether the material blends or tests alpha.</param>
	public static List<TextureProblem> Evaluate(TextureSlotKind slot, string path, TextureFacts? facts,
		bool materialUsesAlpha)
	{
		List<TextureProblem> problems = [];

		if (RoleOfSuffix(SuffixOf(path)) is { } role && IsStrictSlot(slot) && !Accepts(slot, role.Slot))
			problems.Add(new TextureProblem(TextureProblemKind.SuffixMismatch, role.Role));

		if (facts is not { } texture) return problems;

		if (slot == TextureSlotKind.Environment)
		{
			if (!texture.IsCubemap) problems.Add(new TextureProblem(TextureProblemKind.NotCubemap));

			return problems;
		}

		if (texture.IsCubemap)
		{
			problems.Add(new TextureProblem(TextureProblemKind.UnexpectedCubemap));
			return problems;
		}

		switch (slot)
		{
			case TextureSlotKind.Normal or TextureSlotKind.Wrinkles when SuffixOf(path) == "msn":
				// Model-space normals need three channels, so vanilla textures are BC1 or BC3, not BC5.
				if (texture.Format is TextureFormatClass.Bc2 or TextureFormatClass.Bc4 or TextureFormatClass.Bc5
				    or TextureFormatClass.Bc6H or TextureFormatClass.SingleChannel)
					problems.Add(new TextureProblem(TextureProblemKind.WrongFormat, texture.FormatName));

				break;
			case TextureSlotKind.Normal or TextureSlotKind.Wrinkles or TextureSlotKind.SmoothSpec
				or TextureSlotKind.Specular:
				// Vanilla tangent-space normal and specular maps are all BC5.
				switch (texture.Format)
				{
					case TextureFormatClass.Bc5 or TextureFormatClass.Unknown:
						break;
					case TextureFormatClass.Bc4 or TextureFormatClass.Bc6H or TextureFormatClass.SingleChannel:
						problems.Add(new TextureProblem(TextureProblemKind.WrongFormat, texture.FormatName));
						break;
					default:
						problems.Add(new TextureProblem(
							texture.HasAlpha ? TextureProblemKind.UnneededAlpha : TextureProblemKind.NonStandardFormat,
							texture.FormatName));
						break;
				}

				break;
			case TextureSlotKind.Diffuse or TextureSlotKind.Glow:
				if (texture.Format is TextureFormatClass.Bc4 or TextureFormatClass.Bc5 or TextureFormatClass.Bc6H
				    or TextureFormatClass.SingleChannel)
					problems.Add(new TextureProblem(TextureProblemKind.WrongFormat, texture.FormatName));
				else if (slot == TextureSlotKind.Diffuse && materialUsesAlpha &&
				         texture.Format is TextureFormatClass.Bc1 or TextureFormatClass.Rgb)
					problems.Add(new TextureProblem(TextureProblemKind.MissingAlpha, texture.FormatName));

				break;
		}

		return problems;
	}

	/// <summary>Slots whose textures are expected to be named for their purpose.</summary>
	private static bool IsStrictSlot(TextureSlotKind slot) =>
		slot is TextureSlotKind.Diffuse or TextureSlotKind.Normal or TextureSlotKind.SmoothSpec
			or TextureSlotKind.Specular or TextureSlotKind.Environment or TextureSlotKind.Wrinkles;

	private static bool Accepts(TextureSlotKind slot, TextureSlotKind role) =>
		slot == role || (slot == TextureSlotKind.Wrinkles && role == TextureSlotKind.Normal) ||
		(slot == TextureSlotKind.Specular && role == TextureSlotKind.SmoothSpec);
}
