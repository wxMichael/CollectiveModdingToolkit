// Generates THIRD-PARTY-NOTICES.txt from what the app actually ships.
//
//   dotnet run tools/GenerateThirdPartyNotices.cs              regenerate the file
//   dotnet run tools/GenerateThirdPartyNotices.cs -- --check   exit 1 if the file is stale (offline, fast; for CI)
//
// What is included is read from CollectiveModdingToolkit/obj/project.assets.json (run a restore or build first):
// the resolved win-x64 package graph, keeping only packages that put a managed or native file in the output, plus the
// self-contained .NET runtime pack. Build-only packages and other platforms' placeholder packages drop out by themselves.
// License texts are fetched from each package's source repository at the commit named in its NuGet metadata (cached in
// tools/third-party/.cache). Anything that cannot be discovered (code ported from elsewhere, the bundled font, repository
// quirks) lives in tools/third-party/config.json.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

const int RuleLength = 80;
const string LicenseMarker = ">>> LICENSE: ";
string[] licenseFileNames =
[
	"LICENSE", "LICENSE.md", "LICENSE.txt", "LICENSE.TXT", "License.md", "License.txt", "license.txt", "license.md",
	"licence.md", "LICENCE", "COPYING", "COPYING.txt",
];

var check = args.Contains("--check");
var root = FindRoot();
var configPath = Path.Combine(root, "tools", "third-party", "config.json");
var outputPath = Path.Combine(root, "THIRD-PARTY-NOTICES.txt");
var assetsPath = Path.Combine(root, "CollectiveModdingToolkit", "obj", "project.assets.json");
if (!File.Exists(assetsPath)) return Fail($"{assetsPath} not found. Restore the project first (dotnet restore CollectiveModdingToolkit).");

var configBytes = File.ReadAllBytes(configPath);
var config = JsonDocument.Parse(configBytes, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }).RootElement;
var staticEntries = config.GetProperty("static").EnumerateArray().Select(e => new StaticEntry(
	e.GetProperty("name").GetString()!,
	e.GetProperty("license").GetString()!,
	e.GetProperty("details").EnumerateArray().Select(d => d.GetString()!).ToArray(),
	Path.Combine(root, e.GetProperty("textFile").GetString()!))).ToList();
var overrides = config.GetProperty("overrides").EnumerateObject().Where(p => !p.Name.StartsWith('_')).ToDictionary(
	p => p.Name,
	p => new Override(
		Str(p.Value, "repo"), Str(p.Value, "ref"), Str(p.Value, "licensePath"), Str(p.Value, "name"),
		p.Value.TryGetProperty("skipBundledNotices", out var skip) && skip.GetBoolean()));

// ---- What does the app ship? ----
var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath)).RootElement;
var folders = assets.GetProperty("packageFolders").EnumerateObject().Select(p => p.Name).ToList();
var targets = assets.GetProperty("targets").EnumerateObject().ToList();
var target = (targets.FirstOrDefault(t => t.Name.Contains('/')).Value is { ValueKind: JsonValueKind.Object } rid ? rid : targets[0].Value);

var packages = new List<Pkg>();
foreach (var lib in target.EnumerateObject())
{
	if (lib.Value.GetProperty("type").GetString() != "package" || !ShipsFiles(lib.Value)) continue;
	var slash = lib.Name.LastIndexOf('/');
	packages.Add(Locate(lib.Name[..slash], lib.Name[(slash + 1)..]));
}

foreach (var framework in assets.GetProperty("project").GetProperty("frameworks").EnumerateObject())
{
	if (!framework.Value.TryGetProperty("downloadDependencies", out var downloads)) continue;
	foreach (var d in downloads.EnumerateArray())
	{
		// Only the base runtime pack ends up in a self-contained app (not ASP.NET or WindowsDesktop).
		var name = d.GetProperty("name").GetString()!;
		if (!name.StartsWith("Microsoft.NETCore.App.Runtime.", StringComparison.OrdinalIgnoreCase)) continue;
		var version = Regex.Match(d.GetProperty("version").GetString()!, @"\d[^,\]\s]*").Value;
		if (!packages.Any(p => p.Id.Equals(name, StringComparison.OrdinalIgnoreCase))) packages.Add(Locate(name, version));
	}
}

packages = [.. packages.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase)];

