using System.Collections;
using HOS.Application.Execution;
using HOS.Unity.Terminal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HOS.Unity.PlayMode.Tests
{
    public sealed class TerminalSceneTests
    {
        [UnityTest]
        public IEnumerator SampleSceneBootstrapsTerminalAndExecutesLuaAndHosPrograms()
        {
            var load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null;

            var terminal = Object.FindFirstObjectByType<TerminalView>();
            Assert.That(terminal, Is.Not.Null);
            var controller = Object.FindFirstObjectByType<TerminalController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(terminal.IsInitialized, Is.True);
            Assert.That(terminal.BuildPrompt(), Is.EqualTo("player@player-pc:/home/player$"));

            AssertSuccess(terminal.SubmitCommand("mkdir smoke"));
            AssertSuccess(terminal.SubmitCommand("cd smoke"));
            AssertSuccess(terminal.SubmitCommand("write result.txt \"play mode works\""));
            var cat = terminal.SubmitCommand("cat result.txt");

            AssertSuccess(cat);
            Assert.That(cat.StandardOutput, Is.EqualTo("play mode works\n"));
            Assert.That(terminal.RenderedText, Does.Contain("play mode works"));
            Assert.That(terminal.BuildPrompt(), Is.EqualTo("player@player-pc:/home/player/smoke$"));

            var sysinfo = terminal.SubmitCommand("sysinfo");
            AssertSuccess(sysinfo);
            Assert.That(sysinfo.StandardOutput, Does.Contain("hostname: player-pc\n"));
            Assert.That(sysinfo.StandardOutput, Does.Contain("user: player\n"));

            AssertSuccess(terminal.SubmitCommand("cp /bin/sysinfo.hos copied.hos"));
            var binaryDisplay = terminal.SubmitCommand("cat copied.hos");
            AssertSuccess(binaryDisplay);
            Assert.That(binaryDisplay.StandardOutput, Is.Not.Empty);
            Assert.That(binaryDisplay.StandardOutput, Does.Not.Contain("sysinfo"));
            var copiedBinary = terminal.SubmitCommand("./copied.hos");
            AssertSuccess(copiedBinary);
            Assert.That(copiedBinary.StandardOutput, Does.Contain("hostname: player-pc\n"));

            AssertSuccess(terminal.SubmitCommand("nano editor.txt"));
            Assert.That(controller.InputMode, Is.EqualTo(TerminalInputMode.FullScreenApplication));
            foreach (var character in "edited in nano")
                controller.SendProgramInput(ProgramInput.Text(character));
            controller.SendProgramInput(new ProgramInput(ProgramInputKey.O, control: true));
            var nanoExit = controller.SendProgramInput(
                new ProgramInput(ProgramInputKey.X, control: true));
            Assert.That(nanoExit.HasExited, Is.True);
            Assert.That(controller.InputMode, Is.EqualTo(TerminalInputMode.ShellLineEditor));
            var edited = terminal.SubmitCommand("cat editor.txt");
            AssertSuccess(edited);
            Assert.That(edited.StandardOutput, Is.EqualTo("edited in nano\n"));

            AssertSuccess(terminal.SubmitCommand("ssh 10.0.0.2 22"));
            Assert.That(terminal.BuildPrompt(), Is.EqualTo("login:"));
            AssertSuccess(terminal.SubmitCommand("guest"));
            Assert.That(terminal.BuildPrompt(), Is.EqualTo("password:"));
            AssertSuccess(terminal.SubmitCommand("guest"));
            Assert.That(terminal.BuildPrompt(), Is.EqualTo("guest@dev-server:/home/guest$"));
            var remote = terminal.SubmitCommand("./test.lua");
            AssertSuccess(remote);
            Assert.That(remote.StandardOutput, Does.Contain("dev-server"));

            AssertSuccess(terminal.SubmitCommand("exit"));
            const string exploit =
                "function execute(args) " +
                "local c=hos.net.connect('10.0.0.2',8080); " +
                "local q=hos.net.request(c,{operation='challenge'}); " +
                "local s=0; for i=1,#q.nonce do s=(s+string.byte(q.nonce,i)*i)%65535 end; " +
                "local a=hos.net.request(c,{operation='authenticate',proof=tostring(s)}); " +
                "local j=hos.net.request(c,{operation='execute_job',token=a.token," +
                "payload=[[function execute(args) hos.stdout('play payload') return 0 end]]}); " +
                "if not j.success then hos.stderr(j.message) return 1 end return 0 end";
            AssertSuccess(terminal.SubmitCommand(
                "write devsync-play.lua \"" + exploit + "\""));
            AssertSuccess(terminal.SubmitCommand("chmod 700 devsync-play.lua"));
            AssertSuccess(terminal.SubmitCommand("./devsync-play.lua"));
            Assert.That(terminal.BuildPrompt(), Is.EqualTo("deploy@dev-server:/home/deploy$"));
            var payload = terminal.SubmitCommand("./last_job.lua");
            AssertSuccess(payload);
            Assert.That(payload.StandardOutput, Is.EqualTo("play payload\n"));
        }

        private static void AssertSuccess(HOS.Application.Shell.CommandResult result)
        {
            Assert.That(result.IsSuccess, Is.True, result.StandardError);
        }
    }
}
