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

namespace CollectiveModdingToolkit.Views;

public partial class TabTools : UserControl
{
	private const string _bethiniURL = "https://www.nexusmods.com/site/mods/631";
	private const string _classicURL = "https://www.nexusmods.com/fallout4/mods/56255";
	private const string _vefsURL = "https://www.nexusmods.com/fallout4/mods/86374";
	private const string _pjmURL = "https://www.nexusmods.com/fallout4/mods/69978";
	private const string _ddsScanURL = "https://www.nexusmods.com/fallout4/mods/71588";
	private const string _xEditURL = "https://github.com/TES5Edit/TES5Edit#xedit";
	private const string _ckpeURL = "https://www.nexusmods.com/fallout4/mods/51165";
	private const string _unpackrrURL = "https://www.nexusmods.com/fallout4/mods/82082";
	private const string _caoURL = "https://www.nexusmods.com/skyrimspecialedition/mods/23316";
	private const string _bmatURL = "https://www.nexusmods.com/fallout4/mods/89306";
	private const string _iceStormURL = "https://storage.icestormng-mods.de/s/QG43aExydefeGXy";
	private const string _capFrameXURL = "https://www.capframex.com/";

	public TabTools() => InitializeComponent();

	private void Button_Click_OpenBethini(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_bethiniURL);
	private void Button_Click_OpenCLASSIC(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_classicURL);
	private void Button_Click_OpenVEFS(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_vefsURL);
	private void Button_Click_OpenPJM(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_pjmURL);
	private void Button_Click_OpenDDSScanner(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_ddsScanURL);
	private void Button_Click_OpenxEdit(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_xEditURL);
	private void Button_Click_OpenCKPE(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_ckpeURL);
	private void Button_Click_OpenUnpackrr(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_unpackrrURL);
	private void Button_Click_OpenCAO(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_caoURL);
	private void Button_Click_OpenBMAT(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_bmatURL);
	private void Button_Click_OpenIceStormTT(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_iceStormURL);
	private void Button_Click_OpenCapFrameX(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_capFrameXURL);

	private void Button_Click_OpenDowngradeManager(object? sender, RoutedEventArgs e)
	{
		//ArchivePatcher patcher = new();
		//patcher.Show();
	}

	private void Button_Click_OpenArchivePatcher(object? sender, RoutedEventArgs e)
	{
		ArchivePatcher archivePatcher = new()
		{
			DataContext = DataContext
		};
		archivePatcher.Show();
	}
}
