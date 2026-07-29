using HOS.Application.Shell;
using NUnit.Framework;

namespace HOS.Application.Scripting.Tests
{
    public sealed class CommandLineParserTests
    {
        private readonly CommandLineParser parser = new CommandLineParser();

        [Test]
        public void ParsesQuotedAndEscapedArguments()
        {
            var result = parser.Parse("write file.txt \"hello world\" escaped\\ value ''");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Name, Is.EqualTo("write"));
            Assert.That(
                result.Value.Arguments,
                Is.EqualTo(new[] { "file.txt", "hello world", "escaped value", "" }));
        }

        [Test]
        public void RejectsUnterminatedQuote()
        {
            var result = parser.Parse("cat \"unfinished");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(CommandParseError.UnterminatedDoubleQuote));
        }

        [Test]
        public void RejectsDanglingEscape()
        {
            var result = parser.Parse("cat file\\");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(CommandParseError.DanglingEscape));
        }
    }
}
