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

// The BGSM/BGEM layouts are derived from ousnius' Material-Editor (MIT), which builds on work by gibbed (version 2)
// and jonwd7 (version 20 and later). See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CollectiveModdingToolkit.Materials;

public enum MaterialKind
{
	Bgsm,
	Bgem,
}

public enum AlphaBlendMode
{
	/// <summary>Blend values the editor doesn't know. They are kept as read.</summary>
	Custom,
	Unknown,
	None,
	Standard,
	Additive,
	Multiplicative,
}

[Flags]
public enum MaskWriteFlags : byte
{
	Albedo = 1,
	Normal = 2,
	Specular = 4,
	AmbientOcclusion = 8,
	Emissive = 16,
	Gloss = 32,
}

/// <summary>The settings BGSM (material) and BGEM (effect material) files have in common.</summary>
public abstract class MaterialFile
{
	/// <summary>The newest file version this library understands.</summary>
	public const uint MaxSupportedVersion = 22;

	/// <summary>The newest version Fallout 4 itself loads. Later ones are Fallout 76's.</summary>
	public const uint MaxFallout4Version = 2;

	private static readonly string[] AlphaBlendOptions =
		[.. Enum.GetNames<AlphaBlendMode>().Where(n => n != nameof(AlphaBlendMode.Custom))];

	private static readonly (AlphaBlendMode Mode, byte A, uint B, uint C)[] BlendValues =
	[
		(AlphaBlendMode.Unknown, 0, 6, 7),
		(AlphaBlendMode.None, 0, 0, 0),
		(AlphaBlendMode.Standard, 1, 6, 7),
		(AlphaBlendMode.Additive, 1, 6, 0),
		(AlphaBlendMode.Multiplicative, 1, 4, 1),
	];

	private protected MaterialFile()
	{
		Version = 2;
		TileU = true;
		TileV = true;
		UScale = 1;
		VScale = 1;
		Alpha = 1;
		AlphaTestRef = 128;
		ZBufferWrite = true;
		ZBufferTest = true;
		EnvironmentMappingMaskScale = 1;
		MaskWrites = MaskWriteFlags.Albedo | MaskWriteFlags.Normal | MaskWriteFlags.Specular |
		             MaskWriteFlags.AmbientOcclusion | MaskWriteFlags.Emissive | MaskWriteFlags.Gloss;
	}

	public abstract MaterialKind Kind { get; }

	/// <summary>The four-letter magic the file starts with.</summary>
	public abstract string Signature { get; }

	/// <summary>Every value that can be edited, in the order they should be shown.</summary>
	public abstract IReadOnlyList<MaterialField> Fields { get; }

	/// <summary>True if Fallout 4 can load this version. Newer ones are Fallout 76's format.</summary>
	public bool IsFallout4Format => Version <= MaxFallout4Version;

	public uint Version { get; set; }

	public bool TileU { get; set; }
	public bool TileV { get; set; }
	public float UOffset { get; set; }
	public float VOffset { get; set; }
	public float UScale { get; set; }
	public float VScale { get; set; }

	public float Alpha { get; set; }
	public AlphaBlendMode AlphaBlendMode { get; set; } = AlphaBlendMode.None;

	/// <summary>The three raw blend values, kept when <see cref="AlphaBlendMode" /> is <see cref="AlphaBlendMode.Custom" />.</summary>
	public (byte A, uint B, uint C) CustomAlphaBlend { get; set; }

	public byte AlphaTestRef { get; set; }
	public bool AlphaTest { get; set; }
	public bool ZBufferWrite { get; set; }
	public bool ZBufferTest { get; set; }
	public bool ScreenSpaceReflections { get; set; }
	public bool WetnessControlScreenSpaceReflections { get; set; }
	public bool Decal { get; set; }
	public bool TwoSided { get; set; }
	public bool DecalNoFade { get; set; }
	public bool NonOccluder { get; set; }
	public bool Refraction { get; set; }
	public bool RefractionFalloff { get; set; }
	public float RefractionPower { get; set; }
	public bool EnvironmentMapping { get; set; }
	public float EnvironmentMappingMaskScale { get; set; }
	public bool DepthBias { get; set; }
	public bool GrayscaleToPaletteColor { get; set; }
	public MaskWriteFlags MaskWrites { get; set; }

	/// <summary>
	///     Whether the material blends or tests alpha, so its main texture's alpha channel is used.
	/// </summary>
	public bool UsesAlpha => AlphaTest || AlphaBlendMode is not (AlphaBlendMode.None or AlphaBlendMode.Unknown);

	/// <summary>The texture slots this version has, with their paths. Empty slots are included.</summary>
	public IEnumerable<(MaterialField Field, TextureSlotKind Slot, string Path)> TextureSlots =>
		Fields.Where(f => f.Kind == MaterialFieldKind.Texture && f.AppliesTo(Version))
			.Select(f => (f, f.Slot!.Value, (string)f.GetValue(this)));

