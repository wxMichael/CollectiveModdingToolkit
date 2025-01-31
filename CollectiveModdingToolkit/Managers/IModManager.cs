using CollectiveModdingToolkit.Models;
using System;
using System.Collections.Generic;

namespace CollectiveModdingToolkit.Managers;

public interface IModManager
{
	string AppName { get; }
	string AppPath { get; }
	Version? AppVersion { get; }
	string AppVersionString { get; }
	string? GameName { get; }
	string? GamePath { get; }
	string? Profile { get; }
	string? StagePath { get; }
	string PluginsTxtPath { get; }
	//string? ModListTxtPath { get; }
	string GameIniPath { get; }
	List<string> StagePaths { get; }
	List<Module> Modules { get; }
	Dictionary<string, Module> ModulesWinning { get; }
	List<Archive> Archives { get; }
	Dictionary<string, Archive> ArchivesWinning { get; }

	void ReadSettings();

	void Refresh();
}
