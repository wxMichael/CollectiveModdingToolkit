using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace CollectiveModdingToolkit.Models.FileTypes;

public class Archive : IFile
{
	public string Name { get; private set; }
	public string Ext { get; } = ".ba2";
	public string PathFull { get; private set; }
	public string PathRelative { get; private set; }

	public bool WasRead => IsReadable != null;
	[MemberNotNullWhen(true, nameof(ArchiveVersion), nameof(ArchiveFormat))]
	public bool? IsReadable { get; private set; }
	public byte? ArchiveVersion { get; private set; }
	public string? ArchiveFormat { get; private set; }

	public int Priority { get; private set; }
	//[MemberNotNullWhen(true, nameof(Mod))]
	public bool IsManaged => Mod != null;
	public string? Mod { get; private set; }

	public bool? IsEnabled { get; set; }

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
	}

	internal void ReadHeader()
	{

		if (WasRead)
			throw new NotSupportedException("Module read twice");

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
}
