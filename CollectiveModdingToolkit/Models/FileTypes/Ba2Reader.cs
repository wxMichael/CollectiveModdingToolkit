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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace CollectiveModdingToolkit.Models.FileTypes;

/// <summary>A run of a file's data inside an archive. <see cref="PackedSize" /> is 0 when stored uncompressed.</summary>
internal readonly record struct Ba2Chunk(ulong Offset, uint PackedSize, uint UnpackedSize);

/// <summary>One file listed in a BA2's index.</summary>
internal sealed class Ba2Entry
{
	/// <summary>Path inside the archive, e.g. "textures\foo\bar_d.dds".</summary>
	public required string Path { get; init; }

	public required Ba2Chunk[] Chunks { get; init; }

	/// <summary>True for a DX10 archive's texture, which is stored without its DDS header.</summary>
	public bool IsTexture { get; init; }

	// Texture records only: the details needed to rebuild the DDS header.
	public ushort Height { get; init; }
	public ushort Width { get; init; }
	public byte MipCount { get; init; }
	public byte DxgiFormat { get; init; }
	public bool IsCubemap { get; init; }

	/// <summary>Size of the data once unpacked. For a texture this excludes the header.</summary>
	public long UnpackedSize
	{
		get
		{
			long total = 0;
			foreach (var chunk in Chunks) total += chunk.UnpackedSize;
			return total;
		}
	}
}

/// <summary>
///     Reads the file index of Fallout 4 BA2 archives (v1, v7, v8; zlib or uncompressed) without extracting anything.
/// </summary>
internal static class Ba2Reader
{
	private const int HeaderSize = 24;
	private const int GeneralRecordSize = 36;
	private const int TextureRecordSize = 24;

	// ReSharper disable once UnusedMember.Local
	private const int ChunkRecordSize = 24;

	/// <summary>Versions this reader understands. Starfield's v2/v3 have a different header and LZ4.</summary>
	public static bool IsSupportedVersion(uint version) => version is 1 or 7 or 8;

	/// <summary>Reads every file listed in the archive.</summary>
	/// <exception cref="InvalidDataException">The file isn't a supported, well-formed BA2.</exception>
	/// <exception cref="IOException">The file can't be read.</exception>
	public static List<Ba2Entry> Read(string path, CancellationToken token = default)
	{
		using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
			1 << 16);
		using BinaryReader reader = new(stream, Encoding.Latin1);

		var length = stream.Length;
		if (length < HeaderSize || !reader.ReadBytes(4).AsSpan().SequenceEqual(Magic.BTDX))
			throw new InvalidDataException("Not a BA2 archive.");

		var version = reader.ReadUInt32();
		if (!IsSupportedVersion(version)) throw new InvalidDataException($"Unsupported BA2 version {version}.");

		var type = reader.ReadBytes(4);
		bool texture;
		if (type.AsSpan().SequenceEqual(Magic.GNRL))
			texture = false;
		else if (type.AsSpan().SequenceEqual(Magic.DX10))
			texture = true;
		else
			throw new InvalidDataException("Unknown BA2 format.");

		var count = reader.ReadUInt32();
		var nameTableOffset = reader.ReadUInt64();

		var minimum = count * (texture ? TextureRecordSize : GeneralRecordSize);
		if (minimum > length - HeaderSize) throw new InvalidDataException("BA2 file count exceeds file size.");

		List<(Ba2Chunk[] Chunks, ushort Height, ushort Width, byte Mips, byte Format, bool Cube)> records =
			new((int)count);
		for (var i = 0; i < count; i++)
		{
			token.ThrowIfCancellationRequested();
			reader.ReadUInt32(); // name hash
			reader.ReadUInt32(); // extension
			reader.ReadUInt32(); // directory hash
			if (texture)
			{
				reader.ReadByte(); // unknown
				var chunkCount = reader.ReadByte();
				var chunkSize = reader.ReadUInt16();
				var height = reader.ReadUInt16();
				var width = reader.ReadUInt16();
				var mips = reader.ReadByte();
				var format = reader.ReadByte();
				var flags = reader.ReadUInt16();

				if (chunkSize < 20) throw new InvalidDataException("Invalid BA2 texture chunk size.");

				var chunks = new Ba2Chunk[chunkCount];
				for (var c = 0; c < chunkCount; c++)
				{
					var offset = reader.ReadUInt64();
					var packed = reader.ReadUInt32();
					var unpacked = reader.ReadUInt32();
					chunks[c] = new Ba2Chunk(offset, packed, unpacked);

					// Start/end mip and the BAADF00D sentinel, plus anything extra a future version adds.
					stream.Seek(chunkSize - 16, SeekOrigin.Current);
				}

				records.Add((chunks, height, width, mips, format, (flags & 1) != 0));
			}
			else
			{
				reader.ReadUInt32(); // flags
				var offset = reader.ReadUInt64();
				var packed = reader.ReadUInt32();
				var unpacked = reader.ReadUInt32();
				reader.ReadUInt32(); // BAADF00D
				records.Add(([new Ba2Chunk(offset, packed, unpacked)], 0, 0, 0, 0, false));
			}
		}

