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

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;

namespace CollectiveModdingToolkit;

internal static class Utilities
{
	internal static string GetFileHash(string filePath, bool skipBA2Header = false) => $"{Crc32.HashToUInt32(File.ReadAllBytes(filePath).AsSpan(skipBA2Header ? 12 : 0)):X8}";

	internal static string? GetFileVersion(string filePath, bool skipPrivate = false)
	{
		FileVersionInfo version = FileVersionInfo.GetVersionInfo(filePath);
		string privatePart = skipPrivate ? string.Empty : $".{version.FilePrivatePart}";
		return version.FileVersion == null
			? null
			: $"{version.FileMajorPart}.{version.FileMinorPart}.{version.FileBuildPart}{privatePart}";
	}

	internal static string GetFileVersionOrHash(string filePath, bool hashOnly = false) => hashOnly ? GetFileHash(filePath) : GetFileVersion(filePath) ?? GetFileHash(filePath);

	internal static void OpenURL(string url) => Process.Start(new ProcessStartInfo()
	{
		FileName = "explorer.exe",
		ArgumentList = { url },
		UseShellExecute = true,
	});


}
