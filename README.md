# Collective Modding Toolkit

A toolkit for troubleshooting and optimizing your mod setup for Fallout 4, purpose-built for the [Collective Modding Discord](https://discord.gg/tktyEyYHZH) community.

This is a rewrite of the [Python version](https://github.com/wxMichael/Collective-Modding-Toolkit) using C# and [Avalonia UI](https://avaloniaui.net/).

## Features

- Overview of game, mod manager and data file counts (plugins, BA2s)
- Downgrade/upgrade Fallout 4 and the Creation Kit between Old-Gen, Next-Gen, and Anniversary Edition
- Archive (BA2) patching
- F4SE DLL scanning for game version support
- Mod setup scanner for potential issues

## Building

Requirements: Windows, [.NET 9 SDK](https://dotnet.microsoft.com/download).

```
dotnet build CollectiveModdingToolkit.sln
```

To produce a single-file release build in `publish/`, run `clean-build.bat`.