// ---- Fingerprint of the inputs, so staleness can be detected without a network ----
var fingerprintInput = new StringBuilder();
foreach (var p in packages) fingerprintInput.Append(p.Id).Append('/').Append(p.Version).Append('\n');
fingerprintInput.Append(Normalize(Encoding.UTF8.GetString(configBytes)));
foreach (var s in staticEntries) fingerprintInput.Append(File.Exists(s.TextFile) ? Normalize(File.ReadAllText(s.TextFile)) : "missing:" + s.TextFile);
var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput.ToString())))[..16].ToLowerInvariant();

if (check)
{
	var existing = File.Exists(outputPath) ? File.ReadAllText(outputPath) : "";
	var recorded = Regex.Match(existing, @"Inputs fingerprint: ([0-9a-f]{16})").Groups[1].Value;
	if (recorded == fingerprint)
	{
		Console.WriteLine($"THIRD-PARTY-NOTICES.txt is up to date ({packages.Count} packages).");
		return 0;
	}

	return Fail("THIRD-PARTY-NOTICES.txt is out of date with the packages the app uses (or tools/third-party/config.json). " +
	            "Run: dotnet run tools/GenerateThirdPartyNotices.cs");
}

// ---- Group packages by source repository + license file ----
using HttpClient http = new();
http.DefaultRequestHeaders.UserAgent.ParseAdd("CollectiveModdingToolkit-notices-generator");
var cacheDir = Path.Combine(root, "tools", "third-party", ".cache");
Directory.CreateDirectory(cacheDir);

var groups = new Dictionary<string, Group>();
var seenBundled = new HashSet<string>();
var failures = new List<string>();
var warnings = new List<string>();

foreach (var pkg in packages)
{
	var meta = ReadNuspec(pkg);
	var ov = FindOverride(pkg.Id);
	var (owner, repo) = ParseGitHub(ov?.Repo) ?? ParseGitHub(meta.RepoUrl) ?? ParseGitHub(meta.ProjectUrl) ?? default;
	if (owner == null)
	{
		failures.Add($"{pkg.Id} {pkg.Version}: no GitHub repository in its package metadata. Add an override with \"repo\" in tools/third-party/config.json.");
		continue;
	}

	var gitRef = ov?.Ref ?? meta.Commit ?? meta.Branch ?? "HEAD";
	var key = $"{owner}/{repo}@{gitRef}|{ov?.LicensePath}";
	if (!groups.TryGetValue(key, out var group))
	{
		var (text, path, fromRepo) = await GetLicenseText(owner, repo, gitRef, ov?.LicensePath, pkg);
		if (text == null)
		{
			failures.Add($"{pkg.Id} {pkg.Version}: license not found in {owner}/{repo}@{gitRef} or in the package. Add an override with \"licensePath\" in tools/third-party/config.json.");
			continue;
		}

		if (!fromRepo) warnings.Add($"{pkg.Id}: license taken from the package ({path}), not from the repository.");
		group = new Group(repo, $"https://github.com/{owner}/{repo}", gitRef, text);
		groups[key] = group;
	}

	group.Packages.Add(pkg);
	var licenseName = meta.LicenseExpression ?? Detect(group.Text);
	if (!group.LicenseNames.Contains(licenseName)) group.LicenseNames.Add(licenseName);
	if (ov?.Name != null) group.ConfiguredName = ov.Name;

	if (ov?.SkipBundledNotices == true) continue;
	foreach (var file in Directory.GetFiles(pkg.Dir).Where(f => Regex.IsMatch(Path.GetFileName(f), @"^third[-_]?party[-_]?notices?(\.\w+)?$", RegexOptions.IgnoreCase)))
	{
		var text = Normalize(File.ReadAllText(file));
		if (seenBundled.Add(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))))
			group.Bundled.Add(($"{pkg.Id} bundled notices", text));
	}
}

foreach (var w in warnings) Console.Error.WriteLine("warning: " + w);
if (failures.Count > 0) return Fail(string.Join(Environment.NewLine, failures));

// ---- Write the file ----
var entries = new List<(string Name, string License)>();
var body = new StringBuilder();

void Section(string title, IEnumerable<string> details, IEnumerable<(string Label, string License, string Text)> licenses)
{
	body.Append("\n\n").Append('=', RuleLength).Append('\n').Append(title).Append('\n');
	foreach (var d in details) body.Append(d).Append('\n');
	body.Append('=', RuleLength).Append("\n\n");
	foreach (var (label, license, text) in licenses)
	{
		body.Append(LicenseMarker).Append(label).Append(" (").Append(license).Append(")\n\n").Append(text).Append("\n\n");
	}
}

