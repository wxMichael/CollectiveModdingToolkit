# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

Versions have two numbers, `FEATURE.FIX` (e.g. `1.0`, `1.1`, `2.0`):

- **FEATURE** increases for a release that adds or changes functionality. FIX resets to 0.
- **FIX** increases for a release that only contains bug fixes.

Conventions:

- One `## [version] - YYYY-MM-DD` section per release, newest first, with unreleased work under `## [Unreleased]`.
- Group entries under `### Added`, `### Changed`, `### Deprecated`, `### Removed`, `### Fixed` or `### Security`.
- Entries are flat `-` bullets. Wrapped lines are indented two spaces. Prefix with the area when helpful (e.g. `Scanner: ...`).
- Version headings and the compare links at the bottom must match the `FileVersion` in the csproj. The release workflow reads this file.

## [Unreleased]

## [1.0] - 2026-11-14

### Added

- Features are now roughly equivalent to the old Python app, with some improvements.
- Anniversary Edition support
- Downgrader: Switch Fallout 4 and Creation Kit between Old-Gen, Next-Gen, and Anniversary Edition.
- F4SE plugin version detection.
- Overview: Issues panel which lists basic setup issues such as mismatched game and F4SE versions.
- Overview: Address Library in Binaries.
- Vortex support: Files are mapped to their source mod using Vortex's deployment manifests.
- File origin detection for Vortex and Mod Organizer 2: archives, plugins, and F4SE DLLs now show whether they come from a mod, MO2's Overwrite folder, or the game folder.
- Update check via Nexus API (can be disabled)
- Settings: log level, update check, and buttons to show the log and settings files. Settings are saved automatically.
- A log file (`cm-toolkit.log`) and settings (`settings.json`) in `%LOCALAPPDATA%\CollectiveModdingToolkit`.
- Archives, F4SE: "Source" column and a button to show the file in Explorer.
- Archives: Show a patch summary, failure reasons, and refresh Overview after patching.

### Fixed

- A missing plugins.txt, Fallout4.ccc, INI file or Data folder, or an unrecognised game version, no longer stops the app.
- Overview: `Fallout4 - Nvflex.ba2` was never counted because the NVFlex setting was read from the wrong INI.
- Overview: Fixed counts for Unknown & Unreadable.
- Overview: Improved PC hardware detection.
- F4SE: Handle a missing plugins folder.
- F4SE: Plugins that use Address Library or signatures are marked as working across versions of that edition.
- F4SE: Plugins that only export `F4SEPlugin_Preload` are recognised.
- MO2: Detection of F4SE plugin DLLs.
- MO2: Support "Use profile-specific Game INI Files".

### Removed

- Modules tab placeholder. It will return when functional.

[1.0]: https://github.com/wxMichael/CollectiveModdingToolkit/releases/tag/v1.0
[Unreleased]: https://github.com/wxMichael/CollectiveModdingToolkit/commits/main
