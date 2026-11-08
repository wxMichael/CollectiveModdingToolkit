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
using System.Text;

namespace CollectiveModdingToolkit.Models;

/// <summary>
///     Reads the export table of a PE file (DLL) straight from disk, including the contents of exported data.
///     <para>
///         Nothing is loaded or executed, the file is not locked, and 32-bit DLLs read the same as 64-bit ones.
///         F4SE plugins export their version data as a data symbol (<c>F4SEPlugin_Version</c>), so reading the names alone
///         isn't enough.
///     </para>
/// </summary>
internal sealed class PeExportTable
{
	private const ushort MZ = 0x5A4D;
	private const uint PE00 = 0x00004550;
	private const ushort OptionalHeaderPE32 = 0x10B;
	private const ushort OptionalHeaderPE32Plus = 0x20B;
	private const int MaxExports = 100_000;
	private const int MaxNameLength = 512;
	private readonly Dictionary<string, uint> _exports;

	private readonly string _path;
	private readonly List<Section> _sections;

	private PeExportTable(string path, List<Section> sections, Dictionary<string, uint> exports)
	{
		_path = path;
		_sections = sections;
		_exports = exports;
	}

	/// <summary>Names of all exports that are real symbols (forwarders are excluded).</summary>
	public IReadOnlyCollection<string> Names => _exports.Keys;

	public bool Has(string name) => _exports.ContainsKey(name);

