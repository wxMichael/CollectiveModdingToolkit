//using System.Diagnostics;

namespace MO2VFSBypass;

internal class Program
{
	private static void Main(string[] args)
	{
		string dataPath = @"C:\Program Files (x86)\Steam\steamapps\common\Fallout 4\Data";
		string pluginPath = Path.Combine(dataPath, "community fixes merged.esp");
		Console.WriteLine($"MO2VFSBypass: community fixes merged.esp Exists: {File.Exists(pluginPath)}");

		//string bypassPath = @"C:\Users\Michael\source\repos\MO2VFSBypass\GameOverlayUI.exe";
		//ProcessStartInfo info = new()
		//{
		//	CreateNoWindow = false,
		//	UseShellExecute = false,
		//	FileName = bypassPath,
		//};
		//Process? process = Process.Start(info);

		//Console.ReadLine();
	}
}
