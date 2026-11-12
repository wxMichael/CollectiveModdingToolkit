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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CollectiveModdingToolkit.Delta;

namespace DeltaTool;

internal static class Program
{
	private const string Usage = """
	                             DeltaTool: makes and checks the delta patches the Downgrader downloads.

	                             Put one copy of each game file per edition in a folder tree like this:

	                               versions/OG/Fallout4.exe
	                               versions/OG/Fallout4Launcher.exe
	                               versions/OG/steam_api64.dll
	                               versions/OG/CreationKit.exe
	                               versions/OG/Tools/Archive2/Archive2.exe
	                               versions/OG/Tools/Archive2/Archive2Interop.dll
	                               versions/NG/...   versions/AE/...

	                             Files may be missing from an edition; patches are made for every pair that has both sides.

	                             Commands:
	                               crcs    --versions <dir>
	                                   Lists the CRC32 of every file and whether it matches the table in GameFiles.cs.
	                                   Run this first after a game update, then edit the table.
	                               create  --versions <dir> --out <dir> [--force] [--jobs <n>] [--only <file name>]
	                                   Makes every patch, plus manifest.json. Existing patches that are still correct are kept.
	                                   Refuses files whose CRC32 is not in the table. --allow-unknown skips that check for
	                                   dry runs; never publish the result, because the app will not use those patches.
	                               verify  --versions <dir> --patches <dir>
	                                   Applies every patch in <dir> to its source file and compares with the target.

	                             Upload the contents of --out to the GitHub release the app downloads from.
	                             """;

	private static int Main(string[] args)
	{
		if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
		{
			Console.WriteLine(Usage);
			return args.Length == 0 ? 1 : 0;
		}

		try
		{
			var options = ParseOptions(args.Skip(1).ToArray());
			return args[0] switch
			{
				"crcs" => Crcs(Require(options, "versions")),
				"create" => Create(Require(options, "versions"), Require(options, "out"), options),
				"verify" => Verify(Require(options, "versions"), Require(options, "patches")),
				_ => Fail($"Unknown command '{args[0]}'.\n\n{Usage}"),
			};
		}
		catch (ArgumentException e)
		{
			return Fail(e.Message);
		}
		catch (Exception e) when (e is IOException or DeltaException or UnauthorizedAccessException)
		{
			return Fail(e.Message);
		}
	}

	private static int Fail(string message)
	{
		Console.Error.WriteLine(message);
		return 1;
	}

	private static Dictionary<string, string> ParseOptions(string[] args)
	{
		Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < args.Length; i++)
		{
			if (!args[i].StartsWith("--", StringComparison.Ordinal))
				throw new ArgumentException($"Unexpected argument '{args[i]}'.");

			var name = args[i][2..];
			options[name] = name is "force" or "allow-unknown"
				? "true"
				: i + 1 < args.Length
					? args[++i]
					: throw new ArgumentException($"--{name} needs a value.");
		}

