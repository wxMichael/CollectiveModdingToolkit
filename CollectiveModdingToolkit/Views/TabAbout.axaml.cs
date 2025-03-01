using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Threading.Tasks;

namespace CollectiveModdingToolkit.Views;

public partial class TabAbout : UserControl
{
	private const string _nexusURL = "https://www.nexusmods.com/fallout4/mods/87907";
	private const string _discordURL = "https://discord.gg/tktyEyYHZH";
	private const string _githubURL = "https://github.com/wxMichael/Collective-Modding-Toolkit";

	public TabAbout() => InitializeComponent();

	//private static string EscapeURL(string url) => url.Replace("%", "^%").Replace("#", "^#").Replace("&", "^&");

	private async void CopyURL(string url) => await Task.Run(() => TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(url));

	private static void OpenFlyout(object? sender)
	{
		if (sender is Control control && FlyoutBase.GetAttachedFlyout(control) is FlyoutBase fo)
		{
			control.IsEnabled = false;
			fo.Closed += (sender, e) => { control.IsEnabled = true; };
			fo.ShowAt(control);
			DispatcherTimer.RunOnce(fo.Hide, new(0, 0, 2));
		}
	}

	private void Button_Click_OpenNexus(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_nexusURL);
	private void Button_Click_CopyNexus(object? sender, RoutedEventArgs e)
	{
		CopyURL(_nexusURL);
		OpenFlyout(sender);
	}

	private void Button_Click_OpenDiscord(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_discordURL);
	private void Button_Click_CopyDiscord(object? sender, RoutedEventArgs e)
	{
		CopyURL(_discordURL);
		OpenFlyout(sender);
	}

	private void Button_Click_OpenGithub(object? sender, RoutedEventArgs e) => Utilities.OpenURL(_githubURL);
	private void Button_Click_CopyGithub(object? sender, RoutedEventArgs e)
	{
		CopyURL(_githubURL);
		OpenFlyout(sender);
	}
}
