namespace FileTransferTool.Services
{
    class ValidationService : IValidationService
    {
        public bool ValidateFilePaths(string sourcePath, string destinationFolder)
        {
            if (!File.Exists(sourcePath))
            {
                Console.WriteLine("Source file not found.");
                return false;
            }

            if (!Directory.Exists(destinationFolder))
            {
                Console.WriteLine("Destination folder not found.");
                return false;
            }

            string sourceFullPath = Path.GetFullPath(sourcePath);
            string destinationPath = Path.Combine(
                destinationFolder,
                Path.GetFileName(sourcePath)
            );

            if (sourceFullPath == Path.GetFullPath(destinationPath))
            {
                Console.WriteLine("Source and destination files are the same.");
                return false;
            }

            if (new FileInfo(sourcePath).Length == 0)
            {
                Console.WriteLine("Source file is empty.");
                return false;
            }

            return true;
        }
    }
}
