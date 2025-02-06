using System.Text;

namespace CollectiveModdingToolkit.Models;

internal static class Magic
{
	private static readonly Encoding _codePage = Encoding.ASCII;

	internal static readonly byte[] TES4 = _codePage.GetBytes("TES4");
	internal static readonly byte[] BTDX = _codePage.GetBytes("BTDX");
	internal static readonly byte[] GNRL = _codePage.GetBytes("GNRL");
	internal static readonly byte[] DX10 = _codePage.GetBytes("DX10");
	internal static readonly byte[] HEDR = _codePage.GetBytes("HEDR");
	internal static readonly byte[] DDS = _codePage.GetBytes("DDS ");

	internal static readonly int Light = 0x0200;
}

internal static class GameVersion
{
	internal static string DownGrade = "Down-Grade";
	internal static string OldGen = "Old-Gen";
	internal static string NextGen = "Next-Gen";
	internal static string Unknown = "Unknown";
	internal static string NotFound = "Not Found";
	//internal static string NotFoundGood = "Not Found";
	//internal static string NotFoundBad = "Not Found";
}
