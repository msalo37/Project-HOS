using System;
using System.Collections.Generic;
using HOS.Domain.Common;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Domain.Machines;
using HOS.Domain.Network;

namespace HOS.Application.Shell
{
    public sealed class PlayerShellContext
    {
        private readonly Dictionary<string, string> environment =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> localEnvironment =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Stack<ShellLocation> locations = new Stack<ShellLocation>();

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
            LocalUserId = userId;
            LocalWorkingDirectory = initialWorkingDirectory ?? machine.FileSystem.RootId;
            CurrentMachineId = localMachineId;
            CurrentUserId = userId;
            WorkingDirectory = initialWorkingDirectory ?? machine.FileSystem.RootId;
            environment["PATH"] = "/bin:/usr/bin";
            environment["HOME"] = "/";
            localEnvironment["PATH"] = "/bin:/usr/bin";
            localEnvironment["HOME"] = "/";
        }

        public GameWorld World { get; }
        public MachineId LocalMachineId { get; }
        public UserId LocalUserId { get; }
        public Machine LocalMachine => World.TryGetMachine(LocalMachineId, out var machine) ? machine : throw new InvalidOperationException();
        public NodeId LocalWorkingDirectory { get; private set; }
        public MachineId CurrentMachineId { get; private set; }
        public UserId CurrentUserId { get; private set; }
        public NodeId WorkingDirectory { get; private set; }
        public bool IsRemote => CurrentMachineId != LocalMachineId;
        public ConnectionId? CurrentConnectionId { get; private set; }

        public void EnterRemote(AccessGrant grant)
        {
            if (grant == null || !World.TryGetMachine(grant.TargetMachineId, out var machine) ||
                !machine.Users.TryGetUser(grant.UserId, out _))
                throw new InvalidOperationException("Invalid remote access grant.");
            locations.Push(new ShellLocation(
                CurrentMachineId, CurrentUserId, WorkingDirectory,
                new Dictionary<string, string>(environment), CurrentConnectionId));
            CurrentMachineId = grant.TargetMachineId;
            CurrentUserId = grant.UserId;
            WorkingDirectory = grant.HomeDirectoryId;
            CurrentConnectionId = grant.ConnectionId;
            environment.Clear();
            environment["HOME"] = machine.FileSystem.GetPath(grant.HomeDirectoryId).Value;
            environment["PATH"] = "/bin:/usr/bin";
        }

        public bool ExitRemote()
        {
            if (locations.Count == 0) return false;
            if (CurrentConnectionId.HasValue)
                World.Network.Disconnect(CurrentConnectionId.Value);
            var location = locations.Pop();
            CurrentMachineId = location.MachineId;
            CurrentUserId = location.UserId;
            WorkingDirectory = location.WorkingDirectory;
            CurrentConnectionId = location.ConnectionId;
            environment.Clear();
            foreach (var pair in location.Environment) environment[pair.Key] = pair.Value;
            return true;
        }

        private sealed class ShellLocation
        {
            public ShellLocation(MachineId machineId, UserId userId, NodeId workingDirectory, Dictionary<string, string> environment, ConnectionId? connectionId)
            { MachineId = machineId; UserId = userId; WorkingDirectory = workingDirectory; Environment = environment; ConnectionId = connectionId; }
            public MachineId MachineId { get; }
            public UserId UserId { get; }
            public NodeId WorkingDirectory { get; }
            public Dictionary<string, string> Environment { get; }
            public ConnectionId? ConnectionId { get; }
        }

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

        public string GetLocalEnvironment(string name)
        {
            return name != null && localEnvironment.TryGetValue(name, out var value)
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
            if (!IsRemote)
            {
                if (value == null)
                    localEnvironment.Remove(name);
                else
                    localEnvironment[name] = value;
            }
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
            if (!IsRemote)
                LocalWorkingDirectory = WorkingDirectory;
            return Result<Unit, FileSystemError>.Success(Unit.Value);
        }
    }
}
