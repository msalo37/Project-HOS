using System.Linq;
using HOS.Application.Execution;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Scripting;
using HOS.Unity.Bootstrap;
using NUnit.Framework;

namespace HOS.Unity.Tests
{
    public sealed class DefaultGameBootstrapTests
    {
        private static readonly string[] ExpectedCommands =
        {
            "cat.lua",
            "cd.lua",
            "chmod.lua",
            "hostname.lua",
            "kill.lua",
            "ls.lua",
            "mkdir.lua",
            "mv.lua",
            "ps.lua",
            "pwd.lua",
            "rm.lua",
            "touch.lua",
            "whoami.lua",
            "write.lua"
        };

        [Test]
        public void CreateBuildsExpectedPlayerMachine()
        {
            var game = new DefaultGameBootstrap().Create();

            Assert.That(game.World.Machines.Count, Is.EqualTo(1));
            Assert.That(game.PlayerMachine.Hostname, Is.EqualTo("player-pc"));
            Assert.That(game.PlayerMachine.Address.ToString(), Is.EqualTo("10.0.0.1"));
            Assert.That(game.Shell.GetEnvironment("HOME"), Is.EqualTo("/home/player"));
            Assert.That(game.Shell.GetEnvironment("PATH"), Is.EqualTo("/bin:/usr/bin"));
            Assert.That(game.PlayerMachine.Users.TryGetUser(game.PlayerUserId, out var player), Is.True);
            Assert.That(player.Name, Is.EqualTo("player"));

            AssertPathExists(game, "/bin", FileNodeType.Directory);
            AssertPathExists(game, "/home/player", FileNodeType.Directory);
            AssertPathExists(game, "/log", FileNodeType.Directory);
            AssertPathExists(game, "/tmp", FileNodeType.Directory);
            AssertPathExists(game, "/usr/bin", FileNodeType.Directory);
            AssertPathExists(game, "/home/player/readme.txt", FileNodeType.RegularFile);
        }

        [Test]
        public void CreateInstallsAllStandardCommandsAsPlayerExecutableFiles()
        {
            var game = new DefaultGameBootstrap().Create();
            var access = game.Shell.CreateAccessContext();
            var listed = game.PlayerMachine.FileSystem.List(game.BinDirectoryId, access);

            Assert.That(listed.IsSuccess, Is.True);
            Assert.That(
                listed.Value.Select(entry => entry.Name).ToArray(),
                Is.EqualTo(ExpectedCommands));

            foreach (var entry in listed.Value)
            {
                Assert.That(entry.Type, Is.EqualTo(FileNodeType.RegularFile), entry.Name);
                var executable = game.PlayerMachine.FileSystem.CheckAccess(
                    entry.Id,
                    PermissionBits.Read | PermissionBits.Execute,
                    access);
                Assert.That(executable.IsSuccess, Is.True, entry.Name);
            }
        }

        [Test]
        public void InstalledCommandsWorkAgainstBootstrappedWorld()
        {
            var game = new DefaultGameBootstrap().Create();
            var engine = new ShellEngine(game.Shell, new LuaProgramRuntime());

            AssertSuccess(engine.Execute("mkdir code"));
            AssertSuccess(engine.Execute("cd code"));
            AssertSuccess(engine.Execute("write hello.txt \"hello from lua\""));

            var pwd = engine.Execute("pwd");
            var cat = engine.Execute("cat hello.txt");

            AssertSuccess(pwd);
            AssertSuccess(cat);
            Assert.That(pwd.StandardOutput, Is.EqualTo("/home/player/code\n"));
            Assert.That(cat.StandardOutput, Is.EqualTo("hello from lua\n"));
        }

        [Test]
        public void CreateReturnsIndependentWorldEveryTime()
        {
            var bootstrap = new DefaultGameBootstrap();
            var first = bootstrap.Create();
            var second = bootstrap.Create();

            Assert.That(second.World, Is.Not.SameAs(first.World));
            Assert.That(second.PlayerMachine, Is.Not.SameAs(first.PlayerMachine));
            Assert.That(second.PlayerMachine.Id, Is.Not.EqualTo(first.PlayerMachine.Id));
            Assert.That(second.PlayerHomeDirectoryId, Is.Not.EqualTo(first.PlayerHomeDirectoryId));
        }

        private static void AssertPathExists(
            GameBootstrapResult game,
            string path,
            FileNodeType expectedType)
        {
            var parsed = VirtualPath.Parse(path);
            Assert.That(parsed.IsSuccess, Is.True, path);

            var resolved = game.PlayerMachine.FileSystem.Resolve(
                game.Shell.WorkingDirectory,
                parsed.Value,
                game.Shell.CreateAccessContext());
            Assert.That(resolved.IsSuccess, Is.True, path);

            var entry = game.PlayerMachine.FileSystem.Stat(resolved.Value);
            Assert.That(entry.IsSuccess, Is.True, path);
            Assert.That(entry.Value.Type, Is.EqualTo(expectedType), path);
        }

        private static void AssertSuccess(HOS.Application.Shell.CommandResult result)
        {
            Assert.That(result.IsSuccess, Is.True, result.StandardError);
        }
    }
}
