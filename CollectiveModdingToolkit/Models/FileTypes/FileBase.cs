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
using System.Text.RegularExpressions;

namespace CollectiveModdingToolkit.Models.FileTypes;

public abstract partial class FileBase(string name, string pathFull, FileOrigin? origin) : IFile
{
	[GeneratedRegex(@"\.{2,}")] private static partial Regex CollapsePeriodsRegex { get; }

	/// <summary>The first 12 bytes of the file decoded as text. Null if not read or nothing printable was found.</summary>
	public string? Signature { get; private set; }

	public string Name { get; } = name;
	public string PathFull { get; } = pathFull;

	public bool? IsReadable { get; private protected set; }

	public string? Mod => Origin is { IsGameFolder: false } ? Origin.ModName : null;

	public FileOrigin? Origin { get; } = origin;

	private protected void ReadSignature(ReadOnlySpan<byte> headerBytes)
	{
		var bytes = headerBytes[..Math.Min(12, headerBytes.Length)];
		var chars = string.Create(bytes.Length, bytes.ToArray(),
			static (span, src) =>
			{
				for (var i = 0; i < span.Length; i++)
					span[i] = src[i] is >= 32 and < 127 ? (char)src[i] : '.';
			});
		var signature = CollapsePeriodsRegex.Replace(chars, ".").Trim('.');
		Signature = signature.Length > 0 ? signature : null;
	}
}
