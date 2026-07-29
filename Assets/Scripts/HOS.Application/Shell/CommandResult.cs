namespace HOS.Application.Shell
{
    public sealed class CommandResult
    {
        public CommandResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput ?? string.Empty;
            StandardError = standardError ?? string.Empty;
        }

        public int ExitCode { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }
        public bool IsSuccess => ExitCode == 0;

        public static CommandResult Success(string output = "")
        {
            return new CommandResult(0, output, string.Empty);
        }

        public static CommandResult Failure(int exitCode, string error)
        {
            return new CommandResult(exitCode, string.Empty, error);
        }
    }
}
