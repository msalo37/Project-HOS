using System;
using System.Collections.Generic;

namespace HOS.ECS.Entity
{
    public class EntityContainer : IEntityContainer
    {
        public EntityContainer()
        {
            data = new();
        }

        private Dictionary<Type, Dictionary<Guid, BaseEntity>> data;

        private Dictionary<Guid, BaseEntity> GetDictForType(Type type)
        {
            if (data.TryGetValue(type, out var result))
                return result;

            Dictionary<Guid, BaseEntity> newDict = new();
            data.Add(type, newDict);
            return newDict;
        }

        private bool TryToFindReference(Guid guid, out BaseEntity entity, out Dictionary<Guid, BaseEntity> entDict)
        {
            entity = null;
            entDict = null;

            foreach (var dict in data.Values)
                if (dict.TryGetValue(guid, out var result))
                {
                    entDict = dict;
                    return result != null;
                }

            return false;
        }

        public void Add(BaseEntity entity)
        {
            var type = entity.GetType();
            GetDictForType(type).Add(entity.GUID, entity);
        }

        public BaseEntity GetReference(Guid guid)
        {
            if (TryGetReference(guid, out var result))
                return result;
            return null;
        }

        public void Remove(Guid guid)
        {
            if (TryToFindReference(guid, out _, out var dict))
            {
                dict.Remove(guid);
            }
        }

        public bool TryGetReference(Guid guid, out BaseEntity entity)
        {
            return TryToFindReference(guid, out entity, out _);
        }

        public IEnumerable<BaseEntity> EnumerateAll(IEntityFilter filter)
        {
            if (filter is TypeFilter typeFilter && data.TryGetValue(typeFilter.CurrentType, out var dict))
            {
                foreach (var entity in dict.Values)
                    yield return entity;
            }
            else
            {
                foreach (var dictEnt in data.Values)
                    foreach (var entity in dictEnt.Values)
                        if (filter.TryPassFilter(entity))
                            yield return entity;
            }
        }
    }
}