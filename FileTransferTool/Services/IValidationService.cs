namespace FileTransferTool.Services
{
    interface IValidationService
    {
        bool ValidateFilePaths(string sourcePath, string destinationFolder);
    }
}
