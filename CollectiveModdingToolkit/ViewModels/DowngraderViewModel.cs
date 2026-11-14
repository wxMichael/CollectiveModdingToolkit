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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CollectiveModdingToolkit.Delta;
using CollectiveModdingToolkit.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Engine = CollectiveModdingToolkit.Delta.Downgrader;

namespace CollectiveModdingToolkit.ViewModels;

public sealed record FileRow(string Name, string FullPath, string EditionText, Edition Edition)
{
	public bool IsOldGen => Edition == Edition.OldGen;
	public bool IsNextGen => Edition == Edition.NextGen;
	public bool IsAnniversary => Edition == Edition.Anniversary;
	public bool IsMissing => Edition == Edition.NotFound;
	public bool IsBad => Edition is Edition.Unknown or Edition.Obsolete or Edition.NextGenAnniversary;
}

public sealed record LogLine(string Text, MessageKind Kind)
{
	public bool IsGood => Kind == MessageKind.Good;
	public bool IsBad => Kind == MessageKind.Bad;
}

public sealed partial class DowngraderViewModel : GameViewModelBase
{
	private const string AboutTitle = "About Downgrading Fallout 4 & Creation Kit";

	private const string AboutText = """
	                                 This downgrader uses delta patches, which are downloaded as-needed from the Collective Modding Toolkit GitHub page. A patch is far smaller than the file it replaces.

	                                 Backups are created prior to patching, and will be used instead of patches if present.
	                                 Simple Downgrader's backups will also be used.
	                                 Backup naming:
	                                 Fallout4_downgradeBackup.exe
	                                 Fallout4_upgradeBackup.exe
	                                 Fallout4_anniversaryBackup.exe

	                                 Your installed files are only replaced once the new version has been built and checked, so a failed download or patch leaves the game as it was.

	                                 Both Creation Kit and the game require steam_api64.dll to match their version, so they must be patched together (for now).
	                                 """;

	private readonly IDialogService _dialogs;
	private readonly IPatchSource _source;

	private CancellationTokenSource? _cancel;
	private int _inspectVersion;

	/// <summary>Design-time only.</summary>
	public DowngraderViewModel() : this(new GameSession(), new ShellService(), new DialogService())
	{
	}

	public DowngraderViewModel(GameSession session, IShellService shell, IDialogService dialogs,
		IPatchSource? source = null) : base(session, shell)
	{
		_dialogs = dialogs;
		_source = source ?? new HttpPatchSource();
		session.PropertyChanged += (sender, e) =>
		{
			if (e.PropertyName != nameof(GameSession.Game)) return;

			PatchAllCommand.NotifyCanExecuteChanged();
			_ = RefreshFilesAsync();
		};
		Messages.Add(new LogLine("Patches will be downloaded and applied as-needed.", MessageKind.Info));
		_ = RefreshFilesAsync();
	}

	public ObservableCollection<FileRow> GameRows { get; } = [];
	public ObservableCollection<FileRow> CreationKitRows { get; } = [];
	public ObservableCollection<LogLine> Messages { get; } = [];

