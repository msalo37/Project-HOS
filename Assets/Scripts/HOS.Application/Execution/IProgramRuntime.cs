using System.Collections.Generic;
using HOS.Application.Shell;

namespace HOS.Application.Execution
{
    public interface IProgramRuntime
    {
        ProgramStartResult Start(
            ProgramExecutionContext context,
            ProgramImage image,
            IReadOnlyList<string> arguments);
    }
}
