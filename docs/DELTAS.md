# Delta patches for the Downgrader

The Downgrader downloads one small `.cmdelta` patch per file it needs to change, from the GitHub release tagged
`delta-patches` (`HttpPatchSource.DefaultBaseUrl`). This is the maintainer's guide to making them.

## Why this format

Patches are bsdiff-style: the new file is matched against the old one, and only the byte-wise *difference* is stored,
then compressed with zstd level 22. For executables, shifted code and relocated pointers become long zero runs, which
compress far better than xdelta3's copy/insert encoding. Format details: `DeltaFormat.cs`.

On a real pair of Windows binaries (4.4 MB each) the patch was 6.6% of the file.

## Making patches

1. Collect one copy of each file per edition (needs ~1.5 GB RAM for the 66 MB `Fallout4.exe`):

   ```
   versions/OG/Fallout4.exe, Fallout4Launcher.exe, steam_api64.dll, CreationKit.exe
   versions/OG/Tools/Archive2/Archive2.exe, Archive2Interop.dll
   versions/NG/...    versions/AE/...
   ```

2. Check the files are recognised:
   `dotnet run --project tools/DeltaTool -c Release -- crcs --versions versions`
3. Make everything (existing correct patches are kept):
   `dotnet run --project tools/DeltaTool -c Release -- create --versions versions --out patches --jobs 2`
4. Check them against the sources:
   `dotnet run --project tools/DeltaTool -c Release -- verify --versions versions --patches patches`
5. Upload everything in `patches/` (the `.cmdelta` files; `manifest.json` is informational) to the `delta-patches` release.

## When the game updates

1. Run `crcs` on the new files. It prints each CRC32.
2. In `CollectiveModdingToolkit.Delta/GameFiles.cs`, change the previous current CRC of every changed file to
   `Obsolete` and add the new one under its edition. Also update `Binary.BaseFiles` in the app so the Overview recognises it.
3. Put the new files in the edition folder, run `create` and `verify`, upload the new patches.

Only files from Old-Gen, Next-Gen, and Anniversary Edition are patched. Anything else is skipped as "Unsupported Version".
`create` refuses files whose CRC32 is not in the table, so you cannot publish patches the app would never match.
