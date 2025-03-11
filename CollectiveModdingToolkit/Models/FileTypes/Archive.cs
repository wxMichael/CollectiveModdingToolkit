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

using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace CollectiveModdingToolkit.Models.FileTypes;

public enum FailReason : byte
{
	Unknown,
	FileNotFound,
	ReadFailed,
	WriteFailed,
}

public partial class Archive : ObservableObject, IFile
{
	public string Name { get; private set; }
	public string Ext { get; } = ".ba2";
	public string PathFull { get; private set; }
	public string PathRelative { get; private set; }

	public bool WasRead => IsReadable != null;
	[MemberNotNullWhen(true, nameof(ArchiveVersion), nameof(ArchiveFormat))]
	public bool? IsReadable { get; private set; }
	[ObservableProperty]
	public partial byte? ArchiveVersion { get; private set; }
	public string? ArchiveFormat { get; private set; }

	public int Priority { get; private set; }
	//[MemberNotNullWhen(true, nameof(Mod))]
	public bool IsManaged => Mod != null;
	public string? Mod { get; private set; }

	public bool? IsEnabled { get; set; }

	[ObservableProperty]
	public partial bool IsOG { get; private set; }
	[ObservableProperty]
	public partial bool IsNG { get; private set; }
	public bool IsGNRL => ArchiveFormat == "GNRL";
	public bool IsDX10 => ArchiveFormat == "DX10";

	[ObservableProperty]
	public partial bool? PatchSuccessful { get; private set; } = null;
	[ObservableProperty]
	public partial bool? PatchFailed { get; private set; } = null;
	[ObservableProperty]
	public partial FailReason? PatchFailReason { get; private set; } = null;


	// TODO: check if counted against limits (+ba2) when !IsReadable
	internal Archive(
		string name,
		string pathFull,
		string? pathRelative = null,
		int priority = 0,
		string? modName = null)
	{
		Name = name;
		PathFull = pathFull;
		PathRelative = pathRelative ?? name;
		Priority = priority == 0 ? 0 : priority + 1;
		Mod = modName;
		ReadHeader();
	}

	[MemberNotNull(nameof(IsReadable))]
	internal void ReadHeader()
	{
		byte[] header = new byte[12];
		int bytesRead;
		try
		{
			using FileStream file = File.OpenRead(PathFull);
			bytesRead = file.Read(header, 0, header.Length);
		}
		catch (IOException)
		{
			IsReadable = false;
			return;
		}

		if (bytesRead != header.Length || !header.AsSpan(0, 4).SequenceEqual(Magic.BTDX))
		{
			IsReadable = false;
			return;
		}
		ArchiveVersion = header[4];
		IsOG = ArchiveVersion == 1;
		IsNG = ArchiveVersion is 8 or 7;
		//if (ArchiveVersion is not 1 and not 7 and not 8)
		//	IsUnknown = true;

		if (header.AsSpan(8, 4).SequenceEqual(Magic.GNRL))
			ArchiveFormat = "GNRL";
		else if (header.AsSpan(8, 4).SequenceEqual(Magic.DX10))
			ArchiveFormat = "DX10";
		//else
		//	IsUnknown = true;

		IsReadable = true;
	}

	internal void SetVersion(byte version)
	{
		PatchSuccessful = false;
		PatchFailed = true;
		PatchFailReason = null;

		if (!File.Exists(PathFull))
		{
			PatchFailReason = FailReason.FileNotFound;
			return;
		}

		ReadHeader();

		if (!IsReadable.Value)
		{
			PatchFailReason = FailReason.ReadFailed;
			return;
		}


		if (version == ArchiveVersion)
		{
			PatchSuccessful = true;
			PatchFailed = false;
			return;
		}

		try
		{
			using FileStream file = File.OpenWrite(PathFull);
			file.Position = 4;
			file.Write(new ReadOnlySpan<byte>(ref version));
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			PatchFailReason = FailReason.WriteFailed;
			return;
		}
		ArchiveVersion = version;
		IsOG = ArchiveVersion == 1;
		IsNG = ArchiveVersion is 8 or 7;
		PatchSuccessful = true;
		PatchFailed = false;
	}
}
