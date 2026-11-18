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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CollectiveModdingToolkit.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;

namespace CollectiveModdingToolkit.Models;

public static class OperatingSystem
{
	public const string Unknown = "Unknown";
	public const string Windows2000 = "Windows 2000";
	public const string WindowsXP = "Windows XP";
	public const string WindowsXPProx64 = "Windows XP Pro x64";
	public const string WindowsHomeServer = "Windows Home Server";
	public const string WindowsServer2003 = "Windows Server 2003";
	public const string WindowsServer2003R2 = "Windows Server 2003 R2";
	public const string WindowsVista = "Windows Vista";
	public const string WindowsServer2008 = "Windows Server 2008";
	public const string WindowsServer2008R2 = "Windows Server 2008 R2";
	public const string Windows7 = "Windows 7";
	public const string WindowsServer2012 = "Windows Server 2012";
	public const string Windows8 = "Windows 8";
	public const string Windows81 = "Windows 8.1";
	public const string WindowsServer2012R2 = "Windows Server 2012 R2";
	public const string WindowsServer2016 = "Windows Server 2016";
	public const string WindowsServer2019 = "Windows Server 2019";
	public const string Windows10 = "Windows 10";
	public const string Windows11 = "Windows 11";
	public const string WindowsServer2022 = "Windows Server 2022";
}

public partial class Computer : ObservableObject
{
	// Pseudo-handle for the current process. Process.GetCurrentProcess().Handle would be closed once the Process
	// object is finalized, making later NtQueryInformationProcess calls (e.g. on refresh) fail.
	public static readonly nint CurrentProcess = -1;

	public static readonly string AppDataLocal =
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

	public static readonly string Documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
	public static readonly string DocumentsFallout4 = Path.Combine(Documents, "My Games", "Fallout4");

	public Computer()
	{
		OS = GetOS();
		Bits = (byte)(Environment.Is64BitOperatingSystem ? 64 : 32);
		CPU = GetCPU();
		RAM = GetRamGB();

		var adapters = GpuDetector.Detect();
		if (adapters.Count > 0)
		{
			// The adapter with the most video memory is the one to report: a laptop's integrated GPU is never the one games use.
			GPU = adapters[0].Name;
			VRAM = (int)Math.Round(adapters[0].DedicatedMemory / (double)(1L << 30));
			GpuDetails = string.Join(Environment.NewLine, adapters.Select(a => a.DedicatedMemory > 0
				? $"{a.Name}: {Math.Round(a.DedicatedMemory / (double)(1L << 30))} GB"
				: a.Name));
		}
		else
		{
			GPU = "Unknown GPU";
			GpuDetails = "No graphics adapter could be detected.";
		}

		Log.Info($"PC: {OS} {Bits}-bit, {CPU}, {RAM} GB RAM, GPU: {GpuDetails.ReplaceLineEndings("; ")}");
	}

	[ObservableProperty] public partial string OS { get; private set; }
	[ObservableProperty] public partial byte Bits { get; private set; }
	[ObservableProperty] public partial string CPU { get; private set; }
	[ObservableProperty] public partial int RAM { get; private set; }
	[ObservableProperty] public partial string GPU { get; private set; }
	[ObservableProperty] public partial int VRAM { get; private set; }

	/// <summary>Every graphics adapter found, one per line with its video memory, for a tooltip.</summary>
	public string GpuDetails { get; }

	/// <summary>The GPU's name and video memory, or just the name if the memory is unknown.</summary>
	public string GpuText => VRAM > 0 ? $"{GPU} {VRAM} GB" : GPU;

	private static string GetOS()
	{
		var ver = Environment.OSVersion.Version;
		switch (ver.Major)
		{
			case 10:
				switch (ver.Build)
				{
					case >= 22000:
						return OperatingSystem.Windows11;
					case >= 17763:
						return OperatingSystem.Windows10;
				}

				break;
			case 6:
				switch (ver.Minor)
				{
					case 3:
						return OperatingSystem.Windows81;
					case 2:
						return OperatingSystem.Windows8;
					case 1:
						return OperatingSystem.Windows7;
					case 0:
						return OperatingSystem.WindowsVista;
				}

				break;
			case 5:
				return OperatingSystem.WindowsXP;
		}

		return OperatingSystem.Unknown;
	}

	private static string GetCPU()
	{
		try
		{
			using var key =
				Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
			var cpu = key?.GetValue("ProcessorNameString")?.ToString();
			if (cpu != null)
			{
				// e.g. "Genuine Intel(R) CPU 0000 @ 2.50GHz" -> "Intel 0000"
				if (cpu.Contains("Intel", StringComparison.Ordinal) &&
				    !cpu.StartsWith("Intel", StringComparison.Ordinal))
					cpu = $"Intel {cpu.Replace("Intel", string.Empty, StringComparison.Ordinal)}";

				cpu = WhitespaceRegex().Replace(CPURegex().Replace(cpu, string.Empty), " ");
				// The clock speed after "@" is noise: the model says what it is.
				var at = cpu.LastIndexOf('@');
				return (at >= 0 ? cpu[..at] : cpu).Trim();
			}
		}
		catch
		{
			// TODO: Error handling
		}

		return "Unknown CPU";
	}

	private static int GetRamGB()
	{
		// The amount of RAM installed, which is what people think of as their RAM (the OS reports a little less as usable).
		if (GetPhysicallyInstalledSystemMemory(out var kilobytes) && kilobytes > 0)
			return (int)Math.Round(kilobytes / 1024.0 / 1024.0);

		// Not available on every machine (e.g. some virtual machines). Fall back to the memory Windows reports.
		return (int)Math.Round(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (double)(1L << 30));
	}

	[LibraryImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);

	[GeneratedRegex(@"(?:\d+(?:th|rd|nd) Gen| ?Processor| ?CPU|\d*[- ]Core|\(TM\)|\(R\))")]
	private static partial Regex CPURegex();

	[GeneratedRegex(@"\s+")]
	private static partial Regex WhitespaceRegex();
}
