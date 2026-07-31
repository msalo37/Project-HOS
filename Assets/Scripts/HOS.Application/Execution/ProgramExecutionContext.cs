using System;
using HOS.Application.Shell;
using HOS.Domain.Machines;
using HOS.Domain.Processes;
using HOS.Domain.Identity;
using HOS.Application.Remote;

namespace HOS.Application.Execution
{
    public sealed class ProgramExecutionContext
    {
        public ProgramExecutionContext(
            GameWorld world,
            PlayerShellContext shell,
            Machine executionMachine,
            Machine targetMachine,
            UserId executionUserId,
            ProcessId processId,
            RemoteAccessService remoteAccess)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Shell = shell ?? throw new ArgumentNullException(nameof(shell));
            ExecutionMachine = executionMachine ?? throw new ArgumentNullException(nameof(executionMachine));
            TargetMachine = targetMachine ?? throw new ArgumentNullException(nameof(targetMachine));
            ExecutionUserId = executionUserId;
            ProcessId = processId;
            RemoteAccess = remoteAccess;
        }

        public GameWorld World { get; }
        public PlayerShellContext Shell { get; }
        public Machine ExecutionMachine { get; }
        public Machine TargetMachine { get; }
        public Machine Machine => TargetMachine;
        public UserId ExecutionUserId { get; }
        public ProcessId ProcessId { get; }
        public RemoteAccessService RemoteAccess { get; }
    }
}
