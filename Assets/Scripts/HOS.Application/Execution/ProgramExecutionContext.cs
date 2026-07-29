using System;
using HOS.Application.Shell;
using HOS.Domain.Machines;
using HOS.Domain.Processes;

namespace HOS.Application.Execution
{
    public sealed class ProgramExecutionContext
    {
        public ProgramExecutionContext(
            GameWorld world,
            PlayerShellContext shell,
            Machine machine,
            ProcessId processId)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Shell = shell ?? throw new ArgumentNullException(nameof(shell));
            Machine = machine ?? throw new ArgumentNullException(nameof(machine));
            ProcessId = processId;
        }

        public GameWorld World { get; }
        public PlayerShellContext Shell { get; }
        public Machine Machine { get; }
        public ProcessId ProcessId { get; }
    }
}
