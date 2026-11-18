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
using System.Linq;

namespace CollectiveModdingToolkit.Materials;

/// <summary>A BGEM: the material of an effect, such as glows, fog, and holograms.</summary>
public sealed class Bgem : MaterialFile
{
	private static readonly IReadOnlyList<MaterialField> AllFields = BuildFields();

	public override MaterialKind Kind => MaterialKind.Bgem;
	public override string Signature => "BGEM";
	public override IReadOnlyList<MaterialField> Fields => AllFields;

	// Textures
	public string BaseTexture { get; set; } = string.Empty;
	public string GrayscaleTexture { get; set; } = string.Empty;
	public string EnvmapTexture { get; set; } = string.Empty;
	public string NormalTexture { get; set; } = string.Empty;
	public string EnvmapMaskTexture { get; set; } = string.Empty;
	public string SpecularTexture { get; set; } = string.Empty;
	public string LightingTexture { get; set; } = string.Empty;
	public string GlowTexture { get; set; } = string.Empty;

	public bool BloodEnabled { get; set; }
	public bool EffectLightingEnabled { get; set; }
	public bool FalloffEnabled { get; set; }
	public bool FalloffColorEnabled { get; set; }
	public bool GrayscaleToPaletteAlpha { get; set; }
	public bool SoftEnabled { get; set; }

	public MaterialColor BaseColor { get; set; } = MaterialColor.White;
	public float BaseColorScale { get; set; } = 1;

	public float FalloffStartAngle { get; set; } = 1;
	public float FalloffStopAngle { get; set; } = 1;
	public float FalloffStartOpacity { get; set; }
	public float FalloffStopOpacity { get; set; }

	public float LightingInfluence { get; set; } = 1;
	public byte EnvmapMinLod { get; set; }
	public float SoftDepth { get; set; } = 100;
	public MaterialColor EmittanceColor { get; set; } = MaterialColor.White;

	public float AdaptativeEmissiveExposureOffset { get; set; }
	public float AdaptativeEmissiveFinalExposureMin { get; set; }
	public float AdaptativeEmissiveFinalExposureMax { get; set; }
	public bool Glowmap { get; set; }
	public bool EffectPbrSpecular { get; set; }

	public string GlassRoughnessScratch { get; set; } = string.Empty;
	public string GlassDirtOverlay { get; set; } = string.Empty;
	public bool GlassEnabled { get; set; }
	public MaterialColor GlassFresnelColor { get; set; } = MaterialColor.White;
	public float GlassBlurScaleBase { get; set; } = 0.4f;
	public float GlassBlurScaleFactor { get; set; } = 1;
	public float GlassRefractionScaleBase { get; set; } = 0.05f;

