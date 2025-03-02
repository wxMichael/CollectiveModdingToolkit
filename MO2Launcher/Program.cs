using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MO2Launcher
{
	internal class Program
	{
#pragma warning disable IDE0060 // Remove unused parameter
		private static void Main(string[] args)
#pragma warning restore IDE0060 // Remove unused parameter
		{
			//string appFolder = @"C:\Users\Michael\source\repos\CollectiveModdingToolkit\CollectiveModdingToolkit\bin\Release\net9.0-windows\win-x64\publish\";
			string appFolder = Environment.CurrentDirectory;
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
			Process process = Process.Start(info);
			process.WaitForExit();
			if (process.ExitCode != 0)
			{
				ShowConsole();
				Console.WriteLine($"ExitCode: {process.ExitCode}");
				Console.ReadLine();
			}
		}

		[DllImport("kernel32.dll", EntryPoint = "GetStdHandle", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
		public static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll", EntryPoint = "AllocConsole", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
		public static extern int AllocConsole();

		private const int STD_OUTPUT_HANDLE = -11;
		private const int MY_CODE_PAGE = 437;

		private static void ShowConsole()
		{
			AllocConsole();
			IntPtr stdHandle = GetStdHandle(STD_OUTPUT_HANDLE);
			Microsoft.Win32.SafeHandles.SafeFileHandle safeFileHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(stdHandle, true);
			FileStream fileStream = new FileStream(safeFileHandle, FileAccess.Write);
			System.Text.Encoding encoding = System.Text.Encoding.GetEncoding(MY_CODE_PAGE);
			StreamWriter standardOutput = new StreamWriter(fileStream, encoding)
			{
				AutoFlush = true
			};
			Console.SetOut(standardOutput);
		}
	}
}
