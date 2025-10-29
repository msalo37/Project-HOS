using System;
using HOS.ECS.Component;
using HOS.Machines;
using UnityEngine;

namespace HOS.ECS.Systems
{
    public class FileNavigationSystem : SystemBase
    {
        public Guid ResolvePath(string path, Guid currentDirectory)
        {
            if (string.IsNullOrEmpty(path)) return Guid.Empty;
            if (path.StartsWith("/")) return ResolveAbsolute(path);

            Guid current = currentDirectory;
            var parts = path.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return current;

            var childContainer = ECS.Components.GetContainer<DirectoryChildComponent>();

            foreach (var part in parts)
            {
                if (part == ".") continue;
                if (part == "..")
                {
                    if (childContainer.TryGet(current, out var child))
                        current = child.parentGuid;
                    continue;
                }

                if (!TryFindByName(part, current, out Guid found))
                {
                    Debug.LogWarning($"Folder '{part}' not found");
                    return Guid.Empty;
                }
                current = found;
            }

            return current;
        }

        private Guid ResolveAbsolute(string path)
        {
            if (path == "/") return GlobalVars.GetDirectoryGuid(BasicDirectories.Main);
            var parts = path.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return Guid.Empty;

            Guid current = GlobalVars.GetDirectoryGuid(BasicDirectories.Main);
            foreach (var part in parts)
            {
                if (!TryFindByName(part, current, out Guid found))
                    return Guid.Empty;
                current = found;
            }
            return current;
        }

        private bool TryFindByName(string name, Guid dir, out Guid found)
        {
            return ECS.Systems.Get<FileNamingSystem>().TryFindByName(name, dir, out found);
        }
    }
}