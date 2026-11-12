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
using System.IO;
using System.IO.Hashing;
using System.Security.Cryptography;
using System.Threading;
using ZstdSharp;

namespace CollectiveModdingToolkit.Delta;

/// <summary>
///     Makes patches. Only the generator tool needs this; the app only applies them.
///     <para>
///         The matching is Colin Percival's bsdiff 4 (suffix array by Larsson-Sadakane, then greedy approximate matching
///         with forward and backward extension). The output is stored in this project's own container, see
///         <see cref="DeltaFormat" />.
///     </para>
///     <para>
///         This is a C# port of code from bsdiff, Copyright 2003-2005 Colin Percival, used under its 2-clause BSD license.
///         The full notice is in THIRD-PARTY-NOTICES.md.
///     </para>
/// </summary>
public static class DeltaCreator
{
	private const int CompressionLevel = 22;

	/// <summary>
	///     The suffix sort recurses, and a 66 MB executable full of zero runs goes deeper than the default 1 MB stack.
	/// </summary>
	private const int StackBytes = 512 * 1024 * 1024;

	/// <summary>Creates a patch that turns <paramref name="oldData" /> into <paramref name="newData" />.</summary>
	public static byte[] Create(byte[] oldData, byte[] newData)
	{
		if (oldData.LongLength > DeltaFormat.MaxFileSize || newData.LongLength > DeltaFormat.MaxFileSize)
			throw new DeltaException("File is too large for a delta patch.");

		byte[]? patch = null;
		Exception? failure = null;
		Thread worker = new(() =>
		{
			try
			{
				patch = CreateCore(oldData, newData);
			}
			catch (Exception e)
			{
				failure = e;
			}
		}, StackBytes);
		worker.Start();
		worker.Join();
		return failure != null ? throw new DeltaException($"Creating the patch failed: {failure.Message}") : patch!;
	}

	private static byte[] CreateCore(byte[] old, byte[] @new)
	{
		MemoryStream control = new();
		MemoryStream diff = new();
		MemoryStream extra = new();
		Diff(old, @new, control, diff, extra);

		byte[][] raw = [control.ToArray(), diff.ToArray(), extra.ToArray()];
		using Compressor compressor = new(CompressionLevel);
		var compressed = new byte[raw.Length][];
		for (var i = 0; i < raw.Length; i++) compressed[i] = compressor.Wrap(raw[i]).ToArray();

		var length = DeltaFormat.HeaderSize;
		foreach (var part in compressed) length += part.Length;

		var patch = new byte[length];
		DeltaFormat.Magic.CopyTo(patch);
		patch[4] = DeltaFormat.Version;
		BinaryPrimitives.WriteInt64LittleEndian(patch.AsSpan(8), old.LongLength);
		BinaryPrimitives.WriteInt64LittleEndian(patch.AsSpan(16), @new.LongLength);
		BinaryPrimitives.WriteUInt32LittleEndian(patch.AsSpan(24), Crc32.HashToUInt32(old));
		BinaryPrimitives.WriteUInt32LittleEndian(patch.AsSpan(28), Crc32.HashToUInt32(@new));
		SHA256.HashData(@new).CopyTo(patch.AsSpan(32));

		var offset = DeltaFormat.HeaderSize;
		for (var i = 0; i < raw.Length; i++)
		{
			BinaryPrimitives.WriteInt64LittleEndian(patch.AsSpan(64 + i * 16), raw[i].LongLength);
			BinaryPrimitives.WriteInt64LittleEndian(patch.AsSpan(72 + i * 16), compressed[i].LongLength);
			compressed[i].CopyTo(patch, offset);
			offset += compressed[i].Length;
		}

		if (!DeltaApplier.Apply(patch, old).AsSpan().SequenceEqual(@new))
			throw new DeltaException("Internal error: the new patch does not reproduce the target file.");

		return patch;
	}

