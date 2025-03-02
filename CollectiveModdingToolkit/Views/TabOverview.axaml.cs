using Avalonia.Controls;
using Avalonia.Interactivity;
using CollectiveModdingToolkit.Models;
using System.Diagnostics;
using System.IO;

namespace CollectiveModdingToolkit.Views;

public partial class TabOverview : UserControl
{
	public TabOverview() => InitializeComponent();

	private void BinariesList_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
	{
		e.PreventGestureRecognition();
		e.Handled = true;
	}

	private void BinariesList_PointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
	{
		//MainWindow.CriticalError(message: sender?.GetType().ToString());
		Binary binary = (Binary)((DataGridCell)sender!).DataContext!;
		binary.DisplayText = binary.Version ?? binary.GameVersion;
		//e.PreventGestureRecognition();
		e.Handled = true;
	}

	private void BinariesList_PointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
	{
		Binary binary = (Binary)((DataGridCell)sender!).DataContext!;
		binary.DisplayText = binary.GameVersion;
		//e.PreventGestureRecognition();
		e.Handled = true;
	}

	internal void GamePath_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
	{
		if (Game.Instance == null)
			return;
		Process.Start(new ProcessStartInfo()
		{
			FileName = Game.Instance.GamePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
			UseShellExecute = true,
			Verb = "open"
		});
	}

	private void Button_Click_OpenDowngradeManager(object? sender, RoutedEventArgs e)
	{
		//ArchivePatcher patcher = new();
		//patcher.Show();
	}

	private void Button_Click_OpenArchivePatcher(object? sender, RoutedEventArgs e)
	{
		ArchivePatcher archivePatcher = new()
		{
			DataContext = DataContext
		};
		archivePatcher.Show();
	}
}
