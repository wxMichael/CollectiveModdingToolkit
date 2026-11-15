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
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace CollectiveModdingToolkit.Models.FileTypes;

public class Module : IFile
{
	internal Module(
		string name,
		string pathFull,
		string? pathRelative = null,
		string? ext = null,
		int priority = 0,
		string? modName = null)
	{
		Name = name;
		PathFull = pathFull;
		PathRelative = pathRelative ?? name;
		Ext = ext ?? Path.GetExtension(name).ToLowerInvariant();
		Priority = priority == 0 ? 0 : priority + 1;
		Mod = modName;
	}

	public bool? IsLight { get; private set; }
	public double HEDRVersion { get; private set; }
	public string OriginText => Origin?.DisplayName ?? "Unknown";

	public bool? IsEnabled { get; set; }
	public string Name { get; }
	public string Ext { get; }
	public string PathFull { get; }
	public string PathRelative { get; }

	public bool WasRead => IsReadable != null;

	[MemberNotNullWhen(true, nameof(IsLight))]
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

	internal void ReadHeader()
	{
		if (WasRead) throw new NotSupportedException("Module read twice");

		var header = new byte[34];
		int bytesRead;
		try
		{
			using var file = File.OpenRead(this.RealPath);
			bytesRead = file.ReadAtLeast(header, header.Length, false);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			IsReadable = false;
			return;
		}

		if (bytesRead != header.Length || !header.AsSpan(0, 4).SequenceEqual(Magic.TES4) ||
		    !header.AsSpan(24, 4).SequenceEqual(Magic.HEDR))
		{
			IsReadable = false;
			return;
		}

		HEDRVersion = Math.Round(BitConverter.ToSingle(header, 30), 2);
		//if (HEDRVersion != 1 && HEDRVersion != 0.95)
		//	IsUnknown = true;

		if (Name.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
		{
			IsLight = true;
		}
		else
		{
			var flags = BitConverter.ToInt32(header, 8);
			IsLight = (flags & Magic.Light) != 0;
		}

		IsReadable = true;
	}
}
