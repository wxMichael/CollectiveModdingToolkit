using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Models.FileTypes;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;

namespace CollectiveModdingToolkit.Managers;

internal class Unmanaged : ObservableObject, IModManager
{
	public string AppName { get; } = "Not Found";

	public string? AppPath { get; } = null;

	public Version? AppVersion { get; } = null;

	public string? AppVersionString { get; } = null;

	public string? GameName { get; } = null;

	public string? GamePath { get; } = null;

	public string? Profile { get; } = null;

	//public string? StagePath { get; } = null;

	public string PluginsTxtPath { get; } = Path.Combine(Computer.AppDataLocal, "Fallout4", "plugins.txt");

	public string GameIniFolderPath { get; } = Computer.DocumentsFallout4;

	public List<string> StagePaths { get; } = [];

	public List<Module> Modules { get; } = [];

	public Dictionary<string, Module> ModulesWinning { get; } = [];

	public List<Archive> Archives { get; } = [];

	public Dictionary<string, Archive> ArchivesWinning { get; } = [];

	public List<DLL> Dlls { get; } = [];

	public Dictionary<string, DLL> DllsWinning { get; } = [];

	//public bool ReadSettings() => throw new NotImplementedException();

	public void Refresh()
	{
		StagePaths.Clear();
		Modules.Clear();
		ModulesWinning.Clear();
		Archives.Clear();
		ArchivesWinning.Clear();
		Dlls.Clear();
		DllsWinning.Clear();
	}
}
