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
		private readonly int numThreads;
		private readonly int threadMaxBufferSizeInBytes;
		private readonly int maxBufferSizeInBytes;

		ManualResetEvent[] resetEvents;

		private readonly ConcurrentDictionary<int, byte[]> byteArraysToProcess = new ConcurrentDictionary<int, byte[]>();
		private readonly ConcurrentDictionary<int, byte[]> byteArraysProcessed = new ConcurrentDictionary<int, byte[]>();
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="numThreads">Number of threads</param>
		/// <param name="maxThreadBufferSize"> Max thread buffer size in bytes for reading file</param>
		public FileBlockCompressor(int numThreads, int maxThreadBufferSize)
		{
			this.numThreads = numThreads;
			threadMaxBufferSizeInBytes = maxThreadBufferSize;
			maxBufferSizeInBytes = maxThreadBufferSize * numThreads;
		}

		#region compress

		public void Compress(string inputFileName, string outputFileName)
		{
			if (Path.GetExtension(outputFileName) != ".gz")
			{
				throw new ArgumentException("Output file extension must be '.gz'");
			}
			var fileToCompress = new FileInfo(Path.GetFullPath(inputFileName));
			using (var streamToCompress = fileToCompress.OpenRead())
			{
				var fileCompressed = new FileInfo(Path.GetFullPath(outputFileName));
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
		/// <summary>
		/// Determines chunk size to read passed file stream
		/// </summary>
		/// <param name="stream"></param>
		/// <returns>Chunk size in bytes</returns>
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

		private int CompressBlock(FileStream streamToProcess, FileStream streamProcessed, int chunkSize)
		{
			try
			{
				int blockSize = ReadBlockToCompress(streamToProcess, chunkSize, out var realThreadsCount);

				resetEvents = new ManualResetEvent[realThreadsCount];
				for (int threadIdx = 0; threadIdx < realThreadsCount; threadIdx++)
				{
					resetEvents[threadIdx] = new ManualResetEvent(false);
				}

				for (int threadIdx = 0; threadIdx < realThreadsCount; threadIdx++)
				{
					if (byteArraysToProcess.TryRemove(threadIdx, out var bytes))
					{
						var tid = threadIdx;
						Thread t = new Thread(() => CompressAndStashChunk(bytes, tid));
						t.Start();
					}
				}
				WaitHandle.WaitAll(resetEvents);


				for (int threadIdx = 0; threadIdx < realThreadsCount; threadIdx++)
				{
					if (byteArraysProcessed.TryRemove(threadIdx, out var chunkCompressed))
					{
						streamProcessed.Write(BitConverter.GetBytes(chunkCompressed.Length), 0, 4);
						streamProcessed.Write(chunkCompressed, 0, chunkCompressed.Length);
					}
				}
				return blockSize;
			}
			catch (IOException e)
			{
				Console.WriteLine("Error occured on reading input file. " + e.Message);
				throw;
			}
		}

		private int ReadBlockToCompress(FileStream streamToProcess, int chunkSize, out int realThreadsCount)
		{
			int blockSize = 0;
			for (int threadIdx = 0; threadIdx < numThreads; threadIdx++)
			{
				byte[] buffer = new byte[chunkSize];
				var bytesReadCount = streamToProcess.Read(buffer, 0, chunkSize);
				if (bytesReadCount == 0)
				{
					break;
				}
				if (bytesReadCount != chunkSize)
				{
					Array.Resize(ref buffer, bytesReadCount);
				}
				blockSize += bytesReadCount;
				byteArraysToProcess.TryAdd(threadIdx, buffer);
			}
			realThreadsCount = byteArraysToProcess.Count;
			if (realThreadsCount == 0)
				return 0;
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
				byteArraysProcessed.TryAdd(threadIdx, outStream.ToArray());
			}
			resetEvents[threadIdx].Set();
		}
		#endregion
		
		#region decompress

		public void Decompress(string inputFileName, string outputFileName)
		{
			if (Path.GetExtension(inputFileName) != ".gz")
			{
				throw new ArgumentException("Input file extension must be '.gz'");
			}
			var fileToProcess = new FileInfo(Path.GetFullPath(inputFileName));
			using (var streamToProcess = fileToProcess.OpenRead())
			{
				var fileProcessed = new FileInfo(Path.GetFullPath(outputFileName));
				using (var streamProcessed = fileProcessed.OpenWrite())
				{
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
			try
			{
				var currentPosition = streamToProcess.Position;
				int blockSize = ReadBlockToDecompress(streamToProcess, out var realThreadsCount);

				resetEvents = new ManualResetEvent[realThreadsCount];
				for (int threadIdx = 0; threadIdx < realThreadsCount; threadIdx++)
				{
					resetEvents[threadIdx] = new ManualResetEvent(false);
				}

				for (int threadIdx = 0; threadIdx < realThreadsCount; threadIdx++)
				{
					if (byteArraysToProcess.TryRemove(threadIdx, out var bytes))
					{
						var tid = threadIdx;
						Thread t = new Thread(() => DecompressAndStashChunk(bytes, tid));
						t.Start();
					}
				}
				WaitHandle.WaitAll(resetEvents);

				for (int threadIdx = 0; threadIdx < realThreadsCount; threadIdx++)
				{
					if (byteArraysProcessed.TryRemove(threadIdx, out var chunkDecompressed))
					{
						streamProcessed.Write(chunkDecompressed, 0, chunkDecompressed.Length);
					}
				}
				return blockSize;
			}
			catch (IOException e)
			{
				Console.WriteLine("Error occured on reading input file. " + e.Message);
				throw;
			}
		}

		private int ReadBlockToDecompress(FileStream streamToProcess, out int realThreadsCount)
		{
			int blockSize = 0;
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
				byteArraysToProcess.TryAdd(threadIdx, dataBuffer);
			}
			realThreadsCount = byteArraysToProcess.Count;
			if (realThreadsCount == 0)
				return 0;
			return blockSize;
		}

		private void DecompressAndStashChunk(byte[] chunk, int threadIdx)
		{
			using (MemoryStream temp = new MemoryStream(chunk))
			using (GZipStream decompressingStream = new GZipStream(temp, CompressionMode.Decompress))
			using (MemoryStream outStream = new MemoryStream())
			{
				decompressingStream.CopyTo(outStream);
				byteArraysProcessed.TryAdd(threadIdx, outStream.ToArray());
			}
			resetEvents[threadIdx].Set();
		}
		#endregion
	}
}
