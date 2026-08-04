using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using NUnit.Framework;

namespace HOS.Domain.Tests
{
    public sealed class PermissionTests
    {
        [Test]
        public void OtherUserCannotReadPrivateFile()
        {
            var owner = UserId.New();
            var intruder = UserId.New();
            var group = GroupId.New();
            var ownership = new FileOwnership(owner, group);
            var fileSystem = new VirtualFileSystem(ownership);
            var kernel = AccessContext.Kernel(owner);

            var privatePermissions = new FilePermissions(
                PermissionBits.Read | PermissionBits.Write,
                PermissionBits.None,
                PermissionBits.None);

            var file = fileSystem.CreateFile(
                fileSystem.RootId,
                "secret",
                ownership,
                privatePermissions,
                FileContent.FromUtf8("password"),
                kernel).Value;

            var result = fileSystem.ReadFile(
                file,
                new AccessContext(intruder, new GroupId[0]));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(FileSystemError.AccessDenied));
        }

        [Test]
        public void GroupPermissionsAllowReading()
        {
            var owner = UserId.New();
            var colleague = UserId.New();
            var group = GroupId.New();
            var ownership = new FileOwnership(owner, group);
            var fileSystem = new VirtualFileSystem(ownership);
            var kernel = AccessContext.Kernel(owner);

            var permissions = new FilePermissions(
                PermissionBits.Read | PermissionBits.Write,
                PermissionBits.Read,
                PermissionBits.None);

            var file = fileSystem.CreateFile(
                fileSystem.RootId,
                "shared",
                ownership,
                permissions,
                FileContent.FromUtf8("shared"),
                kernel).Value;

            var access = new AccessContext(colleague, new[] { group });
            Assert.That(fileSystem.ReadFile(file, access).IsSuccess, Is.True);
        }

        [Test]
        public void OnlyKernelCanChangeOwner()
        {
            var owner = UserId.New();
            var group = GroupId.New();
            var ownership = new FileOwnership(owner, group);
            var fileSystem = new VirtualFileSystem(ownership);
            var kernel = AccessContext.Kernel(owner);
            var file = fileSystem.CreateFile(
                fileSystem.RootId,
                "file",
                ownership,
                FilePermissions.DefaultFile,
                FileContent.Empty,
                kernel).Value;

            var newOwnership = new FileOwnership(UserId.New(), GroupId.New());
            var denied = fileSystem.ChangeOwner(
                file,
                newOwnership,
                new AccessContext(owner, new[] { group }));

            Assert.That(denied.IsFailure, Is.True);
            Assert.That(fileSystem.ChangeOwner(file, newOwnership, kernel).IsSuccess, Is.True);
        }

        [Test]
        public void CopyRequiresSourceReadAndDestinationWriteAccess()
        {
            var owner = UserId.New();
            var intruder = UserId.New();
            var group = GroupId.New();
            var ownership = new FileOwnership(owner, group);
            var fileSystem = new VirtualFileSystem(ownership);
            var kernel = AccessContext.Kernel(owner);
            var privateFile = fileSystem.CreateFile(
                fileSystem.RootId,
                "private",
                ownership,
                new FilePermissions(
                    PermissionBits.Read | PermissionBits.Write,
                    PermissionBits.None,
                    PermissionBits.None),
                FileContent.FromUtf8("secret"),
                kernel).Value;
            var publicDirectory = fileSystem.CreateDirectory(
                fileSystem.RootId,
                "public",
                ownership,
                new FilePermissions(
                    PermissionBits.Read | PermissionBits.Write | PermissionBits.Execute,
                    PermissionBits.Read | PermissionBits.Write | PermissionBits.Execute,
                    PermissionBits.Read | PermissionBits.Write | PermissionBits.Execute),
                kernel).Value;
            var access = new AccessContext(intruder, new GroupId[0]);

            var unreadable = fileSystem.CopyFile(
                privateFile,
                publicDirectory,
                "copy",
                new FileOwnership(intruder, GroupId.New()),
                access);

            Assert.That(unreadable.IsFailure, Is.True);
            Assert.That(unreadable.Error, Is.EqualTo(FileSystemError.AccessDenied));

            var readableFile = fileSystem.CreateFile(
                fileSystem.RootId,
                "readable",
                ownership,
                new FilePermissions(PermissionBits.Read, PermissionBits.Read, PermissionBits.Read),
                FileContent.FromUtf8("visible"),
                kernel).Value;
            var unwritable = fileSystem.CopyFile(
                readableFile,
                fileSystem.RootId,
                "blocked-copy",
                new FileOwnership(intruder, GroupId.New()),
                access);

            Assert.That(unwritable.IsFailure, Is.True);
            Assert.That(unwritable.Error, Is.EqualTo(FileSystemError.AccessDenied));
        }
    }
}
