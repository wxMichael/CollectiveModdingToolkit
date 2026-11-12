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
using System.IO;
using System.IO.Hashing;

namespace CollectiveModdingToolkit.Delta;

/// <summary>A game edition the Downgrader can patch to, or a state a file can be in.</summary>
public enum Edition
{
	/// <summary>The file doesn't match any known version.</summary>
	Unknown,

	NotFound,

	/// <summary>A known version that is neither Old-Gen, Next-Gen nor Anniversary. Not patchable.</summary>
	Obsolete,

	OldGen,
	NextGen,
	Anniversary,

	/// <summary>A file that is byte-identical in Next-Gen and Anniversary (<c>steam_api64.dll</c>).</summary>
	NextGenAnniversary,
}

/// <summary>A file the Downgrader replaces, with the CRC32 of every known version of it.</summary>
public sealed record TrackedFile(string RelativePath, bool IsCreationKit, IReadOnlyDictionary<uint, Edition> Crcs)
{
	public string Name => Path.GetFileName(RelativePath);

	/// <summary>Which edition a file with this CRC32 belongs to.</summary>
	public Edition Identify(uint crc) => Crcs.TryGetValue(crc, out var edition) ? edition : Edition.Unknown;
}

/// <summary>
///     Every file the Downgrader replaces and every known CRC32 of each.
///     <para>
///         <b>When the game updates</b>: move the old current CRC of each changed file to <see cref="Edition.Obsolete" />,
///         add the new CRC as its edition, then regenerate patches with <c>DeltaTool</c>. Patches only exist for files
///         that
///         are Old-Gen, Next-Gen or Anniversary, so files from an obsolete version are skipped as "Unsupported Version".
///     </para>
/// </summary>
public static class GameFiles
{
	private const Edition Ob = Edition.Obsolete;
	private const Edition OG = Edition.OldGen;
	private const Edition NG = Edition.NextGen;
	private const Edition AE = Edition.Anniversary;
	private const Edition NGAE = Edition.NextGenAnniversary;

	/// <summary>Editions patches are generated between.</summary>
	public static IReadOnlyList<Edition> Supported { get; } = [OG, NG, AE];

	/// <summary>In display order: game files, then Creation Kit files.</summary>
	public static IReadOnlyList<TrackedFile> All { get; } =
	[
		new("Fallout4.exe", false, new Dictionary<uint, Edition>
		{
			[0x97DA3E03] = Ob, // 1.10.120
			[0x2ED2A242] = Ob, // 1.10.130
			[0xA0100017] = Ob, // 1.10.138
			[0x9ABC94F0] = Ob, // 1.10.162
			[0xC6053902] = OG, // 1.10.163
			[0xB61675B1] = Ob, // 1.10.980
			[0xC5965A2E] = NG, // 1.10.984
			[0x0AEB19A7] = Ob, // 1.11.137
			[0x1E90BE57] = Ob, // 1.11.159
			[0x0481725D] = Ob, // 1.11.169a
			[0x0E176ABC] = Ob, // 1.11.169b
			[0xCF47788D] = Ob, // 1.11.191
			[0x67545AFB] = AE, // 1.11.240
		}),
		new("Fallout4Launcher.exe", false, new Dictionary<uint, Edition>
		{
			[0x02445570] = OG, // 1.10.120 to 1.10.163
			[0xF6A06FF5] = NG, // 1.10.980 to 1.10.984
			[0x0E696744] = Ob, // 1.11.137
			[0xD15C6A49] = Ob, // 1.11.159
			[0x8C52BE93] = Ob, // 1.11.169a
			[0x591009C9] = Ob, // 1.11.169b
			[0x720BB9C3] = Ob, // 1.11.191
			[0xCA61EDD7] = Ob, // 1.11.221
			[0x57D1C794] = AE, // 1.11.240
		}),
		new("steam_api64.dll", false, new Dictionary<uint, Edition>
		{
			[0xBBD912FC] = OG,
			[0xE36E7B4D] = NGAE,
		}),
		new("CreationKit.exe", true, new Dictionary<uint, Edition>
		{
			[0x0F5C065B] = OG, // 1.10.163
			[0x481CCE95] = NG, // 1.10.984
			[0x49E45284] = Ob, // 1.11.191
			[0x3E8BBBE7] = AE, // 1.11.240
		}),
		new(Path.Combine("Tools", "Archive2", "Archive2.exe"), true, new Dictionary<uint, Edition>
		{
			[0x4CDFC7B5] = OG, // 1.10.163
			[0x71A5240B] = NG, // 1.10.984
			[0xC867674F] = Ob, // 1.11.191
			[0xB2EEF34A] = AE, // 1.11.240
		}),
		new(Path.Combine("Tools", "Archive2", "Archive2Interop.dll"), true, new Dictionary<uint, Edition>
		{
			[0x850D36A9] = OG, // 1.10.163
			[0xEFBE3622] = NG, // 1.10.984
			[0x7B893B0D] = Ob, // 1.11.191
			[0x2690FD20] = AE, // 1.11.240
		}),
	];

	/// <summary>
	///     Whether a file with this CRC32 is a valid copy of <paramref name="edition" />. A file shared by Next-Gen and
	///     Anniversary counts for both.
	/// </summary>
	public static bool IsEdition(TrackedFile file, uint crc, Edition edition) =>
		file.Crcs.TryGetValue(crc, out var found)
		&& (found == edition || (found == NGAE && edition is NG or AE));

	/// <summary>CRC32 of a whole file, streamed so a 66 MB executable is never held in memory.</summary>
	public static uint Crc32OfFile(string path)
	{
		Crc32 crc = new();
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16,
			FileOptions.SequentialScan);
		crc.Append(stream);
		return crc.GetCurrentHashAsUInt32();
	}

	/// <summary>
	///     The short name used in patch file names and backup names: <c>OG</c>, <c>NG</c>, <c>AE</c>.
	/// </summary>
	public static string Code(Edition edition) => edition switch
	{
		OG => "OG",
		NG => "NG",
		AE => "AE",
		_ => throw new ArgumentOutOfRangeException(nameof(edition), edition, "Not a patchable edition."),
	};

	/// <summary>File name of the patch that turns <paramref name="from" /> into <paramref name="to" />.</summary>
	public static string PatchFileName(Edition from, Edition to, TrackedFile file) =>
		$"{Code(from)}-to-{Code(to)}-{file.Name}{DeltaFormat.Extension}";
}
