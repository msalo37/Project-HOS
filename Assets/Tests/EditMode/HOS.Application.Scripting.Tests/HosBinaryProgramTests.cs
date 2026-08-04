using System.Collections.Generic;
using HOS.Application.Execution;
using HOS.Application.Shell;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Scripting;
using NUnit.Framework;

namespace HOS.Application.Scripting.Tests
{
    public sealed class HosBinaryProgramTests
    {
        [Test]
        public void FormatRoundTripsProgramIdentifier()
        {
            var content = HosBinaryFormat.Create("test-program");

            var parsed = HosBinaryFormat.TryParse(content, out var descriptor, out var error);

            Assert.That(parsed, Is.True, error);
            Assert.That(descriptor.ProgramId, Is.EqualTo("test-program"));
        }

        [Test]
        public void BinaryDoesNotExposeProgramIdentifierAndRejectsTampering()
        {
            var content = HosBinaryFormat.Create("secret-cracker");
            var bytes = content.CopyBytes();

            Assert.That(Contains(bytes, System.Text.Encoding.UTF8.GetBytes("secret-cracker")), Is.False);
            bytes[bytes.Length / 2] ^= 0x5a;

            var parsed = HosBinaryFormat.TryParse(
                new FileContent(bytes),
                out _,
                out var error);
            Assert.That(parsed, Is.False);
            Assert.That(error, Does.Contain("signature"));
        }

        [Test]
        public void BareCommandPrefersHosBinaryAndPassesArguments()
        {
            var world = new TestWorldBuilder();
            world.CreateExecutable(
                world.BinId,
                "inspect.hos",
                HosBinaryFormat.Create("inspect"),
                world.RootOwnership);
            world.CreateExecutable(
                world.BinId,
                "inspect.lua",
                "function execute(args) hos.stdout('lua') return 0 end",
                world.RootOwnership);
            var engine = world.CreateEngine(CreateRuntime(new InspectProgram()));

            var result = engine.Execute("inspect alpha beta");

            Assert.That(result.IsSuccess, Is.True, result.StandardError);
            Assert.That(result.StandardOutput, Is.EqualTo("binary:alpha,beta\n"));
        }

        [Test]
        public void ExplicitLuaExtensionStillSelectsLuaRuntime()
        {
            var world = new TestWorldBuilder();
            world.CreateExecutable(
                world.HomeId,
                "inspect.hos",
                HosBinaryFormat.Create("inspect"),
                world.PlayerOwnership);
            world.CreateExecutable(
                world.HomeId,
                "inspect.lua",
                "function execute(args) hos.stdout('lua') return 0 end",
                world.PlayerOwnership);
            var engine = world.CreateEngine(CreateRuntime(new InspectProgram()));

            var result = engine.Execute("./inspect.lua");

            Assert.That(result.IsSuccess, Is.True, result.StandardError);
            Assert.That(result.StandardOutput, Is.EqualTo("lua\n"));
        }

        [Test]
        public void InvalidAndUnavailableHosBinariesReturnExecutableError()
        {
            var world = new TestWorldBuilder();
            world.CreatePlayerExecutable("broken.hos", "this is not a binary");
            world.CreateExecutable(
                world.HomeId,
                "missing.hos",
                HosBinaryFormat.Create("not-installed"),
                world.PlayerOwnership);
            var engine = world.CreateEngine(CreateRuntime());

            var broken = engine.Execute("./broken.hos");
            var missing = engine.Execute("./missing.hos");

            Assert.That(broken.ExitCode, Is.EqualTo(126));
            Assert.That(broken.StandardError, Does.Contain("invalid HOS executable"));
            Assert.That(missing.ExitCode, Is.EqualTo(126));
            Assert.That(missing.StandardError, Does.Contain("not-installed"));
            Assert.That(missing.StandardError, Does.Contain("unavailable"));
        }

