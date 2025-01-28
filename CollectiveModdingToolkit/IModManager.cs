using System;

namespace CollectiveModdingToolkit;

public interface IModManager
{
	string AppName { get; }
	string AppPath { get; }
	Version? AppVersion { get; }
	string AppVersionString { get; }
	string? GameName { get; }
	string? GamePath { get; }
	string Profile { get; }
	string? StagePath { get; }
	string? PluginsTxt { get; }
	string? ModListTxt { get; }
	string? GameIniPath { get; }

	void ReadSettings();

	void RefreshSettings();
}
