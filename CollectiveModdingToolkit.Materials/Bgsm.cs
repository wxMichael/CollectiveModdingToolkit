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

/// <summary>A BGSM: the material of a normal mesh surface.</summary>
public sealed class Bgsm : MaterialFile
{
	private static readonly IReadOnlyList<MaterialField> AllFields = BuildFields();

	public override MaterialKind Kind => MaterialKind.Bgsm;
	public override string Signature => "BGSM";
	public override IReadOnlyList<MaterialField> Fields => AllFields;

	// Textures
	public string DiffuseTexture { get; set; } = string.Empty;
	public string NormalTexture { get; set; } = string.Empty;
	public string SmoothSpecTexture { get; set; } = string.Empty;
	public string GreyscaleTexture { get; set; } = string.Empty;
	public string EnvmapTexture { get; set; } = string.Empty;
	public string GlowTexture { get; set; } = string.Empty;
	public string InnerLayerTexture { get; set; } = string.Empty;
	public string WrinklesTexture { get; set; } = string.Empty;
	public string DisplacementTexture { get; set; } = string.Empty;
	public string SpecularTexture { get; set; } = string.Empty;
	public string LightingTexture { get; set; } = string.Empty;
	public string FlowTexture { get; set; } = string.Empty;
	public string DistanceFieldAlphaTexture { get; set; } = string.Empty;

	public bool EnableEditorAlphaRef { get; set; }

	public bool RimLighting { get; set; }
	public float RimPower { get; set; } = 2;
	public float BackLightPower { get; set; }
	public bool SubsurfaceLighting { get; set; }
	public float SubsurfaceLightingRolloff { get; set; } = 0.3f;

	public bool SpecularEnabled { get; set; }
	public MaterialColor SpecularColor { get; set; } = MaterialColor.White;
	public float SpecularMult { get; set; } = 1;
	public float Smoothness { get; set; } = 1;
	public float FresnelPower { get; set; } = 5;

	public float WetnessControlSpecScale { get; set; } = -1;
	public float WetnessControlSpecPowerScale { get; set; } = -1;
	public float WetnessControlSpecMinvar { get; set; } = -1;
	public float WetnessControlEnvMapScale { get; set; } = -1;
	public float WetnessControlFresnelPower { get; set; } = -1;
	public float WetnessControlMetalness { get; set; } = -1;

	public bool Pbr { get; set; }
	public bool CustomPorosity { get; set; }
	public float PorosityValue { get; set; }

	public string RootMaterialPath { get; set; } = string.Empty;

	public bool AnisoLighting { get; set; }
	public bool EmitEnabled { get; set; }
	public MaterialColor EmittanceColor { get; set; } = MaterialColor.White;
	public float EmittanceMult { get; set; } = 1;
	public bool ModelSpaceNormals { get; set; }
	public bool ExternalEmittance { get; set; }
	public float LumEmittance { get; set; }
	public bool UseAdaptativeEmissive { get; set; }
	public float AdaptativeEmissiveExposureOffset { get; set; }
	public float AdaptativeEmissiveFinalExposureMin { get; set; }
	public float AdaptativeEmissiveFinalExposureMax { get; set; }

	public bool BackLighting { get; set; }
	public bool ReceiveShadows { get; set; }
	public bool HideSecret { get; set; }
	public bool CastShadows { get; set; }
	public bool DissolveFade { get; set; }
	public bool AssumeShadowmask { get; set; }
	public bool Glowmap { get; set; }
	public bool EnvironmentMappingWindow { get; set; }
	public bool EnvironmentMappingEye { get; set; }

	public bool Hair { get; set; }
	public MaterialColor HairTintColor { get; set; } = new(128 / 255f, 128 / 255f, 128 / 255f);
	public bool Tree { get; set; }
	public bool Facegen { get; set; }
	public bool SkinTint { get; set; }

