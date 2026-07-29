using System;
using System.Collections.Generic;
using HOS.Domain.Common;
using HOS.Domain.Identity;

namespace HOS.Domain.FileSystem
{
    public sealed class VirtualFileSystem
    {
        private const int MaxSymbolicLinkDepth = 32;

        private readonly Dictionary<NodeId, FileNode> nodes =
            new Dictionary<NodeId, FileNode>();

        private readonly Dictionary<NodeId, Dictionary<string, NodeId>> children =
            new Dictionary<NodeId, Dictionary<string, NodeId>>();

        private readonly DomainEventBuffer events = new DomainEventBuffer();

        public VirtualFileSystem(
            FileOwnership rootOwnership,
            FilePermissions? rootPermissions = null)
        {
            RootId = NodeId.New();
            var root = new DirectoryNode(
                RootId,
                null,
                "/",
                rootOwnership,
                rootPermissions ?? FilePermissions.DefaultDirectory,
                GameTime.Zero);

            nodes.Add(RootId, root);
            children.Add(RootId, CreateChildIndex());
        }

        public NodeId RootId { get; }
        public GameTime CurrentTime { get; private set; }
        public int NodeCount => nodes.Count;

        public void AdvanceTime(GameDuration duration)
        {
            CurrentTime = CurrentTime.Advance(duration);
        }

        public IReadOnlyList<IDomainEvent> DrainEvents() => events.Drain();

        public Result<NodeId, FileSystemError> Resolve(
            NodeId workingDirectory,
            VirtualPath path,
            AccessContext access)
        {
            if (!nodes.TryGetValue(workingDirectory, out var workingNode) ||
                workingNode.Type != FileNodeType.Directory)
            {
                return Result<NodeId, FileSystemError>.Failure(
                    FileSystemError.InvalidWorkingDirectory);
            }

            var start = path.IsAbsolute ? RootId : workingDirectory;
            return ResolveFrom(start, path, access, 0);
        }

        public Result<NodeId, FileSystemError> CreateDirectory(
            NodeId parentId,
            string name,
            FileOwnership ownership,
            FilePermissions permissions,
            AccessContext access)
        {
            var validation = ValidateCreation(parentId, name, access);
            if (validation.IsFailure)
                return Result<NodeId, FileSystemError>.Failure(validation.Error);

            var id = NodeId.New();
            var node = new DirectoryNode(
                id,
                parentId,
                name,
                ownership,
                permissions,
                CurrentTime);

            nodes.Add(id, node);
            children.Add(id, CreateChildIndex());
            children[parentId].Add(name, id);
            TouchModified(parentId);
            AddEvent(FileSystemAction.Created, id, access);
            return Result<NodeId, FileSystemError>.Success(id);
        }

        public Result<NodeId, FileSystemError> CreateFile(
            NodeId parentId,
            string name,
            FileOwnership ownership,
            FilePermissions permissions,
            FileContent content,
            AccessContext access)
        {
            var validation = ValidateCreation(parentId, name, access);
            if (validation.IsFailure)
                return Result<NodeId, FileSystemError>.Failure(validation.Error);

            var id = NodeId.New();
            var node = new RegularFileNode(
                id,
                parentId,
                name,
                ownership,
                permissions,
                content,
                CurrentTime);

            nodes.Add(id, node);
            children[parentId].Add(name, id);
            TouchModified(parentId);
            AddEvent(FileSystemAction.Created, id, access);
            return Result<NodeId, FileSystemError>.Success(id);
        }

        public Result<NodeId, FileSystemError> CreateSymbolicLink(
            NodeId parentId,
            string name,
            VirtualPath target,
            FileOwnership ownership,
            FilePermissions permissions,
            AccessContext access)
        {
            var validation = ValidateCreation(parentId, name, access);
            if (validation.IsFailure)
                return Result<NodeId, FileSystemError>.Failure(validation.Error);

            var id = NodeId.New();
            var node = new SymbolicLinkNode(
                id,
                parentId,
                name,
                ownership,
                permissions,
                target,
                CurrentTime);

            nodes.Add(id, node);
            children[parentId].Add(name, id);
            TouchModified(parentId);
            AddEvent(FileSystemAction.Created, id, access);
            return Result<NodeId, FileSystemError>.Success(id);
        }

