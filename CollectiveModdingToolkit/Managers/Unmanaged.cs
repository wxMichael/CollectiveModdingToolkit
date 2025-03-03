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

using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Models.FileTypes;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;

namespace CollectiveModdingToolkit.Managers;

internal class Unmanaged : ObservableObject, IModManager
{
	public string AppName { get; } = "Not Found";

	public string? AppPath { get; } = null;

	public Version? AppVersion { get; } = null;

	public string? AppVersionString { get; } = null;

	public string? GameName { get; } = null;

	public string? GamePath { get; } = null;

	public string? Profile { get; } = null;

	//public string? StagePath { get; } = null;

	public string PluginsTxtPath { get; } = Path.Combine(Computer.AppDataLocal, "Fallout4", "plugins.txt");

	public string GameIniFolderPath { get; } = Computer.DocumentsFallout4;

	public List<string> StagePaths { get; } = [];

	public List<Module> Modules { get; } = [];

	public Dictionary<string, Module> ModulesWinning { get; } = [];

	public List<Archive> Archives { get; } = [];

	public Dictionary<string, Archive> ArchivesWinning { get; } = [];

	public List<DLL> Dlls { get; } = [];

	public Dictionary<string, DLL> DllsWinning { get; } = [];

	//public bool ReadSettings() => throw new NotImplementedException();

	public void Refresh()
	{
		StagePaths.Clear();
		Modules.Clear();
		ModulesWinning.Clear();
		Archives.Clear();
		ArchivesWinning.Clear();
		Dlls.Clear();
		DllsWinning.Clear();
	}
}