	public bool Tessellate { get; set; }
	public float DisplacementTextureBias { get; set; } = -0.5f;
	public float DisplacementTextureScale { get; set; } = 10;
	public float TessellationPnScale { get; set; } = 1;
	public float TessellationBaseFactor { get; set; } = 1;
	public float TessellationFadeDistance { get; set; }

	public float GrayscaleToPaletteScale { get; set; } = 1;
	public bool SkewSpecularAlpha { get; set; }

	public bool Translucency { get; set; }
	public bool TranslucencyThickObject { get; set; }
	public bool TranslucencyMixAlbedoWithSubsurfaceColor { get; set; }
	public MaterialColor TranslucencySubsurfaceColor { get; set; }
	public float TranslucencyTransmissiveScale { get; set; }
	public float TranslucencyTurbulence { get; set; }

	public bool Terrain { get; set; }

	/// <summary>An unidentified value version 3 files have when Terrain is set. Kept so it survives a save.</summary>
	public uint TerrainUnknown { get; set; }

	public float TerrainThresholdFalloff { get; set; }
	public float TerrainTilingDistance { get; set; }
	public float TerrainRotationAngle { get; set; }

	private static List<MaterialField> BuildFields()
	{
		FieldBuilder<Bgsm> tex = new("Textures");
		FieldBuilder<Bgsm> spec = new("Specular");
		FieldBuilder<Bgsm> light = new("Lighting");
		FieldBuilder<Bgsm> emit = new("Emittance");
		FieldBuilder<Bgsm> tl = new("Translucency");
		FieldBuilder<Bgsm> wet = new("Wetness Control");
		FieldBuilder<Bgsm> misc = new("Surface Types");
		FieldBuilder<Bgsm> tess = new("Tessellation / Displacement");
		FieldBuilder<Bgsm> terrain = new("Terrain");
		FieldBuilder<Bgsm> other = new("Other");

		List<MaterialField> fields =
		[
			tex.Texture(TextureSlotKind.Diffuse, "Diffuse", "sDiffuseTexture", m => m.DiffuseTexture,
				(m, v) => m.DiffuseTexture = v),
			tex.Texture(TextureSlotKind.Normal, "Normal", "sNormalTexture", m => m.NormalTexture,
				(m, v) => m.NormalTexture = v),
			tex.Texture(TextureSlotKind.SmoothSpec, "Smooth Spec", "sSmoothSpecTexture", m => m.SmoothSpecTexture,
				(m, v) => m.SmoothSpecTexture = v),
			tex.Texture(TextureSlotKind.Greyscale, "Greyscale", "sGreyscaleTexture", m => m.GreyscaleTexture,
				(m, v) => m.GreyscaleTexture = v),
			tex.Texture(TextureSlotKind.Environment, "Environment Map", "sEnvmapTexture", m => m.EnvmapTexture,
				(m, v) => m.EnvmapTexture = v, 0, 2),
			tex.Texture(TextureSlotKind.Glow, "Glow", "sGlowTexture", m => m.GlowTexture,
				(m, v) => m.GlowTexture = v),
			tex.Texture(TextureSlotKind.InnerLayer, "Inner Layer", "sInnerLayerTexture", m => m.InnerLayerTexture,
				(m, v) => m.InnerLayerTexture = v, 0, 2),
			tex.Texture(TextureSlotKind.Wrinkles, "Wrinkles", "sWrinklesTexture", m => m.WrinklesTexture,
				(m, v) => m.WrinklesTexture = v),
			tex.Texture(TextureSlotKind.Displacement, "Displacement", "sDisplacementTexture",
				m => m.DisplacementTexture, (m, v) => m.DisplacementTexture = v, 0, 2),
			tex.Texture(TextureSlotKind.Specular, "Specular", "sSpecularTexture", m => m.SpecularTexture,
				(m, v) => m.SpecularTexture = v, 3),
			tex.Texture(TextureSlotKind.Lighting, "Lighting", "sLightingTexture", m => m.LightingTexture,
				(m, v) => m.LightingTexture = v, 3),
			tex.Texture(TextureSlotKind.Flow, "Flow", "sFlowTexture", m => m.FlowTexture,
				(m, v) => m.FlowTexture = v, 3),
			tex.Texture(TextureSlotKind.DistanceFieldAlpha, "Distance Field Alpha", "sDistanceFieldAlphaTexture",
				m => m.DistanceFieldAlphaTexture, (m, v) => m.DistanceFieldAlphaTexture = v, 17),
			tex.Text("Root Material", "sRootMaterialPath", m => m.RootMaterialPath, (m, v) => m.RootMaterialPath = v),
			.. CommonFields(),
			spec.Bool("Specular Enabled", "bSpecularEnabled", m => m.SpecularEnabled, (m, v) => m.SpecularEnabled = v),
			spec.Color("Specular Color", "cSpecularColor", m => m.SpecularColor, (m, v) => m.SpecularColor = v),
			spec.Float("Specular Multiplier", "fSpecularMult", m => m.SpecularMult, (m, v) => m.SpecularMult = v),
			spec.Float("Smoothness", "fSmoothness", m => m.Smoothness, (m, v) => m.Smoothness = v),
			spec.Float("Fresnel Power", "fFresnelPower", m => m.FresnelPower, (m, v) => m.FresnelPower = v),
			spec.Bool("Skew Specular Alpha", "bSkewSpecularAlpha", m => m.SkewSpecularAlpha,
				(m, v) => m.SkewSpecularAlpha = v, 1),
			spec.Bool("PBR", "bPBR", m => m.Pbr, (m, v) => m.Pbr = v, 3),
			spec.Bool("Custom Porosity", "bCustomPorosity", m => m.CustomPorosity, (m, v) => m.CustomPorosity = v, 9),
			spec.Float("Porosity", "fPorosityValue", m => m.PorosityValue, (m, v) => m.PorosityValue = v, 9),

			wet.Float("Spec Scale", "fWetnessControl_SpecScale", m => m.WetnessControlSpecScale,
				(m, v) => m.WetnessControlSpecScale = v),
			wet.Float("Spec Power Scale", "fWetnessControl_SpecPowerScale", m => m.WetnessControlSpecPowerScale,
				(m, v) => m.WetnessControlSpecPowerScale = v),
			wet.Float("Spec Minvar", "fWetnessControl_SpecMinvar", m => m.WetnessControlSpecMinvar,
				(m, v) => m.WetnessControlSpecMinvar = v),
			wet.Float("Environment Map Scale", "fWetnessControl_EnvMapScale", m => m.WetnessControlEnvMapScale,
				(m, v) => m.WetnessControlEnvMapScale = v, 0, 9),
			wet.Float("Fresnel Power", "fWetnessControl_FresnelPower", m => m.WetnessControlFresnelPower,
				(m, v) => m.WetnessControlFresnelPower = v),
			wet.Float("Metalness", "fWetnessControl_Metalness", m => m.WetnessControlMetalness,
				(m, v) => m.WetnessControlMetalness = v),

			light.Bool("Rim Lighting", "bRimLighting", m => m.RimLighting, (m, v) => m.RimLighting = v, 0, 7),
			light.Float("Rim Power", "fRimPower", m => m.RimPower, (m, v) => m.RimPower = v, 0, 7),
			light.Float("Back Light Power", "fBackLightPower", m => m.BackLightPower, (m, v) => m.BackLightPower = v,
				0, 7),
			light.Bool("Subsurface Lighting", "bSubsurfaceLighting", m => m.SubsurfaceLighting,
				(m, v) => m.SubsurfaceLighting = v, 0, 7),
			light.Float("Subsurface Rolloff", "fSubsurfaceLightingRolloff", m => m.SubsurfaceLightingRolloff,
				(m, v) => m.SubsurfaceLightingRolloff = v, 0, 7),
			light.Bool("Back Lighting", "bBackLighting", m => m.BackLighting, (m, v) => m.BackLighting = v, 0, 7),
			light.Bool("Aniso Lighting", "bAnisoLighting", m => m.AnisoLighting, (m, v) => m.AnisoLighting = v),
			light.Bool("Model Space Normals", "bModelSpaceNormals", m => m.ModelSpaceNormals,
				(m, v) => m.ModelSpaceNormals = v),
			light.Bool("Receive Shadows", "bReceiveShadows", m => m.ReceiveShadows, (m, v) => m.ReceiveShadows = v),
			light.Bool("Cast Shadows", "bCastShadows", m => m.CastShadows, (m, v) => m.CastShadows = v),
			light.Bool("Assume Shadowmask", "bAssumeShadowmask", m => m.AssumeShadowmask,
				(m, v) => m.AssumeShadowmask = v),
			light.Bool("Dissolve Fade", "bDissolveFade", m => m.DissolveFade, (m, v) => m.DissolveFade = v),

			emit.Bool("Emit Enabled", "bEmitEnabled", m => m.EmitEnabled, (m, v) => m.EmitEnabled = v),
			emit.Color("Emittance Color", "cEmittanceColor", m => m.EmittanceColor, (m, v) => m.EmittanceColor = v),
			emit.Float("Emittance Multiplier", "fEmittanceMult", m => m.EmittanceMult, (m, v) => m.EmittanceMult = v),
			emit.Bool("External Emittance", "bExternalEmittance", m => m.ExternalEmittance,
				(m, v) => m.ExternalEmittance = v),
			emit.Bool("Glowmap", "bGlowmap", m => m.Glowmap, (m, v) => m.Glowmap = v),
			emit.Float("Luminance Emittance", "fLumEmittance", m => m.LumEmittance, (m, v) => m.LumEmittance = v, 12),
			emit.Bool("Adaptive Emissive", "bUseAdaptativeEmissive", m => m.UseAdaptativeEmissive,
				(m, v) => m.UseAdaptativeEmissive = v, 13),
			emit.Float("Exposure Offset", "fAdaptativeEmissive_ExposureOffset",
				m => m.AdaptativeEmissiveExposureOffset, (m, v) => m.AdaptativeEmissiveExposureOffset = v, 13),
			emit.Float("Final Exposure Min", "fAdaptativeEmissive_FinalExposureMin",
				m => m.AdaptativeEmissiveFinalExposureMin, (m, v) => m.AdaptativeEmissiveFinalExposureMin = v, 13),
			emit.Float("Final Exposure Max", "fAdaptativeEmissive_FinalExposureMax",
				m => m.AdaptativeEmissiveFinalExposureMax, (m, v) => m.AdaptativeEmissiveFinalExposureMax = v, 13),

			tl.Bool("Translucency", "bTranslucency", m => m.Translucency, (m, v) => m.Translucency = v, 8),
			tl.Bool("Thick Object", "bTranslucencyThickObject", m => m.TranslucencyThickObject,
				(m, v) => m.TranslucencyThickObject = v, 8),
			tl.Bool("Mix Albedo With Subsurface Color", "bTranslucencyMixAlbedoWithSubsurfaceColor",
				m => m.TranslucencyMixAlbedoWithSubsurfaceColor,
				(m, v) => m.TranslucencyMixAlbedoWithSubsurfaceColor = v, 8),
			tl.Color("Subsurface Color", "cTranslucencySubsurfaceColor", m => m.TranslucencySubsurfaceColor,
				(m, v) => m.TranslucencySubsurfaceColor = v, 8),
			tl.Float("Transmissive Scale", "fTranslucencyTransmissiveScale", m => m.TranslucencyTransmissiveScale,
				(m, v) => m.TranslucencyTransmissiveScale = v, 8),
			tl.Float("Turbulence", "fTranslucencyTurbulence", m => m.TranslucencyTurbulence,
				(m, v) => m.TranslucencyTurbulence = v, 8),

			misc.Bool("Hair", "bHair", m => m.Hair, (m, v) => m.Hair = v),
			misc.Color("Hair Tint Color", "cHairTintColor", m => m.HairTintColor, (m, v) => m.HairTintColor = v),
			misc.Bool("Tree", "bTree", m => m.Tree, (m, v) => m.Tree = v),
			misc.Bool("Facegen", "bFacegen", m => m.Facegen, (m, v) => m.Facegen = v),
			misc.Bool("Skin Tint", "bSkinTint", m => m.SkinTint, (m, v) => m.SkinTint = v),
			misc.Bool("Hide Secret", "bHideSecret", m => m.HideSecret, (m, v) => m.HideSecret = v),
			misc.Bool("Environment Mapping Window", "bEnvironmentMappingWindow", m => m.EnvironmentMappingWindow,
				(m, v) => m.EnvironmentMappingWindow = v, 0, 6),
			misc.Bool("Environment Mapping Eye", "bEnvironmentMappingEye", m => m.EnvironmentMappingEye,
				(m, v) => m.EnvironmentMappingEye = v, 0, 6),

			tess.Bool("Tessellate", "bTessellate", m => m.Tessellate, (m, v) => m.Tessellate = v),
			tess.Float("Displacement Bias", "fDisplacementTextureBias", m => m.DisplacementTextureBias,
				(m, v) => m.DisplacementTextureBias = v, 0, 2),
			tess.Float("Displacement Scale", "fDisplacementTextureScale", m => m.DisplacementTextureScale,
				(m, v) => m.DisplacementTextureScale = v, 0, 2),
			tess.Float("PN Scale", "fTessellationPnScale", m => m.TessellationPnScale,
				(m, v) => m.TessellationPnScale = v, 0, 2),
			tess.Float("Base Factor", "fTessellationBaseFactor", m => m.TessellationBaseFactor,
				(m, v) => m.TessellationBaseFactor = v, 0, 2),
			tess.Float("Fade Distance", "fTessellationFadeDistance", m => m.TessellationFadeDistance,
				(m, v) => m.TessellationFadeDistance = v, 0, 2),

			terrain.Bool("Terrain", "bTerrain", m => m.Terrain, (m, v) => m.Terrain = v, 3),
			terrain.Float("Threshold Falloff", "fTerrainThresholdFalloff", m => m.TerrainThresholdFalloff,
				(m, v) => m.TerrainThresholdFalloff = v, 3),
			terrain.Float("Tiling Distance", "fTerrainTilingDistance", m => m.TerrainTilingDistance,
				(m, v) => m.TerrainTilingDistance = v, 3),
			terrain.Float("Rotation Angle", "fTerrainRotationAngle", m => m.TerrainRotationAngle,
				(m, v) => m.TerrainRotationAngle = v, 3),

			other.Bool("Enable Editor Alpha Ref", "bEnableEditorAlphaRef", m => m.EnableEditorAlphaRef,
				(m, v) => m.EnableEditorAlphaRef = v),
			other.Float("Grayscale To Palette Scale", "fGrayscaleToPaletteScale", m => m.GrayscaleToPaletteScale,
				(m, v) => m.GrayscaleToPaletteScale = v),
		];
		return fields;
	}

