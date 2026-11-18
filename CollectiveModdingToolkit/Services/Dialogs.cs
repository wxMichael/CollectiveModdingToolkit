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

using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using CollectiveModdingToolkit.Views;

namespace CollectiveModdingToolkit.Services;

public interface IClipboardService
{
	Task SetTextAsync(string text);
}

public interface IDialogService
{
	void ShowAbout(string title, string text);
	void ShowFileList(string title, string info, IReadOnlyList<string> lines);

	/// <summary>Asks for a material file to open. Null if cancelled.</summary>
	Task<string?> PickMaterialToOpenAsync(string? folder);

	/// <summary>Asks where to save a material. Null if cancelled.</summary>
	Task<string?> PickMaterialToSaveAsync(string suggestedName, string? folder);
}

/// <summary>Finds the app's main window without anything holding a static reference to it.</summary>
internal static class AppWindow
{
	public static Window? Main =>
		(Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}

public sealed class ClipboardService : IClipboardService
{
	public async Task SetTextAsync(string text)
	{
		if (AppWindow.Main?.Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
	}
}

public sealed class DialogService : IDialogService
{
	private static readonly FilePickerFileType MaterialFiles = new("Materials (BGSM, BGEM, JSON)")
	{
		Patterns = ["*.bgsm", "*.bgem", "*.json"],
	};

	public void ShowAbout(string title, string text)
	{
		AboutWindow.Open(AppWindow.Main, title, text);
	}

	public void ShowFileList(string title, string info, IReadOnlyList<string> lines)
	{
		FileListWindow.Open(AppWindow.Main, title, info, lines);
	}

	public async Task<string?> PickMaterialToOpenAsync(string? folder)
	{
		if (AppWindow.Main?.StorageProvider is not { CanOpen: true } storage) return null;

		var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = "Open Material",
			AllowMultiple = false,
			FileTypeFilter = [MaterialFiles],
			SuggestedStartLocation = await Folder(storage, folder),
		});
		return files.Count > 0 ? files[0].TryGetLocalPath() : null;
	}

	public async Task<string?> PickMaterialToSaveAsync(string suggestedName, string? folder)
	{
		if (AppWindow.Main?.StorageProvider is not { CanSave: true } storage) return null;

		var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
		{
			Title = "Save Material",
			SuggestedFileName = suggestedName,
			FileTypeChoices = [MaterialFiles],
			SuggestedStartLocation = await Folder(storage, folder),
		});
		return file?.TryGetLocalPath();
	}

	private static async Task<IStorageFolder?> Folder(IStorageProvider storage, string? path) =>
		path == null ? null : await storage.TryGetFolderFromPathAsync(path);
}
