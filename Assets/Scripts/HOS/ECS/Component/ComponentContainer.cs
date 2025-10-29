using System;
using System.Collections.Generic;

namespace HOS.ECS.Component
{
    public class ComponentContainer<T> : IComponentContainer<T> where T : struct
    {
        private T[] _components;
        private Guid[] _indexToEntity;
        private Dictionary<Guid, int> _entityToIndex;
        private int _count;

        public Type ComponentType => typeof(T);
        public int Count => _count;

        public ComponentContainer(int initialCapacity = 1024)
        {
            _components = new T[initialCapacity];
            _indexToEntity = new Guid[initialCapacity];
            _entityToIndex = new Dictionary<Guid, int>(initialCapacity);
        }

        public ref T Add(Guid entityId, T component = default)
        {
            if (_entityToIndex.ContainsKey(entityId))
                throw new InvalidOperationException($"Entity {entityId} already has component {typeof(T).Name}");

            if (_count >= _components.Length)
                Resize(_components.Length * 2);

            int index = _count++;
            _components[index] = component;
            _indexToEntity[index] = entityId;
            _entityToIndex[entityId] = index;

            return ref _components[index];
        }

        public ref T Get(Guid entityId)
        {
            if (!_entityToIndex.TryGetValue(entityId, out int index))
                throw new KeyNotFoundException($"Entity {entityId} doesn't have component {typeof(T).Name}");

            return ref _components[index];
        }

        public bool TryGet(Guid entityId, out T component)
        {
            if (_entityToIndex.TryGetValue(entityId, out int index))
            {
                component = _components[index];
                return true;
            }
            component = default;
            return false;
        }

        public bool Has(Guid entityId) => _entityToIndex.ContainsKey(entityId);

        public void Remove(Guid entityId)
        {
            if (!_entityToIndex.TryGetValue(entityId, out int index))
                return;

            // Swap remove
            if (index < _count - 1)
            {
                _components[index] = _components[_count - 1];
                Guid movedEntityId = _indexToEntity[_count - 1];
                _indexToEntity[index] = movedEntityId;
                _entityToIndex[movedEntityId] = index;
            }

            _entityToIndex.Remove(entityId);
            _count--;
        }

        public ComponentSpan<T> GetSpan() => new ComponentSpan<T>(_components, _indexToEntity, _count);

        private void Resize(int newCapacity)
        {
            Array.Resize(ref _components, newCapacity);
            Array.Resize(ref _indexToEntity, newCapacity);
        }

        public IEnumerable<(Guid, T)> EnumerateAll()
        {
            foreach (var kvp in _entityToIndex)
            {
                yield return (kvp.Key, _components[kvp.Value]);
            }
        }
    }
}