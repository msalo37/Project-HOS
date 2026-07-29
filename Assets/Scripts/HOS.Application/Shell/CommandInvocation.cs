using System;
using System.Collections.Generic;

namespace HOS.Application.Shell
{
    public sealed class CommandInvocation
    {
        public CommandInvocation(string rawInput, string name, IReadOnlyList<string> arguments)
        {
            RawInput = rawInput ?? string.Empty;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Arguments = arguments ?? Array.Empty<string>();
        }

        public string RawInput { get; }
        public string Name { get; }
        public IReadOnlyList<string> Arguments { get; }
    }
}
