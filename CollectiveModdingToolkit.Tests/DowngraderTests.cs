using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Threading;
using System.Threading.Tasks;
using CollectiveModdingToolkit.Delta;
using Xunit;

namespace CollectiveModdingToolkit.Tests;

public sealed class DowngraderTests : IDisposable
{
	private readonly string _cache;
	private readonly Dictionary<Edition, byte[]> _data = [];
	private readonly TrackedFile _exe;
	private readonly string _game;
	private readonly string _published;
	private readonly string _root = Path.Combine(Path.GetTempPath(), "cmt-test-" + Guid.NewGuid().ToString("N"));
	private readonly FolderSource _source;

	public DowngraderTests()
	{
		_game = Path.Combine(_root, "game");
		_published = Path.Combine(_root, "published");
		_cache = Path.Combine(_root, "cache");
		Directory.CreateDirectory(_game);
		Directory.CreateDirectory(_published);

		Random random = new(1);
		var og = new byte[100_000];
		random.NextBytes(og);
		var ng = (byte[])og.Clone();
		for (var i = 500; i < ng.Length; i += 211) ng[i] += 7;
		var ae = (byte[])ng.Clone();
		for (var i = 900; i < ae.Length; i += 503) ae[i] ^= 0x55;
		_data[Edition.OldGen] = og;
		_data[Edition.NextGen] = ng;
		_data[Edition.Anniversary] = ae;

		_exe = new TrackedFile("Test.exe", false, new Dictionary<uint, Edition>
		{
			[Crc(og)] = Edition.OldGen,
			[Crc(ng)] = Edition.NextGen,
			[Crc(ae)] = Edition.Anniversary,
			[0xDEADBEEF] = Edition.Obsolete,
		});

		foreach (var from in GameFiles.Supported)
		foreach (var to in GameFiles.Supported)
			if (from != to)
				File.WriteAllBytes(Path.Combine(_published, GameFiles.PatchFileName(from, to, _exe)),
					DeltaCreator.Create(_data[from], _data[to]));

		_source = new FolderSource(_published);
	}

	private string ExePath => Path.Combine(_game, "Test.exe");

	public void Dispose()
	{
		if (!Directory.Exists(_root)) return;

		foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
			File.SetAttributes(file, FileAttributes.Normal);

		Directory.Delete(_root, true);
	}

	private static uint Crc(byte[] data) => Crc32.HashToUInt32(data);

	private Downgrader Make() => new(_game, _source, _cache, [_exe]);

	private void Install(Edition edition) => File.WriteAllBytes(ExePath, _data[edition]);

	private async Task<(DowngradeResult Result, List<DowngradeMessage> Log)> Run(Edition desired, bool keep = true,
		bool delete = true)
	{
		List<DowngradeMessage> log = [];
		var result = await Make().RunAsync(desired, new DowngradeOptions(keep, delete),
			new Progress<DowngradeMessage>(log.Add), new Progress<double>(), CancellationToken.None);
		return (result, log);
	}

	[Fact]
	public async Task PatchesWithDeltaAndBacksUpTheOriginal()
	{
		Install(Edition.Anniversary);
		var (result, _) = await Run(Edition.OldGen);

		Assert.Equal(new DowngradeResult(1, 0, 0), result);
		Assert.Equal(_data[Edition.OldGen], await File.ReadAllBytesAsync(ExePath));
		Assert.Equal(_data[Edition.Anniversary],
			await File.ReadAllBytesAsync(Downgrader.BackupPath(ExePath, Edition.Anniversary)));
		Assert.Empty(Directory.GetFiles(_game, "*.cmt-new"));
		Assert.False(
			File.Exists(Path.Combine(_cache, GameFiles.PatchFileName(Edition.Anniversary, Edition.OldGen, _exe))),
			"Delete Patches should remove the downloaded patch.");
	}

	[Fact]
	public async Task SwitchingBackUsesTheBackupAndNeverDownloads()
	{
		Install(Edition.NextGen);
		await Run(Edition.OldGen);
		Directory.Delete(_published, true); // Any download attempt now fails.

		var (result, _) = await Run(Edition.NextGen);

		Assert.Equal(new DowngradeResult(1, 0, 0), result);
		Assert.Equal(_data[Edition.NextGen], await File.ReadAllBytesAsync(ExePath));
		Assert.Equal(_data[Edition.OldGen],
			await File.ReadAllBytesAsync(Downgrader.BackupPath(ExePath, Edition.OldGen)));
	}

	[Fact]
	public async Task WithoutKeepBackupsNothingIsLeftBehind()
	{
		Install(Edition.NextGen);
		var (result, _) = await Run(Edition.OldGen, false);

		Assert.Equal(1, result.Patched);
		Assert.Equal(_data[Edition.OldGen], await File.ReadAllBytesAsync(ExePath));
		Assert.Equal([ExePath], Directory.GetFiles(_game));
	}

