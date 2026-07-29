using System;
using HOS.Domain.Common;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using NUnit.Framework;

namespace HOS.Domain.Tests
{
    public sealed class VirtualFileSystemTests
    {
        private UserId rootUser;
        private GroupId rootGroup;
        private FileOwnership rootOwnership;
        private AccessContext kernel;
        private VirtualFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            rootUser = UserId.New();
            rootGroup = GroupId.New();
            rootOwnership = new FileOwnership(rootUser, rootGroup);
            kernel = AccessContext.Kernel(rootUser);
            fileSystem = new VirtualFileSystem(rootOwnership);
        }

        [Test]
        public void CreateReadWriteAndResolveFile()
        {
            var home = fileSystem.CreateDirectory(
                fileSystem.RootId,
                "home",
                rootOwnership,
                FilePermissions.DefaultDirectory,
                kernel).Value;

            var script = fileSystem.CreateFile(
                home,
                "exploit.lua",
                rootOwnership,
                FilePermissions.DefaultFile,
                FileContent.FromUtf8("return 1"),
                kernel).Value;

            var path = VirtualPath.Parse("/home/exploit.lua").Value;
            Assert.That(fileSystem.Resolve(fileSystem.RootId, path, kernel).Value, Is.EqualTo(script));
            Assert.That(fileSystem.ReadFile(script, kernel).Value.ReadUtf8(), Is.EqualTo("return 1"));

            fileSystem.AdvanceTime(new GameDuration(10));
            Assert.That(
                fileSystem.WriteFile(script, FileContent.FromUtf8("return 2"), kernel).IsSuccess,
                Is.True);
            Assert.That(fileSystem.ReadFile(script, kernel).Value.ReadUtf8(), Is.EqualTo("return 2"));
            Assert.That(fileSystem.Stat(script).Value.Timestamps.Modified.Ticks, Is.EqualTo(10));
        }

        [Test]
        public void RelativePathHandlesDotDotAndRootBoundary()
        {
            var home = CreateDirectory(fileSystem.RootId, "home");
            var user = CreateDirectory(home, "player");

            var relative = VirtualPath.Parse(".././player").Value;
            Assert.That(fileSystem.Resolve(user, relative, kernel).Value, Is.EqualTo(user));

            var aboveRoot = VirtualPath.Parse("../../../../").Value;
            Assert.That(fileSystem.Resolve(home, aboveRoot, kernel).Value, Is.EqualTo(fileSystem.RootId));
        }

        [Test]
        public void DuplicateNamesAreRejectedAcrossNodeTypes()
        {
            CreateDirectory(fileSystem.RootId, "bin");
            var duplicate = fileSystem.CreateFile(
                fileSystem.RootId,
                "bin",
                rootOwnership,
                FilePermissions.DefaultFile,
                FileContent.Empty,
                kernel);

            Assert.That(duplicate.IsFailure, Is.True);
            Assert.That(duplicate.Error, Is.EqualTo(FileSystemError.NameAlreadyExists));
        }

        [Test]
        public void NonRecursiveDeleteRejectsNonEmptyDirectory()
        {
            var directory = CreateDirectory(fileSystem.RootId, "tmp");
            CreateDirectory(directory, "nested");

            var result = fileSystem.Delete(directory, DeleteOptions.Single, kernel);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(FileSystemError.DirectoryNotEmpty));
            Assert.That(fileSystem.Stat(directory).IsSuccess, Is.True);
        }

        [Test]
        public void RecursiveDeleteRemovesEntireSubtree()
        {
            var directory = CreateDirectory(fileSystem.RootId, "tmp");
            var nested = CreateDirectory(directory, "nested");
            var file = fileSystem.CreateFile(
                nested,
                "data",
                rootOwnership,
                FilePermissions.DefaultFile,
                FileContent.Empty,
                kernel).Value;

            Assert.That(
                fileSystem.Delete(directory, DeleteOptions.RecursiveDelete, kernel).IsSuccess,
                Is.True);
            Assert.That(fileSystem.Stat(directory).IsFailure, Is.True);
            Assert.That(fileSystem.Stat(nested).IsFailure, Is.True);
            Assert.That(fileSystem.Stat(file).IsFailure, Is.True);
        }

        [Test]
        public void DirectoryCannotBeMovedIntoItsDescendant()
        {
            var parent = CreateDirectory(fileSystem.RootId, "parent");
            var child = CreateDirectory(parent, "child");

            var result = fileSystem.Move(parent, child, "parent", kernel);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(FileSystemError.CannotMoveDirectoryIntoItself));
        }

        [Test]
        public void SymbolicLinkResolvesAndLoopIsDetected()
        {
            var target = CreateDirectory(fileSystem.RootId, "target");
            var linkPath = VirtualPath.Parse("/target").Value;
            fileSystem.CreateSymbolicLink(
                fileSystem.RootId,
                "link",
                linkPath,
                rootOwnership,
                FilePermissions.DefaultFile,
                kernel);

            Assert.That(
                fileSystem.Resolve(
                    fileSystem.RootId,
                    VirtualPath.Parse("/link").Value,
                    kernel).Value,
                Is.EqualTo(target));

            fileSystem.CreateSymbolicLink(
                fileSystem.RootId,
                "loop",
                VirtualPath.Parse("/loop").Value,
                rootOwnership,
                FilePermissions.DefaultFile,
                kernel);

            var loop = fileSystem.Resolve(
                fileSystem.RootId,
                VirtualPath.Parse("/loop").Value,
                kernel);
            Assert.That(loop.IsFailure, Is.True);
            Assert.That(loop.Error, Is.EqualTo(FileSystemError.SymbolicLinkLoop));
        }

        [Test]
        public void MutationsProduceDomainEvents()
        {
            var directory = CreateDirectory(fileSystem.RootId, "events");
            var drained = fileSystem.DrainEvents();

            Assert.That(drained.Count, Is.EqualTo(1));
            var created = drained[0] as FileSystemEvent;
            Assert.That(created, Is.Not.Null);
            Assert.That(created.Action, Is.EqualTo(FileSystemAction.Created));
            Assert.That(created.NodeId, Is.EqualTo(directory));
            Assert.That(fileSystem.DrainEvents(), Is.Empty);
        }

        private NodeId CreateDirectory(NodeId parent, string name)
        {
            return fileSystem.CreateDirectory(
                parent,
                name,
                rootOwnership,
                FilePermissions.DefaultDirectory,
                kernel).Value;
        }
    }
}
