/*
 * 
 * Collective Modding Toolkit
 * Copyright (C) 2024, 2025  wxMichael
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
