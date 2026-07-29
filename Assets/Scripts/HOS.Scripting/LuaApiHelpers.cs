using HOS.Application.Execution;
using HOS.Application.Shell;
using HOS.Domain.FileSystem;
using HOS.Domain.Identity;
using MoonSharp.Interpreter;

namespace HOS.Scripting
{
    internal static class LuaApiHelpers
    {
        public static DynValue Success()
        {
            return DynValue.NewTuple(DynValue.True, DynValue.Nil);
        }

        public static DynValue Success(DynValue value)
        {
            return DynValue.NewTuple(value, DynValue.Nil);
        }

        public static DynValue Failure(Script script, FileSystemError error)
        {
            var table = new Table(script);
            table["code"] = DynValue.NewNumber((int)error);
            table["name"] = DynValue.NewString(error.ToString());
            table["message"] = DynValue.NewString(ShellEngine.FormatFileSystemError(error));
            return DynValue.NewTuple(DynValue.Nil, DynValue.NewTable(table));
        }

        public static DynValue Failure(Script script, string name, string message, int code = 1)
        {
            var table = new Table(script);
            table["code"] = DynValue.NewNumber(code);
            table["name"] = DynValue.NewString(name);
            table["message"] = DynValue.NewString(message);
            return DynValue.NewTuple(DynValue.Nil, DynValue.NewTable(table));
        }

        public static string RequiredString(
            CallbackArguments arguments,
            int index,
            string function)
        {
            return arguments.AsType(index, function, DataType.String).String;
        }

        public static bool OptionalBoolean(
            CallbackArguments arguments,
            int index,
            bool defaultValue)
        {
            if (arguments.Count <= index || arguments[index].IsNil())
                return defaultValue;
            return arguments[index].CastToBool();
        }

        public static bool TryResolve(
            LuaExecutionState state,
            string path,
            out NodeId nodeId,
            out FileSystemError error)
        {
            nodeId = default;
            var parsed = VirtualPath.Parse(path);
            if (parsed.IsFailure)
            {
                error = FileSystemError.InvalidName;
                return false;
            }

            var shell = state.Context.Shell;
            var result = state.Context.Machine.FileSystem.Resolve(
                shell.WorkingDirectory,
                parsed.Value,
                shell.CreateAccessContext());
            if (result.IsFailure)
            {
                error = result.Error;
                return false;
            }

            nodeId = result.Value;
            error = default;
            return true;
        }

        public static bool TryResolveParent(
            LuaExecutionState state,
            string path,
            out NodeId parentId,
            out string name,
            out FileSystemError error)
        {
            parentId = default;
            name = null;
            error = default;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = FileSystemError.InvalidName;
                return false;
            }

            var trimmed = path.TrimEnd('/');
            var separator = trimmed.LastIndexOf('/');
            string parentPath;

            if (separator < 0)
            {
                parentPath = ".";
                name = trimmed;
            }
            else
            {
                parentPath = separator == 0 ? "/" : trimmed.Substring(0, separator);
                name = trimmed.Substring(separator + 1);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                error = FileSystemError.InvalidName;
                return false;
            }

            return TryResolve(state, parentPath, out parentId, out error);
        }

        public static FileOwnership CurrentOwnership(LuaExecutionState state)
        {
            var shell = state.Context.Shell;
            state.Context.Machine.Users.TryGetUser(shell.CurrentUserId, out var user);
            return new FileOwnership(user.Id, user.PrimaryGroup);
        }

        public static Table FileEntryToTable(Script script, FileEntry entry, string path)
        {
            var table = new Table(script);
            table["id"] = DynValue.NewString(entry.Id.ToString());
            table["name"] = DynValue.NewString(entry.Name);
            table["path"] = DynValue.NewString(path ?? entry.Name);
            table["type"] = DynValue.NewString(ToLuaType(entry.Type));
            table["size"] = DynValue.NewNumber(entry.ContentLength);
            table["owner"] = DynValue.NewString(entry.Ownership.Owner.ToString());
            table["group"] = DynValue.NewString(entry.Ownership.Group.ToString());
            table["permissions"] = DynValue.NewString(FormatPermissions(entry.Permissions));
            return table;
        }

        public static string FormatPermissions(FilePermissions permissions)
        {
            return FormatBits(permissions.Owner) +
                   FormatBits(permissions.Group) +
                   FormatBits(permissions.Other);
        }

        public static bool TryParsePermissions(string value, out FilePermissions permissions)
        {
            permissions = default;
            if (value == null || value.Length != 3)
                return false;

            if (!TryParseDigit(value[0], out var owner) ||
                !TryParseDigit(value[1], out var group) ||
                !TryParseDigit(value[2], out var other))
            {
                return false;
            }

            permissions = new FilePermissions(owner, group, other);
            return true;
        }

        private static bool TryParseDigit(char character, out PermissionBits bits)
        {
            bits = PermissionBits.None;
            if (character < '0' || character > '7')
                return false;
            bits = (PermissionBits)(character - '0');
            return true;
        }

        private static string FormatBits(PermissionBits bits)
        {
            return ((bits & PermissionBits.Read) != 0 ? "r" : "-") +
                   ((bits & PermissionBits.Write) != 0 ? "w" : "-") +
                   ((bits & PermissionBits.Execute) != 0 ? "x" : "-");
        }

        private static string ToLuaType(FileNodeType type)
        {
            switch (type)
            {
                case FileNodeType.Directory:
                    return "directory";
                case FileNodeType.SymbolicLink:
                    return "symlink";
                default:
                    return "file";
            }
        }
    }
}
