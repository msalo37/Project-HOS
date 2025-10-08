using System;

namespace HOS.ECS.Entity
{
    public struct TypeFilter : IEntityFilter
    {
        public TypeFilter(Type type)
        {
            CurrentType = type;
        }

        public Type CurrentType { private set; get; }

        public bool TryPassFilter(BaseEntity baseEntity)
        {
            return baseEntity.GetType() == CurrentType;
        }
    }
}