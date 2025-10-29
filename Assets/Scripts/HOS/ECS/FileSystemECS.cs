using HOS.ECS.Component;
using HOS.ECS.Entity;
using HOS.ECS.Meta;
using HOS.ECS.Systems;

namespace HOS.ECS
{
    public class FileSystemECS
    {
        public FileSystemECS()
        {
            entityContainer = new EntityContainer();
            componentRegistry = new ComponentRegistry();
            metaHandler = new MetaHandler();
            systemRegistry = new SystemRegistry(this);
        }

        public void Update(float deltaTime)
        {
            systemRegistry.UpdateAll(deltaTime);
        }

        private IEntityContainer entityContainer;
        private ComponentRegistry componentRegistry;
        private MetaHandler metaHandler;
        private SystemRegistry systemRegistry;

        public IEntityContainer Entities => entityContainer;
        public ComponentRegistry Components => componentRegistry;
        public MetaHandler Meta => metaHandler;
        public SystemRegistry Systems => systemRegistry;
    }
}