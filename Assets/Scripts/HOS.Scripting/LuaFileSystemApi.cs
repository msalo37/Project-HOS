using System;
using HOS.Application.Execution;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using MoonSharp.Interpreter;

namespace HOS.Scripting
{
    internal static class LuaFileSystemApi
    {
        public static Table Create(Script script, LuaExecutionState state)
        {
            var api = new Table(script);
            api["cwd"] = Callback((_, __) => Cwd(script, state), "hos.fs.cwd");
            api["list"] = Callback((_, args) => List(script, state, args), "hos.fs.list");
            api["stat"] = Callback((_, args) => Stat(script, state, args), "hos.fs.stat");
            api["read"] = Callback((_, args) => Read(script, state, args), "hos.fs.read");
            api["write"] = Callback((_, args) => Write(script, state, args, false), "hos.fs.write");
            api["append"] = Callback((_, args) => Write(script, state, args, true), "hos.fs.append");
            api["create"] = Callback((_, args) => CreateFile(script, state, args), "hos.fs.create");
            api["mkdir"] = Callback((_, args) => CreateDirectory(script, state, args), "hos.fs.mkdir");
            api["remove"] = Callback((_, args) => Remove(script, state, args), "hos.fs.remove");
            api["copy"] = Callback((_, args) => Copy(script, state, args), "hos.fs.copy");
            api["move"] = Callback((_, args) => Move(script, state, args), "hos.fs.move");
            api["chmod"] = Callback((_, args) => Chmod(script, state, args), "hos.fs.chmod");
            api["exists"] = Callback((_, args) => Exists(state, args), "hos.fs.exists");
            return api;
        }

        private static DynValue Cwd(Script script, LuaExecutionState state)
        {
            var path = state.Context.Machine.FileSystem.GetPath(
                state.Context.Shell.WorkingDirectory);
            return path.IsSuccess
                ? LuaApiHelpers.Success(DynValue.NewString(path.Value))
                : LuaApiHelpers.Failure(script, path.Error);
        }

        private static DynValue List(
            Script script,
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var path = arguments.Count == 0 || arguments[0].IsNil()
                ? "."
                : LuaApiHelpers.RequiredString(arguments, 0, "hos.fs.list");
            if (!LuaApiHelpers.TryResolve(state, path, out var nodeId, out var error))
                return LuaApiHelpers.Failure(script, error);

            var list = state.Context.Machine.FileSystem.List(
                nodeId,
                state.Context.Shell.CreateAccessContext());
            if (list.IsFailure)
                return LuaApiHelpers.Failure(script, list.Error);

            var result = new Table(script);
            var parentPath = state.Context.Machine.FileSystem.GetPath(nodeId);
            foreach (var entry in list.Value)
            {
                var fullPath = parentPath.IsSuccess && parentPath.Value != "/"
                    ? parentPath.Value + "/" + entry.Name
                    : "/" + entry.Name;
                result.Append(DynValue.NewTable(
                    LuaApiHelpers.FileEntryToTable(script, entry, fullPath)));
            }

            return LuaApiHelpers.Success(DynValue.NewTable(result));
        }

        private static DynValue Stat(
            Script script,
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var path = LuaApiHelpers.RequiredString(arguments, 0, "hos.fs.stat");
            if (!LuaApiHelpers.TryResolve(state, path, out var nodeId, out var error))
                return LuaApiHelpers.Failure(script, error);

            var entry = state.Context.Machine.FileSystem.Stat(nodeId);
            if (entry.IsFailure)
                return LuaApiHelpers.Failure(script, entry.Error);
            var fullPath = state.Context.Machine.FileSystem.GetPath(nodeId);
            return LuaApiHelpers.Success(DynValue.NewTable(
                LuaApiHelpers.FileEntryToTable(
                    script,
                    entry.Value,
                    fullPath.IsSuccess ? fullPath.Value : path)));
        }

        private static DynValue Read(
            Script script,
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var path = LuaApiHelpers.RequiredString(arguments, 0, "hos.fs.read");
            if (!LuaApiHelpers.TryResolve(state, path, out var nodeId, out var error))
                return LuaApiHelpers.Failure(script, error);

            var content = state.Context.Machine.FileSystem.ReadFile(
                nodeId,
                state.Context.Shell.CreateAccessContext());
            if (content.IsFailure)
                return LuaApiHelpers.Failure(script, content.Error);

            var display = path.EndsWith(
                              HosBinaryFormat.Extension,
                              StringComparison.OrdinalIgnoreCase) ||
                          HosBinaryFormat.IsBinary(content.Value)
                ? HosBinaryFormat.ToDisplayString(content.Value)
                : content.Value.ReadUtf8();
            return LuaApiHelpers.Success(DynValue.NewString(display));
        }

