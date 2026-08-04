using System.Linq;
using HOS.Application.Execution;
using HOS.Application.Remote;
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
            "cp.lua",
            "exit.lua",
            "hostname.lua",
            "kill.lua",
            "ls.lua",
            "mkdir.lua",
            "mv.lua",
            "nano.hos",
            "ps.lua",
            "pwd.lua",
            "rm.lua",
            "scan.lua",
            "ssh.lua",
            "sysinfo.hos",
            "touch.lua",
            "whoami.lua",
            "write.lua"
        };

        [Test]
        public void CreateBuildsExpectedPlayerMachine()
        {
            var game = new DefaultGameBootstrap().Create();

            Assert.That(game.World.Machines.Count, Is.EqualTo(2));
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
        public void InstalledHosBinaryRunsFromPath()
        {
            var game = new DefaultGameBootstrap().Create();
            var engine = new ShellEngine(game.Shell, CreateRuntime());

            var result = engine.Execute("sysinfo");

            AssertSuccess(result);
            Assert.That(result.StandardOutput, Does.Contain("hostname: player-pc\n"));
            Assert.That(result.StandardOutput, Does.Contain("address: 10.0.0.1\n"));
            Assert.That(result.StandardOutput, Does.Contain("user: player\n"));
        }

        [Test]
        public void CopiedHosBinaryRunsOnRemoteMachine()
        {
            var game = new DefaultGameBootstrap().Create();
            var localAccess = game.Shell.CreateAccessContext();
            var localBinary = Resolve(
                game.PlayerMachine.FileSystem,
                game.PlayerMachine.FileSystem.RootId,
                "/bin/sysinfo.hos",
                localAccess);
            var binaryContent = game.PlayerMachine.FileSystem.ReadFile(localBinary, localAccess);
            Assert.That(binaryContent.IsSuccess, Is.True);

            var remote = game.DevelopmentServer;
            Assert.That(remote.Users.TryGetUser("guest", out var guest), Is.True);
            var remoteRoot = remote.Users.CreateAccessContext(remote.RootUserId, true);
            var guestHome = Resolve(
                remote.FileSystem,
                remote.FileSystem.RootId,
                "/home/guest",
                remoteRoot);
            var executable = new FilePermissions(
                PermissionBits.Read | PermissionBits.Write | PermissionBits.Execute,
                PermissionBits.Read | PermissionBits.Execute,
                PermissionBits.None);
            var copied = remote.FileSystem.CreateFile(
                guestHome,
                "sysinfo.hos",
                new FileOwnership(guest.Id, guest.PrimaryGroup),
                executable,
                binaryContent.Value,
                remoteRoot);
            Assert.That(copied.IsSuccess, Is.True);

            var engine = new ShellEngine(game.Shell, CreateRuntime());
            AssertSuccess(engine.Execute("ssh 10.0.0.2 22"));
            AssertSuccess(engine.Execute("guest"));
            AssertSuccess(engine.Execute("guest"));

            var result = engine.Execute("./sysinfo.hos");

            AssertSuccess(result);
            Assert.That(result.StandardOutput, Does.Contain("hostname: dev-server\n"));
            Assert.That(result.StandardOutput, Does.Contain("address: 10.0.0.2\n"));
            Assert.That(result.StandardOutput, Does.Contain("user: guest\n"));
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

        [Test]
        public void LocalToolsOperateOnRemoteMachineAndExplicitProgramRunsRemotely()
        {
            var game = new DefaultGameBootstrap().Create();
            var engine = new ShellEngine(game.Shell, new LuaProgramRuntime());

            AssertSuccess(engine.Execute("ssh 10.0.0.2 22"));
            Assert.That(engine.IsAwaitingInput, Is.True);
            AssertSuccess(engine.Execute("guest"));
            Assert.That(engine.IsSecretInput, Is.True);
            AssertSuccess(engine.Execute("guest"));
            Assert.That(game.Shell.CurrentMachine.Hostname, Is.EqualTo("dev-server"));

            var cat = engine.Execute("cat welcome.txt");
            var remoteProgram = engine.Execute("./test.lua");
            AssertSuccess(cat);
            AssertSuccess(remoteProgram);
            Assert.That(cat.StandardOutput, Is.EqualTo("Welcome to dev-server.\n"));
            Assert.That(remoteProgram.StandardOutput, Does.Contain("dev-server"));
            Assert.That(game.PlayerMachine.Processes.List().Any(p => p.Name == "cat"), Is.True);
            Assert.That(game.DevelopmentServer.Processes.List().Any(p => p.Name == "./test.lua"), Is.True);

            AssertSuccess(engine.Execute("exit"));
            Assert.That(game.Shell.CurrentMachine.Hostname, Is.EqualTo("player-pc"));
        }

        [Test]
        public void LocalNanoEditsFileOnCurrentRemoteMachine()
        {
            var game = new DefaultGameBootstrap().Create();
            var engine = new ShellEngine(game.Shell, CreateRuntime());
            AssertSuccess(engine.Execute("ssh 10.0.0.2 22"));
            AssertSuccess(engine.Execute("guest"));
            AssertSuccess(engine.Execute("guest"));

            var started = engine.Execute("nano welcome.txt");

            AssertSuccess(started);
            Assert.That(engine.HasForegroundProgram, Is.True);
            engine.SendForegroundInput(new ProgramInput(ProgramInputKey.End));
            engine.SendForegroundInput(new ProgramInput(ProgramInputKey.Enter));
            foreach (var character in "edited remotely")
                engine.SendForegroundInput(ProgramInput.Text(character));
            engine.SendForegroundInput(new ProgramInput(ProgramInputKey.O, control: true));
            var exited = engine.SendForegroundInput(
                new ProgramInput(ProgramInputKey.X, control: true));

            Assert.That(exited.HasExited, Is.True);
            Assert.That(exited.Result.IsSuccess, Is.True, exited.Result.StandardError);
            var content = engine.Execute("cat welcome.txt");
            AssertSuccess(content);
            Assert.That(content.StandardOutput, Is.EqualTo("Welcome to dev-server.\nedited remotely\n"));
        }

        [Test]
        public void PasswordlessServiceEntersRemoteMachine()
        {
            var game = new DefaultGameBootstrap().Create();
            var engine = new ShellEngine(game.Shell, new LuaProgramRuntime());

            var result = engine.Execute("ssh 10.0.0.2 31337");

            AssertSuccess(result);
            Assert.That(engine.IsAwaitingInput, Is.False);
            Assert.That(game.Shell.CurrentMachine.Hostname, Is.EqualTo("dev-server"));
        }

        [Test]
        public void DevSyncRejectsInvalidProtocolSequence()
        {
            var game = new DefaultGameBootstrap().Create();
            var remote = new RemoteAccessService(game.Shell);
            var connection = remote.Connect("10.0.0.2", 8080);
            Assert.That(connection.IsSuccess, Is.True);

            var beforeAuthentication = remote.Request(
                connection.Value,
                new ServiceRequest(
                    "execute_job",
                    new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["token"] = "missing",
                        ["payload"] = "function execute(args) return 0 end"
                    }));
            var challenge = remote.Request(
                connection.Value,
                new ServiceRequest("challenge"));
            var wrongProof = remote.Request(
                connection.Value,
                new ServiceRequest(
                    "authenticate",
                    new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["proof"] = "wrong"
                    }));

            Assert.That(beforeAuthentication.IsSuccess, Is.True);
            Assert.That(beforeAuthentication.Value.Success, Is.False);
            Assert.That(challenge.Value.Success, Is.True);
            Assert.That(challenge.Value.Fields["nonce"], Is.Not.Empty);
            Assert.That(wrongProof.Value.Success, Is.False);
            Assert.That(game.Shell.IsRemote, Is.False);
        }

        [Test]
        public void LocalLuaExploitOpensDeployShellAndUploadsRemotePayload()
        {
            var game = new DefaultGameBootstrap().Create();
            var local = game.PlayerMachine;
            local.Users.TryGetUser(game.PlayerUserId, out var player);
            var root = local.Users.CreateAccessContext(local.RootUserId, true);
            var permissions = new FilePermissions(
                PermissionBits.Read | PermissionBits.Write | PermissionBits.Execute,
                PermissionBits.Read | PermissionBits.Execute,
                PermissionBits.None);
            var source =
                "local function proof(nonce)\n" +
                "  local sum = 0\n" +
                "  for i = 1, #nonce do sum = (sum + string.byte(nonce, i) * i) % 65535 end\n" +
                "  return tostring(sum)\n" +
                "end\n" +
                "function execute(args)\n" +
                "  local c, e = hos.net.connect('10.0.0.2', 8080)\n" +
                "  if not c then hos.stderr(e.message) return 1 end\n" +
                "  local challenge = hos.net.request(c, { operation = 'challenge' })\n" +
                "  local auth = hos.net.request(c, { operation = 'authenticate', proof = proof(challenge.nonce) })\n" +
                "  if not auth.success then hos.stderr(auth.message) return 1 end\n" +
                "  local job = hos.net.request(c, { operation = 'execute_job', token = auth.token,\n" +
                "    payload = \"function execute(args) hos.stdout('payload ran') return 0 end\" })\n" +
                "  if not job.success then hos.stderr(job.message) return 1 end\n" +
                "  return 0\n" +
                "end";
            var created = local.FileSystem.CreateFile(
                game.PlayerHomeDirectoryId,
                "devsync_exploit.lua",
                new FileOwnership(player.Id, player.PrimaryGroup),
                permissions,
                FileContent.FromUtf8(source),
                root);
            Assert.That(created.IsSuccess, Is.True);
            var engine = new ShellEngine(game.Shell, new LuaProgramRuntime());

            var exploited = engine.Execute("./devsync_exploit.lua");

            AssertSuccess(exploited);
            Assert.That(game.Shell.CurrentMachine.Hostname, Is.EqualTo("dev-server"));
            Assert.That(
                game.Shell.CurrentMachine.Users.TryGetUser(
                    game.Shell.CurrentUserId, out var remoteUser),
                Is.True);
            Assert.That(remoteUser.Name, Is.EqualTo("deploy"));
            var payload = engine.Execute("./last_job.lua");
            AssertSuccess(payload);
            Assert.That(payload.StandardOutput, Is.EqualTo("payload ran\n"));
            var objective = engine.Execute("cat objective.txt");
            AssertSuccess(objective);
            Assert.That(objective.StandardOutput, Does.Contain("Prototype objective"));
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

        private static NodeId Resolve(
            VirtualFileSystem fileSystem,
            NodeId workingDirectory,
            string path,
            AccessContext access)
        {
            var parsed = VirtualPath.Parse(path);
            Assert.That(parsed.IsSuccess, Is.True, path);
            var resolved = fileSystem.Resolve(workingDirectory, parsed.Value, access);
            Assert.That(resolved.IsSuccess, Is.True, path);
            return resolved.Value;
        }

        private static IProgramRuntime CreateRuntime()
        {
            return new ProgramRuntimeRouter(
                new LuaProgramRuntime(),
                new BinaryProgramRuntime(
                    new BuiltinProgramRegistry(
                        new IBuiltinProgram[]
                        {
                            new NanoProgram(),
                            new SystemInfoProgram()
                        })));
        }

        private static void AssertSuccess(HOS.Application.Shell.CommandResult result)
        {
            Assert.That(result.IsSuccess, Is.True, result.StandardError);
        }
    }
}
