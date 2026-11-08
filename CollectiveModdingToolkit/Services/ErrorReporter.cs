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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectiveModdingToolkit.Services;

/// <summary>
///     Collects unrecoverable startup/scan errors from anywhere in the app and exposes them as bindable state, so
///     models and managers never touch the window. The main window binds to <see cref="IsOpen" /> and
///     <see cref="Message" />.
/// </summary>
public sealed partial class ErrorReporter : ObservableObject
{
	private readonly TaskCompletionSource _hostLoaded = new();
	private int _reported;

	public static ErrorReporter Current { get; } = new();

	[ObservableProperty] public partial bool IsOpen { get; private set; }
	[ObservableProperty] public partial string Message { get; private set; } = string.Empty;

	/// <summary>The dialog host can only show a dialog once it has loaded; errors raised earlier wait for this.</summary>
	public void MarkHostLoaded()
	{
		_hostLoaded.TrySetResult();
	}

	/// <summary>Reports a critical error. Only the first one is shown.</summary>
	public void Critical(Type? type = null, string? message = null)
	{
		if (Interlocked.Exchange(ref _reported, 1) == 1) return;

		var text = type switch
		{
			not null when type == typeof(DirectoryNotFoundException) => $"Directory not found:\n{message}",
			not null when type == typeof(FileNotFoundException) => $"File not found:\n{message}",
			not null when type == typeof(NotImplementedException) =>
				$"Handling for scenario not yet implemented.\n{message}",
			_ => message ?? "Unspecified Error",
		};

		_ = Dispatcher.UIThread.InvokeAsync(async () =>
		{
			await _hostLoaded.Task;
			Message = text;
			IsOpen = true;
		});
	}
}
