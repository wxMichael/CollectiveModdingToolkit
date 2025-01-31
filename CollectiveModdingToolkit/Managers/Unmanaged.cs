using CollectiveModdingToolkit.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace CollectiveModdingToolkit.Managers;

internal class Unmanaged : ObservableObject, IModManager
{
	public string AppName => "Not Found";

	public string AppPath => string.Empty;

	public Version? AppVersion => null;

	public string AppVersionString => string.Empty;

	public string? GameName => null;

	public string? GamePath => null;

	public string? Profile => null;

	public string? StagePath => null;

	public string PluginsTxtPath => string.Empty;

	public string GameIniPath => Computer.DocumentsFallout4;

	public List<string> StagePaths => [];

	public List<Module> Modules => throw new NotImplementedException();

	public Dictionary<string, Module> ModulesWinning => throw new NotImplementedException();

	public List<Archive> Archives => throw new NotImplementedException();

	public Dictionary<string, Archive> ArchivesWinning => throw new NotImplementedException();

	public void ReadSettings() { }
	public void Refresh() => throw new NotImplementedException();
}
//Path.Combine(Computer.AppDataLocal, "Fallout4", "plugins.txt");