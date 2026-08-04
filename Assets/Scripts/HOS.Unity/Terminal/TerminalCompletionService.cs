using System;
using System.Collections.Generic;
using HOS.Application.Shell;
using HOS.Domain.FileSystem;

namespace HOS.Unity.Terminal
{
    public sealed class TerminalCompletionResult
    {
        public TerminalCompletionResult(int tokenStart, int tokenLength, string replacement, IReadOnlyList<string> candidates)
        {
            TokenStart = tokenStart;
            TokenLength = tokenLength;
            Replacement = replacement ?? string.Empty;
            Candidates = candidates ?? Array.Empty<string>();
        }

        public int TokenStart { get; }
        public int TokenLength { get; }
        public string Replacement { get; }
        public IReadOnlyList<string> Candidates { get; }
    }

    public sealed class TerminalCompletionService
    {
        private readonly PlayerShellContext shell;

        public TerminalCompletionService(PlayerShellContext shell)
        {
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        }

        public TerminalCompletionResult Complete(string input, int caretIndex)
        {
            input = input ?? string.Empty;
            caretIndex = Math.Max(0, Math.Min(caretIndex, input.Length));
            var start = caretIndex;
            while (start > 0 && !char.IsWhiteSpace(input[start - 1]))
                start--;

            var token = input.Substring(start, caretIndex - start);
            var firstToken = string.IsNullOrWhiteSpace(input.Substring(0, start));
            var candidates = firstToken && token.IndexOf('/') < 0
                ? CompleteCommand(token)
                : CompletePath(token);
            var replacement = candidates.Count == 0 ? token : LongestCommonPrefix(candidates);
            if (candidates.Count == 1 && IsDirectoryCandidate(candidates[0]))
                replacement += "/";
            else if (candidates.Count == 1)
                replacement += " ";

            return new TerminalCompletionResult(start, caretIndex - start, replacement, candidates);
        }

        private List<string> CompleteCommand(string prefix)
        {
            var result = new SortedSet<string>(StringComparer.Ordinal);
            var path = shell.GetLocalEnvironment("PATH") ?? "/bin";
            var machine = shell.LocalMachine;
            var access = machine.Users.CreateAccessContext(shell.LocalUserId);

            foreach (var directory in path.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parsed = VirtualPath.Parse(directory);
                if (parsed.IsFailure)
                    continue;
                var resolved = machine.FileSystem.Resolve(shell.LocalWorkingDirectory, parsed.Value, access);
                if (resolved.IsFailure)
                    continue;
                var listed = machine.FileSystem.List(resolved.Value, access);
                if (listed.IsFailure)
                    continue;

                foreach (var entry in listed.Value)
                {
                    if (entry.Type != FileNodeType.RegularFile)
                        continue;
                    var executable = machine.FileSystem.CheckAccess(
                        entry.Id,
                        HOS.Domain.Identity.PermissionBits.Execute,
                        access);
                    if (executable.IsFailure)
                        continue;
                    var name = entry.Name.EndsWith(".lua", StringComparison.Ordinal)
                        ? entry.Name.Substring(0, entry.Name.Length - 4)
                        : entry.Name;
                    if (name.StartsWith(prefix, StringComparison.Ordinal))
                        result.Add(name);
                }
            }
            return new List<string>(result);
        }

        private List<string> CompletePath(string token)
        {
            var slash = token.LastIndexOf('/');
            var directoryText = slash >= 0 ? token.Substring(0, slash + 1) : string.Empty;
            var namePrefix = slash >= 0 ? token.Substring(slash + 1) : token;
            var lookupPath = directoryText.Length == 0 ? "." : directoryText;
            var parsed = VirtualPath.Parse(lookupPath);
            var result = new List<string>();
            if (parsed.IsFailure)
                return result;

            var machine = shell.CurrentMachine;
            var access = shell.CreateAccessContext();
            var resolved = machine.FileSystem.Resolve(shell.WorkingDirectory, parsed.Value, access);
            if (resolved.IsFailure)
                return result;
            var listed = machine.FileSystem.List(resolved.Value, access);
            if (listed.IsFailure)
                return result;

            foreach (var entry in listed.Value)
            {
                if (!entry.Name.StartsWith(namePrefix, StringComparison.Ordinal))
                    continue;
                result.Add(directoryText + entry.Name);
            }
            return result;
        }

        private bool IsDirectoryCandidate(string candidate)
        {
            var parsed = VirtualPath.Parse(candidate);
            if (parsed.IsFailure)
                return false;
            var resolved = shell.CurrentMachine.FileSystem.Resolve(
                shell.WorkingDirectory, parsed.Value, shell.CreateAccessContext());
            if (resolved.IsFailure)
                return false;
            var stat = shell.CurrentMachine.FileSystem.Stat(resolved.Value);
            return stat.IsSuccess && stat.Value.Type == FileNodeType.Directory;
        }

        private static string LongestCommonPrefix(IReadOnlyList<string> values)
        {
            if (values.Count == 0)
                return string.Empty;
            var prefix = values[0];
            for (var i = 1; i < values.Count; i++)
            {
                var length = Math.Min(prefix.Length, values[i].Length);
                var index = 0;
                while (index < length && prefix[index] == values[i][index])
                    index++;
                prefix = prefix.Substring(0, index);
            }
            return prefix;
        }
    }
}
