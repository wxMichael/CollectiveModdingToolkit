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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Threading;
using CollectiveModdingToolkit.Managers;
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Models.FileTypes;

namespace CollectiveModdingToolkit.Services;

public sealed record ScanOptions(
	bool OverviewIssues,
	bool WrongFormat,
	bool LoosePrevis,
	bool JunkFiles,
	bool ProblemOverrides,
	bool RaceSubgraphs,
	bool TextureIssues,
	bool EmptyFiles,
	bool MaterialIssues,
	bool BestPractices,
	bool ReadArchives)
{
	/// <summary>True if any option needs the Data folder walked. Overview Issues and Race Subgraphs don't.</summary>
	public bool NeedsFileWalk =>
		WrongFormat || LoosePrevis || JunkFiles || ProblemOverrides || TextureIssues || EmptyFiles ||
		MaterialIssues || BestPractices;

	public bool Any => OverviewIssues || NeedsFileWalk || RaceSubgraphs;

	public static ScanOptions FromSettings(AppSettings s) =>
		new(
			s.ScanOverviewIssues, s.ScanWrongFormat, s.ScanLoosePrevis, s.ScanJunkFiles, s.ScanProblemOverrides,
			s.ScanRaceSubgraphs, s.ScanTextureIssues, s.ScanEmptyFiles,
			s.ScanMaterialIssues, s.ScanBestPractices, s.ScanReadArchives);
}

public static class Scanner
{
	private const int RaceSubgraphThreshold = 100;

	public const string InfoRaceSubgraphs =
		"Counts race animation subgraph records (RACE \\ SADD).\n"
		+ "Depending on your PC, adding too many of these may result in stutter when loading Cells.\n"
		+ "This issue needs more investigation as this may be mere correlation and not causation.";

	private const string SummaryPrevis =
		"Loose previs files should be archived so they only win conflicts according to their plugin's load order.\n"
		+ "Loose previs files are also not supported by PJM's Previs Scripts.";

	private const string EmptyFileSolution =
		"[i]This might be intentional[/i], such as disabling an HKX file, so investigation is needed.";

	/// <summary>Folders that are never scanned. Lowercase.</summary>
	private static readonly string[] IgnoreFolders = ["bodyslide", "fo4edit", "robco_patcher", "source"];

	/// <summary>
	///     Top-level Data folders that are scanned, with the file extensions allowed in them (null means any). Lowercase,
	///     no dot.
	/// </summary>
	private static readonly Dictionary<string, HashSet<string>?> DataWhitelist = new()
	{
		["f4se"] = null,
		["materials"] = ["bgem", "bgsm", "json", "txt"],
		["meshes"] =
		[
			"bto", "btr", "hko", "hkx", "hkx_back", "hkx_backup", "lst", "max", "nif", "obj", "sclp", "ssf", "tri",
			"txt", "xml",
		],
		["music"] = ["wav", "xwm"],
		["textures"] = ["dds"],
		["scripts"] = ["pex", "psc", "txt", "zip"],
		["sound"] = ["cdf", "fuz", "hkx", "lip", "wav", "xwm"],
		["vis"] = ["uvd"],
	};

	private static readonly HashSet<string> JunkFiles =
		[with(StringComparer.OrdinalIgnoreCase), "thumbs.db", "desktop.ini", ".ds_store"];

	private static readonly string[] JunkFileSuffixes = [".tmp", ".bak"];

	/// <summary>Formats that are often used by mistake, and what the game expects instead.</summary>
	private static readonly Dictionary<string, string[]> ProperFormats = new()
	{
		["bmp"] = ["dds"],
		["jpeg"] = ["dds"],
		["jpg"] = ["dds"],
		["png"] = ["dds"],
		["psd"] = ["dds"],
		["tga"] = ["dds"],
		["mp3"] = ["wav", "xwm"],
	};

	// TODO: Check if these have been removed from INIs.
	/// <summary>Archives from the game or Creation Kit, which never need a plugin.</summary>
	private static readonly HashSet<string> ArchiveNameWhitelist =
	[
		with(StringComparer.OrdinalIgnoreCase),
		"creationkit - shaders.ba2", "creationkit - textures.ba2",
		"fallout4 - animations.ba2", "fallout4 - interface.ba2", "fallout4 - materials.ba2",
		"fallout4 - meshes.ba2",
		"fallout4 - meshesextra.ba2", "fallout4 - misc.ba2", "fallout4 - nvflex.ba2", "fallout4 - shaders.ba2",
		"fallout4 - sounds.ba2", "fallout4 - startup.ba2", "fallout4 - texturespatch.ba2", "fallout4 - voices.ba2",
		.. Enumerable.Range(1, 9).Select(i => $"fallout4 - textures{i}.ba2"),
		.. Enumerable.Range(1, 16).Select(i => $"dlcultrahighresolution - textures{i:00}.ba2"),
	];

