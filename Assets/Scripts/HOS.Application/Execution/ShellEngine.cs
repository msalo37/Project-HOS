using System;
using HOS.Application.Shell;
using HOS.Domain.FileSystem;
using HOS.Application.Remote;

namespace HOS.Application.Execution
{
    public sealed class ShellEngine
    {
        private readonly CommandLineParser parser;
        private readonly ExecutableResolver resolver;
        private readonly IProgramRuntime runtime;
        private readonly PlayerShellContext shell;
        private readonly RemoteAccessService remoteAccess;

        public ShellEngine(
            PlayerShellContext shell,
            IProgramRuntime runtime,
            CommandLineParser parser = null,
            ExecutableResolver resolver = null,
            RemoteAccessService remoteAccess = null)
        {
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.parser = parser ?? new CommandLineParser();
            this.resolver = resolver ?? new ExecutableResolver();
            this.remoteAccess = remoteAccess ?? new RemoteAccessService(shell);
        }

        public bool IsAwaitingInput => remoteAccess.HasPendingLogin;
        public bool IsSecretInput => remoteAccess.IsSecretInput;
        public string InteractionPrompt => remoteAccess.InteractionPrompt;

        public CommandResult Execute(string input)
        {
            if (remoteAccess.HasPendingLogin)
                return remoteAccess.SubmitInteraction(input);
            var parsed = parser.Parse(input);
            if (parsed.IsFailure)
                return CommandResult.Failure(2, $"shell: {FormatParseError(parsed.Error)}");

            var invocation = parsed.Value;
            var executable = resolver.Resolve(invocation.Name, shell);
            if (executable.IsFailure)
                return FormatResolutionFailure(invocation.Name, executable.Error);

            if (!shell.World.TryGetMachine(executable.Value.MachineId, out var executionMachine))
                return CommandResult.Failure(126, $"{invocation.Name}: execution machine unavailable");
            var executionAccess = executionMachine.Users.CreateAccessContext(executable.Value.UserId);
            var source = executionMachine.FileSystem.ReadFile(executable.Value.NodeId, executionAccess);
            if (source.IsFailure)
            {
                return CommandResult.Failure(
                    126,
                    $"{invocation.Name}: {FormatFileSystemError(source.Error)}");
            }

            var started = executionMachine.StartProcess(
                executable.Value.NodeId,
                invocation.Name,
                executionAccess);
            if (started.IsFailure)
                return CommandResult.Failure(126, $"{invocation.Name}: cannot start process");

            CommandResult result;
            try
            {
                result = runtime.Execute(
                    new ProgramExecutionContext(
                        shell.World,
                        shell,
                        executionMachine,
                        shell.CurrentMachine,
                        executable.Value.UserId,
                        started.Value,
                        remoteAccess),
                    source.Value.ReadUtf8(),
                    invocation.Arguments);
            }
            catch (Exception exception)
            {
                result = CommandResult.Failure(
                    1,
                    $"{invocation.Name}: runtime failure: {exception.Message}");
            }

            executionMachine.Processes.Exit(
                started.Value,
                result.ExitCode,
                executable.Value.UserId,
                executionAccess.IsKernel);
            return result;
        }

        private static CommandResult FormatResolutionFailure(
            string command,
            ExecutableResolutionError error)
        {
            switch (error)
            {
                case ExecutableResolutionError.AccessDenied:
                    return CommandResult.Failure(126, $"{command}: permission denied");
                case ExecutableResolutionError.NotExecutable:
                    return CommandResult.Failure(126, $"{command}: not executable");
                default:
                    return CommandResult.Failure(127, $"{command}: command not found");
            }
        }

        private static string FormatParseError(CommandParseError error)
        {
            switch (error)
            {
                case CommandParseError.UnterminatedSingleQuote:
                case CommandParseError.UnterminatedDoubleQuote:
                    return "unterminated quote";
                case CommandParseError.DanglingEscape:
                    return "dangling escape";
                default:
                    return "empty command";
            }
        }

        public static string FormatFileSystemError(FileSystemError error)
        {
            switch (error)
            {
                case FileSystemError.AccessDenied:
                    return "permission denied";
                case FileSystemError.NotADirectory:
                    return "not a directory";
                case FileSystemError.NotAFile:
                    return "not a file";
                case FileSystemError.NameAlreadyExists:
                    return "already exists";
                case FileSystemError.DirectoryNotEmpty:
                    return "directory not empty";
                case FileSystemError.InvalidName:
                    return "invalid name";
                default:
                    return "file or directory not found";
            }
        }
    }
}
