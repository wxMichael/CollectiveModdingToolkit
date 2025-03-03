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

using System.Text;

namespace CollectiveModdingToolkit.Models;

internal static class Magic
{
	private static readonly Encoding _codePage = Encoding.ASCII;

	internal static readonly byte[] TES4 = _codePage.GetBytes("TES4");
	internal static readonly byte[] BTDX = _codePage.GetBytes("BTDX");
	internal static readonly byte[] GNRL = _codePage.GetBytes("GNRL");
	internal static readonly byte[] DX10 = _codePage.GetBytes("DX10");
	internal static readonly byte[] HEDR = _codePage.GetBytes("HEDR");
	internal static readonly byte[] DDS = _codePage.GetBytes("DDS ");

	internal static readonly int Light = 0x0200;
}

internal static class GameVersion
{
	internal static string DownGrade = "Down-Grade";
	internal static string OldGen = "Old-Gen";
	internal static string NextGen = "Next-Gen";
	internal static string Unknown = "Unknown";
	internal static string NotFound = "Not Found";
}
