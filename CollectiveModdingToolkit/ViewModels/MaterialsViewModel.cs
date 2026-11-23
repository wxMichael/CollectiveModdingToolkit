/*
 *
 * Collective Modding Toolkit
 * Copyright (C) 2024, 2025, 2026  wxMichael
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, see <https://www.gnu.org/licenses/>.
 *
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CollectiveModdingToolkit.Materials;
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Models.FileTypes;
using CollectiveModdingToolkit.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectiveModdingToolkit.ViewModels;

/// <summary>A material file found in Data, a mod, or an archive the game loads.</summary>
/// <param name="FullPath">The file's path, or for a material in an archive, a key combining the archive and the material.</param>
/// <param name="Archive">The archive holding the material, or null for a loose file.</param>
public sealed record MaterialListItem(string Relative, string FullPath, string? Mod, string? Archive = null)
{
	public string Name => Path.GetFileName(Relative);

	public bool IsArchived => Archive != null;
}

/// <summary>Browses the game's materials and edits one at a time.</summary>
public sealed partial class MaterialsViewModel : GameViewModelBase
{
	private static readonly string[] ListedExtensions = [".bgsm", ".bgem", ".json"];

	private readonly IDialogService _dialogs;
	private readonly IShellService _shell;
	private List<MaterialListItem> _all = [];

	/// <summary>The archived materials in the list, by their <see cref="MaterialListItem.FullPath" /> key.</summary>
	private Dictionary<string, Ba2Entry> _archived = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The existing file a pending Save to Overwrite would replace. Set by the first click, confirmed by the second.</summary>
	private string? _pendingOverwrite;

	private bool _reverting;

	/// <summary>Design-time only.</summary>
	public MaterialsViewModel() : this(new GameSession(), new ShellService(), new DialogService())
	{
		if (Design.IsDesignMode) LoadDesignData();
	}

	public MaterialsViewModel(GameSession session, IShellService shell, IDialogService dialogs) : base(session, shell)
	{
		_shell = shell;
		_dialogs = dialogs;
		session.PropertyChanged += (sender, e) =>
		{
			if (e.PropertyName != nameof(GameSession.Game)) return;

			OnPropertyChanged(nameof(HasOverwrite));
			_ = RefreshListAsync();
		};
		_ = RefreshListAsync();
	}

	/// <summary>True to show the fields every material has; false to show the type-specific ones.</summary>
	[ObservableProperty]
	public partial bool GroupByGeneral { get; set; } = true;

	public bool GroupBySpecific
	{
		get => !GroupByGeneral;
		set => GroupByGeneral = !value;
	}

	public ObservableCollection<MaterialListItem> Items { get; } = [];

	[ObservableProperty] public partial string Filter { get; set; } = string.Empty;

	[ObservableProperty] public partial MaterialListItem? SelectedItem { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(SaveAsCommand), nameof(RevertCommand),
		nameof(ConvertToFallout4Command), nameof(RevealCommand), nameof(SaveToOverwriteCommand))]
	public partial MaterialDocument? Document { get; private set; }

	[ObservableProperty] public partial string StatusText { get; private set; } = string.Empty;

	/// <summary>True when the last status was a failure.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(StatusLevel))]
	public partial bool IsStatusError { get; private set; }

	public StatusLevel StatusLevel => IsStatusError ? StatusLevel.Bad : StatusLevel.None;

	[ObservableProperty] public partial string ListInfo { get; private set; } = string.Empty;

	public bool HasDocument => Document != null;

	/// <summary>True when the mod manager has an Overwrite folder to save to (MO2).</summary>
	public bool HasOverwrite => Session.Game?.Manager.OverwritePath != null;

	/// <summary>Whether the open material is in a newer game's format, and can be converted back.</summary>
	public bool CanConvert => Document is { IsFallout4Format: false };

	partial void OnGroupByGeneralChanged(bool value)
	{
		OnPropertyChanged(nameof(GroupBySpecific));
		if (Document != null) Document.ShowGeneral = value;
	}

	/// <summary>Fills the list and editor with mock data so the XAML previewer has something to show.</summary>
	private void LoadDesignData()
	{
		_all =
		[
			new MaterialListItem(@"Materials\Armor\CombatArmor\CombatArmor.bgsm", @"C:\Mods\Armor\CombatArmor.bgsm",
				"Armor Overhaul"),
			new MaterialListItem(@"Materials\Armor\CombatArmor\CombatArmorHelmet.bgsm", @"C:\Mods\Armor\Helmet.bgsm",
				"Armor Overhaul"),
			new MaterialListItem(@"Materials\Effects\Glow.bgem", @"C:\Mods\Fx\Glow.bgem", "Visual Effects"),
			new MaterialListItem(@"Materials\Landscape\Rock01.bgsm", @"C:\Game\Data\Rock01.bgsm", null),
		];
		ApplyFilter();

		Document = MaterialDocument.CreateDesignSample();
		_reverting = true;
		SelectedItem = Items[0];
		_reverting = false;
	}

	partial void OnFilterChanged(string value) => ApplyFilter();

	partial void OnDocumentChanged(MaterialDocument? oldValue, MaterialDocument? newValue)
	{
		if (oldValue != null) oldValue.PropertyChanged -= OnDocumentPropertyChanged;
		if (newValue != null) newValue.PropertyChanged += OnDocumentPropertyChanged;

		_pendingOverwrite = null;
		OnPropertyChanged(nameof(HasDocument));
		OnPropertyChanged(nameof(CanConvert));
	}

	private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(MaterialDocument.IsArchived)) SaveCommand.NotifyCanExecuteChanged();

		if (e.PropertyName == nameof(MaterialDocument.IsDirty))
		{
			SaveCommand.NotifyCanExecuteChanged();
			RevertCommand.NotifyCanExecuteChanged();
		}
		else if (e.PropertyName == nameof(MaterialDocument.IsFallout4Format))
		{
			OnPropertyChanged(nameof(CanConvert));
			ConvertToFallout4Command.NotifyCanExecuteChanged();
		}
	}

	partial void OnSelectedItemChanged(MaterialListItem? value)
	{
		if (_reverting || value == null || value.FullPath == Document?.Path) return;

		if (Document is { IsDirty: true })
		{
			// Never throw away edits silently.
			SetStatus("Save or revert your changes before opening another material.", true);
			_reverting = true;
			SelectedItem = Items.FirstOrDefault(i => i.FullPath == Document.Path);
			_reverting = false;
			return;
		}

		LoadDocument(value.FullPath);
	}

	/// <summary>Opens a material by path. False if unsaved changes are in the way or the file can't be read.</summary>
	public bool TryOpen(string path)
	{
		if (Document is { IsDirty: true } && Document.Path != path)
		{
			SetStatus("Save or revert your changes before opening another material.", true);
			return false;
		}

		// An archived material is only known from the list, and the archive may not be one the game loads.
		var known = _all.FirstOrDefault(i => i.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase));
		if (known == null && path.Contains("::", StringComparison.Ordinal))
		{
			SetStatus("That material is in an archive the game doesn't load, so it isn't listed.", true);
			return false;
		}

		path = known?.FullPath ?? path;
		_reverting = true;
		SelectedItem = Items.FirstOrDefault(i => i.FullPath == path);
		_reverting = false;
		return LoadDocument(path);
	}

	private bool LoadDocument(string path)
	{
		try
		{
			var item = Items.FirstOrDefault(i => i.FullPath == path) ?? _all.FirstOrDefault(i => i.FullPath == path);
			if (item is { Archive: { } archive } && _archived.TryGetValue(path, out var entry))
			{
				var size = entry.UnpackedSize;
				if (size > MaterialChecker.MaxSize)
					throw new InvalidDataException(
						$"It's unusually large ({size:N0} bytes) and probably isn't a material.");

				var data = Ba2Reader.ReadStart(archive, entry, (int)size);
				Document = MaterialDocument.LoadArchived(data, path, archive, item.Relative);
			}
			else
			{
				Document = MaterialDocument.Load(path);
			}

			Document.ShowGeneral = GroupByGeneral;
			Document.TexturePicker = PickTextureAsync;
			Document.MaterialPicker = PickMaterialPathAsync;
			SetStatus(Document.IsArchived
				? $"This material is inside {Path.GetFileName(Document.ArchivePath)}. Use Save As or Save to Overwrite to save an edited copy."
				: Document.SourceFormat == MaterialFormat.Json
					? "This material is JSON. Saving will convert it to the binary format."
					: string.Empty);
			return true;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
		{
			Document = null;
			SetStatus($"Couldn't open {Path.GetFileName(path.Split("::")[^1])}: {e.Message}", true);
			return false;
		}
	}

	private void SetStatus(string text, bool error = false)
	{
		StatusText = text;
		IsStatusError = error;
	}

	// ---- The list ----

	[RelayCommand]
	private async Task RefreshListAsync()
	{
		var game = Session.Game;
		if (game == null || !Directory.Exists(game.DataPath))
		{
			_all = [];
			ApplyFilter();
			ListInfo = game == null ? "Loading..." : "The game's Data folder was not found.";
			return;
		}

		ListInfo = "Looking for materials...";
		try
		{
			var (found, archived) = await Task.Run(() =>
			{
				var index = Scanner.IndexFolder(game, "Materials");
				var items = index.Values
					.Where(e => IsListed(e.Relative))
					.Select(e => new MaterialListItem(e.Relative, Scanner.GetSourcePath(game.Manager, e),
						Scanner.GetMod(game.Manager, e)))
					.ToList();

				// Loose files win over archives.
				var inArchives = IndexArchives(game, index.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase));
				items.AddRange(inArchives.Items);
				items.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Relative, b.Relative));
				return (items, inArchives.Entries);
			});

			// A refresh may have replaced the game while this ran.
			if (!ReferenceEquals(game, Session.Game)) return;

			_all = found;
			_archived = archived;
			ApplyFilter();
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			Log.Warning($"Materials: unable to list materials: {e.Message}");
			_all = [];
			ApplyFilter();
			ListInfo = "Couldn't read the Materials folder.";
		}
	}

	private static bool IsListed(string relative) =>
		ListedExtensions.Contains(Path.GetExtension(relative), StringComparer.OrdinalIgnoreCase);

	/// <summary>Lists the materials inside the archives the game loads, skipping any a loose file replaces.</summary>
	private static (List<MaterialListItem> Items, Dictionary<string, Ba2Entry> Entries) IndexArchives(Game game,
		HashSet<string> loose)
	{
		List<MaterialListItem> items = [];
		Dictionary<string, Ba2Entry> entries = new(StringComparer.OrdinalIgnoreCase);
		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
		// The archive loaded last wins, so go through them from the end.
		foreach (var name in Enumerable.Reverse(game.ArchivesLoadOrder))
		{
			if (!game.ArchivesWinning.TryGetValue(Game.ArchiveKey(name), out var archive)) continue;

			var archivePath = archive.RealPath;
			try
			{
				foreach (var entry in Ba2Reader.Read(archivePath))
				{
					var relative = entry.Path.Replace('/', '\\');
					if (!relative.StartsWith(@"materials\", StringComparison.OrdinalIgnoreCase) ||
					    !IsListed(relative) || loose.Contains(relative) || !seen.Add(relative))
						continue;

					var key = $"{archivePath}::{relative}";
					entries[key] = entry;
					items.Add(new MaterialListItem(relative, key, archive.Mod ?? name, archivePath));
				}
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
			{
				// Damaged or unsupported archives are reported by the scanner.
				Log.Warning($"Materials: unable to read archive {name}: {e.Message}");
			}
		}

		return (items, entries);
	}

	private void ApplyFilter()
	{
		var terms = Filter.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var shown = _all.Where(i => terms.All(t =>
			i.Relative.Contains(t, StringComparison.OrdinalIgnoreCase) ||
			(i.Mod?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false))).ToList();

		_reverting = true;
		var selected = Document?.Path;
		Items.Clear();
		foreach (var item in shown) Items.Add(item);

		SelectedItem = Items.FirstOrDefault(i => i.FullPath == selected);
		_reverting = false;

		ListInfo = _all.Count == 0
			? "No materials found."
			: shown.Count == _all.Count
				? $"{_all.Count} materials"
				: $"{shown.Count} of {_all.Count} materials";
	}

	// ---- The open material ----

	[RelayCommand]
	private async Task OpenFileAsync()
	{
		if (Document is { IsDirty: true })
		{
			SetStatus("Save or revert your changes before opening another material.", true);
			return;
		}

		var path = await _dialogs.PickMaterialToOpenAsync(Session.Game?.DataPath is { } data
			? Path.Combine(data, "Materials")
			: null);
		if (path != null) TryOpen(path);
	}

	private bool CanSave() =>
		Document is { IsArchived: false } d && (d.IsDirty || d.SourceFormat == MaterialFormat.Json);

	[RelayCommand(CanExecute = nameof(CanSave))]
	private void Save()
	{
		if (Document is not { } document) return;

		var wasJson = document.SourceFormat == MaterialFormat.Json;
		try
		{
			document.Save();
			SetStatus(wasJson ? "Saved, converted to the binary format." : "Saved.");
			if (wasJson) LoadDocument(document.Path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			SetStatus($"Couldn't save {document.FileName}: {e.Message}", true);
		}
	}

	private bool HasOpenDocument() => Document != null;

	[RelayCommand(CanExecute = nameof(HasOpenDocument))]
	private async Task SaveAsAsync()
	{
		if (Document is not { } document) return;

		var suggested = document.FileName;
		if (!suggested.EndsWith(document.File.Kind == MaterialKind.Bgem ? ".bgem" : ".bgsm",
			    StringComparison.OrdinalIgnoreCase))
			suggested = Path.GetFileNameWithoutExtension(suggested) +
			            (document.File.Kind == MaterialKind.Bgem ? ".bgem" : ".bgsm");

		var path = await _dialogs.PickMaterialToSaveAsync(suggested,
			Path.GetDirectoryName(document.ArchivePath ?? document.Path));
		if (path == null) return;

		try
		{
			document.Save(path);
			SetStatus($"Saved to {path}.");
			await RefreshListAsync();
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			SetStatus($"Couldn't save {Path.GetFileName(path)}: {e.Message}", true);
		}
	}

	/// <summary>Lets the user choose a texture, returned the way the open material stores texture paths.</summary>
	private async Task<string?> PickTextureAsync()
	{
		var start = Session.Game?.DataPath is { } data ? Path.Combine(data, "Textures") : null;
		if (await _dialogs.PickTextureAsync(start) is not { } picked) return null;

		// Outside a Textures folder the path can't be worked out; fall back to just the file name.
		var relative = DataRelativePath(picked, "Textures") ?? Path.GetFileName(picked);

		// Fallout 4's materials leave out the leading "Textures\".
		return Document is { IsFallout4Format: true } &&
		       relative.StartsWith(@"Textures\", StringComparison.OrdinalIgnoreCase)
			? relative[9..]
			: relative;
	}

	/// <summary>Lets the user choose a material, returned as a path below Data (or the file name if that's unknown).</summary>
	private async Task<string?> PickMaterialPathAsync()
	{
		var start = Session.Game?.DataPath is { } data ? Path.Combine(data, "Materials") : null;
		return await _dialogs.PickMaterialAsync(start) is { } picked
			? DataRelativePath(picked) ?? Path.GetFileName(picked)
			: null;
	}

	/// <summary>A material's path below Data, e.g. "Materials\Foo\Bar.bgsm". Null if it can't be worked out.</summary>
	private string? DataRelativePath(string path, string marker = "Materials")
	{
		var full = Path.GetFullPath(path);
		var roots = new List<string>();
		if (Session.Game is { } game) roots.AddRange([game.DataPath, .. game.Manager.StagePaths]);

		foreach (var root in roots.Where(r => r.Length > 0))
		{
			var prefix = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
			if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return full[prefix.Length..];
		}

		// Elsewhere (a download, a working folder): use the last Materials folder in the path.
		marker = Path.DirectorySeparatorChar + marker + Path.DirectorySeparatorChar;
		var index = full.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
		return index >= 0 ? full[(index + 1)..] : null;
	}

	[RelayCommand(CanExecute = nameof(HasOpenDocument))]
	private async Task SaveToOverwriteAsync()
	{
		if (Document is not { } document || Session.Game?.Manager.OverwritePath is not { } overwrite) return;

		if ((document.ArchiveRelative ?? DataRelativePath(document.Path)) is not { } relative)
		{
			SetStatus("Can't tell where this material belongs in Data, so it can't be placed in Overwrite.", true);
			return;
		}

		var target = Path.GetFullPath(Path.Combine(overwrite, relative));
		var same = string.Equals(target, Path.GetFullPath(document.Path), StringComparison.OrdinalIgnoreCase);
		if (!same && File.Exists(target) && _pendingOverwrite != target)
		{
			_pendingOverwrite = target;
			SetStatus($"A file already exists in Overwrite at {relative}. Press Save to Overwrite again to replace it.",
				true);
			return;
		}

		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(target)!);
			document.Save(target);
			_pendingOverwrite = null;
			SetStatus($"Saved to Overwrite as {relative}.");
			await RefreshListAsync();
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			SetStatus($"Couldn't save to Overwrite: {e.Message}", true);
		}
	}

	private bool CanRevert() => Document is { IsDirty: true };

	[RelayCommand(CanExecute = nameof(CanRevert))]
	private void Revert()
	{
		if (Document is { } document && LoadDocument(document.Path)) SetStatus("Reverted to the saved file.");
	}

	private bool CanConvertNow() => CanConvert;

	[RelayCommand(CanExecute = nameof(CanConvertNow))]
	private void ConvertToFallout4()
	{
		Document?.ConvertToFallout4();
		SetStatus("Switched to the Fallout 4 format. Values only newer versions have won't be saved.");
	}

	[RelayCommand(CanExecute = nameof(HasOpenDocument))]
	private void Reveal()
	{
		if (Document != null) _shell.Reveal(Document.ArchivePath ?? Document.Path);
	}
}
