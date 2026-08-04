using System;
using System.Collections.Generic;
using HOS.Application.Shell;
using HOS.Domain.Common;
using HOS.Domain.FileSystem;
using HOS.Domain.Machines;
using HOS.Domain.Network;
using HOS.Domain.Services;

namespace HOS.Application.Remote
{
    public enum RemoteAccessError
    {
        InvalidAddress, NetworkFailure, ConnectionNotFound, UnsupportedService,
        AuthenticationRequired, InvalidCredentials, UserNotFound, HomeNotFound,
        HandlerNotFound, PayloadInstallFailed
    }

    public sealed class RemoteAccessService
    {
        private readonly PlayerShellContext shell;
        private readonly Dictionary<ServiceProtocol, IVirtualServiceHandler> handlers =
            new Dictionary<ServiceProtocol, IVirtualServiceHandler>();

        public RemoteAccessService(PlayerShellContext shell)
        {
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            RegisterHandler(new DevSyncServiceHandler());
        }

        public bool HasPendingLogin => Pending != null;
        public bool IsSecretInput => Pending != null && Pending.Username != null;
        public string InteractionPrompt => IsSecretInput ? "password:" : "login:";
        public PendingRemoteLogin Pending { get; private set; }

        public Result<ConnectionId, RemoteAccessError> Connect(string address, int port)
        {
            if (!VirtualIpAddress.TryParse(address, out var ip))
                return Result<ConnectionId, RemoteAccessError>.Failure(RemoteAccessError.InvalidAddress);
            var result = shell.World.Network.Connect(
                shell.CurrentMachineId, ip, new PortBinding(port, TransportProtocol.Tcp));
            return result.IsSuccess
                ? Result<ConnectionId, RemoteAccessError>.Success(result.Value)
                : Result<ConnectionId, RemoteAccessError>.Failure(RemoteAccessError.NetworkFailure);
        }

        public Result<AccessGrant, RemoteAccessError> Authenticate(
            ConnectionId connectionId, string username, string password)
        {
            if (!TryGetEndpoint(connectionId, out var connection, out var machine, out var service))
                return Result<AccessGrant, RemoteAccessError>.Failure(RemoteAccessError.ConnectionNotFound);
            if (!service.AllowsPasswordAuthentication)
                return Result<AccessGrant, RemoteAccessError>.Failure(RemoteAccessError.UnsupportedService);
            if (!machine.Users.TryGetUser(username, out var user))
                return Result<AccessGrant, RemoteAccessError>.Failure(RemoteAccessError.UserNotFound);
            if (!machine.Credentials.VerifyPassword(user.Id, password))
                return Result<AccessGrant, RemoteAccessError>.Failure(RemoteAccessError.InvalidCredentials);
            return CreateGrant(connection, machine, user.Id, username, AccessMethod.Password);
        }

        public Result<AccessGrant, RemoteAccessError> AuthenticateAndEnter(
            ConnectionId connectionId, string username, string password)
        {
            var result = Authenticate(connectionId, username, password);
            if (result.IsSuccess) shell.EnterRemote(result.Value);
            return result;
        }

        public bool Disconnect(ConnectionId connectionId) =>
            shell.World.Network.Disconnect(connectionId).IsSuccess;

        public Result<MachineService, RemoteAccessError> GetService(ConnectionId connectionId)
        {
            return TryGetEndpoint(connectionId, out _, out _, out var service)
                ? Result<MachineService, RemoteAccessError>.Success(service)
                : Result<MachineService, RemoteAccessError>.Failure(RemoteAccessError.ConnectionNotFound);
        }

        public void RegisterHandler(IVirtualServiceHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            handlers[handler.Protocol] = handler;
        }

        public Result<ServiceResponse, RemoteAccessError> Request(
            ConnectionId connectionId,
            ServiceRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!TryGetEndpoint(connectionId, out var connection, out var machine, out var service))
                return Result<ServiceResponse, RemoteAccessError>.Failure(RemoteAccessError.ConnectionNotFound);
            if (!handlers.TryGetValue(service.Protocol, out var handler))
                return Result<ServiceResponse, RemoteAccessError>.Failure(RemoteAccessError.HandlerNotFound);

            var response = handler.Handle(
                new VirtualServiceContext(connection, machine, service),
                request);
            if (!response.Success || !response.GrantedUserId.HasValue)
                return Result<ServiceResponse, RemoteAccessError>.Success(response);

            if (!machine.Users.TryGetUser(response.GrantedUserId.Value, out var user))
                return Result<ServiceResponse, RemoteAccessError>.Failure(RemoteAccessError.UserNotFound);
            var grant = CreateGrant(
                connection, machine, user.Id, user.Name, AccessMethod.Exploit);
            if (grant.IsFailure)
                return Result<ServiceResponse, RemoteAccessError>.Failure(grant.Error);
            if (!InstallPayload(machine, grant.Value, user.PrimaryGroup, response.Payload))
                return Result<ServiceResponse, RemoteAccessError>.Failure(RemoteAccessError.PayloadInstallFailed);

