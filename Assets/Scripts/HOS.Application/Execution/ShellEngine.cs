using System;
using HOS.Application.Shell;
using HOS.Domain.FileSystem;
using HOS.Application.Remote;
using HOS.Domain.Identity;
using HOS.Domain.Machines;
using HOS.Domain.Processes;

namespace HOS.Application.Execution
{
    public sealed class ShellEngine
    {
        private readonly CommandLineParser parser;
        private readonly ExecutableResolver resolver;
        private readonly IProgramRuntime runtime;
        private readonly PlayerShellContext shell;
        private readonly RemoteAccessService remoteAccess;
        private ForegroundExecution foreground;

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
        public bool HasForegroundProgram => foreground != null;
        public TerminalFrame ForegroundFrame => foreground?.Session.CurrentFrame;
        public ProcessId? ForegroundProcessId => foreground?.ProcessId;

        public CommandResult Execute(string input)
        {
            if (HasForegroundProgram)
                return CommandResult.Failure(1, "shell: foreground program is active");
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

            var processStarted = executionMachine.StartProcess(
                executable.Value.NodeId,
                invocation.Name,
                executionAccess);
            if (processStarted.IsFailure)
                return CommandResult.Failure(126, $"{invocation.Name}: cannot start process");

            ProgramStartResult startResult;
            try
            {
                startResult = runtime.Start(
                    new ProgramExecutionContext(
                        shell.World,
                        shell,
                        executionMachine,
                        shell.CurrentMachine,
                        executable.Value.UserId,
                        processStarted.Value,
                        remoteAccess),
                    new ProgramImage(executable.Value.Path, source.Value),
                    invocation.Arguments);
            }
            catch (Exception exception)
            {
                startResult = ProgramStartResult.Completed(
                    CommandResult.Failure(
                        1,
                        $"{invocation.Name}: runtime failure: {exception.Message}"));
            }

            if (startResult == null)
            {
                startResult = ProgramStartResult.Completed(
                    CommandResult.Failure(1, $"{invocation.Name}: runtime returned no result"));
            }

            if (startResult.IsRunning)
            {
                foreground = new ForegroundExecution(
                    startResult.Session,
                    executionMachine,
                    executable.Value.UserId,
                    processStarted.Value,
                    executionAccess.IsKernel);
                return CommandResult.Success();
            }

            var result = startResult.Result ??
                         CommandResult.Failure(1, $"{invocation.Name}: runtime returned no result");
            executionMachine.Processes.Exit(
                processStarted.Value,
                result.ExitCode,
                executable.Value.UserId,
                executionAccess.IsKernel);
            return result;
        }

        public ProgramSessionUpdate SendForegroundInput(ProgramInput input)
        {
            if (foreground == null)
            {
                return ProgramSessionUpdate.Exited(
                    CommandResult.Failure(1, "shell: no foreground program"));
            }

            try
            {
                return ApplyForegroundUpdate(foreground.Session.HandleInput(input));
            }
            catch (Exception exception)
            {
                return ApplyForegroundUpdate(
                    ProgramSessionUpdate.Exited(
                        CommandResult.Failure(
                            1,
                            $"foreground program failure: {exception.Message}")));
            }
        }

        public ProgramSessionUpdate SendForegroundSignal(ProgramSignal signal)
        {
            if (foreground == null)
            {
                return ProgramSessionUpdate.Exited(
                    CommandResult.Failure(1, "shell: no foreground program"));
            }

            try
            {
                return ApplyForegroundUpdate(foreground.Session.HandleSignal(signal));
            }
            catch (Exception exception)
            {
                return ApplyForegroundUpdate(
                    ProgramSessionUpdate.Exited(
                        CommandResult.Failure(
                            1,
                            $"foreground program failure: {exception.Message}")));
            }
        }

        private ProgramSessionUpdate ApplyForegroundUpdate(ProgramSessionUpdate update)
        {
            if (update == null)
            {
                update = ProgramSessionUpdate.Exited(
                    CommandResult.Failure(1, "foreground program returned no update"));
            }
            if (!update.HasExited)
                return update;

            var completed = foreground;
            foreground = null;
            completed.Machine.Processes.Exit(
                completed.ProcessId,
                update.Result.ExitCode,
                completed.UserId,
                completed.IsKernel);
            return update;
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

        private sealed class ForegroundExecution
        {
            public ForegroundExecution(
                IProgramSession session,
                Machine machine,
                UserId userId,
                ProcessId processId,
                bool isKernel)
            {
                Session = session;
                Machine = machine;
                UserId = userId;
                ProcessId = processId;
                IsKernel = isKernel;
            }

            public IProgramSession Session { get; }
            public Machine Machine { get; }
            public UserId UserId { get; }
            public ProcessId ProcessId { get; }
            public bool IsKernel { get; }
        }
    }
}