		return options;
	}

	private static string Require(Dictionary<string, string> options, string name) =>
		options.TryGetValue(name, out var value) ? value : throw new ArgumentException($"--{name} is required.");

	/// <summary>Every file of every edition folder that exists, in table order.</summary>
	private static List<(Edition Edition, TrackedFile File, string Path)> FindSources(string versionsDir)
	{
		if (!Directory.Exists(versionsDir)) throw new ArgumentException($"{versionsDir} does not exist.");

		List<(Edition, TrackedFile, string)> found = [];
		foreach (var edition in GameFiles.Supported)
		foreach (var file in GameFiles.All)
		{
			var path = Path.Combine(versionsDir, GameFiles.Code(edition), file.RelativePath);
			if (File.Exists(path)) found.Add((edition, file, path));
		}

		return found;
	}

	private static int Crcs(string versionsDir)
	{
		var bad = 0;
		foreach (var (edition, file, path) in FindSources(versionsDir))
		{
			var crc = GameFiles.Crc32OfFile(path);
			var known = file.Identify(crc);
			var ok = GameFiles.IsEdition(file, crc, edition);
			if (!ok) bad++;

			Console.WriteLine(
				$"{(ok ? "ok      " : "MISMATCH")} {GameFiles.Code(edition),-3} {file.RelativePath,-34} {crc:X8}  table says: {known}");
		}

		if (bad > 0)
			Console.WriteLine(
				$"\n{bad} file(s) are not recorded as that edition in GameFiles.cs. Update the table, or the file is in the wrong folder.");

		return bad == 0 ? 0 : 1;
	}

	private sealed record Job(Edition From, Edition To, TrackedFile File, string FromPath, string ToPath);

	private static int Create(string versionsDir, string outDir, Dictionary<string, string> options)
	{
		var force = options.ContainsKey("force");
		var jobs = options.TryGetValue("jobs", out var j) && int.TryParse(j, out var n) && n > 0 ? n : 1;
		options.TryGetValue("only", out var only);

		var sources = FindSources(versionsDir);
		var wrong = sources.Where(s =>
			!GameFiles.IsEdition(s.File, GameFiles.Crc32OfFile(s.Path), s.Edition)).ToList();
		if (wrong.Count > 0 && !options.ContainsKey("allow-unknown"))
		{
			foreach (var (edition, file, path) in wrong)
				Console.Error.WriteLine($"{path}: not a known {GameFiles.Code(edition)} {file.Name} (see 'crcs').");

			return Fail("Refusing to make patches the app would never use. Fix the table or the folders first.");
		}

		Directory.CreateDirectory(outDir);
		List<Job> work = [];
		foreach (var file in GameFiles.All)
		{
			if (only != null && !file.Name.Equals(only, StringComparison.OrdinalIgnoreCase)) continue;

			var versions = sources.Where(s => s.File == file).ToList();
			foreach (var from in versions)
			foreach (var to in versions)
			{
				if (from.Edition == to.Edition) continue;

				// Next-Gen and Anniversary share some files. Nothing to patch between identical files.
				if (GameFiles.Crc32OfFile(from.Path) == GameFiles.Crc32OfFile(to.Path)) continue;

				work.Add(new Job(from.Edition, to.Edition, file, from.Path, to.Path));
			}
		}

		Console.WriteLine($"{work.Count} patch(es) to make, {jobs} at a time.");
		ParallelOptions parallel = new() { MaxDegreeOfParallelism = jobs };
		Parallel.ForEach(work, parallel, job => MakePatch(job, outDir, force));

		WriteManifest(outDir);
		Console.WriteLine($"Wrote {Path.Combine(outDir, "manifest.json")}");
		return 0;
	}

	private static void MakePatch(Job job, string outDir, bool force)
	{
		var name = GameFiles.PatchFileName(job.From, job.To, job.File);
		var patchPath = Path.Combine(outDir, name);
		var oldData = File.ReadAllBytes(job.FromPath);
		var newData = File.ReadAllBytes(job.ToPath);

		if (!force && File.Exists(patchPath))
		{
			try
			{
				var existing = DeltaFormat.ReadInfo(File.ReadAllBytes(patchPath));
				if (existing.OldCrc == System.IO.Hashing.Crc32.HashToUInt32(oldData)
				    && existing.NewCrc == System.IO.Hashing.Crc32.HashToUInt32(newData))
				{
					Console.WriteLine($"keep   {name}");
					return;
				}
			}
			catch (DeltaException)
			{
				// Damaged or from an older format: remake it.
			}
		}

		var clock = Stopwatch.StartNew();
		var patch = DeltaCreator.Create(oldData, newData);
		File.WriteAllBytes(patchPath, patch);
		Console.WriteLine(
			$"made   {name}: {Format(patch.Length)} (target {Format(newData.Length)}, {patch.Length * 100.0 / newData.Length:F2}%) in {clock.Elapsed.TotalSeconds:F0}s");
	}

	private static void WriteManifest(string outDir)
	{
		var entries = new List<object>();
		foreach (var path in Directory.EnumerateFiles(outDir, "*" + DeltaFormat.Extension).Order())
		{
			var info = DeltaFormat.ReadInfo(File.ReadAllBytes(path));
			entries.Add(new
			{
				patch = Path.GetFileName(path),
				bytes = new FileInfo(path).Length,
				fromCrc = $"{info.OldCrc:X8}",
				toCrc = $"{info.NewCrc:X8}",
				fromBytes = info.OldSize,
				toBytes = info.NewSize,
				toSha256 = Convert.ToHexString(info.NewSha256),
			});
		}

		File.WriteAllText(Path.Combine(outDir, "manifest.json"),
			JsonSerializer.Serialize(new { format = (int)1, patches = entries },
				new JsonSerializerOptions { WriteIndented = true }));
	}

	private static int Verify(string versionsDir, string patchesDir)
	{
		if (!Directory.Exists(patchesDir)) throw new ArgumentException($"{patchesDir} does not exist.");

		var sources = FindSources(versionsDir);
		var patches = Directory.EnumerateFiles(patchesDir, "*" + DeltaFormat.Extension).Order().ToList();
		var failures = 0;
		foreach (var patchPath in patches)
		{
			var name = Path.GetFileName(patchPath);
			Job? job = null;
			foreach (var file in GameFiles.All)
			foreach (var from in GameFiles.Supported)
			foreach (var to in GameFiles.Supported)
				if (from != to && GameFiles.PatchFileName(from, to, file) == name)
				{
					var source = sources.FirstOrDefault(s => s.Edition == from && s.File == file);
					var target = sources.FirstOrDefault(s => s.Edition == to && s.File == file);
					if (source.Path != null && target.Path != null)
						job = new Job(from, to, file, source.Path, target.Path);
				}

			if (job == null)
			{
				Console.WriteLine($"skip   {name}: no source and target in {versionsDir}");
				continue;
			}

			try
			{
				var result = DeltaApplier.Apply(File.ReadAllBytes(patchPath), File.ReadAllBytes(job.FromPath));
				if (!result.AsSpan().SequenceEqual(File.ReadAllBytes(job.ToPath)))
					throw new DeltaException("result differs from the target file");

				Console.WriteLine($"ok     {name}");
			}
			catch (DeltaException e)
			{
				failures++;
				Console.WriteLine($"FAILED {name}: {e.Message}");
			}
		}

		Console.WriteLine(failures == 0 ? $"All {patches.Count} patches verified." : $"{failures} patch(es) failed.");
		return failures == 0 ? 0 : 1;
	}

	private static string Format(long bytes) => bytes switch
	{
		>= 1 << 20 => $"{bytes / 1048576.0:F2} MB",
		>= 1 << 10 => $"{bytes / 1024.0:F1} KB",
		_ => $"{bytes} B",
	};
}
