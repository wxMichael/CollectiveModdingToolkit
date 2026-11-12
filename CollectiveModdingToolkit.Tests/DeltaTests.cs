using System;
using System.IO.Hashing;
using CollectiveModdingToolkit.Delta;
using Xunit;

namespace CollectiveModdingToolkit.Tests;

public class DeltaTests
{
	/// <summary>Random bytes with long zero runs and repeated blocks, like machine code and padding.</summary>
	private static byte[] FakeExecutable(int size, int seed)
	{
		Random random = new(seed);
		var data = new byte[size];
		var i = 0;
		while (i < size)
		{
			var run = Math.Min(random.Next(8, 400), size - i);
			switch (random.Next(3))
			{
				case 0: // zero padding
					break;
				case 1: // fresh "code"
					random.NextBytes(data.AsSpan(i, run));
					break;
				default: // repeat earlier code
					if (i > run) data.AsSpan(random.Next(0, i - run), run).CopyTo(data.AsSpan(i));
					break;
			}

			i += run;
		}

		return data;
	}

	[Fact]
	public void Roundtrip_InsertionDeletionAndRelocation()
	{
		var old = FakeExecutable(400_000, 1);
		var @new = new byte[old.Length + 1000];
		// Insert 1000 bytes near the start: everything after shifts, like a rebuilt executable.
		old.AsSpan(0, 5000).CopyTo(@new);
		new Random(2).NextBytes(@new.AsSpan(5000, 1000));
		old.AsSpan(5000).CopyTo(@new.AsSpan(6000));
		// Nudge "pointers" scattered through the file.
		for (var i = 20_000; i < @new.Length; i += 997) @new[i] += 3;
		// Delete a block as well.
		var trimmed = new byte[@new.Length - 7000];
		@new.AsSpan(0, 100_000).CopyTo(trimmed);
		@new.AsSpan(107_000).CopyTo(trimmed.AsSpan(100_000));

		var patch = DeltaCreator.Create(old, trimmed);
		Assert.Equal(trimmed, DeltaApplier.Apply(patch, old));
		Assert.True(patch.Length < trimmed.Length / 20, $"Patch of {patch.Length} bytes is too large.");
	}

	[Fact]
	public void Roundtrip_IdenticalEmptyAndUnrelated()
	{
		var data = FakeExecutable(50_000, 3);
		Assert.Equal(data, DeltaApplier.Apply(DeltaCreator.Create(data, data), data));

		byte[] empty = [];
		Assert.Equal(data, DeltaApplier.Apply(DeltaCreator.Create(empty, data), empty));
		Assert.Equal(empty, DeltaApplier.Apply(DeltaCreator.Create(data, empty), data));

		var other = FakeExecutable(60_000, 99);
		Assert.Equal(other, DeltaApplier.Apply(DeltaCreator.Create(data, other), data));
	}

	[Fact]
	public void RejectsPatchForDifferentSource()
	{
		var old = FakeExecutable(20_000, 4);
		var patch = DeltaCreator.Create(old, FakeExecutable(20_000, 5));
		var wrong = (byte[])old.Clone();
		wrong[100] ^= 1;
		Assert.Throws<DeltaException>(() => DeltaApplier.Apply(patch, wrong));
	}

	[Fact]
	public void DamagedPatchesThrowDeltaExceptionOnly()
	{
		var old = FakeExecutable(30_000, 6);
		var patch = DeltaCreator.Create(old, FakeExecutable(30_000, 7));
		Random random = new(8);
		for (var attempt = 0; attempt < 300; attempt++)
		{
			var damaged = (byte[])patch.Clone();
			damaged[random.Next(damaged.Length)] ^= (byte)random.Next(1, 256);
			try
			{
				// A flipped bit may land in a place that still decodes; the result must then still verify.
				DeltaApplier.Apply(damaged, old);
			}
			catch (DeltaException)
			{
			}
		}

		for (var length = 0; length < patch.Length; length += Math.Max(1, patch.Length / 50))
			Assert.Throws<DeltaException>(() => DeltaApplier.Apply(patch[..length], old));
	}

	[Fact]
	public void ReadInfoMatchesFiles()
	{
		var old = FakeExecutable(10_000, 9);
		var @new = FakeExecutable(12_000, 10);
		var info = DeltaFormat.ReadInfo(DeltaCreator.Create(old, @new));
		Assert.Equal(old.Length, info.OldSize);
		Assert.Equal(@new.Length, info.NewSize);
		Assert.Equal(Crc32.HashToUInt32(@new), info.NewCrc);
	}

	[Fact]
	public void GameFileTable_IsConsistent()
	{
		foreach (var file in GameFiles.All)
		{
			Assert.NotEmpty(file.Crcs);
			foreach (var crc in file.Crcs.Keys) Assert.Equal(file.Crcs[crc], file.Identify(crc));
		}

		var ngae = GameFiles.All[2];
		Assert.Equal("steam_api64.dll", ngae.Name);
		Assert.True(GameFiles.IsEdition(ngae, 0xE36E7B4D, Edition.NextGen));
		Assert.True(GameFiles.IsEdition(ngae, 0xE36E7B4D, Edition.Anniversary));
		Assert.False(GameFiles.IsEdition(ngae, 0xE36E7B4D, Edition.OldGen));
		Assert.Equal("OG-to-NG-Fallout4.exe.cmdelta",
			GameFiles.PatchFileName(Edition.OldGen, Edition.NextGen, GameFiles.All[0]));
	}
}
