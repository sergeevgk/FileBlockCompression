using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileBlockCompression
{
	public interface IFileCompressor
	{
		void Compress(string inputFileName, string outputFileName);
		void Decompress(string inputFileName, string outputFileName);

	}
}
