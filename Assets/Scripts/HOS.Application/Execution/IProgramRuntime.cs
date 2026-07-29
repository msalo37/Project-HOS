using System.Collections.Generic;
using HOS.Application.Shell;

namespace HOS.Application.Execution
{
    public interface IProgramRuntime
    {
        CommandResult Execute(
            ProgramExecutionContext context,
            string sourceCode,
            IReadOnlyList<string> arguments);
    }
}
