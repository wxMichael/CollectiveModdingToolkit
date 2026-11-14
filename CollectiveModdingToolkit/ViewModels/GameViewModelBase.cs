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

using CollectiveModdingToolkit.Managers;
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Services;
using CommunityToolkit.Mvvm.Input;

namespace CollectiveModdingToolkit.ViewModels;

/// <summary>Base for tabs that show the loaded game; forwards the session's state so views can bind to it directly.</summary>
public abstract partial class GameViewModelBase : ViewModelBase
{
	private readonly IShellService _shell;

	protected GameViewModelBase(GameSession session, IShellService shell)
	{
		Session = session;
		_shell = shell;
		// Session property names match the forwarders below, so a refresh updates every bound view.
		session.PropertyChanged += (_, e) =>
		{
			OnPropertyChanged(e.PropertyName);
			if (e.PropertyName == nameof(GameSession.Game)) OnPropertyChanged(nameof(IsLoading));
		};
	}

	protected GameSession Session { get; }

	public Computer? Computer => Session.Computer;
	public IModManager? ModManager => Session.ModManager;
	public Game? Game => Session.Game;

	/// <summary>True until the first game load finishes. Tabs show a loading overlay meanwhile.</summary>
	public bool IsLoading => Session.Game is null;

	[RelayCommand]
	private void RevealFile(object? item) => _shell.RevealFile(item);
}
