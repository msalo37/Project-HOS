using HOS.Domain.FileSystem;
using MoonSharp.Interpreter;

namespace HOS.Scripting
{
    internal static class LuaLocalFileSystemApi
    {
        public static Table Create(Script script, LuaExecutionState state)
        {
            var api = new Table(script);
            api["read"] = DynValue.NewCallback((_, args) =>
            {
                if (!TryResolve(state, LuaApiHelpers.RequiredString(args, 0, "hos.local.fs.read"), out var id, out var error))
                    return LuaApiHelpers.Failure(script, error);
                var content = state.Context.Shell.LocalMachine.FileSystem.ReadFile(
                    id, state.Context.Shell.LocalMachine.Users.CreateAccessContext(state.Context.Shell.LocalUserId));
                return content.IsSuccess
                    ? LuaApiHelpers.Success(DynValue.NewString(content.Value.ReadUtf8()))
                    : LuaApiHelpers.Failure(script, content.Error);
            }, "hos.local.fs.read");
            api["exists"] = DynValue.NewCallback((execution, args) =>
            {
                var exists = TryResolve(
                    state,
                    LuaApiHelpers.RequiredString(args, 0, "hos.local.fs.exists"),
                    out var ignoredId,
                    out var ignoredError);
                return DynValue.NewBoolean(exists);
            }, "hos.local.fs.exists");
            return api;
        }

        private static bool TryResolve(
            LuaExecutionState state, string value, out NodeId id, out FileSystemError error)
        {
            id = default;
            var path = VirtualPath.Parse(value);
            if (path.IsFailure) { error = FileSystemError.InvalidName; return false; }
            var shell = state.Context.Shell;
            var result = shell.LocalMachine.FileSystem.Resolve(
                shell.LocalWorkingDirectory, path.Value,
                shell.LocalMachine.Users.CreateAccessContext(shell.LocalUserId));
            if (result.IsFailure) { error = result.Error; return false; }
            id = result.Value;
            error = default;
            return true;
        }
    }
}