	private static List<MaterialField> BuildFields()
	{
		FieldBuilder<Bgem> tex = new("Textures");
		FieldBuilder<Bgem> basic = new("Base");
		FieldBuilder<Bgem> falloff = new("Falloff");
		FieldBuilder<Bgem> emit = new("Emittance");
		FieldBuilder<Bgem> env = new("Environment Mapping");
		FieldBuilder<Bgem> glass = new("Glass");

		return
		[
			tex.Texture(TextureSlotKind.Diffuse, "Base", "sBaseTexture", m => m.BaseTexture,
				(m, v) => m.BaseTexture = v),
			tex.Texture(TextureSlotKind.Greyscale, "Greyscale", "sGrayscaleTexture", m => m.GrayscaleTexture,
				(m, v) => m.GrayscaleTexture = v),
			tex.Texture(TextureSlotKind.Environment, "Environment Map", "sEnvmapTexture", m => m.EnvmapTexture,
				(m, v) => m.EnvmapTexture = v),
			tex.Texture(TextureSlotKind.Normal, "Normal", "sNormalTexture", m => m.NormalTexture,
				(m, v) => m.NormalTexture = v),
			tex.Texture(TextureSlotKind.EnvironmentMask, "Environment Map Mask", "sEnvmapMaskTexture",
				m => m.EnvmapMaskTexture, (m, v) => m.EnvmapMaskTexture = v),
			tex.Texture(TextureSlotKind.Specular, "Specular", "sSpecularTexture", m => m.SpecularTexture,
				(m, v) => m.SpecularTexture = v, 11),
			tex.Texture(TextureSlotKind.Lighting, "Lighting", "sLightingTexture", m => m.LightingTexture,
				(m, v) => m.LightingTexture = v, 11),
			tex.Texture(TextureSlotKind.Glow, "Glow", "sGlowTexture", m => m.GlowTexture,
				(m, v) => m.GlowTexture = v, 11),
			// BGEM stores the environment mapping switch with the textures from version 10 on, so it applies to all.
			.. CommonFields().Where(f => f.JsonKey is not ("bEnvironmentMapping" or "fEnvironmentMappingMaskScale")),

			env.Bool("Environment Mapping", "bEnvironmentMapping", m => m.EnvironmentMapping,
				(m, v) => m.EnvironmentMapping = v),
			env.Float("Mask Scale", "fEnvironmentMappingMaskScale", m => m.EnvironmentMappingMaskScale,
				(m, v) => m.EnvironmentMappingMaskScale = v),
			env.Byte("Min LOD", "iEnvmapMinLOD", m => m.EnvmapMinLod, (m, v) => m.EnvmapMinLod = v),

			basic.Color("Base Color", "cBaseColor", m => m.BaseColor, (m, v) => m.BaseColor = v),
			basic.Float("Base Color Scale", "fBaseColorScale", m => m.BaseColorScale, (m, v) => m.BaseColorScale = v),
			basic.Bool("Blood Enabled", "bBloodEnabled", m => m.BloodEnabled, (m, v) => m.BloodEnabled = v),
			basic.Bool("Effect Lighting", "bEffectLightingEnabled", m => m.EffectLightingEnabled,
				(m, v) => m.EffectLightingEnabled = v),
			basic.Float("Lighting Influence", "fLightingInfluence", m => m.LightingInfluence,
				(m, v) => m.LightingInfluence = v),
			basic.Bool("Grayscale To Palette Alpha", "bGrayscaleToPaletteAlpha", m => m.GrayscaleToPaletteAlpha,
				(m, v) => m.GrayscaleToPaletteAlpha = v),
			basic.Bool("Soft", "bSoftEnabled", m => m.SoftEnabled, (m, v) => m.SoftEnabled = v),
			basic.Float("Soft Depth", "fSoftDepth", m => m.SoftDepth, (m, v) => m.SoftDepth = v),
			basic.Bool("Effect PBR Specular", "bEffectPbrSpecular", m => m.EffectPbrSpecular,
				(m, v) => m.EffectPbrSpecular = v, 20),

			falloff.Bool("Falloff", "bFalloffEnabled", m => m.FalloffEnabled, (m, v) => m.FalloffEnabled = v),
			falloff.Bool("Falloff Color", "bFalloffColorEnabled", m => m.FalloffColorEnabled,
				(m, v) => m.FalloffColorEnabled = v),
			falloff.Float("Start Angle", "fFalloffStartAngle", m => m.FalloffStartAngle,
				(m, v) => m.FalloffStartAngle = v),
			falloff.Float("Stop Angle", "fFalloffStopAngle", m => m.FalloffStopAngle,
				(m, v) => m.FalloffStopAngle = v),
			falloff.Float("Start Opacity", "fFalloffStartOpacity", m => m.FalloffStartOpacity,
				(m, v) => m.FalloffStartOpacity = v),
			falloff.Float("Stop Opacity", "fFalloffStopOpacity", m => m.FalloffStopOpacity,
				(m, v) => m.FalloffStopOpacity = v),

			emit.Color("Emittance Color", "cEmittanceColor", m => m.EmittanceColor, (m, v) => m.EmittanceColor = v,
				11),
			emit.Bool("Glowmap", "bGlowmap", m => m.Glowmap, (m, v) => m.Glowmap = v, 16),
			emit.Float("Exposure Offset", "fAdaptativeEmissive_ExposureOffset",
				m => m.AdaptativeEmissiveExposureOffset, (m, v) => m.AdaptativeEmissiveExposureOffset = v, 15),
			emit.Float("Final Exposure Min", "fAdaptativeEmissive_FinalExposureMin",
				m => m.AdaptativeEmissiveFinalExposureMin, (m, v) => m.AdaptativeEmissiveFinalExposureMin = v, 15),
			emit.Float("Final Exposure Max", "fAdaptativeEmissive_FinalExposureMax",
				m => m.AdaptativeEmissiveFinalExposureMax, (m, v) => m.AdaptativeEmissiveFinalExposureMax = v, 15),

			glass.Texture(TextureSlotKind.GlassRoughness, "Roughness / Scratch", "sGlassRoughnessScratch",
				m => m.GlassRoughnessScratch, (m, v) => m.GlassRoughnessScratch = v, 21),
			glass.Texture(TextureSlotKind.GlassDirt, "Dirt Overlay", "sGlassDirtOverlay", m => m.GlassDirtOverlay,
				(m, v) => m.GlassDirtOverlay = v, 21),
			glass.Bool("Glass Enabled", "bGlassEnabled", m => m.GlassEnabled, (m, v) => m.GlassEnabled = v, 21),
			glass.Color("Fresnel Color", "cGlassFresnelColor", m => m.GlassFresnelColor,
				(m, v) => m.GlassFresnelColor = v, 21),
			glass.Float("Blur Scale Base", "fGlassBlurScaleBase", m => m.GlassBlurScaleBase,
				(m, v) => m.GlassBlurScaleBase = v, 21),
			glass.Float("Blur Scale Factor", "fGlassBlurScaleFactor", m => m.GlassBlurScaleFactor,
				(m, v) => m.GlassBlurScaleFactor = v, 22),
			glass.Float("Refraction Scale Base", "fGlassRefractionScaleBase", m => m.GlassRefractionScaleBase,
				(m, v) => m.GlassRefractionScaleBase = v, 21),
		];
	}

