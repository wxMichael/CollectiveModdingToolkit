using Avalonia.Controls;

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
		binary.DisplayText = (binary.Version ?? binary.GameVersion).PadRight(10);
		//e.PreventGestureRecognition();
		e.Handled = true;
	}

	private void BinariesList_PointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
	{
		Binary binary = (Binary)((DataGridCell)sender!).DataContext!;
		binary.DisplayText = binary.GameVersion.PadRight(10);
		//e.PreventGestureRecognition();
		e.Handled = true;
	}
}
