using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using NUnit.Framework;

namespace HOS.Application.Scripting.Tests
{
    public sealed class StandardCommandTests
    {
        [Test]
        public void FileCommandsWorkEndToEnd()
        {
            var world = new TestWorldBuilder();
            world.InstallStandardCommands("mkdir", "cd", "pwd", "write", "cat", "rm");

            AssertSuccess(world.Engine.Execute("mkdir work"));
            AssertSuccess(world.Engine.Execute("cd work"));

            var pwd = world.Engine.Execute("pwd");
            AssertSuccess(pwd);
            Assert.That(pwd.StandardOutput, Is.EqualTo("/home/player/work\n"));

            AssertSuccess(world.Engine.Execute("write note.txt \"hello world\""));
            var cat = world.Engine.Execute("cat note.txt");
            AssertSuccess(cat);
            Assert.That(cat.StandardOutput, Is.EqualTo("hello world\n"));

            AssertSuccess(world.Engine.Execute("rm note.txt"));
            var path = world.Machine.FileSystem.Resolve(
                world.Shell.WorkingDirectory,
                VirtualPath.Parse("note.txt").Value,
                world.Shell.CreateAccessContext());
            Assert.That(path.IsFailure, Is.True);
        }

        [Test]
        public void IdentityCommandsUseCurrentMachineAndUser()
        {
            var world = new TestWorldBuilder();
            world.InstallStandardCommands("whoami", "hostname");

            var whoami = world.Engine.Execute("whoami");
            var hostname = world.Engine.Execute("hostname");

            AssertSuccess(whoami);
            AssertSuccess(hostname);
            Assert.That(whoami.StandardOutput, Is.EqualTo("player\n"));
            Assert.That(hostname.StandardOutput, Is.EqualTo("player-pc\n"));
        }

        [Test]
        public void NonExecutableProgramIsRejectedBeforeLuaRuntime()
        {
            var world = new TestWorldBuilder();
            world.CreateFile(
                world.HomeId,
                "plain.lua",
                "function execute(args) return 0 end",
                world.PlayerOwnership,
                FilePermissions.DefaultFile);

            var result = world.Engine.Execute("./plain");

            Assert.That(result.ExitCode, Is.EqualTo(126));
            Assert.That(result.StandardError, Does.Contain("permission denied"));
        }

        [Test]
        public void UnknownCommandUsesConventionalExitCode()
        {
            var world = new TestWorldBuilder();

            var result = world.Engine.Execute("missing-command");

            Assert.That(result.ExitCode, Is.EqualTo(127));
            Assert.That(result.StandardError, Does.Contain("command not found"));
        }

        private static void AssertSuccess(HOS.Application.Shell.CommandResult result)
        {
            Assert.That(result.IsSuccess, Is.True, result.StandardError);
        }
    }
}
