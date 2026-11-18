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

// ReSharper disable InconsistentNaming

namespace CollectiveModdingToolkit.Models.FileTypes;

/// <summary>
///     The subset of DXGI_FORMAT values commonly found in DDS textures. Other values are still carried through as raw
///     numbers.
/// </summary>
internal enum DXGI_FORMAT : uint
{
	UNKNOWN = 0,
	R32G32B32A32_FLOAT = 2,
	R16G16B16A16_FLOAT = 10,
	R16G16B16A16_UNORM = 11,
	R10G10B10A2_UNORM = 24,
	R8G8B8A8_UNORM = 28,
	R8G8B8A8_UNORM_SRGB = 29,
	R16_UNORM = 56,
	R8_UNORM = 61,
	A8_UNORM = 65,
	BC1_UNORM = 71,
	BC1_UNORM_SRGB = 72,
	BC2_UNORM = 74,
	BC2_UNORM_SRGB = 75,
	BC3_UNORM = 77,
	BC3_UNORM_SRGB = 78,
	BC4_UNORM = 80,
	BC4_SNORM = 81,
	BC5_UNORM = 83,
	BC5_SNORM = 84,
	B5G6R5_UNORM = 85,
	B5G5R5A1_UNORM = 86,
	B8G8R8A8_UNORM = 87,
	B8G8R8X8_UNORM = 88,
	B8G8R8A8_UNORM_SRGB = 91,
	BC6H_UF16 = 95,
	BC6H_SF16 = 96,
	BC7_UNORM = 98,
	BC7_UNORM_SRGB = 99,
}

public class DDS(string name, string pathFull, FileOrigin? origin) : FileBase(name, pathFull, origin)
{
	private const uint DDPF_FOURCC = 0x4;
	private const uint DDSCAPS2_CUBEMAP = 0x200;
	private const uint FourCC_DX10 = 0x30315844; // "DX10" little-endian

	/// <summary>Magic (4) + DDS_HEADER (124).</summary>
	private const int HeaderSize = 128;

	/// <summary>Size of the optional DDS_HEADER_DXT10 that follows the header when FourCC is "DX10".</summary>
	private const int HeaderDx10Size = 20;

	/// <summary>The DX10 extension header, or null if this file has none.</summary>
	private DDS_HEADER_DXT10? _headerDx10;

	/// <summary>Whether the pixel format declares the DX10 extension header.</summary>
	private bool HasDx10Header => (_ddspf.Flags & DDPF_FOURCC) != 0 && _ddspf.FourCC == FourCC_DX10;

	/// <summary>Whether the texture is a cubemap, per the legacy caps or the DX10 header.</summary>
	internal bool IsCubemap =>
		(Caps2 & DDSCAPS2_CUBEMAP) != 0 || (_headerDx10 is { } dx10 && (dx10.MiscFlag & 0x4) != 0);

	/// <summary>Whether the file has a DX10 extension header.</summary>
	internal bool HasExtendedHeader => _headerDx10 != null;

	/// <summary>The DXGI format from the DX10 header, or null if there is none.</summary>
	internal uint? DxgiFormat => _headerDx10 is { } dx10 ? (uint)dx10.DXGIFormat : null;

	/// <summary>Number of array slices from the DX10 header (cubemaps count as one slice per cube), or 1.</summary>
	internal uint ArraySize => _headerDx10 is { } dx10 ? Math.Max(1, dx10.ArraySize) : 1;

	/// <summary>Whether the texture is a 3D volume, per the legacy caps or the DX10 header.</summary>
	internal bool IsVolume => (Caps2 & 0x200000) != 0 || _headerDx10 is { ResourceDimension: 4 };

	/// <summary>The legacy pixel format flags.</summary>
	internal uint PixelFormatFlags => _ddspf.Flags;

	/// <summary>The legacy pixel format FourCC.</summary>
	internal uint FourCC => _ddspf.FourCC;

	/// <summary>The legacy pixel format bit count for uncompressed formats.</summary>
	internal uint RGBBitCount => _ddspf.RGBBitCount;

	/// <summary>Size of the header(s) preceding the pixel data.</summary>
	internal int DataOffset => HeaderSize + (HasDx10Header ? HeaderDx10Size : 0);

