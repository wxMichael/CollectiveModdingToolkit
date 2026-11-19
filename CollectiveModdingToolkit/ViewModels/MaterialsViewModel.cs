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
using CollectiveModdingToolkit.Materials;
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectiveModdingToolkit.ViewModels;

/// <summary>A material file found in Data or a mod.</summary>
public sealed record MaterialListItem(string Relative, string FullPath, string? Mod)
{
	public string Name => Path.GetFileName(Relative);
}

/// <summary>Browses the game's loose materials and edits one at a time.</summary>
public sealed partial class MaterialsViewModel : GameViewModelBase
{
	private static readonly string[] ListedExtensions = [".bgsm", ".bgem", ".json"];

	private readonly IDialogService _dialogs;
	private readonly IShellService _shell;
	private List<MaterialListItem> _all = [];

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

		_reverting = true;
		SelectedItem = Items.FirstOrDefault(i => i.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase));
		_reverting = false;
		return LoadDocument(path);
	}

	private bool LoadDocument(string path)
	{
		try
		{
			Document = MaterialDocument.Load(path);
			SetStatus(Document.SourceFormat == MaterialFormat.Json
				? "This material is JSON. Saving will convert it to the binary format."
				: string.Empty);
			return true;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
		{
			Document = null;
			SetStatus($"Couldn't open {Path.GetFileName(path)}: {e.Message}", true);
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
			var found = await Task.Run(() =>
			{
				var index = Scanner.IndexFolder(game, "Materials");
				return index.Values
					.Where(e => ListedExtensions.Contains(Path.GetExtension(e.Relative),
						StringComparer.OrdinalIgnoreCase))
					.Select(e => new MaterialListItem(e.Relative, Scanner.GetSourcePath(game.Manager, e),
						Scanner.GetMod(game.Manager, e)))
					.OrderBy(i => i.Relative, StringComparer.OrdinalIgnoreCase)
					.ToList();
			});

			// A refresh may have replaced the game while this ran.
			if (!ReferenceEquals(game, Session.Game)) return;

			_all = found;
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
			? "No loose materials found."
			: shown.Count == _all.Count
				? $"{_all.Count} loose materials"
				: $"{shown.Count} of {_all.Count} loose materials";
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

	private bool CanSave() => Document is { } d && (d.IsDirty || d.SourceFormat == MaterialFormat.Json);

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

		var path = await _dialogs.PickMaterialToSaveAsync(suggested, Path.GetDirectoryName(document.Path));
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

	/// <summary>A material's path below Data, e.g. "Materials\Foo\Bar.bgsm". Null if it can't be worked out.</summary>
	private string? DataRelativePath(string path)
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
		var marker = Path.DirectorySeparatorChar + "Materials" + Path.DirectorySeparatorChar;
		var index = full.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
		return index >= 0 ? full[(index + 1)..] : null;
	}

	[RelayCommand(CanExecute = nameof(HasOpenDocument))]
	private async Task SaveToOverwriteAsync()
	{
		if (Document is not { } document || Session.Game?.Manager.OverwritePath is not { } overwrite) return;

		if (DataRelativePath(document.Path) is not { } relative)
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
		if (Document != null) _shell.Reveal(Document.Path);
	}
}
