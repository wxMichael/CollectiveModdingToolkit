using System.Diagnostics.CodeAnalysis;

namespace CollectiveModdingToolkit.Models;

internal interface IFile
{
	string Name { get; }

	/// <summary>
	/// A dot-prefixed, lowercase file name extension.
	/// </summary>
	string PathFull { get; }
	string PathRelative { get; }
	string Ext { get; }
	bool? IsReadable { get; }

	bool WasRead { get; }

	int Priority { get; }
	[MemberNotNullWhen(true, nameof(Mod))]
	bool IsManaged { get; }
	string? Mod { get; }

}
