namespace HOS.ECS.Systems
{
    public interface ISystem
    {
        void Initialize(FileSystemECS ecs);
        void Update(float deltaTime);
    }
}