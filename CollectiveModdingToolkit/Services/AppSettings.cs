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
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectiveModdingToolkit.Services;

public enum UpdateSource
{
	/// <summary>GitHub and Nexus Mods.</summary>
	Both,

	/// <summary>GitHub releases, which come first.</summary>
	GitHub,

	/// <summary>Nexus Mods, which may be delayed to allow more testing.</summary>
	Nexus,
	Never,
}

/// <summary>
///     User settings, saved to <c>settings.json</c> in the app data folder whenever one changes.
/// </summary>
/// <remarks>
///     Loading is forgiving: a missing, corrupt or hand-edited file never stops the app. Each value is read on its own,
///     so one bad value only resets that value. Unknown keys are dropped the next time the file is saved.
/// </remarks>
public sealed partial class AppSettings : ObservableObject
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		IndentCharacter = '\t',
		IndentSize = 1,
		WriteIndented = true,
	};

	/// <summary>True while values are being loaded, so loading doesn't trigger saves.</summary>
	private bool _loading;

	public static AppSettings Current { get; private set; } = new();

	[ObservableProperty] public partial LogLevel LogLevel { get; set; } = LogLevel.Info;

	[ObservableProperty] private partial UpdateSource UpdateSource { get; set; } = UpdateSource.GitHub;

	/// <summary>Downgrader: keep backups of the files being replaced.</summary>
	[ObservableProperty]
	private partial bool KeepBackups { get; set; } = true;

	/// <summary>Downgrader: delete downloaded delta patches after use.</summary>
	[ObservableProperty]
	private partial bool DeleteDeltas { get; set; } = true;

	// Scanner options. Each is saved as "scanner<Name>".
	[ObservableProperty] public partial bool ScanOverviewIssues { get; set; } = true;
	[ObservableProperty] public partial bool ScanWrongFormat { get; set; } = true;
	[ObservableProperty] public partial bool ScanLoosePrevis { get; set; } = true;
	[ObservableProperty] public partial bool ScanJunkFiles { get; set; } = true;
	[ObservableProperty] public partial bool ScanProblemOverrides { get; set; } = true;
	[ObservableProperty] public partial bool ScanRaceSubgraphs { get; set; } = true;

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

					if (TryReadEnum(root, "updateSource", out UpdateSource source)) settings.UpdateSource = source;

					if (TryReadBool(root, "keepBackups", out var keep)) settings.KeepBackups = keep;

					if (TryReadBool(root, "deleteDeltas", out var delete)) settings.DeleteDeltas = delete;

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
			var json = JsonSerializer.Serialize(
				new Dictionary<string, object>
				{
					["logLevel"] = LogLevel.ToString(),
					["updateSource"] = UpdateSource.ToString(),
					["keepBackups"] = KeepBackups,
					["deleteDeltas"] = DeleteDeltas,
					["scannerOverviewIssues"] = ScanOverviewIssues,
					["scannerWrongFormat"] = ScanWrongFormat,
					["scannerLoosePrevis"] = ScanLoosePrevis,
					["scannerJunkFiles"] = ScanJunkFiles,
					["scannerProblemOverrides"] = ScanProblemOverrides,
					["scannerRaceSubgraphs"] = ScanRaceSubgraphs,
				}, JsonOptions);

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
}
