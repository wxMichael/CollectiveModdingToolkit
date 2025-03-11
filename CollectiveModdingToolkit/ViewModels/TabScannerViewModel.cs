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

using Avalonia.Collections;
using CollectiveModdingToolkit.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace CollectiveModdingToolkit.ViewModels;

public partial class TabScannerViewModel : ViewModelBase
{
	[ObservableProperty]
	public partial AvaloniaDictionary<string, ObservableCollection<ProblemInfo>> IssuesByMod { get; set; }
	[ObservableProperty]
	public partial ObservableCollection<ProblemInfo> Issues { get; set; } = [];

	private string? CurrentMod { get; set; } = null;


	public TabScannerViewModel()
	{
		//Problems = [
		//	new("prp.esp", @"C:\Mods\PRP\prp.esp", PathType.File, "HEDR Version Bad", "PRP", "DELET"),
		//	new("ufo4p.esp", @"C:\Mods\UFO4P\ufo4p.esp", PathType.File, "HEDR Version Bad", "UFO4P", "DELET"),
		//];
		IssuesByMod = new() {
			{ "PRP", [
				new("ppf.esm", @"C:\Mods\PRP\ppf.esm", PathType.File, "HEDR Version Bad", "PRP", "DELET"),
				new("prp.esp", @"C:\Mods\PRP\prp.esp", PathType.File, "Something Icky", "PRP", "DELET"),
			] },
			{ "UFO4P", [
				new("ufo4p.esp", @"C:\Mods\UFO4P\ufo4p.esp", PathType.File, "HEDR Version Bad", "UFO4P", "DELET"),
				new("ufo4p.ba2", @"C:\Mods\UFO4P\ufo4p.ba2", PathType.File, "Invalid BA2 Name", "UFO4P", "DELET"),
				new("ufo4p - Stuff.ba2", @"C:\Mods\UFO4P\ufo4p - Stuff.ba2", PathType.File, "Invalid BA2 Name", "UFO4P", "DELET")
			] },
		};
	}

	public void SetSelectedMod(object? selectedItem)
	{
		if (selectedItem == null)
		{
			if (CurrentMod != null)
			{
				CurrentMod = null;
				Issues = [];
			}
			return;
		}
		if (selectedItem is string item)
		{
			if (item == CurrentMod)
				return;

			CurrentMod = item;
			Issues = IssuesByMod[item];
		}
	}
}
