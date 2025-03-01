using System;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;

namespace CollectiveModdingToolkit;

internal static class Utilities
{
	internal static string GetFileHash(string filePath, bool skipBA2Header = false) => $"{Crc32.HashToUInt32(File.ReadAllBytes(filePath).AsSpan(skipBA2Header ? 12 : 0)):X8}";

	internal static string? GetFileVersion(string filePath, bool skipPrivate = false)
	{
		FileVersionInfo version = FileVersionInfo.GetVersionInfo(filePath);
		string privatePart = skipPrivate ? string.Empty : $".{version.FilePrivatePart}";
		return version.FileVersion == null
			? null
			: $"{version.FileMajorPart}.{version.FileMinorPart}.{version.FileBuildPart}{privatePart}";
	}

	internal static string GetFileVersionOrHash(string filePath, bool hashOnly = false) => hashOnly ? GetFileHash(filePath) : GetFileVersion(filePath) ?? GetFileHash(filePath);

	internal static void OpenURL(string url) => Process.Start(new ProcessStartInfo()
	{
		FileName = "explorer.exe",
		ArgumentList = { url },
		UseShellExecute = true,
	});


}
