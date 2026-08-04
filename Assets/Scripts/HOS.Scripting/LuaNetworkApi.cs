using System.Collections.Generic;
using System.Globalization;
using HOS.Application.Remote;
using HOS.Domain.Network;
using MoonSharp.Interpreter;

namespace HOS.Scripting
{
    internal static class LuaNetworkApi
    {
        public static Table Create(Script script, LuaExecutionState state)
        {
            var api = new Table(script);
            api["connect"] = DynValue.NewCallback((_, args) =>
            {
                var connection = state.Context.RemoteAccess.Connect(
                    LuaApiHelpers.RequiredString(args, 0, "hos.net.connect"),
                    args.Count > 1 ? args.AsInt(1, "hos.net.connect") : 22);
                return connection.IsSuccess
                    ? LuaApiHelpers.Success(DynValue.NewString(connection.Value.ToString()))
                    : LuaApiHelpers.Failure(script, connection.Error.ToString(), "unable to connect");
            }, "hos.net.connect");
            api["authenticate"] = DynValue.NewCallback((_, args) =>
            {
                if (!ConnectionId.TryParse(
                        LuaApiHelpers.RequiredString(args, 0, "hos.net.authenticate"),
                        out var connectionId))
                    return LuaApiHelpers.Failure(script, "InvalidConnection", "invalid connection id");
                var result = state.Context.RemoteAccess.AuthenticateAndEnter(
                    connectionId,
                    LuaApiHelpers.RequiredString(args, 1, "hos.net.authenticate"),
                    LuaApiHelpers.RequiredString(args, 2, "hos.net.authenticate"));
                return result.IsSuccess
                    ? LuaApiHelpers.Success()
                    : LuaApiHelpers.Failure(script, result.Error.ToString(), "authentication failed");
            }, "hos.net.authenticate");
            api["disconnect"] = DynValue.NewCallback((_, args) =>
            {
                return ConnectionId.TryParse(
                           LuaApiHelpers.RequiredString(args, 0, "hos.net.disconnect"),
                           out var connectionId) &&
                       state.Context.RemoteAccess.Disconnect(connectionId)
                    ? LuaApiHelpers.Success()
                    : LuaApiHelpers.Failure(script, "InvalidConnection", "unable to disconnect");
            }, "hos.net.disconnect");
            api["service_info"] = DynValue.NewCallback((_, args) =>
            {
                if (!ConnectionId.TryParse(
                        LuaApiHelpers.RequiredString(args, 0, "hos.net.service_info"),
                        out var connectionId))
                    return LuaApiHelpers.Failure(script, "InvalidConnection", "invalid connection id");
                var service = state.Context.RemoteAccess.GetService(connectionId);
                if (service.IsFailure)
                    return LuaApiHelpers.Failure(script, service.Error.ToString(), "connection not found");
                var info = new Table(script);
                info["name"] = DynValue.NewString(service.Value.Name);
                info["protocol"] = DynValue.NewString(service.Value.Protocol.ToString().ToLowerInvariant());
                info["version"] = DynValue.NewString(service.Value.Version);
                info["port"] = DynValue.NewNumber(service.Value.Binding.Port);
                return LuaApiHelpers.Success(DynValue.NewTable(info));
            }, "hos.net.service_info");
            api["request"] = DynValue.NewCallback((_, args) =>
            {
                if (!ConnectionId.TryParse(
                        LuaApiHelpers.RequiredString(args, 0, "hos.net.request"),
                        out var connectionId))
                    return LuaApiHelpers.Failure(script, "InvalidConnection", "invalid connection id");
                var table = args.AsType(1, "hos.net.request", DataType.Table).Table;
                var operation = table.Get("operation");
                if (operation.Type != DataType.String || string.IsNullOrWhiteSpace(operation.String))
                    return LuaApiHelpers.Failure(script, "InvalidRequest", "operation is required");

                var fields = new Dictionary<string, string>();
                foreach (var pair in table.Pairs)
                {
                    if (pair.Key.Type != DataType.String || pair.Key.String == "operation")
                        continue;
                    switch (pair.Value.Type)
                    {
                        case DataType.String:
                            fields[pair.Key.String] = pair.Value.String;
                            break;
                        case DataType.Number:
                            fields[pair.Key.String] = pair.Value.Number.ToString(CultureInfo.InvariantCulture);
                            break;
                        case DataType.Boolean:
                            fields[pair.Key.String] = pair.Value.Boolean ? "true" : "false";
                            break;
                    }
                }

                var requested = state.Context.RemoteAccess.Request(
                    connectionId,
                    new ServiceRequest(operation.String, fields));
                if (requested.IsFailure)
                    return LuaApiHelpers.Failure(
                        script, requested.Error.ToString(), "service request failed");

                var response = new Table(script);
                response["success"] = DynValue.NewBoolean(requested.Value.Success);
                response["message"] = DynValue.NewString(requested.Value.Message);
                foreach (var field in requested.Value.Fields)
                    response[field.Key] = DynValue.NewString(field.Value);
                return LuaApiHelpers.Success(DynValue.NewTable(response));
            }, "hos.net.request");
            api["login"] = DynValue.NewCallback((_, args) =>
            {
                var ip = LuaApiHelpers.RequiredString(args, 0, "hos.net.login");
                var port = args.Count > 1 && !args[1].IsNil() ? args.AsInt(1, "hos.net.login") : 22;
                var result = state.Context.RemoteAccess.BeginInteractiveLogin(ip, port);
                return result.IsSuccess
                    ? LuaApiHelpers.Success(DynValue.NewString(result.StandardOutput))
                    : LuaApiHelpers.Failure(script, "NetworkError", result.StandardError);
            }, "hos.net.login");
            api["scan"] = DynValue.NewCallback((_, args) =>
            {
                var services = state.Context.RemoteAccess.Scan(
                    LuaApiHelpers.RequiredString(args, 0, "hos.net.scan"));
                var result = new Table(script);
                foreach (var service in services)
                {
                    var item = new Table(script);
                    item["port"] = DynValue.NewNumber(service.Binding.Port);
                    item["protocol"] = DynValue.NewString(service.Protocol.ToString().ToLowerInvariant());
                    item["name"] = DynValue.NewString(service.Name);
                    item["version"] = DynValue.NewString(service.Version);
                    result.Append(DynValue.NewTable(item));
                }
                return LuaApiHelpers.Success(DynValue.NewTable(result));
            }, "hos.net.scan");
            return api;
        }
    }
}
