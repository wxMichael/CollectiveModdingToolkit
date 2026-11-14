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
using System.Buffers.Binary;
using System.Linq;
using System.Text;

namespace CollectiveModdingToolkit.Models;

/// <summary>
///     Game executable versions packed the way F4SE packs them (<c>MAKE_EXE_VERSION</c>), as stored in a plugin's
///     compatible-versions list.
/// </summary>
internal static class F4SERuntime
{
	private static readonly uint V1_10_980 = Pack(1, 10, 980);
	private static readonly uint V1_10_984 = Pack(1, 10, 984);
	private static readonly uint V1_11_137 = Pack(1, 11, 137);

	internal static readonly uint[] NextGenVersions = [V1_10_980, V1_10_984];

	/// <summary>Every Anniversary Edition release. A plugin that lists any of these supports AE.</summary>
	internal static readonly uint[] AnniversaryVersions =
	[
		V1_11_137,
		Pack(1, 11, 159),
		Pack(1, 11, 169),
		Pack(1, 11, 191),
		Pack(1, 11, 221),
		Pack(1, 11, 240),
	];

	private static uint Pack(uint major, uint minor, uint build, uint sub = 0) => ((major & 0xFF) << 24) |
		((minor & 0xFF) << 16) | ((build & 0xFFF) << 4) | (sub & 0xF);

	/// <summary>Packs a dotted file version such as <c>1.11.240.0</c>. Returns null if it isn't one.</summary>
	internal static uint? Pack(string? version)
	{
		if (version == null || !Version.TryParse(version, out var v)) return null;

		return Pack((uint)v.Major, (uint)v.Minor, (uint)Math.Max(v.Build, 0), (uint)Math.Max(v.Revision, 0));
	}

	internal static bool IsNextGen(uint version) => version >= V1_10_980 && version <= V1_10_984;

	internal static bool IsAnniversary(uint version) => version >= V1_11_137;

	/// <summary>Before Next-Gen, plugins use the old <c>F4SEPlugin_Query</c> export and don't carry version data.</summary>
	internal static bool IsOldGen(uint version) => version < V1_10_980;
}

internal enum F4SEEdition
{
	NextGen,
	Anniversary,
}

/// <summary>
///     The <c>F4SEPlugin_Version</c> data a Next-Gen/Anniversary F4SE plugin exports (F4SE's <c>F4SEPluginVersionData</c>
///     ).
/// </summary>
internal sealed class F4SEPluginVersionData
{
	internal const int StructSize = 1116;
	private const uint kVersion = 1;

	private const uint kAddressIndependence_Signatures = 1 << 0;
	private const uint kAddressIndependence_AddressLibrary_1_10_980 = 1 << 1;
	private const uint kAddressIndependence_AddressLibrary_1_11_137 = 1 << 2;

	private const uint kStructureIndependence_NoStructs = 1 << 0;
	private const uint kStructureIndependence_1_10_980Layout = 1 << 1;
	private const uint kStructureIndependence_1_11_137Layout = 1 << 2;

	private uint DataVersion { get; init; }
	public uint PluginVersion { get; private init; }
	private string Name { get; init; } = string.Empty;
	public string Author { get; private init; } = string.Empty;
	private uint AddressIndependence { get; init; }
	private uint StructureIndependence { get; init; }
	private uint[] CompatibleVersions { get; init; } = [];
	public uint SeVersionRequired { get; private init; }
	private uint ReservedBreaking { get; init; }

	/// <summary>
	///     Whether F4SE would accept this data at all: it must be version 1 and name the plugin.
	///     Without that, F4SE disables the plugin regardless of the other fields.
	/// </summary>
	public bool IsValid => DataVersion == kVersion && Name.Length > 0;

	public bool SupportsNextGen => F4SERuntime.NextGenVersions.Any(SupportsRuntime);
	public bool SupportsAnniversary => F4SERuntime.AnniversaryVersions.Any(SupportsRuntime);

	/// <summary>Parses the raw struct. Returns null if <paramref name="data" /> is too short.</summary>
	public static F4SEPluginVersionData? Parse(ReadOnlySpan<byte> data)
	{
		if (data.Length < StructSize) return null;

		// Layout (packed, little-endian):
		//   0 dataVersion u32 | 4 pluginVersion u32 | 8 name[256] | 264 author[256]
		// 520 addressIndependence u32 | 524 structureIndependence u32 | 528 compatibleVersions[16] u32
		// 592 seVersionRequired u32 | 596 reservedNonBreaking u32 | 600 reservedBreaking u32 | 604 reserved[512]
		var compatible = new uint[16];
		for (var i = 0; i < compatible.Length; i++)
			compatible[i] = BinaryPrimitives.ReadUInt32LittleEndian(data[(528 + i * 4)..]);

		return new F4SEPluginVersionData
		{
			DataVersion = BinaryPrimitives.ReadUInt32LittleEndian(data),
			PluginVersion = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]),
			Name = ReadAsciiZ(data.Slice(8, 256)),
			Author = ReadAsciiZ(data.Slice(264, 256)),
			AddressIndependence = BinaryPrimitives.ReadUInt32LittleEndian(data[520..]),
			StructureIndependence = BinaryPrimitives.ReadUInt32LittleEndian(data[524..]),
			CompatibleVersions = compatible,
			SeVersionRequired = BinaryPrimitives.ReadUInt32LittleEndian(data[592..]),
			ReservedBreaking = BinaryPrimitives.ReadUInt32LittleEndian(data[600..]),
		};
	}

	private static string ReadAsciiZ(ReadOnlySpan<byte> field)
	{
		var end = field.IndexOf((byte)0);
		return Encoding.Latin1.GetString(end < 0 ? field : field[..end]);
	}

	private bool HasAddressIndependence(F4SEEdition edition) =>
		(AddressIndependence & kAddressIndependence_Signatures) != 0
		|| (AddressIndependence & (edition == F4SEEdition.Anniversary
			? kAddressIndependence_AddressLibrary_1_11_137
			: kAddressIndependence_AddressLibrary_1_10_980)) != 0;

	private bool HasStructureIndependence(F4SEEdition edition) =>
		(StructureIndependence & kStructureIndependence_NoStructs) != 0
		|| (StructureIndependence & (edition == F4SEEdition.Anniversary
			? kStructureIndependence_1_11_137Layout
			: kStructureIndependence_1_10_980Layout)) != 0;

	/// <summary>
	///     Whether the plugin should keep working across updates of this edition because it finds addresses with the Address
	///     Library
	///     (or signatures) and doesn't depend on game struct layouts.
	/// </summary>
	public bool IsVersionIndependent(F4SEEdition edition) =>
		IsValid
		// Anything in the "breaking" field means a game update has broken compatibility, so independence can't be trusted.
		&& ReservedBreaking == 0
		&& HasAddressIndependence(edition)
		&& HasStructureIndependence(edition);

	/// <summary>
	///     Whether F4SE would load this plugin on a game with the given packed executable version.
	/// </summary>
	public bool SupportsRuntime(uint runtimeVersion)
	{
		if (!IsValid) return false;

		if (F4SERuntime.IsAnniversary(runtimeVersion))
		{
			if (IsVersionIndependent(F4SEEdition.Anniversary)) return true;
		}
		else if (F4SERuntime.IsNextGen(runtimeVersion) && IsVersionIndependent(F4SEEdition.NextGen))
		{
			return true;
		}

		// The list is zero-terminated. Anything after the first zero is not part of it.
		return CompatibleVersions.TakeWhile(v => v != 0).Contains(runtimeVersion);
	}
}
