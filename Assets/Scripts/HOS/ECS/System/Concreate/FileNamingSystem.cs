using System;
using HOS.ECS.Component;
using HOS.ECS.Entity;
using UnityEngine;

namespace HOS.ECS.Systems
{
    public class FileNamingSystem : SystemBase
    {
        public string GetFullName(Guid entityId)
        {
            var name = GetName(entityId);
            var ext = ECS.Components.TryGetComponent<ExtensionComponent>(entityId, out var extComp)
                ? $".{extComp.extension}"
                : "";

            return name + ext;
        }

        public string GetName(Guid entityId)
        {
            if (ECS.Components.TryGetComponent<NameComponent>(entityId, out var nameComp))
                return nameComp.name.ToString();

            Debug.LogError($"Entity {entityId} has no NameComponent");
            return "[NO NAME]";
        }

        public bool TryFindByNameWithoutExtension(string name, Guid dir, out Guid found)
        {
            found = Guid.Empty;
            var childrenContainer = ECS.Components.GetContainer<DirectoryChildComponent>();
            
            foreach (var (entityId, child) in childrenContainer.EnumerateAll())
            {
                if (child.parentGuid == dir &&
                    GetName(entityId) == name)
                {
                    found = entityId;
                    return true;
                }
            }
            return false;
        }
        
        public bool TryFindByName(string name, Guid dir, out Guid found)
        {
            found = Guid.Empty;
            var childrenContainer = ECS.Components.GetContainer<DirectoryChildComponent>();
            
            foreach (var (entityId, child) in childrenContainer.EnumerateAll())
            {
                if (child.parentGuid == dir &&
                    GetFullName(entityId) == name)
                {
                    found = entityId;
                    return true;
                }
            }
            return false;
        }
        
        public bool DirectoryExists(string name, Guid directory)
        {
            if (TryFindByName(name, directory, out Guid found))
            {
                return ECS.Entities.GetReference(found) is DirectoryEntity;
            }

            return false;
        }

        public bool FileExists(string name, Guid directory)
        {
            if (TryFindByName(name, directory, out Guid found))
            {
                return ECS.Entities.GetReference(found) is FileEntity;
            }

            return false;
        }
    }
}