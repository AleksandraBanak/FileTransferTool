using FileTransferTool.Services;

class Program
{
    static void Main()
    {
        IValidationService validationService = new ValidationService();
        IHashService hashService = new HashService();
        IFileTransferService fileTransferService = new FileTransferService(hashService);

        Console.Write("Source file path: ");
        string sourcePath = Console.ReadLine()!;

        Console.Write("Destination folder path: ");
        string destinationFolder = Console.ReadLine()!;

        if (!validationService.ValidateFilePaths(sourcePath, destinationFolder))
        {
            Console.WriteLine("Invalid file locations provided");
            return;
        }

        fileTransferService.TransferFile(sourcePath, destinationFolder);
    }
}
