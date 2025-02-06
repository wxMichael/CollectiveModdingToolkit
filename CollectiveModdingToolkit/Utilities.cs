using System;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;

namespace CollectiveModdingToolkit;

internal static class Utilities
{
	internal static string GetFileVersionOrHash(string filePath, bool hashOnly = false)
	{
		if (hashOnly)
			return $"{Crc32.HashToUInt32(File.ReadAllBytes(filePath).AsSpan()):X8}";
		FileVersionInfo version = FileVersionInfo.GetVersionInfo(filePath);
		string versionString = version.FileVersion == null
			? $"{Crc32.HashToUInt32(File.ReadAllBytes(filePath).AsSpan()):X8}"
			: $"{version.FileMajorPart}.{version.FileMinorPart}.{version.FileBuildPart}.{version.FilePrivatePart}";
		return versionString;
	}

	//internal static string? GetFileVersion(string filePath)
	//{
	//	FileVersionInfo version = FileVersionInfo.GetVersionInfo(filePath);
	//	return version.FileVersion == null
	//		? null
	//		: (string?)$"{version.FileMajorPart}.{version.FileMinorPart}.{version.FileBuildPart}.{version.FilePrivatePart}";
	//}

	//internal static string GetFileHash(string filePath) => $"{Crc32.HashToUInt32(File.ReadAllBytes(filePath).AsSpan()):X8}";
}
