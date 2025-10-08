using System;
using System.Collections.Generic;

namespace HOS.ECS.Entity
{
    public interface IEntityContainer
    {
        public void Add(BaseEntity entity);
        public void Remove(Guid guid);

        public BaseEntity GetReference(Guid guid);
        public bool TryGetReference(Guid guid, out BaseEntity baseEntity);
        public IEnumerable<BaseEntity> EnumerateAll(IEntityFilter filter);
    }
}