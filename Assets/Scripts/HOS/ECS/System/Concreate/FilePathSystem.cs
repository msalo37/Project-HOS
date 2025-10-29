using System;
using System.Collections.Generic;
using System.Text;
using HOS.ECS.Component;
using UnityEngine.Pool;

namespace HOS.ECS.Systems
{
    public class FilePathSystem : SystemBase
    {
        private readonly StringBuilder stringBuilder = new();

        public string GetPath(Guid entityId)
        {
            var list = ListPool<string>.Get();
            var childContainer = ECS.Components.GetContainer<DirectoryChildComponent>();

            stringBuilder.Clear();
            Guid current = entityId;
            var namingSystem = ECS.Systems.Get<FileNamingSystem>();
            
            list.Add(namingSystem.GetFullName(current));

            while (childContainer.TryGet(current, out var child))
            {
                current = child.parentGuid;
                list.Add(namingSystem.GetName(current));
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                stringBuilder.Append(list[i]);
                if (i > 0) stringBuilder.Append('/');
            }

            ListPool<string>.Release(list);
            return stringBuilder.ToString();
        }

        public void GetChildren(Guid dirId, List<Guid> output)
        {
            output.Clear();
            var container = ECS.Components.GetContainer<DirectoryChildComponent>();
            foreach (var (entityId, child) in container.EnumerateAll())
            {
                if (child.parentGuid == dirId)
                    output.Add(entityId);
            }
        }
    }
}