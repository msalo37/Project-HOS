using System;
using System.Text;

namespace HOS.Domain.FileSystem
{
    public sealed class FileContent
    {
        private readonly byte[] bytes;

        public FileContent(byte[] bytes)
        {
            this.bytes = bytes == null ? Array.Empty<byte>() : (byte[])bytes.Clone();
        }

        public int Length => bytes.Length;
        public byte[] CopyBytes() => (byte[])bytes.Clone();
        public string ReadUtf8() => Encoding.UTF8.GetString(bytes);

        public static FileContent Empty => new FileContent(Array.Empty<byte>());
        public static FileContent FromUtf8(string text)
        {
            return new FileContent(Encoding.UTF8.GetBytes(text ?? string.Empty));
        }
    }
}
