using HOS.Domain.Processes;
using MoonSharp.Interpreter;

namespace HOS.Scripting
{
    internal static class LuaProcessApi
    {
        public static Table Create(Script script, LuaExecutionState state)
        {
            var api = new Table(script);
            api["current"] = DynValue.NewCallback(
                (_, __) => DynValue.NewNumber(state.Context.ProcessId.Value),
                "hos.process.current");
            api["list"] = DynValue.NewCallback((_, __) => List(script, state), "hos.process.list");
            api["kill"] = DynValue.NewCallback((_, args) => Kill(script, state, args), "hos.process.kill");
            api["exit"] = DynValue.NewCallback((_, args) => Exit(state, args), "hos.process.exit");
            return api;
        }

        private static DynValue List(Script script, LuaExecutionState state)
        {
            var table = new Table(script);
            foreach (var process in state.Context.Machine.Processes.List())
            {
                var item = new Table(script);
                item["pid"] = DynValue.NewNumber(process.Id.Value);
                item["name"] = DynValue.NewString(process.Name);
                item["state"] = DynValue.NewString(process.State.ToString().ToLowerInvariant());
                item["owner"] = DynValue.NewString(process.Owner.ToString());
                table.Append(DynValue.NewTable(item));
            }

            return DynValue.NewTable(table);
        }

        private static DynValue Kill(
            Script script,
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var pid = arguments.AsInt(0, "hos.process.kill");
            var result = state.Context.Machine.Processes.Exit(
                new ProcessId(pid),
                -9,
                state.Context.Shell.CurrentUserId);
            return result.IsSuccess
                ? LuaApiHelpers.Success()
                : LuaApiHelpers.Failure(
                    script,
                    result.Error.ToString(),
                    "unable to terminate process",
                    (int)result.Error);
        }

        private static DynValue Exit(
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var code = arguments.Count == 0 ? 0 : arguments.AsInt(0, "hos.process.exit");
            state.RequestExit(code);
            return DynValue.NewNumber(code);
        }
    }
}
