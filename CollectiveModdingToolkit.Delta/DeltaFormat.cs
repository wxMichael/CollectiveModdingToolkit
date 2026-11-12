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
using System.Buffers.Binary;

namespace CollectiveModdingToolkit.Delta;

/// <summary>A patch is damaged, was made for a different file, or could not be applied.</summary>
public sealed class DeltaException(string message) : Exception(message);

/// <summary>What a patch file says about the file it patches and the file it makes.</summary>
public sealed record DeltaInfo(long OldSize, long NewSize, uint OldCrc, uint NewCrc, byte[] NewSha256);

/// <summary>
///     The <c>.cmdelta</c> patch format: a bsdiff-style delta, compressed with zstd.
///     <para>
///         bsdiff matches the new file against the old one at approximate offsets and stores the byte-wise
///         <i>difference</i> instead of copying literal runs. In executables a moved function or a shifted pointer table
///         becomes a long run of zeros plus a few small values, which compresses to almost nothing. That is why it beats
///         copy/insert encoders such as xdelta3 on code. The three streams (control, diff, extra) are compressed
///         separately so each is homogeneous.
///     </para>
///     <para>
///         Layout, little-endian: <c>"CMDL"</c>, version byte, 3 reserved bytes, old size (8), new size (8), old CRC32
///         (4),
///         new CRC32 (4), new SHA-256 (32), then for each of control, diff and extra: raw length (8) and compressed
///         length (8). The zstd frames follow in that order.
///     </para>
/// </summary>
public static class DeltaFormat
{
	public const string Extension = ".cmdelta";

	/// <summary>Largest file either side may be. Fallout 4's biggest executable is about 66 MB.</summary>
	public const long MaxFileSize = 1L << 30;

	internal const byte Version = 1;
	internal const int StreamCount = 3;
	internal const int HeaderSize = 4 + 1 + 3 + 8 + 8 + 4 + 4 + 32 + StreamCount * 16;
	internal static ReadOnlySpan<byte> Magic => "CMDL"u8;

	/// <summary>Reads the header only, so callers can check the source file before decompressing anything.</summary>
	public static DeltaInfo ReadInfo(ReadOnlySpan<byte> patch) => ReadHeader(patch, out _);

	internal static DeltaInfo ReadHeader(ReadOnlySpan<byte> patch, out (long Raw, long Compressed)[] streams)
	{
		if (patch.Length < HeaderSize || !patch[..4].SequenceEqual(Magic))
			throw new DeltaException("Not a delta patch.");

		if (patch[4] != Version) throw new DeltaException($"Unsupported patch version {patch[4]}.");

		var oldSize = BinaryPrimitives.ReadInt64LittleEndian(patch[8..]);
		var newSize = BinaryPrimitives.ReadInt64LittleEndian(patch[16..]);
		if (oldSize is < 0 or > MaxFileSize || newSize is < 0 or > MaxFileSize)
			throw new DeltaException("Patch describes an implausible file size.");

		var oldCrc = BinaryPrimitives.ReadUInt32LittleEndian(patch[24..]);
		var newCrc = BinaryPrimitives.ReadUInt32LittleEndian(patch[28..]);
		var sha = patch.Slice(32, 32).ToArray();

		streams = new (long, long)[StreamCount];
		long total = HeaderSize;
		for (var i = 0; i < StreamCount; i++)
		{
			var raw = BinaryPrimitives.ReadInt64LittleEndian(patch[(64 + i * 16)..]);
			var compressed = BinaryPrimitives.ReadInt64LittleEndian(patch[(72 + i * 16)..]);
			// Control is a few bytes per record; diff and extra can't exceed the new file.
			var limit = i == 0 ? MaxFileSize * 4 : newSize;
			if (raw < 0 || raw > limit || compressed < 0 || compressed > patch.Length)
				throw new DeltaException("Patch is damaged (bad stream length).");

			streams[i] = (raw, compressed);
			total += compressed;
		}

		if (total != patch.Length) throw new DeltaException("Patch is damaged (wrong length).");

		return new DeltaInfo(oldSize, newSize, oldCrc, newCrc, sha);
	}
}
