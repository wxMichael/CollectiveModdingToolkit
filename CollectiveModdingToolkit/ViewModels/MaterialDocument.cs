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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CollectiveModdingToolkit.Materials;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectiveModdingToolkit.ViewModels;

/// <summary>A material open for editing: its values, whether they've changed, and how to save them.</summary>
public sealed partial class MaterialDocument : ObservableObject
{
	private readonly List<MaterialFieldViewModel> _fields;

	private MaterialDocument(string path, MaterialFile file, MaterialFormat sourceFormat)
	{
		Path = path;
		File = file;
		SourceFormat = sourceFormat;
		Version = (int)file.Version;
		_fields = [.. file.Fields.Select(f => new MaterialFieldViewModel(file, f, OnEdited))];
		RebuildGroups();
	}

	/// <summary>Where the material was read from, and where Save writes it.</summary>
	public string Path { get; private set; }

	public string FileName => System.IO.Path.GetFileName(Path);

	public MaterialFile File { get; }

	/// <summary>The format the file was in on disk. Saving always writes binary.</summary>
	public MaterialFormat SourceFormat { get; }

	/// <summary>The sections to show, holding only the fields the file's version stores.</summary>
	[ObservableProperty]
	public partial IReadOnlyList<MaterialGroupViewModel> Groups { get; private set; } = [];

	[ObservableProperty] public partial bool IsDirty { get; private set; }

	/// <summary>Every version the editor can write.</summary>
	public static IReadOnlyList<int> VersionOptions { get; } =
		[.. Enumerable.Range(0, (int)MaterialFile.MaxSupportedVersion + 1)];

	[ObservableProperty] public partial int Version { get; set; }

	public string TypeName => File.Signature;

	public bool IsFallout4Format => File.IsFallout4Format;

	/// <summary>A line about what the file is, e.g. "BGSM v2 (Fallout 4)".</summary>
	public string Description =>
		$"{File.Signature} v{File.Version} ({(File.IsFallout4Format ? "Fallout 4" : "Fallout 76")} format)"
		+ (SourceFormat == MaterialFormat.Json ? ", read from JSON" : string.Empty);

	/// <summary>An in-memory sample material for the XAML previewer.</summary>
	internal static MaterialDocument CreateDesignSample() =>
		new(@"C:\Games\Fallout 4\Data\Materials\Armor\CombatArmor\CombatArmor.bgsm", new Bgsm
		{
			Version = 2,
			TileU = true,
			TileV = true,
			UScale = 1,
			VScale = 1,
			Alpha = 1,
			ZBufferWrite = true,
			ZBufferTest = true,
			DiffuseTexture = @"Armor\CombatArmor\CombatArmor_d.dds",
			NormalTexture = @"Armor\CombatArmor\CombatArmor_n.dds",
			SmoothSpecTexture = @"Armor\CombatArmor\CombatArmor_s.dds",
			SpecularEnabled = true,
			SpecularMult = 1.5f,
			Smoothness = 0.8f,
			RimLighting = true,
			CastShadows = true,
			ReceiveShadows = true,
		}, MaterialFormat.Binary);

	/// <summary>Reads a material from disk.</summary>
	/// <exception cref="InvalidDataException">The file isn't a valid material.</exception>
	/// <exception cref="IOException">The file can't be read.</exception>
	public static MaterialDocument Load(string path)
	{
		var data = System.IO.File.ReadAllBytes(path);
		var format = MaterialLoader.Inspect(data).Format;
		return new MaterialDocument(path, MaterialLoader.Parse(data, System.IO.Path.GetFileName(path)), format);
	}

	/// <summary>Writes the material as binary, in place so hard links and virtual file systems stay intact.</summary>
	/// <param name="path">Where to write, or null for <see cref="Path" />.</param>
	public void Save(string? path = null)
	{
		// Serialize first so a failure can't leave a half-written file.
		var bytes = File.Write();
		var target = path ?? Path;
		System.IO.File.WriteAllBytes(target, bytes);
		Path = target;
		OnPropertyChanged(nameof(Path));
		OnPropertyChanged(nameof(FileName));
		IsDirty = false;
	}

	/// <summary>Switches to a Fallout 4 version, which drops values only newer versions store.</summary>
	public void ConvertToFallout4()
	{
		Version = (int)MaterialFile.MaxFallout4Version;
	}

	partial void OnVersionChanged(int value)
	{
		var version = (uint)value;
		if (version == File.Version) return;

		File.Version = version;
		foreach (var field in _fields) field.Refresh();

		RebuildGroups();
		OnPropertyChanged(nameof(Description));
		OnPropertyChanged(nameof(IsFallout4Format));
		IsDirty = true;
	}

	private void OnEdited() => IsDirty = true;

	private void RebuildGroups() =>
		Groups =
		[
			.. _fields.Where(f => f.AppliesToVersion)
				.GroupBy(f => f.Group)
				.Select(g => new MaterialGroupViewModel(g.Key, [.. g])),
		];
}
