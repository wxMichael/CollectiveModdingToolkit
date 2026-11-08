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
using Avalonia.Controls;
using CollectiveModdingToolkit.Services;

namespace CollectiveModdingToolkit.ViewModels;

/// <summary>Composes the window: owns the shared session and one view model per tab.</summary>
public sealed class MainWindowViewModel : ViewModelBase
{
	/// <summary>Also used by the XAML previewer.</summary>
	public MainWindowViewModel() : this(new ShellService(), new ClipboardService(), new DialogService())
	{
	}

	public MainWindowViewModel(IShellService shell, IClipboardService clipboard, IDialogService dialogs)
	{
		Session = new GameSession();
		Overview = new OverviewViewModel(Session, shell);
		F4SE = new F4SEViewModel(Session, shell);
		Archives = new ArchivesViewModel(Session, shell, dialogs);
		Scanner = new TabScannerViewModel(Session, shell, clipboard, dialogs);
		Tools = new ToolsViewModel(shell, clipboard);
		Settings = new SettingsViewModel(shell);
		About = new AboutViewModel(shell, clipboard);
	}

	public GameSession Session { get; }

	public OverviewViewModel Overview { get; }
	public F4SEViewModel F4SE { get; }
	public ArchivesViewModel Archives { get; }
	public TabScannerViewModel Scanner { get; }
	public ToolsViewModel Tools { get; }
	public SettingsViewModel Settings { get; }
	public AboutViewModel About { get; }

	public static ErrorReporter Errors => ErrorReporter.Current;

	public static string WindowTitle => AppInfo.WindowTitle;

	/// <summary>Loads the game and hardware info in the background. Call once the window is showing.</summary>
	public Task InitializeAsync() =>
		// The XAML previewer instantiates this via Design.DataContext; skip game scanning and hardware probing there.
		Design.IsDesignMode ? Task.CompletedTask : Session.LoadAsync();
}
