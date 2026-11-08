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
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace CollectiveModdingToolkit.Services;

public enum LogLevel
{
	Debug,
	Info,
	Warning,
	Error,
}

/// <summary>Where the app keeps its own files. This is a per-user folder, never the game or mod folders.</summary>
internal static class AppPaths
{
	/// <summary>
	///     <c>%LOCALAPPDATA%\CollectiveModdingToolkit</c>.
	/// </summary>
	internal static string DataFolder { get; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"CollectiveModdingToolkit");

	internal static string SettingsFile { get; } = Path.Combine(DataFolder, "settings.json");
	internal static string LogFile { get; } = Path.Combine(DataFolder, "cm-toolkit.log");
}

internal static class Log
{
	private const long MaxFileBytes = 1_000_000;

	private static readonly Lock Lock = new();
	private static readonly UTF8Encoding Utf8NoBom = new(false);
	private static bool _initialized;

	internal static LogLevel Level { get; set; } = LogLevel.Warning;

	internal static void Initialize(LogLevel level, string appVersion)
	{
		Level = level;
		lock (Lock)
		{
			try
			{
				Directory.CreateDirectory(AppPaths.DataFolder);
				FileInfo file = new(AppPaths.LogFile);
				if (file is { Exists: true, Length: > MaxFileBytes })
					File.Delete(AppPaths.LogFile);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// Fall through: Write() will fail the same way and be ignored.
			}

			_initialized = true;
		}

		Write(LogLevel.Info,
			$"==== Collective Modding Toolkit v{appVersion} started ({Environment.OSVersion.VersionString}) ====");
	}

	internal static void Debug(string message) => Write(LogLevel.Debug, message);

	internal static void Info(string message) => Write(LogLevel.Info, message);

	internal static void Warning(string message) => Write(LogLevel.Warning, message);

	internal static void Error(string message, Exception? exception = null) =>
		Write(LogLevel.Error, exception == null ? message : $"{message}\n{exception}");

	private static void Write(LogLevel level, string message)
	{
		if (level < Level) return;

		var line = string.Create(CultureInfo.InvariantCulture,
			$"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level.ToString().ToUpperInvariant(),-7}: {message}{Environment.NewLine}");
		lock (Lock)
		{
			if (!_initialized) return;

			try
			{
				File.AppendAllText(AppPaths.LogFile, line, Utf8NoBom);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// Another instance may have the file open, or the folder isn't writable.
			}
		}
	}
}
