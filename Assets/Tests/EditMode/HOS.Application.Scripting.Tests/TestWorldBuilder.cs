using System;
using System.Collections.Generic;
using HOS.Application.Execution;
using HOS.Application.Shell;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Domain.Machines;
using HOS.Domain.Network;
using HOS.Scripting;
using NUnit.Framework;
using UnityEngine;

namespace HOS.Application.Scripting.Tests
{
    internal sealed class TestWorldBuilder
    {
        private static readonly FilePermissions ExecutablePermissions =
            new FilePermissions(
                PermissionBits.Read | PermissionBits.Write | PermissionBits.Execute,
                PermissionBits.Read | PermissionBits.Execute,
                PermissionBits.Read | PermissionBits.Execute);

        public TestWorldBuilder(LuaRuntimeOptions options = null)
        {
            VirtualIpAddress.TryParse("10.0.0.1", out var address);
            Machine = Machine.Create(MachineId.New(), "player-pc", address);
            World = new GameWorld();
            Assert.That(World.AddMachine(Machine).IsSuccess, Is.True);

            RootAccess = Machine.Users.CreateAccessContext(Machine.RootUserId, true);
            RootOwnership = new FileOwnership(Machine.RootUserId, Machine.RootGroupId);
            var usersGroup = Machine.Users.CreateGroup("users").Value;
            PlayerId = Machine.Users.CreateUser("player", usersGroup).Value;
            PlayerOwnership = new FileOwnership(PlayerId, usersGroup);

            BinId = CreateDirectory(Machine.FileSystem.RootId, "bin", RootOwnership);
            var home = CreateDirectory(Machine.FileSystem.RootId, "home", RootOwnership);
            HomeId = CreateDirectory(home, "player", PlayerOwnership);

            Shell = new PlayerShellContext(World, Machine.Id, PlayerId, HomeId);
            Shell.SetEnvironment("HOME", "/home/player");
            Engine = new ShellEngine(Shell, new LuaProgramRuntime(options));
        }

        public GameWorld World { get; }
        public Machine Machine { get; }
        public UserId PlayerId { get; }
        public FileOwnership RootOwnership { get; }
        public FileOwnership PlayerOwnership { get; }
        public AccessContext RootAccess { get; }
        public NodeId BinId { get; }
        public NodeId HomeId { get; }
        public PlayerShellContext Shell { get; }
        public ShellEngine Engine { get; }

        public NodeId InstallStandardCommand(string command)
        {
            var asset = Resources.Load<TextAsset>("HOS/StandardCommands/" + command);
            Assert.That(asset, Is.Not.Null, $"Standard command {command}.lua was not imported.");
            return CreateExecutable(BinId, command + ".lua", asset.text, RootOwnership);
        }

        public void InstallStandardCommands(params string[] commands)
        {
            foreach (var command in commands)
                InstallStandardCommand(command);
        }

        public NodeId CreatePlayerExecutable(string name, string source)
        {
            return CreateExecutable(HomeId, name, source, PlayerOwnership);
        }

        public NodeId CreateFile(
            NodeId parent,
            string name,
            string content,
            FileOwnership ownership,
            FilePermissions permissions)
        {
            return Machine.FileSystem.CreateFile(
                parent,
                name,
                ownership,
                permissions,
                FileContent.FromUtf8(content),
                RootAccess).Value;
        }

        private NodeId CreateExecutable(
            NodeId parent,
            string name,
            string source,
            FileOwnership ownership)
        {
            return CreateFile(parent, name, source, ownership, ExecutablePermissions);
        }

        private NodeId CreateDirectory(
            NodeId parent,
            string name,
            FileOwnership ownership)
        {
            return Machine.FileSystem.CreateDirectory(
                parent,
                name,
                ownership,
                FilePermissions.DefaultDirectory,
                RootAccess).Value;
        }
    }
}
