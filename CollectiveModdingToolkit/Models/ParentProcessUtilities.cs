/*
 *
 * Collective Modding Toolkit
 * Copyright (C) 2024, 2025, 2026  wxMichael
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

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace CollectiveModdingToolkit.Models;

public enum PROCESSINFOCLASS
{
	ProcessBasicInformation = 0,
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

// ReSharper disable once ClassNeverInstantiated.Global
public partial class NtQueryInformationProcessWrapper
{
	private const int ProcessCommandLineInformation = 60;
	private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);

	[LibraryImport("ntdll.dll", SetLastError = true)]
	public static partial int NtQueryInformationProcess(
		IntPtr hProcess,
		PROCESSINFOCLASS processInformationClass,
		ref PROCESS_BASIC_INFORMATION pbi,
		uint length,
		ref uint returnLength);

	[LibraryImport("ntdll.dll", EntryPoint = "NtQueryInformationProcess")]
	private static partial int NtQueryInformationProcessBuffer(
		IntPtr hProcess,
		int processInformationClass,
		IntPtr buffer,
		uint length,
		out uint returnLength);

	[LibraryImport("shell32.dll", EntryPoint = "CommandLineToArgvW", StringMarshalling = StringMarshalling.Utf16)]
	private static partial IntPtr CommandLineToArgv(string commandLine, out int argumentCount);

	/// <summary>Reads another process's command line, split into arguments the way Windows programs do.</summary>
	/// <returns>The arguments, starting with the program, or null if they couldn't be read.</returns>
	public static string[]? GetCommandLineArguments(IntPtr processHandle)
	{
		var status = NtQueryInformationProcessBuffer(processHandle, ProcessCommandLineInformation, IntPtr.Zero, 0,
			out var length);
		if (status != StatusInfoLengthMismatch || length == 0) return null;

		var buffer = Marshal.AllocHGlobal((int)length);
		try
		{
			if (NtQueryInformationProcessBuffer(processHandle, ProcessCommandLineInformation, buffer, length,
				    out _) != 0) return null;

			// The buffer starts with a UNICODE_STRING pointing at the text later in the buffer.
			var byteLength = (ushort)Marshal.ReadInt16(buffer);
			var text = Marshal.ReadIntPtr(buffer, IntPtr.Size);
			var commandLine = Marshal.PtrToStringUni(text, byteLength / 2);
			return commandLine.Length == 0 ? null : SplitCommandLine(commandLine);
		}
		finally
		{
			Marshal.FreeHGlobal(buffer);
		}
	}

	private static string[]? SplitCommandLine(string commandLine)
	{
		var argv = CommandLineToArgv(commandLine, out var count);
		if (argv == IntPtr.Zero) return null;

		try
		{
			var arguments = new string[count];
			for (var i = 0; i < count; i++)
				arguments[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size)) ?? string.Empty;
			return arguments;
		}
		finally
		{
			Marshal.FreeHGlobal(argv);
		}
	}
}

public static partial class RegistryKeyInfo
{
	[LibraryImport("advapi32.dll", EntryPoint = "RegQueryInfoKeyW")]
	private static partial int RegQueryInfoKey(SafeRegistryHandle key, IntPtr className, IntPtr classLength,
		IntPtr reserved, IntPtr subKeys, IntPtr maxSubKeyLength, IntPtr maxClassLength, IntPtr values,
		IntPtr maxValueNameLength, IntPtr maxValueLength, IntPtr securityDescriptorLength, out long lastWriteTime);

	/// <returns>When the key or any of its values was last written, or null if it couldn't be read.</returns>
	public static DateTime? GetLastWriteTimeUtc(RegistryKey key) =>
		RegQueryInfoKey(key.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
			IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out var fileTime) == 0
			? DateTime.FromFileTimeUtc(fileTime)
			: null;
}
