using System;
using System.Collections.Generic;
using System.Text;
using HOS.ECS;
using HOS.ECS.Component;
using UnityEngine;
using UnityEngine.Pool;

namespace HOS.Utils
{
    public static class HOSUtils
    {
        private static StringBuilder stringBuilder = new();
        private const string FileNameError = "NO FILE NAME";

        public static string GetFullFileName(Guid fileGuid, FileSystemECS ecs)
        {
            stringBuilder.Clear();

            stringBuilder.Append(GetFileName(fileGuid, ecs));

            if (ecs.ComponentRegistry.TryGetComponent<ExtensionComponent>(fileGuid, out var extComp))
            {
                stringBuilder.Append('.');
                stringBuilder.Append(extComp.ToString());
            }

            return stringBuilder.ToString();
        }

        public static string GetFileName(Guid fileGuid, FileSystemECS ecs)
        {
            if (ecs.ComponentRegistry.TryGetComponent<NameComponent>(fileGuid, out var nameComp))
            {
                return nameComp.ToString();
            }
            Debug.LogError("File doesn't have name!");
            return FileNameError;
        }

        public static string GetPath(Guid fileGuid, FileSystemECS ecs)
        {
            var list = ListPool<string>.Get();

            var childContainer = ecs.ComponentRegistry.GetContainer<DirectoryChildComponent>();

            DirectoryChildComponent childComponent;
            Guid checkGuid = fileGuid;
            list.Add(GetFullFileName(fileGuid, ecs));

            while (childContainer.TryGet(checkGuid, out childComponent))
            {
                checkGuid = childComponent.parentGuid;
                list.Add(GetFileName(checkGuid, ecs));
            }

            stringBuilder.Clear();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                stringBuilder.Append(list[i]);
                if (i < list.Count - 1)
                    stringBuilder.Append("/");
            }
            ListPool<string>.Release(list);
            return stringBuilder.ToString();
        }

        public static void GetFiles(Guid dirGuid, FileSystemECS ecs, List<Guid> noAllocList)
        {
            noAllocList.Clear();
            var container = ecs.ComponentRegistry.GetContainer<DirectoryChildComponent>();
            foreach (var kvp in container.EnumerateAll())
            {
                if (kvp.Item2.parentGuid == dirGuid)
                    noAllocList.Add(kvp.Item1);
            }
        }

        public static bool TryToFindByName(string name, Guid directoryGuid, FileSystemECS ecs, out Guid foundGuid)
        {
            foundGuid = Guid.Empty;
            var childContainer = ecs.ComponentRegistry.GetContainer<DirectoryChildComponent>();
            var nameContainer = ecs.ComponentRegistry.GetContainer<NameComponent>();

            foreach (var child in childContainer.EnumerateAll())
            {
                if (child.Item2.parentGuid == directoryGuid &&
                nameContainer.TryGet(child.Item1, out var nameComp) &&
                nameComp.ToString().Equals(name))
                {
                    foundGuid = child.Item1;
                    return true;
                }
            }
            return false;
        }
    }    
}