using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Domain.Machines;
using HOS.Domain.Network;
using NUnit.Framework;

namespace HOS.Domain.Tests
{
    public sealed class MachineAndWorldTests
    {
        [Test]
        public void MachinesOwnIndependentFileSystems()
        {
            var first = CreateMachine("first", "10.0.0.1");
            var second = CreateMachine("second", "10.0.0.2");
            var firstKernel = first.Users.CreateAccessContext(first.RootUserId, true);
            var ownership = new FileOwnership(first.RootUserId, first.RootGroupId);

            first.FileSystem.CreateDirectory(
                first.FileSystem.RootId,
                "only-first",
                ownership,
                FilePermissions.DefaultDirectory,
                firstKernel);

            var path = VirtualPath.Parse("/only-first").Value;
            Assert.That(
                first.FileSystem.Resolve(first.FileSystem.RootId, path, firstKernel).IsSuccess,
                Is.True);

            var secondKernel = second.Users.CreateAccessContext(second.RootUserId, true);
            Assert.That(
                second.FileSystem.Resolve(second.FileSystem.RootId, path, secondKernel).IsFailure,
                Is.True);
            Assert.That(first.FileSystem.RootId, Is.Not.EqualTo(second.FileSystem.RootId));
        }

        [Test]
        public void WorldRejectsDuplicateAddresses()
        {
            var world = new GameWorld();
            var first = CreateMachine("first", "10.0.0.1");
            var duplicate = CreateMachine("duplicate", "10.0.0.1");

            Assert.That(world.AddMachine(first).IsSuccess, Is.True);
            Assert.That(world.AddMachine(duplicate).IsFailure, Is.True);
        }

        private static Machine CreateMachine(string hostname, string addressText)
        {
            Assert.That(VirtualIpAddress.TryParse(addressText, out var address), Is.True);
            return Machine.Create(MachineId.New(), hostname, address);
        }
    }
}