	private static void Diff(byte[] old, byte[] @new, Stream control, Stream diff, Stream extra)
	{
		var oldSize = old.Length;
		var newSize = @new.Length;
		var sa = BuildSuffixArray(old);

		int scan = 0, len = 0, pos = 0;
		int lastScan = 0, lastPos = 0, lastOffset = 0;
		while (scan < newSize)
		{
			var oldScore = 0;
			var scsc = scan += len;
			for (; scan < newSize; scan++)
			{
				len = Search(sa, old, @new, scan, 0, oldSize, out pos);
				for (; scsc < scan + len; scsc++)
					if (scsc + lastOffset < oldSize && old[scsc + lastOffset] == @new[scsc])
						oldScore++;

				if ((len == oldScore && len != 0) || len > oldScore + 8) break;

				if (scan + lastOffset < oldSize && old[scan + lastOffset] == @new[scan]) oldScore--;
			}

			if (len == oldScore && scan != newSize) continue;

			int s = 0, bestForward = 0, lenForward = 0;
			for (var i = 0; lastScan + i < scan && lastPos + i < oldSize;)
			{
				if (old[lastPos + i] == @new[lastScan + i]) s++;
				i++;
				if (s * 2 - i > bestForward * 2 - lenForward)
				{
					bestForward = s;
					lenForward = i;
				}
			}

			var lenBack = 0;
			if (scan < newSize)
			{
				s = 0;
				var bestBack = 0;
				for (var i = 1; scan >= lastScan + i && pos >= i; i++)
				{
					if (old[pos - i] == @new[scan - i]) s++;
					if (s * 2 - i > bestBack * 2 - lenBack)
					{
						bestBack = s;
						lenBack = i;
					}
				}
			}

			if (lastScan + lenForward > scan - lenBack)
			{
				var overlap = lastScan + lenForward - (scan - lenBack);
				s = 0;
				var bestSplit = 0;
				var lenSplit = 0;
				for (var i = 0; i < overlap; i++)
				{
					if (@new[lastScan + lenForward - overlap + i] == old[lastPos + lenForward - overlap + i]) s++;
					if (@new[scan - lenBack + i] == old[pos - lenBack + i]) s--;
					if (s > bestSplit)
					{
						bestSplit = s;
						lenSplit = i + 1;
					}
				}

				lenForward += lenSplit - overlap;
				lenBack -= lenSplit;
			}

			for (var i = 0; i < lenForward; i++) diff.WriteByte((byte)(@new[lastScan + i] - old[lastPos + i]));

			var extraLen = scan - lenBack - (lastScan + lenForward);
			extra.Write(@new, lastScan + lenForward, extraLen);

			WriteControl(control, lenForward);
			WriteControl(control, extraLen);
			WriteControl(control, pos - lenBack - (lastPos + lenForward));

			lastScan = scan - lenBack;
			lastPos = pos - lenBack;
			lastOffset = pos - scan;
		}
	}

	/// <summary>Writes one zigzag varint.</summary>
	private static void WriteControl(Stream stream, long value)
	{
		var v = (ulong)((value << 1) ^ (value >> 63));
		while (v >= 0x80)
		{
			stream.WriteByte((byte)(v | 0x80));
			v >>= 7;
		}

		stream.WriteByte((byte)v);
	}

	/// <summary>Finds the longest match for <c>new[scan..]</c> in <paramref name="old" /> by bisecting the suffix array.</summary>
	private static int Search(int[] sa, byte[] old, byte[] @new, int scan, int start, int end, out int pos)
	{
		while (end - start >= 2)
		{
			var mid = start + (end - start) / 2;
			var length = Math.Min(old.Length - sa[mid], @new.Length - scan);
			if (old.AsSpan(sa[mid], length).SequenceCompareTo(@new.AsSpan(scan, length)) < 0)
				start = mid;
			else
				end = mid;
		}

		var x = MatchLength(old, sa[start], @new, scan);
		var y = MatchLength(old, sa[end], @new, scan);
		if (x > y)
		{
			pos = sa[start];
			return x;
		}

		pos = sa[end];
		return y;
	}

