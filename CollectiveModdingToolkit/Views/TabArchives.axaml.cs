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
using Avalonia.Interactivity;
using CollectiveModdingToolkit.ViewModels;

namespace CollectiveModdingToolkit.Views;

public partial class TabArchives : UserControl
{
	//ABOUT_ARCHIVES_TITLE = "Bethesda Archive (BA2) Formats & Versions"

	private static readonly string aboutTitle = "About Archives";
	private static readonly string aboutText = """
		There are 2 formats and 3 versions for Fallout 4 BA2 files:
		
		• General (GNRL)
		• Textures (DX10)
		
		• v1: Supported by all FO4 game versions. Required by v1.10.163 and earlier.
		• v7/8: Only supported by FO4 v1.10.980 and later.
		
		It's suspected there are format differences between versions for XBox/PS, but for PC the only difference is the number itself.
		v7/8 are identical so this tool only patches to v1 & v8.
		
		Why Patch Versions?
		
		Patching is ONLY needed if you use older apps that require it.
		Apps may check the version to ensure compatibility but v7/8 didn't exist when most apps were made, so they assume it's a different format and show errors.
		Because they're actually identical, you can just patch the version number in the file header and such apps will read them.
		""";

	public TabArchives() => InitializeComponent();

	private void Button_Click_About(object? sender, RoutedEventArgs e) => AboutWindow.Open(aboutTitle, aboutText);

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
		if (MainWindowViewModel.Instance == null)
			return;

		MainWindowViewModel.Instance.ArchiveSortColumn = e.Column.Header.ToString() ?? "Archive";
	}
}