	private static string SolutionText(SolutionType type)
	{
		return type switch
		{
			SolutionType.ArchiveFolder => "These folders should be [accent]packed into BA2 archives[/accent].",
			SolutionType.DeleteOrIgnoreFile or SolutionType.DeleteOrIgnoreFolder =>
				"It can either be [accent]deleted or ignored[/accent].",
			SolutionType.ConvertDeleteOrIgnoreFile =>
				"This file may need to be [accent]converted[/accent] and relevant files [accent]updated for the new name[/accent].\nOtherwise it can likely be [accent]deleted or ignored[/accent].",
			SolutionType.RenameArchive =>
				"Archives [accent]must[/accent] be named the same as a plugin with an added suffix or added to an INI.",
			SolutionType.DownloadMod => "Download the mod here:",
			SolutionType.VerifyFiles =>
				"[accent]Verify files[/accent] with Steam or [accent]reinstall[/accent] the game.\nIf you downgraded the game you will need to do so again afterward.",
			SolutionType.UnknownFormat => "If this file type is expected here, please [accent]report it[/accent].",
			_ => string.Empty,
		};
	}

	public static List<ProblemInfo> Scan(Game game, ScanOptions options, IProgress<Progress>? progress = null,
		CancellationToken token = default)
	{
		List<ProblemInfo> problems = [];

		if (options.OverviewIssues) problems.AddRange(game.Problems);

		try
		{
			ScanFiles(game, options, problems, progress, token);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception e)
		{
			Log.Error("Scanner: scan failed", e);
			problems.Add(new ProblemInfo(
				ProblemType.ScanError,
				Severity.Critical,
				$"The scan [warning]failed[/warning] and results may be incomplete.\n{e.GetType().Name}: {e.Message}",
				solution: "Please [accent]report this error[/accent].") { Title = "Scanner" });
		}

		return problems;
	}

	private static void ScanFiles(Game game, ScanOptions options, List<ProblemInfo> problems,
		IProgress<Progress>? progress, CancellationToken token)
	{
		if (options.RaceSubgraphs)
		{
			progress?.Report(new Progress("Counting race subgraph records...", 1));
			ScanRaceSubgraphs(game, problems, token);
		}

		if (!options.NeedsFileWalk) return;

		if (!Directory.Exists(game.DataPath))
		{
			problems.Add(new ProblemInfo(
				ProblemType.FileNotFound,
				Severity.Critical,
				"The game's Data folder was not found, so its files can't be scanned.",
				game.DataPath,
				game.GamePath,
				solution: SolutionText(SolutionType.VerifyFiles),
				solutionType: SolutionType.VerifyFiles));
			return;
		}

		var manager = game.Manager;
		var skipDirectories = new HashSet<string>([.. IgnoreFolders, .. manager.SkipDirectories],
			StringComparer.OrdinalIgnoreCase);
		string[] skipSuffixes = [.. manager.SkipFileSuffixes];

		progress?.Report(new Progress("Building mod file index...", 1));
		var files = BuildIndex(game, manager, skipDirectories, skipSuffixes, token);
		string[] topFolders =
		[
			.. files.Values
				.Select(e => e.Relative.Split('\\'))
				.Where(p => p.Length > 1)
				.Select(p => p[0])
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Order(StringComparer.OrdinalIgnoreCase),
		];

		var context = new ScanContext(game, options, manager, problems, token) { Files = files };
		if (options.MaterialIssues || options.BestPractices)
			context.Textures = TextureLookup.Build(game, files, e => GetSourcePath(manager, e),
				text => progress?.Report(new Progress(text, 1)), token);

		var reportedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var archivesEnabled = new HashSet<string>(game.ArchivesEnabled, StringComparer.OrdinalIgnoreCase);
		var readArchives = options.ReadArchives &&
		                   (options.WrongFormat || options.JunkFiles || options.ProblemOverrides ||
		                    options.TextureIssues || options.EmptyFiles || options.MaterialIssues ||
		                    options.BestPractices);
		var currentTop = string.Empty;

		foreach (var entry in files.Values.OrderBy(e => e.Relative, StringComparer.OrdinalIgnoreCase))
		{
			token.ThrowIfCancellationRequested();
			var parts = entry.Relative.Split('\\');
			var dirs = parts[..^1];
			var root = dirs.Length > 0 ? dirs[0].ToLowerInvariant() : null;

			if (root != null && !root.Equals(currentTop, StringComparison.Ordinal))
			{
				currentTop = root;
				var index = Array.FindIndex(topFolders, f => f.Equals(root, StringComparison.OrdinalIgnoreCase)) + 1;
				progress?.Report(new Progress($"Scanning... {index}/{topFolders.Length}: {dirs[0]}",
					100.0 * index / Math.Max(1, topFolders.Length)));
			}

			ScanFile(context, entry, entry.Relative, name => files.ContainsKey(name), reportedFolders, null);

			// Only archives directly in Data are loaded by the game.
			var fileName = parts[^1];
			if (root != null || !fileName.EndsWith(".ba2", StringComparison.OrdinalIgnoreCase) ||
			    ArchiveNameWhitelist.Contains(fileName))
				continue;

			if (readArchives)
			{
				progress?.Report(new Progress($"Reading archive: {fileName}", 100.0));
				ScanArchive(context, entry);
			}

			if (!archivesEnabled.Contains(fileName)) CheckArchiveName(context, entry, fileName);
		}
	}

