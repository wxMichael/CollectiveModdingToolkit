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
using CollectiveModdingToolkit.Models.FileTypes;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CollectiveModdingToolkit.Managers;

internal partial class Vortex : ObservableObject, IModManager
{
	public Vortex(ProcessModule? processModule) => throw new NotImplementedException();

	public string AppName => throw new NotImplementedException();

	public string? AppPath => throw new NotImplementedException();

	public Version? AppVersion => throw new NotImplementedException();

	public string? AppVersionString => throw new NotImplementedException();

	public string? GameName => throw new NotImplementedException();

	public string? GamePath => throw new NotImplementedException();

	public string Profile => throw new NotImplementedException();

	//public string StagePath => throw new NotImplementedException();

	public string GameIniFolderPath => throw new NotImplementedException();

	public List<string> StagePaths => throw new NotImplementedException();

	public string PluginsTxtPath => throw new NotImplementedException();

	public List<Module> Modules => throw new NotImplementedException();

	public Dictionary<string, Module> ModulesWinning => throw new NotImplementedException();

	public List<Archive> Archives => throw new NotImplementedException();

	public AvaloniaDictionary<string, Archive> ArchivesWinning => throw new NotImplementedException();

	public List<DLL> Dlls => throw new NotImplementedException();

	public Dictionary<string, DLL> DllsWinning => throw new NotImplementedException();

	//public bool ReadSettings() => throw new NotImplementedException();
	public void Refresh() => throw new NotImplementedException();
	public void GetOverviewInfo() => throw new NotImplementedException();
}
