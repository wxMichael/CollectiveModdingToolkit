using CollectiveModdingToolkit.Models.FileTypes;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CollectiveModdingToolkit.Managers;

internal partial class Vortex : ObservableObject, IModManager
{
	public Vortex(ProcessModule? processModule) => throw new NotImplementedException();

	public string AppName => throw new NotImplementedException();

	public string AppPath => throw new NotImplementedException();

	public Version? AppVersion => throw new NotImplementedException();

	public string AppVersionString => throw new NotImplementedException();

	public string? GameName => throw new NotImplementedException();

	public string? GamePath => throw new NotImplementedException();

	public string Profile => throw new NotImplementedException();

	public string StagePath => throw new NotImplementedException();

	public string GameIniPath => throw new NotImplementedException();

	public List<string> StagePaths => throw new NotImplementedException();

	public string PluginsTxtPath => throw new NotImplementedException();

	public List<Module> Modules => throw new NotImplementedException();

	public Dictionary<string, Module> ModulesWinning => throw new NotImplementedException();

	public List<Archive> Archives => throw new NotImplementedException();

	public Dictionary<string, Archive> ArchivesWinning => throw new NotImplementedException();

	public List<DLL> Dlls => throw new NotImplementedException();

	public Dictionary<string, DLL> DllsWinning => throw new NotImplementedException();

	public bool ReadSettings() => throw new NotImplementedException();
	public void Refresh() => throw new NotImplementedException();
}
