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
using Avalonia;
using Avalonia.Media;

namespace CollectiveModdingToolkit.Services;

/// <summary>The two colors that identify a game. Everything else (hover, pressed, tints, text-on-accent) is derived.</summary>
public readonly record struct AccentScheme(Color Base, Color Bright);

/// <summary>
///     Drives runtime theming. Colors live as <c>*Color</c> resources (Theme/Colors.axaml); the semantic
///     brushes in Theme/Brushes.axaml and the Semi.Avalonia overrides all follow them via DynamicResource,
///     so setting a color here recolors the whole UI immediately.
/// </summary>
public static class ThemeService
{
	private static IReadOnlyDictionary<string, AccentScheme> GameAccents { get; } =
		new Dictionary<string, AccentScheme>(StringComparer.OrdinalIgnoreCase)
		{
			["Fallout4"] = new(Color.Parse("#05B62E"), Color.Parse("#57FF64")),
		};

	public static string CurrentGame { get; private set; } = "Fallout4";

	public static void ApplyGame(string gameId)
	{
		if (!GameAccents.TryGetValue(gameId, out var accent)) return;

		CurrentGame = gameId;
		ApplyAccent(accent);
	}

	private static void ApplyAccent(AccentScheme accent)
	{
		var baseColor = accent.Base;
		SetColor("AccentColor", baseColor);
		SetColor("AccentBrightColor", accent.Bright);
		SetColor("AccentHoverColor", Mix(baseColor, Colors.White, 0.15));
		SetColor("AccentActiveColor", Mix(baseColor, Colors.Black, 0.20));
		SetColor("AccentDimColor", Mix(baseColor, Colors.Black, 0.65));
		SetColor("AccentTintColor", Color.FromArgb(0x33, baseColor.R, baseColor.G, baseColor.B));
		SetColor("OnAccentColor", PickReadableOn(baseColor));
	}

	/// <summary>Overrides any color in Theme/Colors.axaml (e.g. <c>GoodColor</c>) for user color schemes.</summary>
	private static void SetColor(string key, Color color)
	{
		if (Application.Current is { } app) app.Resources[key] = color;
	}

	private static Color Mix(Color from, Color to, double t) =>
		Color.FromRgb(
			(byte)Math.Round(from.R + (to.R - from.R) * t),
			(byte)Math.Round(from.G + (to.G - from.G) * t),
			(byte)Math.Round(from.B + (to.B - from.B) * t));

	/// <summary>Near-black or white, whichever has the higher WCAG contrast against <paramref name="background" />.</summary>
	private static Color PickReadableOn(Color background)
	{
		var dark = Color.Parse("#0D0D0D");
		var bg = Luminance(background);
		var contrastDark = (bg + 0.05) / (Luminance(dark) + 0.05);
		var contrastWhite = 1.05 / (bg + 0.05);
		return contrastDark >= contrastWhite ? dark : Colors.White;
	}

	private static double Luminance(Color c)
	{
		return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);

		static double Lin(byte v)
		{
			var s = v / 255.0;
			return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
		}
	}
}