foreach (var s in staticEntries)
{
	entries.Add((s.Name, s.License));
	Section(s.Name, s.Details, [(s.Name, s.License, Normalize(File.ReadAllText(s.TextFile)))]);
}

// A group's name: the configured one, else the package id when it is a single package, else the repository name.
foreach (var g in groups.Values) g.Name = g.ConfiguredName ?? (g.Packages.Count == 1 ? g.Packages[0].Id : g.Name);

foreach (var g in groups.Values.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
{
	var license = string.Join(", ", g.LicenseNames.Select(n => n.Replace('(', '[').Replace(')', ']')));
	entries.Add((g.Name, license));
	List<string> details = [.. Wrap("Packages: " + string.Join(", ", g.Packages.Select(p => $"{p.Id} {p.Version}")))];
	details.Add($"Source: {g.RepoUrl}  ({(g.Ref == "HEAD" ? "default branch" : g.Ref.Length == 40 ? "commit " + g.Ref : g.Ref)})");
	List<(string, string, string)> licenses = [(g.Name, license, g.Text)];
	licenses.AddRange(g.Bundled.Select(b => (b.Label, "Various", b.Text)));
	Section(g.Name, details, licenses);
}

var head = new StringBuilder();
head.Append("""
	THIRD-PARTY NOTICES
	===================

	Collective Modding Toolkit is licensed under the GNU General Public License,
	version 2 or (at your option) any later version. See LICENSE.md.

	It includes, or is derived from, the third-party software listed below. Each
	component is used under its own license, reproduced in full.

	This file is generated by tools/GenerateThirdPartyNotices.cs from the packages the
	app ships. Do not edit it by hand. Inputs fingerprint:
	""".Replace("\r\n", "\n")).Append(' ').Append(fingerprint).Append("\n\nContents\n");
for (var i = 0; i < entries.Count; i++) head.Append($"{i + 1,3}. {entries[i].Name,-34} {entries[i].License}\n");

var output = (head + body.ToString()).Replace("\r\n", "\n").TrimEnd() + "\n";
File.WriteAllText(outputPath, output, new UTF8Encoding(false));
Console.WriteLine($"Wrote {outputPath}: {entries.Count} entries from {packages.Count} packages ({output.Length / 1024} KB).");
return 0;

// ================= helpers =================

static string FindRoot()
{
	for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
		if (File.Exists(Path.Combine(dir.FullName, "CollectiveModdingToolkit.sln"))) return dir.FullName;

	throw new InvalidOperationException("Run this from inside the repository.");
}

static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetString() : null;

static int Fail(string message)
{
	Console.Error.WriteLine(message);
	return 1;
}

static string Normalize(string text) => text.TrimStart('﻿').ReplaceLineEndings("\n").TrimEnd();

static bool ShipsFiles(JsonElement lib)
{
	// Reference-only (compile) and build-only assets do not end up in the output. Placeholder files ("_._") mark
	// packages with nothing for this platform.
	foreach (var kind in new[] { "runtime", "native", "runtimeTargets" })
	{
		if (!lib.TryGetProperty(kind, out var files)) continue;
		if (files.EnumerateObject().Any(f => !f.Name.EndsWith("/_._", StringComparison.Ordinal))) return true;
	}

	return false;
}

IEnumerable<string> Wrap(string text)
{
	var line = new StringBuilder();
	foreach (var word in text.Split(' '))
	{
		if (line.Length + word.Length + 1 > RuleLength - 2 && line.Length > 0)
		{
			yield return line.ToString().TrimEnd();
			line.Clear().Append("  ");
		}

		line.Append(word).Append(' ');
	}

	yield return line.ToString().TrimEnd();
}

Pkg Locate(string id, string version)
{
	foreach (var folder in folders)
	{
		var dir = Path.Combine(folder, id.ToLowerInvariant(), version);
		if (Directory.Exists(dir)) return new Pkg(id, version, dir);
	}

	throw new InvalidOperationException($"{id} {version} is not in the NuGet package folders. Restore the project.");
}

Override? FindOverride(string id)
{
	string? best = null;
	foreach (var key in overrides.Keys)
	{
		var matches = key.EndsWith('*') ? id.StartsWith(key[..^1], StringComparison.OrdinalIgnoreCase) : id.Equals(key, StringComparison.OrdinalIgnoreCase);
		if (matches && (best == null || key.Length > best.Length)) best = key;
	}

	return best == null ? null : overrides[best];
}

static (string, string)? ParseGitHub(string? url)
{
	if (url == null) return null;
	var m = Regex.Match(url, @"github\.com/([^/]+)/([^/#?]+)", RegexOptions.IgnoreCase);
	return m.Success ? (m.Groups[1].Value, Regex.Replace(m.Groups[2].Value, @"\.git$", "")) : null;
}

static Meta ReadNuspec(Pkg pkg)
{
	var file = Directory.GetFiles(pkg.Dir, "*.nuspec").Single();
	var metadata = XDocument.Load(file).Root!.Elements().First(e => e.Name.LocalName == "metadata");
	XElement? Find(string name) => metadata.Elements().FirstOrDefault(e => e.Name.LocalName == name);
	var license = Find("license");
	var repository = Find("repository");
	return new Meta(
		license?.Attribute("type")?.Value == "expression" ? license.Value.Trim() : null,
		repository?.Attribute("url")?.Value,
		repository?.Attribute("commit")?.Value,
		repository?.Attribute("branch")?.Value?.Replace("refs/heads/", ""),
		Find("projectUrl")?.Value);
}

static string Detect(string text)
{
	if (text.Contains("SIL OPEN FONT LICENSE", StringComparison.OrdinalIgnoreCase)) return "OFL-1.1";
	if (text.Contains("Microsoft Public License", StringComparison.OrdinalIgnoreCase)) return "MS-PL";
	if (text.Contains("Apache License", StringComparison.OrdinalIgnoreCase)) return "Apache-2.0";
	if (text.Contains("Permission is hereby granted, free of charge", StringComparison.OrdinalIgnoreCase)) return "MIT";
	if (text.Contains("Redistribution and use in source and binary forms", StringComparison.OrdinalIgnoreCase))
		return text.Contains("Neither the name", StringComparison.OrdinalIgnoreCase) ? "BSD-3-Clause" : "BSD-2-Clause";

	return "See license text";
}

async Task<(string? Text, string? Path, bool FromRepo)> GetLicenseText(string owner, string repo, string gitRef, string? path, Pkg pkg)
{
	var cacheFile = Path.Combine(cacheDir, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{owner}/{repo}@{gitRef}|{path}")))[..24] + ".txt");
	if (File.Exists(cacheFile))
	{
		var cached = File.ReadAllText(cacheFile);
		var nl = cached.IndexOf('\n');
		return (cached[(nl + 1)..], cached[..nl], true);
	}

	foreach (var candidate in path != null ? [path] : licenseFileNames)
	{
		var response = await http.GetAsync($"https://raw.githubusercontent.com/{owner}/{repo}/{gitRef}/{candidate}");
		if (response.StatusCode == HttpStatusCode.NotFound) continue;
		response.EnsureSuccessStatusCode();
		var text = Normalize(await response.Content.ReadAsStringAsync());
		File.WriteAllText(cacheFile, candidate + "\n" + text);
		return (text, candidate, true);
	}

	// Last resort: the license shipped inside the package.
	var packaged = Directory.GetFiles(pkg.Dir).FirstOrDefault(f => Regex.IsMatch(Path.GetFileName(f), @"^(license|licence|copying)(\.\w+)?$", RegexOptions.IgnoreCase));
	return packaged == null ? (null, null, false) : (Normalize(File.ReadAllText(packaged)), Path.GetFileName(packaged), false);
}

// ================= types =================

record Pkg(string Id, string Version, string Dir);

record Meta(string? LicenseExpression, string? RepoUrl, string? Commit, string? Branch, string? ProjectUrl);

record Override(string? Repo, string? Ref, string? LicensePath, string? Name, bool SkipBundledNotices);

record StaticEntry(string Name, string License, string[] Details, string TextFile);

class Group(string name, string repoUrl, string gitRef, string text)
{
	public string Name { get; set; } = name;
	public string? ConfiguredName { get; set; }
	public string RepoUrl { get; } = repoUrl;
	public string Ref { get; } = gitRef;
	public string Text { get; } = text;
	public List<Pkg> Packages { get; } = [];
	public List<string> LicenseNames { get; } = [];
	public List<(string Label, string Text)> Bundled { get; } = [];
}
