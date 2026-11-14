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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using CollectiveModdingToolkit.Services;
using Microsoft.Win32;

namespace CollectiveModdingToolkit.Models;

/// <summary>
///     Finds the computer's graphics adapters.
///     <para>
///         DXGI, the API games use to pick a GPU, decides which adapters exist. It skips the virtual displays that remote
///         desktop and
///         USB display drivers add, and flags software renderers, so those are never reported as the user's GPU. DXGI
///         holds back a
///         little video memory, so the larger of its figure and the driver's own registry figure is used.
///     </para>
///     <para>
///         The registry alone isn't enough: virtual display drivers appear there too, sometimes first, with no memory
///         information.
///         WMI's <c>AdapterRAM</c> is not an option because it is 32-bit and tops out at 4 GB.
///     </para>
/// </summary>
internal static class GpuDetector
{
	private const uint DxgiAdapterFlagSoftware = 2;
	private const uint VendorMicrosoft = 0x1414;

	private const string DisplayAdapterClassKey =
		@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

	// Calls DXGI through raw vtable function pointers rather than COM interop, which Native AOT doesn't support.
	// Slot numbers count IUnknown (QueryInterface 0, AddRef 1, Release 2) and then each base interface in order.
	private const int ReleaseSlot = 2;
	private const int FactoryEnumAdapters1Slot = 12;
	private const int AdapterGetDesc1Slot = 10;

	/// <summary>
	///     All graphics adapters found, with the most capable (most dedicated memory) first. Empty if none could be
	///     found.
	/// </summary>
	internal static IReadOnlyList<Adapter> Detect()
	{
		var registry = ReadRegistry();

		List<Adapter> adapters = [];
		try
		{
			adapters = EnumerateDxgi();
		}
		catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or COMException
			                          or MarshalDirectiveException)
		{
			// e.g. under Wine. The registry still has the adapters.
			Log.Warning($"DXGI is unavailable, using the registry to find the GPU: {e.Message}");
		}

		if (adapters.Count == 0)
			adapters = [.. registry.Select(kv => new Adapter(kv.Key, kv.Value))];
		else
			adapters =
			[
				.. adapters.Select(a =>
					registry.TryGetValue(a.Name, out var registryMemory) && registryMemory > a.DedicatedMemory
						? a with { DedicatedMemory = registryMemory }
						: a),
			];

		// OrderBy is stable, so equal adapters stay in the order they were found.
		return [.. adapters.OrderByDescending(a => a.DedicatedMemory)];
	}

	private static unsafe List<Adapter> EnumerateDxgi()
	{
		var factoryId = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
		nint factory;
		var hr = CreateDXGIFactory1(&factoryId, &factory);
		if (hr < 0) throw new COMException("CreateDXGIFactory1 failed.", hr);

		List<Adapter> adapters = [];
		try
		{
			for (uint index = 0;; index++)
			{
				nint adapter;
				// Fails with DXGI_ERROR_NOT_FOUND once there are no more adapters.
				if (((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)VTable(factory, FactoryEnumAdapters1Slot))(
					    factory, index, &adapter) < 0) break;

				try
				{
					DXGI_ADAPTER_DESC1 description;
					if (((delegate* unmanaged[Stdcall]<nint, DXGI_ADAPTER_DESC1*, int>)VTable(adapter,
						    AdapterGetDesc1Slot))(adapter, &description) < 0) continue;

					// "Microsoft Basic Render Driver" and WARP are software, not a GPU the user could be playing on.
					if ((description.Flags & DxgiAdapterFlagSoftware) != 0 ||
					    description.VendorId == VendorMicrosoft) continue;

					Adapter found = new(new string(description.Description).TrimEnd('\0').Trim(),
						(long)description.DedicatedVideoMemory);
					// Some systems list one card more than once (e.g. an indirect display driver adds a copy). Show it once.
					if (!adapters.Any(a => a.Name == found.Name && a.DedicatedMemory == found.DedicatedMemory))
						adapters.Add(found);
				}
				finally
				{
					Release(adapter);
				}
			}
		}
		finally
		{
			Release(factory);
		}

		return adapters;
	}

	private static unsafe nint VTable(nint comObject, int slot) => (*(nint**)comObject)[slot];

	// ReSharper disable once RedundantUnsafeContext
	private static unsafe void Release(nint comObject)
	{
		((delegate* unmanaged[Stdcall]<nint, uint>)VTable(comObject, ReleaseSlot))(comObject);
	}

	/// <summary>Adapter name to video memory in bytes (0 if the driver doesn't say), from the display driver registry entries.</summary>
	private static Dictionary<string, long> ReadRegistry()
	{
		Dictionary<string, long> result = new(StringComparer.OrdinalIgnoreCase);
		try
		{
			using var classKey = Registry.LocalMachine.OpenSubKey(DisplayAdapterClassKey);
			if (classKey == null) return result;

			foreach (var name in classKey.GetSubKeyNames())
			{
				// Entries are numbered 0000, 0001... Other subkeys (e.g. Configuration) aren't adapters.
				if (name.Length != 4 || !name.All(char.IsAsciiDigit)) continue;

				try
				{
					using var key = classKey.OpenSubKey(name);
					if (key == null) continue;

					var adapterName = (key.GetValue("HardwareInformation.AdapterString") as string
					                   ?? key.GetValue("DriverDesc") as string)?.Trim();
					if (string.IsNullOrEmpty(adapterName)) continue;

					var memory = key.GetValue("HardwareInformation.qwMemorySize") switch
					{
						long l => l,
						int i => (uint)i, // Older drivers store a 32-bit value.
						byte[] { Length: 8 } b => BitConverter.ToInt64(b),
						byte[] { Length: 4 } b => BitConverter.ToUInt32(b),
						_ => 0,
					};

					result[adapterName] = Math.Max(memory, result.GetValueOrDefault(adapterName));
				}
				catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
				{
					// Some entries aren't readable without admin rights. Skip them.
				}
			}
		}
		catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
		{
			Log.Warning($"Unable to read display adapters from the registry: {e.Message}");
		}

		return result;
	}

	[DllImport("dxgi.dll", EntryPoint = "CreateDXGIFactory1", ExactSpelling = true)]
	private static extern int CreateDXGIFactory1(Guid* riid, nint* factory);

	internal readonly record struct Adapter(string Name, long DedicatedMemory);

	[StructLayout(LayoutKind.Sequential)]
	private struct DXGI_ADAPTER_DESC1
	{
		public fixed char Description[128];
		public uint VendorId;
		public uint DeviceId;
		public uint SubSysId;
		public uint Revision;
		public nuint DedicatedVideoMemory;
		public nuint DedicatedSystemMemory;
		public nuint SharedSystemMemory;
		public long AdapterLuid;
		public uint Flags;
	}
}
