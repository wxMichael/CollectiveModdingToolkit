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
using CollectiveModdingToolkit.ViewModels;

namespace CollectiveModdingToolkit.Views;

public partial class TabScanner : UserControl
{
	public TabScanner() => InitializeComponent();

	private TabScannerViewModel? ViewModel => DataContext as TabScannerViewModel;

	private void Pointer_Pressed_OpenLocation(object? sender, PointerPressedEventArgs e) =>
		ViewModel?.RevealIssueCommand.Execute(null);

	// Left-click opens, right-click copies; telling the buttons apart is the only view concern here.
	private void Pointer_Pressed_Url(object? sender, PointerPressedEventArgs e)
	{
		var properties = e.GetCurrentPoint(this).Properties;
		if (properties.IsLeftButtonPressed)
			ViewModel?.OpenIssueUrlCommand.Execute(null);
		else if (properties.IsRightButtonPressed) ViewModel?.CopyIssueUrlCommand.Execute(null);
	}
}
