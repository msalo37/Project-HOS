using System;

namespace HOS.ECS.Component
{
    public ref struct ComponentSpan<T> where T : struct
    {
        private readonly Span<T> _components;
        private readonly Span<Guid> _entities;
        private readonly int _count;

        public ComponentSpan(T[] components, Guid[] entities, int count)
        {
            _components = new Span<T>(components, 0, count);
            _entities = new Span<Guid>(entities, 0, count);
            _count = count;
        }

        public int Length => _count;
        public ref T this[int index] => ref _components[index];
        public Guid GetEntity(int index) => _entities[index];
    }
}