		List<Ba2Entry> entries = new(records.Count);
		if (nameTableOffset == 0 || nameTableOffset >= (ulong)length)
			throw new InvalidDataException("BA2 has no name table.");

		stream.Seek((long)nameTableOffset, SeekOrigin.Begin);
		foreach (var record in records)
		{
			token.ThrowIfCancellationRequested();
			var nameLength = reader.ReadUInt16();
			var name = new string(reader.ReadChars(nameLength));
			entries.Add(new Ba2Entry
			{
				Path = name,
				Chunks = record.Chunks,
				IsTexture = texture,
				Height = record.Height,
				Width = record.Width,
				MipCount = record.Mips,
				DxgiFormat = record.Format,
				IsCubemap = record.Cube,
			});
		}

		return entries;
	}

	/// <summary>
	///     Reads up to <paramref name="count" /> bytes from the start of a general entry's unpacked data.
	/// </summary>
	/// <returns>The bytes read, which may be fewer than requested if the entry is shorter.</returns>
	public static byte[] ReadStart(string path, Ba2Entry entry, int count)
	{
		if (entry.Chunks.Length == 0) return [];

		var chunk = entry.Chunks[0];
		using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
			4096);
		if (chunk.Offset >= (ulong)stream.Length)
			throw new InvalidDataException("Entry offset is past the end of the archive.");

		stream.Seek((long)chunk.Offset, SeekOrigin.Begin);
		var buffer = new byte[count];
		Stream source = chunk.PackedSize == 0 ? stream : new ZLibStream(stream, CompressionMode.Decompress);
		try
		{
			var read = source.ReadAtLeast(buffer, count, false);
			return read == count ? buffer : buffer[..read];
		}
		catch (InvalidDataException)
		{
			return [];
		}
		finally
		{
			if (!ReferenceEquals(source, stream)) source.Dispose();
		}
	}

	/// <summary>
	///     Builds the 148-byte DDS header for a texture stored in a DX10 archive.
	/// </summary>
	public static byte[] BuildDdsHeader(Ba2Entry entry)
	{
		const uint flags = 0x1 | 0x2 | 0x4 | 0x1000 | 0x20000; // CAPS | HEIGHT | WIDTH | PIXELFORMAT | MIPMAPCOUNT
		const uint caps = 0x1000 | 0x8 | 0x400000; // TEXTURE | COMPLEX | MIPMAP
		const uint cubeCaps2 = 0x200 | 0xFC00; // CUBEMAP | all six faces

		var header = new byte[148];
		Span<byte> span = header;
		Magic.DDS.CopyTo(span);
		BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 124);
		BinaryPrimitives.WriteUInt32LittleEndian(span[8..], flags);
		BinaryPrimitives.WriteUInt32LittleEndian(span[12..], entry.Height);
		BinaryPrimitives.WriteUInt32LittleEndian(span[16..], entry.Width);
		BinaryPrimitives.WriteUInt32LittleEndian(span[28..], entry.MipCount);
		BinaryPrimitives.WriteUInt32LittleEndian(span[76..], 32); // pixel format size
		BinaryPrimitives.WriteUInt32LittleEndian(span[80..], 0x4); // FOURCC
		Encoding.ASCII.GetBytes("DX10", span[84..]);
		BinaryPrimitives.WriteUInt32LittleEndian(span[108..], caps);
		BinaryPrimitives.WriteUInt32LittleEndian(span[112..], entry.IsCubemap ? cubeCaps2 : 0);
		BinaryPrimitives.WriteUInt32LittleEndian(span[128..], entry.DxgiFormat);
		BinaryPrimitives.WriteUInt32LittleEndian(span[132..], 3); // TEXTURE2D
		BinaryPrimitives.WriteUInt32LittleEndian(span[136..], entry.IsCubemap ? 0x4u : 0u);
		BinaryPrimitives.WriteUInt32LittleEndian(span[140..], 1); // array size
		return header;
	}
}
