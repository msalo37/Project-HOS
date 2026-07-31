using System;
using System.Collections.Generic;
using HOS.Domain.Common;
using HOS.Domain.Identity;

namespace HOS.Domain.Services
{
    public enum ServiceProtocol { Unknown, Ssh, Http, Ftp, Backdoor }
    public readonly struct ServiceId : IEquatable<ServiceId>
    {
        public ServiceId(Guid value) => Value = value;
        public Guid Value { get; }
        public static ServiceId New() => new ServiceId(Guid.NewGuid());
        public bool Equals(ServiceId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is ServiceId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(ServiceId left, ServiceId right) => left.Equals(right);
        public static bool operator !=(ServiceId left, ServiceId right) => !left.Equals(right);
    }

    public enum TransportProtocol
    {
        Tcp,
        Udp
    }

    public readonly struct PortBinding : IEquatable<PortBinding>
    {
        public PortBinding(int port, TransportProtocol protocol)
        {
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port));

            Port = port;
            Protocol = protocol;
        }

        public int Port { get; }
        public TransportProtocol Protocol { get; }

        public bool Equals(PortBinding other) =>
            Port == other.Port && Protocol == other.Protocol;

        public override bool Equals(object obj) => obj is PortBinding other && Equals(other);
        public override int GetHashCode() => (Port * 397) ^ (int)Protocol;
    }

    public enum ServiceState
    {
        Stopped,
        Running
    }

    public enum ServiceError
    {
        ServiceNotFound,
        NameAlreadyExists,
        PortAlreadyInUse,
        InvalidName,
        AccessDenied,
        AlreadyRunning,
        AlreadyStopped
    }

    public sealed class MachineService
    {
        internal MachineService(
            ServiceId id,
            string name,
            UserId owner,
            PortBinding binding,
            ServiceProtocol protocol,
            string version,
            bool allowsPasswordAuthentication,
            UserId? passwordlessUserId)
        {
            Id = id;
            Name = name;
            Owner = owner;
            Binding = binding;
            Protocol = protocol;
            Version = version ?? string.Empty;
            AllowsPasswordAuthentication = allowsPasswordAuthentication;
            PasswordlessUserId = passwordlessUserId;
            State = ServiceState.Stopped;
        }

        public ServiceId Id { get; }
        public string Name { get; }
        public UserId Owner { get; }
        public PortBinding Binding { get; }
        public ServiceProtocol Protocol { get; }
        public string Version { get; }
        public bool AllowsPasswordAuthentication { get; }
        public UserId? PasswordlessUserId { get; }
        public ServiceState State { get; internal set; }
    }

    public sealed class ServiceManager
    {
        private readonly Dictionary<ServiceId, MachineService> services =
            new Dictionary<ServiceId, MachineService>();

        private readonly Dictionary<string, ServiceId> byName =
            new Dictionary<string, ServiceId>(StringComparer.Ordinal);

        private readonly Dictionary<PortBinding, ServiceId> listening =
            new Dictionary<PortBinding, ServiceId>();

        public Result<ServiceId, ServiceError> Add(
            string name,
            UserId owner,
            PortBinding binding,
            ServiceProtocol protocol = ServiceProtocol.Unknown,
            string version = "",
            bool allowsPasswordAuthentication = false,
            UserId? passwordlessUserId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result<ServiceId, ServiceError>.Failure(ServiceError.InvalidName);
            if (byName.ContainsKey(name))
                return Result<ServiceId, ServiceError>.Failure(
                    ServiceError.NameAlreadyExists);

            var id = ServiceId.New();
            services.Add(id, new MachineService(id, name, owner, binding, protocol, version, allowsPasswordAuthentication, passwordlessUserId));
            byName.Add(name, id);
            return Result<ServiceId, ServiceError>.Success(id);
        }

        public Result<Unit, ServiceError> Start(
            ServiceId id,
            UserId caller,
            bool isKernel = false)
        {
            if (!services.TryGetValue(id, out var service))
                return Result<Unit, ServiceError>.Failure(ServiceError.ServiceNotFound);
            if (!isKernel && service.Owner != caller)
                return Result<Unit, ServiceError>.Failure(ServiceError.AccessDenied);
            if (service.State == ServiceState.Running)
                return Result<Unit, ServiceError>.Failure(ServiceError.AlreadyRunning);
            if (listening.ContainsKey(service.Binding))
                return Result<Unit, ServiceError>.Failure(ServiceError.PortAlreadyInUse);

            listening.Add(service.Binding, id);
            service.State = ServiceState.Running;
            return Result<Unit, ServiceError>.Success(Unit.Value);
        }

        public Result<Unit, ServiceError> Stop(
            ServiceId id,
            UserId caller,
            bool isKernel = false)
        {
            if (!services.TryGetValue(id, out var service))
                return Result<Unit, ServiceError>.Failure(ServiceError.ServiceNotFound);
            if (!isKernel && service.Owner != caller)
                return Result<Unit, ServiceError>.Failure(ServiceError.AccessDenied);
            if (service.State == ServiceState.Stopped)
                return Result<Unit, ServiceError>.Failure(ServiceError.AlreadyStopped);

            listening.Remove(service.Binding);
            service.State = ServiceState.Stopped;
            return Result<Unit, ServiceError>.Success(Unit.Value);
        }

        public bool TryGetListening(PortBinding binding, out MachineService service)
        {
            if (listening.TryGetValue(binding, out var id))
                return services.TryGetValue(id, out service);

            service = null;
            return false;
        }

        public IReadOnlyList<MachineService> ListListening()
        {
            var result = new List<MachineService>();
            foreach (var id in listening.Values) result.Add(services[id]);
            result.Sort((a, b) => a.Binding.Port.CompareTo(b.Binding.Port));
            return result;
        }
    }
}
