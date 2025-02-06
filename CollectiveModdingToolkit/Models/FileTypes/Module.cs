using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace CollectiveModdingToolkit.Models.FileTypes;

public class Module : IFile
{
	public string Name { get; private set; }
	public string Ext { get; private set; }
	public string PathFull { get; private set; }
	public string PathRelative { get; private set; }

	public bool WasRead => IsReadable != null;
	[MemberNotNullWhen(true, nameof(IsLight), nameof(HEDRVersion))]
	public bool? IsReadable { get; private set; }
	public bool? IsLight { get; private set; }
	public double? HEDRVersion { get; private set; }

	public int Priority { get; private set; }
	//[MemberNotNullWhen(true, nameof(Mod))]
	public bool IsManaged => Mod != null;
	public string? Mod { get; private set; }

	public bool? IsEnabled { get; set; }

	// TODO: check if counted against limits (+ba2) when !IsReadable
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
		Ext = ext ?? Path.GetExtension(name).ToLower();
		Priority = priority == 0 ? 0 : priority + 1;
		Mod = modName;
	}

	internal void ReadHeader()
	{
		if (WasRead)
			throw new NotSupportedException("Module read twice");

		byte[] header = new byte[34];
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

		if (bytesRead != header.Length || !header[0..4].SequenceEqual(Magic.TES4) || !header[24..28].SequenceEqual(Magic.HEDR))
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
			int flags = BitConverter.ToInt32(header, 8);
			IsLight = (flags & Magic.Light) != 0;
		}
		IsReadable = true;
	}
}
