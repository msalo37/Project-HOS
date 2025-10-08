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
        public ref T AddComponent<T>(Guid entityId, T component = default) where T : struct
        {
            return ref GetContainer<T>().Add(entityId, component);
        }

        // Получить компонент
        public ref T GetComponent<T>(Guid entityId) where T : struct
        {
            return ref GetContainer<T>().Get(entityId);
        }

        public bool TryGetComponent<T>(Guid entityId, out T component) where T : struct
        {
            return GetContainer<T>().TryGet(entityId, out component);
        }

        // Проверить наличие компонента
        public bool HasComponent<T>(Guid entityId) where T : struct
        {
            return GetContainer<T>().Has(entityId);
        }

        // Удалить компонент
        public void RemoveComponent<T>(Guid entityId) where T : struct
        {
            GetContainer<T>().Remove(entityId);
        }

        // Альтернативная версия без рефлексии (если добавим базовый интерфейс)
        public void RemoveAllComponentsFast(Guid entityId)
        {
            // Эта версия будет работать если добавим не-generic интерфейс
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

        // Быстрые запросы
        public IEnumerable<Guid> QueryEntitiesWith<T1, T2>() where T1 : struct where T2 : struct
        {
            var container1 = GetContainer<T1>();
            var container2 = GetContainer<T2>();
            
            var span = container1.GetSpan();
            for (int i = 0; i < span.Length; i++)
            {
                var entityId = span.GetEntity(i);
                if (container2.Has(entityId))
                {
                    yield return entityId;
                }
            }
        }
    }
}