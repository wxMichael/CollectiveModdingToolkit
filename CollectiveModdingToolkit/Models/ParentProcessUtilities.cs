using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace NtQueryProcessCOMWrapper;

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

public enum PROCESSINFOCLASS
{
	ProcessBasicInformation = 0
}

[GeneratedComInterface]
[Guid("D16D4D1A-BE01-11d0-9C8D-00C04FC2B078")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public partial interface INtQueryInformationProcess
{
	// Define the method signature for NtQueryInformationProcess
	int NtQueryInformationProcess(
		IntPtr ProcessHandle,
		PROCESSINFOCLASS ProcessInformationClass,
		IntPtr ProcessInformation,
		uint ProcessInformationLength,
		out uint ReturnLength);
}

public class NtQueryInformationProcessWrapper : INtQueryInformationProcess
{
	public int NtQueryInformationProcess(
		IntPtr ProcessHandle,
		PROCESSINFOCLASS ProcessInformationClass,
		IntPtr ProcessInformation,
		uint ProcessInformationLength,
		out uint ReturnLength) => NativeMethods.NtQueryInformationProcess(
			ProcessHandle,
			ProcessInformationClass,
			ProcessInformation,
			ProcessInformationLength,
			out ReturnLength);
}

internal static class NativeMethods
{
	[DllImport("ntdll.dll", SetLastError = true)]
	public static extern int NtQueryInformationProcess(
		IntPtr ProcessHandle,
		PROCESSINFOCLASS ProcessInformationClass,
		IntPtr ProcessInformation,
		uint ProcessInformationLength,
		out uint ReturnLength);
}
