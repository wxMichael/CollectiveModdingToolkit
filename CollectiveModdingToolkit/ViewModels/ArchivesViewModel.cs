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
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Models.FileTypes;
using CollectiveModdingToolkit.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectiveModdingToolkit.ViewModels;

public partial class ArchivesViewModel : GameViewModelBase
{
	private const string AboutTitle = "About Archives";

	private const string AboutText = """
	                                 There are 2 formats and 3 versions for Fallout 4 BA2 files:

	                                 • General (GNRL)
	                                 • Textures (DX10)

	                                 • v1: Supported by all FO4 game versions. Required by v1.10.163 and earlier.
	                                 • v7/8: Only supported by FO4 v1.10.980 and later.

	                                 It's suspected there are format differences between versions for XBox/PS, but for PC the only difference is the number itself.
	                                 v7/8 are identical so this tool only patches to v1 & v8.

	                                 Why Patch Versions?

	                                 Patching is ONLY needed if you use older apps that require it.
	                                 Apps may check the version to ensure compatibility but v7/8 didn't exist when most apps were made, so they assume it's a different format and show errors.
	                                 Because they're actually identical, you can just patch the version number in the file header and such apps will read them.
	                                 """;

	private readonly IDialogService _dialogs;

	private Game? _watchedGame;

	/// <summary>Design-time only.</summary>
	public ArchivesViewModel() : this(new GameSession(), new ShellService(), new DialogService())
	{
	}

	public ArchivesViewModel(GameSession session, IShellService shell, IDialogService dialogs) : base(session, shell)
	{
		_dialogs = dialogs;
		session.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(GameSession.Game)) WatchGame();
		};
		WatchGame();
	}

	public ObservableCollection<Archive> DisplayedArchives { get; } = [];

	[ObservableProperty] public partial string ArchiveFilter { get; set; } = string.Empty;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(PatchArchivesCommand))]
	public partial bool IsNotProcessing { get; set; } = true;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsDesiredV1), nameof(IsDesiredV8))]
	public partial byte DesiredArchiveVersion { get; set; } = 1;

	// One bool per radio button; unchecking the other button arrives as false and is ignored.
	public bool IsDesiredV1
	{
		get => DesiredArchiveVersion == 1;
		set
		{
			if (value) DesiredArchiveVersion = 1;
		}
	}

	public bool IsDesiredV8
	{
		get => DesiredArchiveVersion == 8;
		set
		{
			if (value) DesiredArchiveVersion = 8;
		}
	}

	partial void OnArchiveFilterChanged(string value)
	{
		UpdateDisplayedArchives();
	}

	/// <summary>Follows the current game's archive list, which is replaced wholesale on refresh.</summary>
	private void WatchGame()
	{
		if (_watchedGame != null) _watchedGame.ArchivesWinning.CollectionChanged -= HandleArchivesChanged;

		_watchedGame = Session.Game;
		if (_watchedGame != null) _watchedGame.ArchivesWinning.CollectionChanged += HandleArchivesChanged;

		UpdateDisplayedArchives();
	}

	private void HandleArchivesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		UpdateDisplayedArchives();
	}

	/// <summary>
	///     Brings <see cref="DisplayedArchives" /> in line with the game and filter by removing, inserting and moving
	///     only what differs, so the grid keeps its scroll position instead of resetting on every change.
	/// </summary>
	private void UpdateDisplayedArchives()
	{
		List<Archive> wanted = [];
		if (Session.Game is { } game)
			wanted.AddRange(game.ArchivesWinning.Values.Where(archive =>
				string.IsNullOrWhiteSpace(ArchiveFilter) ||
				archive.Name.Contains(ArchiveFilter, StringComparison.OrdinalIgnoreCase)));

		HashSet<Archive> keep = [.. wanted];
		for (var i = DisplayedArchives.Count - 1; i >= 0; i--)
			if (!keep.Contains(DisplayedArchives[i]))
				DisplayedArchives.RemoveAt(i);

		for (var i = 0; i < wanted.Count; i++)
		{
			if (i < DisplayedArchives.Count && ReferenceEquals(DisplayedArchives[i], wanted[i])) continue;

			var existing = DisplayedArchives.IndexOf(wanted[i]);
			if (existing >= 0)
				DisplayedArchives.Move(existing, i);
			else
				DisplayedArchives.Insert(i, wanted[i]);
		}
	}

	[RelayCommand]
	private void ShowAbout()
	{
		_dialogs.ShowAbout(AboutTitle, AboutText);
	}

	[RelayCommand(CanExecute = nameof(IsNotProcessing))]
	private async Task PatchArchives()
	{
		IsNotProcessing = false;
		// Snapshot on the UI thread; the filter or a refresh may change the list while patching runs.
		var archives = DisplayedArchives.ToList();
		var version = DesiredArchiveVersion;
		try
		{
			await Task.Run(() =>
			{
				foreach (var archive in archives) archive.SetVersion(version);
			});
		}
		finally
		{
			IsNotProcessing = true;
		}
	}
}
