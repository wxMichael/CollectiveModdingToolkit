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
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectiveModdingToolkit.Models;

public enum PathType
{
	File,
	Directory,
}

public enum Severity
{
	Unknown,
	NoImpact,
	Low,
	Medium,
	Critical,
}

public enum ProblemType
{
	Unknown,
	JunkFile,
	UnexpectedFormat,
	LoosePrevis,
	AnimTextDataFolder,
	InvalidArchive,
	InvalidModule,
	InvalidArchiveName,
	F4SEOverride,
	FileNotFound,
	WrongVersion,
	LimitExceeded,
	NoModManager,
	UnknownGameVersion,
	RaceSubgraphs,
	InvalidTexture,
	EmptyFile,
	Configuration,
	ScanError,
}

/// <summary>What kind of fix a problem suggests. The text itself is in <see cref="ProblemInfo.Solution" />.</summary>
public enum SolutionType
{
	None,
	ArchiveFolder,
	DeleteOrIgnoreFile,
	DeleteOrIgnoreFolder,
	ConvertDeleteOrIgnoreFile,
	RenameArchive,
	DownloadMod,
	VerifyFiles,
	UnknownFormat,
}

/// <summary>
///     One thing worth the user's attention: a bad file, a count over a game limit, a missing requirement.
///     Found by the Overview checks and (later) the Scanner, and shown with the same UI.
/// </summary>
public class ProblemInfo : ObservableObject
{
	public ProblemInfo(
		ProblemType type,
		Severity severity,
		string summary,
		string? fullPath = null,
		string? basePath = null,
		string? mod = null,
		string? solution = null,
		SolutionType solutionType = SolutionType.None,
		string? url = null,
		Collection<string>? fileList = null)
	{
		Type = type;
		Problem = Describe(type);
		Severity = severity;
		Summary = summary;
		Mod = mod;
		Solution = solution;
		SolutionType = solutionType;
		Url = url;
		FileList = fileList;

		if (!string.IsNullOrEmpty(fullPath))
		{
			FullPath = fullPath;
			BasePath = basePath ?? string.Empty;
			FileName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			RelativePath = string.IsNullOrEmpty(basePath) ? FileName : MakeRelative(basePath, fullPath);
			PathType = Directory.Exists(fullPath) ? PathType.Directory : PathType.File;
		}
	}

	/// <summary>Creates a problem from just a file's details. Used by the scanner's sample data.</summary>
	public ProblemInfo(string fileName, string fullPath, string basePath, PathType pathType, string problem,
		Severity severity, string? mod = null, string? solution = null, Collection<string>? fileList = null)
	{
		Type = ProblemType.Unknown;
		FileName = fileName;
		FullPath = fullPath;
		BasePath = basePath;
		RelativePath = MakeRelative(basePath, fullPath);
		PathType = pathType;
		Problem = problem;
		Summary = problem;
		Severity = severity;
		Mod = mod;
		Solution = solution;
		FileList = fileList;
	}

	public ProblemType Type { get; private set; }

	/// <summary>Short name of the kind of problem, e.g. "File Not Found".</summary>
	public string Problem { get; }

	/// <summary>Longer explanation of this specific problem.</summary>
	public string Summary { get; private set; }

	public string FileName { get; } = string.Empty;
	public string FullPath { get; } = string.Empty;
	public string BasePath { get; private set; } = string.Empty;
	public string RelativePath { get; private set; } = string.Empty;
	public PathType PathType { get; private set; }

	/// <summary>The mod the file comes from. Null means unmanaged, the game folder, or not applicable.</summary>
	public string? Mod { get; private set; }

	/// <summary>What the user can do about it.</summary>
	public string? Solution { get; private set; }

	public SolutionType SolutionType { get; private set; }

	/// <summary>A web page that helps with the solution, e.g. a mod to download.</summary>
	public string? Url { get; private set; }

	/// <summary>Extra rows for problems that are about many files at once.</summary>
	public Collection<string>? FileList { get; private set; }

	public Severity Severity { get; }

	/// <summary>
	///     Overrides <see cref="DisplayName" /> for problems that are about many files, e.g. "212 SADD Records from 9
	///     modules".
	/// </summary>
	public string? Title { get; init; }

	/// <summary>
	///     A shorter name for lists, when <see cref="DisplayName" /> is long, e.g. the file name of a file inside an
	///     archive.
	/// </summary>
	public string? ListTitle { get; init; }

	/// <summary>What to call this problem in a list: the title, else the file name, else the problem kind.</summary>
	public string DisplayName => Title ?? (FileName.Length > 0 ? FileName : Problem);

	/// <summary>True if there's a file or folder to show in Explorer.</summary>
	public bool HasPath => FullPath.Length > 0;

	/// <summary>True if this is about a loose material file the Materials tab can open.</summary>
	public bool IsMaterialFile =>
		PathType == PathType.File && HasPath &&
		Path.GetExtension(FullPath) is { } ext &&
		(ext.Equals(".bgsm", StringComparison.OrdinalIgnoreCase) ||
		 ext.Equals(".bgem", StringComparison.OrdinalIgnoreCase) ||
		 ext.Equals(".json", StringComparison.OrdinalIgnoreCase));

	public bool IsCritical => Severity == Severity.Critical;
	public bool IsWarning => Severity is Severity.Medium or Severity.Low;
	public bool IsInfo => Severity is Severity.NoImpact or Severity.Unknown;

	/// <summary>How to color this problem: red when critical, yellow when minor, neutral when informational.</summary>
	public StatusLevel Level => IsCritical ? StatusLevel.Bad : IsWarning ? StatusLevel.Caution : StatusLevel.Neutral;

	/// <summary>Symbol for the severity, to show beside the problem.</summary>
	public string SeverityMark => Severity switch
	{
		Severity.Critical => "❌",
		Severity.Medium or Severity.Low => "⚠️",
		_ => "ℹ️",
	};

	/// <summary>
	///     Path relative to <paramref name="basePath" />, or the full path if it lies outside it (avoids "..\..\"
	///     chains).
	/// </summary>
	private static string MakeRelative(string basePath, string fullPath)
	{
		var relative = Path.GetRelativePath(basePath, fullPath);
		return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? fullPath : relative;
	}

	/// <summary>Opens Explorer at the file or folder this problem is about.</summary>
	public bool Reveal() => HasPath && Utilities.RevealInExplorer(FullPath);

	public static string Describe(ProblemType type)
	{
		return type switch
		{
			ProblemType.JunkFile => "Junk File",
			ProblemType.UnexpectedFormat => "Unexpected Format",
			ProblemType.LoosePrevis => "Loose Previs",
			ProblemType.AnimTextDataFolder => "Loose AnimTextData",
			ProblemType.InvalidArchive => "Invalid Archive",
			ProblemType.InvalidModule => "Invalid Module",
			ProblemType.InvalidArchiveName => "Invalid Archive Name",
			ProblemType.F4SEOverride => "F4SE Script Override",
			ProblemType.FileNotFound => "File Not Found",
			ProblemType.WrongVersion => "Wrong Version",
			ProblemType.LimitExceeded => "Limit Exceeded",
			ProblemType.NoModManager => "No Mod Manager",
			ProblemType.UnknownGameVersion => "Unknown Game Version",
			ProblemType.RaceSubgraphs => "Race Subgraph Record Count",
			ProblemType.InvalidTexture => "Texture Issue",
			ProblemType.EmptyFile => "Empty File",
			ProblemType.Configuration => "Configuration",
			ProblemType.ScanError => "Scan Error",
			_ => "Problem",
		};
	}
}
