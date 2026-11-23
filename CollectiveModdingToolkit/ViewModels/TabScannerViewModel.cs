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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CollectiveModdingToolkit.Controls;
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectiveModdingToolkit.ViewModels;

/// <summary>An entry in the left list: a mod or problem type with results count.</summary>
public sealed record ScanGroup(string Name, int Count)
{
	public string Label => $"{Name} ({Count})";
}

/// <summary>
///     A row in the issues list. The second column is the problem when grouped by mod, and the mod when grouped by
///     problem.
/// </summary>
public sealed record ScanRow(ProblemInfo Problem, string Secondary)
{
	public string Title => Problem.ListTitle ?? Problem.DisplayName;

	public StatusLevel Level => Problem.IsInfo ? StatusLevel.None : Problem.Level;
}

public partial class TabScannerViewModel : ViewModelBase
{
	private readonly IClipboardService _clipboard;
	private readonly IDialogService _dialogs;
	private readonly Action<string>? _editMaterial;
	private readonly GameSession _session;
	private readonly IShellService _shell;
	private List<ProblemInfo> _problems = [];
	private CancellationTokenSource? _scanCts;

	/// <summary>Design-time only.</summary>
	public TabScannerViewModel() : this(new GameSession(), new ShellService(), new ClipboardService(),
		new DialogService())
	{
	}

