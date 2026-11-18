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
using CollectiveModdingToolkit.Materials;

// ReSharper disable InconsistentNaming

namespace CollectiveModdingToolkit.Models.FileTypes;

/// <summary>
///     The subset of DXGI_FORMAT values commonly found in DDS textures. Other values are still carried through as raw
///     numbers.
/// </summary>
// ReSharper disable UnusedMember.Global
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
// ReSharper restore UnusedMember.Global

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

	/// <summary>The kind of pixel data, with uncompressed formats grouped by their channels.</summary>
	internal TextureFormatClass Format
	{
		get
		{
			if (_headerDx10 is { } dx10)
				return (uint)dx10.DXGIFormat switch
				{
					>= 70 and <= 72 => TextureFormatClass.Bc1,
					>= 73 and <= 75 => TextureFormatClass.Bc2,
					>= 76 and <= 78 => TextureFormatClass.Bc3,
					>= 79 and <= 81 => TextureFormatClass.Bc4,
					>= 82 and <= 84 => TextureFormatClass.Bc5,
					>= 94 and <= 96 => TextureFormatClass.Bc6H,
					>= 97 and <= 99 => TextureFormatClass.Bc7,
					// Uncompressed with alpha: 32/16-bit float and int RGBA, RGB10A2, RGBA8, A8, BGRA5551/8888/4444.
					>= 1 and <= 4 or >= 9 and <= 14 or >= 23 and <= 25 or >= 27 and <= 32 or 65 or 86 or 87 or 90
						or 91 or 115 => TextureFormatClass.Rgba,
					>= 5 and <= 8 or >= 35 and <= 39 or >= 48 and <= 52 or 85 or 88 or 92 or 93 =>
						TextureFormatClass.Rgb,
					41 or 54 or 56 or 61 => TextureFormatClass.SingleChannel,
					_ => TextureFormatClass.Unknown,
				};

			if ((_ddspf.Flags & DDPF_FOURCC) != 0)
				return _ddspf.FourCC switch
				{
					0x31545844 => TextureFormatClass.Bc1,
					0x32545844 or 0x33545844 => TextureFormatClass.Bc2,
					0x34545844 or 0x35545844 => TextureFormatClass.Bc3,
					0x31495441 or 0x55344342 or 0x53344342 => TextureFormatClass.Bc4,
					0x32495441 or 0x55354342 or 0x53354342 => TextureFormatClass.Bc5,
					_ => TextureFormatClass.Unknown,
				};

			const uint alphaPixels = 0x1, alphaOnly = 0x2, rgb = 0x40, luminance = 0x20000;
			if ((_ddspf.Flags & rgb) != 0)
				return (_ddspf.Flags & alphaPixels) != 0 ? TextureFormatClass.Rgba : TextureFormatClass.Rgb;

			return (_ddspf.Flags & (luminance | alphaOnly)) != 0
				? TextureFormatClass.SingleChannel
				: TextureFormatClass.Unknown;
		}
	}

	/// <summary>What a material needs to know about this texture.</summary>
	internal TextureFacts Facts => new(Format, IsCubemap);

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

		Parse(headerBytes[..Math.Max(0, bytesRead)]);
	}

	/// <summary>Reads the header from bytes already in memory, e.g. from inside an archive.</summary>
	internal void ReadHeader(ReadOnlySpan<byte> data)
	{
		ResetFields();
		if (data.Length > 0) ReadSignature(data);

		Parse(data[..Math.Min(data.Length, HeaderSize + HeaderDx10Size)]);
	}

	private void Parse(ReadOnlySpan<byte> headerBytes)
	{
		var bytesRead = headerBytes.Length;
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

	// ReSharper disable NotAccessedField.Local
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
	// ReSharper restore NotAccessedField.Local

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
