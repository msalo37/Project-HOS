using System;
using System.Linq;
using HOS.Application.Shell;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Domain.Machines;
using HOS.Domain.Network;
using HOS.Domain.Services;
using UnityEngine;

namespace HOS.Unity.Bootstrap
{
    public sealed class DefaultGameBootstrap
    {
        private const string StandardCommandsResourcePath = "HOS/StandardCommands";

        private static readonly FilePermissions ExecutablePermissions =
            new FilePermissions(
                PermissionBits.Read | PermissionBits.Write | PermissionBits.Execute,
                PermissionBits.Read | PermissionBits.Execute,
                PermissionBits.Read | PermissionBits.Execute);

        private static readonly FilePermissions PublicTemporaryDirectoryPermissions =
            new FilePermissions(
                PermissionBits.Read | PermissionBits.Write | PermissionBits.Execute,
                PermissionBits.Read | PermissionBits.Write | PermissionBits.Execute,
                PermissionBits.Read | PermissionBits.Write | PermissionBits.Execute);

        public GameBootstrapResult Create()
        {
            if (!VirtualIpAddress.TryParse("10.0.0.1", out var address))
                throw new InvalidOperationException("The default player IP address is invalid.");

            var machine = Machine.Create(MachineId.New(), "player-pc", address);
            var world = new GameWorld();
            RequireSuccess(world.AddMachine(machine), "register the player machine");

            var rootAccess = machine.Users.CreateAccessContext(machine.RootUserId, true);
            var rootOwnership = new FileOwnership(machine.RootUserId, machine.RootGroupId);
            var usersGroup = RequireValue(machine.Users.CreateGroup("users"), "create users group");
            var playerUser = RequireValue(
                machine.Users.CreateUser("player", usersGroup),
                "create player user");
            var playerOwnership = new FileOwnership(playerUser, usersGroup);

            var fileSystem = machine.FileSystem;
            var bin = CreateDirectory(
                fileSystem,
                fileSystem.RootId,
                "bin",
                rootOwnership,
                FilePermissions.DefaultDirectory,
                rootAccess);
            var home = CreateDirectory(
                fileSystem,
                fileSystem.RootId,
                "home",
                rootOwnership,
                FilePermissions.DefaultDirectory,
                rootAccess);
            var playerHome = CreateDirectory(
                fileSystem,
                home,
                "player",
                playerOwnership,
                FilePermissions.DefaultDirectory,
                rootAccess);
            CreateDirectory(
                fileSystem,
                fileSystem.RootId,
                "log",
                rootOwnership,
                FilePermissions.DefaultDirectory,
                rootAccess);
            CreateDirectory(
                fileSystem,
                fileSystem.RootId,
                "tmp",
                rootOwnership,
                PublicTemporaryDirectoryPermissions,
                rootAccess);
            var usr = CreateDirectory(
                fileSystem,
                fileSystem.RootId,
                "usr",
                rootOwnership,
                FilePermissions.DefaultDirectory,
                rootAccess);
            CreateDirectory(
                fileSystem,
                usr,
                "bin",
                rootOwnership,
                FilePermissions.DefaultDirectory,
                rootAccess);

            InstallStandardCommands(fileSystem, bin, rootOwnership, rootAccess);
            CreateReadme(fileSystem, playerHome, playerOwnership, rootAccess);

            var shell = new PlayerShellContext(world, machine.Id, playerUser, playerHome);
            shell.SetEnvironment("HOME", "/home/player");
            shell.SetEnvironment("PATH", "/bin:/usr/bin");
            var developmentServer = CreateDevelopmentServer(world, machine);

            return new GameBootstrapResult(
                world,
                machine,
                developmentServer,
                shell,
                playerUser,
                bin,
                playerHome);
        }

