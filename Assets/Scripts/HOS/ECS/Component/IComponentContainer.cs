using System;
using Unity.VisualScripting;

namespace HOS.ECS.Component
{
    public interface IComponentContainer<T> : IComponentContainerBase where T : struct
    {
        // Типизированные методы
        ref T Add(Guid entityId, T component = default);
        ref T Get(Guid entityId);
        bool TryGet(Guid entityId, out T component);

        // Эффективный доступ
        ComponentSpan<T> GetSpan();
    }

    public interface IComponentContainerBase
    {
        void Remove(Guid entityId);
        bool Has(Guid entityId);
        int Count { get; }
    }
}