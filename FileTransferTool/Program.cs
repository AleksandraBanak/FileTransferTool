class Program
{
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

    }
}
