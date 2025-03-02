using CollectiveModdingToolkit.Imports;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace CollectiveModdingToolkit.Models.FileTypes;

public partial class DLL : IFile
{
	private static readonly HashSet<string> _OGNGWhitelist = [
		"AchievementsModsEnablerLoader.dll",
		"BetterConsole.dll",
		"Buffout4.dll",
		"ClockWidget.dll",
		"FloatingDamage.dll",
		"GCBugFix.dll",
		"HUDPlusPlus.dll",
		"IndirectFire.dll",
		"MinimalMinimap.dll",
		"MoonRotationFix.dll",
		"mute_on_focus_loss.dll",
		"SprintStutteringFix.dll",
		"UnlimitedFastTravel.dll",
		"WeaponDebrisCrashFix.dll",
		"x-cell-fo4.dll",
	];

	public string Name { get; private set; }
	public string Ext { get; } = ".dll";
	public string PathFull { get; private set; }
	public string PathRelative { get; private set; }

	public bool WasRead => IsReadable != null;
	[MemberNotNullWhen(true, nameof(IsF4SE), nameof(SupportsOG), nameof(SupportsNG))]
	public bool? IsReadable { get; private set; }

	public int Priority { get; private set; }
	//[MemberNotNullWhen(true, nameof(Mod))]
	public bool IsManaged => Mod != null;
	public string? Mod { get; private set; }

	public bool IsF4SE => _symbols.Contains("F4SEPlugin_Load");
	public bool SupportsOG => _symbols.Contains("F4SEPlugin_Query");
	public bool SupportsNG => _symbols.Contains("F4SEPlugin_Version");
	public bool SupportsOGNG => SupportsOG && SupportsNG;
	public bool SupportsOGNGVerified => SupportsOGNG && _OGNGWhitelist.Contains(Name);
	public bool SupportsOGNGUnverified => SupportsOGNG && !_OGNGWhitelist.Contains(Name);
	public bool? SupportsCurrent =>
		Game.Instance != null
		&& ((Game.Instance.IsOG && SupportsOG)
		 || (Game.Instance.IsNG && SupportsNG));

	private readonly HashSet<string> _symbols = [];

	internal DLL(
		string name,
		string pathFull,
		string? pathRelative = null,
		int priority = 0,
		string? modName = null)
	{
		Name = name;
		PathFull = pathFull;
		PathRelative = pathRelative ?? $"F4SE/Plugins/{name}";
		Priority = priority == 0 ? 0 : priority + 1;
		Mod = modName;
	}


	internal bool EnumSymbolsCallback(ref DbgHelp.SYMBOL_INFO SymbolInfo, uint SymbolSize, nint UserContext)
	{
		if (!SymbolInfo.Name.Contains("$dtor"))
			_symbols.Add(SymbolInfo.Name);
		return true;
	}

	internal void Read()
	{
		if (WasRead)
			throw new NotSupportedException("DLL read twice");

		try
		{
			if (!DbgHelp.SymInitializeW(Computer.CurrentProcess, null!, false))
			{
				Console.WriteLine("Failed to initialize sym.");
				return;
			}

			ulong baseOfDll = DbgHelp.SymLoadModuleExW(Computer.CurrentProcess, nint.Zero, PathFull, null!, 0, 0, nint.Zero, 0);
			if (baseOfDll == 0)
			{
				int error = Marshal.GetLastWin32Error();
				Console.WriteLine($"Failed to load DLL: {Marshal.GetPInvokeErrorMessage(error)}");
				IsReadable = false;
				return;
			}

			bool status = DbgHelp.SymEnumSymbols(Computer.CurrentProcess, baseOfDll, "F4SEPlugin*", EnumSymbolsCallback, nint.Zero);
			if (!status)
			{
				Console.WriteLine("Failed to enum symbols.");
				IsReadable = false;
				return;
			}
			IsReadable = true;
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			IsReadable = false;
		}
		finally
		{
			DbgHelp.SymCleanup(Computer.CurrentProcess);
		}
	}
}