	private static int MatchLength(byte[] old, int oldStart, byte[] @new, int newStart)
	{
		var length = Math.Min(old.Length - oldStart, @new.Length - newStart);
		return old.AsSpan(oldStart, length).CommonPrefixLength(@new.AsSpan(newStart, length));
	}

	// Suffix array construction: Larsson & Sadakane's qsufsort, as used by bsdiff.

	private static int[] BuildSuffixArray(byte[] old)
	{
		var size = old.Length;
		var sa = new int[size + 1];
		var rank = new int[size + 1];
		var buckets = new int[256];

		foreach (var b in old) buckets[b]++;

		for (var i = 1; i < 256; i++) buckets[i] += buckets[i - 1];

		for (var i = 255; i > 0; i--) buckets[i] = buckets[i - 1];

		buckets[0] = 0;

		for (var i = 0; i < size; i++) sa[++buckets[old[i]]] = i;

		sa[0] = size;
		for (var i = 0; i < size; i++) rank[i] = buckets[old[i]];

		rank[size] = 0;
		for (var i = 1; i < 256; i++)
			if (buckets[i] == buckets[i - 1] + 1)
				sa[buckets[i]] = -1;

		sa[0] = -1;

		for (var h = 1; sa[0] != -(size + 1); h += h)
		{
			var length = 0;
			var i = 0;
			while (i < size + 1)
				if (sa[i] < 0)
				{
					length -= sa[i];
					i -= sa[i];
				}
				else
				{
					if (length != 0) sa[i - length] = -length;

					length = rank[sa[i]] + 1 - i;
					Split(sa, rank, i, length, h);
					i += length;
					length = 0;
				}

			if (length != 0) sa[i - length] = -length;
		}

		for (var i = 0; i < size + 1; i++) sa[rank[i]] = i;

		return sa;
	}

	private static void Split(int[] sa, int[] rank, int start, int length, int h)
	{
		if (length < 16)
		{
			var k = start;
			while (k < start + length)
			{
				var j = 1;
				var x = rank[sa[k] + h];
				for (var i = 1; k + i < start + length; i++)
				{
					var value = rank[sa[k + i] + h];
					if (value < x)
					{
						x = value;
						j = 0;
					}

					if (value == x)
					{
						(sa[k + j], sa[k + i]) = (sa[k + i], sa[k + j]);
						j++;
					}
				}

				for (var i = 0; i < j; i++) rank[sa[k + i]] = k + j - 1;

				if (j == 1) sa[k] = -1;

				k += j;
			}

			return;
		}

		var pivot = rank[sa[start + length / 2] + h];
		var less = 0;
		var equal = 0;
		for (var i = start; i < start + length; i++)
		{
			var value = rank[sa[i] + h];
			if (value < pivot) less++;
			if (value == pivot) equal++;
		}

		var lessEnd = start + less;
		var equalEnd = lessEnd + equal;

		var a = start;
		var b = 0;
		var c = 0;
		while (a < lessEnd)
		{
			var value = rank[sa[a] + h];
			if (value < pivot)
			{
				a++;
			}
			else if (value == pivot)
			{
				(sa[a], sa[lessEnd + b]) = (sa[lessEnd + b], sa[a]);
				b++;
			}
			else
			{
				(sa[a], sa[equalEnd + c]) = (sa[equalEnd + c], sa[a]);
				c++;
			}
		}

		while (lessEnd + b < equalEnd)
			if (rank[sa[lessEnd + b] + h] == pivot)
			{
				b++;
			}
			else
			{
				(sa[lessEnd + b], sa[equalEnd + c]) = (sa[equalEnd + c], sa[lessEnd + b]);
				c++;
			}

		if (lessEnd > start) Split(sa, rank, start, lessEnd - start, h);

		for (var i = 0; i < equalEnd - lessEnd; i++) rank[sa[lessEnd + i]] = equalEnd - 1;

		if (lessEnd == equalEnd - 1) sa[lessEnd] = -1;

		if (start + length > equalEnd) Split(sa, rank, equalEnd, start + length - equalEnd, h);
	}
}