	private static void CheckArchiveName(ScanContext c, Entry entry, string fileName)
	{
		var stem = fileName[..fileName.LastIndexOf('.')];
		var split = stem.LastIndexOf(" - ", StringComparison.Ordinal);
		var suffix = split < 0 ? null : stem[(split + 3)..].ToLowerInvariant();
		if (suffix != null && c.Game.BA2Suffixes.Contains(suffix)) return;

		var baseName = split < 0 ? stem : stem[..split];
		AddFileProblem(c.Problems, c.Game, c.Manager, entry, ProblemType.InvalidArchiveName, Severity.Medium,
			"This is not a valid archive name and won't be loaded by the game.",
			SolutionType.RenameArchive,
			$"{SolutionText(SolutionType.RenameArchive)}\n\nValid Suffixes: {string.Join(", ", c.Game.BA2Suffixes)}\nExample: {baseName} - Main.ba2");
	}

	/// <summary>
	///     Applies the per-file checks to one file, loose or inside an archive.
	/// </summary>
	/// <param name="c">Scan state.</param>
	/// <param name="entry">The loose file, or the archive the file is in.</param>
	/// <param name="relative">Path relative to Data (or the mod folder), or inside the archive.</param>
	/// <param name="exists">Whether another file exists at a relative path, in the same place as this one.</param>
	/// <param name="reportedFolders">Folders already reported.</param>
	/// <param name="inner">The file inside <paramref name="entry" />, if it is an archive.</param>
	private static void ScanFile(ScanContext c, Entry entry, string relative, Func<string, bool> exists,
		HashSet<string> reportedFolders, Ba2Entry? inner)
	{
		var game = c.Game;
		var options = c.Options;
		var manager = c.Manager;
		var problems = c.Problems;
		var parts = relative.Split('\\');
		var fileName = parts[^1];
		var dirs = parts[..^1];
		var root = dirs.Length > 0 ? dirs[0].ToLowerInvariant() : null;
		var archived = inner != null;

		// Nothing else can be checked in a file with no contents, and the fomod folder is reported as a whole.
		if (options.EmptyFiles && root != "fomod" && !IsPlaceholder(fileName) && IsEmpty(entry, inner))
		{
			AddFileProblem(problems, game, manager, entry, ProblemType.EmptyFile, EmptyFileSeverity(fileName),
				"This file is [bad]empty[/bad] (0 bytes).",
				null, EmptyFileSolution, inner);
			return;
		}

		HashSet<string>? whitelist = null;
		if (root != null)
		{
			if (root == "fomod")
			{
				if (options.JunkFiles && reportedFolders.Add(archived ? $"{entry.FullPath}|fomod" : "fomod"))
					AddFolderProblem(problems, game, manager, entry, 1, ProblemType.JunkFile, Severity.Low,
						"This is a junk folder not used by the game or mod managers.",
						SolutionType.DeleteOrIgnoreFolder, archived ? dirs[0] : null);

				return;
			}

			if (!DataWhitelist.TryGetValue(root, out whitelist)) return;

			// Packed previs and AnimTextData are the fix for these problems, not the problem.
			if (!archived)
				switch (root)
				{
					case "vis" when options.LoosePrevis:
					{
						if (reportedFolders.Add("vis"))
							AddFolderProblem(problems, game, manager, entry, 1, ProblemType.LoosePrevis,
								Severity.Medium,
								SummaryPrevis, SolutionType.ArchiveFolder);

						return;
					}
					case "meshes"
						when IsInProblemFolder(options, game, manager, problems, reportedFolders, entry, dirs):
						return;
				}
		}

		if (options.JunkFiles && (JunkFiles.Contains(fileName) ||
		                          JunkFileSuffixes.Any(s =>
			                          fileName.EndsWith(s, StringComparison.OrdinalIgnoreCase))))
		{
			AddFileProblem(problems, game, manager, entry, ProblemType.JunkFile, Severity.Low,
				"This is a junk file not used by the game or mod managers.", SolutionType.DeleteOrIgnoreFile,
				inner: inner);
			return;
		}

		// Only scripts directly in Data\Scripts.
		if (root == "scripts" && dirs.Length == 1 && options.ProblemOverrides)
			if (F4SEScripts.TryGetHash(game.GameVersion, fileName, out var f4SEScriptCRC))
			{
				var mod = GetMod(manager, entry);

				var scriptCRC = GetScriptHash(c, entry, inner);
				if (scriptCRC == null) return;

				if (!scriptCRC.Equals(f4SEScriptCRC, StringComparison.OrdinalIgnoreCase))
					AddFileProblem(problems, game, manager, entry, ProblemType.F4SEOverride, Severity.Critical,
						"This is an [bad]override of an F4SE script[/bad] which does not match your game's version.",
						null,
						mod == null || mod.Contains("Fallout 4 Script Extender")
							? $"Install F4SE for {game.GameVersion}."
							: "Check if this mod is supposed to override F4SE Scripts.\n"
							  + "If this is a script extender/library or requires one, this is likely intentional but it must support your game version explicitly.\n"
							  + "Otherwise, this mod or file may need to be deleted.", inner);

				else if (mod != null && !mod.Contains("Fallout 4 Script Extender"))
					AddFileProblem(problems, game, manager, entry, ProblemType.F4SEOverride, Severity.Low,
						"This is an [warning]override of an F4SE script[/warning]. It matches your game's version so won't "
						+ "cause issues now, but if this is unintentionally bundled with a mod it may cause "
						+ "issues if your game or F4SE version ever changes.",
						null,
						"If this file isn't expected in this mod, [accent]inform the author[/accent] of the potential mistake",
						inner);

				return;
			}

		var dot = fileName.LastIndexOf('.');
		if (dot < 0) return;

		var ext = fileName[(dot + 1)..].ToLowerInvariant();
		var stem = fileName[..dot];

		if (options.TextureIssues && ext == "dds")
		{
			if (inner != null)
				CheckArchivedTexture(c, entry, inner);
			else
				CheckTexture(problems, game, manager, entry);
		}

		if ((options.MaterialIssues || options.BestPractices) && root == "materials" &&
		    ext is "bgsm" or "bgem" or "json")
			CheckMaterial(c, entry, relative, inner);

		if (!options.WrongFormat) return;

		var misplacedDll = ext == "dll" &&
		                   !relative.StartsWith(@"f4se\plugins", StringComparison.OrdinalIgnoreCase);
		if ((whitelist == null || whitelist.Contains(ext)) && !misplacedDll) return;

		var location = root ?? "Data";
		string summary;
		SolutionType solution;
		if (ProperFormats.TryGetValue(ext, out var proper))
		{
			var folder = dirs.Length > 0 ? string.Join('\\', dirs) + '\\' : string.Empty;
			string[] found =
			[
				.. proper.Where(e => exists($"{folder}{stem}.{e}"))
					.Select(e => $"{stem}.{e}"),
			];
			if (found.Length > 0)
			{
				summary =
					$"Format not in whitelist for {location}.\nA file with the expected format was found ({string.Join(", ", found)}).";
				solution = SolutionType.DeleteOrIgnoreFile;
			}
			else
			{
				summary =
					$"Format not in whitelist for {location}.\nA file with the expected format was NOT found ({string.Join(", ", proper)}).";
				solution = SolutionType.ConvertDeleteOrIgnoreFile;
			}
		}
		else
		{
			summary =
				$"Format not in whitelist for {location}.\nUnable to determine whether the game will use this file.";
			solution = SolutionType.UnknownFormat;
		}

		AddFileProblem(problems, game, manager, entry, ProblemType.UnexpectedFormat, Severity.Medium, summary,
			solution, inner: inner);
	}

