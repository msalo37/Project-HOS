using System;
using UnityEngine;

namespace HOS.ECS.Entity
{
    [Serializable]
    public abstract class BaseEntity
    {
        public BaseEntity(Guid guid)
        {
            this.guid = guid;
        }

        public BaseEntity()
        {
            this.guid = Guid.NewGuid();
        }

        [SerializeField] protected Guid guid;
        public Guid GUID => guid;
    }
}