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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace CollectiveModdingToolkit.Models.FileTypes;

/// <summary>What the F4SE tab shows for one DLL in one column.</summary>
public enum DllMark
{
	/// <summary>Not supported.</summary>
	Bad,

	/// <summary>Supported.</summary>
	Good,

	/// <summary>Supports this edition regardless of game version (Address Library or signatures).</summary>
	Indie,

	/// <summary>Supports this edition, but not the version the game is running.</summary>
	Note,

	/// <summary>Not an F4SE plugin, or couldn't be read.</summary>
	Unknown,
}

public static class DllMarkExtensions
{
	extension(DllMark mark)
	{
		/// <summary>The symbol the F4SE tab shows for this mark.</summary>
		public string Symbol()
		{
			return mark switch
			{
				DllMark.Good => "✔",
				DllMark.Indie => "✱",
				DllMark.Note => "⚠️",
				DllMark.Unknown => "❓",
				_ => "❌",
			};
		}

		/// <summary>Ordering for column sorting: best support first.</summary>
		public int SortRank()
		{
			return mark switch
			{
				DllMark.Good => 0,
				DllMark.Indie => 1,
				DllMark.Note => 2,
				DllMark.Bad => 3,
				_ => 4,
			};
		}

		/// <summary>The color that goes with this mark.</summary>
		public StatusLevel Level()
		{
			return mark switch
			{
				DllMark.Good => StatusLevel.Good,
				DllMark.Indie => StatusLevel.Blue,
				DllMark.Note => StatusLevel.Caution,
				DllMark.Unknown => StatusLevel.Neutral,
				_ => StatusLevel.Bad,
			};
		}
	}
}

public class DLL : IFile
{
	private const string ExportLoad = "F4SEPlugin_Load";
	private const string ExportPreload = "F4SEPlugin_Preload";
	private const string ExportQuery = "F4SEPlugin_Query";
	private const string ExportVersion = "F4SEPlugin_Version";

	internal DLL(
		string name,
		string pathFull,
		string? pathRelative = null,
		int priority = 0,
		string? modName = null)
	{
		Name = name;
		PathFull = pathFull;
		PathRelative = pathRelative ?? $"F4SE/Plugins/{name}";
		Priority = priority == 0 ? 0 : priority + 1;
		Mod = modName;
	}

	public string OriginText => Origin?.DisplayName ?? "Unknown";

	/// <summary>Exports an F4SE entry point (<c>F4SEPlugin_Load</c> or <c>F4SEPlugin_Preload</c>).</summary>
	private bool IsF4SE { get; set; }

	/// <summary>Old-Gen: exports <c>F4SEPlugin_Query</c>.</summary>
	private bool SupportsOG { get; set; }

	/// <summary>Next-Gen/Anniversary: exports <c>F4SEPlugin_Version</c>, which says which game versions it works with.</summary>
	public bool HasVersionData => VersionData != null;

	private F4SEPluginVersionData? VersionData { get; set; }

	// private bool SupportsNG => VersionData?.SupportsNextGen == true;
	// private bool SupportsAE => VersionData?.SupportsAnniversary == true;

	/// <summary>Whether this plugin supports the game version that is installed.</summary>
	private bool SupportsCurrent { get; set; }

	private DllMark OGMark { get; set; } = DllMark.Unknown;
	private DllMark NGMark { get; set; } = DllMark.Unknown;
	private DllMark AEMark { get; set; } = DllMark.Unknown;
	private DllMark CurrentMark { get; set; } = DllMark.Unknown;

	public string OGText => OGMark.Symbol();
	public string NGText => NGMark.Symbol();
	public string AEText => AEMark.Symbol();
	public string CurrentText => CurrentMark.Symbol();

	// Sort keys: best support first (Good, Indie, Note, Bad, Unknown).
	public int OGSort => OGMark.SortRank();
	public int NGSort => NGMark.SortRank();
	public int AESort => AEMark.SortRank();
	public int CurrentSort => CurrentMark.SortRank();

	public StatusLevel OGLevel => OGMark.Level();
	public StatusLevel NGLevel => NGMark.Level();
	public StatusLevel AELevel => AEMark.Level();
	public StatusLevel CurrentLevel => CurrentMark.Level();

	public string Name { get; }
	public string Ext => ".dll";
	public string PathFull { get; }
	public string PathRelative { get; }

	public bool WasRead => IsReadable != null;
	public bool? IsReadable { get; private set; }

	public int Priority { get; private set; }

	[MemberNotNullWhen(true, nameof(Mod))] public bool IsManaged => Mod != null;

	public string? Mod { get; private set; }

	public FileOrigin? Origin { get; private set; }

	internal void SetOrigin(FileOrigin? origin)
	{
		Origin = origin;
		Mod = origin is { IsGameFolder: false } ? origin.ModName : null;
		Priority = origin?.Priority ?? 0;
	}

	/// <summary>
	///     Reads the DLL's exports (and, for Next-Gen/Anniversary plugins, its version data) without loading it.
	/// </summary>
	/// <param name="gameExeVersion">
	///     Packed version of Fallout4.exe (see <see cref="F4SERuntime.Pack(string?)" />), or null if unknown.
	/// </param>
	internal void Read(uint? gameExeVersion)
	{
		if (WasRead) throw new NotSupportedException("DLL read twice");

		try
		{
			var exports = PeExportTable.Load(this.RealPath);
			IsF4SE = exports.Has(ExportLoad) || exports.Has(ExportPreload);
			SupportsOG = exports.Has(ExportQuery);

			if (IsF4SE && exports.Has(ExportVersion))
			{
				var data = exports.ReadExportData(ExportVersion, F4SEPluginVersionData.StructSize);
				VersionData = data == null ? null : F4SEPluginVersionData.Parse(data);
				if (VersionData == null) Debug.WriteLine($"{Name}: {ExportVersion} data is unreadable.");
			}

			IsReadable = true;
		}
		catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"{Name}: unable to read: {e.Message}");
			IsReadable = false;
			return;
		}

		SetMarks(gameExeVersion);
	}

	private void SetMarks(uint? exeVersion)
	{
		if (!IsF4SE)
		{
			OGMark = NGMark = AEMark = CurrentMark = DllMark.Unknown;
			return;
		}

		OGMark = SupportsOG ? DllMark.Good : DllMark.Bad;

		// Before Next-Gen there is no version data to inspect. F4SE only asks the plugin (Query), so the export is all we can know.
		var oldGen = exeVersion is { } v0 && F4SERuntime.IsOldGen(v0);
		SupportsCurrent = exeVersion is { } v
		                  && (oldGen ? SupportsOG : VersionData?.SupportsRuntime(v) == true);

		if (VersionData is { } data)
		{
			NGMark = Mark(data.IsVersionIndependent(F4SEEdition.NextGen), data.SupportsNextGen);
			AEMark = Mark(data.IsVersionIndependent(F4SEEdition.Anniversary), data.SupportsAnniversary);
		}
		else
		{
			NGMark = AEMark = DllMark.Bad;
		}

		CurrentMark = exeVersion == null
			? DllMark.Unknown
			: SupportsCurrent
				? DllMark.Good
				: DllMark.Bad;
		return;

		DllMark Mark(bool independent, bool supported) =>
			independent ? DllMark.Indie
			: supported ? SupportsCurrent ? DllMark.Good : DllMark.Note
			: DllMark.Bad;
	}
}
