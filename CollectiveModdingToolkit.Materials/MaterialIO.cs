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
using System.Text;

namespace CollectiveModdingToolkit.Materials;

/// <summary>Reads the little-endian values materials are made of.</summary>
internal sealed class MaterialReader(ReadOnlySpan<byte> data)
{
	private readonly byte[] _data = data.ToArray();
	private int _position;

	public int Remaining => _data.Length - _position;

	public ReadOnlySpan<byte> ReadBytes(int count)
	{
		if (count < 0 || count > Remaining) throw new EndOfStreamException();

		var span = _data.AsSpan(_position, count);
		_position += count;
		return span;
	}

	public byte Byte() => ReadBytes(1)[0];

	// Bools are a whole byte; anything non-zero counts, as in the game.
	public bool Bool() => Byte() != 0;

	public uint UInt32() => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(4));

	public float Single() => BinaryPrimitives.ReadSingleLittleEndian(ReadBytes(4));

	public MaterialColor Color() => new(Single(), Single(), Single());

	/// <summary>A length-prefixed string. The length includes a terminating null, which isn't returned.</summary>
	public string String()
	{
		var length = UInt32();
		if (length > Remaining) throw new EndOfStreamException();

		var bytes = ReadBytes((int)length);
		var end = bytes.IndexOf((byte)0);
		return Encoding.Latin1.GetString(end >= 0 ? bytes[..end] : bytes);
	}
}

/// <summary>Writes the little-endian values materials are made of.</summary>
internal sealed class MaterialWriter(Stream stream) : IDisposable
{
	private readonly BinaryWriter _writer = new(stream, Encoding.Latin1, true);

	public void Dispose() => _writer.Dispose();

	public void Raw(ReadOnlySpan<byte> bytes) => _writer.Write(bytes);

	public void Byte(byte value) => _writer.Write(value);

	public void Bool(bool value) => _writer.Write(value);

	public void UInt32(uint value) => _writer.Write(value);

	public void Single(float value) => _writer.Write(value);

	public void Color(MaterialColor value)
	{
		Single(value.R);
		Single(value.G);
		Single(value.B);
	}

	public void String(string? value)
	{
		var bytes = Encoding.Latin1.GetBytes(value ?? string.Empty);
		UInt32((uint)bytes.Length + 1);
		_writer.Write(bytes);
		_writer.Write((byte)0);
	}
}
