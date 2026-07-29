using System;

namespace HOS.Domain.Identity
{
    public readonly struct UserId : IEquatable<UserId>
    {
        public UserId(Guid value) => Value = value;
        public Guid Value { get; }
        public static UserId New() => new UserId(Guid.NewGuid());
        public bool Equals(UserId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is UserId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(UserId left, UserId right) => left.Equals(right);
        public static bool operator !=(UserId left, UserId right) => !left.Equals(right);
    }

    public readonly struct GroupId : IEquatable<GroupId>
    {
        public GroupId(Guid value) => Value = value;
        public Guid Value { get; }
        public static GroupId New() => new GroupId(Guid.NewGuid());
        public bool Equals(GroupId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is GroupId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(GroupId left, GroupId right) => left.Equals(right);
        public static bool operator !=(GroupId left, GroupId right) => !left.Equals(right);
    }
}