        private static Machine CreateDevelopmentServer(GameWorld world, Machine playerMachine)
        {
            VirtualIpAddress.TryParse("10.0.0.2", out var address);
            var machine = Machine.Create(MachineId.New(), "dev-server", address);
            RequireSuccess(world.AddMachine(machine), "register development server");
            world.Network.AddBidirectionalRoute(playerMachine.Id, machine.Id);

            var root = machine.Users.CreateAccessContext(machine.RootUserId, true);
            var rootOwnership = new FileOwnership(machine.RootUserId, machine.RootGroupId);
            var users = RequireValue(machine.Users.CreateGroup("users"), "create remote users group");
            var guest = RequireValue(machine.Users.CreateUser("guest", users), "create guest user");
            machine.Credentials.SetPassword(guest, "guest");
            var guestOwnership = new FileOwnership(guest, users);

            var home = CreateDirectory(machine.FileSystem, machine.FileSystem.RootId, "home", rootOwnership, FilePermissions.DefaultDirectory, root);
            var guestHome = CreateDirectory(machine.FileSystem, home, "guest", guestOwnership, FilePermissions.DefaultDirectory, root);
            RequireSuccess(machine.FileSystem.CreateFile(
                guestHome, "test.lua", guestOwnership, ExecutablePermissions,
                FileContent.FromUtf8("function execute(args) hos.stdout('remote program on ' .. hos.shell.hostname()) return 0 end"), root),
                "create remote test program");
            RequireSuccess(machine.FileSystem.CreateFile(
                guestHome, "welcome.txt", guestOwnership, FilePermissions.DefaultFile,
                FileContent.FromUtf8("Welcome to dev-server."), root), "create remote welcome file");

            var ssh = RequireValue(machine.Services.Add(
                "openssh", machine.RootUserId, new PortBinding(22, TransportProtocol.Tcp),
                ServiceProtocol.Ssh, "9.1", true), "add ssh service");
            RequireSuccess(machine.Services.Start(ssh, machine.RootUserId), "start ssh service");
            var backdoor = RequireValue(machine.Services.Add(
                "maintenance", machine.RootUserId, new PortBinding(31337, TransportProtocol.Tcp),
                ServiceProtocol.Backdoor, "1.0", false, guest), "add backdoor service");
            RequireSuccess(machine.Services.Start(backdoor, machine.RootUserId), "start backdoor service");
            return machine;
        }

        private static void InstallStandardCommands(
            VirtualFileSystem fileSystem,
            NodeId bin,
            FileOwnership rootOwnership,
            AccessContext rootAccess)
        {
            var commands = Resources.LoadAll<TextAsset>(StandardCommandsResourcePath)
                .OrderBy(asset => asset.name, StringComparer.Ordinal)
                .ToArray();
            if (commands.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No Lua commands found in Resources/{StandardCommandsResourcePath}.");
            }

            foreach (var command in commands)
            {
                var created = fileSystem.CreateFile(
                    bin,
                    command.name + ".lua",
                    rootOwnership,
                    ExecutablePermissions,
                    FileContent.FromUtf8(command.text),
                    rootAccess);
                RequireSuccess(created, $"install command {command.name}");
            }
        }

        private static void CreateReadme(
            VirtualFileSystem fileSystem,
            NodeId playerHome,
            FileOwnership playerOwnership,
            AccessContext rootAccess)
        {
            const string content =
                "Welcome to HOS.\n" +
                "Commands are Lua programs stored in /bin.\n" +
                "Create your own .lua file, grant execute permission, and run it with ./name.\n";

            var created = fileSystem.CreateFile(
                playerHome,
                "readme.txt",
                playerOwnership,
                FilePermissions.DefaultFile,
                FileContent.FromUtf8(content),
                rootAccess);
            RequireSuccess(created, "create player readme");
        }

        private static NodeId CreateDirectory(
            VirtualFileSystem fileSystem,
            NodeId parent,
            string name,
            FileOwnership ownership,
            FilePermissions permissions,
            AccessContext access)
        {
            return RequireValue(
                fileSystem.CreateDirectory(parent, name, ownership, permissions, access),
                $"create directory {name}");
        }

        private static TValue RequireValue<TValue, TError>(
            HOS.Domain.Common.Result<TValue, TError> result,
            string operation)
        {
            if (result.IsFailure)
                throw new InvalidOperationException($"Failed to {operation}: {result.Error}");
            return result.Value;
        }

        private static void RequireSuccess<TValue, TError>(
            HOS.Domain.Common.Result<TValue, TError> result,
            string operation)
        {
            if (result.IsFailure)
                throw new InvalidOperationException($"Failed to {operation}: {result.Error}");
        }
    }
}