        [Test]
        public void CatShowsSafeOpaqueRepresentationForHosBinary()
        {
            var world = new TestWorldBuilder();
            world.InstallStandardCommand("cat");
            world.CreateExecutable(
                world.HomeId,
                "inspect.hos",
                HosBinaryFormat.Create("inspect"),
                world.PlayerOwnership);
            var engine = world.CreateEngine(CreateRuntime(new InspectProgram()));

            var result = engine.Execute("cat inspect.hos");

            Assert.That(result.IsSuccess, Is.True, result.StandardError);
            Assert.That(result.StandardOutput, Is.Not.Empty);
            Assert.That(result.StandardOutput, Does.Not.Contain("inspect"));
            foreach (var character in result.StandardOutput)
                Assert.That("�▓▒░\n".IndexOf(character), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void CpAndMvPreserveSignedBinaryAndExecution()
        {
            var world = new TestWorldBuilder();
            world.InstallStandardCommands("cp", "mv");
            var originalId = world.CreateExecutable(
                world.HomeId,
                "inspect.hos",
                HosBinaryFormat.Create("inspect"),
                world.PlayerOwnership);
            var engine = world.CreateEngine(CreateRuntime(new InspectProgram()));

            var copied = engine.Execute("cp inspect.hos copied.hos");
            Assert.That(copied.IsSuccess, Is.True, copied.StandardError);
            var copiedId = Resolve(world, "copied.hos");
            Assert.That(copiedId, Is.Not.EqualTo(originalId));
            Assert.That(
                world.Machine.FileSystem.ReadFile(copiedId, world.RootAccess).Value.CopyBytes(),
                Is.EqualTo(
                    world.Machine.FileSystem.ReadFile(originalId, world.RootAccess).Value.CopyBytes()));

            var moved = engine.Execute("mv copied.hos renamed.hos");
            Assert.That(moved.IsSuccess, Is.True, moved.StandardError);
            Assert.That(world.Machine.FileSystem.Stat(copiedId).Value.Name, Is.EqualTo("renamed.hos"));

            var executed = engine.Execute("./renamed.hos value");
            Assert.That(executed.IsSuccess, Is.True, executed.StandardError);
            Assert.That(executed.StandardOutput, Is.EqualTo("binary:value\n"));
        }

        [Test]
        public void PlayerCannotForgeBinaryFromReadableManifestText()
        {
            var world = new TestWorldBuilder();
            world.InstallStandardCommand("cat");
            world.CreatePlayerExecutable(
                "forged.hos",
                "HOSBIN/1\nprogram=inspect\n");
            var engine = world.CreateEngine(CreateRuntime(new InspectProgram()));

            var displayed = engine.Execute("cat forged.hos");
            var result = engine.Execute("./forged.hos");

            Assert.That(displayed.IsSuccess, Is.True, displayed.StandardError);
            Assert.That(displayed.StandardOutput, Does.Not.Contain("program=inspect"));
            Assert.That(result.ExitCode, Is.EqualTo(126));
            Assert.That(result.StandardError, Does.Contain("invalid HOS executable"));
        }

        private static NodeId Resolve(TestWorldBuilder world, string path)
        {
            return world.Machine.FileSystem.Resolve(
                world.Shell.WorkingDirectory,
                VirtualPath.Parse(path).Value,
                world.Shell.CreateAccessContext()).Value;
        }

        private static bool Contains(byte[] source, byte[] value)
        {
            if (value.Length == 0 || value.Length > source.Length)
                return false;

            for (var offset = 0; offset <= source.Length - value.Length; offset++)
            {
                var matches = true;
                for (var index = 0; index < value.Length; index++)
                {
                    if (source[offset + index] == value[index])
                        continue;
                    matches = false;
                    break;
                }

                if (matches)
                    return true;
            }

            return false;
        }

        private static IProgramRuntime CreateRuntime(params IBuiltinProgram[] programs)
        {
            return new ProgramRuntimeRouter(
                new LuaProgramRuntime(),
                new BinaryProgramRuntime(new BuiltinProgramRegistry(programs)));
        }

        private sealed class InspectProgram : IBuiltinProgram
        {
            public string Id => "inspect";

            public ProgramStartResult Start(
                ProgramExecutionContext context,
                IReadOnlyList<string> arguments)
            {
                return ProgramStartResult.Completed(
                    CommandResult.Success("binary:" + string.Join(",", arguments) + "\n"));
            }
        }
    }
}