	public static AppSettings Settings => AppSettings.Current;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(PatchAllCommand), nameof(CancelCommand))]
	public partial bool IsBusy { get; private set; }

	/// <summary>Overall progress of the running job, 0 to 100.</summary>
	[ObservableProperty]
	public partial double Progress { get; private set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsDesiredOldGen), nameof(IsDesiredNextGen), nameof(IsDesiredAnniversary))]
	// ReSharper disable once MemberCanBeMadeStatic.Local
	private partial Edition DesiredEdition { get; set; } = Edition.NextGen;

	// One bool per radio button; unchecking the other button arrives as false and is ignored.
	public bool IsDesiredOldGen
	{
		get => DesiredEdition == Edition.OldGen;
		set
		{
			if (value) DesiredEdition = Edition.OldGen;
		}
	}

	public bool IsDesiredNextGen
	{
		get => DesiredEdition == Edition.NextGen;
		set
		{
			if (value) DesiredEdition = Edition.NextGen;
		}
	}

	public bool IsDesiredAnniversary
	{
		get => DesiredEdition == Edition.Anniversary;
		set
		{
			if (value) DesiredEdition = Edition.Anniversary;
		}
	}

	private bool CanPatch => !IsBusy && Session.Game != null;

	private static string PatchFolder => Path.Combine(AppPaths.DataFolder, "patches");

	private async Task RefreshFilesAsync()
	{
		var version = Interlocked.Increment(ref _inspectVersion);
		var gamePath = Session.Game?.GamePath;
		if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
		{
			GameRows.Clear();
			CreationKitRows.Clear();
			return;
		}

		IReadOnlyList<FileState> states;
		try
		{
			states = await Task.Run(() => new Engine(gamePath, _source, PatchFolder).Inspect());
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			Log.Error("Downgrader could not read the game files", e);
			return;
		}

		if (version != _inspectVersion) return;

		// Next-Gen and Anniversary share steam_api64.dll, so the executable says which one it is.
		var exe = states.FirstOrDefault(s => s.File.Name.Equals("Fallout4.exe", StringComparison.OrdinalIgnoreCase))
			?.Edition;
		var shared = exe is Edition.NextGen or Edition.Anniversary ? exe.Value : Edition.NextGenAnniversary;

		GameRows.Clear();
		CreationKitRows.Clear();
		foreach (var state in states)
		{
			var edition = state.Edition == Edition.NextGenAnniversary ? shared : state.Edition;
			FileRow row = new(state.File.Name, Path.Combine(gamePath, state.File.RelativePath),
				Engine.Describe(edition), edition);
			(state.File.IsCreationKit ? CreationKitRows : GameRows).Add(row);
		}

		if (!IsBusy && exe is Edition.OldGen or Edition.NextGen or Edition.Anniversary) DesiredEdition = exe.Value;
	}

	[RelayCommand]
	private void ShowAbout() => _dialogs.ShowAbout(AboutTitle, AboutText);

	[RelayCommand(CanExecute = nameof(CanPatch))]
	private async Task PatchAll()
	{
		if (Session.Game is not { } game) return;

		IsBusy = true;
		Progress = 0;
		Messages.Clear();
		DowngradeResult? result = null;

		using CancellationTokenSource cancel = new();
		_cancel = cancel;
		// Created here so reports are marshalled back to the UI thread.
		Progress<DowngradeMessage> log = new(m => AddMessage(m.Text, m.Kind));
		Progress<double> progress = new(p => Progress = p);
		var desired = DesiredEdition;
		var options = new DowngradeOptions(Settings.DowngraderKeepBackups, Settings.DowngraderDeleteDeltas);
		try
		{
			Log.Info($"Downgrader: switching to {Engine.Describe(desired)} in {game.GamePath}");
			var engine = new Engine(game.GamePath, _source, PatchFolder);
			result = await Task.Run(() => engine.RunAsync(desired, options, log, progress, cancel.Token));
			AddMessage(
				$"Done: {result.Patched} patched, {result.Failed} failed, {result.Skipped} skipped.",
				result.Failed > 0 ? MessageKind.Bad : result.Patched > 0 ? MessageKind.Good : MessageKind.Info);
		}
		catch (OperationCanceledException)
		{
			AddMessage("Cancelled. Files that were already patched stay patched.", MessageKind.Info);
		}
		catch (Exception e)
		{
			Log.Error("Downgrader failed", e);
			AddMessage($"Failed: {e.Message}", MessageKind.Bad);
		}
		finally
		{
			_cancel = null;
			IsBusy = false;
		}

		await RefreshFilesAsync();
		// The Overview, F4SE and Scanner results depend on the game version.
		if (result is not { Patched: 0 }) await Session.RefreshAsync();
	}

	[RelayCommand(CanExecute = nameof(IsBusy))]
	private void Cancel() => _cancel?.Cancel();

	/// <summary>Called when the user tries to close the window mid-job.</summary>
	internal void ExplainBusy() =>
		AddMessage("Patching is in progress. Wait for it to finish, or press Cancel, before closing.",
			MessageKind.Bad);

	private void AddMessage(string text, MessageKind kind)
	{
		Messages.Add(new LogLine(text, kind));
		if (kind == MessageKind.Bad) Log.Warning($"Downgrader: {text}");
		else Log.Info($"Downgrader: {text}");
	}
}
