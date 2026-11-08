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
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CollectiveModdingToolkit.Views;

/// <summary>A scrollable list of lines, for problems that are about many files at once.</summary>
public partial class FileListWindow : Window
{
	// ReSharper disable once MemberCanBePrivate.Global
	public FileListWindow()
	{
		InitializeComponent();
		KeyDown += (_, e) =>
		{
			if (e.Key == Key.Escape) Close();
		};
	}

	public static void Open(Window? owner, string title, string info, IReadOnlyList<string> lines)
	{
		FileListWindow window = new()
		{
			Title = title,
			InfoText = { Text = info, IsVisible = info.Length > 0 },
			FileList = { ItemsSource = lines },
		};
		if (owner != null)
			window.ShowDialog(owner);
		else
			window.Show();
	}

	private void Button_Click_Close(object? sender, RoutedEventArgs e)
	{
		Close();
	}
}
