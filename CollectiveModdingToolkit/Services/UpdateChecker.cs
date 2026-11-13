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
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CollectiveModdingToolkit.Services;

/// <summary>A failed update check, with a human-readable message.</summary>
public sealed class UpdateCheckException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Checks the latest version of this app on Nexus Mods via the v2 GraphQL API.</summary>
public static class UpdateChecker
{
	public const string NexusPageUrl = "https://www.nexusmods.com/site/mods/1597";

	private const string GraphQlUrl = "https://api.nexusmods.com/v2/graphql";
	private const string Query = "query mod($modId: ID!, $gameId: ID!) {mod(modId: $modId, gameId: $gameId) {version}}";
	private const int GameId = 2295;
	private const int ModId = 1597;

	private static readonly HttpClient Client = CreateClient();

	private static HttpClient CreateClient()
	{
		HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
		client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", AppInfo.WindowTitle);
		return client;
	}

	/// <summary>Fetches the latest version string from Nexus Mods.</summary>
	/// <exception cref="UpdateCheckException">The check failed; the message explains why.</exception>
	public static async Task<string> GetLatestVersionAsync(CancellationToken cancellationToken = default)
	{
		var body = new { query = Query, variables = new { gameId = GameId, modId = ModId } };
		try
		{
			using var response = await Client.PostAsJsonAsync(GraphQlUrl, body, cancellationToken);
			if (response.StatusCode == HttpStatusCode.TooManyRequests)
				throw new UpdateCheckException("Nexus Mods is limiting requests right now. Try again later.");

			if (!response.IsSuccessStatusCode)
				throw new UpdateCheckException(
					$"Nexus Mods returned an error ({(int)response.StatusCode} {response.ReasonPhrase}). Try again later.");

			using var document =
				await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
					cancellationToken: cancellationToken);
			return ReadVersion(document.RootElement);
		}
		catch (UpdateCheckException)
		{
			throw;
		}
		catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
		{
			throw new UpdateCheckException("Nexus Mods took too long to respond. Try again later.", e);
		}
		catch (HttpRequestException e)
		{
			throw new UpdateCheckException("Couldn't connect to Nexus Mods. Check your internet connection.", e);
		}
		catch (JsonException e)
		{
			throw new UpdateCheckException("Nexus Mods sent a response that couldn't be read.", e);
		}
	}

	private static string ReadVersion(JsonElement root)
	{
		if (root.ValueKind != JsonValueKind.Object)
			throw new UpdateCheckException("Nexus Mods sent a response that couldn't be read.");

		if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array &&
		    errors.GetArrayLength() > 0)
		{
			var message = errors[0].ValueKind == JsonValueKind.Object &&
			              errors[0].TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
				? m.GetString()
				: null;
			throw new UpdateCheckException(string.IsNullOrWhiteSpace(message)
				? "Nexus Mods reported an error. Try again later."
				: $"Nexus Mods reported an error: {message}");
		}

		if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object &&
		    data.TryGetProperty("mod", out var mod) && mod.ValueKind == JsonValueKind.Object &&
		    mod.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.String)
		{
			var text = version.GetString()!.Trim().TrimStart('v', 'V');
			if (TryParseVersion(text, out _)) return text;

			throw new UpdateCheckException($"Nexus Mods lists an unrecognized version: \"{version.GetString()}\".");
		}

		throw new UpdateCheckException("Nexus Mods sent a response without version information.");
	}

	/// <summary>True if <paramref name="latest" /> is newer than the running version.</summary>
	public static bool IsNewer(string? latest) =>
		TryParseVersion(latest, out var latestVersion) &&
		TryParseVersion(AppInfo.VersionString, out var current) &&
		latestVersion > current;

	/// <summary>Parses a version, treating missing parts as 0 so that "1.2" equals "1.2.0".</summary>
	private static bool TryParseVersion(string? text, out Version version)
	{
		version = new Version();
		text = text?.Trim().TrimStart('v', 'V');
		if (text is null) return false;

		// Version.TryParse needs at least two parts.
		if (!text.Contains('.')) text += ".0";

		if (!Version.TryParse(text, out var parsed)) return false;

		version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0));
		return true;
	}
}
