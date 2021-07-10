using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileBlockCompression
{
	class FileBlockCompressor : IFileCompressor
	{
		const int numThreads = 2;
		const int threadMaxBufferSizeInBytes = 1 << 21; // 1 << 21 == 1 Mb
		const int maxBufferSizeInBytes = threadMaxBufferSizeInBytes * numThreads;

		ManualResetEvent[] resetEvents;

		private ConcurrentDictionary<int, byte[]> streamsToProcess = new ConcurrentDictionary<int, byte[]>();
		private ConcurrentDictionary<int, byte[]> streamsProcessed = new ConcurrentDictionary<int, byte[]>();

		#region compress
		public void Compress(string inputFileName, string outputFileName)
		{
			if (Path.GetExtension(outputFileName) != ".gz")
			{
				throw new ArgumentException("Output file extension must be '.gz'");
			}
			var fileToCompress = new FileInfo(Path.GetFullPath(inputFileName));
			var fileCompressed = new FileInfo(Path.GetFullPath(outputFileName));
			using (var streamToCompress = fileToCompress.OpenRead())
			{
				using (var streamCompressed = fileCompressed.OpenWrite())
				{
					var sizeInBytes = fileToCompress.Length;
					int blockSize = 0;
					for (long currentPosition = 0; currentPosition < sizeInBytes; currentPosition += blockSize)
					{
						int chunkSize = GetChunkSize(streamToCompress);
						blockSize = CompressBlock(streamToCompress, streamCompressed, chunkSize);
					}
					streamCompressed.Flush();
				}
			}
		}

		private int GetChunkSize(FileStream stream)
		{
			long totalBytesToRead = stream.Length;
			int chunkSize = threadMaxBufferSizeInBytes;
			if (totalBytesToRead < numThreads)
			{
				chunkSize = numThreads;
			}
			if (totalBytesToRead < maxBufferSizeInBytes)
			{
				chunkSize = (int)Math.Ceiling((double)totalBytesToRead / numThreads);
			}
			return chunkSize;
		}

		private int CompressBlock(FileStream streamToCompress, FileStream streamCompressed, int chunkSize)
		{
			int blockSize = 0;
			for (int threadIdx = 0; threadIdx < numThreads; threadIdx++)
			{
				byte[] buffer = new byte[chunkSize];
				var bytesReadCount = streamToCompress.Read(buffer, 0, chunkSize);
				if (bytesReadCount == 0)
				{
					break;
				}
				if (bytesReadCount != chunkSize)
				{
					Array.Resize<byte>(ref buffer, bytesReadCount);
				}
				blockSize += bytesReadCount;
				var stream = new MemoryStream(buffer);
				streamsToProcess.TryAdd(threadIdx, buffer);
			}
			int realThreadsCount = streamsToProcess.Count;
			if (realThreadsCount == 0)
				return 0;

			resetEvents = new ManualResetEvent[realThreadsCount];
			for (int threadIdx = 0; threadIdx < realThreadsCount; threadIdx++)
			{
				resetEvents[threadIdx] = new ManualResetEvent(false);
			}

			for (int threadIdx = 0; threadIdx < realThreadsCount; threadIdx++)
			{
				if (streamsToProcess.TryRemove(threadIdx, out var bytes))
				{
					var tid = threadIdx;
					Thread t = new Thread(() => CompressAndStashChunk(bytes, tid));
					t.Start();
				}
			}
			WaitHandle.WaitAll(resetEvents);


			for (int threadIdx = 0; threadIdx < realThreadsCount; threadIdx++)
			{
				if (streamsProcessed.TryRemove(threadIdx, out var chunkCompressed))
				{
					streamCompressed.Write(BitConverter.GetBytes(chunkCompressed.Length), 0, 4);
					streamCompressed.Write(chunkCompressed, 0, chunkCompressed.Length);
				}
			}
			return blockSize;
		}

		private void CompressAndStashChunk(byte[] chunk, int threadIdx)
		{
			using (MemoryStream outStream = new MemoryStream())
			{
				using (GZipStream compressionStream = new GZipStream(outStream, CompressionMode.Compress))
				using (var mStream = new MemoryStream(chunk))
				{
					mStream.CopyTo(compressionStream);
				}
				streamsProcessed.TryAdd(threadIdx, outStream.ToArray());
			}
			resetEvents[threadIdx].Set();
		}
		#endregion

		public void Decompress(string inputFileName, string outputFileName)
		{
			if (Path.GetExtension(inputFileName) != ".gz")
			{
				throw new ArgumentException("Input file extension must be '.gz'");
			}
			var fileToProcess = new FileInfo(Path.GetFullPath(inputFileName));
			var fileProcessed = new FileInfo(Path.GetFullPath(outputFileName));
			using (var streamToProcess = fileToProcess.OpenRead())
			{
				using (var streamProcessed = fileProcessed.OpenWrite())
				{
					var sizeInBytes = fileToProcess.Length;

					while (true)
					{
						var blockSize = DecompressBlock(streamToProcess, streamProcessed);
						if (blockSize == 0)
						{
							break;
						}
					}
					streamProcessed.Flush();
				}
			}
		}

		private int DecompressBlock(FileStream streamToProcess, FileStream streamProcessed)
		{
			var currentPosition = streamToProcess.Position;
			int blockSize = 0;
			try
			{
				byte[] chunkSizeBuffer = new byte[4];
				for (int threadIdx = 0; threadIdx < numThreads; threadIdx++)
				{
					if (streamToProcess.Read(chunkSizeBuffer, 0, 4) == 0)
					{
						break;
					}
					int chunkSize = BitConverter.ToInt32(chunkSizeBuffer, 0);
					blockSize += chunkSize;

					byte[] dataBuffer = new byte[chunkSize];
					streamToProcess.Read(dataBuffer, 0, chunkSize);
					streamsToProcess.TryAdd(threadIdx, dataBuffer);
				}
				int realThreadsCount = streamsToProcess.Count;
				if (realThreadsCount == 0)
					return 0;

				resetEvents = new ManualResetEvent[realThreadsCount];
				for (int threadIdx = 0; threadIdx < realThreadsCount; threadIdx++)
				{
					resetEvents[threadIdx] = new ManualResetEvent(false);
				}

				for (int threadIdx = 0; threadIdx < realThreadsCount; threadIdx++)
				{
					if (streamsToProcess.TryRemove(threadIdx, out var bytes))
					{
						var tid = threadIdx;
						Thread t = new Thread(() => DecompressAndStashChunk(bytes, tid));
						t.Start();
					}
				}
				WaitHandle.WaitAll(resetEvents);


				for (int threadIdx = 0; threadIdx < realThreadsCount; threadIdx++)
				{
					if (streamsProcessed.TryRemove(threadIdx, out var chunkDecompressed))
					{
						streamProcessed.Write(chunkDecompressed, 0, chunkDecompressed.Length);
					}
				}
			}
			catch (IOException e)
			{
				Console.WriteLine("Error occured on reading input file. " + e.Message);
				throw;
			}
			return blockSize;
		}
		private void DecompressAndStashChunk(byte[] chunk, int threadIdx)
		{
			using (MemoryStream temp = new MemoryStream(chunk))
			using (GZipStream decompressingStream = new GZipStream(temp, CompressionMode.Decompress))
			using (MemoryStream outStream = new MemoryStream())
			{
				decompressingStream.CopyTo(outStream);
				streamsProcessed.TryAdd(threadIdx, outStream.ToArray());
			}
			resetEvents[threadIdx].Set();
		}

	}
}
