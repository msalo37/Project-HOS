using System;
using HOS.Domain.FileSystem;

namespace HOS.Application.Execution
{
    public sealed class ProgramImage
    {
        public ProgramImage(string path, FileContent content)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Program path cannot be empty.", nameof(path));

            Path = path;
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public string Path { get; }
        public FileContent Content { get; }
        public string ReadUtf8() => Content.ReadUtf8();
    }
}
