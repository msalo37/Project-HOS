using System;

namespace HOS.Domain.Common
{
    public readonly struct GameDuration : IEquatable<GameDuration>, IComparable<GameDuration>
    {
        public GameDuration(long ticks)
        {
            if (ticks < 0)
                throw new ArgumentOutOfRangeException(nameof(ticks));

            Ticks = ticks;
        }

        public long Ticks { get; }
        public static GameDuration Zero => new GameDuration(0);

        public int CompareTo(GameDuration other) => Ticks.CompareTo(other.Ticks);
        public bool Equals(GameDuration other) => Ticks == other.Ticks;
        public override bool Equals(object obj) => obj is GameDuration other && Equals(other);
        public override int GetHashCode() => Ticks.GetHashCode();
    }

    public readonly struct GameTime : IEquatable<GameTime>, IComparable<GameTime>
    {
        public GameTime(long ticks)
        {
            if (ticks < 0)
                throw new ArgumentOutOfRangeException(nameof(ticks));

            Ticks = ticks;
        }

        public long Ticks { get; }
        public static GameTime Zero => new GameTime(0);

        public GameTime Advance(GameDuration duration)
        {
            return new GameTime(checked(Ticks + duration.Ticks));
        }

        public int CompareTo(GameTime other) => Ticks.CompareTo(other.Ticks);
        public bool Equals(GameTime other) => Ticks == other.Ticks;
        public override bool Equals(object obj) => obj is GameTime other && Equals(other);
        public override int GetHashCode() => Ticks.GetHashCode();
    }
}
