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
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using CollectiveModdingToolkit.Models;

namespace CollectiveModdingToolkit.Views;

/// <summary>Lists the bundled third-party components. Each license name opens that license on its own.</summary>
public partial class LicensesWindow : Window
{
	private const string NoticesUri = "avares://CollectiveModdingToolkit/Assets/THIRD-PARTY-NOTICES.txt";

	// ReSharper disable once MemberCanBePrivate.Global
	public LicensesWindow()
	{
		InitializeComponent();
		LicenseList.ItemsSource = LoadNotices();
		KeyDown += (_, e) =>
		{
			if (e.Key == Key.Escape) Close();
		};
	}

	private static LicenseNotice[] LoadNotices()
	{
		try
		{
			using var stream = AssetLoader.Open(new Uri(NoticesUri));
			using StreamReader reader = new(stream);
			return [.. LicenseNotice.Parse(reader.ReadToEnd())];
		}
		catch (Exception e)
		{
			return [new LicenseNotice("The license notices could not be loaded", "Details", "", e.Message)];
		}
	}

	public static void Open(Window? owner)
	{
		LicensesWindow window = new();
		if (owner != null)
			window.ShowDialog(owner);
		else
			window.Show();
	}

	private void Button_Click_ShowLicense(object? sender, RoutedEventArgs e)
	{
		if ((sender as Control)?.DataContext is LicenseNotice notice) LicenseTextWindow.Open(this, notice);
	}

	private void Button_Click_Close(object? sender, RoutedEventArgs e)
	{
		Close();
	}
}
