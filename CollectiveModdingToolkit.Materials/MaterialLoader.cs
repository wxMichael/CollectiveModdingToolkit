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
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace CollectiveModdingToolkit.Materials;

/// <summary>What a file's contents turned out to be.</summary>
public enum MaterialFormat
{
	/// <summary>Zero bytes.</summary>
	Empty,

	/// <summary>A binary BGSM or BGEM.</summary>
	Binary,

	/// <summary>Text. Fallout 4 reads JSON materials, but they are slower to load and larger than binary ones.</summary>
	Json,

	/// <summary>Neither of the above.</summary>
	Unknown,
}

/// <summary>What <see cref="MaterialLoader.Inspect" /> found out about a file without fully reading it.</summary>
public readonly record struct MaterialHeader(MaterialFormat Format, MaterialKind? Kind, uint? Version);

/// <summary>Detects, reads, and writes BGSM and BGEM material files.</summary>
public static class MaterialLoader
{
	public static readonly string[] Extensions = [".bgsm", ".bgem"];
	private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

	/// <summary>The kind a file name's extension promises, or null if it isn't a material extension.</summary>
	public static MaterialKind? KindFromExtension(string fileName) =>
		fileName.EndsWith(".bgsm", StringComparison.OrdinalIgnoreCase) ? MaterialKind.Bgsm :
		fileName.EndsWith(".bgem", StringComparison.OrdinalIgnoreCase) ? MaterialKind.Bgem : null;

	/// <summary>Looks at the start of a file to say what it holds. Cheap; use for scanning.</summary>
	public static MaterialHeader Inspect(ReadOnlySpan<byte> data)
	{
		if (data.Length == 0) return new MaterialHeader(MaterialFormat.Empty, null, null);

		if (data.Length >= 4)
		{
			MaterialKind? kind = data[..4].SequenceEqual("BGSM"u8) ? MaterialKind.Bgsm
				: data[..4].SequenceEqual("BGEM"u8) ? MaterialKind.Bgem : null;
			if (kind != null)
				return new MaterialHeader(MaterialFormat.Binary, kind,
					data.Length >= 8 ? BitConverter.ToUInt32(data[4..8]) : null);
		}

		return LooksLikeJson(data)
			? new MaterialHeader(MaterialFormat.Json, null, null)
			: new MaterialHeader(MaterialFormat.Unknown, null, null);
	}

	private static bool LooksLikeJson(ReadOnlySpan<byte> data)
	{
		if (data.StartsWith(Utf8Bom)) data = data[3..];

		// ParseJson skips comments, so they can come before the first brace.
		for (var i = 0; i < data.Length; i++)
		{
			var b = data[i];
			if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') continue;

			if (b == (byte)'/' && i + 1 < data.Length)
			{
				if (data[i + 1] == (byte)'/')
				{
					var end = data[(i + 2)..].IndexOf((byte)'\n');
					if (end < 0) return false;

					i += 2 + end;
					continue;
				}

				if (data[i + 1] == (byte)'*')
				{
					var end = data[(i + 2)..].IndexOf("*/"u8);
					if (end < 0) return false;

					i += 3 + end;
					continue;
				}
			}

			return b is (byte)'{' or (byte)'[';
		}

		return false;
	}

	/// <summary>Reads a binary or JSON material.</summary>
	/// <param name="data">The whole file.</param>
	/// <param name="fileName">Used to tell a JSON BGSM from a JSON BGEM when its contents don't.</param>
	/// <exception cref="InvalidDataException">The data isn't a valid material.</exception>
	public static MaterialFile Parse(ReadOnlySpan<byte> data, string fileName = "")
	{
		var header = Inspect(data);
		switch (header.Format)
		{
			case MaterialFormat.Binary:
			{
				MaterialFile file = header.Kind == MaterialKind.Bgem ? new Bgem() : new Bgsm();
				file.Read(data);
				return file;
			}
			case MaterialFormat.Json:
				return ParseJson(data, fileName);
			case MaterialFormat.Empty:
				throw new InvalidDataException("The file is empty.");
			default:
				throw new InvalidDataException("The file is not a BGSM, BGEM, or JSON material.");
		}
	}

	private static MaterialFile ParseJson(ReadOnlySpan<byte> data, string fileName)
	{
		if (data.StartsWith(Utf8Bom)) data = data[3..];

		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(data.ToArray(),
				new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
		}
		catch (JsonException e)
		{
			throw new InvalidDataException($"Invalid JSON: {e.Message}", e);
		}

		using (document)
		{
			var root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object)
				throw new InvalidDataException("The JSON material must be an object.");

			MaterialFile file = IsEffect(root, fileName) ? new Bgem() : new Bgsm();
			if (root.TryGetProperty("Version", out var version) && version.TryGetUInt32(out var v))
				file.Version = v;

			foreach (var field in file.Fields)
				if (root.TryGetProperty(field.JsonKey, out var value))
					ApplyJson(file, field, value);

			return file;
		}
	}

	private static bool IsEffect(JsonElement root, string fileName) =>
		root.TryGetProperty("sBaseTexture", out _) || root.TryGetProperty("bFalloffEnabled", out _) ||
		root.TryGetProperty("bBloodEnabled", out _) ||
		(!root.TryGetProperty("sDiffuseTexture", out _) && KindFromExtension(fileName) == MaterialKind.Bgem);

	private static void ApplyJson(MaterialFile file, MaterialField field, JsonElement value)
	{
		switch (field.Kind)
		{
			case MaterialFieldKind.Bool:
				if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
					field.SetValue(file, value.GetBoolean());
				else if (value.ValueKind == JsonValueKind.Number)
					field.SetValue(file, value.GetDouble() != 0);

				break;
			case MaterialFieldKind.Float:
				if (ReadNumber(value) is { } number) field.SetValue(file, (float)number);

				break;
			case MaterialFieldKind.Byte:
				if (ReadNumber(value) is { } integer)
					field.SetValue(file, (byte)Math.Clamp(integer, byte.MinValue, byte.MaxValue));

				break;
			case MaterialFieldKind.Color:
				if (value.ValueKind == JsonValueKind.String &&
				    MaterialColor.TryParseHex(value.GetString()) is { } color)
					field.SetValue(file, color);

				break;
			default:
				if (value.ValueKind == JsonValueKind.String) field.SetValue(file, value.GetString() ?? string.Empty);

				break;
		}
	}

	private static double? ReadNumber(JsonElement value) =>
		value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: value.ValueKind == JsonValueKind.String &&
			  double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
				? d
				: null;
}
