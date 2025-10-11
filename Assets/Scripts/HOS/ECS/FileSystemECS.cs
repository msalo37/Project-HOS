using System;
using HOS.ECS.Component;
using HOS.ECS.Entity;

namespace HOS.ECS
{
    public class FileSystemECS
    {
        public FileSystemECS()
        {
            entityContainer = new EntityContainer();
            componentRegistry = new ComponentRegistry();
        }

        private IEntityContainer entityContainer;
        private ComponentRegistry componentRegistry;

        public IEntityContainer EntityContainer => entityContainer;
        public ComponentRegistry ComponentRegistry => componentRegistry;
    }
}