using System;
using System.Collections.Generic;

namespace HOS.ECS.Component
{
    public class ComponentRegistry
    {
        private readonly Dictionary<Type, object> _containers = new Dictionary<Type, object>();
        private readonly object _lock = new object();

        // Получить контейнер для типа компонента
        public IComponentContainer<T> GetContainer<T>() where T : struct
        {
            var type = typeof(T);
            
            lock (_lock)
            {
                if (!_containers.TryGetValue(type, out var container))
                {
                    container = new ComponentContainer<T>();
                    _containers[type] = container;
                }
                return (IComponentContainer<T>)container;
            }
        }

        // Добавить компонент
        public ref T AddComponent<T>(Guid entityId, T component = default) where T : unmanaged
        {
            return ref GetContainer<T>().Add(entityId, component);
        }

        // Получить компонент
        public ref T GetComponent<T>(Guid entityId) where T : unmanaged
        {
            return ref GetContainer<T>().Get(entityId);
        }

        public bool TryGetComponent<T>(Guid entityId, out T component) where T : unmanaged
        {
            return GetContainer<T>().TryGet(entityId, out component);
        }

        // Проверить наличие компонента
        public bool HasComponent<T>(Guid entityId) where T : unmanaged
        {
            return GetContainer<T>().Has(entityId);
        }

        // Удалить компонент
        public void RemoveComponent<T>(Guid entityId) where T : unmanaged
        {
            GetContainer<T>().Remove(entityId);
        }

        public void RemoveAllComponentsFast(Guid entityId)
        {
            foreach (var container in _containers.Values)
            {
                if (container is IComponentContainerBase baseContainer)
                {
                    baseContainer.Remove(entityId);
                }
            }
        }

        public (int ContainerCount, int TotalComponents) GetStats()
        {
            int totalComponents = 0;
            foreach (var (componentType, container) in _containers)
            {
                if (container is IComponentContainerBase containerBase)
                {
                    totalComponents += containerBase.Count;
                }
            }
            return (_containers.Count, totalComponents);
        }

        public void GetComponentTypes(Guid entityId, List<Type> noAllocList)
        {
            noAllocList.Clear();
            foreach (var (type, container) in _containers)
            {
                if (container is IComponentContainerBase containerBase && containerBase.Has(entityId))
                {
                    noAllocList.Add(type);
                }
            }
        }

        // Дополнительные оптимизированные методы
        public int GetComponentCount<T>() where T : struct
        {
            var container = GetContainer<T>();
            var span = container.GetSpan();
            return span.Length;
        }
    }
}