using System;
using System.Collections.Generic;
using HOS.Application.Execution;
using HOS.Application.Shell;
using MoonSharp.Interpreter;

namespace HOS.Scripting
{
    public sealed class LuaProgramRuntime : IProgramRuntime
    {
        private readonly LuaRuntimeOptions options;

        public LuaProgramRuntime(LuaRuntimeOptions options = null)
        {
            this.options = options ?? new LuaRuntimeOptions();
        }

        public CommandResult Execute(
            ProgramExecutionContext context,
            string sourceCode,
            IReadOnlyList<string> arguments)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var state = new LuaExecutionState(context, options.MaximumOutputCharacters);
            var script = new Script(CoreModules.Preset_HardSandbox);
            script.Options.DebugPrint = state.WriteOutput;
            InstallApi(script, state);

            try
            {
                var chunk = script.LoadString(sourceCode ?? string.Empty, null, "program.lua");
                RunWithBudget(script, chunk);

                var execute = script.Globals.Get("execute");
                if (execute.Type != DataType.Function)
                {
                    return CommandResult.Failure(
                        1,
                        "lua: program must define function execute(args)");
                }

                var argumentTable = new Table(script);
                if (arguments != null)
                {
                    foreach (var argument in arguments)
                        argumentTable.Append(DynValue.NewString(argument ?? string.Empty));
                }

                var result = RunWithBudget(
                    script,
                    execute,
                    DynValue.NewTable(argumentTable));
                var exitCode = state.RequestedExitCode ?? ToExitCode(result);
                return new CommandResult(
                    exitCode,
                    state.StandardOutput,
                    state.StandardError);
            }
            catch (SyntaxErrorException exception)
            {
                return CommandResult.Failure(1, "lua syntax error: " + exception.DecoratedMessage);
            }
            catch (ScriptRuntimeException exception)
            {
                return CommandResult.Failure(1, "lua runtime error: " + exception.DecoratedMessage);
            }
            catch (LuaInstructionLimitException)
            {
                return CommandResult.Failure(124, "lua: instruction limit exceeded");
            }
        }

        private void InstallApi(Script script, LuaExecutionState state)
        {
            var hos = new Table(script);
            hos["stdout"] = DynValue.NewCallback(
                (execution, args) =>
                {
                    state.WriteOutput(args.AsStringUsingMeta(execution, 0, "hos.stdout"));
                    return DynValue.Nil;
                },
                "hos.stdout");
            hos["stderr"] = DynValue.NewCallback(
                (execution, args) =>
                {
                    state.WriteError(args.AsStringUsingMeta(execution, 0, "hos.stderr"));
                    return DynValue.Nil;
                },
                "hos.stderr");
            hos["fs"] = DynValue.NewTable(LuaFileSystemApi.Create(script, state));
            hos["shell"] = DynValue.NewTable(LuaShellApi.Create(script, state));
            hos["process"] = DynValue.NewTable(LuaProcessApi.Create(script, state));
            hos["net"] = DynValue.NewTable(LuaNetworkApi.Create(script, state));
            var local = new Table(script);
            local["fs"] = DynValue.NewTable(LuaLocalFileSystemApi.Create(script, state));
            hos["local"] = DynValue.NewTable(local);
            script.Globals["hos"] = DynValue.NewTable(hos);
        }

        private DynValue RunWithBudget(
            Script script,
            DynValue function,
            params DynValue[] arguments)
        {
            var coroutineValue = script.CreateCoroutine(function);
            var coroutine = coroutineValue.Coroutine;
            coroutine.AutoYieldCounter = options.InstructionsPerSlice;
            long consumed = 0;
            var result = coroutine.Resume(arguments);

            while (coroutine.State == CoroutineState.ForceSuspended ||
                   coroutine.State == CoroutineState.Suspended)
            {
                consumed += options.InstructionsPerSlice;
                if (consumed >= options.MaximumInstructions)
                    throw new LuaInstructionLimitException();
                result = coroutine.Resume();
            }

            return result;
        }

        private static int ToExitCode(DynValue result)
        {
            if (result == null || result.IsNil() || result.Type == DataType.Void)
                return 0;
            if (result.Type != DataType.Number)
                throw new ScriptRuntimeException("execute(args) must return a numeric exit code");
            return (int)result.Number;
        }

        private sealed class LuaInstructionLimitException : Exception
        {
        }
    }
}