	private protected static FieldBuilder<MaterialFile> CommonBuilder(string group) => new(group);

	/// <summary>The fields every material has, for the subclasses to place in their own lists.</summary>
	private protected static List<MaterialField> CommonFields()
	{
		var t = CommonBuilder("Texture Transform");
		var a = CommonBuilder("Alpha");
		var r = CommonBuilder("Rendering");
		var rf = CommonBuilder("Refraction");
		var e = CommonBuilder("Environment Mapping");
		var m = CommonBuilder("Mask Writes");
		List<MaterialField> fields =
		[
			t.Bool("Tile U", "bTileU", f => f.TileU, (f, v) => f.TileU = v),
			t.Bool("Tile V", "bTileV", f => f.TileV, (f, v) => f.TileV = v),
			t.Float("Offset U", "fUOffset", f => f.UOffset, (f, v) => f.UOffset = v),
			t.Float("Offset V", "fVOffset", f => f.VOffset, (f, v) => f.VOffset = v),
			t.Float("Scale U", "fUScale", f => f.UScale, (f, v) => f.UScale = v),
			t.Float("Scale V", "fVScale", f => f.VScale, (f, v) => f.VScale = v),

			a.Float("Alpha", "fAlpha", f => f.Alpha, (f, v) => f.Alpha = v),
			a.Enum("Alpha Blend Mode", "eAlphaBlendMode", AlphaBlendOptions, f => f.AlphaBlendMode.ToString(),
				(f, v) => f.AlphaBlendMode = ParseBlendMode(v, f.AlphaBlendMode)),
			a.Byte("Alpha Test Reference", "fAlphaTestRef", f => f.AlphaTestRef, (f, v) => f.AlphaTestRef = v),
			a.Bool("Alpha Test", "bAlphaTest", f => f.AlphaTest, (f, v) => f.AlphaTest = v),

			r.Bool("Z-Buffer Write", "bZBufferWrite", f => f.ZBufferWrite, (f, v) => f.ZBufferWrite = v),
			r.Bool("Z-Buffer Test", "bZBufferTest", f => f.ZBufferTest, (f, v) => f.ZBufferTest = v),
			r.Bool("Screen Space Reflections", "bScreenSpaceReflections", f => f.ScreenSpaceReflections,
				(f, v) => f.ScreenSpaceReflections = v),
			r.Bool("Wetness Control SSR", "bWetnessControl_ScreenSpaceReflections",
				f => f.WetnessControlScreenSpaceReflections, (f, v) => f.WetnessControlScreenSpaceReflections = v),
			r.Bool("Decal", "bDecal", f => f.Decal, (f, v) => f.Decal = v),
			r.Bool("Two-Sided", "bTwoSided", f => f.TwoSided, (f, v) => f.TwoSided = v),
			r.Bool("Decal No Fade", "bDecalNoFade", f => f.DecalNoFade, (f, v) => f.DecalNoFade = v),
			r.Bool("Non-Occluder", "bNonOccluder", f => f.NonOccluder, (f, v) => f.NonOccluder = v),
			r.Bool("Depth Bias", "bDepthBias", f => f.DepthBias, (f, v) => f.DepthBias = v, 10),
			r.Bool("Grayscale to Palette Color", "bGrayscaleToPaletteColor", f => f.GrayscaleToPaletteColor,
				(f, v) => f.GrayscaleToPaletteColor = v),

			rf.Bool("Refraction", "bRefraction", f => f.Refraction, (f, v) => f.Refraction = v),
			rf.Bool("Refraction Falloff", "fRefractionFalloff", f => f.RefractionFalloff,
				(f, v) => f.RefractionFalloff = v),
			rf.Float("Refraction Power", "fRefractionPower", f => f.RefractionPower, (f, v) => f.RefractionPower = v),

			e.Bool("Environment Mapping", "bEnvironmentMapping", f => f.EnvironmentMapping,
				(f, v) => f.EnvironmentMapping = v, 0, 9),
			e.Float("Mask Scale", "fEnvironmentMappingMaskScale", f => f.EnvironmentMappingMaskScale,
				(f, v) => f.EnvironmentMappingMaskScale = v, 0, 9),

			Mask(m, "Albedo", "bWriteMaskAlbedo", MaskWriteFlags.Albedo),
			Mask(m, "Normal", "bWriteMaskNormal", MaskWriteFlags.Normal),
			Mask(m, "Specular", "bWriteMaskSpecular", MaskWriteFlags.Specular),
			Mask(m, "Ambient Occlusion", "bWriteMaskAmbientOcclusion", MaskWriteFlags.AmbientOcclusion),
			Mask(m, "Emissive", "bWriteMaskEmissive", MaskWriteFlags.Emissive),
			Mask(m, "Gloss", "bWriteMaskGloss", MaskWriteFlags.Gloss),
		];
		foreach (var field in fields) field.IsCommon = true;

		return fields;
	}

