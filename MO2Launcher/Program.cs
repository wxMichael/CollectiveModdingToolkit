using System.Diagnostics;

namespace MO2Launcher
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			ProcessStartInfo info = new ProcessStartInfo()
			{
				CreateNoWindow = false,
				UseShellExecute = true,
				LoadUserProfile = true,
				FileName = @"C:\Users\Michael\source\repos\CollectiveModdingToolkit\CollectiveModdingToolkit\bin\publish\GameOverlayUI.exe",
			};
			Process.Start(info)?.WaitForExit();
		}
	}
}