        public Result<IReadOnlyList<FileEntry>, FileSystemError> List(
            NodeId directoryId,
            AccessContext access)
        {
            if (!nodes.TryGetValue(directoryId, out var rawNode))
                return Result<IReadOnlyList<FileEntry>, FileSystemError>.Failure(
                    FileSystemError.NodeNotFound);
            if (!(rawNode is DirectoryNode directory))
                return Result<IReadOnlyList<FileEntry>, FileSystemError>.Failure(
                    FileSystemError.NotADirectory);
            if (!Allows(directory, access, PermissionBits.Read | PermissionBits.Execute))
                return Result<IReadOnlyList<FileEntry>, FileSystemError>.Failure(
                    FileSystemError.AccessDenied);

            var result = new List<FileEntry>(children[directoryId].Count);
            foreach (var childId in children[directoryId].Values)
                result.Add(ToEntry(nodes[childId]));

            result.Sort((left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
            directory.Timestamps = directory.Timestamps.WithAccessed(CurrentTime);
            return Result<IReadOnlyList<FileEntry>, FileSystemError>.Success(result);
        }

        public Result<FileEntry, FileSystemError> Stat(NodeId nodeId)
        {
            return nodes.TryGetValue(nodeId, out var node)
                ? Result<FileEntry, FileSystemError>.Success(ToEntry(node))
                : Result<FileEntry, FileSystemError>.Failure(FileSystemError.NodeNotFound);
        }

        public Result<string, FileSystemError> GetPath(NodeId nodeId)
        {
            if (!nodes.TryGetValue(nodeId, out var node))
                return Result<string, FileSystemError>.Failure(FileSystemError.NodeNotFound);
            if (nodeId == RootId)
                return Result<string, FileSystemError>.Success("/");

            var segments = new List<string>();
            var current = node;
            while (current.ParentId.HasValue)
            {
                segments.Add(current.Name);
                current = nodes[current.ParentId.Value];
            }

            segments.Reverse();
            return Result<string, FileSystemError>.Success("/" + string.Join("/", segments));
        }

        public Result<Unit, FileSystemError> CheckAccess(
            NodeId nodeId,
            PermissionBits required,
            AccessContext access)
        {
            if (!nodes.TryGetValue(nodeId, out var node))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.NodeNotFound);

            return Allows(node, access, required)
                ? Result<Unit, FileSystemError>.Success(Unit.Value)
                : Result<Unit, FileSystemError>.Failure(FileSystemError.AccessDenied);
        }

        public Result<FileContent, FileSystemError> ReadFile(
            NodeId nodeId,
            AccessContext access)
        {
            if (!nodes.TryGetValue(nodeId, out var rawNode))
                return Result<FileContent, FileSystemError>.Failure(FileSystemError.NodeNotFound);
            if (!(rawNode is RegularFileNode file))
                return Result<FileContent, FileSystemError>.Failure(FileSystemError.NotAFile);
            if (!Allows(file, access, PermissionBits.Read))
                return Result<FileContent, FileSystemError>.Failure(FileSystemError.AccessDenied);

            file.Timestamps = file.Timestamps.WithAccessed(CurrentTime);
            AddEvent(FileSystemAction.Read, nodeId, access);
            return Result<FileContent, FileSystemError>.Success(
                new FileContent(file.Content.CopyBytes()));
        }

        public Result<Unit, FileSystemError> WriteFile(
            NodeId nodeId,
            FileContent content,
            AccessContext access)
        {
            if (!nodes.TryGetValue(nodeId, out var rawNode))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.NodeNotFound);
            if (!(rawNode is RegularFileNode file))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.NotAFile);
            if (!Allows(file, access, PermissionBits.Write))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.AccessDenied);

