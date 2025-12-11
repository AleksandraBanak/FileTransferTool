namespace FileTransferTool.Services
{
    interface IHashService
    {
        string ComputeMd5(byte[] data);
        string ComputeSha256(string path);
        string ConvertToHex(byte[] bytes);
    }
}
