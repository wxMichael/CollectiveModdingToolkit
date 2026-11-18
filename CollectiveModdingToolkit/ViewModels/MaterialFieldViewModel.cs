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
using System.Globalization;
using Avalonia.Media;
using CollectiveModdingToolkit.Materials;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectiveModdingToolkit.ViewModels;

/// <summary>One editable value of the open material, as the editor shows it.</summary>
public sealed partial class MaterialFieldViewModel : ObservableObject
{
	private readonly Action _changed;
	private readonly MaterialField _field;
	private readonly MaterialFile _file;
	private bool _refreshing;

	public MaterialFieldViewModel(MaterialFile file, MaterialField field, Action changed)
	{
		_file = file;
		_field = field;
		_changed = changed;
		Refresh();
	}

	[ObservableProperty] public partial string Text { get; set; } = string.Empty;

	/// <summary>True while <see cref="Text" /> can't be turned into a value.</summary>
	[ObservableProperty]
	public partial bool IsInvalid { get; private set; }

	public string Label => _field.Label;
	public string Group => _field.Group;
	public MaterialFieldKind Kind => _field.Kind;
	public IReadOnlyList<string>? Options => _field.Options;
	public string JsonKey => _field.JsonKey;

	public bool AppliesToVersion => _field.AppliesTo(_file.Version);

	public bool IsBool => Kind == MaterialFieldKind.Bool;
	public bool IsColor => Kind == MaterialFieldKind.Color;
	public bool IsEnum => Kind == MaterialFieldKind.Enum;
	public bool IsText => Kind is MaterialFieldKind.Text or MaterialFieldKind.Texture;
	public bool IsNumber => Kind is MaterialFieldKind.Float or MaterialFieldKind.Byte;

	public decimal NumberMaximum => Kind == MaterialFieldKind.Byte ? byte.MaxValue : decimal.MaxValue;
	public decimal NumberIncrement => Kind == MaterialFieldKind.Byte ? 1 : 0.1m;
	public string NumberFormat => Kind == MaterialFieldKind.Byte ? "0" : "0.######";

	/// <summary>The value of a float or byte field. Null while the box is empty, which changes nothing.</summary>
	public decimal? Number
	{
		get
		{
			try
			{
				return _field.GetValue(_file) switch
				{
					float f => (decimal)f,
					byte b => b,
					_ => null,
				};
			}
			catch (OverflowException)
			{
				// Floats beyond decimal's range can't be shown; leave them as they are.
				return null;
			}
		}
		set
		{
			if (value is not { } number || number == Number) return;

			_field.SetValue(_file,
				Kind == MaterialFieldKind.Byte ? (object)(byte)Math.Clamp(number, 0, 255) : (float)number);
			OnPropertyChanged();
			_changed();
		}
	}

	/// <summary>The color as the picker shows it.</summary>
	public Color PickerColor
	{
		get => IsColor && _field.GetValue(_file) is MaterialColor c
			? Color.FromRgb(ToByte(c.R), ToByte(c.G), ToByte(c.B))
			: default;
		set
		{
			if (!IsColor || value == PickerColor) return;

			const float scale = 1f / 255;
			_field.SetValue(_file, new MaterialColor(value.R * scale, value.G * scale, value.B * scale));
			Text = ((MaterialColor)_field.GetValue(_file)).ToHex();
			OnPropertyChanged();
			OnPropertyChanged(nameof(Swatch));
			_changed();
		}
	}

	public bool BoolValue
	{
		get => IsBool && (bool)_field.GetValue(_file);
		set
		{
			if (BoolValue == value) return;

			_field.SetValue(_file, value);
			OnPropertyChanged();
			_changed();
		}
	}

	/// <summary>The color to show beside a color's hex text.</summary>
	public IBrush? Swatch => IsColor && _field.GetValue(_file) is MaterialColor c
		? new SolidColorBrush(Color.FromRgb(ToByte(c.R), ToByte(c.G), ToByte(c.B)))
		: null;

	/// <summary>Re-reads the value from the material, e.g. after the version changed what's stored.</summary>
	public void Refresh()
	{
		_refreshing = true;
		Text = _field.GetValue(_file) switch
		{
			float f => f.ToString(CultureInfo.InvariantCulture),
			byte b => b.ToString(CultureInfo.InvariantCulture),
			MaterialColor c => c.ToHex(),
			string s => s,
			_ => string.Empty,
		};
		_refreshing = false;
		IsInvalid = false;
		OnPropertyChanged(nameof(BoolValue));
		OnPropertyChanged(nameof(Number));
		OnPropertyChanged(nameof(PickerColor));
		OnPropertyChanged(nameof(Swatch));
		OnPropertyChanged(nameof(AppliesToVersion));
	}

	partial void OnTextChanged(string value)
	{
		if (_refreshing || IsBool) return;

		object? parsed = Kind switch
		{
			MaterialFieldKind.Color => MaterialColor.TryParseHex(value) is { } c ? c : null,
			_ => value,
		};

		IsInvalid = parsed == null;
		if (parsed == null) return;

		// Typing a color keeps the exact floats unless the hex text actually changed.
		if (parsed is MaterialColor color && _field.GetValue(_file) is MaterialColor old &&
		    old.ToHex() == color.ToHex())
			return;

		_field.SetValue(_file, parsed);
		OnPropertyChanged(nameof(Swatch));
		OnPropertyChanged(nameof(PickerColor));
		_changed();
	}

	private static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255), 0, 255);
}

/// <summary>A titled section of <see cref="MaterialFieldViewModel" />s.</summary>
public sealed class MaterialGroupViewModel(string name, IReadOnlyList<MaterialFieldViewModel> fields)
{
	public string Name { get; } = name;
	public IReadOnlyList<MaterialFieldViewModel> Fields { get; } = fields;
}
