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
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Services;
using CommunityToolkit.Mvvm.Input;

namespace CollectiveModdingToolkit.ViewModels;

public partial class OverviewViewModel : GameViewModelBase
{
	private readonly IShellService _shell;

	/// <summary>Design-time only.</summary>
	public OverviewViewModel() : this(new GameSession(), new ShellService())
	{
	}

	public OverviewViewModel(GameSession session, IShellService shell) : base(session, shell)
	{
		_shell = shell;
		session.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(GameSession.CanRefresh)) RefreshCommand.NotifyCanExecuteChanged();
		};
	}

	private bool CanRefresh() => Session.CanRefresh;

	[RelayCommand(CanExecute = nameof(CanRefresh))]
	private Task Refresh() => Session.RefreshAsync();

	[RelayCommand]
	private static void RevealProblem(ProblemInfo? problem) => problem?.Reveal();

	[RelayCommand]
	private void OpenProblemUrl(ProblemInfo? problem)
	{
		if (problem?.Url is { } url) _shell.OpenUrl(url);
	}

	[RelayCommand]
	private void OpenGameFolder()
	{
		if (Game is { } game) _shell.OpenFolder(game.GamePath);
	}
}
