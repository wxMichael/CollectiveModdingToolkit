using System;
using System.Runtime.InteropServices;

namespace NtQueryProcessCOMWrapper;

// Enum for the information class to retrieve
public enum PROCESSINFOCLASS
{
	ProcessBasicInformation = 0
	// Other values can be added as needed (e.g., ProcessTimes, ProcessMemory, etc.)
}

// Struct for basic process information
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

	//[GeneratedComInterface]
	//[Guid("D16D4D1A-BE01-11d0-9C8D-00C04FC2B078")]
	//[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[LibraryImport("ntdll.dll", SetLastError = true)]
	public static partial int NtQueryInformationProcess(
		IntPtr hProcess,
		PROCESSINFOCLASS processInformationClass,
		ref PROCESS_BASIC_INFORMATION pbi,
		uint length,
		ref uint returnLength);

	// Import OpenProcess using LibraryImport
	//[LibraryImport("kernel32.dll", SetLastError = true)]
	//public static partial IntPtr OpenProcess(
	//	ProcessAccessFlags accessFlags,
	//	[MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
	//	int processId);

	//[Flags]
	//public enum ProcessAccessFlags : uint
	//{
	//	QueryInformation = 0x0400
	//}

	//public static void GetProcessBasicInformation(int pid)
	//{
	//	IntPtr hProcess = OpenProcess(ProcessAccessFlags.QueryInformation, false, pid);
	//	if (hProcess == IntPtr.Zero)
	//	{
	//		Console.WriteLine("Unable to open process");
	//		return;
	//	}

	//	PROCESS_BASIC_INFORMATION pbi = new();
	//	uint returnLength = 0;

	//	// Call the NtQueryInformationProcess function
	//	int status = NtQueryInformationProcess(hProcess, PROCESSINFOCLASS.ProcessBasicInformation, ref pbi, (uint)Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)), ref returnLength);

	//	if (status == 0) // STATUS_SUCCESS
	//	{
	//		Console.WriteLine("Process ID: " + pbi.UniqueProcessId);
	//		Console.WriteLine("Inherited From Process ID: " + pbi.InheritedFromUniqueProcessId);
	//	}
	//	else
	//	{
	//		Console.WriteLine("Error querying process information.");
	//	}
	//}
}



//[StructLayout(LayoutKind.Sequential)]
//public struct PROCESS_BASIC_INFORMATION
//{
//	public IntPtr Reserved;
//	public IntPtr PebBaseAddress;
//	public IntPtr AffinityMask;
//	public IntPtr BasePriority;
//	public IntPtr UniqueProcessId;
//	public IntPtr InheritedFromUniqueProcessId;
//}

//public enum PROCESSINFOCLASS
//{
//	ProcessBasicInformation = 0
//}

//[GeneratedComInterface]
//[Guid("D16D4D1A-BE01-11d0-9C8D-00C04FC2B078")]
//[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
//public partial interface INtQueryInformationProcess
//{
//	int NtQueryInformationProcess(
//		IntPtr ProcessHandle,
//		PROCESSINFOCLASS ProcessInformationClass,
//		IntPtr ProcessInformation,
//		uint ProcessInformationLength,
//		out uint ReturnLength);
//}

//public class NtQueryInformationProcessWrapper : INtQueryInformationProcess
//{
//	public int NtQueryInformationProcess(
//		IntPtr ProcessHandle,
//		PROCESSINFOCLASS ProcessInformationClass,
//		IntPtr ProcessInformation,
//		uint ProcessInformationLength,
//		out uint ReturnLength) => NativeMethods.NtQueryInformationProcess(
//			ProcessHandle,
//			ProcessInformationClass,
//			ProcessInformation,
//			ProcessInformationLength,
//			out ReturnLength);
//}

//public static class NativeMethods
//{
//	[DllImport("ntdll.dll", SetLastError = true)]
//	public static extern int NtQueryInformationProcess(
//		IntPtr ProcessHandle,
//		PROCESSINFOCLASS ProcessInformationClass,
//		IntPtr ProcessInformation,
//		uint ProcessInformationLength,
//		out uint ReturnLength);
//}
