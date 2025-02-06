using System;

namespace CollectiveModdingToolkit.Models.FileTypes;
internal class DDS : IFile
{
	public string Name => throw new NotImplementedException();

	public string PathFull => throw new NotImplementedException();

	public string PathRelative => throw new NotImplementedException();

	public string Ext => ".dds";

	public bool? IsReadable => throw new NotImplementedException();

	public bool WasRead => throw new NotImplementedException();

	public int Priority => throw new NotImplementedException();

	public bool IsManaged => throw new NotImplementedException();

	public string? Mod => throw new NotImplementedException();
}
