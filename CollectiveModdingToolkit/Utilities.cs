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

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;
using CollectiveModdingToolkit.Models.FileTypes;

namespace CollectiveModdingToolkit;

internal static class Utilities
{
	internal static string GetFileHash(string filePath, bool skipBA2Header = false) =>
		$"{Crc32.HashToUInt32(File.ReadAllBytes(filePath).AsSpan(skipBA2Header ? 12 : 0)):X8}";

	internal static string? GetFileVersion(string filePath, bool skipPrivate = false)
	{
		var version = FileVersionInfo.GetVersionInfo(filePath);
		var privatePart = skipPrivate ? string.Empty : $".{version.FilePrivatePart}";
		return version.FileVersion == null
			? null
			: $"{version.FileMajorPart}.{version.FileMinorPart}.{version.FileBuildPart}{privatePart}";
	}

	internal static void OpenURL(string url)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = "explorer.exe",
			ArgumentList = { url },
			UseShellExecute = true,
		});
	}

	/// <summary>
	///     Opens Explorer with <paramref name="path" /> selected. Directories are opened directly.
	///     If the path no longer exists, the nearest existing parent folder is opened instead.
	/// </summary>
	/// <returns>False if nothing could be opened.</returns>
	internal static bool RevealInExplorer(string? path)
	{
		if (string.IsNullOrWhiteSpace(path)) return false;

		// Explorer ignores /select with forward slashes.
		string full;
		try
		{
			full = Path.GetFullPath(path.Replace('/', '\\'));
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (NotSupportedException)
		{
			return false;
		}

		// Arguments must be one string.
		// ArgumentList would wrap the whole "/select,<path>" in quotes, which Explorer rejects.
		string arguments;
		if (File.Exists(full))
		{
			arguments = $"/select,\"{full}\"";
		}
		else if (Directory.Exists(full))
		{
			arguments = $"\"{full}\"";
		}
		else
		{
			var parent = Path.GetDirectoryName(full);
			while (parent != null && !Directory.Exists(parent)) parent = Path.GetDirectoryName(parent);

			if (parent == null) return false;

			arguments = $"\"{parent}\"";
		}

		try
		{
			Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = false });
			return true;
		}
		catch (Win32Exception)
		{
			return false;
		}
	}

	/// <summary>
	///     Shows a file in Explorer, using its real on-disk location (the staged mod file, not the virtual or linked
	///     game-folder path).
	/// </summary>
	internal static bool RevealFile(object? item) =>
		item is IFile file && RevealInExplorer(file.Origin?.SourcePath ?? file.PathFull);

	/// <summary>
	///     Resolves <paramref name="path" /> (absolute, or relative to the game folder) and returns it relative to the game
	///     folder.
	///     Fails if the path is outside the game folder.
	/// </summary>
	internal static bool TryGetGameRelativePath(string gamePath, string path, out string fullPath,
		out string relativePath)
	{
		fullPath = string.Empty;
		relativePath = string.Empty;
		try
		{
			path = path.Replace('/', '\\');
			fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(gamePath, path));
			relativePath = Path.GetRelativePath(gamePath, fullPath);
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (NotSupportedException)
		{
			return false;
		}

		return relativePath != "."
		       && relativePath != ".."
		       && !relativePath.StartsWith(@"..\", StringComparison.Ordinal)
		       && !Path.IsPathRooted(relativePath);
	}
}
