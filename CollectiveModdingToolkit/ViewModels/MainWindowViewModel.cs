/*
 * 
 * Collective Modding Toolkit
 * Copyright (C) 2024, 2025  wxMichael
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
using CollectiveModdingToolkit.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NtQueryProcessCOMWrapper;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CollectiveModdingToolkit.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
	[ObservableProperty] internal partial Computer Computer { get; private set; } = null!;
	[ObservableProperty] internal partial IModManager? ModManager { get; private set; }
	[ObservableProperty] internal partial Game Game { get; set; } = null!;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
	private partial bool CanRefresh { get; set; } = false;

	internal static string AppVersionString { get; } = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "0.0";
	internal string WindowTitle { get; } = $"Collective Modding Toolkit v{AppVersionString}";

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0044 // Add readonly modifier
	private static bool DEBUG_MODE = false;
#pragma warning restore IDE0044 // Add readonly modifier
#pragma warning restore IDE0079 // Remove unnecessary suppression

	public MainWindowViewModel()
	{
#if DEBUG
		DEBUG_MODE = true;
#endif
		Computer = new();
		try
		{
			CanRefresh = false;
			ModManager = DetectModManager();
			Game = new(ModManager);
			CanRefresh = true;
		}
		catch (Exception e)
		{
			MainWindow.CriticalError(e.GetType(), e.ToString());
		}
	}

	[RelayCommand(CanExecute = nameof(CanRefresh))]
	internal void Refresh()
	{
		CanRefresh = false;
		Task.Run(() =>
		{
			IModManager modManager;
			if (ModManager == null)
			{
				modManager = DetectModManager();
			}
			else
			{
				modManager = ModManager;
				modManager.Refresh();
			}
			Game game = new(modManager);

			Avalonia.Threading.Dispatcher.UIThread.Post(() =>
			{
				ModManager = modManager;
				Game = game;
				CanRefresh = true;
			});

		});
	}

	private static IModManager DetectModManager()
	{
		if (DEBUG_MODE)
			return new ModOrganizer();

		IntPtr processHandle = Computer.CurrentProcess;
		uint bufferSize = (uint)Marshal.SizeOf<PROCESS_BASIC_INFORMATION>();
		int status;
		PROCESS_BASIC_INFORMATION pbi = new();
		Process? parent = null;
		for (int i = 0; i < 4; i++)
		{
			uint returnLength = 0;
			status = NtQueryInformationProcessWrapper.NtQueryInformationProcess(processHandle, PROCESSINFOCLASS.ProcessBasicInformation, ref pbi, bufferSize, ref returnLength);
			if (status != 0)
				break;

			try
			{
				parent = Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
			}
			catch (ArgumentException) { break; }

			if (parent == null || parent.ProcessName == "explorer")
				break;
			if (parent.ProcessName == "ModOrganizer")
				return new ModOrganizer(parent.MainModule);
			if (parent.ProcessName == "Vortex")
				return new Vortex(parent.MainModule);

			processHandle = parent.Handle;
		}
		return new Unmanaged();
	}
}
