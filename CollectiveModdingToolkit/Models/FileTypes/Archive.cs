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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

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

	[ObservableProperty] public partial byte? ArchiveVersion { get; private set; }
	public string? ArchiveFormat { get; private set; }
	public string OriginText => Origin?.DisplayName ?? "Unknown";

	/// <summary>
	///     The real file on disk: the manager's source file for mod archives (correct with or without MO2's VFS),
	///     otherwise the game-folder path.
	/// </summary>
	internal string RealPath => Origin?.SourcePath ?? PathFull;

	public bool? IsEnabled { get; set; }

	[ObservableProperty] public partial bool IsOG { get; private set; }
	[ObservableProperty] public partial bool IsNG { get; private set; }
	public bool IsGNRL => ArchiveFormat == "GNRL";
	public bool IsDX10 => ArchiveFormat == "DX10";

	[ObservableProperty] public partial bool? PatchSuccessful { get; private set; } = null;
	[ObservableProperty] public partial bool? PatchFailed { get; private set; } = null;
	[ObservableProperty] public partial FailReason? PatchFailReason { get; private set; } = null;
	public string Name { get; }
	public string Ext => ".ba2";
	public string PathFull { get; }
	public string PathRelative { get; }

	public bool WasRead => IsReadable != null;

	[MemberNotNullWhen(true, nameof(ArchiveVersion), nameof(ArchiveFormat))]
	public bool? IsReadable { get; private set; }

	public int Priority { get; private set; }

	//[MemberNotNullWhen(true, nameof(Mod))]
	public bool IsManaged => Mod != null;
	public string? Mod { get; private set; }

	public FileOrigin? Origin { get; private set; }

	/// <summary>
	///     Patching runs on a background thread, but these properties are bound to the UI, so notifications raised
	///     off the UI thread are posted over to it.
	/// </summary>
	protected override void OnPropertyChanged(PropertyChangedEventArgs e)
	{
		if (Dispatcher.UIThread.CheckAccess())
			base.OnPropertyChanged(e);
		else
			Dispatcher.UIThread.Post(() => base.OnPropertyChanged(e));
	}

	internal void SetOrigin(FileOrigin? origin)
	{
		Origin = origin;
		Mod = origin is { IsGameFolder: false } ? origin.ModName : null;
		Priority = origin?.Priority ?? 0;

		// The constructor read PathFull; if the real file lives elsewhere (mod folder), read that instead.
		if (origin != null && !string.Equals(origin.SourcePath, PathFull, StringComparison.OrdinalIgnoreCase))
			ReadHeader();
	}

	[MemberNotNull(nameof(IsReadable))]
	private void ReadHeader()
	{
		var header = new byte[12];
		int bytesRead;
		try
		{
			using var file = File.OpenRead(RealPath);
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
		else if (header.AsSpan(8, 4).SequenceEqual(Magic.DX10)) ArchiveFormat = "DX10";
		//else
		//	IsUnknown = true;

		IsReadable = true;
	}

	private static void WriteVersion(string path, byte version)
	{
		using var file = File.OpenWrite(path);
		file.Position = 4;
		file.Write(new ReadOnlySpan<byte>(ref version));
	}

	private static int ReadVersionByte(string path)
	{
		using var file = File.OpenRead(path);
		file.Position = 4;
		return file.ReadByte();
	}

	internal void SetVersion(byte version)
	{
		PatchSuccessful = false;
		PatchFailed = true;
		PatchFailReason = null;

		if (!File.Exists(RealPath))
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
			WriteVersion(RealPath, version);

			// Vortex: if deployment copied the file instead of hard-linking it, the game-folder copy is separate data and must match.
			var deployed = Origin?.DeployedPath;
			if (deployed != null && File.Exists(deployed) && ReadVersionByte(deployed) != version)
				WriteVersion(deployed, version);
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
