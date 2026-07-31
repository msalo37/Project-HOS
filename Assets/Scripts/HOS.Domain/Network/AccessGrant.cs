using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Domain.Machines;

namespace HOS.Domain.Network
{
    public enum AccessMethod { Password, Passwordless, Exploit }

    public sealed class AccessGrant
    {
        public AccessGrant(ConnectionId connectionId, MachineId targetMachineId, UserId userId, NodeId homeDirectoryId, AccessMethod method)
        {
            ConnectionId = connectionId;
            TargetMachineId = targetMachineId;
            UserId = userId;
            HomeDirectoryId = homeDirectoryId;
            Method = method;
        }

        public ConnectionId ConnectionId { get; }
        public MachineId TargetMachineId { get; }
        public UserId UserId { get; }
        public NodeId HomeDirectoryId { get; }
        public AccessMethod Method { get; }
    }
}
