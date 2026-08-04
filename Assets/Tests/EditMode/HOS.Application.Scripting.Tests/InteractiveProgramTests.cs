using HOS.Application.Execution;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using HOS.Domain.Processes;
using HOS.Scripting;
using NUnit.Framework;

namespace HOS.Application.Scripting.Tests
{
    public sealed class InteractiveProgramTests
    {
        [Test]
        public void NanoKeepsProcessRunningAndSavesExistingFile()
        {
            var world = CreateWorld(out var engine);
            var note = world.CreateFile(
                world.HomeId,
                "note.txt",
                "hello",
                world.PlayerOwnership,
                FilePermissions.DefaultFile);

            var started = engine.Execute("nano note.txt");

            Assert.That(started.IsSuccess, Is.True, started.StandardError);
            Assert.That(engine.HasForegroundProgram, Is.True);
            var processId = engine.ForegroundProcessId.Value;
            Assert.That(world.Machine.Processes.TryGet(processId, out var process), Is.True);
            Assert.That(process.State, Is.EqualTo(ProcessState.Running));
            Assert.That(engine.Execute("cat note.txt").StandardError, Does.Contain("foreground"));

            engine.SendForegroundInput(new ProgramInput(ProgramInputKey.End));
            engine.SendForegroundInput(ProgramInput.Text('!'));
            engine.SendForegroundInput(new ProgramInput(ProgramInputKey.O, control: true));
            var exited = engine.SendForegroundInput(
                new ProgramInput(ProgramInputKey.X, control: true));

            Assert.That(exited.HasExited, Is.True);
            Assert.That(exited.Result.IsSuccess, Is.True, exited.Result.StandardError);
            Assert.That(engine.HasForegroundProgram, Is.False);
            Assert.That(world.Machine.Processes.TryGet(processId, out process), Is.True);
            Assert.That(process.State, Is.EqualTo(ProcessState.Exited));
            Assert.That(
                world.Machine.FileSystem.ReadFile(note, world.RootAccess).Value.ReadUtf8(),
                Is.EqualTo("hello!"));
        }

        [Test]
        public void NanoCreatesNewFileOnSave()
        {
            var world = CreateWorld(out var engine);
            Assert.That(engine.Execute("nano created.txt").IsSuccess, Is.True);

            Type(engine, "first line");
            engine.SendForegroundInput(new ProgramInput(ProgramInputKey.Enter));
            Type(engine, "second line");
            engine.SendForegroundInput(new ProgramInput(ProgramInputKey.O, control: true));
            var exited = engine.SendForegroundInput(
                new ProgramInput(ProgramInputKey.X, control: true));

            Assert.That(exited.HasExited, Is.True);
            var id = Resolve(world, "created.txt");
            Assert.That(
                world.Machine.FileSystem.ReadFile(id, world.RootAccess).Value.ReadUtf8(),
                Is.EqualTo("first line\nsecond line"));
            Assert.That(world.Machine.FileSystem.Stat(id).Value.Ownership, Is.EqualTo(world.PlayerOwnership));
        }

        [Test]
        public void DirtyNanoRequiresSecondExitAndDoesNotSaveWithoutCommand()
        {
            var world = CreateWorld(out var engine);
            var note = world.CreateFile(
                world.HomeId,
                "note.txt",
                "original",
                world.PlayerOwnership,
                FilePermissions.DefaultFile);
            Assert.That(engine.Execute("nano note.txt").IsSuccess, Is.True);
            engine.SendForegroundInput(ProgramInput.Text('x'));

            var warning = engine.SendForegroundInput(
                new ProgramInput(ProgramInputKey.X, control: true));

            Assert.That(warning.HasExited, Is.False);
            Assert.That(engine.HasForegroundProgram, Is.True);
            Assert.That(engine.ForegroundFrame.Text, Does.Contain("Unsaved changes"));

            var discarded = engine.SendForegroundInput(
                new ProgramInput(ProgramInputKey.X, control: true));
            Assert.That(discarded.HasExited, Is.True);
            Assert.That(
                world.Machine.FileSystem.ReadFile(note, world.RootAccess).Value.ReadUtf8(),
                Is.EqualTo("original"));
        }

