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
using System.Globalization;
using System.Linq;

namespace CollectiveModdingToolkit.Materials;

/// <summary>An RGB color stored as three floats, exactly as the file has it.</summary>
public readonly record struct MaterialColor(float R, float G, float B)
{
	public static MaterialColor White { get; } = new(1, 1, 1);

	/// <summary>Lowercase "#rrggbb".</summary>
	public string ToHex() => $"#{Channel(R):x2}{Channel(G):x2}{Channel(B):x2}";

	/// <summary>Parses "#rgb", "#rrggbb" (the # is optional). Returns null if the text isn't a color.</summary>
	public static MaterialColor? TryParseHex(string? text)
	{
		if (text == null) return null;

		var hex = text.Trim().TrimStart('#');
		if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));

		if (hex.Length != 6 ||
		    !uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var v))
			return null;

		const float scale = 1f / 255;
		return new MaterialColor(((v >> 16) & 0xFF) * scale, ((v >> 8) & 0xFF) * scale, (v & 0xFF) * scale);
	}

	private static byte Channel(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255), 0, 255);
}
