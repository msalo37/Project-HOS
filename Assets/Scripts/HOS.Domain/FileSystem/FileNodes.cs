using System;
using HOS.Domain.Common;
using HOS.Domain.Identity;

namespace HOS.Domain.FileSystem
{
    public enum FileNodeType
    {
        Directory,
        RegularFile,
        SymbolicLink
    }

    public readonly struct FileTimestamps
    {
        public FileTimestamps(GameTime created, GameTime modified, GameTime accessed)
        {
            Created = created;
            Modified = modified;
            Accessed = accessed;
        }

        public GameTime Created { get; }
        public GameTime Modified { get; }
        public GameTime Accessed { get; }

        public FileTimestamps WithModified(GameTime time) =>
            new FileTimestamps(Created, time, Accessed);

        public FileTimestamps WithAccessed(GameTime time) =>
            new FileTimestamps(Created, Modified, time);
    }

    public abstract class FileNode
    {
        protected FileNode(
            NodeId id,
            NodeId? parentId,
            string name,
            FileOwnership ownership,
            FilePermissions permissions,
            GameTime createdAt)
        {
            Id = id;
            ParentId = parentId;
            Name = name;
            Ownership = ownership;
            Permissions = permissions;
            Timestamps = new FileTimestamps(createdAt, createdAt, createdAt);
        }

        public NodeId Id { get; }
        public NodeId? ParentId { get; internal set; }
        public string Name { get; internal set; }
        public FileOwnership Ownership { get; internal set; }
        public FilePermissions Permissions { get; internal set; }
        public FileTimestamps Timestamps { get; internal set; }
        public abstract FileNodeType Type { get; }
    }

    public sealed class DirectoryNode : FileNode
    {
        internal DirectoryNode(
            NodeId id,
            NodeId? parentId,
            string name,
            FileOwnership ownership,
            FilePermissions permissions,
            GameTime createdAt)
            : base(id, parentId, name, ownership, permissions, createdAt)
        {
        }

        public override FileNodeType Type => FileNodeType.Directory;
    }

    public sealed class RegularFileNode : FileNode
    {
        internal RegularFileNode(
            NodeId id,
            NodeId parentId,
            string name,
            FileOwnership ownership,
            FilePermissions permissions,
            FileContent content,
            GameTime createdAt)
            : base(id, parentId, name, ownership, permissions, createdAt)
        {
            Content = content ?? FileContent.Empty;
        }

        public FileContent Content { get; internal set; }
        public override FileNodeType Type => FileNodeType.RegularFile;
    }

    public sealed class SymbolicLinkNode : FileNode
    {
        internal SymbolicLinkNode(
            NodeId id,
            NodeId parentId,
            string name,
            FileOwnership ownership,
            FilePermissions permissions,
            VirtualPath target,
            GameTime createdAt)
            : base(id, parentId, name, ownership, permissions, createdAt)
        {
            Target = target;
        }

        public VirtualPath Target { get; internal set; }
        public override FileNodeType Type => FileNodeType.SymbolicLink;
    }

    public readonly struct FileEntry
    {
        public FileEntry(
            NodeId id,
            string name,
            FileNodeType type,
            FileOwnership ownership,
            FilePermissions permissions,
            FileTimestamps timestamps,
            int contentLength)
        {
            Id = id;
            Name = name;
            Type = type;
            Ownership = ownership;
            Permissions = permissions;
            Timestamps = timestamps;
            ContentLength = contentLength;
        }

        public NodeId Id { get; }
        public string Name { get; }
        public FileNodeType Type { get; }
        public FileOwnership Ownership { get; }
        public FilePermissions Permissions { get; }
        public FileTimestamps Timestamps { get; }
        public int ContentLength { get; }
    }
}