	private static MaterialField Mask(FieldBuilder<MaterialFile> b, string label, string key, MaskWriteFlags flag) =>
		b.Bool(label, key, f => (f.MaskWrites & flag) != 0,
			(f, v) => f.MaskWrites = v ? f.MaskWrites | flag : f.MaskWrites & ~flag, 6);

	private static AlphaBlendMode ParseBlendMode(string value, AlphaBlendMode current) =>
		Enum.TryParse<AlphaBlendMode>(value, true, out var mode) ? mode : current;

	// ---- Binary ----

	/// <summary>Reads a material from a file's bytes, which must start with this type's signature.</summary>
	/// <exception cref="InvalidDataException">The data isn't a valid material of this type.</exception>
	public void Read(ReadOnlySpan<byte> data)
	{
		var reader = new MaterialReader(data);
		try
		{
			if (!reader.ReadBytes(4).SequenceEqual(Encoding.ASCII.GetBytes(Signature)))
				throw new InvalidDataException($"Not a {Signature} file.");

			Version = reader.UInt32();
			if (Version > MaxSupportedVersion)
				throw new InvalidDataException($"Unsupported {Signature} version {Version}.");

			ReadBody(reader);
		}
		catch (EndOfStreamException)
		{
			throw new InvalidDataException("The file ended unexpectedly.");
		}

		if (reader.Remaining > 0)
			throw new InvalidDataException($"{reader.Remaining} unexpected bytes after the end of the data.");
	}

	/// <summary>Writes the material in its binary format.</summary>
	public byte[] Write()
	{
		using MemoryStream stream = new();
		using (MaterialWriter writer = new(stream))
		{
			writer.Raw(Encoding.ASCII.GetBytes(Signature));
			writer.UInt32(Version);
			WriteBody(writer);
		}

		return stream.ToArray();
	}

	private protected virtual void ReadBody(MaterialReader input)
	{
		var tile = input.UInt32();
		TileU = (tile & 2) != 0;
		TileV = (tile & 1) != 0;
		UOffset = input.Single();
		VOffset = input.Single();
		UScale = input.Single();
		VScale = input.Single();

		Alpha = input.Single();
		var a = input.Byte();
		var b = input.UInt32();
		var c = input.UInt32();
		var known = BlendValues.FirstOrDefault(v => v.A == a && v.B == b && v.C == c);
		if (known.Mode == default)
		{
			AlphaBlendMode = AlphaBlendMode.Custom;
			CustomAlphaBlend = (a, b, c);
		}
		else
		{
			AlphaBlendMode = known.Mode;
		}

		AlphaTestRef = input.Byte();
		AlphaTest = input.Bool();
		ZBufferWrite = input.Bool();
		ZBufferTest = input.Bool();
		ScreenSpaceReflections = input.Bool();
		WetnessControlScreenSpaceReflections = input.Bool();
		Decal = input.Bool();
		TwoSided = input.Bool();
		DecalNoFade = input.Bool();
		NonOccluder = input.Bool();
		Refraction = input.Bool();
		RefractionFalloff = input.Bool();
		RefractionPower = input.Single();

		if (Version < 10)
		{
			EnvironmentMapping = input.Bool();
			EnvironmentMappingMaskScale = input.Single();
		}
		else
		{
			DepthBias = input.Bool();
		}

		GrayscaleToPaletteColor = input.Bool();
		if (Version >= 6) MaskWrites = (MaskWriteFlags)input.Byte();
	}

	private protected virtual void WriteBody(MaterialWriter output)
	{
		output.UInt32((TileU ? 2u : 0u) | (TileV ? 1u : 0u));
		output.Single(UOffset);
		output.Single(VOffset);
		output.Single(UScale);
		output.Single(VScale);

		output.Single(Alpha);
		var (a, b, c) = AlphaBlendMode == AlphaBlendMode.Custom
			? CustomAlphaBlend
			: BlendValues.FirstOrDefault(bv => bv.Mode == AlphaBlendMode) is { Mode: not AlphaBlendMode.Custom } v
				? (v.A, v.B, v.C)
				: (default(byte), 0u, 0u);
		output.Byte(a);
		output.UInt32(b);
		output.UInt32(c);

		output.Byte(AlphaTestRef);
		output.Bool(AlphaTest);
		output.Bool(ZBufferWrite);
		output.Bool(ZBufferTest);
		output.Bool(ScreenSpaceReflections);
		output.Bool(WetnessControlScreenSpaceReflections);
		output.Bool(Decal);
		output.Bool(TwoSided);
		output.Bool(DecalNoFade);
		output.Bool(NonOccluder);
		output.Bool(Refraction);
		output.Bool(RefractionFalloff);
		output.Single(RefractionPower);

		if (Version < 10)
		{
			output.Bool(EnvironmentMapping);
			output.Single(EnvironmentMappingMaskScale);
		}
		else
		{
			output.Bool(DepthBias);
		}

		output.Bool(GrayscaleToPaletteColor);
		if (Version >= 6) output.Byte((byte)MaskWrites);
	}
}
