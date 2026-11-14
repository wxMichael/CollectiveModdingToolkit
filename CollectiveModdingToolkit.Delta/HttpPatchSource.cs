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
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CollectiveModdingToolkit.Delta;

/// <summary>Downloads patches from a GitHub release (or any HTTPS folder).</summary>
public sealed class HttpPatchSource : IPatchSource
{
	/// <summary>The GitHub release the generated patches are uploaded to.</summary>
	private const string DefaultBaseUrl =
		"https://github.com/wxMichael/CollectiveModdingToolkit/releases/download/delta-patches/";

	/// <summary>How long a connection or a stalled transfer may sit idle before giving up.</summary>
	private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(15);

	private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };

	private readonly string _baseUrl;

	public HttpPatchSource(string baseUrl = DefaultBaseUrl)
	{
		_baseUrl = baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
		if (!Uri.TryCreate(_baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
			throw new ArgumentException("Patches must be downloaded over HTTPS.", nameof(baseUrl));
	}

	public async Task FetchAsync(string patchName, string destination, IProgress<double>? progress,
		CancellationToken cancel)
	{
		var part = destination + ".part";
		using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancel);
		try
		{
			idle.CancelAfter(IdleTimeout);
			using var response = await Client.GetAsync(_baseUrl + Uri.EscapeDataString(patchName),
				HttpCompletionOption.ResponseHeadersRead, idle.Token);
			if (response.StatusCode == HttpStatusCode.NotFound)
				throw new IOException("This patch has not been published yet.");

			response.EnsureSuccessStatusCode();

			var total = response.Content.Headers.ContentLength ?? 0;
			await using var input = await response.Content.ReadAsStreamAsync(idle.Token);
			await using (var output = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16,
				             true))
			{
				var buffer = new byte[1 << 16];
				long received = 0;
				int read;
				while ((read = await input.ReadAsync(buffer, idle.Token)) > 0)
				{
					idle.CancelAfter(IdleTimeout);
					await output.WriteAsync(buffer.AsMemory(0, read), idle.Token);
					received += read;
					if (total > 0) progress?.Report(received * 100.0 / total);
				}
			}

			File.Move(part, destination, true);
			progress?.Report(100);
		}
		catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
		{
			throw new IOException("The connection timed out.");
		}
		catch (HttpRequestException e)
		{
			throw new IOException(e.StatusCode is { } code ? $"Server returned {(int)code} {code}." : e.Message);
		}
		finally
		{
			try
			{
				File.Delete(part);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// Left behind, replaced by the next attempt.
			}
		}
	}
}