	/// <exception cref="IOException">The file couldn't be read.</exception>
	/// <exception cref="InvalidDataException">The file isn't a well-formed PE image.</exception>
	public static PeExportTable Load(string path)
	{
		// Games and mod managers may have the file open.
		using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

		var dos = ReadAt(stream, 0, 0x40);
		if (BinaryPrimitives.ReadUInt16LittleEndian(dos) != MZ)
			throw new InvalidDataException("Not a PE file (missing MZ header).");

		var peOffset = BinaryPrimitives.ReadInt32LittleEndian(dos.AsSpan(0x3C));
		if (peOffset <= 0 || peOffset > stream.Length - 24) throw new InvalidDataException("Invalid PE header offset.");

		var coff = ReadAt(stream, peOffset, 24);
		if (BinaryPrimitives.ReadUInt32LittleEndian(coff) != PE00)
			throw new InvalidDataException("Missing PE signature.");

		int sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(coff.AsSpan(6));
		int optionalHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(coff.AsSpan(20));
		long optionalOffset = peOffset + 24;
		if (sectionCount is 0 or > 96 || optionalHeaderSize < 96)
			throw new InvalidDataException("Invalid PE section or optional header size.");

		var optional = ReadAt(stream, optionalOffset, optionalHeaderSize);
		var magic = BinaryPrimitives.ReadUInt16LittleEndian(optional);
		int dataDirectoryOffset;
		int directoryCountOffset;
		switch (magic)
		{
			case OptionalHeaderPE32:
				directoryCountOffset = 92;
				dataDirectoryOffset = 96;
				break;
			case OptionalHeaderPE32Plus:
				directoryCountOffset = 108;
				dataDirectoryOffset = 112;
				break;
			default:
				throw new InvalidDataException($"Unknown optional header magic 0x{magic:X}.");
		}

		List<Section> sections = [];
		var sectionTable = ReadAt(stream, optionalOffset + optionalHeaderSize, sectionCount * 40);
		for (var i = 0; i < sectionCount; i++)
		{
			ReadOnlySpan<byte> s = sectionTable.AsSpan(i * 40, 40);
			sections.Add(new Section(
				VirtualSize: BinaryPrimitives.ReadUInt32LittleEndian(s[8..]),
				VirtualAddress: BinaryPrimitives.ReadUInt32LittleEndian(s[12..]),
				RawSize: BinaryPrimitives.ReadUInt32LittleEndian(s[16..]),
				RawPointer: BinaryPrimitives.ReadUInt32LittleEndian(s[20..])));
		}

		Dictionary<string, uint> exports = new(StringComparer.Ordinal);
		PeExportTable table = new(path, sections, exports);

		var directoryCount = BinaryPrimitives.ReadUInt32LittleEndian(optional.AsSpan(directoryCountOffset));
		if (directoryCount == 0 ||
		    optional.Length <
		    dataDirectoryOffset + 8) return table; // No data directories at all: valid, just no exports.

		var exportRva = BinaryPrimitives.ReadUInt32LittleEndian(optional.AsSpan(dataDirectoryOffset));
		var exportSize = BinaryPrimitives.ReadUInt32LittleEndian(optional.AsSpan(dataDirectoryOffset + 4));
		if (exportRva == 0 || exportSize == 0) return table; // e.g. a managed DLL or one that only imports.

		var directory = table.ReadRva(stream, exportRva, 40)
		                ?? throw new InvalidDataException("Export directory is outside any section.");
		var nameCount = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(24));
		var functionCount = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(20));
		var functionsRva = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(28));
		var namesRva = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(32));
		var ordinalsRva = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(36));
		if (nameCount > MaxExports || functionCount > MaxExports)
			throw new InvalidDataException("Implausible export count.");

		if (nameCount == 0) return table;

		var functions = table.ReadRva(stream, functionsRva, (int)functionCount * 4)
		                ?? throw new InvalidDataException("Export address table is outside any section.");
		var names = table.ReadRva(stream, namesRva, (int)nameCount * 4)
		            ?? throw new InvalidDataException("Export name table is outside any section.");
		var ordinals = table.ReadRva(stream, ordinalsRva, (int)nameCount * 2)
		               ?? throw new InvalidDataException("Export ordinal table is outside any section.");

		for (var i = 0; i < nameCount; i++)
		{
			var nameRva = BinaryPrimitives.ReadUInt32LittleEndian(names.AsSpan(i * 4));
			int ordinal = BinaryPrimitives.ReadUInt16LittleEndian(ordinals.AsSpan(i * 2));
			if (ordinal >= functionCount) continue;

			var functionRva = BinaryPrimitives.ReadUInt32LittleEndian(functions.AsSpan(ordinal * 4));
			// An address inside the export directory is a forwarder string ("OTHER.Func"), not code or data.
			if (functionRva >= exportRva && functionRva < exportRva + exportSize) continue;

			var name = table.ReadAsciiZ(stream, nameRva);
			if (!string.IsNullOrEmpty(name)) exports[name] = functionRva;
		}

		return table;
	}

	/// <summary>
	///     Reads <paramref name="length" /> bytes of an exported data symbol. Bytes beyond the section's file data
	///     (zero-initialized in memory) read as zero.
	/// </summary>
	/// <returns>Null if there's no such export or its address isn't inside the image.</returns>
	/// <exception cref="IOException">The file couldn't be read.</exception>
	public byte[]? ReadExportData(string name, int length)
	{
		if (!_exports.TryGetValue(name, out var rva)) return null;

		using FileStream stream = new(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		return ReadRva(stream, rva, length);
	}

	private byte[]? ReadRva(FileStream stream, uint rva, int length)
	{
		if (length < 0) return null;

		var result = new byte[length];
		if (length == 0) return result;

		var section = FindSection(rva);
		if (section is not { } s) return null;

		var offsetInSection = rva - s.VirtualAddress;
		// Only the part backed by file data can be read; the rest of the section's virtual size is zeros.
		var available = Math.Max(0, (long)s.RawSize - offsetInSection);
		var toRead = (int)Math.Min(length, available);
		if (toRead > 0)
		{
			var position = (long)s.RawPointer + offsetInSection;
			if (position + toRead > stream.Length)
				throw new InvalidDataException("Section data extends past end of file.");

			stream.Seek(position, SeekOrigin.Begin);
			stream.ReadExactly(result, 0, toRead);
		}

		return result;
	}

	private Section? FindSection(uint rva)
	{
		foreach (var s in _sections)
		{
			var size = Math.Max(s.VirtualSize, s.RawSize);
			if (rva >= s.VirtualAddress && rva - s.VirtualAddress < size) return s;
		}

		return null;
	}

	private string? ReadAsciiZ(FileStream stream, uint rva)
	{
		var section = FindSection(rva);
		if (section is not { } s) return null;

		var position = (long)s.RawPointer + (rva - s.VirtualAddress);
		var limit = (long)s.RawPointer + s.RawSize;
		if (position >= limit) return null;

		var count = (int)Math.Min(MaxNameLength, Math.Min(limit - position, stream.Length - position));
		if (count <= 0) return null;

		var buffer = ReadAt(stream, position, count);
		var end = Array.IndexOf(buffer, (byte)0);
		return end < 0 ? null : Encoding.ASCII.GetString(buffer, 0, end);
	}

	private static byte[] ReadAt(FileStream stream, long position, int count)
	{
		if (position < 0 || position + count > stream.Length)
			throw new InvalidDataException("PE structure extends past end of file.");

		var buffer = new byte[count];
		stream.Seek(position, SeekOrigin.Begin);
		stream.ReadExactly(buffer, 0, count);
		return buffer;
	}

	private readonly record struct Section(uint VirtualAddress, uint VirtualSize, uint RawSize, uint RawPointer);
}
