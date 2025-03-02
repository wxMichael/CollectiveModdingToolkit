using Avalonia.Controls;
using System;

namespace CollectiveModdingToolkit.Views;

public partial class ArchivePatcher : Window
{
	public ArchivePatcher() => InitializeComponent();

	private void TextBox_TextChanged(object? sender, TextChangedEventArgs e)
	{
		//archivePatcherList.ItemsSource = new string[] { "One", "Two", "Three" };
		Console.WriteLine();
		Console.WriteLine();
	}
}