	/// <summary>Whether the pixel data is block-compressed (BC1-BC7, or the legacy DXTn/ATIn FourCCs).</summary>
	internal bool IsBlockCompressed
	{
		get
		{
			if (_headerDx10 is { } dx10)
			{
				var format = (uint)dx10.DXGIFormat;
				return format is >= 70 and <= 84 or >= 94 and <= 99;
			}

			if ((_ddspf.Flags & DDPF_FOURCC) == 0) return false;

			return _ddspf.FourCC is
				0x31545844 or 0x32545844 or 0x33545844 or 0x34545844 or 0x35545844 // DXT1-5
				or 0x31495441 or 0x32495441 // ATI1, ATI2
				or 0x55344342 or 0x53344342 or 0x55354342 or 0x53354342; // BC4U, BC4S, BC5U, BC5S
		}
	}

	internal void ReadHeader()
	{
		ResetFields();

		var headerBytes = new byte[HeaderSize + HeaderDx10Size].AsSpan();
		int bytesRead;
		try
		{
			using var file = File.OpenRead(this.RealPath);
			bytesRead = file.ReadAtLeast(headerBytes, HeaderSize, false);

			if (bytesRead > 0) ReadSignature(headerBytes);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			IsReadable = false;
			return;
		}

		if (bytesRead < HeaderSize || !headerBytes[..4].SequenceEqual(Magic.DDS) ||
		    BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[4..]) != 124)
		{
			IsReadable = false;
			return;
		}

		Flags = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[8..]);
		Height = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[12..]);
		Width = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[16..]);
		PitchOrLinearSize = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[20..]);
		Depth = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[24..]);
		MipMapCount = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[28..]);

		for (var i = 0; i < 11; i++)
			_reserved1[i] = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[(32 + i * 4)..]);

		_ddspf = new DDSPixelFormat
		{
			Size = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[76..]),
			Flags = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[80..]),
			FourCC = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[84..]),
			RGBBitCount = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[88..]),
			RBitMask = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[92..]),
			GBitMask = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[96..]),
			BBitMask = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[100..]),
			ABitMask = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[104..]),
		};

		Caps = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[108..]);
		Caps2 = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[112..]);
		Caps3 = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[116..]);
		Caps4 = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[120..]);
		Reserved2 = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes[124..]);

		if (HasDx10Header)
		{
			if (bytesRead < HeaderSize + HeaderDx10Size)
			{
				ResetFields();
				IsReadable = false;
				return;
			}

			var dx10 = headerBytes[HeaderSize..];
			_headerDx10 = new DDS_HEADER_DXT10
			{
				DXGIFormat = (DXGI_FORMAT)BinaryPrimitives.ReadUInt32LittleEndian(dx10),
				ResourceDimension = BinaryPrimitives.ReadUInt32LittleEndian(dx10[4..]),
				MiscFlag = BinaryPrimitives.ReadUInt32LittleEndian(dx10[8..]),
				ArraySize = BinaryPrimitives.ReadUInt32LittleEndian(dx10[12..]),
				MiscFlags2 = BinaryPrimitives.ReadUInt32LittleEndian(dx10[16..]),
			};
		}

		IsReadable = true;
	}

	private void ResetFields()
	{
		Flags = Height = Width = PitchOrLinearSize = Depth = MipMapCount = 0;
		Array.Clear(_reserved1);
		_ddspf = default;
		Caps = Caps2 = Caps3 = Caps4 = Reserved2 = 0;
		_headerDx10 = null;
	}

	private struct DDSPixelFormat
	{
		internal uint Size;
		internal uint Flags;
		internal uint FourCC;
		internal uint RGBBitCount;
		internal uint RBitMask;
		internal uint GBitMask;
		internal uint BBitMask;
		internal uint ABitMask;
	}

	private struct DDS_HEADER_DXT10
	{
		internal DXGI_FORMAT DXGIFormat;
		internal uint ResourceDimension;
		internal uint MiscFlag; // see D3D11_RESOURCE_MISC_FLAG
		internal uint ArraySize;
		internal uint MiscFlags2;
	}

	// Header fields in file order.
	// @formatter:off
	internal uint Flags;
	internal uint Height;
	internal uint Width;
	internal uint PitchOrLinearSize;
	internal uint Depth;
	internal uint MipMapCount;
	private readonly uint[] _reserved1 = new uint[11]; // dwReserved1[11]
	private DDSPixelFormat _ddspf;
	internal uint Caps;
	internal uint Caps2;
	internal uint Caps3;
	internal uint Caps4;
	internal uint Reserved2;
	// @formatter:on
}
