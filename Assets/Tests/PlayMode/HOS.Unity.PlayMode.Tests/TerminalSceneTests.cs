using System.Collections;
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
        public IEnumerator SampleSceneBootstrapsTerminalAndExecutesLuaCommands()
        {
            var load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null;

            var terminal = Object.FindFirstObjectByType<TerminalView>();
            Assert.That(terminal, Is.Not.Null);
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
        }

        private static void AssertSuccess(HOS.Application.Shell.CommandResult result)
        {
            Assert.That(result.IsSuccess, Is.True, result.StandardError);
        }
    }
}
