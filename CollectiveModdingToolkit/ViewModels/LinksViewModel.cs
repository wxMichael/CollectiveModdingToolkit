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

using System.Threading.Tasks;
using CollectiveModdingToolkit.Services;
using CommunityToolkit.Mvvm.Input;

namespace CollectiveModdingToolkit.ViewModels;

/// <summary>Base for tabs made of buttons that open or copy a link.</summary>
public abstract partial class LinksViewModel(IShellService shell, IClipboardService clipboard) : ViewModelBase
{
	[RelayCommand]
	private void OpenUrl(string? url)
	{
		if (!string.IsNullOrEmpty(url)) shell.OpenUrl(url);
	}

	[RelayCommand]
	private async Task CopyUrl(string? url)
	{
		if (!string.IsNullOrEmpty(url)) await clipboard.SetTextAsync(url);
	}
}

public sealed class ToolsViewModel(IShellService shell, IClipboardService clipboard) : LinksViewModel(shell, clipboard)
{
	/// <summary>Design-time only.</summary>
	public ToolsViewModel() : this(new ShellService(), new ClipboardService())
	{
	}
}

public sealed class AboutViewModel(IShellService shell, IClipboardService clipboard) : LinksViewModel(shell, clipboard)
{
	/// <summary>Design-time only.</summary>
	public AboutViewModel() : this(new ShellService(), new ClipboardService())
	{
	}

	public static string AppVersionString => AppInfo.VersionString;
}
