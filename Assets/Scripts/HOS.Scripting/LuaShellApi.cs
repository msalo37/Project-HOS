using System;
using HOS.Domain.FileSystem;
using MoonSharp.Interpreter;

namespace HOS.Scripting
{
    internal static class LuaShellApi
    {
        public static Table Create(Script script, LuaExecutionState state)
        {
            var api = new Table(script);
            api["cwd"] = DynValue.NewCallback((_, __) => Cwd(script, state), "hos.shell.cwd");
            api["chdir"] = DynValue.NewCallback((_, args) => Chdir(script, state, args), "hos.shell.chdir");
            api["getenv"] = DynValue.NewCallback((_, args) => GetEnvironment(state, args), "hos.shell.getenv");
            api["setenv"] = DynValue.NewCallback((_, args) => SetEnvironment(state, args), "hos.shell.setenv");
            api["hostname"] = DynValue.NewCallback(
                (_, __) => DynValue.NewString(state.Context.Machine.Hostname),
                "hos.shell.hostname");
            api["user"] = DynValue.NewCallback((_, __) => CurrentUser(state), "hos.shell.user");
            api["disconnect"] = DynValue.NewCallback((_, __) =>
                DynValue.NewBoolean(state.Context.Shell.ExitRemote()), "hos.shell.disconnect");
            return api;
        }

        private static DynValue Cwd(Script script, LuaExecutionState state)
        {
            var path = state.Context.Machine.FileSystem.GetPath(
                state.Context.Shell.WorkingDirectory);
            return path.IsSuccess
                ? DynValue.NewString(path.Value)
                : LuaApiHelpers.Failure(script, path.Error);
        }

        private static DynValue Chdir(
            Script script,
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var pathValue = LuaApiHelpers.RequiredString(arguments, 0, "hos.shell.chdir");
            var path = VirtualPath.Parse(pathValue);
            if (path.IsFailure)
                return LuaApiHelpers.Failure(script, FileSystemError.InvalidName);

            var changed = state.Context.Shell.ChangeDirectory(path.Value);
            return changed.IsSuccess
                ? LuaApiHelpers.Success()
                : LuaApiHelpers.Failure(script, changed.Error);
        }

        private static DynValue GetEnvironment(
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var name = LuaApiHelpers.RequiredString(arguments, 0, "hos.shell.getenv");
            var value = state.Context.Shell.GetEnvironment(name);
            return value == null ? DynValue.Nil : DynValue.NewString(value);
        }

        private static DynValue SetEnvironment(
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var name = LuaApiHelpers.RequiredString(arguments, 0, "hos.shell.setenv");
            string value = null;
            if (arguments.Count > 1 && !arguments[1].IsNil())
                value = LuaApiHelpers.RequiredString(arguments, 1, "hos.shell.setenv");
            state.Context.Shell.SetEnvironment(name, value);
            return DynValue.True;
        }

        private static DynValue CurrentUser(LuaExecutionState state)
        {
            return state.Context.Machine.Users.TryGetUser(
                state.Context.Shell.CurrentUserId,
                out var user)
                ? DynValue.NewString(user.Name)
                : DynValue.Nil;
        }
    }
}