	[Fact]
	public async Task UsesABackupOfTheWantedVersionWhateverItIsNamed()
	{
		// Simple Downgrader style: the Old-Gen file saved under the Next-Gen backup name.
		Install(Edition.NextGen);
		await File.WriteAllBytesAsync(Downgrader.BackupPath(ExePath, Edition.NextGen), _data[Edition.OldGen]);
		Directory.Delete(_published, true);

		var (result, _) = await Run(Edition.OldGen);

		Assert.Equal(1, result.Patched);
		Assert.Equal(_data[Edition.OldGen], await File.ReadAllBytesAsync(ExePath));
		Assert.Equal(_data[Edition.NextGen],
			await File.ReadAllBytesAsync(Downgrader.BackupPath(ExePath, Edition.NextGen)));
	}

	[Fact]
	public async Task MissingPatchLeavesTheInstallUntouched()
	{
		Install(Edition.NextGen);
		Directory.Delete(_published, true);

		var (result, log) = await Run(Edition.OldGen);

		Assert.Equal(new DowngradeResult(0, 1, 0), result);
		Assert.Equal(_data[Edition.NextGen], await File.ReadAllBytesAsync(ExePath));
		Assert.Equal([ExePath], Directory.GetFiles(_game));
		Assert.Contains(log, m => m.Kind == MessageKind.Bad);
	}

	[Fact]
	public async Task CorruptPatchLeavesTheInstallUntouchedAndIsDiscarded()
	{
		Install(Edition.NextGen);
		var patchName = GameFiles.PatchFileName(Edition.NextGen, Edition.OldGen, _exe);
		var bytes = await File.ReadAllBytesAsync(Path.Combine(_published, patchName));
		bytes[^5] ^= 0xFF;
		await File.WriteAllBytesAsync(Path.Combine(_published, patchName), bytes);

		var (result, _) = await Run(Edition.OldGen, delete: false);

		Assert.Equal(1, result.Failed);
		Assert.Equal(_data[Edition.NextGen], await File.ReadAllBytesAsync(ExePath));
		Assert.False(File.Exists(Path.Combine(_cache, patchName)));
	}

	[Fact]
	public async Task SkipsUnknownMissingAndAlreadyCorrectFiles()
	{
		File.WriteAllBytes(ExePath, [1, 2, 3]);
		var (unknown, _) = await Run(Edition.OldGen);
		Assert.Equal(new DowngradeResult(0, 0, 1), unknown);
		Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(ExePath));

		File.Delete(ExePath);
		Assert.Equal(new DowngradeResult(0, 0, 1), (await Run(Edition.OldGen)).Result);

		Install(Edition.OldGen);
		Assert.Equal(new DowngradeResult(0, 0, 1), (await Run(Edition.OldGen)).Result);
	}

	[Fact]
	public async Task ReadOnlyFilesArePatched()
	{
		Install(Edition.NextGen);
		File.SetAttributes(ExePath, FileAttributes.ReadOnly);

		var (result, _) = await Run(Edition.OldGen);

		Assert.Equal(1, result.Patched);
		Assert.Equal(_data[Edition.OldGen], await File.ReadAllBytesAsync(ExePath));
	}

	[Fact]
	public async Task LockedFileFailsCleanly()
	{
		Install(Edition.NextGen);
		await using (new FileStream(ExePath, FileMode.Open, FileAccess.Read, FileShare.Read))
		{
			var (result, _) = await Run(Edition.OldGen);
			Assert.Equal(1, result.Failed);
		}

		Assert.Equal(_data[Edition.NextGen], await File.ReadAllBytesAsync(ExePath));
		Assert.Empty(Directory.GetFiles(_game, "*.cmt-new"));
	}

	[Fact]
	public async Task CancellationStopsBeforeTouchingTheInstall()
	{
		Install(Edition.NextGen);
		using CancellationTokenSource cancel = new();
		await cancel.CancelAsync();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Make().RunAsync(Edition.OldGen,
			new DowngradeOptions(true, true), new Progress<DowngradeMessage>(), new Progress<double>(), cancel.Token));
		Assert.Equal(_data[Edition.NextGen], await File.ReadAllBytesAsync(ExePath, cancel.Token));
	}

	private sealed class FolderSource(string folder) : IPatchSource
	{
		public Task FetchAsync(string patchName, string destination, IProgress<double>? progress,
			CancellationToken cancel)
		{
			var source = Path.Combine(folder, patchName);
			if (!File.Exists(source)) throw new IOException("This patch has not been published yet.");

			File.Copy(source, destination, true);
			return Task.CompletedTask;
		}
	}
}
