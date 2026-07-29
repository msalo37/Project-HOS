using System.Collections.Generic;
using System.Text;
using HOS.Domain.Common;

namespace HOS.Application.Shell
{
    public enum CommandParseError
    {
        EmptyInput,
        UnterminatedSingleQuote,
        UnterminatedDoubleQuote,
        DanglingEscape
    }

    public sealed class CommandLineParser
    {
        public Result<CommandInvocation, CommandParseError> Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Result<CommandInvocation, CommandParseError>.Failure(
                    CommandParseError.EmptyInput);
            }

            var tokens = new List<string>();
            var token = new StringBuilder();
            var tokenStarted = false;
            var escaped = false;
            var quote = '\0';

            for (var i = 0; i < input.Length; i++)
            {
                var character = input[i];

                if (escaped)
                {
                    token.Append(character);
                    tokenStarted = true;
                    escaped = false;
                    continue;
                }

                if (character == '\\' && quote != '\'')
                {
                    escaped = true;
                    tokenStarted = true;
                    continue;
                }

                if (quote != '\0')
                {
                    if (character == quote)
                    {
                        quote = '\0';
                    }
                    else
                    {
                        token.Append(character);
                    }

                    tokenStarted = true;
                    continue;
                }

                if (character == '\'' || character == '"')
                {
                    quote = character;
                    tokenStarted = true;
                    continue;
                }

                if (char.IsWhiteSpace(character))
                {
                    FlushToken(tokens, token, ref tokenStarted);
                    continue;
                }

                token.Append(character);
                tokenStarted = true;
            }

            if (escaped)
            {
                return Result<CommandInvocation, CommandParseError>.Failure(
                    CommandParseError.DanglingEscape);
            }

            if (quote == '\'')
            {
                return Result<CommandInvocation, CommandParseError>.Failure(
                    CommandParseError.UnterminatedSingleQuote);
            }

            if (quote == '"')
            {
                return Result<CommandInvocation, CommandParseError>.Failure(
                    CommandParseError.UnterminatedDoubleQuote);
            }

            FlushToken(tokens, token, ref tokenStarted);
            if (tokens.Count == 0)
            {
                return Result<CommandInvocation, CommandParseError>.Failure(
                    CommandParseError.EmptyInput);
            }

            var arguments = new string[tokens.Count - 1];
            for (var i = 1; i < tokens.Count; i++)
                arguments[i - 1] = tokens[i];

            return Result<CommandInvocation, CommandParseError>.Success(
                new CommandInvocation(input, tokens[0], arguments));
        }

        private static void FlushToken(
            ICollection<string> tokens,
            StringBuilder token,
            ref bool tokenStarted)
        {
            if (!tokenStarted)
                return;

            tokens.Add(token.ToString());
            token.Clear();
            tokenStarted = false;
        }
    }
}
