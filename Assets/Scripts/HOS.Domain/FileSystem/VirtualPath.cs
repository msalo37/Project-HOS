using System;
using System.Collections.Generic;
using HOS.Domain.Common;

namespace HOS.Domain.FileSystem
{
    public enum PathError
    {
        Empty,
        ContainsNullCharacter
    }

    public readonly struct VirtualPath
    {
        private readonly string[] segments;

        private VirtualPath(bool isAbsolute, string[] segments)
        {
            IsAbsolute = isAbsolute;
            this.segments = segments;
        }

        public bool IsAbsolute { get; }
        public IReadOnlyList<string> Segments => segments ?? Array.Empty<string>();

        public static Result<VirtualPath, PathError> Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Result<VirtualPath, PathError>.Failure(PathError.Empty);
            if (value.IndexOf('\0') >= 0)
                return Result<VirtualPath, PathError>.Failure(PathError.ContainsNullCharacter);

            var isAbsolute = value[0] == '/';
            var parts = value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return Result<VirtualPath, PathError>.Success(new VirtualPath(isAbsolute, parts));
        }

        public override string ToString()
        {
            var prefix = IsAbsolute ? "/" : string.Empty;
            return prefix + string.Join("/", Segments);
        }
    }
}
