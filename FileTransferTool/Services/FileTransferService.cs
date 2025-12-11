using System.Collections.Concurrent;
using FileTransferTool.Helpers;

namespace FileTransferTool.Services
{
    class FileTransferService : IFileTransferService
    {
        private const int BlockSize = 1024 * 1024; // 1MB of data per block
        private const int MaxRetries = 3;
        const int WorkerCount = 2;

        private readonly IHashService _hashService;

        public FileTransferService(IHashService hashService)
        {
            _hashService = hashService;
        }

        public void TransferFile(string sourcePath, string destinationFolder)
        {
            string destinationPath = Path.Combine(destinationFolder, Path.GetFileName(sourcePath));
            long totalBytes = new FileInfo(sourcePath).Length;
            long totalBlocks = (totalBytes + BlockSize - 1) / BlockSize;

            using (var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                fs.SetLength(totalBytes);

            ConcurrentQueue<long> blockQueue = new ConcurrentQueue<long>();
            for (long i = 0; i < totalBlocks; i++)
                blockQueue.Enqueue(i);

            ConcurrentDictionary<long, string> blockChecksums = new ConcurrentDictionary<long, string>();
            ConcurrentBag<string> errors = new ConcurrentBag<string>();

            Task[] workers = new Task[WorkerCount];
            for (int i = 0; i < WorkerCount; i++)
            {
                workers[i] = Task.Run(() =>
                    RunWorker(sourcePath, destinationPath, totalBytes, blockQueue, blockChecksums, errors)
                );
            }

            Task.WaitAll(workers);

            if (!errors.IsEmpty)
            {
                foreach (var e in errors)
                    Console.WriteLine(e);
                return;
            }


            PrintBlockChecksums(blockChecksums);
            var result = VerifyFinalHashes(sourcePath, destinationPath);
            PrintFinalVerification(result.srcHash, result.dstHash, result.match);
        }

        private void RunWorker(string sourcePath, string destPath, long totalBytes,
            ConcurrentQueue<long> blockQueue,
            ConcurrentDictionary<long, string> blockChecksums,
            ConcurrentBag<string> errors)
        {
            using var source = new FileBlockStreamer(
                sourcePath, FileAccess.Read, BlockSize, FileShare.Read);

            using var destination = new FileBlockStreamer(
                destPath, FileAccess.ReadWrite, BlockSize, FileShare.ReadWrite);

            while (blockQueue.TryDequeue(out long blockIndex))
            {
                Console.WriteLine($"Thread {Task.CurrentId} processing block {blockIndex}");

                long offset = blockIndex * BlockSize;

                byte[] data;
                try
                {
                    data = source.ReadBlock(blockIndex, totalBytes);
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                    return;
                }

                string sourceHash = _hashService.ComputeMd5(data);
                bool valid = false;

                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    destination.WriteBlock(blockIndex, data);
                    byte[] verify;
                    try
                    {
                        verify = destination.ReadBack(blockIndex, data.Length);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex.Message);
                        return;
                    }

                    string destHash = _hashService.ComputeMd5(verify);
                    if (VerifyHashes(sourceHash, destHash))
                    {
                        blockChecksums[offset] = sourceHash;
                        valid = true;
                        break;
                    }
                }

                if (!valid)
                {
                    errors.Add($"Block at offset {offset} failed verification.");
                    return;
                }
            }
        }

        private bool VerifyHashes(string hash1, string hash2)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(hash1, hash2);
        }
        private (string srcHash, string dstHash, bool match) VerifyFinalHashes(string sourcePath, string destinationPath)
        {
            string srcHash = _hashService.ComputeSha256(sourcePath);
            string dstHash = _hashService.ComputeSha256(destinationPath);
            bool match = VerifyHashes(srcHash, dstHash);

            return (srcHash, dstHash, match);
        }
        private void PrintBlockChecksums(ConcurrentDictionary<long, string> checksums)
        {
            Console.WriteLine("\nBlock checksums:");
            foreach (var entry in checksums.OrderBy(e => e.Key))
                Console.WriteLine($"position = {entry.Key}, hash = {entry.Value}");
        }
        private void PrintFinalVerification(string srcHash, string dstHash, bool match)
        {
            Console.WriteLine("\nFinal SHA256 checksums:");
            Console.WriteLine($"Source SHA256:      {srcHash}");
            Console.WriteLine($"Destination SHA256: {dstHash}");
            Console.WriteLine(match ? "Files match" : "Files do not match");
        }
    }
}
