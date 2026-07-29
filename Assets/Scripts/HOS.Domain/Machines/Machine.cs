using System;
using HOS.Domain.Common;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Domain.Network;
using HOS.Domain.Processes;
using HOS.Domain.Services;

namespace HOS.Domain.Machines
{
    public readonly struct MachineId : IEquatable<MachineId>
    {
        public MachineId(Guid value) => Value = value;
        public Guid Value { get; }
        public static MachineId New() => new MachineId(Guid.NewGuid());
        public bool Equals(MachineId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is MachineId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(MachineId left, MachineId right) => left.Equals(right);
        public static bool operator !=(MachineId left, MachineId right) => !left.Equals(right);
    }

    public sealed class Machine
    {
        private Machine(
            MachineId id,
            string hostname,
            VirtualIpAddress address,
            UserRegistry users,
            UserId rootUserId,
            GroupId rootGroupId,
            VirtualFileSystem fileSystem)
        {
            Id = id;
            Hostname = hostname;
            Address = address;
            Users = users;
            RootUserId = rootUserId;
            RootGroupId = rootGroupId;
            FileSystem = fileSystem;
            Processes = new ProcessTable();
            Services = new ServiceManager();
        }

        public MachineId Id { get; }
        public string Hostname { get; }
        public VirtualIpAddress Address { get; }
        public UserRegistry Users { get; }
        public UserId RootUserId { get; }
        public GroupId RootGroupId { get; }
        public VirtualFileSystem FileSystem { get; }
        public ProcessTable Processes { get; }
        public ServiceManager Services { get; }

        public Result<ProcessId, ProcessError> StartProcess(
            NodeId executableId,
            string name,
            AccessContext access,
            ProcessId? parentId = null)
        {
            var entry = FileSystem.Stat(executableId);
            if (entry.IsFailure || entry.Value.Type != FileNodeType.RegularFile)
            {
                return Result<ProcessId, ProcessError>.Failure(
                    ProcessError.InvalidExecutable);
            }

            var executeAccess = FileSystem.CheckAccess(
                executableId,
                PermissionBits.Execute,
                access);
            if (executeAccess.IsFailure)
                return Result<ProcessId, ProcessError>.Failure(ProcessError.AccessDenied);

            return Processes.Start(
                executableId,
                name,
                access.UserId,
                parentId,
                FileSystem.CurrentTime);
        }

        public static Machine Create(
            MachineId id,
            string hostname,
            VirtualIpAddress address)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                throw new ArgumentException("Hostname cannot be empty.", nameof(hostname));

            var users = new UserRegistry();
            var rootGroup = users.CreateGroup("root").Value;
            var rootUser = users.CreateUser("root", rootGroup).Value;
            var ownership = new FileOwnership(rootUser, rootGroup);
            var fileSystem = new VirtualFileSystem(ownership);

            return new Machine(
                id,
                hostname,
                address,
                users,
                rootUser,
                rootGroup,
                fileSystem);
        }
    }
}
