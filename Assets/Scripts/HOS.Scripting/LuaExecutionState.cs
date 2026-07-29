using System.Text;
using HOS.Application.Execution;
using MoonSharp.Interpreter;

namespace HOS.Scripting
{
    internal sealed class LuaExecutionState
    {
        private readonly int maximumOutputCharacters;
        private readonly StringBuilder standardOutput = new StringBuilder();
        private readonly StringBuilder standardError = new StringBuilder();

        public LuaExecutionState(
            ProgramExecutionContext context,
            int maximumOutputCharacters)
        {
            Context = context;
            this.maximumOutputCharacters = maximumOutputCharacters;
        }

        public ProgramExecutionContext Context { get; }
        public int? RequestedExitCode { get; private set; }
        public string StandardOutput => standardOutput.ToString();
        public string StandardError => standardError.ToString();

        public void WriteOutput(string value)
        {
            Append(standardOutput, value);
        }

        public void WriteError(string value)
        {
            Append(standardError, value);
        }

        public void RequestExit(int code)
        {
            RequestedExitCode = code;
        }

        private void Append(StringBuilder target, string value)
        {
            value = value ?? "nil";
            var required = standardOutput.Length + standardError.Length + value.Length + 1;
            if (required > maximumOutputCharacters)
                throw new ScriptRuntimeException("process output limit exceeded");

            target.Append(value);
            target.Append('\n');
        }
    }
}
