using FileTransferTool.Helpers;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

class Program
{
    private const int BlockSize = 1024 * 1024; // 1MB of data per block
    private const int MaxRetries = 3;
    const int WorkerCount = 2;
    static void Main()
    {
        Console.Write("Source file path: ");
        string sourcePath = Console.ReadLine()!;

        if (!File.Exists(sourcePath))
        {
            Console.WriteLine("Source file not found.");
            return;
        }

        Console.Write("Destination folder path: ");
        string destinationFolder = Console.ReadLine()!;

        if (!Directory.Exists(destinationFolder))
        {
            Console.WriteLine("Destination folder not found.");
            return;
        }

        string destinationPath = Path.Combine(
            destinationFolder,
            Path.GetFileName(sourcePath)
        );

        long totalBytes = new FileInfo(sourcePath).Length;
        long totalBlocks = (totalBytes + BlockSize - 1) / BlockSize;
        using (var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
        {
            fs.SetLength(totalBytes);
        }

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
            Console.WriteLine("Transfer failed:");
            foreach (var e in errors)
                Console.WriteLine(e);
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Block checksums:");
        foreach (var entry in blockChecksums.OrderBy(e => e.Key))
        {
            Console.WriteLine($"position = {entry.Key}, hash = {entry.Value}");
        }

        Console.WriteLine();
        Console.WriteLine("Final SHA256 checksums:");
        string sourceShaHash = ComputeFileHash(sourcePath);
        string destinationShaHash = ComputeFileHash(destinationPath);

        Console.WriteLine($"Source SHA256:      {sourceShaHash}");
        Console.WriteLine($"Destination SHA256: {destinationShaHash}");

        Console.WriteLine(sourceShaHash == destinationShaHash ? "Files match" : "Files do not match");

        static void RunWorker(
        string sourcePath,
        string destinationPath,
        long totalBytes,
        ConcurrentQueue<long> blockQueue,
        ConcurrentDictionary<long, string> blockChecksums,
        ConcurrentBag<string> errors)
        {
            using var source = new FileBlockStreamer(sourcePath, FileAccess.Read, BlockSize, FileShare.Read);
            using var destination = new FileBlockStreamer(destinationPath, FileAccess.ReadWrite, BlockSize, FileShare.ReadWrite);
            using MD5 md5 = MD5.Create();

            while (blockQueue.TryDequeue(out long blockIndex))
            {
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

                string sourceHash = ConvertToHex(md5.ComputeHash(data));
                bool valid = false;

                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    destination.WriteBlock(blockIndex, data);

                    byte[] verify;
                    try
                    {
                        verify = destination.ReadBack(blockIndex, data.Length);
                    }
                    catch(Exception ex)
                    {
                        errors.Add(ex.Message);
                        return;
                    }

                    string destHash = ConvertToHex(md5.ComputeHash(verify));

                    if (sourceHash == destHash)
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
    }
    static string ComputeFileHash(string path)
    {
        using SHA256 sha256 = SHA256.Create();
        using FileStream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        return ConvertToHex(sha256.ComputeHash(stream));
    }
    static string ConvertToHex(byte[] bytes)
    {
        StringBuilder sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
