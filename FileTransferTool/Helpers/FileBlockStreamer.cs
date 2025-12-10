namespace FileTransferTool.Helpers
{
    class FileBlockStreamer : IDisposable
    {
        private readonly FileStream _stream;
        private readonly int _blockSize;

        public FileBlockStreamer(string path, FileAccess access, int blockSize, FileShare share)
        {
            _blockSize = blockSize;
            _stream = new FileStream(path, FileMode.Open, access, share);
        }

        public byte[] ReadBlock(long blockIndex, long totalBytes)
        {
            long offset = blockIndex * _blockSize;
            int bytesToRead = (int)Math.Min(_blockSize, totalBytes - offset);

            byte[] buffer = new byte[bytesToRead];

            _stream.Seek(offset, SeekOrigin.Begin);

            int totalRead = 0;

            while (totalRead < bytesToRead)
            {
                int read = _stream.Read(buffer, totalRead, bytesToRead - totalRead);

                if (read == 0)
                    throw new EndOfStreamException(
                        $"Unexpected end of file at offset {offset}"
                    );

                totalRead += read;
            }

            return buffer;
        }

        public void WriteBlock(long blockIndex, byte[] dataBuffer)
        {
            long offset = blockIndex * _blockSize;

            _stream.Seek(offset, SeekOrigin.Begin);
            _stream.Write(dataBuffer, 0, dataBuffer.Length);
            _stream.Flush();
        }

        public byte[] ReadBack(long blockIndex, int size)
        {
            long offset = blockIndex * _blockSize;
            byte[] buffer = new byte[size];

            _stream.Seek(offset, SeekOrigin.Begin);

            int totalRead = 0;
            while (totalRead < size)
            {
                int read = _stream.Read(buffer, totalRead, size - totalRead);

                if (read == 0)
                    throw new EndOfStreamException(
                        $"Unexpected end of file while reading back at offset {offset}"
                    );

                totalRead += read;
            }

            return buffer;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }

}
