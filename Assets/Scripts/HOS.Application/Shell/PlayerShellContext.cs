using System;
using System.Collections.Generic;
using HOS.Domain.Common;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Domain.Machines;

namespace HOS.Application.Shell
{
    public sealed class PlayerShellContext
    {
        private readonly Dictionary<string, string> environment =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public PlayerShellContext(
            GameWorld world,
            MachineId localMachineId,
            UserId userId,
            NodeId? initialWorkingDirectory = null)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            if (!world.TryGetMachine(localMachineId, out var machine))
                throw new ArgumentException("Local machine is not in the world.", nameof(localMachineId));
            if (!machine.Users.TryGetUser(userId, out _))
                throw new ArgumentException("User is not registered on the local machine.", nameof(userId));

            LocalMachineId = localMachineId;
            CurrentMachineId = localMachineId;
            CurrentUserId = userId;
            WorkingDirectory = initialWorkingDirectory ?? machine.FileSystem.RootId;
            environment["PATH"] = "/bin:/usr/bin";
            environment["HOME"] = "/";
        }

        public GameWorld World { get; }
        public MachineId LocalMachineId { get; }
        public MachineId CurrentMachineId { get; private set; }
        public UserId CurrentUserId { get; private set; }
        public NodeId WorkingDirectory { get; private set; }
        public bool IsRemote => CurrentMachineId != LocalMachineId;

        public Machine CurrentMachine
        {
            get
            {
                if (World.TryGetMachine(CurrentMachineId, out var machine))
                    return machine;

                throw new InvalidOperationException("Current machine is missing from the world.");
            }
        }

        public AccessContext CreateAccessContext(bool isKernel = false)
        {
            return CurrentMachine.Users.CreateAccessContext(CurrentUserId, isKernel);
        }

        public string GetEnvironment(string name)
        {
            return name != null && environment.TryGetValue(name, out var value)
                ? value
                : null;
        }

        public void SetEnvironment(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Environment variable name cannot be empty.", nameof(name));

            if (value == null)
                environment.Remove(name);
            else
                environment[name] = value;
        }

        public Result<Unit, FileSystemError> ChangeDirectory(VirtualPath path)
        {
            var machine = CurrentMachine;
            var access = CreateAccessContext();
            var resolved = machine.FileSystem.Resolve(WorkingDirectory, path, access);
            if (resolved.IsFailure)
                return Result<Unit, FileSystemError>.Failure(resolved.Error);

            var entry = machine.FileSystem.Stat(resolved.Value);
            if (entry.IsFailure)
                return Result<Unit, FileSystemError>.Failure(entry.Error);
            if (entry.Value.Type != FileNodeType.Directory)
                return Result<Unit, FileSystemError>.Failure(FileSystemError.NotADirectory);

            var permission = machine.FileSystem.CheckAccess(
                resolved.Value,
                PermissionBits.Execute,
                access);
            if (permission.IsFailure)
                return permission;

            WorkingDirectory = resolved.Value;
            return Result<Unit, FileSystemError>.Success(Unit.Value);
        }
    }
}
