using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace lwoPatcherCLI
{
	class DownloadUtils
	{
		protected static string GetMD5HashFromFile(string fileName)
		{
			FileStream file = new FileStream(fileName, FileMode.Open);
			MD5 md5 = new MD5CryptoServiceProvider();
			byte[] retVal = md5.ComputeHash(file);
			file.Close();

			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < retVal.Length; i++)
			{
				sb.Append(retVal[i].ToString("x2"));
			}
			return sb.ToString();
		} //GetMD5HashFromFile

		public static byte[] Decompress(byte[] gzip)
		{
			var ogStream = new MemoryStream(gzip);
			using (var stream = new Ionic.Zlib.ZlibStream(ogStream, Ionic.Zlib.CompressionMode.Decompress))
			{
				var outStream = new MemoryStream();
				const int size = 262145;
				byte[] buffer = new byte[size];

				int read;

				while ((read = stream.Read(buffer, 0, size)) > 0)
				{
					outStream.Write(buffer, 0, read);
				}

				ogStream.Close();
				stream.Close();
				return outStream.ToArray();
			}
		} //Decompress

		public static void DownloadVersions(WebClient webClient)
		{
			Directory.CreateDirectory(@"..\versions\");
			Console.WriteLine("versions.txt");
			webClient.DownloadFile(new Uri("http://cache.lbbstudios.net/luclient/version.txt"), @"..\versions\version.txt");

			Console.WriteLine("hotfix.txt");
			webClient.DownloadFile(new Uri("http://cache.lbbstudios.net/luclient/hotfix.txt"), @"..\versions\hotfix.txt");
		} //DownloadVersions

		public static void DownloadFile(String line, WebClient webClient)
		{
			//Set up some of the vars:
			string[] tokens = line.Split(',');

			//if (tokens.Length < 5) return;
			String fileName = tokens[0];
			int uncompressedSize = Int32.Parse(tokens[1]);
			String uncompressedChecksum = tokens[2];
			int compressedSize = Int32.Parse(tokens[3]);
			String compressedChecksum = tokens[4];

			String sd0Name = uncompressedChecksum[0] + "/" + uncompressedChecksum[1] + "/" + uncompressedChecksum + ".sd0";

			string[] fileNameArray = fileName.Split('/');
			String tempFileName = fileNameArray[fileNameArray.Length - 1] + ".sd0";

			//Create the dirs for this file:
			String foldersToMake = @"..\";
			foreach (string folder in fileNameArray)
			{
				if (!folder.Contains(".")) //!= a file, so it's a folder
				{
					foldersToMake = foldersToMake + @"\" + folder;
				}
				else if (folder == ".mayaSwatches") //exception
				{
					foldersToMake = foldersToMake + @"\" + folder;
				}
			}

			Directory.CreateDirectory(foldersToMake);

			//Make sure we don't already have this file:
			if (File.Exists(@"..\" + fileName))
			{
				if (GetMD5HashFromFile(@"..\" + fileName) == uncompressedChecksum)
				{
					return;
				}
			}

			try
			{
				// try to download file here
				webClient.DownloadFile(new Uri("http://cache.lbbstudios.net/luclient/" + sd0Name), @"..\versions\" + tempFileName);

				//Decompress the sd0 if the file isn't an uncompressed .txt:
				if (fileName != "version.txt" & fileName != "hotfix.txt")
				{
					if (fileNameArray.Length == 1) fileName = @"versions\" + fileName; //If no subdir, just download to /versions/

					byte[] data = File.ReadAllBytes(@"..\versions\" + tempFileName).Skip(0x9).ToArray();
					MemoryStream bos = new MemoryStream(data.Length);

					byte[] buffer = new byte[262144];
					int iLocation = 5;
					bool bIsDecompressing = true;

					while (bIsDecompressing)
					{
						using (var fileStream = File.Open(@"..\versions\" + tempFileName, FileMode.Open))
						using (var binaryStream = new BinaryReader(fileStream))
						{
							binaryStream.ReadInt32();
							binaryStream.ReadChar(); //Skipped the SD0 header
							var totalLength = (int)binaryStream.BaseStream.Length;
							while (iLocation < totalLength)
							{
								int compressedLength = binaryStream.ReadInt32();
								iLocation += sizeof(int);

								if (compressedLength == 0)
								{
									bIsDecompressing = false;
									break;
								}

								int iBytesToSkip = iLocation;

								byte[] compData = binaryStream.ReadBytes(compressedLength);

								byte[] decompBuff = Decompress(compData);
								bos.Write(decompBuff, 0, decompBuff.Length);

								iLocation += compressedLength;
							}

							bIsDecompressing = false;
						}
					}

					// Get the decompressed data  
					byte[] decompressedData = bos.ToArray();

					File.WriteAllBytes(@"..\" + fileName, decompressedData);
					File.Delete(@"..\versions\" + tempFileName);
				}
			}
			catch (WebException ex)
			{
				if (ex.Status == WebExceptionStatus.ProtocolError)
				{
					if (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
					{
						return;
					}

					Console.WriteLine("An exception occurred when downloading " + fileName);
				}
			}

		} //DownloadFile

		public static void DownloadFilesFromTxt(WebClient webClient, String versionTxt, bool shouldThread)
		{
			foreach (string l in File.ReadLines(versionTxt))
			{
				if (l.Contains(".") && !l.Contains("82,")) //To skip any files that don't have a file extension. (and other useless lines)
				{
					string line = l;
					line = line.Replace("client/", "server/");

					if (line.Contains("server/"))
					{
						//skip unwanted files
						if (!line.Contains("/BrickModels/")
						 && !line.Contains("/maps/")
						 && !line.Contains("/macros/")
						 && !line.Contains("/names/")) continue;
					}

					if (line.Contains(".raw")) continue; //large unwanted terrain files
					if (line.Contains(".dds")) continue; //usually minimaps we don't want either

					if (shouldThread)
					{
						string[] tokens = line.Split(',');
						Console.WriteLine(tokens[0]);

						new Task(() => { DownloadFile(line, webClient); }).RunSynchronously();
					}
					else
					{
						DownloadFile(line, webClient);
					}
				}
			} //foreach loop
		} //DownloadFilesFromTxt
	}
}