        private static DynValue Write(
            Script script,
            LuaExecutionState state,
            CallbackArguments arguments,
            bool append)
        {
            var function = append ? "hos.fs.append" : "hos.fs.write";
            var path = LuaApiHelpers.RequiredString(arguments, 0, function);
            var value = LuaApiHelpers.RequiredString(arguments, 1, function);
            var fileSystem = state.Context.Machine.FileSystem;
            var access = state.Context.Shell.CreateAccessContext();

            if (LuaApiHelpers.TryResolve(state, path, out var nodeId, out var resolveError))
            {
                if (append)
                {
                    var existing = fileSystem.ReadFile(nodeId, access);
                    if (existing.IsFailure)
                        return LuaApiHelpers.Failure(script, existing.Error);
                    value = existing.Value.ReadUtf8() + value;
                }

                var written = fileSystem.WriteFile(
                    nodeId,
                    FileContent.FromUtf8(value),
                    access);
                return written.IsSuccess
                    ? LuaApiHelpers.Success()
                    : LuaApiHelpers.Failure(script, written.Error);
            }

            if (resolveError != FileSystemError.NodeNotFound)
                return LuaApiHelpers.Failure(script, resolveError);
            if (!LuaApiHelpers.TryResolveParent(
                    state,
                    path,
                    out var parentId,
                    out var name,
                    out var parentError))
            {
                return LuaApiHelpers.Failure(script, parentError);
            }

            var created = fileSystem.CreateFile(
                parentId,
                name,
                LuaApiHelpers.CurrentOwnership(state),
                FilePermissions.DefaultFile,
                FileContent.FromUtf8(value),
                access);
            return created.IsSuccess
                ? LuaApiHelpers.Success(DynValue.NewString(created.Value.ToString()))
                : LuaApiHelpers.Failure(script, created.Error);
        }

        private static DynValue CreateFile(
            Script script,
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var path = LuaApiHelpers.RequiredString(arguments, 0, "hos.fs.create");
            if (!LuaApiHelpers.TryResolveParent(
                    state,
                    path,
                    out var parentId,
                    out var name,
                    out var error))
            {
                return LuaApiHelpers.Failure(script, error);
            }

            var created = state.Context.Machine.FileSystem.CreateFile(
                parentId,
                name,
                LuaApiHelpers.CurrentOwnership(state),
                FilePermissions.DefaultFile,
                FileContent.Empty,
                state.Context.Shell.CreateAccessContext());
            return created.IsSuccess
                ? LuaApiHelpers.Success(DynValue.NewString(created.Value.ToString()))
                : LuaApiHelpers.Failure(script, created.Error);
        }

        private static DynValue CreateDirectory(
            Script script,
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var path = LuaApiHelpers.RequiredString(arguments, 0, "hos.fs.mkdir");
            if (!LuaApiHelpers.TryResolveParent(
                    state,
                    path,
                    out var parentId,
                    out var name,
                    out var error))
            {
                return LuaApiHelpers.Failure(script, error);
            }

            var created = state.Context.Machine.FileSystem.CreateDirectory(
                parentId,
                name,
                LuaApiHelpers.CurrentOwnership(state),
                FilePermissions.DefaultDirectory,
                state.Context.Shell.CreateAccessContext());
            return created.IsSuccess
                ? LuaApiHelpers.Success(DynValue.NewString(created.Value.ToString()))
                : LuaApiHelpers.Failure(script, created.Error);
        }

        private static DynValue Remove(
            Script script,
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var path = LuaApiHelpers.RequiredString(arguments, 0, "hos.fs.remove");
            var recursive = LuaApiHelpers.OptionalBoolean(arguments, 1, false);
            if (!LuaApiHelpers.TryResolve(state, path, out var nodeId, out var error))
                return LuaApiHelpers.Failure(script, error);

            var deleted = state.Context.Machine.FileSystem.Delete(
                nodeId,
                recursive ? DeleteOptions.RecursiveDelete : DeleteOptions.Single,
                state.Context.Shell.CreateAccessContext());
            return deleted.IsSuccess
                ? LuaApiHelpers.Success()
                : LuaApiHelpers.Failure(script, deleted.Error);
        }

