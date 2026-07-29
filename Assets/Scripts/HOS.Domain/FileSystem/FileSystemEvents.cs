using HOS.Domain.Common;
using HOS.Domain.Identity;

namespace HOS.Domain.FileSystem
{
    public enum FileSystemAction
    {
        Created,
        Read,
        Written,
        Moved,
        Deleted,
        PermissionsChanged,
        OwnerChanged
    }

    public sealed class FileSystemEvent : IDomainEvent
    {
        public FileSystemEvent(
            FileSystemAction action,
            NodeId nodeId,
            UserId userId,
            GameTime occurredAt)
        {
            Action = action;
            NodeId = nodeId;
            UserId = userId;
            OccurredAt = occurredAt;
        }

        public FileSystemAction Action { get; }
        public NodeId NodeId { get; }
        public UserId UserId { get; }
        public GameTime OccurredAt { get; }
    }
}
