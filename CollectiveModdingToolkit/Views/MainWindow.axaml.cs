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
using CollectiveModdingToolkit.Services;
using CollectiveModdingToolkit.ViewModels;

namespace CollectiveModdingToolkit.Views;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		KeyDown += MainWindow_KeyDown;
		Opened += async (_, _) =>
		{
			if (DataContext is MainWindowViewModel viewModel) await viewModel.InitializeAsync();
		};
	}

	private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape) Close();
	}

	public void DialogHost_Loaded(object? sender = null, RoutedEventArgs? e = null) =>
		ErrorReporter.Current.MarkHostLoaded();

	private void Button_Click_Exit(object? sender, RoutedEventArgs e) => Close();
}
