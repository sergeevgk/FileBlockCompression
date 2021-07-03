using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileBlockCompression
{
	internal class FileCompressor : IFileCompressor
	{
		public void Compress(string inputFileName, string outputFileName)
		{
            if (Path.GetExtension(outputFileName) != ".gz")
			{
                throw new ArgumentException("Output file extension must be '.gz'");
			}
			FileInfo fileToCompress = new FileInfo(Path.GetFullPath(inputFileName));
            using (FileStream originalFileStream = fileToCompress.OpenRead())
            {
                if ((File.GetAttributes(fileToCompress.FullName) &
                   FileAttributes.Hidden) != FileAttributes.Hidden & fileToCompress.Extension != ".gz")
                {
                    using (FileStream compressedFileStream = File.Create(Path.GetFullPath(outputFileName)))
                    {
                        using (GZipStream compressionStream = new GZipStream(compressedFileStream,
                           CompressionMode.Compress))
                        {
                            originalFileStream.CopyTo(compressionStream);
                        }
                    }
                    FileInfo info = new FileInfo(outputFileName);
                    Console.WriteLine($"Compressed {fileToCompress.Name} from {fileToCompress.Length} to {info.Length} bytes.");
                }
            }
        }

		public void Decompress(string inputFileName, string outputFileName)
		{
            FileInfo fileToDecompress = new FileInfo(Path.GetFullPath(inputFileName));
            using (FileStream originalFileStream = fileToDecompress.OpenRead())
            {
                using (FileStream decompressedFileStream = File.Create(outputFileName))
                {
                    using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                    {
                        decompressionStream.CopyTo(decompressedFileStream);
						Console.WriteLine($"Decompressed: {fileToDecompress.Name}");
					}
                }
            }
        }
	}
}
