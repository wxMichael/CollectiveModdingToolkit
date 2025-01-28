using CollectiveModdingToolkit.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NtQueryProcessCOMWrapper;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CollectiveModdingToolkit.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
	public static bool DEBUG_MODE = false;

	private static readonly Version _version = new("0.6"); // TODO: preprocessor directive?

	public string WindowTitle { get; } = $"Collective Modding Toolkit {_version}";
	[ObservableProperty] public partial Computer Computer { get; private set; }
	[ObservableProperty] public partial IModManager? ModManager { get; private set; }
	[ObservableProperty] public partial Game Game { get; private set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
	private partial bool CanRefresh { get; set; } = false;

	public MainWindowViewModel()
	{
#if DEBUG
		DEBUG_MODE = true;
#endif
		Computer = new();
		try
		{
			ModManager = DetectModManager();
		}
		catch (Exception e)
		{
			ModManager = null;
			Console.WriteLine(e.Message);
		}
		Game = new(ModManager);
		CanRefresh = true;

		string dataPath = @"C:\Program Files (x86)\Steam\steamapps\common\Fallout 4\Data";
		string pluginPath = Path.Combine(dataPath, "community fixes merged.esp");
		Console.WriteLine($"Main: community fixes merged.esp Exists: {File.Exists(pluginPath)}");

		ProcessStartInfo info = new()
		{
			CreateNoWindow = false,
			UseShellExecute = false,
			FileName = @"C:\Users\Michael\source\repos\CollectiveModdingToolkit\MO2VFSBypass\bin\publish\MO2VFSBypass.exe",
		};
		Process? process = Process.Start(info);

	}

	private static IModManager? DetectModManager()
	{
		if (DEBUG_MODE)
			return new ModOrganizer();

		// TODO: Abstract code
		IntPtr processHandle = Process.GetCurrentProcess().Handle;
		NtQueryInformationProcessWrapper comWrapper = new();
		uint bufferSize = (uint)Marshal.SizeOf<PROCESS_BASIC_INFORMATION>();
		IntPtr? pbiPtr = null;
		try
		{
			pbiPtr = Marshal.AllocHGlobal((int)bufferSize);
			int status;
			PROCESS_BASIC_INFORMATION pbi;
			Process? parent = null;
			for (int i = 0; i < 10; i++)
			{
				status = comWrapper.NtQueryInformationProcess(processHandle, PROCESSINFOCLASS.ProcessBasicInformation, pbiPtr.Value, bufferSize, out _);
				if (status != 0)
					return null;

				pbi = Marshal.PtrToStructure<PROCESS_BASIC_INFORMATION>(pbiPtr.Value);
				parent = Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
				if (parent == null)
					return null;

				if (parent.ProcessName == "ModOrganizer")
					return new ModOrganizer(parent.MainModule);
				if (parent.ProcessName == "Vortex")
					return new Vortex(parent.MainModule);

				processHandle = parent.Handle;
			}
			//if (Name == null) return null;
			// TODO: Handle nulls
		}
		catch (Exception e)
		{
			Console.WriteLine(e.Message);
			return null;
		}
		finally
		{
			if (pbiPtr.HasValue)
				Marshal.FreeHGlobal(pbiPtr.Value);
		}
		return null;
	}

	[RelayCommand(CanExecute = nameof(CanRefresh))]
	public void Refresh()
	{
		CanRefresh = false;
		Task.Run(async () =>
		{
			Computer computer = new();
			ModManager?.RefreshSettings();
			Game game = new(ModManager);

			await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
			{
				Computer = computer;
				Game = game;
				CanRefresh = true;
			});
		});
	}
}