            file.Content = content == null
                ? FileContent.Empty
                : new FileContent(content.CopyBytes());
            file.Timestamps = file.Timestamps.WithModified(CurrentTime);
            AddEvent(FileSystemAction.Written, nodeId, access);
            return Result<Unit, FileSystemError>.Success(Unit.Value);
        }

        public Result<Unit, FileSystemError> Move(
            NodeId nodeId,
            NodeId destinationDirectoryId,
            string newName,
            AccessContext access)
        {
            if (nodeId == RootId)
                return Result<Unit, FileSystemError>.Failure(FileSystemError.CannotMoveRoot);
            if (!nodes.TryGetValue(nodeId, out var node))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.NodeNotFound);
            if (!IsValidName(newName))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.InvalidName);
            if (!nodes.TryGetValue(destinationDirectoryId, out var destinationRaw))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.ParentNotFound);
            if (!(destinationRaw is DirectoryNode destination))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.NotADirectory);

            var sourceParentId = node.ParentId.Value;
            var sourceParent = (DirectoryNode)nodes[sourceParentId];

            if (!Allows(sourceParent, access, PermissionBits.Write | PermissionBits.Execute) ||
                !Allows(destination, access, PermissionBits.Write | PermissionBits.Execute))
            {
                return Result<Unit, FileSystemError>.Failure(FileSystemError.AccessDenied);
            }

            if (children[destinationDirectoryId].TryGetValue(newName, out var existing) &&
                existing != nodeId)
            {
                return Result<Unit, FileSystemError>.Failure(
                    FileSystemError.NameAlreadyExists);
            }

            if (node.Type == FileNodeType.Directory &&
                IsSameOrDescendant(destinationDirectoryId, nodeId))
            {
                return Result<Unit, FileSystemError>.Failure(
                    FileSystemError.CannotMoveDirectoryIntoItself);
            }

            if (sourceParentId == destinationDirectoryId && node.Name == newName)
                return Result<Unit, FileSystemError>.Success(Unit.Value);

            children[sourceParentId].Remove(node.Name);
            children[destinationDirectoryId][newName] = nodeId;
            node.ParentId = destinationDirectoryId;
            node.Name = newName;
            node.Timestamps = node.Timestamps.WithModified(CurrentTime);
            TouchModified(sourceParentId);
            TouchModified(destinationDirectoryId);
            AddEvent(FileSystemAction.Moved, nodeId, access);
            return Result<Unit, FileSystemError>.Success(Unit.Value);
        }

        public Result<Unit, FileSystemError> Delete(
            NodeId nodeId,
            DeleteOptions options,
            AccessContext access)
        {
            if (nodeId == RootId)
                return Result<Unit, FileSystemError>.Failure(FileSystemError.CannotDeleteRoot);
            if (!nodes.TryGetValue(nodeId, out var node))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.NodeNotFound);

            if (node.Type == FileNodeType.Directory &&
                children[nodeId].Count > 0 &&
                !options.Recursive)
            {
                return Result<Unit, FileSystemError>.Failure(
                    FileSystemError.DirectoryNotEmpty);
            }

            var deletionOrder = new List<NodeId>();
            var validation = CollectForDeletion(nodeId, access, deletionOrder);
            if (validation.IsFailure)
                return validation;

            var originalParent = node.ParentId.Value;
            foreach (var id in deletionOrder)
            {
                var item = nodes[id];
                if (item.ParentId.HasValue && children.TryGetValue(item.ParentId.Value, out var siblings))
                    siblings.Remove(item.Name);

                children.Remove(id);
                nodes.Remove(id);
                AddEvent(FileSystemAction.Deleted, id, access);
            }

            TouchModified(originalParent);
            return Result<Unit, FileSystemError>.Success(Unit.Value);
        }

        public Result<Unit, FileSystemError> ChangePermissions(
            NodeId nodeId,
            FilePermissions permissions,
            AccessContext access)
        {
            if (!nodes.TryGetValue(nodeId, out var node))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.NodeNotFound);
            if (!access.IsKernel && node.Ownership.Owner != access.UserId)
                return Result<Unit, FileSystemError>.Failure(FileSystemError.AccessDenied);

            node.Permissions = permissions;
            node.Timestamps = node.Timestamps.WithModified(CurrentTime);
            AddEvent(FileSystemAction.PermissionsChanged, nodeId, access);
            return Result<Unit, FileSystemError>.Success(Unit.Value);
        }

        public Result<Unit, FileSystemError> ChangeOwner(
            NodeId nodeId,
            FileOwnership ownership,
            AccessContext access)
        {
            if (!nodes.TryGetValue(nodeId, out var node))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.NodeNotFound);
            if (!access.IsKernel)
                return Result<Unit, FileSystemError>.Failure(FileSystemError.AccessDenied);

            node.Ownership = ownership;
            node.Timestamps = node.Timestamps.WithModified(CurrentTime);
            AddEvent(FileSystemAction.OwnerChanged, nodeId, access);
            return Result<Unit, FileSystemError>.Success(Unit.Value);
        }

        private Result<NodeId, FileSystemError> ResolveFrom(
            NodeId start,
            VirtualPath path,
            AccessContext access,
            int symbolicLinkDepth)
        {
            if (symbolicLinkDepth > MaxSymbolicLinkDepth)
                return Result<NodeId, FileSystemError>.Failure(
                    FileSystemError.SymbolicLinkLoop);

            var current = path.IsAbsolute ? RootId : start;
            var segments = path.Segments;

            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment == ".")
                    continue;

                if (segment == "..")
                {
                    if (!(nodes[current] is DirectoryNode parentSource))
                        return Result<NodeId, FileSystemError>.Failure(
                            FileSystemError.NotADirectory);
                    if (!Allows(parentSource, access, PermissionBits.Execute))
                        return Result<NodeId, FileSystemError>.Failure(
                            FileSystemError.AccessDenied);
                    if (nodes[current].ParentId.HasValue)
                        current = nodes[current].ParentId.Value;
                    continue;
                }

                if (!(nodes[current] is DirectoryNode directory))
                    return Result<NodeId, FileSystemError>.Failure(
                        FileSystemError.NotADirectory);
                if (!Allows(directory, access, PermissionBits.Execute))
                    return Result<NodeId, FileSystemError>.Failure(
                        FileSystemError.AccessDenied);
                if (!children[current].TryGetValue(segment, out var found))
                    return Result<NodeId, FileSystemError>.Failure(
                        FileSystemError.NodeNotFound);

                if (nodes[found] is SymbolicLinkNode link)
                {
                    var linkBase = link.ParentId ?? RootId;
                    var resolvedLink = ResolveFrom(
                        linkBase,
                        link.Target,
                        access,
                        symbolicLinkDepth + 1);

                    if (resolvedLink.IsFailure)
                        return resolvedLink;

                    current = resolvedLink.Value;
                }
                else
                {
                    current = found;
                }
            }

            return Result<NodeId, FileSystemError>.Success(current);
        }

        private Result<Unit, FileSystemError> ValidateCreation(
            NodeId parentId,
            string name,
            AccessContext access)
        {
            if (!IsValidName(name))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.InvalidName);
            if (!nodes.TryGetValue(parentId, out var rawParent))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.ParentNotFound);
            if (!(rawParent is DirectoryNode parent))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.NotADirectory);
            if (!Allows(parent, access, PermissionBits.Write | PermissionBits.Execute))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.AccessDenied);
            if (children[parentId].ContainsKey(name))
                return Result<Unit, FileSystemError>.Failure(
                    FileSystemError.NameAlreadyExists);

            return Result<Unit, FileSystemError>.Success(Unit.Value);
        }

        private Result<Unit, FileSystemError> CollectForDeletion(
            NodeId nodeId,
            AccessContext access,
            List<NodeId> order)
        {
            var node = nodes[nodeId];
            var parent = (DirectoryNode)nodes[node.ParentId.Value];
            if (!Allows(parent, access, PermissionBits.Write | PermissionBits.Execute))
                return Result<Unit, FileSystemError>.Failure(FileSystemError.AccessDenied);

            if (node.Type == FileNodeType.Directory)
            {
                var childIds = new List<NodeId>(children[nodeId].Values);
                foreach (var childId in childIds)
                {
                    var nested = CollectForDeletion(childId, access, order);
                    if (nested.IsFailure)
                        return nested;
                }
            }

            order.Add(nodeId);
            return Result<Unit, FileSystemError>.Success(Unit.Value);
        }

        private bool IsSameOrDescendant(NodeId candidate, NodeId ancestor)
        {
            var current = candidate;
            while (true)
            {
                if (current == ancestor)
                    return true;
                if (!nodes[current].ParentId.HasValue)
                    return false;
                current = nodes[current].ParentId.Value;
            }
        }

        private static bool IsValidName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                   name != "." &&
                   name != ".." &&
                   name != "/" &&
                   name.Length <= 255 &&
                   name.IndexOf('/') < 0 &&
                   name.IndexOf('\0') < 0;
        }

        private static Dictionary<string, NodeId> CreateChildIndex()
        {
            return new Dictionary<string, NodeId>(StringComparer.Ordinal);
        }

        private static bool Allows(
            FileNode node,
            AccessContext access,
            PermissionBits required)
        {
            return PermissionPolicy.Allows(
                node.Ownership,
                node.Permissions,
                access,
                required);
        }

        private void TouchModified(NodeId nodeId)
        {
            var node = nodes[nodeId];
            node.Timestamps = node.Timestamps.WithModified(CurrentTime);
        }

        private void AddEvent(
            FileSystemAction action,
            NodeId nodeId,
            AccessContext access)
        {
            events.Add(new FileSystemEvent(action, nodeId, access.UserId, CurrentTime));
        }

        private static FileEntry ToEntry(FileNode node)
        {
            var contentLength = node is RegularFileNode file ? file.Content.Length : 0;
            return new FileEntry(
                node.Id,
                node.Name,
                node.Type,
                node.Ownership,
                node.Permissions,
                node.Timestamps,
                contentLength);
        }
    }
}
