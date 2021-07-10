using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using FileBlockCompression;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FileBlockCompressionTests
{
	[TestClass]
	public class FileCompressionTests
	{
		private string[] textInputFiles = { "test1", "test2", "a29" };
		private string projectDirectory = Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).Parent.FullName;

		[TestMethod]
		public void CompressDecompressEquality()
		{
			IFileCompressor fileCompressor = new FileCompressor();
			foreach (var fileName in textInputFiles)
			{
				string txtName = Path.Combine(projectDirectory, "tests", fileName + ".txt");
				string resName = Path.Combine(projectDirectory, "tests", "decompr_" + fileName + ".txt");
				string gzName = Path.Combine(projectDirectory, "tests", fileName + ".gz");

				fileCompressor.Compress(txtName, gzName);
				fileCompressor.Decompress(gzName, resName);

				FileInfo sourceFileInfo = new FileInfo(txtName);
				FileInfo resultFileInfo = new FileInfo(resName);

				Assert.AreEqual(sourceFileInfo.Length, resultFileInfo.Length);

				Assert.IsTrue(File.ReadLines(txtName)
					.SequenceEqual(File.ReadLines(resName)));
			}

		}
	}
}
