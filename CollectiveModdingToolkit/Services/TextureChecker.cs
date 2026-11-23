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
using System.Numerics;
using System.Text.RegularExpressions;
using CollectiveModdingToolkit.Materials;
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Models.FileTypes;

// ReSharper disable InconsistentNaming

namespace CollectiveModdingToolkit.Services;

/// <summary>One problem found with a texture.</summary>
public readonly record struct TextureIssue(
	ProblemType Type,
	Severity Severity,
	string Summary,
	string? Solution = null);

/// <summary>
///     Header-level checks for DDS textures, based on vanilla Fallout 4 headers and D3D11's limits.
/// </summary>
public static partial class TextureChecker
{
	/// <summary>Largest texture edge D3D11 (feature level 11) can create.</summary>
	private const uint MaxEdge = 16384;

	// ReSharper disable once UnusedMember.Local
	private const uint DDSD_MIPMAPCOUNT = 0x20000;
	private const uint DDPF_ALPHAPIXELS = 0x1;
	private const uint DDPF_ALPHA = 0x2;
	private const uint DDPF_FOURCC = 0x4;
	private const uint DDPF_RGB = 0x40;
	private const uint DDPF_YUV = 0x200;
	private const uint DDPF_LUMINANCE = 0x20000;
	private const uint CubeFaceMask = 0xFC00;

	/// <summary>Problems that may crash the game or break rendering.</summary>
	private const Severity Error = Severity.Medium;

	/// <summary>Problems that work but are suboptimal or confusing.</summary>
	private const Severity Warning = Severity.Low;

	private const string ReportSolution = "[accent]Report it[/accent] to the mod author.";

	private const string ResaveSolution =
		ReportSolution + " [accent]Resaving the file[/accent] [i]might[/i] repair it.";

	/// <summary>Suffixes of regular (non-cubemap) textures.</summary>
	private static readonly HashSet<string> RegularSuffixes = ["d", "n", "s", "g", "r", "l", "msn", "h"];

	/// <summary>Names Windows (" - Copy", " - Copy (2)", " (2)") and macOS (" copy", " copy 2") give copies.</summary>
	[GeneratedRegex(@" - copy( \(\d+\))?$| copy( \d+)?$| \(\d+\)$", RegexOptions.IgnoreCase)]
	private static partial Regex CopyNameRegex();