	/// <param name="editMaterial">Opens a material file in the editor. Null hides the Edit button's effect.</param>
	public TabScannerViewModel(GameSession session, IShellService shell, IClipboardService clipboard,
		IDialogService dialogs, Action<string>? editMaterial = null)
	{
		_editMaterial = editMaterial;
		_session = session;
		_shell = shell;
		_clipboard = clipboard;
		_dialogs = dialogs;
		// A refresh swaps the game; re-evaluate whether a scan can start.
		_session.PropertyChanged += (_, _) => StartScanCommand.NotifyCanExecuteChanged();
		// Scan needs at least one option, so keep the button in step with the checkboxes.
		Settings.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName?.StartsWith("Scan", StringComparison.Ordinal) == true)
				StartScanCommand.NotifyCanExecuteChanged();
		};
	}

	public static AppSettings Settings => AppSettings.Current;

	public ObservableCollection<ScanGroup> Groups { get; } = [];
	public ObservableCollection<ScanRow> Issues { get; } = [];

	[ObservableProperty] public partial ScanGroup? SelectedGroup { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CurrentIssue), nameof(CanEditCurrent))]
	[NotifyCanExecuteChangedFor(nameof(EditMaterialCommand))]
	public partial ScanRow? SelectedRow { get; set; }

	/// <summary>Whether the selected issue is a material the editor can open.</summary>
	public bool CanEditCurrent => CanEditMaterial();

	public ProblemInfo? CurrentIssue => SelectedRow?.Problem;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(GroupByProblem), nameof(GroupHeading), nameof(SecondaryHeading))]
	public partial bool GroupByMod { get; set; } = false;

	[ObservableProperty] public partial string ResultsInfo { get; set; } = "Press Scan to look for problems.";
	[ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
	[ObservableProperty] public partial double Progress { get; set; }

	public string GroupHeading => GroupByMod ? "Mods" : "Problems";

	public string SecondaryHeading => GroupByMod ? "Problem" : "Mod";

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
	public partial bool IsNotProcessing { get; private set; } = true;

	public bool GroupByProblem
	{
		get => !GroupByMod;
		set => GroupByMod = !value;
	}

	private bool CanStartScan() =>
		IsNotProcessing && ScanOptions.FromSettings(Settings).Any &&
		_session.Game != null;

	partial void OnSelectedGroupChanged(ScanGroup? value)
	{
		SelectedRow = null;
		Issues.Clear();
		if (value == null) return;

		foreach (var p in _problems
			         .Where(p => GroupKey(p) == value.Name)
			         .OrderBy(p => p.RelativePath, StringComparer.OrdinalIgnoreCase))
			Issues.Add(new ScanRow(p, GroupByMod ? p.Problem : ModName(p)));
	}

	partial void OnGroupByModChanged(bool value) => BuildGroups();

	private static string ModName(ProblemInfo p) =>
		p.Mod ?? (p.HasPath && p.Type != ProblemType.FileNotFound ? "<Unmanaged>" : "General");

	private string GroupKey(ProblemInfo p) => GroupByMod ? ModName(p) : p.Problem;

	private void BuildGroups()
	{
		var previous = SelectedGroup?.Name;
		SelectedGroup = null;
		Groups.Clear();
		foreach (var group in _problems.GroupBy(GroupKey)
			         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
			Groups.Add(new ScanGroup(group.Key, group.Count()));

		SelectedGroup = Groups.FirstOrDefault(g => g.Name == previous) ?? Groups.FirstOrDefault();
	}

	/// <summary>The text Copy Details puts on the clipboard.</summary>
	public string? GetDetails()
	{
		if (CurrentIssue is not { } p) return null;

		StringBuilder text = new();
		text.AppendLine($"File: {p.DisplayName}");
		text.AppendLine($"Mod: {ModName(p)}");
		if (p.HasPath) text.AppendLine($"Location: {p.RelativePath}");

		text.AppendLine($"Summary: {RichText.Strip(p.Summary)}");
		text.AppendLine($"Solution: {(p.Solution is null ? "No solution suggestion." : RichText.Strip(p.Solution))}");
		return text.ToString();
	}

	[RelayCommand]
	private void RevealIssue() => CurrentIssue?.Reveal();

	private bool CanEditMaterial() => _editMaterial != null && CurrentIssue is { IsMaterialFile: true };

	[RelayCommand(CanExecute = nameof(CanEditMaterial))]
	private void EditMaterial()
	{
		if (CurrentIssue is { IsMaterialFile: true } issue) _editMaterial?.Invoke(issue.MaterialKey);
	}

	[RelayCommand]
	private void OpenIssueUrl()
	{
		if (CurrentIssue?.Url is { } url) _shell.OpenUrl(url);
	}

	[RelayCommand]
	private async Task CopyIssueUrl()
	{
		if (CurrentIssue?.Url is { } url) await _clipboard.SetTextAsync(url);
	}

	[RelayCommand]
	private async Task CopyDetails()
	{
		if (GetDetails() is { } details) await _clipboard.SetTextAsync(details);
	}

	[RelayCommand]
	private void ShowFileList()
	{
		if (CurrentIssue is not { FileList: { } files } issue) return;

		var raceSubgraphs = issue.Type == ProblemType.RaceSubgraphs;
		_dialogs.ShowFileList(raceSubgraphs ? "Race Animation Subgraph Records" : "Files",
			raceSubgraphs ? Scanner.InfoRaceSubgraphs : string.Empty, files.ToList());
	}

	[RelayCommand]
	private void CancelScan() => _scanCts?.Cancel();

	[RelayCommand(CanExecute = nameof(CanStartScan))]
	private async Task StartScan()
	{
		var game = _session.Game;
		if (game == null) return;

		var options = ScanOptions.FromSettings(Settings);
		_scanCts = new CancellationTokenSource();
		var token = _scanCts.Token;
		IsNotProcessing = false;
		_problems = [];
		BuildGroups();
		ResultsInfo = string.Empty;
		Progress = 1;
		StatusText = options.NeedsFileWalk ? "Building mod file index..." : "Scanning...";

		Progress<Scanner.Progress> progress = new(p =>
		{
			StatusText = p.Text;
			Progress = p.Percent;
		});

		try
		{
			_problems = await Task.Run(() => Scanner.Scan(game, options, progress, token), token);
		}
		catch (OperationCanceledException)
		{
			_problems = [];
		}
		catch (Exception e)
		{
			Log.Error("Scanner: unexpected failure", e);
			_problems =
			[
				new ProblemInfo(ProblemType.ScanError, Severity.Critical, $"{e.GetType().Name}: {e.Message}",
					solution: "Please report this error.") { Title = "Scanner" },
			];
		}

		var cancelled = token.IsCancellationRequested;
		_scanCts.Dispose();
		_scanCts = null;

		BuildGroups();
		ResultsInfo = cancelled ? "Scan cancelled."
			: _problems.Count == 0 ? "No problems found." : $"{_problems.Count} results";
		StatusText = string.Empty;
		Progress = 100;
		IsNotProcessing = true;
	}
}
