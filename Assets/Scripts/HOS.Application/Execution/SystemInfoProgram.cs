using System.Collections.Generic;
using System.Text;
using HOS.Application.Shell;

namespace HOS.Application.Execution
{
    public sealed class SystemInfoProgram : IBuiltinProgram
    {
        public const string ProgramId = "sysinfo";

        public string Id => ProgramId;

        public ProgramStartResult Start(
            ProgramExecutionContext context,
            IReadOnlyList<string> arguments)
        {
            var machine = context.TargetMachine;
            var userName = machine.Users.TryGetUser(context.Shell.CurrentUserId, out var user)
                ? user.Name
                : "unknown";
            var output = new StringBuilder()
                .Append("hostname: ").Append(machine.Hostname).Append('\n')
                .Append("address: ").Append(machine.Address).Append('\n')
                .Append("user: ").Append(userName).Append('\n')
                .ToString();
            return ProgramStartResult.Completed(CommandResult.Success(output));
        }
    }
}
