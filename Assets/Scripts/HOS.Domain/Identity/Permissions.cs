using System;
using System.Collections.Generic;

namespace HOS.Domain.Identity
{
    [Flags]
    public enum PermissionBits : byte
    {
        None = 0,
        Execute = 1,
        Write = 2,
        Read = 4
    }

    public readonly struct FilePermissions : IEquatable<FilePermissions>
    {
        public FilePermissions(
            PermissionBits owner,
            PermissionBits group,
            PermissionBits other)
        {
            Owner = owner;
            Group = group;
            Other = other;
        }

        public PermissionBits Owner { get; }
        public PermissionBits Group { get; }
        public PermissionBits Other { get; }

        public static FilePermissions DefaultFile =>
            new FilePermissions(
                PermissionBits.Read | PermissionBits.Write,
                PermissionBits.Read,
                PermissionBits.Read);

        public static FilePermissions DefaultDirectory =>
            new FilePermissions(
                PermissionBits.Read | PermissionBits.Write | PermissionBits.Execute,
                PermissionBits.Read | PermissionBits.Execute,
                PermissionBits.Read | PermissionBits.Execute);

        public bool Equals(FilePermissions other)
        {
            return Owner == other.Owner && Group == other.Group && Other == other.Other;
        }

        public override bool Equals(object obj) => obj is FilePermissions other && Equals(other);
        public override int GetHashCode() => ((int)Owner << 8) ^ ((int)Group << 4) ^ (int)Other;
    }

    public readonly struct FileOwnership : IEquatable<FileOwnership>
    {
        public FileOwnership(UserId owner, GroupId group)
        {
            Owner = owner;
            Group = group;
        }

        public UserId Owner { get; }
        public GroupId Group { get; }

        public bool Equals(FileOwnership other) => Owner == other.Owner && Group == other.Group;
        public override bool Equals(object obj) => obj is FileOwnership other && Equals(other);
        public override int GetHashCode() => (Owner.GetHashCode() * 397) ^ Group.GetHashCode();
    }

    public sealed class AccessContext
    {
        private readonly HashSet<GroupId> groups;

        public AccessContext(UserId userId, IEnumerable<GroupId> groups, bool isKernel = false)
        {
            UserId = userId;
            this.groups = groups == null
                ? new HashSet<GroupId>()
                : new HashSet<GroupId>(groups);
            IsKernel = isKernel;
        }

        public UserId UserId { get; }
        public bool IsKernel { get; }
        public bool IsMemberOf(GroupId groupId) => groups.Contains(groupId);

        public static AccessContext Kernel(UserId userId)
        {
            return new AccessContext(userId, Array.Empty<GroupId>(), true);
        }
    }

    public static class PermissionPolicy
    {
        public static bool Allows(
            FileOwnership ownership,
            FilePermissions permissions,
            AccessContext access,
            PermissionBits required)
        {
            if (access == null)
                throw new ArgumentNullException(nameof(access));

            if (access.IsKernel)
                return true;

            PermissionBits actual;
            if (ownership.Owner == access.UserId)
                actual = permissions.Owner;
            else if (access.IsMemberOf(ownership.Group))
                actual = permissions.Group;
            else
                actual = permissions.Other;

            return (actual & required) == required;
        }
    }
}
