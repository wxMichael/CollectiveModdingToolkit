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

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CollectiveModdingToolkit.Models;

namespace CollectiveModdingToolkit.Views;

/// <summary>Shows one third-party license.</summary>
public partial class LicenseTextWindow : Window
{
	// ReSharper disable once MemberCanBePrivate.Global
	public LicenseTextWindow()
	{
		InitializeComponent();
		KeyDown += (_, e) =>
		{
			if (e.Key == Key.Escape) Close();
		};
	}

	public static void Open(Window? owner, LicenseNotice notice)
	{
		LicenseTextWindow window = new()
		{
			Title = $"{notice.Name} - {notice.License}",
			DetailsText = { Text = notice.Details, IsVisible = notice.Details.Length > 0 },
			LicenseText = { Text = notice.Text },
		};
		if (owner != null)
			window.ShowDialog(owner);
		else
			window.Show();
	}

	private void Button_Click_Close(object? sender, RoutedEventArgs e) => Close();
}
