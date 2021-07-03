using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;

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
				IFileCompressor fileCompressor = new FileCompressor();
				fileCompressor.Compress(commandArgsInfo.inputFile, commandArgsInfo.outputFile);
				fileCompressor.Decompress(commandArgsInfo.outputFile, "decompr_" + commandArgsInfo.inputFile);
				Console.WriteLine("Successfully compressed / decompressed");
			}
			catch (ArgumentException e)
			{
				Console.WriteLine(e.Message);
				Console.WriteLine("Please provide command arguments as follows: " + commandArgsFormat);
			}
		}
	}
}
