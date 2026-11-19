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
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectiveModdingToolkit.Services;

/// <summary>
///     User settings, saved to <c>settings.json</c> in the app data folder whenever one changes.
/// </summary>
/// <remarks>
///     Loading is forgiving: a missing, corrupt or hand-edited file never stops the app. Each value is read on its own,
///     so one bad value only resets that value. Unknown keys are dropped the next time the file is saved.
/// </remarks>
public sealed partial class AppSettings : ObservableObject
{
	private static readonly JsonWriterOptions WriterOptions = new()
	{
		IndentCharacter = '\t',
		IndentSize = 1,
		Indented = true,
	};

	/// <summary>True while values are being loaded, so loading doesn't trigger saves.</summary>
	private bool _loading;

	public static AppSettings Current { get; private set; } = new();

	[ObservableProperty] public partial LogLevel LogLevel { get; set; } = LogLevel.Info;

	/// <summary>Check Nexus Mods for a newer version when the app starts.</summary>
	[ObservableProperty]
	public partial bool CheckForUpdates { get; set; } = true;

	/// <summary>When the last successful update check finished, to limit automatic checks.</summary>
	[ObservableProperty]
	public partial DateTimeOffset? LastUpdateCheck { get; set; }

	/// <summary>The newest version found by the last successful update check.</summary>
	[ObservableProperty]
	public partial string? LatestVersion { get; set; }

	/// <summary>Downgrader: keep backups of the files being replaced.</summary>
	[ObservableProperty]
	public partial bool DowngraderKeepBackups { get; set; } = true;

	/// <summary>Downgrader: delete downloaded delta patches after use.</summary>
	[ObservableProperty]
	public partial bool DowngraderDeleteDeltas { get; set; } = true;

	// Scanner options. Each is saved as "scanner<Name>".
	[ObservableProperty] public partial bool ScanOverviewIssues { get; set; } = true;
	[ObservableProperty] public partial bool ScanWrongFormat { get; set; } = true;
	[ObservableProperty] public partial bool ScanLoosePrevis { get; set; } = true;
	[ObservableProperty] public partial bool ScanJunkFiles { get; set; } = false;
	[ObservableProperty] public partial bool ScanProblemOverrides { get; set; } = true;
	[ObservableProperty] public partial bool ScanRaceSubgraphs { get; set; } = false;
	[ObservableProperty] public partial bool ScanTextureIssues { get; set; } = true;
	[ObservableProperty] public partial bool ScanEmptyFiles { get; set; } = false;
	[ObservableProperty] public partial bool ScanMaterialIssues { get; set; } = true;
	[ObservableProperty] public partial bool ScanBestPractices { get; set; } = false;
	[ObservableProperty] public partial bool ScanReadArchives { get; set; } = true;

	partial void OnLogLevelChanged(LogLevel value) => Log.Level = value;

	/// <summary>Every property is a persisted setting, so any change saves.</summary>
	protected override void OnPropertyChanged(PropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		Save();
	}

