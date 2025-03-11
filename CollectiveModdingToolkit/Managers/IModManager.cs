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
using System;
using System.Collections.Generic;

namespace CollectiveModdingToolkit.Managers;

public interface IModManager
{
	string AppName { get; }
	string? AppPath { get; }
	Version? AppVersion { get; }
	string? AppVersionString { get; }
	string? GameName { get; }
	string? GamePath { get; }
	string? Profile { get; }
	//string? StagePath { get; }
	string PluginsTxtPath { get; }
	//string? ModListTxtPath { get; }
	string GameIniFolderPath { get; }
	List<string> StagePaths { get; }
	List<Module> Modules { get; }
	Dictionary<string, Module> ModulesWinning { get; }
	List<Archive> Archives { get; }
	AvaloniaDictionary<string, Archive> ArchivesWinning { get; }
	List<DLL> Dlls { get; }
	Dictionary<string, DLL> DllsWinning { get; }

	//bool ReadSettings();

	void Refresh();
	void GetOverviewInfo();
}
