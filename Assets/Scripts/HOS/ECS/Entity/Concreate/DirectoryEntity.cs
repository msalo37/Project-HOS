using System;
using HOS.ECS.Entity;
using UnityEngine;

namespace HOS.ECS.Entity
{
    public class DirectoryEntity : BaseEntity
    {
        public DirectoryEntity()
        {
        }

        public DirectoryEntity(Guid guid) : base(guid)
        {
        }
    }

    public class FileEntity : BaseEntity
    {
        public FileEntity()
        {
        }

        public FileEntity(Guid guid) : base(guid)
        {
        }
    }
}