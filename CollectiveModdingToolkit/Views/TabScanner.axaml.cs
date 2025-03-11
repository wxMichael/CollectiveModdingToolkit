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
using CollectiveModdingToolkit.ViewModels;

namespace CollectiveModdingToolkit.Views;

public partial class TabScanner : UserControl
{
	public TabScanner() => InitializeComponent();

	private void DataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		e.Handled = true;
		if (sender is DataGrid dataGrid)
		{
			dataGrid.SelectedIndex = -1;
			dataGrid.SelectedItem = null;
		}
	}

	private void DataGrid_Sorting(object? sender, DataGridColumnEventArgs e)
	{
	}

	private void ListBox_SelectionChanged_Mod(object? sender, SelectionChangedEventArgs e)
	{
		if (sender is ListBox listBox && DataContext is TabScannerViewModel viewModel)
			viewModel.SetSelectedMod(listBox.SelectedItem);
	}

	private void ListBox_SelectionChanged_Issue(object? sender, SelectionChangedEventArgs e)
	{
		//	e.Handled = true;
		//	if (sender is ListBox listBox)
		//	{
		//		listBox.SelectedIndex = -1;
		//		listBox.SelectedItem = null;
		//	}
	}
}
