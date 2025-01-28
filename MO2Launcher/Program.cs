using System.Diagnostics;

namespace MO2Launcher
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			string exeName = "CollectiveModdingToolkit";
			//string exeName = "GameOverlayUI";
			ProcessStartInfo info = new ProcessStartInfo()
			{
				CreateNoWindow = false,
				UseShellExecute = true,
				LoadUserProfile = true,
				FileName = $@"C:\Users\Michael\source\repos\CollectiveModdingToolkit\CollectiveModdingToolkit\bin\publish\{exeName}.exe",
			};
			Process.Start(info)?.WaitForExit();
		}
	}
}
