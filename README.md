# Collective Modding Toolkit

Get it on Nexus Mods: https://www.nexusmods.com/site/mods/1597

A toolkit for troubleshooting your mod setup for Fallout 4, purpose-built for the [Collective Modding Discord](https://discord.gg/tktyEyYHZH) community.

This is a rewrite of the [Python version](https://github.com/wxMichael/Collective-Modding-Toolkit) using C# and [Avalonia UI](https://avaloniaui.net/).

[Join on Discord](https://discord.gg/tktyEyYHZH) and look for the **#🔧-cm-toolkit** channel.

[![CM-External-Banner-01](https://i.postimg.cc/dttk8WxL/CM-External-Banner-01.png)](https://discord.gg/tktyEyYHZH)

## Features

- Downgrade or upgrade Fallout 4 and Creation kit between versions with delta patches.
- Provides counts of data files by type; plugins (Full/Light) and BA2s (General and Textures)
- Patches BA2 files to either v1 (OG) or v8 (NG)
- Scans F4SE DLLs for game version support
- Scan your mod setup for potential issues (limited usefulness currently, but more to come)

## Building

Requirements: Windows, [.NET 11 SDK](https://dotnet.microsoft.com/download).

```
dotnet build CollectiveModdingToolkit.sln
```

To produce a single-file release build in `publish/`, run `clean-build.bat`.
