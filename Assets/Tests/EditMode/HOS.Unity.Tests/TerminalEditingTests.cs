using HOS.Unity.Bootstrap;
using HOS.Unity.Terminal;
using NUnit.Framework;

namespace HOS.Unity.Tests
{
    public sealed class TerminalEditingTests
    {
        [Test]
        public void BufferEditsAtCaretAndMovesAcrossLines()
        {
            var buffer = new TerminalTextBuffer();
            buffer.SetText("one\nlonger\nx", 6);

            Assert.That(buffer.MoveUp(), Is.True);
            Assert.That(buffer.CaretIndex, Is.EqualTo(2));
            Assert.That(buffer.MoveDown(), Is.True);
            Assert.That(buffer.CaretIndex, Is.EqualTo(6));
            Assert.That(buffer.MoveDown(), Is.True);
            Assert.That(buffer.CaretIndex, Is.EqualTo(12));

            buffer.Insert('!');
            buffer.Backspace();
            buffer.Delete();
            Assert.That(buffer.Text, Is.EqualTo("one\nlonger\nx"));
        }

        [Test]
        public void HistoryPreservesDraftWhenNavigating()
        {
            var history = new TerminalCommandHistory();
            history.Add("pwd");
            history.Add("ls");

            Assert.That(history.TryPrevious("draft", out var latest), Is.True);
            Assert.That(latest, Is.EqualTo("ls"));
            Assert.That(history.TryPrevious(latest, out var previous), Is.True);
            Assert.That(previous, Is.EqualTo("pwd"));
            Assert.That(history.TryNext(out var next), Is.True);
            Assert.That(next, Is.EqualTo("ls"));
            Assert.That(history.TryNext(out var draft), Is.True);
            Assert.That(draft, Is.EqualTo("draft"));
        }

        [Test]
        public void CompletionUsesPathAndCurrentVirtualDirectory()
        {
            var game = new DefaultGameBootstrap().Create();
            var completion = new TerminalCompletionService(game.Shell);

            var command = completion.Complete("who", 3);
            Assert.That(command.Candidates, Does.Contain("whoami"));
            Assert.That(command.Replacement, Is.EqualTo("whoami "));

            var file = completion.Complete("cat rea", 7);
            Assert.That(file.Candidates, Does.Contain("readme.txt"));
            Assert.That(file.Replacement, Is.EqualTo("readme.txt "));

            var directory = completion.Complete("cd d", 4);
            Assert.That(directory.Candidates, Does.Contain("docs"));
            Assert.That(directory.Replacement, Is.EqualTo("docs/"));
        }
    }
}
