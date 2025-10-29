using System;
using HOS.ECS.Component;

namespace HOS.ECS.Systems
{
    public class FileExtensionSystem : SystemBase
    {
        public bool HasExtension(Guid entityId, params string[] extensions)
        {
            if (!ECS.Components.TryGetComponent<ExtensionComponent>(entityId, out var ext))
                return false;

            foreach (var e in extensions)
                if (e == ext.extension.ToString())
                    return true;

            return false;
        }

        public bool IsReadonly(Guid entityId)
        {
            return ECS.Components.HasComponent<ReadonlyComponent>(entityId);
        }
    }
}