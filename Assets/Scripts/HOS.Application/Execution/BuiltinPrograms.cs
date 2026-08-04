using System;
using System.Collections.Generic;
using HOS.Application.Shell;

namespace HOS.Application.Execution
{
    public interface IBuiltinProgram
    {
        string Id { get; }

        ProgramStartResult Start(
            ProgramExecutionContext context,
            IReadOnlyList<string> arguments);
    }

    public sealed class BuiltinProgramRegistry
    {
        private readonly Dictionary<string, IBuiltinProgram> programs =
            new Dictionary<string, IBuiltinProgram>(StringComparer.Ordinal);

        public BuiltinProgramRegistry(IEnumerable<IBuiltinProgram> programs = null)
        {
            if (programs == null)
                return;

            foreach (var program in programs)
                Register(program);
        }

        public void Register(IBuiltinProgram program)
        {
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            if (!HosBinaryFormat.IsValidProgramId(program.Id))
                throw new ArgumentException("Builtin program id is invalid.", nameof(program));
            if (programs.ContainsKey(program.Id))
                throw new InvalidOperationException($"Builtin program '{program.Id}' is already registered.");

            programs.Add(program.Id, program);
        }

        public bool TryGet(string id, out IBuiltinProgram program) =>
            programs.TryGetValue(id, out program);
    }
}
