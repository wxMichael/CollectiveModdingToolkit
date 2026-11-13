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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CollectiveModdingToolkit.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectiveModdingToolkit.ViewModels;

/// <summary>
///     Update checking, shared by the Settings and Update tabs.
/// </summary>
/// <remarks>
///     Only successful checks count against the limits: automatic checks run at most once per
///     <see cref="AutomaticInterval" /> (persisted), manual checks at most once per <see cref="ManualInterval" />
///     (this session only).
/// </remarks>
public sealed partial class UpdateViewModel : ViewModelBase
{
	private static readonly TimeSpan AutomaticInterval = TimeSpan.FromHours(12);
	private static readonly TimeSpan ManualInterval = TimeSpan.FromMinutes(5);

	private readonly IShellService _shell;

	/// <summary>Measures time since the last successful manual check.</summary>
	private Stopwatch? _sinceManualCheck;

	public UpdateViewModel(IShellService shell)
	{
		_shell = shell;
		LatestVersion = Settings.LatestVersion;
	}

	/// <summary>Design-time only.</summary>
	public UpdateViewModel() : this(new ShellService())
	{
	}

	public static AppSettings Settings => AppSettings.Current;

	/// <summary>The newest version on Nexus Mods, if known.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsUpdateAvailable), nameof(LinkText))]
	public partial string? LatestVersion { get; private set; }

	public bool IsUpdateAvailable => UpdateChecker.IsNewer(LatestVersion);

	/// <summary>The latest version as major.minor, e.g. "1.2" for "1.2.0".</summary>
	private string DisplayVersion => string.Join('.', (LatestVersion ?? "").Split('.').Take(2));

	public string LinkText => $"Get v{DisplayVersion} on Nexus Mods";

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
	public partial bool IsChecking { get; private set; }

	/// <summary>Why the last check failed, or null.</summary>
	[ObservableProperty]
	public partial string? ErrorMessage { get; private set; }

	/// <summary>The outcome of the last manual check (up to date, or checked too recently), or null.</summary>
	[ObservableProperty]
	public partial string? StatusMessage { get; private set; }

	/// <summary>Runs the automatic check if enabled and none has succeeded recently. Never throws.</summary>
	public async Task CheckOnLaunchAsync()
	{
		if (!Settings.CheckForUpdates) return;

		var now = DateTimeOffset.UtcNow;
		// A last check "in the future" means the clock moved; don't let that block checks.
		if (Settings.LastUpdateCheck is { } last && last <= now && now - last < AutomaticInterval)
		{
			Log.Debug($"Skipping update check; last checked {last.ToLocalTime():g}.");
			return;
		}

		await CheckAsync();
	}

	private bool CanCheckNow() => !IsChecking;

	[RelayCommand(CanExecute = nameof(CanCheckNow))]
	private async Task CheckNow()
	{
		if (_sinceManualCheck is { } elapsed && elapsed.Elapsed < ManualInterval)
		{
			var wait = ManualInterval - elapsed.Elapsed;
			var minutes = (int)Math.Ceiling(wait.TotalMinutes);
			ErrorMessage = null;
			StatusMessage = $"Checked recently. Try again in {minutes} minute{(minutes == 1 ? "" : "s")}.";
			return;
		}

		if (await CheckAsync())
		{
			_sinceManualCheck = Stopwatch.StartNew();
			StatusMessage = IsUpdateAvailable
				? $"Version {DisplayVersion} is available."
				: "You have the latest version.";
		}
	}

	/// <summary>Queries Nexus Mods and records the result. Returns true on success. Never throws.</summary>
	private async Task<bool> CheckAsync()
	{
		if (IsChecking) return false;

		IsChecking = true;
		ErrorMessage = null;
		StatusMessage = null;
		try
		{
			var latest = await UpdateChecker.GetLatestVersionAsync();
			Log.Info($"Update check: latest version is {latest}, running {AppInfo.VersionString}.");
			LatestVersion = latest;
			Settings.LatestVersion = latest;
			Settings.LastUpdateCheck = DateTimeOffset.UtcNow;
			return true;
		}
		catch (UpdateCheckException e)
		{
			Log.Warning($"Update check failed: {e.Message} {e.InnerException?.Message}");
			ErrorMessage = e.Message;
		}
		catch (Exception e)
		{
			Log.Warning($"Update check failed unexpectedly: {e}");
			ErrorMessage = "The update check failed unexpectedly. See the log file for details.";
		}
		finally
		{
			IsChecking = false;
		}

		return false;
	}

	[RelayCommand]
	private void OpenNexusPage() => _shell.OpenUrl(UpdateChecker.NexusPageUrl);
}
