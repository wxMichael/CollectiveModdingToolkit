using CollectiveModdingToolkit.Managers;
using CollectiveModdingToolkit.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NtQueryProcessCOMWrapper;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CollectiveModdingToolkit.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
	private static bool DEBUG_MODE = false;

	private static readonly Version _version = new("0.6"); // TODO: preprocessor directive?

	public string WindowTitle { get; } = $"Collective Modding Toolkit {_version}";
	[ObservableProperty] public partial Computer Computer { get; private set; } = null!;
	[ObservableProperty] public partial IModManager? ModManager { get; private set; }
	[ObservableProperty] public partial Game Game { get; set; } = null!;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
	private partial bool CanRefresh { get; set; } = false;

	public MainWindowViewModel()
	{
#if DEBUG
		DEBUG_MODE = true;
#endif
		Refresh();//.Wait();

		//string dataPath = @"C:\Program Files (x86)\Steam\steamapps\common\Fallout 4\Data";
		//string pluginPath = Path.Combine(dataPath, "community fixes merged.esp");
		//Console.WriteLine($"Main: community fixes merged.esp Exists: {File.Exists(pluginPath)}");

		//ProcessStartInfo info = new()
		//{
		//	CreateNoWindow = false,
		//	UseShellExecute = false,
		//	FileName = @"C:\Users\Michael\source\repos\CollectiveModdingToolkit\MO2VFSBypass\bin\publish\MO2VFSBypass.exe",
		//};
		//Process? process = Process.Start(info);
	}

	[RelayCommand(CanExecute = nameof(CanRefresh))]
	public Task Refresh()
	{
		CanRefresh = false;
		return Task.Run(async () =>
		{
			Computer computer = new();
			IModManager modManager;
			if (ModManager == null)
			{
				modManager = DetectModManager();
				//try
				//{
				//	modManager = DetectModManager();
				//}
				//catch (Exception e)
				//{
				//	modManager = null;
				//	Console.WriteLine(e.Message);
				//}
			}
			else
			{
				modManager = ModManager;
				modManager.Refresh();
			}
			Game game = new(modManager);

			await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
			{
				Computer = computer;
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
					return new Unmanaged();

				pbi = Marshal.PtrToStructure<PROCESS_BASIC_INFORMATION>(pbiPtr.Value);
				parent = Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
				if (parent == null)
					return new Unmanaged();

				if (parent.ProcessName == "ModOrganizer")
					return new ModOrganizer(parent.MainModule);
				if (parent.ProcessName == "Vortex")
					return new Vortex(parent.MainModule);

				processHandle = parent.Handle;
			}
			//if (Name == null) return new Unmanaged();
			// TODO: Handle nulls
		}
		catch (Exception e)
		{
			Console.WriteLine(e.Message);
			return new Unmanaged();
		}
		finally
		{
			if (pbiPtr.HasValue)
				Marshal.FreeHGlobal(pbiPtr.Value);
		}
		return new Unmanaged();
	}
}
