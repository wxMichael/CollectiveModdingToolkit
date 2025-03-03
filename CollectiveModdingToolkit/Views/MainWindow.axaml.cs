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
using CollectiveModdingToolkit.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CollectiveModdingToolkit.Views;

public partial class MainWindow : Window
{
	private static bool _criticalErrorOccurred = false;
	private static bool _dialogHostLoaded = false;

	public MainWindow()
	{
		InitializeComponent();
		KeyDown += MainWindow_KeyDown;
	}

	private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
			Close();
	}

	internal static void CriticalErrorDialog(object? sender = null, RoutedEventArgs? e = null) => CriticalError();

	internal static void DialogHost_Loaded(object? sender = null, RoutedEventArgs? e = null) => _dialogHostLoaded = true;

	public static async void CriticalError(Type? type = null, string? message = null)
	{
		if (_criticalErrorOccurred)
			return;
		_criticalErrorOccurred = true;

		if (Computer.Instance == null)
			throw new InvalidOperationException(message);

		//if (Computer.Instance.ErrorMessage != string.Empty)
		//	return;

		while (!_dialogHostLoaded)
			await Task.Run(() => Thread.Sleep(100));

		if (type == typeof(DirectoryNotFoundException))
			Computer.Instance.ErrorMessage = $"Directory not found:\n{message}";
		else if (type == typeof(FileNotFoundException))
			Computer.Instance.ErrorMessage = $"File not found:\n{message}";
		else if (type == typeof(FileNotFoundException))
			Computer.Instance.ErrorMessage = $"File not found:\n{message}";
		else if (type == typeof(NotImplementedException))
			Computer.Instance.ErrorMessage = $"Handling for scenario not yet implemented.\n{message}";
		else
		{
			message ??= "Unspecified Error";
			Computer.Instance.ErrorMessage = message;
		}

		Computer.Instance.CriticalErrorOpen = true;
	}
}
