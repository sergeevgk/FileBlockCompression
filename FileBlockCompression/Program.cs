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
				method = args[1];
				inputFile = args[2];
				outputFile = args[3];
			}
		}

		static CommandArgsInfo GetCommandLineArgs(string[] args)
		{
			if (args.Length < 4)
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
