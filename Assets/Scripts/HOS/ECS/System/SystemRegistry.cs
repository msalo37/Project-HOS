using System;
using System.Collections.Generic;

namespace HOS.ECS.Systems
{
    public class SystemRegistry
    {
        public SystemRegistry(FileSystemECS ecs)
        {
            this.ecs = ecs;
        }
        
        private readonly Dictionary<Type, ISystem> systems = new();
        private FileSystemECS ecs;

        public T Get<T>() where T : class, ISystem
        {
            if (systems.TryGetValue(typeof(T), out var system))
                return (T)system;

            var instance = Activator.CreateInstance<T>();
            instance.Initialize(ecs);
            systems[typeof(T)] = instance;
            return (T)instance;
        }

        public void UpdateAll(float deltaTime)
        {
            foreach (var system in systems.Values)
                system.Update(deltaTime);
        }
    }
}