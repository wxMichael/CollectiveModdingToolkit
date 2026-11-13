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

namespace CollectiveModdingToolkit.Managers;

/// <summary>Works out which mod manager (if any) launched this app.</summary>
public static class ModManagerDetector
{
	public static IModManager Detect()
	{
#if DEBUG
		// Set CMT_DEBUG_MANAGER=Vortex (e.g. in launchSettings.json) to simulate Vortex; defaults to MO2.
		return string.Equals(Environment.GetEnvironmentVariable("CMT_DEBUG_MANAGER"), "Vortex",
			StringComparison.OrdinalIgnoreCase)
			? new Vortex()
			: new ModOrganizer();
#else
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

			switch (parent.ProcessName)
			{
				case "ModOrganizer":
					return new ModOrganizer(parent.MainModule);
				case "Vortex":
					return new Vortex(parent.MainModule);
				default:
					processHandle = parent.Handle;
					break;
			}
		}

		return new Unmanaged();
#endif
	}
}
