using lwoPatcherCLI;
using System;
using System.Net;

namespace lwoServerPatcher
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("DLU server patcher - v1.0");
			Console.WriteLine("Uses the cache.lbbstudios.net server to pull assets the server needs, skipping most of /res/");
			Console.WriteLine("(navmeshes have to be pulled manually");
			Console.WriteLine("\nMake sure the working dir for this program is set to the directory where your server exes are");

			WebClient webClient = new WebClient();
			DownloadUtils.DownloadVersions(webClient); //These are always re-downloaded. (version/hotfix)

			//Process them in order:
			DownloadUtils.DownloadFilesFromTxt(webClient, @"..\versions\version.txt", false);
			DownloadUtils.DownloadFilesFromTxt(webClient, @"..\versions\index.txt", false);
			DownloadUtils.DownloadFilesFromTxt(webClient, @"..\versions\frontend.txt", true);
			DownloadUtils.DownloadFilesFromTxt(webClient, @"..\versions\trunk.txt", true);

			//Lastly, to make sure we're on top of any hotfixes:
			DownloadUtils.DownloadFilesFromTxt(webClient, @"..\versions\hotfix.txt", false);

			//Get navmeshes, and CDServer.sqlite:


			webClient.Dispose();
		}
	}
}
