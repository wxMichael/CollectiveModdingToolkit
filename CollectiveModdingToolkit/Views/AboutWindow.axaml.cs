/*
 * 
 * Collective Modding Toolkit
 * Copyright (C) 2024, 2025  wxMichael
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

namespace CollectiveModdingToolkit.Views;

public partial class AboutWindow : Window
{
	public AboutWindow()
	{
		InitializeComponent();

		KeyDown += AboutWindow_KeyDown;
	}

	private void AboutWindow_KeyDown(object? sender, KeyEventArgs e)
	{
		//if (e.Key is Key.Space or Key.Escape)
		if (e.Key == Key.Escape)
			Close();
	}

	public static void Open(string title, string text)
	{
		AboutWindow aboutWindow = new()
		{
			Title = title
		};
		aboutWindow.AboutText.Text = text;
		aboutWindow.Height = text.CountLines() * 30; // TODO: Improve TextBlock size calculation.
		aboutWindow.ShowDialog(MainWindow.Instance!);
	}

	private void Button_Click_Close(object? sender, RoutedEventArgs e) => Close();
}
