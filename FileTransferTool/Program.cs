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
            using FileStream source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream destination = new FileStream(destinationPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using MD5 md5 = MD5.Create();

            while (blockQueue.TryDequeue(out long blockIndex))
            {
                long offset = blockIndex * BlockSize;
                int bytesToRead = (int)Math.Min(BlockSize, totalBytes - offset);

                byte[] buffer = new byte[bytesToRead];

                source.Seek(offset, SeekOrigin.Begin);
                int totalRead = 0;

                while (totalRead < bytesToRead)
                {
                    int read = source.Read(buffer, totalRead, bytesToRead - totalRead);

                    if (read == 0)
                    {
                        errors.Add($"Unexpected end of file at offset {offset}");
                        return;
                    }

                    totalRead += read;
                }

                string sourceHash = ConvertToHex(md5.ComputeHash(buffer));

                bool valid = false;

                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    destination.Seek(offset, SeekOrigin.Begin);
                    destination.Write(buffer, 0, bytesToRead);
                    destination.Flush();

                    destination.Seek(offset, SeekOrigin.Begin);
                    byte[] verify = new byte[bytesToRead];
                    destination.Read(verify, 0, bytesToRead);

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
