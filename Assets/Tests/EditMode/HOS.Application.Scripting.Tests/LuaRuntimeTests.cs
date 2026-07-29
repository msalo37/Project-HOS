using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Scripting;
using NUnit.Framework;

namespace HOS.Application.Scripting.Tests
{
    public sealed class LuaRuntimeTests
    {
        [Test]
        public void RunsPlayerProgramThroughSameFileSystemApi()
        {
            var world = new TestWorldBuilder();
            world.CreatePlayerExecutable(
                "create-result.lua",
                "function execute(args)\n" +
                "  local ok, err = hos.fs.write('result.txt', args[1])\n" +
                "  if not ok then hos.stderr(err.message) return 1 end\n" +
                "  return 0\n" +
                "end");

            var result = world.Engine.Execute("./create-result hello");

            Assert.That(result.IsSuccess, Is.True, result.StandardError);
            var resolved = world.Machine.FileSystem.Resolve(
                world.HomeId,
                VirtualPath.Parse("result.txt").Value,
                world.Shell.CreateAccessContext());
            Assert.That(resolved.IsSuccess, Is.True);
            Assert.That(
                world.Machine.FileSystem.ReadFile(
                    resolved.Value,
                    world.Shell.CreateAccessContext()).Value.ReadUtf8(),
                Is.EqualTo("hello"));
        }

        [Test]
        public void GlobalsDoNotLeakBetweenProcesses()
        {
            var world = new TestWorldBuilder();
            world.CreatePlayerExecutable(
                "setter.lua",
                "leaked_value = 'secret'\nfunction execute(args) return 0 end");
            world.CreatePlayerExecutable(
                "reader.lua",
                "function execute(args)\n" +
                "  if leaked_value ~= nil then hos.stderr('leaked') return 1 end\n" +
                "  return 0\n" +
                "end");

            Assert.That(world.Engine.Execute("./setter").IsSuccess, Is.True);
            var reader = world.Engine.Execute("./reader");
            Assert.That(reader.IsSuccess, Is.True, reader.StandardError);
        }

        [Test]
        public void RealIoAndOsModulesAreUnavailable()
        {
            var world = new TestWorldBuilder();
            world.CreatePlayerExecutable(
                "sandbox.lua",
                "function execute(args)\n" +
                "  if io ~= nil or os ~= nil or debug ~= nil or require ~= nil then return 1 end\n" +
                "  return 0\n" +
                "end");

            var result = world.Engine.Execute("./sandbox");

            Assert.That(result.IsSuccess, Is.True, result.StandardError);
        }

        [Test]
        public void InfiniteLoopIsStoppedByInstructionBudget()
        {
            var options = new LuaRuntimeOptions(1000, 5000, 1024);
            var world = new TestWorldBuilder(options);
            world.CreatePlayerExecutable(
                "loop.lua",
                "function execute(args) while true do end end");

            var result = world.Engine.Execute("./loop");

            Assert.That(result.ExitCode, Is.EqualTo(124));
            Assert.That(result.StandardError, Does.Contain("instruction limit"));
        }

        [Test]
        public void TopLevelInfiniteLoopIsAlsoStopped()
        {
            var options = new LuaRuntimeOptions(1000, 5000, 1024);
            var world = new TestWorldBuilder(options);
            world.CreatePlayerExecutable("top-loop.lua", "while true do end");

            var result = world.Engine.Execute("./top-loop");

            Assert.That(result.ExitCode, Is.EqualTo(124));
        }

        [Test]
        public void MissingExecuteFunctionReturnsReadableError()
        {
            var world = new TestWorldBuilder();
            world.CreatePlayerExecutable("invalid.lua", "value = 42");

            var result = world.Engine.Execute("./invalid");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StandardError, Does.Contain("execute"));
        }

        [Test]
        public void OutputLimitStopsNoisyProgram()
        {
            var world = new TestWorldBuilder(new LuaRuntimeOptions(1000, 100000, 32));
            world.CreatePlayerExecutable(
                "noisy.lua",
                "function execute(args)\n" +
                "  for i = 1, 100 do hos.stdout('abcdefghij') end\n" +
                "  return 0\n" +
                "end");

            var result = world.Engine.Execute("./noisy");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StandardError, Does.Contain("output limit"));
        }

        [Test]
        public void FilePermissionsAreEnforcedInsideLuaApi()
        {
            var world = new TestWorldBuilder();
            world.InstallStandardCommand("cat");
            var privatePermissions = new FilePermissions(
                PermissionBits.Read | PermissionBits.Write,
                PermissionBits.None,
                PermissionBits.None);
            world.CreateFile(
                world.Machine.FileSystem.RootId,
                "secret",
                "classified",
                world.RootOwnership,
                privatePermissions);

            var result = world.Engine.Execute("cat /secret");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.StandardError, Does.Contain("permission denied"));
        }
    }
}
