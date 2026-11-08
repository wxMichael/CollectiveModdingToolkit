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

using System.Text;

namespace CollectiveModdingToolkit.Models;

internal static class Magic
{
	internal const int Light = 0x0200;
	private static readonly Encoding CodePage = Encoding.ASCII;

	internal static readonly byte[] TES4 = CodePage.GetBytes("TES4");
	internal static readonly byte[] BTDX = CodePage.GetBytes("BTDX");
	internal static readonly byte[] GNRL = CodePage.GetBytes("GNRL");
	internal static readonly byte[] DX10 = CodePage.GetBytes("DX10");
	internal static readonly byte[] HEDR = CodePage.GetBytes("HEDR");
	internal static readonly byte[] DDS = CodePage.GetBytes("DDS ");
}

internal static class GameVersion
{
	internal const string DownGrade = "Down-Grade";
	internal const string OldGen = "Old-Gen";
	internal const string NextGen = "Next-Gen";
	internal const string Anniversary = "Anniversary";

	/// <summary>A file that is identical in Next-Gen and Anniversary installs.</summary>
	internal const string NextGenAnniversary = "Next-Gen & Anniversary";

	/// <summary>A known, but outdated, version.</summary>
	internal const string Obsolete = "Obsolete";

	internal const string Unknown = "Unknown";
	internal const string NotFound = "Not Found";
}
