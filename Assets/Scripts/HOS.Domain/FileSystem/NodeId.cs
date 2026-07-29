using System;

namespace HOS.Domain.FileSystem
{
    public readonly struct NodeId : IEquatable<NodeId>
    {
        public NodeId(Guid value) => Value = value;
        public Guid Value { get; }
        public static NodeId New() => new NodeId(Guid.NewGuid());
        public bool Equals(NodeId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is NodeId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(NodeId left, NodeId right) => left.Equals(right);
        public static bool operator !=(NodeId left, NodeId right) => !left.Equals(right);
    }
}
