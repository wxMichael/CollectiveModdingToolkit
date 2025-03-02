using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CollectiveModdingToolkit.ViewModels;
using CollectiveModdingToolkit.Views;
using System.Linq;

namespace CollectiveModdingToolkit;

public partial class App : Application
{
	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			DisableAvaloniaDataAnnotationValidation();
			desktop.MainWindow = new MainWindow
			{
				DataContext = new MainWindowViewModel(),
			};
		}

		base.OnFrameworkInitializationCompleted();
	}

	private static void DisableAvaloniaDataAnnotationValidation()
	{
		DataAnnotationsValidationPlugin[] dataValidationPluginsToRemove =
			[.. BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>()];

		foreach (DataAnnotationsValidationPlugin? plugin in dataValidationPluginsToRemove)
		{
			BindingPlugins.DataValidators.Remove(plugin);
		}
	}
}
