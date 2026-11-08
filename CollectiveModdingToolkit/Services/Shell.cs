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

using System.Diagnostics;
using System.IO;

namespace CollectiveModdingToolkit.Services;

/// <summary>Opening things in the user's shell (browser, Explorer), so view models don't call process APIs.</summary>
public interface IShellService
{
	void OpenUrl(string url);
	bool Reveal(string? path);
	bool RevealFile(object? item);
	void OpenFolder(string path);
}

public sealed class ShellService : IShellService
{
	public void OpenUrl(string url) => Utilities.OpenURL(url);

	public bool Reveal(string? path) => Utilities.RevealInExplorer(path);

	public bool RevealFile(object? item) => Utilities.RevealFile(item);

	public void OpenFolder(string path)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
			UseShellExecute = true,
			Verb = "open",
		});
	}
}
