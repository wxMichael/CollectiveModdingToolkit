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
using System.IO;
using System.IO.Hashing;
using System.Security.Cryptography;
using ZstdSharp;

namespace CollectiveModdingToolkit.Delta;

public static class DeltaApplier
{
	/// <summary>
	///     Applies <paramref name="patch" /> to <paramref name="oldData" /> and returns the new file. Checks the source and
	///     the result against the sizes, CRC32, and SHA-256 recorded in the patch, so a wrong or corrupt file never produces
	///     output.
	/// </summary>
	/// <exception cref="DeltaException">The patch is damaged or does not fit <paramref name="oldData" />.</exception>
	public static byte[] Apply(byte[] patch, byte[] oldData)
	{
		var info = DeltaFormat.ReadHeader(patch, out var streams);
		if (oldData.LongLength != info.OldSize || Crc32.HashToUInt32(oldData) != info.OldCrc)
			throw new DeltaException("This patch was made for a different version of the file.");

		var offset = DeltaFormat.HeaderSize;
		var data = new byte[DeltaFormat.StreamCount][];
		using Decompressor decompressor = new();
		for (var i = 0; i < data.Length; i++)
		{
			var (raw, compressed) = streams[i];
			data[i] = new byte[raw];
			int written;
			try
			{
				written = decompressor.Unwrap(patch.AsSpan(offset, (int)compressed), data[i]);
			}
			catch (Exception e) when (e is ZstdException or InvalidOperationException or ArgumentException)
			{
				throw new DeltaException("Patch is damaged (decompression failed).");
			}

			if (written != raw) throw new DeltaException("Patch is damaged (wrong stream size).");

			offset += (int)compressed;
		}

		var result = Reconstruct(data[0], data[1], data[2], oldData, info.NewSize);
		if (Crc32.HashToUInt32(result) != info.NewCrc ||
		    !SHA256.HashData(result).AsSpan().SequenceEqual(info.NewSha256))
			throw new DeltaException("The patched file failed verification.");

		return result;
	}

	/// <summary>
	///     Patches <paramref name="oldPath" /> into <paramref name="newPath" />. Nothing is written unless the result
	///     verifies.
	/// </summary>
	public static void ApplyFile(string patchPath, string oldPath, string newPath)
	{
		var patch = File.ReadAllBytes(patchPath);
		var info = DeltaFormat.ReadInfo(patch);
		if (new FileInfo(oldPath).Length != info.OldSize)
			throw new DeltaException("This patch was made for a different version of the file.");

		var result = Apply(patch, File.ReadAllBytes(oldPath));
		var part = newPath + ".part";
		try
		{
			File.WriteAllBytes(part, result);
			File.Move(part, newPath, true);
		}
		finally
		{
			if (File.Exists(part)) File.Delete(part);
		}
	}

	private static byte[] Reconstruct(byte[] control, byte[] diff, byte[] extra, byte[] old, long newSize)
	{
		var result = new byte[newSize];
		long newPos = 0, oldPos = 0, controlPos = 0, diffPos = 0, extraPos = 0;
		while (newPos < newSize)
		{
			var diffLen = NextControl(control, ref controlPos);
			var extraLen = NextControl(control, ref controlPos);
			var seek = NextControl(control, ref controlPos);

			if (diffLen < 0 || extraLen < 0 || diffLen > newSize - newPos || diffLen > diff.Length - diffPos
			    || extraLen > newSize - newPos - diffLen || extraLen > extra.Length - extraPos
			    || Math.Abs(seek) > DeltaFormat.MaxFileSize * 2)
				throw new DeltaException("Patch is damaged (bad control data).");

			for (long i = 0; i < diffLen; i++)
			{
				var b = diff[diffPos + i];
				var o = oldPos + i;
				if ((ulong)o < (ulong)old.Length) b += old[o];
				result[newPos + i] = b;
			}

			newPos += diffLen;
			oldPos += diffLen;
			diffPos += diffLen;

			Buffer.BlockCopy(extra, (int)extraPos, result, (int)newPos, (int)extraLen);
			newPos += extraLen;
			extraPos += extraLen;
			oldPos += seek;
		}

		if (controlPos != control.Length || diffPos != diff.Length || extraPos != extra.Length)
			throw new DeltaException("Patch is damaged (unused data).");

		return result;
	}

	/// <summary>Reads one zigzag varint.</summary>
	private static long NextControl(byte[] control, ref long pos)
	{
		ulong value = 0;
		for (var shift = 0; shift < 64; shift += 7)
		{
			if (pos >= control.Length) throw new DeltaException("Patch is damaged (truncated control data).");

			var b = control[pos++];
			value |= (ulong)(b & 0x7F) << shift;
			if ((b & 0x80) == 0) return (long)(value >> 1) ^ -(long)(value & 1);
		}

		throw new DeltaException("Patch is damaged (bad control data).");
	}
}