	private protected override void ReadBody(MaterialReader input)
	{
		base.ReadBody(input);

		DiffuseTexture = input.String();
		NormalTexture = input.String();
		SmoothSpecTexture = input.String();
		GreyscaleTexture = input.String();

		if (Version > 2)
		{
			GlowTexture = input.String();
			WrinklesTexture = input.String();
			SpecularTexture = input.String();
			LightingTexture = input.String();
			FlowTexture = input.String();
			if (Version >= 17) DistanceFieldAlphaTexture = input.String();
		}
		else
		{
			EnvmapTexture = input.String();
			GlowTexture = input.String();
			InnerLayerTexture = input.String();
			WrinklesTexture = input.String();
			DisplacementTexture = input.String();
		}

		EnableEditorAlphaRef = input.Bool();

		if (Version >= 8)
		{
			Translucency = input.Bool();
			TranslucencyThickObject = input.Bool();
			TranslucencyMixAlbedoWithSubsurfaceColor = input.Bool();
			TranslucencySubsurfaceColor = input.Color();
			TranslucencyTransmissiveScale = input.Single();
			TranslucencyTurbulence = input.Single();
		}
		else
		{
			RimLighting = input.Bool();
			RimPower = input.Single();
			BackLightPower = input.Single();
			SubsurfaceLighting = input.Bool();
			SubsurfaceLightingRolloff = input.Single();
		}

		SpecularEnabled = input.Bool();
		SpecularColor = input.Color();
		SpecularMult = input.Single();
		Smoothness = input.Single();

		FresnelPower = input.Single();
		WetnessControlSpecScale = input.Single();
		WetnessControlSpecPowerScale = input.Single();
		WetnessControlSpecMinvar = input.Single();
		if (Version < 10) WetnessControlEnvMapScale = input.Single();

		WetnessControlFresnelPower = input.Single();
		WetnessControlMetalness = input.Single();

		if (Version > 2)
		{
			Pbr = input.Bool();
			if (Version >= 9)
			{
				CustomPorosity = input.Bool();
				PorosityValue = input.Single();
			}
		}

		RootMaterialPath = input.String();

		AnisoLighting = input.Bool();
		EmitEnabled = input.Bool();
		if (EmitEnabled) EmittanceColor = input.Color();

		EmittanceMult = input.Single();
		ModelSpaceNormals = input.Bool();
		ExternalEmittance = input.Bool();

		if (Version >= 12) LumEmittance = input.Single();

		if (Version >= 13)
		{
			UseAdaptativeEmissive = input.Bool();
			AdaptativeEmissiveExposureOffset = input.Single();
			AdaptativeEmissiveFinalExposureMin = input.Single();
			AdaptativeEmissiveFinalExposureMax = input.Single();
		}

		if (Version < 8) BackLighting = input.Bool();

		ReceiveShadows = input.Bool();
		HideSecret = input.Bool();
		CastShadows = input.Bool();
		DissolveFade = input.Bool();
		AssumeShadowmask = input.Bool();
		Glowmap = input.Bool();

		if (Version < 7)
		{
			EnvironmentMappingWindow = input.Bool();
			EnvironmentMappingEye = input.Bool();
		}

		Hair = input.Bool();
		HairTintColor = input.Color();
		Tree = input.Bool();
		Facegen = input.Bool();
		SkinTint = input.Bool();
		Tessellate = input.Bool();

		if (Version < 3)
		{
			DisplacementTextureBias = input.Single();
			DisplacementTextureScale = input.Single();
			TessellationPnScale = input.Single();
			TessellationBaseFactor = input.Single();
			TessellationFadeDistance = input.Single();
		}

		GrayscaleToPaletteScale = input.Single();

		if (Version >= 1) SkewSpecularAlpha = input.Bool();

		if (Version >= 3)
		{
			Terrain = input.Bool();
			if (Terrain)
			{
				if (Version == 3) TerrainUnknown = input.UInt32();

				TerrainThresholdFalloff = input.Single();
				TerrainTilingDistance = input.Single();
				TerrainRotationAngle = input.Single();
			}
		}
	}