        [Test]
        public void InterruptExitsWithCode130WithoutSaving()
        {
            var world = CreateWorld(out var engine);
            var note = world.CreateFile(
                world.HomeId,
                "note.txt",
                "original",
                world.PlayerOwnership,
                FilePermissions.DefaultFile);
            Assert.That(engine.Execute("nano note.txt").IsSuccess, Is.True);
            var processId = engine.ForegroundProcessId.Value;
            engine.SendForegroundInput(ProgramInput.Text('x'));

            var interrupted = engine.SendForegroundSignal(ProgramSignal.Interrupt);

            Assert.That(interrupted.HasExited, Is.True);
            Assert.That(interrupted.Result.ExitCode, Is.EqualTo(130));
            Assert.That(engine.HasForegroundProgram, Is.False);
            Assert.That(world.Machine.Processes.TryGet(processId, out var process), Is.True);
            Assert.That(process.State, Is.EqualTo(ProcessState.Failed));
            Assert.That(
                world.Machine.FileSystem.ReadFile(note, world.RootAccess).Value.ReadUtf8(),
                Is.EqualTo("original"));
        }

        [Test]
        public void NanoReportsSaveFailureForReadOnlyFileAndRejectsHosBinary()
        {
            var world = CreateWorld(out var engine);
            var readOnly = new FilePermissions(
                PermissionBits.Read,
                PermissionBits.Read,
                PermissionBits.Read);
            var note = world.CreateFile(
                world.HomeId,
                "readonly.txt",
                "locked",
                world.PlayerOwnership,
                readOnly);
            world.CreateExecutable(
                world.HomeId,
                "program.hos",
                HosBinaryFormat.Create("nano"),
                world.PlayerOwnership);

            Assert.That(engine.Execute("nano readonly.txt").IsSuccess, Is.True);
            engine.SendForegroundInput(ProgramInput.Text('x'));
            var save = engine.SendForegroundInput(
                new ProgramInput(ProgramInputKey.O, control: true));

            Assert.That(save.HasExited, Is.False);
            Assert.That(engine.ForegroundFrame.Text, Does.Contain("Save failed: permission denied"));
            Assert.That(
                world.Machine.FileSystem.ReadFile(note, world.RootAccess).Value.ReadUtf8(),
                Is.EqualTo("locked"));
            engine.SendForegroundSignal(ProgramSignal.Interrupt);

            var binary = engine.Execute("nano program.hos");
            Assert.That(binary.ExitCode, Is.EqualTo(1));
            Assert.That(binary.StandardError, Does.Contain("binary file"));
        }

        private static TestWorldBuilder CreateWorld(out ShellEngine engine)
        {
            var world = new TestWorldBuilder();
            world.CreateExecutable(
                world.BinId,
                "nano.hos",
                HosBinaryFormat.Create(NanoProgram.ProgramId),
                world.RootOwnership);
            engine = world.CreateEngine(
                new ProgramRuntimeRouter(
                    new LuaProgramRuntime(),
                    new BinaryProgramRuntime(
                        new BuiltinProgramRegistry(
                            new IBuiltinProgram[] { new NanoProgram() }))));
            return world;
        }

        private static void Type(ShellEngine engine, string value)
        {
            foreach (var character in value)
                engine.SendForegroundInput(ProgramInput.Text(character));
        }

        private static NodeId Resolve(TestWorldBuilder world, string path)
        {
            return world.Machine.FileSystem.Resolve(
                world.Shell.WorkingDirectory,
                VirtualPath.Parse(path).Value,
                world.Shell.CreateAccessContext()).Value;
        }
    }
}
