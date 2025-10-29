namespace HOS.ECS.Systems
{
    public abstract class SystemBase : ISystem
    {
        protected FileSystemECS ECS { get; private set; }
        
        public void Initialize(FileSystemECS ecs)
        {
            ECS = ecs;
        }

        public virtual void Update(float deltaTime) { }
    }
}