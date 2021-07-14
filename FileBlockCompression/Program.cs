using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using System.IO;

namespace FileBlockCompression
{
	class Program
	{

		static string commandArgsFormat = "FileBlockCompression.exe compress/decompress [input file name] [output file name]";
		static readonly string[] actions = { "compress", "decompress" } ; 

		private readonly struct CommandArgsInfo
		{
			public readonly string method;
			public readonly string inputFile;
			public readonly string outputFile;

			public CommandArgsInfo(string[] args) : this()
			{
				method = args[0];
				inputFile = args[1];
				outputFile = args[2];
			}
		}

		static CommandArgsInfo GetCommandLineArgs(string[] args)
		{
			if (args.Length < 3)
			{
				throw new ArgumentException("Not enough command line arguments");
			}
			CommandArgsInfo info = new CommandArgsInfo(args);
			if (!actions.Contains(info.method))
			{
				throw new ArgumentException("Invalid method");
			}
			if (info.inputFile == null || info.outputFile == null)
			{
				throw new ArgumentException("Invalid input or output file");
			}
			return info;
		}

		static void Main(string[] args)
		{
			try
			{
				var commandArgsInfo = GetCommandLineArgs(args);
				IFileCompressor fileCompressor = new FileBlockCompressor(2, 1 << 21);
				if (File.Exists(commandArgsInfo.outputFile))
				{
					File.Delete(commandArgsInfo.outputFile);
				}
				if (commandArgsInfo.method == "compress")
				{
					fileCompressor.Compress(commandArgsInfo.inputFile, commandArgsInfo.outputFile);
				}
				else
				{
					fileCompressor.Decompress(commandArgsInfo.inputFile, commandArgsInfo.outputFile);
				}
				//fileCompressor.Compress(commandArgsInfo.inputFile, commandArgsInfo.outputFile);
				//fileCompressor.Decompress(commandArgsInfo.outputFile, "decompr_" + commandArgsInfo.inputFile);
				Console.WriteLine($"Successfully {commandArgsInfo.method}ed");
			}
			catch (ArgumentException e)
			{
				Console.WriteLine(e.Message);
				Console.WriteLine("Please provide command arguments as follows: " + commandArgsFormat);
			}
		}

		static void Test()
		{
			var inputString = "“ ... ”";
			byte[] compressed;
			string output;

			using (var outStream = new MemoryStream())
			{
				using (var tinyStream = new GZipStream(outStream, CompressionMode.Compress))
				using (var mStream = new MemoryStream(Encoding.UTF8.GetBytes(inputString)))
					mStream.CopyTo(tinyStream);

				compressed = outStream.ToArray();
			}

			// “compressed” now contains the compressed string.
			// Also, all the streams are closed and the above is a self-contained operation.

			using (var inStream = new MemoryStream(compressed))
			using (var bigStream = new GZipStream(inStream, CompressionMode.Decompress))
			using (var bigStreamOut = new MemoryStream())
			{
				bigStream.CopyTo(bigStreamOut);
				output = Encoding.UTF8.GetString(bigStreamOut.ToArray());
			}

			// “output” now contains the uncompressed string.
			Console.WriteLine(output);
		}
	}
}
