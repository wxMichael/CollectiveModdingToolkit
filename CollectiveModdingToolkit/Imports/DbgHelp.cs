using System;
using System.Runtime.InteropServices;

namespace CollectiveModdingToolkit.Imports;

internal static class DbgHelp
{
	[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool SymInitializeW(
		[In] IntPtr hProcess,
		[In, Optional] string UserSearchPath,
		[In][MarshalAs(UnmanagedType.Bool)] bool fInvadeProcess);

	[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool SymCleanup(
		[In] IntPtr hProcess);

	[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	internal static extern ulong SymLoadModuleExW(
		[In] IntPtr hProcess,
		[In] IntPtr hFile,
		[In] string ImageName,
		[In] string ModuleName,
		[In] long BaseOfDll,
		[In] int DllSize,
		[In] IntPtr Data,
		[In] int Flags);

	[DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	internal static extern bool SymEnumSymbols(
		[In] IntPtr hProcess,
		[In] ulong BaseOfDll,
		[In, Optional] string Mask,
		[In] SymEnumSymbolsProc EnumSymbolsCallback,
		[In, Optional] IntPtr UserContext);

	internal delegate bool SymEnumSymbolsProc(ref SYMBOL_INFO SymbolInfo, uint SymbolSize, IntPtr UserContext);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	internal struct SYMBOL_INFO
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
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string Name;
	}
}