	/// <summary>Checks a texture whose header was read successfully.</summary>
	/// <param name="dds">The texture, with its header read.</param>
	/// <param name="relative">Path relative to Data (or the mod folder).</param>
	/// <param name="fileLength">Size of the file in bytes.</param>
	public static List<TextureIssue> Check(DDS dds, string relative, long fileLength)
	{
		List<TextureIssue> issues = [];

		if (dds.Width == 0 || dds.Height == 0)
		{
			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Error,
				"This texture has [bad]a width or height of 0[/bad].", ResaveSolution));
			return issues;
		}

		if (!IsFormatValid(dds))
		{
			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Error,
				"This texture's [accent]pixel format[/accent] is [bad]unknown or invalid[/bad].", ResaveSolution));
			return issues;
		}

		var stem = Stem(relative);
		var suffix = TextureSlotRules.SuffixOf(relative);

		CheckCubemap(issues, dds, relative, suffix);
		CheckDimensions(issues, dds);
		CheckMips(issues, dds);
		CheckFormat(issues, dds, relative, suffix);
		CheckSize(issues, dds, fileLength);
		CheckName(issues, relative, stem);
		return issues;
	}

	/// <summary>The file name without its directory or extension.</summary>
	private static string Stem(string path)
	{
		var name = path[(path.LastIndexOfAny(['\\', '/']) + 1)..];
		var dot = name.LastIndexOf('.');
		return dot >= 0 ? name[..dot] : name;
	}

	private static void CheckCubemap(List<TextureIssue> issues, DDS dds, string relative, string suffix)
	{
		if (dds.IsCubemap)
		{
			// DX10 cubemaps carry no per-face bits.
			if (!dds.HasExtendedHeader && (dds.Caps2 & CubeFaceMask) != CubeFaceMask)
				issues.Add(new TextureIssue(ProblemType.InvalidTexture, Error,
					"This [accent]cubemap[/accent] [bad]doesn't contain all 6 faces[/bad].",
					ReportSolution));

			if (RegularSuffixes.Contains(suffix))
				issues.Add(new TextureIssue(ProblemType.InvalidTexture, Warning,
					$"This is a [accent]cubemap[/accent] but is [warning]named like a regular texture ([b]'_{suffix}'[/b])[/warning].",
					"Cubemaps are expected in [accent]environment map[/accent] slots. Check how this texture is used or [accent]report it[/accent] to the mod author."));

			return;
		}

		const string solution =
			"[i]If this texture is being used as a [accent]cubemap[/accent][/i] it [warning]will not render properly[/warning]. " +
			ReportSolution;

		// Color grading LUTs are named '_e' but are plain textures.
		if (suffix == "e" && !relative.Contains(@"\LUTs\", StringComparison.OrdinalIgnoreCase))
			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Error,
				"This texture is named like a [accent]cubemap[/accent] ([b]'_e'[/b]), but [warning]isn't saved as a cubemap[/warning].",
				solution));

		if (relative.Contains("Cubemap", StringComparison.OrdinalIgnoreCase))
			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Error,
				"This texture is in a [accent]cubemap folder[/accent], but [warning]isn't saved as a cubemap[/warning].",
				solution));
	}

	private static void CheckDimensions(List<TextureIssue> issues, DDS dds)
	{
		const string solution =
			ReportSolution + " [accent]Resize[/accent] the texture. Using it [bad]will cause a crash[/bad].";
		var size = $"{dds.Width} x {dds.Height}";
		var compressed = dds.IsBlockCompressed;

		if (dds.Width > MaxEdge || dds.Height > MaxEdge)
		{
			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Error,
				$"This texture is [bad]{size}[/bad]. The largest size the game can load is [accent]{MaxEdge}[/accent].",
				solution));
			return;
		}

		if ((compressed || dds.IsCubemap) &&
		    ((dds.Width >= 4 && dds.Width % 4 != 0) || (dds.Height >= 4 && dds.Height % 4 != 0)))
			// TODO: Test <4 is valid for cubemaps
			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Error,
				$"This texture is [bad]{size}[/bad]. Dimensions must be a [accent]multiple of 4[/accent] for a [accent]{(dds.IsCubemap ? "cubemap" : "block-compressed texture")}[/accent].",
				solution));
	}

	private static uint FullMipCount(DDS dds) =>
		(uint)BitOperations.Log2(Math.Max(Math.Max(dds.Width, dds.Height), dds.IsVolume ? Math.Max(1, dds.Depth) : 1)) +
		1;

	private static void CheckMips(List<TextureIssue> issues, DDS dds)
	{
		var full = FullMipCount(dds);

		if (dds.MipMapCount > full)
			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Error,
				$"This texture claims [bad]{dds.MipMapCount}[/bad] mipmaps, but at most [accent]{full}[/accent] are possible for its size.",
				ResaveSolution));
	}

	/// <summary>Says what about a format breaks a slot. Continues the sentence "which ...".</summary>
	internal static string WrongFormatReason(string? format) => format switch
	{
		"BC4" or "single-channel" =>
			"stores only [bad]one channel[/bad], so the other channels the slot needs are missing",
		"BC5" => "stores only [bad]two channels[/bad], so the [bad]blue channel[/bad] is missing",
		"BC6H" => "is an [bad]HDR[/bad] format with [bad]no alpha channel[/bad], not meant for standard textures",
		_ => "can't hold data it needs",
	};

	/// <summary>
	///     Says what about a format makes it a poor fit for a slot that vanilla files save as BC5 (two channels, high
	///     precision). Continues the sentence "which ...".
	/// </summary>
	internal static string FormatDrawback(string? format) => format switch
	{
		"BC1" =>
			"stores color at [warning]low precision[/warning] (5:6:5), which causes banding, and has a third channel the slot doesn't need",
		"BC2" or "BC3" =>
			"stores color at [warning]low precision[/warning] (5:6:5), which causes banding, and has an [warning]alpha channel[/warning] the slot doesn't use",
		"BC7" => "spreads its precision across [warning]channels the slot doesn't use[/warning]",
		"uncompressed RGB" or "uncompressed RGBA" =>
			"is [warning]uncompressed[/warning], using several times the memory of BC5 and storing channels the slot doesn't use",
		_ => "isn't the format vanilla files use for this slot",
	};

	private static void CheckFormat(List<TextureIssue> issues, DDS dds, string relative, string suffix)
	{
		// TODO: Verify these work in-game
		if (dds.IsVolume || (dds.ArraySize > 1 && !dds.IsCubemap))
			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Warning,
				"This is a [warning]volume or array[/warning] texture, which Fallout 4 doesn't normally use.",
				"These work in-game, but [accent]tell the mod author[/accent] they might have saved the file in the wrong format."));

		// Environment maps and cubemaps are covered by CheckCubemap.
		if (dds.IsCubemap || TextureSlotRules.RoleOfSuffix(suffix) is not
			    { Slot: not TextureSlotKind.Environment } role)
			return;

		var facts = new TextureFacts(dds.Format, dds.IsCubemap);
		foreach (var problem in TextureSlotRules.Evaluate(role.Slot, relative, facts, false))
		{
			// WrongFormat breaks the texture; the others are suggestions (Best Practices).
			var (summary, severity) = problem.Kind switch
			{
				TextureProblemKind.WrongFormat when suffix == "msn" =>
					($"This [accent]model-space normal map[/accent] is saved as [bad]{problem.Detail}[/bad], which can't hold the [accent]blue channel[/accent] it needs. Model-space normals use all three channels.",
						Error),
				TextureProblemKind.WrongFormat =>
					($"This [accent]{role.Role}[/accent] is saved as [bad]{problem.Detail}[/bad], which {WrongFormatReason(problem.Detail)}.",
						Error),
				TextureProblemKind.NonStandardFormat or TextureProblemKind.UnneededAlpha =>
					($"This [accent]{role.Role}[/accent] is saved as [warning]{problem.Detail}[/warning], which {FormatDrawback(problem.Detail)}. Vanilla files use [accent]BC5[/accent].",
						Warning),
				_ => (null, Warning),
			};

			if (summary != null)
				issues.Add(new TextureIssue(ProblemType.InvalidTexture, severity, summary,
					ReportSolution + " It works in-game, but may not look as intended."));
		}
	}

	private static void CheckSize(List<TextureIssue> issues, DDS dds, long fileLength)
	{
		if (ExpectedDataSize(dds) is not { } expected) return;

		var actual = fileLength - dds.DataOffset;
		if (actual < expected)
		{
			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Error,
				$"This texture's pixel data is incomplete ([bad]{Math.Max(0, actual):N0} of {expected:N0} bytes[/bad]). The file may be truncated or its header wrong.",
				ResaveSolution));
		}
		else if (actual > expected)
		{
			// Some tools write a full mip chain but declare fewer mips; ignores the extras.
			var declared = Math.Max(1, dds.MipMapCount);
			if (declared < FullMipCount(dds) && ExpectedDataSize(dds, FullMipCount(dds)) == actual) return;

			// Six faces of data but no cubemap flag: the other five faces are ignored.
			if (!dds.IsCubemap && !dds.IsVolume && dds.ArraySize == 1 && actual == expected * 6)
			{
				issues.Add(new TextureIssue(ProblemType.InvalidTexture, Error,
					"This texture contains [accent]six faces[/accent] of pixel data, but its header is [bad]not flagged as a cubemap[/bad]. Only the first face will be used.",
					ResaveSolution));
				return;
			}

			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Warning,
				$"This texture has [warning]{actual - expected:N0} unexpected bytes[/warning] after its pixel data.",
				ResaveSolution));
		}
	}

	private static void CheckName(List<TextureIssue> issues, string relative, string stem)
	{
		var name = relative[(relative.LastIndexOfAny(['\\', '/']) + 1)..];
		if (name.EndsWith(".dds.dds", StringComparison.OrdinalIgnoreCase))
			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Warning,
				"This file has a double extension ([warning].dds.dds[/warning]).",
				ReportSolution + " If the mod doesn't reference it using the same typo, it will not work in-game."));

		if (stem.EndsWith(' '))
			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Warning,
				"This file name has [warning]a space before its extension[/warning].",
				ReportSolution + " If the mod doesn't reference it using the same typo, it will not work in-game."));

		if (CopyNameRegex().IsMatch(stem))
			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Warning,
				"This looks like [warning]an accidental copy[/warning] of another file.",
				ReportSolution + " It might be a junk file or mistake."));

		if (stem.Length > 1 && stem[0] == '_' && (RegularSuffixes.Contains(stem[1..].ToLowerInvariant()) ||
		                                          stem[1..].Equals("e", StringComparison.OrdinalIgnoreCase)))
			issues.Add(new TextureIssue(ProblemType.InvalidTexture, Warning,
				"This file has [warning]no name besides its suffix[/warning].",
				ReportSolution + " If the mod doesn't reference it using the same name, it will not work in-game."));
	}

	private static bool IsFormatValid(DDS dds)
	{
		if (dds.DxgiFormat is { } dxgi) return dxgi is > 0 and < 200;

		var flags = dds.PixelFormatFlags;
		if ((flags & DDPF_FOURCC) != 0)
			// Numeric D3DFORMAT values are legal; textual ones must be known.
			return dds.FourCC < 0x100 || dds.IsBlockCompressed;

		return (flags & (DDPF_RGB | DDPF_ALPHA | DDPF_LUMINANCE | DDPF_YUV | DDPF_ALPHAPIXELS)) != 0;
	}

	/// <summary>Bits per pixel, or for block formats bits per 4x4 block. Null if unknown.</summary>
	private static int? BitsPerElement(DDS dds, TextureFormatClass kind)
	{
		switch (kind)
		{
			case TextureFormatClass.Bc1 or TextureFormatClass.Bc4:
				return 64;
			case TextureFormatClass.Bc2 or TextureFormatClass.Bc3 or TextureFormatClass.Bc5 or TextureFormatClass.Bc6H
				or TextureFormatClass.Bc7:
				return 128;
		}

		if (dds.DxgiFormat is { } dxgi)
			return dxgi switch
			{
				>= 1 and <= 4 => 128,
				>= 5 and <= 8 => 96,
				>= 9 and <= 22 => 64,
				>= 23 and <= 47 => 32,
				>= 48 and <= 59 => 16,
				>= 60 and <= 65 => 8,
				67 => 32,
				68 or 69 => 16,
				85 or 86 or 115 => 16,
				>= 87 and <= 93 => 32,
				_ => null,
			};

		var bits = dds.RGBBitCount;
		return (dds.PixelFormatFlags & DDPF_FOURCC) == 0 && bits is 8 or 16 or 24 or 32 ? (int)bits : null;
	}

	/// <summary>The exact size of all pixel data, or null if it can't be determined from the header.</summary>
	/// <param name="dds">The texture.</param>
	/// <param name="mipCount">Mip levels to count, or null to use the header's.</param>
	private static long? ExpectedDataSize(DDS dds, uint? mipCount = null)
	{
		if (BitsPerElement(dds, dds.Format) is not { } bits) return null;

		var mips = Math.Max(1, mipCount ?? dds.MipMapCount);
		if (mips > FullMipCount(dds)) return null;

		long surfaces;
		if (dds.IsCubemap)
		{
			var faces = dds.HasExtendedHeader ? 6 : BitOperations.PopCount(dds.Caps2 & CubeFaceMask);
			if (faces != 6) return null;

			surfaces = faces * (dds.HasExtendedHeader ? dds.ArraySize : 1);
		}
		else
		{
			surfaces = dds.IsVolume ? 1 : dds.ArraySize;
		}

		var compressed = dds.IsBlockCompressed;
		long total = 0;
		long w = dds.Width, h = dds.Height, d = dds.IsVolume ? Math.Max(1, dds.Depth) : 1;
		for (var i = 0; i < mips; i++)
		{
			var level = compressed
				? Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * bits / 8
				: (w * bits + 7) / 8 * h;
			total += level * d;
			w = Math.Max(1, w / 2);
			h = Math.Max(1, h / 2);
			d = Math.Max(1, d / 2);
		}

		return total * surfaces;
	}
}