        private static DynValue Move(
            Script script,
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var sourcePath = LuaApiHelpers.RequiredString(arguments, 0, "hos.fs.move");
            var destinationPath = LuaApiHelpers.RequiredString(arguments, 1, "hos.fs.move");
            if (!LuaApiHelpers.TryResolve(state, sourcePath, out var sourceId, out var sourceError))
                return LuaApiHelpers.Failure(script, sourceError);

            var sourceEntry = state.Context.Machine.FileSystem.Stat(sourceId);
            if (sourceEntry.IsFailure)
                return LuaApiHelpers.Failure(script, sourceEntry.Error);

            NodeId destinationParent;
            string destinationName;
            if (LuaApiHelpers.TryResolve(
                    state,
                    destinationPath,
                    out var existingDestination,
                    out _))
            {
                var destinationEntry = state.Context.Machine.FileSystem.Stat(existingDestination);
                if (destinationEntry.IsFailure ||
                    destinationEntry.Value.Type != FileNodeType.Directory)
                {
                    return LuaApiHelpers.Failure(
                        script,
                        "DestinationExists",
                        "destination already exists");
                }

                destinationParent = existingDestination;
                destinationName = sourceEntry.Value.Name;
            }
            else if (!LuaApiHelpers.TryResolveParent(
                         state,
                         destinationPath,
                         out destinationParent,
                         out destinationName,
                         out var destinationError))
            {
                return LuaApiHelpers.Failure(script, destinationError);
            }

            var moved = state.Context.Machine.FileSystem.Move(
                sourceId,
                destinationParent,
                destinationName,
                state.Context.Shell.CreateAccessContext());
            return moved.IsSuccess
                ? LuaApiHelpers.Success()
                : LuaApiHelpers.Failure(script, moved.Error);
        }

        private static DynValue Copy(
            Script script,
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var sourcePath = LuaApiHelpers.RequiredString(arguments, 0, "hos.fs.copy");
            var destinationPath = LuaApiHelpers.RequiredString(arguments, 1, "hos.fs.copy");
            if (!LuaApiHelpers.TryResolve(state, sourcePath, out var sourceId, out var sourceError))
                return LuaApiHelpers.Failure(script, sourceError);

            var fileSystem = state.Context.Machine.FileSystem;
            var sourceEntry = fileSystem.Stat(sourceId);
            if (sourceEntry.IsFailure)
                return LuaApiHelpers.Failure(script, sourceEntry.Error);

            NodeId destinationParent;
            string destinationName;
            if (LuaApiHelpers.TryResolve(
                    state,
                    destinationPath,
                    out var existingDestination,
                    out _))
            {
                var destinationEntry = fileSystem.Stat(existingDestination);
                if (destinationEntry.IsFailure ||
                    destinationEntry.Value.Type != FileNodeType.Directory)
                {
                    return LuaApiHelpers.Failure(
                        script,
                        "DestinationExists",
                        "destination already exists");
                }

                destinationParent = existingDestination;
                destinationName = sourceEntry.Value.Name;
            }
            else if (!LuaApiHelpers.TryResolveParent(
                         state,
                         destinationPath,
                         out destinationParent,
                         out destinationName,
                         out var destinationError))
            {
                return LuaApiHelpers.Failure(script, destinationError);
            }

            var copied = fileSystem.CopyFile(
                sourceId,
                destinationParent,
                destinationName,
                LuaApiHelpers.CurrentOwnership(state),
                state.Context.Shell.CreateAccessContext());
            return copied.IsSuccess
                ? LuaApiHelpers.Success(DynValue.NewString(copied.Value.ToString()))
                : LuaApiHelpers.Failure(script, copied.Error);
        }

        private static DynValue Chmod(
            Script script,
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var path = LuaApiHelpers.RequiredString(arguments, 0, "hos.fs.chmod");
            var mode = LuaApiHelpers.RequiredString(arguments, 1, "hos.fs.chmod");
            if (!LuaApiHelpers.TryParsePermissions(mode, out var permissions))
                return LuaApiHelpers.Failure(script, "InvalidMode", "mode must be an octal value like 755", 2);
            if (!LuaApiHelpers.TryResolve(state, path, out var nodeId, out var error))
                return LuaApiHelpers.Failure(script, error);

            var changed = state.Context.Machine.FileSystem.ChangePermissions(
                nodeId,
                permissions,
                state.Context.Shell.CreateAccessContext());
            return changed.IsSuccess
                ? LuaApiHelpers.Success()
                : LuaApiHelpers.Failure(script, changed.Error);
        }

        private static DynValue Exists(
            LuaExecutionState state,
            CallbackArguments arguments)
        {
            var path = LuaApiHelpers.RequiredString(arguments, 0, "hos.fs.exists");
            return DynValue.NewBoolean(
                LuaApiHelpers.TryResolve(state, path, out _, out _));
        }

        private static DynValue Callback(
            Func<ScriptExecutionContext, CallbackArguments, DynValue> callback,
            string name)
        {
            return DynValue.NewCallback(callback, name);
        }
    }
}
