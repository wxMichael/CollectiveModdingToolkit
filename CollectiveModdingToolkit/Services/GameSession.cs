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
using System.Threading;
using System.Threading.Tasks;
using CollectiveModdingToolkit.Managers;
using CollectiveModdingToolkit.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectiveModdingToolkit.Services;

/// <summary>
///     Owns the detected PC, mod manager, and loaded <see cref="Models.Game" />, and shares them with the tab view
///     models. Raises <c>PropertyChanged</c> when a refresh replaces the game.
/// </summary>
public sealed partial class GameSession : ObservableObject
{
	private static readonly Lock ComputerLock = new();
	private static Task<Computer>? _sharedComputer;

	[ObservableProperty] public partial Computer? Computer { get; private set; }
	[ObservableProperty] public partial IModManager? ModManager { get; private set; }
	[ObservableProperty] public partial Game? Game { get; private set; }
	[ObservableProperty] public partial bool CanRefresh { get; private set; }

	/// <summary>
	///     The game being modded. Chosen by the user at start-up and changeable later through <see cref="SwitchGameAsync" />.
	/// </summary>
	[ObservableProperty]
	public partial string GameName { get; private set; } = "Fallout 4";
	// TODO: Actually support other games.

	public async Task LoadAsync()
	{
		var computerTask = LoadComputerAsync();
		try
		{
			var (modManager, game) = await Task.Run(() =>
			{
				var manager = ModManagerDetector.Detect();
				return (manager, new Game(manager, GameName));
			});
			ModManager = modManager;
			Game = game;
			CanRefresh = true;
		}
		catch (Exception e)
		{
			ErrorReporter.Current.Critical(e.GetType(), e.ToString());
		}

		await computerTask;
	}

	private static Task<Computer> GetSharedComputer()
	{
		lock (ComputerLock)
		{
			if (_sharedComputer is null || _sharedComputer.IsFaulted || _sharedComputer.IsCanceled)
				_sharedComputer = Task.Run(() => new Computer());
			return _sharedComputer;
		}
	}

	private async Task LoadComputerAsync()
	{
		try
		{
			Computer = await GetSharedComputer();
		}
		catch (Exception e)
		{
			Log.Error("Hardware detection failed", e);
			ErrorReporter.Current.Critical(e.GetType(), e.ToString());
		}
	}

	/// <summary>Reloads the current game (and mod manager state) off the UI thread, then swaps it in.</summary>
	public Task RefreshAsync() => ReloadAsync(GameName);

	/// <summary>Switches to another game and loads it. Does nothing while a load is already running.</summary>
	public Task SwitchGameAsync(string gameName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
		return ReloadAsync(gameName);
	}

	private async Task ReloadAsync(string gameName)
	{
		if (!CanRefresh) return;

		CanRefresh = false;
		var computerTask = Computer is null ? LoadComputerAsync() : Task.CompletedTask;
		try
		{
			// A fresh manager per load. The previous one may still be in use by a running scan or the old Game.
			var (modManager, game) = await Task.Run(() =>
			{
				var manager = ModManagerDetector.Detect();
				return (manager, new Game(manager, gameName));
			});
			GameName = gameName;
			ModManager = modManager;
			Game = game;
			CanRefresh = true;
		}
		catch (Exception e)
		{
			Log.Error("Refresh failed", e);
			ErrorReporter.Current.Critical(e.GetType(), e.ToString());
		}

		await computerTask;
	}
}
