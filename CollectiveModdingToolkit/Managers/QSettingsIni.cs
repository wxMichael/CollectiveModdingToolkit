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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CollectiveModdingToolkit.Managers;

/// <summary>
///     Reads an INI file written by Qt's QSettings, such as MO2's ModOrganizer.ini and profile settings.ini.
///     Values are unescaped the way QSettings does: quoting, backslash escapes, comma separated lists, and the
///     @ByteArray(), @Invalid() and @@ markers.
/// </summary>
internal sealed class QSettingsIni
{
	private readonly Dictionary<string, Dictionary<string, string>> _sections =
		new(StringComparer.OrdinalIgnoreCase);

	private QSettingsIni()
	{
	}

	/// <exception cref="IOException" />
	/// <exception cref="UnauthorizedAccessException" />
	public static QSettingsIni Read(string path)
	{
		QSettingsIni ini = new();
		var section = ini.GetOrAddSection("General");

		// Qt 6 writes UTF-8. Qt 5 wrote ASCII with \x escapes, which UTF-8 also reads.
		foreach (var rawLine in File.ReadLines(path, Encoding.UTF8))
		{
			var line = rawLine.TrimStart();
			if (line.Length == 0 || line[0] is ';' or '#') continue;

			if (line[0] == '[')
			{
				var end = line.IndexOf(']');
				if (end > 0) section = ini.GetOrAddSection(UnescapeKey(line[1..end]));
				continue;
			}

			var equals = line.IndexOf('=');
			if (equals <= 0) continue;

			// Later duplicates replace earlier ones, as in QSettings.
			section[UnescapeKey(line[..equals].Trim())] = line[(equals + 1)..];
		}

		return ini;
	}

	/// <returns>The value as a string, with @ByteArray() decoded as UTF-8 like MO2 does, or null if missing.</returns>
	public string? GetString(string section, string key)
	{
		if (GetRaw(section, key) is not { } raw) return null;

		var parts = UnescapeList(raw);
		if (parts.Count == 1 && parts[0] is { } part && part.StartsWith("@ByteArray(") && part.EndsWith(')'))
			return Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(part[11..^1]));

		return string.Join(",", parts.Select(DecodeString));
	}

	/// <returns>The value as a list, empty for @Invalid(), or null if missing.</returns>
	public IReadOnlyList<string>? GetStringList(string section, string key)
	{
		if (GetRaw(section, key) is not { } raw) return null;

		var parts = UnescapeList(raw);
		if (parts is ["@Invalid()"]) return [];

		return [.. parts.Select(DecodeString)];
	}

	/// <returns>The value as a bool, the way QVariant converts it: "", "0", and "false" are false.</returns>
	public bool GetBool(string section, string key, bool fallback)
	{
		var value = GetString(section, key)?.Trim();
		if (value == null) return fallback;

		return value.Length != 0 && value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
	}

	private string? GetRaw(string section, string key) =>
		_sections.TryGetValue(section, out var values) && values.TryGetValue(key, out var value) ? value : null;

	private Dictionary<string, string> GetOrAddSection(string name)
	{
		if (_sections.TryGetValue(name, out var section)) return section;
		section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		_sections[name] = section;

		return section;
	}

	/// <summary>"@@text" is how QSettings stores a string that starts with "@".</summary>
	private static string DecodeString(string value) => value.StartsWith("@@") ? value[1..] : value;

	/// <summary>Section and key names escape special characters as %XX or %UXXXX.</summary>
	private static string UnescapeKey(string key)
	{
		if (!key.Contains('%')) return key;

		StringBuilder result = new(key.Length);
		for (var i = 0; i < key.Length; i++)
		{
			if (key[i] == '%')
			{
				if (i + 5 < key.Length && key[i + 1] == 'U' &&
				    int.TryParse(key.AsSpan(i + 2, 4), NumberStyles.HexNumber, null, out var wide))
				{
					result.Append((char)wide);
					i += 5;
					continue;
				}

				if (i + 2 < key.Length &&
				    int.TryParse(key.AsSpan(i + 1, 2), NumberStyles.HexNumber, null, out var narrow))
				{
					result.Append((char)narrow);
					i += 2;
					continue;
				}
			}

			result.Append(key[i]);
		}

		return result.ToString();
	}

	/// <summary>
	///     Splits a value on unquoted commas and undoes QSettings' escaping. Whitespace outside quotes around each
	///     item is dropped.
	/// </summary>
	private static List<string> UnescapeList(string raw)
	{
		List<string> items = [];
		StringBuilder current = new();
		var inQuotes = false;
		// Length of current up to the last character that must be kept, so unquoted trailing spaces can be cut.
		var keepLength = 0;

		for (var i = 0; i < raw.Length; i++)
		{
			var ch = raw[i];

			if (ch == '\\' && i + 1 < raw.Length)
			{
				var escape = raw[++i];
				switch (escape)
				{
					case 'a': current.Append('\a'); break;
					case 'b': current.Append('\b'); break;
					case 'f': current.Append('\f'); break;
					case 'n': current.Append('\n'); break;
					case 'r': current.Append('\r'); break;
					case 't': current.Append('\t'); break;
					case 'v': current.Append('\v'); break;
					case 'x':
					{
						var start = i + 1;
						var end = start;
						while (end < raw.Length && char.IsAsciiHexDigit(raw[end])) end++;
						// Qt 5 escapes each UTF-16 unit, so surrogate pairs arrive as two escapes and are kept as is.
						if (end > start &&
						    int.TryParse(raw.AsSpan(start, Math.Min(end - start, 6)), NumberStyles.HexNumber, null,
							    out var code))
						{
							if (code <= 0xFFFF) current.Append((char)code);
							else if (code <= 0x10FFFF) current.Append(char.ConvertFromUtf32(code));
						}

						i = end - 1;
						break;
					}
					case >= '0' and <= '7':
					{
						var end = i;
						while (end < raw.Length && end - i < 3 && raw[end] is >= '0' and <= '7') end++;
						current.Append((char)Convert.ToInt32(raw[i..end], 8));
						i = end - 1;
						break;
					}
					default:
						// \\, \", \', \?, and anything unknown are the character itself.
						current.Append(escape);
						break;
				}

				keepLength = current.Length;
				continue;
			}

			if (ch == '"')
			{
				inQuotes = !inQuotes;
				keepLength = current.Length;
				continue;
			}

			if (!inQuotes)
			{
				if (ch == ',')
				{
					items.Add(current.ToString(0, keepLength));
					current.Clear();
					keepLength = 0;
					continue;
				}

				if (char.IsWhiteSpace(ch))
				{
					if (current.Length != 0) current.Append(ch);
					continue;
				}
			}

			current.Append(ch);
			keepLength = current.Length;
		}

		items.Add(current.ToString(0, keepLength));
		return items;
	}
}
