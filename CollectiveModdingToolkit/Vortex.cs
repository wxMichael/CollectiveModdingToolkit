using CommunityToolkit.Mvvm.ComponentModel;
using System;
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
	public string? PluginsTxt { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	public string? GameIniPath => throw new NotImplementedException();

	public string AppVersionString => throw new NotImplementedException();

	public Vortex(ProcessModule? processModule) => throw new NotImplementedException();

	public void ReadSettings() => throw new NotImplementedException();
	public void RefreshSettings() => throw new NotImplementedException();
}
