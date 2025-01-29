using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CollectiveModdingToolkit;

internal partial class Vortex : ObservableObject, IModManager
{
	public string AppName => throw new NotImplementedException();

	public string AppPath => throw new NotImplementedException();

	public Version? AppVersion { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public string? GameName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public string? GamePath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public string Profile { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public string? StagePath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public string? PluginsTxtPath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public string? GameIniPath => throw new NotImplementedException();

	public string AppVersionString => throw new NotImplementedException();

	public List<string> StagePaths => throw new NotImplementedException();

	public Vortex(ProcessModule? processModule) => throw new NotImplementedException();

	public void ReadSettings() => throw new NotImplementedException();
	public void Refresh() => throw new NotImplementedException();
}
