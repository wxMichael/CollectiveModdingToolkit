using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace CollectiveModdingToolkit.Models;

public partial class DLL : IFile
{
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

	private readonly HashSet<string> _symbols = [];

	private static readonly IntPtr hCurrentProcess = Process.GetCurrentProcess().Handle;

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

	internal void Read()
	{
		if (WasRead)
			throw new NotSupportedException("DLL read twice");

		try
		{
			if (!SymInitializeW(hCurrentProcess, null!, false))
			{
				Console.WriteLine("Failed to initialize sym.");
				return;
			}

			ulong baseOfDll = SymLoadModuleExW(hCurrentProcess, nint.Zero, PathFull, null!, 0, 0, nint.Zero, 0);

			if (baseOfDll == 0)
			{
				int error = Marshal.GetLastWin32Error();
				Console.WriteLine($"Failed to load module. {WinErrors.GetSystemMessage((uint)error)}");
				IsReadable = false;
				//SymCleanup(hCurrentProcess);
				return;
			}

			//if (!SymEnumerateSymbols64(hCurrentProcess, baseOfDll, EnumSyms, IntPtr.Zero))
			if (!SymEnumSymbols(hCurrentProcess, baseOfDll, "", EnumSymbolsCallback, IntPtr.Zero))
			{
				Console.WriteLine("Failed to enum symbols.");
				IsReadable = false;
				return;
			}
			IsReadable = true;
		}
		catch (Exception ex)
		{
			//IsReadable = false;
			Console.WriteLine(ex.Message);
		}
		finally
		{
			bool v = SymCleanup(hCurrentProcess);
			if (!v)
				Console.WriteLine(v);
		}
	}

	[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SymInitializeW(
		[In] IntPtr hProcess,
		[In, Optional] string UserSearchPath,
		[In][MarshalAs(UnmanagedType.Bool)] bool fInvadeProcess);

	[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SymCleanup(
		[In] IntPtr hProcess);

	[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern ulong SymLoadModuleExW(
		[In] IntPtr hProcess,
		[In] IntPtr hFile,
		[In] string ImageName,
		[In] string ModuleName,
		[In] long BaseOfDll,
		[In] int DllSize,
		[In] IntPtr Data,
		[In] int Flags);

	[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SymEnumerateSymbols64(
		[In] IntPtr hProcess,
		[In] ulong BaseOfDll,
		[In] SymEnumerateSymbolsProc64 EnumSymbolsCallback,
		[In, Optional] IntPtr UserContext);

	[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern bool SymEnumSymbols(
		[In] IntPtr hProcess,
		[In] ulong BaseOfDll,
		[In, Optional] string Mask,
		[In] SymEnumSymbolsProc EnumSymbolsCallback,
		[In, Optional] IntPtr UserContext);


	private delegate bool SymEnumerateSymbolsProc64(string SymbolName, ulong SymbolAddress, uint SymbolSize, IntPtr UserContext);
	private delegate bool SymEnumSymbolsProc(IntPtr pSymInfo, uint SymbolSize, IntPtr UserContext);

	private bool EnumSyms(string name, ulong address, uint size, IntPtr context)
	{
		if (name.StartsWith("F4SEPlugin"))
		{
			Console.WriteLine($"{Name}: {name}");
			_symbols.Add(name);
		}
		return true;
	}

	private bool EnumSymbolsCallback(SYMBOL_INFO pSymInfo, uint SymbolSize, IntPtr UserContext)
	{

		if (pSymInfo.Name.StartsWith("F4SEPlugin"))
		{
			Console.WriteLine($"{Name}: {pSymInfo.Name}");
			_symbols.Add(pSymInfo.Name);
		}
		return true;
	}

	//private void readit()
	//{
	//	IntPtr processHandle = Process.GetCurrentProcess().Handle;
	//	NtQueryInformationProcessWrapper comWrapper = new();
	//	uint bufferSize = (uint)Marshal.SizeOf<SYMBOL_INFO>();
	//	IntPtr? pSymInfo = null;
	//	try
	//	{
	//		pSymInfo = Marshal.AllocHGlobal((int)bufferSize);
	//		int status;
	//		SYMBOL_INFO symInfo;
	//		Process? parent = null;
	//		for (int i = 0; i < 10; i++)
	//		{
	//			status = comWrapper.NtQueryInformationProcess(processHandle, PROCESSINFOCLASS.ProcessBasicInformation, pSymInfo.Value, bufferSize, out _);
	//			if (status != 0)
	//				return new Unmanaged();

	//			symInfo = Marshal.PtrToStructure<SYMBOL_INFO>(pSymInfo.Value);
	//		}
	//		//if (Name == null) return new Unmanaged();
	//		// TODO: Handle nulls
	//	}
	//	catch (Exception e)
	//	{
	//		Console.WriteLine(e.Message);
	//		return new Unmanaged();
	//	}
	//	finally
	//	{
	//		if (pSymInfo.HasValue)
	//			Marshal.FreeHGlobal(pSymInfo.Value);
	//	}
	//}
}

public static class WinErrors
{
	/// <summary>
	/// Gets a user friendly string message for a system error code
	/// </summary>
	/// <param name="errorCode">System error code</param>
	/// <returns>Error string</returns>
	public static string GetSystemMessage(uint errorCode)
	{
		Win32Exception exception = new((int)errorCode);
		return exception.Message;
	}
}


//public delegate IntPtr PSYMBOL_INFO();




[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct SYMBOL_INFO
{
	public uint SizeOfStruct;
	public uint TypeIndex;
	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
	public ulong[] Reserved;
	public uint Index;
	public uint Size;
	public ulong ModBase;
	public uint Flags;
	public ulong Value;
	public ulong Address;
	public uint Register;
	public uint Scope;
	public uint Tag;
	public uint NameLen;
	public uint MaxNameLen;
	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1)]
	public string Name; // Note: This will need to be handled differently for actual usage
}

public class DbgHelp
{

	[DllImport("dbghelp.dll", SetLastError = true)]
	private static extern bool SymEnumSymbols(IntPtr hProcess, ulong BaseOfDll, string Mask, EnumSymbolsCallback Callback, IntPtr UserContext);

	private delegate bool EnumSymbolsCallback(ref SYMBOL_INFO SymbolInfo, uint SymbolSize, IntPtr UserContext);

}
