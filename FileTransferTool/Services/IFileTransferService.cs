namespace FileTransferTool.Services
{
    interface IFileTransferService
    {
        void TransferFile(string sourcePath, string destinationFolder);
    }
}