	/// <summary>Empty files that exist on purpose: mod manager markers and dotfiles such as .gitkeep.</summary>
	private static bool IsPlaceholder(string fileName) =>
		fileName.StartsWith('.') ||
		fileName.StartsWith("__folder_managed_by_vortex", StringComparison.OrdinalIgnoreCase);

	/// <summary>Files the game loads at start-up or crashes on when empty, versus ones nothing reads.</summary>
	private static Severity EmptyFileSeverity(string fileName)
	{
		var ext = Path.GetExtension(fileName).ToLowerInvariant();
		return ext switch
		{
			"" => Severity.Low,
			".esp" or ".esm" or ".esl" or ".ba2" or ".dll" => Severity.Critical,
			".txt" or ".md" or ".log" or ".url" or ".ini" or ".cfg" or ".json" or ".xml" or ".pdf" or ".rtf" => Severity
				.Low,
			_ => Severity.Medium,
		};
	}

	/// <summary>Whether a loose file or a file inside an archive has no contents.</summary>
	private static bool IsEmpty(Entry entry, Ba2Entry? inner)
	{
		if (inner != null) return inner.Chunks.Length == 0 || inner.UnpackedSize == 0;

		if (entry.Length != 0) return false;

		// Links to files can report 0, so confirm by asking the file itself.
		try
		{
			using var handle = File.OpenHandle(entry.FullPath, FileMode.Open, FileAccess.Read,
				FileShare.ReadWrite | FileShare.Delete);
			return RandomAccess.GetLength(handle) == 0;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return false;
		}
	}

