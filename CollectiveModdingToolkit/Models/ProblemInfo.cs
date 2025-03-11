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

using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectiveModdingToolkit.Models;

public enum PathType
{
	File,
	Directory,
}

public class ProblemInfo : ObservableObject
{
	public string FileName { get; private set; }
	public string FullPath { get; private set; }
	public PathType PathType { get; private set; }
	public string Problem { get; private set; }
	public string? Mod { get; private set; }
	public string? Solution { get; private set; }
	public string[]? FileList { get; private set; } = null;

	public ProblemInfo(string fileName, string fullPath, PathType pathType, string problem, string? mod = null, string? solution = null, string[]? fileList = null)
	{
		FileName = fileName;
		FullPath = fullPath;
		PathType = pathType;
		Problem = problem;
		Mod = mod;
		Solution = solution;
		FileList = fileList;
	}
}
