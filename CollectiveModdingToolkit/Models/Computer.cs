using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

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
	public static Computer? Instance { get; private set; }

	public static readonly string AppDataLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
	public static readonly string AppDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
	public static readonly string Documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
	public static readonly string DocumentsFallout4 = Path.Combine(Documents, "My Games", "Fallout4");

	[ObservableProperty] public partial bool CriticalErrorOpen { get; set; } = false;
	[ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;

	[ObservableProperty] public partial string Os { get; private set; }
	[ObservableProperty] public partial byte Bits { get; private set; }
	[ObservableProperty] public partial string Cpu { get; private set; }
	[ObservableProperty] public partial byte Ram { get; private set; }
	[ObservableProperty] public partial string Gpu { get; private set; }
	[ObservableProperty] public partial byte Vram { get; private set; }

	public Computer()
	{
		Instance = this;

		Os = GetOS();
		Bits = (byte)(Environment.Is64BitOperatingSystem ? 64 : 32);
		Cpu = GetCPU();
		GetPhysicallyInstalledSystemMemory(out long ram);
		Ram = (byte)(ram / 1024 / 1024);
		(Gpu, long vram) = GetGPU();
		Vram = (byte)Math.Round((double)vram / 1024 / 1024 / 1024);
	}

	private static string GetOS()
	{
		Version ver = Environment.OSVersion.Version;
		if (ver.Major == 10)
		{
			if (ver.Build >= 22000)
				return OperatingSystem.Windows11;
			if (ver.Build >= 17763)
				return OperatingSystem.Windows10;
		}
		else if (ver.Major == 6)
		{
			if (ver.Minor == 3)
				return OperatingSystem.Windows81;
			if (ver.Minor == 2)
				return OperatingSystem.Windows8;
			if (ver.Minor == 1)
				return OperatingSystem.Windows7;
			if (ver.Minor == 0)
				return OperatingSystem.WindowsVista;
		}
		else if (ver.Major == 5)
			return OperatingSystem.WindowsXP;
		return OperatingSystem.Unknown;
	}

	private static string GetCPU()
	{
		try
		{
			using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
			string? cpu = key?.GetValue("ProcessorNameString")?.ToString();
			if (cpu != null)
				return CPURegex().Replace(cpu, "");
		}
		catch { }
		return "Unknown CPU";
	}

	private static Tuple<string, long> GetGPU()
	{
		List<Tuple<string, long>> adapters = [];
		RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
		if (key == null)
			return new Tuple<string, long>("????", 0);

		foreach (string subKey in key.GetSubKeyNames())
		{
			if (!subKey.StartsWith('0'))
				continue;
			RegistryKey? productKey = key.OpenSubKey(subKey);
			if (productKey == null)
				continue;

			string? adapterString = productKey.GetValue("HardwareInformation.AdapterString") as string;
			long? memorySize = productKey.GetValue("HardwareInformation.qwMemorySize") as long?;

			adapters.Add(new(adapterString ?? "????", memorySize ?? 0));
		}
		if (adapters.Count == 0)
			return new Tuple<string, long>("????", 0);
		return adapters[0]; // TODO: Identify proper adapter
	}

	[DllImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);
	[GeneratedRegex(@"(?:\d+(?:th|rd|nd) Gen| ?Processor| ?CPU|\d*[- ]Core|\(TM\)|\(R\))")]
	private static partial Regex CPURegex();
}