	private protected override void WriteBody(MaterialWriter output)
	{
		base.WriteBody(output);

		output.String(DiffuseTexture);
		output.String(NormalTexture);
		output.String(SmoothSpecTexture);
		output.String(GreyscaleTexture);

		if (Version > 2)
		{
			output.String(GlowTexture);
			output.String(WrinklesTexture);
			output.String(SpecularTexture);
			output.String(LightingTexture);
			output.String(FlowTexture);
			if (Version >= 17) output.String(DistanceFieldAlphaTexture);
		}
		else
		{
			output.String(EnvmapTexture);
			output.String(GlowTexture);
			output.String(InnerLayerTexture);
			output.String(WrinklesTexture);
			output.String(DisplacementTexture);
		}

		output.Bool(EnableEditorAlphaRef);

		if (Version >= 8)
		{
			output.Bool(Translucency);
			output.Bool(TranslucencyThickObject);
			output.Bool(TranslucencyMixAlbedoWithSubsurfaceColor);
			output.Color(TranslucencySubsurfaceColor);
			output.Single(TranslucencyTransmissiveScale);
			output.Single(TranslucencyTurbulence);
		}
		else
		{
			output.Bool(RimLighting);
			output.Single(RimPower);
			output.Single(BackLightPower);
			output.Bool(SubsurfaceLighting);
			output.Single(SubsurfaceLightingRolloff);
		}

		output.Bool(SpecularEnabled);
		output.Color(SpecularColor);
		output.Single(SpecularMult);
		output.Single(Smoothness);

		output.Single(FresnelPower);
		output.Single(WetnessControlSpecScale);
		output.Single(WetnessControlSpecPowerScale);
		output.Single(WetnessControlSpecMinvar);
		if (Version < 10) output.Single(WetnessControlEnvMapScale);

		output.Single(WetnessControlFresnelPower);
		output.Single(WetnessControlMetalness);

		if (Version > 2)
		{
			output.Bool(Pbr);
			if (Version >= 9)
			{
				output.Bool(CustomPorosity);
				output.Single(PorosityValue);
			}
		}

		output.String(RootMaterialPath);

		output.Bool(AnisoLighting);
		output.Bool(EmitEnabled);
		if (EmitEnabled) output.Color(EmittanceColor);

		output.Single(EmittanceMult);
		output.Bool(ModelSpaceNormals);
		output.Bool(ExternalEmittance);

		if (Version >= 12) output.Single(LumEmittance);

		if (Version >= 13)
		{
			output.Bool(UseAdaptativeEmissive);
			output.Single(AdaptativeEmissiveExposureOffset);
			output.Single(AdaptativeEmissiveFinalExposureMin);
			output.Single(AdaptativeEmissiveFinalExposureMax);
		}

		if (Version < 8) output.Bool(BackLighting);

		output.Bool(ReceiveShadows);
		output.Bool(HideSecret);
		output.Bool(CastShadows);
		output.Bool(DissolveFade);
		output.Bool(AssumeShadowmask);
		output.Bool(Glowmap);

		if (Version < 7)
		{
			output.Bool(EnvironmentMappingWindow);
			output.Bool(EnvironmentMappingEye);
		}

		output.Bool(Hair);
		output.Color(HairTintColor);
		output.Bool(Tree);
		output.Bool(Facegen);
		output.Bool(SkinTint);
		output.Bool(Tessellate);

		if (Version < 3)
		{
			output.Single(DisplacementTextureBias);
			output.Single(DisplacementTextureScale);
			output.Single(TessellationPnScale);
			output.Single(TessellationBaseFactor);
			output.Single(TessellationFadeDistance);
		}

		output.Single(GrayscaleToPaletteScale);

		if (Version >= 1) output.Bool(SkewSpecularAlpha);

		if (Version >= 3)
		{
			output.Bool(Terrain);
			if (Terrain)
			{
				if (Version == 3) output.UInt32(TerrainUnknown);

				output.Single(TerrainThresholdFalloff);
				output.Single(TerrainTilingDistance);
				output.Single(TerrainRotationAngle);
			}
		}
	}
}
