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
using System.Text;

namespace CollectiveModdingToolkit.Models;

/// <summary>One third-party license, as listed in THIRD-PARTY-NOTICES.txt.</summary>
/// <param name="Name">The component the license is for.</param>
/// <param name="License">The license's name, e.g. "MIT".</param>
/// <param name="Details">Versions and source locations, from the section the license is in.</param>
/// <param name="Text">The full license text.</param>
public sealed record LicenseNotice(string Name, string License, string Details, string Text)
{
	private const string LicenseMarker = ">>> LICENSE: ";
	private const int SectionRuleLength = 80;

	/// <summary>
	///     Reads the notices file: sections are framed by a rule of 80 '=' around a title and source details, and each
	///     license inside a section starts with a <c>&gt;&gt;&gt; LICENSE: Name (License)</c> line.
	/// </summary>
	public static List<LicenseNotice> Parse(string notices)
	{
		List<LicenseNotice> result = [];
		var inHeader = false;
		var details = new StringBuilder();
		string? label = null;
		var text = new StringBuilder();

		foreach (var line in notices.ReplaceLineEndings("\n").Split('\n'))
		{
			if (IsRule(line))
			{
				if (inHeader)
				{
					inHeader = false;
				}
				else
				{
					Finish();
					details.Clear();
					inHeader = true;
				}

				continue;
			}

			if (inHeader)
			{
				details.AppendLine(line);
			}
			else if (line.StartsWith(LicenseMarker, StringComparison.Ordinal))
			{
				Finish();
				label = line[LicenseMarker.Length..].Trim();
			}
			else if (label != null)
			{
				text.AppendLine(line);
			}
		}

		Finish();
		return result;

		void Finish()
		{
			if (label != null) result.Add(Create(label, details.ToString().Trim(), text.ToString().Trim()));

			label = null;
			text.Clear();
		}
	}

	private static bool IsRule(string line) =>
		line.Length == SectionRuleLength && line.AsSpan().IndexOfAnyExcept('=') < 0;

	/// <summary>Splits "Name (License)" at the last parenthesis.</summary>
	private static LicenseNotice Create(string label, string details, string text)
	{
		var open = label.LastIndexOf(" (", StringComparison.Ordinal);
		return open > 0 && label.EndsWith(')')
			? new LicenseNotice(label[..open], label[(open + 2)..^1], details, text)
			: new LicenseNotice(label, "License", details, text);
	}
}
