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

namespace CollectiveModdingToolkit.Materials;

public enum MaterialFieldKind
{
	Bool,
	Float,
	Byte,
	Color,
	Text,
	Texture,
	Enum,
}

/// <summary>What a texture slot is meant to hold. Used to check the texture assigned to it.</summary>
public enum TextureSlotKind
{
	Diffuse,
	Normal,
	SmoothSpec,
	Specular,
	Greyscale,
	Environment,
	EnvironmentMask,
	Glow,
	Lighting,
	Wrinkles,
	Flow,
	Displacement,
	InnerLayer,
	DistanceFieldAlpha,
	GlassRoughness,
	GlassDirt,
}

/// <summary>
///     Describes one editable value of a material: how to show it, where it is in JSON, which versions have it, and how
///     to get and set it. The editor and the JSON reader are driven by these, so they never need reflection.
/// </summary>
public sealed class MaterialField
{
	private readonly Func<MaterialFile, object> _get;
	private readonly Action<MaterialFile, object> _set;

	internal MaterialField(string group, string label, string jsonKey, MaterialFieldKind kind,
		Func<MaterialFile, object> get, Action<MaterialFile, object> set, int minVersion, int maxVersion)
	{
		Group = group;
		Label = label;
		JsonKey = jsonKey;
		Kind = kind;
		MinVersion = minVersion;
		MaxVersion = maxVersion;
		_get = get;
		_set = set;
	}

	/// <summary>The section this field is shown in.</summary>
	public string Group { get; }

	public string Label { get; }

	/// <summary>The property name used by JSON materials, e.g. "sDiffuseTexture".</summary>
	public string JsonKey { get; }

	public MaterialFieldKind Kind { get; }

	/// <summary>For <see cref="MaterialFieldKind.Texture" /> fields, what the slot is for.</summary>
	public TextureSlotKind? Slot { get; internal init; }

	/// <summary>For <see cref="MaterialFieldKind.Enum" /> fields, the allowed values.</summary>
	public IReadOnlyList<string>? Options { get; internal init; }

	/// <summary>First file version that stores this field.</summary>
	public int MinVersion { get; }

	/// <summary>Last file version that stores this field.</summary>
	public int MaxVersion { get; }

	public bool AppliesTo(uint version) => version >= MinVersion && version <= MaxVersion;

	/// <summary>The value as bool, float, byte, <see cref="MaterialColor" />, or string.</summary>
	public object GetValue(MaterialFile file) => _get(file);

	/// <summary>Sets the value. It must be the type <see cref="GetValue" /> returns.</summary>
	public void SetValue(MaterialFile file, object value) => _set(file, value);
}

/// <summary>Builds the <see cref="MaterialField" /> lists for one kind of material.</summary>
internal sealed class FieldBuilder<T>(string group) where T : MaterialFile
{
	public MaterialField Bool(string label, string key, Func<T, bool> get, Action<T, bool> set, int min = 0,
		int max = int.MaxValue) =>
		new(group, label, key, MaterialFieldKind.Bool, f => get((T)f), (f, v) => set((T)f, (bool)v), min, max);

	public MaterialField Float(string label, string key, Func<T, float> get, Action<T, float> set, int min = 0,
		int max = int.MaxValue) =>
		new(group, label, key, MaterialFieldKind.Float, f => get((T)f), (f, v) => set((T)f, (float)v), min, max);

	public MaterialField Byte(string label, string key, Func<T, byte> get, Action<T, byte> set, int min = 0,
		int max = int.MaxValue) =>
		new(group, label, key, MaterialFieldKind.Byte, f => get((T)f), (f, v) => set((T)f, (byte)v), min, max);

	public MaterialField Color(string label, string key, Func<T, MaterialColor> get, Action<T, MaterialColor> set,
		int min = 0, int max = int.MaxValue) =>
		new(group, label, key, MaterialFieldKind.Color, f => get((T)f), (f, v) => set((T)f, (MaterialColor)v), min,
			max);

	public MaterialField Text(string label, string key, Func<T, string> get, Action<T, string> set, int min = 0,
		int max = int.MaxValue) =>
		new(group, label, key, MaterialFieldKind.Text, f => get((T)f), (f, v) => set((T)f, (string)v), min, max);

	public MaterialField Texture(TextureSlotKind slot, string label, string key, Func<T, string> get,
		Action<T, string> set, int min = 0, int max = int.MaxValue) =>
		new(group, label, key, MaterialFieldKind.Texture, f => get((T)f), (f, v) => set((T)f, (string)v), min, max)
		{
			Slot = slot,
		};

	public MaterialField Enum(string label, string key, IReadOnlyList<string> options, Func<T, string> get,
		Action<T, string> set, int min = 0, int max = int.MaxValue) =>
		new(group, label, key, MaterialFieldKind.Enum, f => get((T)f), (f, v) => set((T)f, (string)v), min, max)
		{
			Options = options,
		};
}
