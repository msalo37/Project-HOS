using System;
using System.Collections.Generic;
using HOS.Application.Shell;
using HOS.Domain.Common;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Domain.Machines;

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
        public ResolvedExecutable(MachineId machineId, UserId userId, NodeId nodeId, string path)
        {
            MachineId = machineId;
            UserId = userId;
            NodeId = nodeId;
            Path = path;
        }

        public MachineId MachineId { get; }
        public UserId UserId { get; }
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

            var explicitPath = commandName.IndexOf('/') >= 0;
            var candidates = BuildCandidates(
                commandName,
                commandName.IndexOf('/') >= 0
                    ? shell.GetEnvironment("PATH")
                    : shell.GetLocalEnvironment("PATH"));
            var machine = explicitPath ? shell.CurrentMachine : shell.LocalMachine;
            var userId = explicitPath ? shell.CurrentUserId : shell.LocalUserId;
            var workingDirectory = explicitPath ? shell.WorkingDirectory : shell.LocalWorkingDirectory;
            var access = machine.Users.CreateAccessContext(userId);
            var accessDenied = false;
            var notExecutable = false;

            foreach (var candidate in candidates)
            {
                var parsed = VirtualPath.Parse(candidate);
                if (parsed.IsFailure)
                    continue;

                var resolved = machine.FileSystem.Resolve(
                    workingDirectory,
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
                    new ResolvedExecutable(machine.Id, userId, resolved.Value, candidate));
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
            if (commandName.IndexOf('/') >= 0)
            {
                var explicitCandidates = new List<string>();
                AddCandidates(explicitCandidates, commandName);
                return explicitCandidates;
            }

            var path = string.IsNullOrWhiteSpace(pathEnvironment)
                ? "/bin"
                : pathEnvironment;
            var directories = path.Split(
                new[] { ':' },
                StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>(directories.Length * 3);

            foreach (var directory in directories)
                AddCandidates(result, directory.TrimEnd('/') + "/" + commandName);

            return result;
        }

        private static void AddCandidates(ICollection<string> candidates, string path)
        {
            candidates.Add(path);
            if (HasExtension(path))
                return;

            candidates.Add(path + HosBinaryFormat.Extension);
            candidates.Add(path + ".lua");
        }

        private static bool HasExtension(string path)
        {
            var lastSeparator = path.LastIndexOf('/');
            var lastDot = path.LastIndexOf('.');
            return lastDot > lastSeparator;
        }
    }
}
