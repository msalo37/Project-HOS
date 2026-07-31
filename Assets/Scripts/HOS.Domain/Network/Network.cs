using System;
using System.Collections.Generic;
using System.Net;
using HOS.Domain.Common;
using HOS.Domain.Machines;
using HOS.Domain.Services;

namespace HOS.Domain.Network
{
    public readonly struct VirtualIpAddress : IEquatable<VirtualIpAddress>
    {
        private VirtualIpAddress(string value) => Value = value;
        public string Value { get; }

        public static bool TryParse(string value, out VirtualIpAddress address)
        {
            if (IPAddress.TryParse(value, out var parsed) &&
                parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                address = new VirtualIpAddress(parsed.ToString());
                return true;
            }

            address = default;
            return false;
        }

        public bool Equals(VirtualIpAddress other) =>
            StringComparer.Ordinal.Equals(Value, other.Value);

        public override bool Equals(object obj) =>
            obj is VirtualIpAddress other && Equals(other);

        public override int GetHashCode() =>
            Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(VirtualIpAddress left, VirtualIpAddress right) => left.Equals(right);
        public static bool operator !=(VirtualIpAddress left, VirtualIpAddress right) => !left.Equals(right);
    }

    public readonly struct ConnectionId : IEquatable<ConnectionId>
    {
        public ConnectionId(Guid value) => Value = value;
        public Guid Value { get; }
        public static ConnectionId New() => new ConnectionId(Guid.NewGuid());
        public static bool TryParse(string value, out ConnectionId id)
        {
            if (Guid.TryParse(value, out var parsed))
            {
                id = new ConnectionId(parsed);
                return true;
            }
            id = default;
            return false;
        }
        public bool Equals(ConnectionId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is ConnectionId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
    }

    public enum NetworkError
    {
        SourceNotFound,
        DestinationNotFound,
        AddressAlreadyRegistered,
        RouteUnavailable,
        PortClosed,
        ConnectionNotFound
    }

    public sealed class NetworkConnection
    {
        internal NetworkConnection(
            ConnectionId id,
            MachineId source,
            MachineId destination,
            PortBinding endpoint)
        {
            Id = id;
            Source = source;
            Destination = destination;
            Endpoint = endpoint;
        }

        public ConnectionId Id { get; }
        public MachineId Source { get; }
        public MachineId Destination { get; }
        public PortBinding Endpoint { get; }
    }

    public sealed class VirtualNetwork
    {
        private readonly Dictionary<VirtualIpAddress, Machine> machinesByAddress =
            new Dictionary<VirtualIpAddress, Machine>();

        private readonly Dictionary<MachineId, HashSet<MachineId>> routes =
            new Dictionary<MachineId, HashSet<MachineId>>();

        private readonly Dictionary<ConnectionId, NetworkConnection> connections =
            new Dictionary<ConnectionId, NetworkConnection>();

        public Result<Unit, NetworkError> Register(Machine machine)
        {
            if (machinesByAddress.ContainsKey(machine.Address))
                return Result<Unit, NetworkError>.Failure(
                    NetworkError.AddressAlreadyRegistered);

            machinesByAddress.Add(machine.Address, machine);
            routes[machine.Id] = new HashSet<MachineId>();
            return Result<Unit, NetworkError>.Success(Unit.Value);
        }

        public void AddBidirectionalRoute(MachineId first, MachineId second)
        {
            if (!routes.ContainsKey(first) || !routes.ContainsKey(second))
                throw new InvalidOperationException("Both machines must be registered.");

            routes[first].Add(second);
            routes[second].Add(first);
        }

        public Result<ConnectionId, NetworkError> Connect(
            MachineId sourceId,
            VirtualIpAddress destinationAddress,
            PortBinding endpoint)
        {
            if (!routes.ContainsKey(sourceId))
                return Result<ConnectionId, NetworkError>.Failure(
                    NetworkError.SourceNotFound);
            if (!machinesByAddress.TryGetValue(destinationAddress, out var destination))
                return Result<ConnectionId, NetworkError>.Failure(
                    NetworkError.DestinationNotFound);
            if (!HasRoute(sourceId, destination.Id))
                return Result<ConnectionId, NetworkError>.Failure(
                    NetworkError.RouteUnavailable);
            if (!destination.Services.TryGetListening(endpoint, out _))
                return Result<ConnectionId, NetworkError>.Failure(NetworkError.PortClosed);

            var id = ConnectionId.New();
            connections.Add(
                id,
                new NetworkConnection(id, sourceId, destination.Id, endpoint));
            return Result<ConnectionId, NetworkError>.Success(id);
        }

        public bool TryGetConnection(ConnectionId id, out NetworkConnection connection) =>
            connections.TryGetValue(id, out connection);

        public bool TryGetMachine(VirtualIpAddress address, out Machine machine) =>
            machinesByAddress.TryGetValue(address, out machine);

        public Result<Unit, NetworkError> Disconnect(ConnectionId id) =>
            connections.Remove(id)
                ? Result<Unit, NetworkError>.Success(Unit.Value)
                : Result<Unit, NetworkError>.Failure(NetworkError.ConnectionNotFound);

        public bool CanReach(MachineId sourceId, VirtualIpAddress address) =>
            routes.ContainsKey(sourceId) &&
            machinesByAddress.TryGetValue(address, out var destination) &&
            HasRoute(sourceId, destination.Id);

        private bool HasRoute(MachineId source, MachineId destination)
        {
            if (source == destination)
                return true;

            var visited = new HashSet<MachineId> { source };
            var queue = new Queue<MachineId>();
            queue.Enqueue(source);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var next in routes[current])
                {
                    if (next == destination)
                        return true;
                    if (visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            return false;
        }
    }
}
