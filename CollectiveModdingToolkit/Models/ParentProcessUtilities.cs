using System;
using System.Runtime.InteropServices;

namespace NtQueryProcessCOMWrapper;

public enum PROCESSINFOCLASS
{
	ProcessBasicInformation = 0
}

[StructLayout(LayoutKind.Sequential)]
public struct PROCESS_BASIC_INFORMATION
{
	public IntPtr Reserved;
	public IntPtr PebBaseAddress;
	public IntPtr AffinityMask;
	public IntPtr BasePriority;
	public IntPtr UniqueProcessId;
	public IntPtr InheritedFromUniqueProcessId;
}

public partial class NtQueryInformationProcessWrapper
{
	[LibraryImport("ntdll.dll", SetLastError = true)]
	public static partial int NtQueryInformationProcess(
		IntPtr hProcess,
		PROCESSINFOCLASS processInformationClass,
		ref PROCESS_BASIC_INFORMATION pbi,
		uint length,
		ref uint returnLength);
}
