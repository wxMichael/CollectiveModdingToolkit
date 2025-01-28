using System.Diagnostics;
using System.IO;

namespace MO2Launcher
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			string appFolder = @"C:\Users\Michael\source\repos\CollectiveModdingToolkit\CollectiveModdingToolkit\bin\publish\";
			string appName = "CollectiveModdingToolkit.exe";
			string appPath = Path.Combine(appFolder, appName);
			string renameTo = "GameOverlayUI.exe";
			string renamePath = Path.Combine(appFolder, renameTo);
			if (File.Exists(appPath))
				File.Move(appPath, renamePath);
			ProcessStartInfo info = new ProcessStartInfo()
			{
				CreateNoWindow = false,
				UseShellExecute = true,
				LoadUserProfile = true,
				FileName = renamePath,
			};
			Process.Start(info)?.WaitForExit();
		}
	}
}