            shell.EnterRemote(grant.Value);
            response.With("access_granted", "true").With("user", user.Name);
            return Result<ServiceResponse, RemoteAccessError>.Success(response);
        }

        public Result<AccessGrant, RemoteAccessError> TryPasswordless(ConnectionId connectionId)
        {
            if (!TryGetEndpoint(connectionId, out var connection, out var machine, out var service))
                return Result<AccessGrant, RemoteAccessError>.Failure(RemoteAccessError.ConnectionNotFound);
            if (!service.PasswordlessUserId.HasValue ||
                !machine.Users.TryGetUser(service.PasswordlessUserId.Value, out var user))
                return Result<AccessGrant, RemoteAccessError>.Failure(RemoteAccessError.AuthenticationRequired);
            return CreateGrant(connection, machine, user.Id, user.Name, AccessMethod.Passwordless);
        }

        public CommandResult BeginInteractiveLogin(string address, int port)
        {
            var connected = Connect(address, port);
            if (connected.IsFailure) return CommandResult.Failure(1, connected.Error.ToString());
            var passwordless = TryPasswordless(connected.Value);
            if (passwordless.IsSuccess)
            {
                shell.EnterRemote(passwordless.Value);
                return CommandResult.Success($"Connected to {shell.CurrentMachine.Hostname}.\n");
            }
            Pending = new PendingRemoteLogin(connected.Value);
            return CommandResult.Success("Connected.\nlogin:\n");
        }

        public CommandResult SubmitInteraction(string input)
        {
            if (Pending == null) return CommandResult.Failure(1, "no pending login");
            if (Pending.Username == null)
            {
                Pending.Username = input ?? string.Empty;
                return CommandResult.Success("password:\n");
            }
            var result = Authenticate(Pending.ConnectionId, Pending.Username, input ?? string.Empty);
            if (result.IsFailure)
            {
                shell.World.Network.Disconnect(Pending.ConnectionId);
                Pending = null;
                return CommandResult.Failure(1, "authentication failed");
            }
            Pending = null;
            shell.EnterRemote(result.Value);
            return CommandResult.Success($"Connected to {shell.CurrentMachine.Hostname}.\n");
        }

        public IReadOnlyList<MachineService> Scan(string address)
        {
            if (!VirtualIpAddress.TryParse(address, out var ip) ||
                !shell.World.Network.CanReach(shell.CurrentMachineId, ip) ||
                !shell.World.Network.TryGetMachine(ip, out var machine))
                return Array.Empty<MachineService>();
            return machine.Services.ListListening();
        }

        private bool TryGetEndpoint(ConnectionId id, out NetworkConnection connection, out Machine machine, out MachineService service)
        {
            service = null; machine = null;
            if (!shell.World.Network.TryGetConnection(id, out connection) ||
                !shell.World.TryGetMachine(connection.Destination, out machine))
                return false;
            return machine.Services.TryGetListening(connection.Endpoint, out service);
        }

        private static Result<AccessGrant, RemoteAccessError> CreateGrant(
            NetworkConnection connection, Machine machine, HOS.Domain.Identity.UserId userId,
            string username, AccessMethod method)
        {
            var access = machine.Users.CreateAccessContext(userId);
            var path = VirtualPath.Parse("/home/" + username);
            var home = machine.FileSystem.Resolve(machine.FileSystem.RootId, path.Value, access);
            if (home.IsFailure)
                return Result<AccessGrant, RemoteAccessError>.Failure(RemoteAccessError.HomeNotFound);
            return Result<AccessGrant, RemoteAccessError>.Success(
                new AccessGrant(connection.Id, machine.Id, userId, home.Value, method));
        }

        private static bool InstallPayload(
            Machine machine,
            AccessGrant grant,
            HOS.Domain.Identity.GroupId groupId,
            string payload)
        {
            var root = machine.Users.CreateAccessContext(machine.RootUserId, true);
            var path = VirtualPath.Parse("last_job.lua");
            var existing = machine.FileSystem.Resolve(grant.HomeDirectoryId, path.Value, root);
            if (existing.IsSuccess)
            {
                return machine.FileSystem.WriteFile(
                    existing.Value, FileContent.FromUtf8(payload), root).IsSuccess;
            }

            var permissions = new HOS.Domain.Identity.FilePermissions(
                HOS.Domain.Identity.PermissionBits.Read |
                HOS.Domain.Identity.PermissionBits.Write |
                HOS.Domain.Identity.PermissionBits.Execute,
                HOS.Domain.Identity.PermissionBits.Read |
                HOS.Domain.Identity.PermissionBits.Execute,
                HOS.Domain.Identity.PermissionBits.None);
            return machine.FileSystem.CreateFile(
                grant.HomeDirectoryId,
                "last_job.lua",
                new HOS.Domain.Identity.FileOwnership(grant.UserId, groupId),
                permissions,
                FileContent.FromUtf8(payload),
                root).IsSuccess;
        }
    }

    public sealed class PendingRemoteLogin
    {
        public PendingRemoteLogin(ConnectionId connectionId) { ConnectionId = connectionId; }
        public ConnectionId ConnectionId { get; }
        public string Username { get; internal set; }
    }
}
