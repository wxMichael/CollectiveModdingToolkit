using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace NtQueryProcessCOMWrapper;

// Define the structure for PROCESS_BASIC_INFORMATION
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

// Define the enumeration for PROCESSINFOCLASS
public enum PROCESSINFOCLASS
{
	ProcessBasicInformation = 0
}

// Define the COM interface for NtQueryInformationProcess
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

// COM Callable Wrapper class
public class NtQueryInformationProcessWrapper : INtQueryInformationProcess
{
	// Implement the NtQueryInformationProcess function
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

// Define the native method using P/Invoke
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


//[DllImport("ntdll.dll", SetLastError = true)]
//public static extern int NtQueryInformationProcess(
//	IntPtr ProcessHandle,
//	PROCESSINFOCLASS ProcessInformationClass,
//	IntPtr ProcessInformation,
//	uint ProcessInformationLength,
//	out uint ReturnLength);

//public static Process? GetParentProcess()
//{
//	return GetParentProcess(Process.GetCurrentProcess().Handle);
//}

//public static Process? GetParentProcess(int id)
//{
//	return GetParentProcess(Process.GetProcessById(id).Handle);
//}

//public static Process? GetParentProcess(IntPtr handle)
//{
//	ParentProcessUtilities pbi = new();
//	int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out _);
//	if (status != 0) throw new Win32Exception(status);

//	try
//	{
//		return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
//	}
//	catch (ArgumentException)
//	{
//		// not found
//		return null;
//	}
//}
//}
