using System.Diagnostics.CodeAnalysis;

namespace CollectiveModdingToolkit.Models.FileTypes;

internal interface IFile
{
	string Name { get; }
	string Ext { get; }

	/// <summary>
	/// A dot-prefixed, lowercase file name extension.
	/// </summary>
	string PathFull { get; }
	string PathRelative { get; }
	bool WasRead { get; }
	bool? IsReadable { get; }

	int Priority { get; }
	[MemberNotNullWhen(true, nameof(Mod))]
	bool IsManaged { get; }
	string? Mod { get; }
}
