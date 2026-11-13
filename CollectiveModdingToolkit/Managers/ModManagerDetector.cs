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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Services;

namespace CollectiveModdingToolkit.Managers;

/// <summary>Works out which mod manager (if any) launched this app.</summary>
public static class ModManagerDetector
{
	/// <summary>
	///     Debug builds only: the full path of a ModOrganizer.exe or Vortex.exe to act as if launched by, for running
	///     from the IDE.
	/// </summary>
	private const string DebugManagerVariable = "CMT_DEBUG_MANAGER";

	public static IModManager Detect()
	{
#if DEBUG
		if (Environment.GetEnvironmentVariable(DebugManagerVariable) is { Length: > 0 } debugPath &&
		    FromExePath(debugPath) is { } debugManager)
			return debugManager;
#endif

		var processHandle = Computer.CurrentProcess;
		var bufferSize = (uint)Marshal.SizeOf<PROCESS_BASIC_INFORMATION>();
		PROCESS_BASIC_INFORMATION pbi = new();
		for (var i = 0; i < 4; i++)
		{
			uint returnLength = 0;
			var status = NtQueryInformationProcessWrapper.NtQueryInformationProcess(processHandle,
				PROCESSINFOCLASS.ProcessBasicInformation, ref pbi, bufferSize, ref returnLength);
			if (status != 0) break;

			Process? parent;
			try
			{
				parent = Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
			}
			catch (ArgumentException)
			{
				break;
			}

			if (parent.ProcessName == "explorer") break;

			if (parent.ProcessName is "ModOrganizer" or "Vortex")
			{
				string? exePath;
				try
				{
					exePath = parent.MainModule?.FileName;
				}
				catch (Win32Exception e)
				{
					Log.Warning($"Unable to read the path of {parent.ProcessName}: {e.Message}");
					break;
				}

				if (exePath != null && FromExePath(exePath, parent) is { } manager) return manager;
				break;
			}

			processHandle = parent.Handle;
		}

		return new Unmanaged();
	}

	private static IModManager? FromExePath(string exePath, Process? process = null) =>
		Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant() switch
		{
			"modorganizer" => new ModOrganizer(exePath, process),
			"vortex" => new Vortex(exePath),
			_ => null,
		};
}