	/// <summary>The CRC of a script, loose or archived. Null if it can't be read.</summary>
	private static string? GetScriptHash(ScanContext c, Entry entry, Ba2Entry? inner)
	{
		if (inner == null) return Utilities.GetFileHash(entry.FullPath);

		const long maxScriptSize = 16 << 20;
		if (inner.UnpackedSize > maxScriptSize) return null;

		try
		{
			var data = Ba2Reader.ReadStart(GetSourcePath(c.Manager, entry), inner, (int)inner.UnpackedSize);
			return $"{Crc32.HashToUInt32(data):X8}";
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
		{
			return null;
		}
	}

	/// <summary>Reads a BA2's index and applies the per-file checks to everything in it.</summary>
	private static void ScanArchive(ScanContext c, Entry entry)
	{
		var path = GetSourcePath(c.Manager, entry);
		List<Ba2Entry> files;
		try
		{
			files = Ba2Reader.Read(path, c.Token);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
		{
			// Unsupported versions (e.g. Starfield's) are skipped quietly.
			if (e is InvalidDataException && e.Message.StartsWith("Unsupported", StringComparison.Ordinal)) return;

			Log.Warning($"Scanner: unable to read archive {path}: {e.Message}");
			AddFileProblem(c.Problems, c.Game, c.Manager, entry, ProblemType.InvalidArchive, Severity.Medium,
				$"This archive [warning]could not be read[/warning] to check its contents. {e.Message}", null,
				"[accent]Report it[/accent] to the mod author. The archive may be corrupted or incomplete.");
			return;
		}

		// Paths inside archives may use either slash.
		var names = new HashSet<string>(files.Select(f => f.Path.Replace('/', '\\')), StringComparer.OrdinalIgnoreCase);
		var reportedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var file in files)
		{
			c.Token.ThrowIfCancellationRequested();
			ScanFile(c, entry, file.Path.Replace('/', '\\'), names.Contains, reportedFolders, file);
		}
	}

	/// <summary>Reports problems with a material and the textures it uses.</summary>
	private static void CheckMaterial(ScanContext c, Entry entry, string relative, Ba2Entry? inner)
	{
		if (c.Textures == null) return;

		// Materials in an archive the game doesn't load are never used, and their textures may not be loaded either.
		if (inner != null && !c.Game.ArchivesEnabled.Contains(Path.GetFileName(entry.FullPath))) return;

		var path = GetSourcePath(c.Manager, entry);
		byte[] data;
		try
		{
			var length = inner?.UnpackedSize ?? new FileInfo(path).Length;
			if (length > MaterialChecker.MaxSize)
			{
				AddFileProblem(c.Problems, c.Game, c.Manager, entry, ProblemType.InvalidMaterial, Severity.Medium,
					$"This material is [bad]unusually large[/bad] ({length:N0} bytes). Materials are a few hundred bytes, so this is probably not a material.",
					null, "[accent]Report it[/accent] to the mod author.", inner);
				return;
			}

			data = inner == null ? File.ReadAllBytes(path) : Ba2Reader.ReadStart(path, inner, (int)length);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
		{
			Log.Warning($"Scanner: unable to read material {relative}: {e.Message}");
			return;
		}

		// Suggestions (Low) belong to Best Practices; everything else to Material Issues.
		foreach (var issue in MaterialChecker.Check(relative, data, c.Textures))
			if (issue.Severity == Severity.Low ? c.Options.BestPractices : c.Options.MaterialIssues)
				AddFileProblem(c.Problems, c.Game, c.Manager, entry, issue.Type, issue.Severity, issue.Summary, null,
					issue.Solution, inner);
	}

	/// <summary>Reports a DDS texture inside a BA2 with an invalid header, dimensions, format, or naming.</summary>
	private static void CheckArchivedTexture(ScanContext c, Entry entry, Ba2Entry file)
	{
		var path = GetSourcePath(c.Manager, entry);
		var relative = file.Path.Replace('/', '\\');
		DDS dds = new(Path.GetFileName(relative), path, null);
		long length;
		if (file.IsTexture)
		{
			var header = Ba2Reader.BuildDdsHeader(file);
			dds.ReadHeader(header);
			length = header.Length + file.UnpackedSize;
		}
		else
		{
			try
			{
				dds.ReadHeader(Ba2Reader.ReadStart(path, file, 148));
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
			{
				return;
			}

			length = file.UnpackedSize;
		}

		if (dds.IsReadable != true)
		{
			AddFileProblem(c.Problems, c.Game, c.Manager, entry, ProblemType.InvalidTexture, Severity.Medium,
				dds.Signature == null
					? "This texture's [accent]DDS header[/accent] is [bad]missing, corrupted, or unreadable[/bad]."
					: $"This file's header is [bad]invalid for DDS[/bad]. The signature read was [b]'{dds.Signature}'[/b].",
				null,
				"[accent]Report it[/accent] to the mod author. [accent]Resaving[/accent] the file [i]might[/i] repair it.",
				file);
			return;
		}

		foreach (var issue in TextureChecker.Check(dds, relative, length))
			AddFileProblem(c.Problems, c.Game, c.Manager, entry, ProblemType.InvalidTexture, issue.Severity,
				issue.Summary, null, issue.Solution, file);
	}

	/// <summary>Reports DDS textures with invalid headers, dimensions, formats, or naming.</summary>
	private static void CheckTexture(List<ProblemInfo> problems, Game game, IModManager manager, Entry entry)
	{
		var path = GetSourcePath(manager, entry);
		DDS dds = new(Path.GetFileName(path), path, null);
		dds.ReadHeader();

		if (dds.IsReadable != true)
		{
			AddFileProblem(problems, game, manager, entry, ProblemType.InvalidTexture, Severity.Medium,
				dds.Signature == null
					? "This texture's [accent]DDS header[/accent] is [bad]missing, corrupted, or unreadable[/bad]."
					: $"This file's header is [bad]invalid for DDS[/bad]. The signature read was [b]'{dds.Signature}'[/b].",
				null,
				"[accent]Report it[/accent] to the mod author. Resaving the file [i]might[/i] repair it.");

			return;
		}

		long length;
		try
		{
			length = new FileInfo(path).Length;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return;
		}

		// TODO: Check for use in Materials
		// TODO: Check for use in Meshes
		foreach (var issue in TextureChecker.Check(dds, entry.Relative, length))
			AddFileProblem(problems, game, manager, entry, ProblemType.InvalidTexture, issue.Severity, issue.Summary,
				null, issue.Solution);
	}

	/// <summary>
	///     Reports a loose precombined or AnimTextData folder under Meshes, once. True if the file is inside one and
	///     shouldn't be checked further.
	/// </summary>
	private static bool IsInProblemFolder(
		ScanOptions options, Game game, IModManager manager, List<ProblemInfo> problems, HashSet<string> reported,
		Entry entry, string[] dirs)
	{
		for (var i = 1; i < dirs.Length; i++)
		{
			var name = dirs[i];
			ProblemType type;
			string summary;
			Severity severity;
			if (options.LoosePrevis && name.Equals("precombined", StringComparison.OrdinalIgnoreCase))
			{
				type = ProblemType.LoosePrevis;
				summary = SummaryPrevis;
				severity = Severity.Medium;
			}
			else if (options.ProblemOverrides && name.Equals("animtextdata", StringComparison.OrdinalIgnoreCase))
			{
				type = ProblemType.AnimTextDataFolder;
				summary = "The existence of unpacked [accent]AnimTextData[/accent] is reported to cause crashes.";
				severity = Severity.Medium;
			}
			else
			{
				continue;
			}

			// Reported once per mod, as each mod has its own folder.
			if (reported.Add($"{entry.Root}|{string.Join('\\', dirs[..(i + 1)])}"))
				AddFolderProblem(problems, game, manager, entry, i + 1, type, severity, summary,
					SolutionType.ArchiveFolder);

			return true;
		}

		return false;
	}

	/// <summary>
	///     Merges the Data folder and every mod's files, later layers winning.
	/// </summary>
	private static Dictionary<string, Entry> BuildIndex(Game game, IModManager manager, HashSet<string> skipDirectories,
		string[] skipSuffixes, CancellationToken token) =>
		BuildIndex(game, manager, skipDirectories, skipSuffixes, null, token);

	/// <summary>
	///     Merges the winning loose files under one top-level Data folder, e.g. "Materials", for tools that don't need the
	///     whole scanner. Keys are paths relative to Data.
	/// </summary>
	internal static Dictionary<string, Entry> IndexFolder(Game game, string folder, CancellationToken token = default)
	{
		var manager = game.Manager;
		var skipDirectories = new HashSet<string>([.. IgnoreFolders, .. manager.SkipDirectories],
			StringComparer.OrdinalIgnoreCase);
		return BuildIndex(game, manager, skipDirectories, [.. manager.SkipFileSuffixes], folder, token);
	}

	private static Dictionary<string, Entry> BuildIndex(Game game, IModManager manager, HashSet<string> skipDirectories,
		string[] skipSuffixes, string? onlyUnder, CancellationToken token)
	{
		Dictionary<string, Entry> files = new(StringComparer.OrdinalIgnoreCase);

		// The Data folder is the lowest layer. Under Vortex it holds hard links to mod files; under MO2 it only holds game files
		// (or everything, if this app is running inside MO2's virtual file system, which the mod layers below then agree with).
		Walk(game.DataPath, null);

		// MO2: StagePaths is lowest priority first with Overwrite last.
		if (manager is not ModOrganizer) return files;
		foreach (var stage in manager.StagePaths)
			Walk(stage, Path.GetFileName(stage.TrimEnd('\\', '/')));

		return files;

		void Walk(string root, string? mod)
		{
			var start = onlyUnder == null ? root : Path.Combine(root, onlyUnder);
			if (!Directory.Exists(start)) return;

			Stack<string> pending = new();
			pending.Push(start);
			while (pending.Count > 0)
			{
				token.ThrowIfCancellationRequested();
				var folder = pending.Pop();
				IEnumerable<FileSystemInfo> children;
				try
				{
					children = [.. new DirectoryInfo(folder).EnumerateFileSystemInfos()];
				}
				catch (Exception e) when (e is IOException or UnauthorizedAccessException)
				{
					Log.Warning($"Scanner: unable to read {folder}: {e.Message}");
					continue;
				}

				foreach (var child in children)
				{
					var name = child.Name;
					if (child is DirectoryInfo)
					{
						if (!skipDirectories.Contains(name)) pending.Push(child.FullName);

						continue;
					}

					if (skipSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase))) continue;

					var relative = Path.GetRelativePath(root, child.FullName);
					files[relative] = new Entry(relative, child.FullName, mod, root, (child as FileInfo)?.Length ?? 0);
				}
			}
		}
	}

	/// <summary>The mod a file comes from, or null if it's a game-folder file or the manager can't say.</summary>
	internal static string? GetMod(IModManager manager, Entry entry)
	{
		if (entry.Mod != null) return entry.Mod;

		if (manager is ModOrganizer) return null;

		var origin = manager.Resolve(entry.FullPath);
		return origin is { IsGameFolder: false } ? origin.ModName ?? origin.DisplayName : null;
	}

	/// <summary>The path to show and open: the mod's real file, not the game-folder link.</summary>
	internal static string GetSourcePath(IModManager manager, Entry entry) =>
		entry.Mod != null || manager is ModOrganizer
			? entry.FullPath
			: manager.Resolve(entry.FullPath)?.SourcePath ?? entry.FullPath;

	/// <summary>The folder the file's relative path starts from: the mod's folder, or Data.</summary>
	private static string GetBasePath(Game game, Entry entry, string sourcePath) =>
		sourcePath.EndsWith(entry.Relative, StringComparison.OrdinalIgnoreCase) &&
		sourcePath.Length > entry.Relative.Length
			? sourcePath[..^entry.Relative.Length].TrimEnd('\\', '/')
			: game.DataPath;

	private static void AddFileProblem(
		List<ProblemInfo> problems, Game game, IModManager manager, Entry entry,
		ProblemType type, Severity severity, string summary, SolutionType? solutionType, string? solution = null,
		Ba2Entry? inner = null)
	{
		var path = GetSourcePath(manager, entry);
		var mod = GetMod(manager, entry);
		var basePath = GetBasePath(game, entry, path);
		problems.Add(new ProblemInfo(
			type, severity, summary, path, basePath, mod,
			solution ?? (solutionType is { } t ? SolutionText(t) : null),
			solutionType ?? SolutionType.None)
		{
			Title = inner?.Path, ListTitle = inner == null ? null : Path.GetFileName(inner.Path),
		});
	}

	/// <summary>
	///     Reports a folder, <paramref name="depth" /> levels below Data, found as the parent of <paramref name="entry" />.
	/// </summary>
	private static void AddFolderProblem(
		List<ProblemInfo> problems, Game game, IModManager manager, Entry entry, int depth,
		ProblemType type, Severity severity, string summary, SolutionType solutionType, string? innerFolder = null)
	{
		var parts = entry.Relative.Split('\\');
		var sourceFile = GetSourcePath(manager, entry);

		// Walk up from the file by the number of levels below the folder to get the real folder.
		var folder = sourceFile;
		for (var i = parts.Length; i > depth; i--) folder = Path.GetDirectoryName(folder)!;

		var mod = GetMod(manager, entry);
		var basePath = GetBasePath(game, entry, sourceFile);

		// Inside an archive the folder isn't on disk, so point at the archive and name the folder.
		if (innerFolder != null) folder = sourceFile;

		problems.Add(new ProblemInfo(type, severity, summary, folder, basePath, mod, SolutionText(solutionType),
			solutionType) { Title = innerFolder });
	}

	private static void ScanRaceSubgraphs(Game game, List<ProblemInfo> problems, CancellationToken token)
	{
		// TODO: Determine whether overrides matter and ignore them if not.

		byte[] pattern = [0x00, 0x53, 0x41, 0x44, 0x44]; // .SADD
		List<(int Count, string Name, string? Mod)> modules = [];
		var total = 0;

		foreach (var name in game.ModulesEnabled)
		{
			token.ThrowIfCancellationRequested();
			if (!game.ModulesWinning.TryGetValue(name, out var module)) continue;

			int count;
			try
			{
				count = Count(module.RealPath, pattern, token);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				continue;
			}

			if (count <= 0) continue;
			modules.Add((count, name, module.Origin?.DisplayName));
			total += count;
		}

		if (total <= RaceSubgraphThreshold) return;

		Collection<string> list =
		[
			.. modules.OrderByDescending(m => m.Count).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
				.Select(m => $"{m.Count,5}   {m.Name}{(m.Mod == null ? string.Empty : $"  ({m.Mod})")}"),
		];

		problems.Add(new ProblemInfo(
			ProblemType.RaceSubgraphs,
			Severity.NoImpact,
			InfoRaceSubgraphs,
			solution:
			"IF you are experiencing stutter when moving between cells, removing some of these mods could alleviate performance issues.\nMerging them may also reduce stutter.",
			fileList: list) { Title = $"{total} SADD Records from {modules.Count} modules" });
	}

	/// <summary>Counts non-overlapping occurrences of <paramref name="pattern" /> in a file, without loading it all.</summary>
	private static int Count(string path, byte[] pattern, CancellationToken token)
	{
		using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920);
		var buffer = new byte[1 << 20];
		var keep = pattern.Length - 1;
		var count = 0;
		var filled = 0;
		int read;
		while ((read = stream.Read(buffer, filled, buffer.Length - filled)) > 0)
		{
			token.ThrowIfCancellationRequested();
			var length = filled + read;
			ReadOnlySpan<byte> span = buffer.AsSpan(0, length);
			var start = 0;
			int index;
			while ((index = span[start..].IndexOf(pattern)) >= 0)
			{
				count++;
				start += index + pattern.Length;
			}

			// Carry over the tail that could be the start of a match split across reads. Skip bytes already counted.
			var tail = Math.Min(keep, length - start);
			Buffer.BlockCopy(buffer, length - tail, buffer, 0, tail);
			filled = tail;
		}

		return count;
	}

	private sealed record ScanContext(
		Game Game,
		ScanOptions Options,
		IModManager Manager,
		List<ProblemInfo> Problems,
		CancellationToken Token)
	{
		/// <summary>Every loose file the game would see, by path relative to Data.</summary>
		public Dictionary<string, Entry> Files { get; init; } = [];

		/// <summary>Textures for checking materials. Null unless material checks are on.</summary>
		public TextureLookup? Textures { get; set; }
	}

	/// <summary>One file as the game would see it: the winning copy across the Data folder and every mod.</summary>
	internal sealed record Entry(string Relative, string FullPath, string? Mod, string Root, long Length);

	public readonly record struct Progress(string Text, double Percent);
}
