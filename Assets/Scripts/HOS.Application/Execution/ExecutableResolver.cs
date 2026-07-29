using System;
using System.Collections.Generic;
using HOS.Application.Shell;
using HOS.Domain.Common;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;

namespace HOS.Application.Execution
{
    public enum ExecutableResolutionError
    {
        NotFound,
        AccessDenied,
        NotExecutable,
        InvalidPath
    }

    public readonly struct ResolvedExecutable
    {
        public ResolvedExecutable(NodeId nodeId, string path)
        {
            NodeId = nodeId;
            Path = path;
        }

        public NodeId NodeId { get; }
        public string Path { get; }
    }

    public sealed class ExecutableResolver
    {
        public Result<ResolvedExecutable, ExecutableResolutionError> Resolve(
            string commandName,
            PlayerShellContext shell)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                return Result<ResolvedExecutable, ExecutableResolutionError>.Failure(
                    ExecutableResolutionError.InvalidPath);
            }

            var candidates = BuildCandidates(commandName, shell.GetEnvironment("PATH"));
            var accessDenied = false;
            var notExecutable = false;

            foreach (var candidate in candidates)
            {
                var parsed = VirtualPath.Parse(candidate);
                if (parsed.IsFailure)
                    continue;

                var machine = shell.CurrentMachine;
                var access = shell.CreateAccessContext();
                var resolved = machine.FileSystem.Resolve(
                    shell.WorkingDirectory,
                    parsed.Value,
                    access);

                if (resolved.IsFailure)
                {
                    if (resolved.Error == FileSystemError.AccessDenied)
                        accessDenied = true;
                    continue;
                }

                var entry = machine.FileSystem.Stat(resolved.Value);
                if (entry.IsFailure || entry.Value.Type != FileNodeType.RegularFile)
                {
                    notExecutable = true;
                    continue;
                }

                var execute = machine.FileSystem.CheckAccess(
                    resolved.Value,
                    PermissionBits.Execute,
                    access);
                if (execute.IsFailure)
                {
                    accessDenied = true;
                    continue;
                }

                return Result<ResolvedExecutable, ExecutableResolutionError>.Success(
                    new ResolvedExecutable(resolved.Value, candidate));
            }

            if (accessDenied)
            {
                return Result<ResolvedExecutable, ExecutableResolutionError>.Failure(
                    ExecutableResolutionError.AccessDenied);
            }

            if (notExecutable)
            {
                return Result<ResolvedExecutable, ExecutableResolutionError>.Failure(
                    ExecutableResolutionError.NotExecutable);
            }

            return Result<ResolvedExecutable, ExecutableResolutionError>.Failure(
                ExecutableResolutionError.NotFound);
        }

        private static IReadOnlyList<string> BuildCandidates(
            string commandName,
            string pathEnvironment)
        {
            var suffix = commandName.EndsWith(".lua", StringComparison.Ordinal)
                ? string.Empty
                : ".lua";

            if (commandName.IndexOf('/') >= 0)
                return new[] { commandName + suffix };

            var path = string.IsNullOrWhiteSpace(pathEnvironment)
                ? "/bin"
                : pathEnvironment;
            var directories = path.Split(
                new[] { ':' },
                StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>(directories.Length);

            foreach (var directory in directories)
                result.Add(directory.TrimEnd('/') + "/" + commandName + suffix);

            return result;
        }
    }
}