	private protected override void ReadBody(MaterialReader input)
	{
		base.ReadBody(input);

		BaseTexture = input.String();
		GrayscaleTexture = input.String();
		EnvmapTexture = input.String();
		NormalTexture = input.String();
		EnvmapMaskTexture = input.String();

		if (Version >= 11)
		{
			SpecularTexture = input.String();
			LightingTexture = input.String();
			GlowTexture = input.String();
		}

		if (Version >= 21)
		{
			GlassRoughnessScratch = input.String();
			GlassDirtOverlay = input.String();
			GlassEnabled = input.Bool();
			if (GlassEnabled)
			{
				GlassFresnelColor = input.Color();
				GlassBlurScaleBase = input.Single();
				if (Version >= 22) GlassBlurScaleFactor = input.Single();

				GlassRefractionScaleBase = input.Single();
			}
		}

		if (Version >= 10)
		{
			EnvironmentMapping = input.Bool();
			EnvironmentMappingMaskScale = input.Single();
		}

		BloodEnabled = input.Bool();
		EffectLightingEnabled = input.Bool();
		FalloffEnabled = input.Bool();
		FalloffColorEnabled = input.Bool();
		GrayscaleToPaletteAlpha = input.Bool();
		SoftEnabled = input.Bool();

		BaseColor = input.Color();
		BaseColorScale = input.Single();

		FalloffStartAngle = input.Single();
		FalloffStopAngle = input.Single();
		FalloffStartOpacity = input.Single();
		FalloffStopOpacity = input.Single();

		LightingInfluence = input.Single();
		EnvmapMinLod = input.Byte();
		SoftDepth = input.Single();

		if (Version >= 11) EmittanceColor = input.Color();

		if (Version >= 15)
		{
			AdaptativeEmissiveExposureOffset = input.Single();
			AdaptativeEmissiveFinalExposureMin = input.Single();
			AdaptativeEmissiveFinalExposureMax = input.Single();
		}

		if (Version >= 16) Glowmap = input.Bool();
		if (Version >= 20) EffectPbrSpecular = input.Bool();
	}

	private protected override void WriteBody(MaterialWriter output)
	{
		base.WriteBody(output);

		output.String(BaseTexture);
		output.String(GrayscaleTexture);
		output.String(EnvmapTexture);
		output.String(NormalTexture);
		output.String(EnvmapMaskTexture);

		if (Version >= 11)
		{
			output.String(SpecularTexture);
			output.String(LightingTexture);
			output.String(GlowTexture);
		}

		if (Version >= 21)
		{
			output.String(GlassRoughnessScratch);
			output.String(GlassDirtOverlay);
			output.Bool(GlassEnabled);
			if (GlassEnabled)
			{
				output.Color(GlassFresnelColor);
				output.Single(GlassBlurScaleBase);
				if (Version >= 22) output.Single(GlassBlurScaleFactor);

				output.Single(GlassRefractionScaleBase);
			}
		}

		if (Version >= 10)
		{
			output.Bool(EnvironmentMapping);
			output.Single(EnvironmentMappingMaskScale);
		}

		output.Bool(BloodEnabled);
		output.Bool(EffectLightingEnabled);
		output.Bool(FalloffEnabled);
		output.Bool(FalloffColorEnabled);
		output.Bool(GrayscaleToPaletteAlpha);
		output.Bool(SoftEnabled);

		output.Color(BaseColor);
		output.Single(BaseColorScale);

		output.Single(FalloffStartAngle);
		output.Single(FalloffStopAngle);
		output.Single(FalloffStartOpacity);
		output.Single(FalloffStopOpacity);

		output.Single(LightingInfluence);
		output.Byte(EnvmapMinLod);
		output.Single(SoftDepth);

		if (Version >= 11) output.Color(EmittanceColor);

		if (Version >= 15)
		{
			output.Single(AdaptativeEmissiveExposureOffset);
			output.Single(AdaptativeEmissiveFinalExposureMin);
			output.Single(AdaptativeEmissiveFinalExposureMax);
		}

		if (Version >= 16) output.Bool(Glowmap);
		if (Version >= 20) output.Bool(EffectPbrSpecular);
	}
}
