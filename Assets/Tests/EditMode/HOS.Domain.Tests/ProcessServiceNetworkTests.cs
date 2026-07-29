using HOS.Domain.Common;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Domain.Machines;
using HOS.Domain.Network;
using HOS.Domain.Services;
using NUnit.Framework;

namespace HOS.Domain.Tests
{
    public sealed class ProcessServiceNetworkTests
    {
        [Test]
        public void ProcessOwnerCanExitOwnProcess()
        {
            var machine = CreateMachine("host", "10.0.0.1");
            var kernel = machine.Users.CreateAccessContext(machine.RootUserId, true);
            var ownership = new FileOwnership(machine.RootUserId, machine.RootGroupId);
            var executable = machine.FileSystem.CreateFile(
                machine.FileSystem.RootId,
                "script.lua",
                ownership,
                new FilePermissions(
                    PermissionBits.Read | PermissionBits.Write | PermissionBits.Execute,
                    PermissionBits.Read | PermissionBits.Execute,
                    PermissionBits.Read | PermissionBits.Execute),
                FileContent.FromUtf8("return 0"),
                kernel).Value;
            var started = machine.StartProcess(executable, "script.lua", kernel);

            Assert.That(started.IsSuccess, Is.True);
            Assert.That(
                machine.Processes.Exit(started.Value, 0, machine.RootUserId).IsSuccess,
                Is.True);
            Assert.That(machine.Processes.TryGet(started.Value, out var process), Is.True);
            Assert.That(process.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public void RunningServiceAcceptsConnectionAcrossConfiguredRoute()
        {
            var source = CreateMachine("source", "10.0.0.1");
            var destination = CreateMachine("destination", "10.0.0.2");
            var binding = new PortBinding(22, TransportProtocol.Tcp);
            var service = destination.Services.Add(
                "ssh",
                destination.RootUserId,
                binding).Value;

            destination.Services.Start(service, destination.RootUserId);

            var world = new GameWorld();
            world.AddMachine(source);
            world.AddMachine(destination);
            world.Network.AddBidirectionalRoute(source.Id, destination.Id);

            var connection = world.Network.Connect(
                source.Id,
                destination.Address,
                binding);

            Assert.That(connection.IsSuccess, Is.True);
        }

        [Test]
        public void ClosedPortRejectsConnection()
        {
            var source = CreateMachine("source", "10.0.0.1");
            var destination = CreateMachine("destination", "10.0.0.2");
            var world = new GameWorld();
            world.AddMachine(source);
            world.AddMachine(destination);
            world.Network.AddBidirectionalRoute(source.Id, destination.Id);

            var connection = world.Network.Connect(
                source.Id,
                destination.Address,
                new PortBinding(80, TransportProtocol.Tcp));

            Assert.That(connection.IsFailure, Is.True);
            Assert.That(connection.Error, Is.EqualTo(NetworkError.PortClosed));
        }

        private static Machine CreateMachine(string hostname, string addressText)
        {
            VirtualIpAddress.TryParse(addressText, out var address);
            return Machine.Create(MachineId.New(), hostname, address);
        }
    }
}
