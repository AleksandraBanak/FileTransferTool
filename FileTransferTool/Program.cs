using System.Security.Cryptography;
using System.Text;

class Program
{
    private const int BlockSize = 1024 * 1024; // 1MB of data per block
    private const int MaxRetries = 3;
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

        Dictionary<long, string> blockChecksums = new();

        using (FileStream source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (FileStream destination = new FileStream(destinationPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            using MD5 md5 = MD5.Create();
            byte[] buffer = new byte[BlockSize];
            long offset = 0;

            int bytesRead;
            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                byte[] blockData = new byte[bytesRead];
                Array.Copy(buffer, blockData, bytesRead);

                string sourceHash = ConvertToHex(md5.ComputeHash(blockData));
                bool verified = false;

                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    destination.Seek(offset, SeekOrigin.Begin);
                    destination.Write(blockData, 0, bytesRead);
                    destination.Flush();

                    destination.Seek(offset, SeekOrigin.Begin);
                    byte[] verifyBuffer = new byte[bytesRead];
                    destination.Read(verifyBuffer, 0, bytesRead);

                    string destHash = ConvertToHex(md5.ComputeHash(verifyBuffer));

                    if (sourceHash == destHash)
                    {
                        blockChecksums[offset] = sourceHash;
                        verified = true;
                        break;
                    }
                }

                if (!verified)
                {
                    Console.WriteLine($"Block at offset {offset} failed verification.");
                    return;
                }

                offset += bytesRead;
            }
        }

        Console.WriteLine();
        Console.WriteLine("Block checksums:");
        foreach (var entry in blockChecksums)
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