	/// <summary>Reads settings from disk into <see cref="Current" />. Never throws.</summary>
	internal static AppSettings Load()
	{
		AppSettings settings = new() { _loading = true };
		try
		{
			if (File.Exists(AppPaths.SettingsFile))
			{
				using var document = JsonDocument.Parse(File.ReadAllText(AppPaths.SettingsFile),
					new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
				if (document.RootElement.ValueKind == JsonValueKind.Object)
				{
					var root = document.RootElement;
					if (TryReadEnum(root, "logLevel", out LogLevel level)) settings.LogLevel = level;

					if (TryReadBool(root, "checkForUpdates", out var checkUpdates))
						settings.CheckForUpdates = checkUpdates;

					if (TryReadDate(root, "lastUpdateCheck", out var lastCheck)) settings.LastUpdateCheck = lastCheck;

					if (TryReadString(root, "latestVersion", out var latest)) settings.LatestVersion = latest;

					if (TryReadBool(root, "downgraderKeepBackups", out var keep)) settings.DowngraderKeepBackups = keep;

					if (TryReadBool(root, "downgraderDeleteDeltas", out var delete))
						settings.DowngraderDeleteDeltas = delete;

					if (TryReadBool(root, "scannerOverviewIssues", out var scanOverview))
						settings.ScanOverviewIssues = scanOverview;

					if (TryReadBool(root, "scannerWrongFormat", out var scanFormat))
						settings.ScanWrongFormat = scanFormat;

					if (TryReadBool(root, "scannerLoosePrevis", out var scanPrevis))
						settings.ScanLoosePrevis = scanPrevis;

					if (TryReadBool(root, "scannerJunkFiles", out var scanJunk)) settings.ScanJunkFiles = scanJunk;

					if (TryReadBool(root, "scannerProblemOverrides", out var scanOverrides))
						settings.ScanProblemOverrides = scanOverrides;

					if (TryReadBool(root, "scannerRaceSubgraphs", out var scanRace))
						settings.ScanRaceSubgraphs = scanRace;

					if (TryReadBool(root, "scannerTextureIssues", out var scanTextures))
						settings.ScanTextureIssues = scanTextures;

					if (TryReadBool(root, "scannerEmptyFiles", out var scanEmpty))
						settings.ScanEmptyFiles = scanEmpty;

					if (TryReadBool(root, "scannerMaterialIssues", out var scanMaterials))
						settings.ScanMaterialIssues = scanMaterials;

					if (TryReadBool(root, "scannerBestPractices", out var scanBest))
						settings.ScanBestPractices = scanBest;

					if (TryReadBool(root, "scannerReadArchives", out var scanArchives))
						settings.ScanReadArchives = scanArchives;
				}
				else
				{
					Log.Warning($"{AppPaths.SettingsFile} is not a JSON object. Using default settings.");
				}
			}
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
		{
			Log.Warning($"Unable to read settings from {AppPaths.SettingsFile}: {e.Message}. Using default settings.");
		}

		settings._loading = false;
		Current = settings;

		return settings;
	}

	/// <summary>Writes settings to disk. Never throws.</summary>
	private void Save()
	{
		if (_loading) return;

		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.SettingsFile)!);
			using var buffer = new MemoryStream();
			using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
			{
				writer.WriteStartObject();
				writer.WriteString("logLevel", LogLevel.ToString());
				writer.WriteBoolean("checkForUpdates", CheckForUpdates);
				if (LastUpdateCheck is { } lastCheck)
					writer.WriteString("lastUpdateCheck", lastCheck.ToString("O"));
				else
					writer.WriteNull("lastUpdateCheck");

				if (LatestVersion is { } latest)
					writer.WriteString("latestVersion", latest);
				else
					writer.WriteNull("latestVersion");

				writer.WriteBoolean("downgraderKeepBackups", DowngraderKeepBackups);
				writer.WriteBoolean("downgraderDeleteDeltas", DowngraderDeleteDeltas);
				writer.WriteBoolean("scannerOverviewIssues", ScanOverviewIssues);
				writer.WriteBoolean("scannerWrongFormat", ScanWrongFormat);
				writer.WriteBoolean("scannerLoosePrevis", ScanLoosePrevis);
				writer.WriteBoolean("scannerJunkFiles", ScanJunkFiles);
				writer.WriteBoolean("scannerProblemOverrides", ScanProblemOverrides);
				writer.WriteBoolean("scannerRaceSubgraphs", ScanRaceSubgraphs);
				writer.WriteBoolean("scannerTextureIssues", ScanTextureIssues);
				writer.WriteBoolean("scannerEmptyFiles", ScanEmptyFiles);
				writer.WriteBoolean("scannerMaterialIssues", ScanMaterialIssues);
				writer.WriteBoolean("scannerBestPractices", ScanBestPractices);
				writer.WriteBoolean("scannerReadArchives", ScanReadArchives);
				writer.WriteEndObject();
			}

			var json = Encoding.UTF8.GetString(buffer.ToArray());

			var temp = AppPaths.SettingsFile + ".tmp";
			File.WriteAllText(temp, json + Environment.NewLine);
			File.Move(temp, AppPaths.SettingsFile, true);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			Log.Warning($"Unable to save settings to {AppPaths.SettingsFile}: {e.Message}");
		}
	}

	private static bool TryReadEnum<T>(JsonElement root, string name, out T value) where T : struct, Enum
	{
		value = default;
		if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String) return false;

		if (Enum.TryParse(element.GetString(), true, out T parsed) && Enum.IsDefined(parsed))
		{
			value = parsed;
			return true;
		}

		Log.Warning($"Ignoring invalid value for '{name}': {element.GetString()}");
		return false;
	}

	private static bool TryReadBool(JsonElement root, string name, out bool value)
	{
		value = false;
		if (!root.TryGetProperty(name, out var element) ||
		    element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
			return false;

		value = element.GetBoolean();
		return true;
	}

	private static bool TryReadString(JsonElement root, string name, out string value)
	{
		value = "";
		if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String) return false;

		value = element.GetString() ?? "";
		return value.Length > 0;
	}

	private static bool TryReadDate(JsonElement root, string name, out DateTimeOffset value)
	{
		value = default;
		if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String) return false;

		if (DateTimeOffset.TryParse(element.GetString(), CultureInfo.InvariantCulture,
			    DateTimeStyles.RoundtripKind, out value))
			return true;

		Log.Warning($"Ignoring invalid value for '{name}': {element.GetString()}");
		return false;
	}
}
