using System;
using System.Collections.Generic;
using HOS.Commands;
using HOS.ECS.Component;
using HOS.ECS.Entity;
using HOS.Machines;
using UnityEngine.Pool;

namespace HOS.ECS.Systems
{
    public class FileDeletionSystem : SystemBase
    {
        private readonly List<Guid> _toDelete = new();

        public CommandExecutionExitCode DeletePath(string path, Guid currentDirectory)
        {
            if (string.IsNullOrEmpty(path))
                return CommandExecutionExitCode.InvalidArguments;

            var navSystem = ECS.Systems.Get<FileNavigationSystem>();
            var pathSystem = ECS.Systems.Get<FilePathSystem>();
            var extSystem = ECS.Systems.Get<FileExtensionSystem>();

            bool isWildcard = path.EndsWith("/*");
            string cleanPath = isWildcard ? path.Substring(0, path.Length - 2) : path;

            // Разрешаем путь
            Guid targetGuid = navSystem.ResolvePath(cleanPath, currentDirectory);
            if (targetGuid == Guid.Empty)
                return CommandExecutionExitCode.FileOrDirectoryNotExist;

            // Если есть Readonly — отказ
            if (extSystem.IsReadonly(targetGuid))
                return CommandExecutionExitCode.AccessDenied;

            if (isWildcard)
            {
                return DeleteDirectoryContents(targetGuid);
            }
            else
            {
                return DeleteSingle(targetGuid, pathSystem);
            }
        }

        private CommandExecutionExitCode DeleteDirectoryContents(Guid dirGuid)
        {
            var pathSystem = ECS.Systems.Get<FilePathSystem>();
            var list = ListPool<Guid>.Get();
            pathSystem.GetChildren(dirGuid, list);

            int failed = 0;
            foreach (var childGuid in list)
            {
                var code = DeleteRecursive(childGuid);
                if (code != CommandExecutionExitCode.Success)
                    failed++;
            }

            ListPool<Guid>.Release(list);
            return failed == 0 ? CommandExecutionExitCode.Success : CommandExecutionExitCode.RecursiveDeleteFailed;
        }

        private CommandExecutionExitCode DeleteSingle(Guid entityGuid, FilePathSystem pathSys)
        {
            // Проверяем, папка ли это
            var entity = ECS.Entities.GetReference(entityGuid);
            bool isDirectory = entity is DirectoryEntity;

            if (isDirectory)
            {
                var list = ListPool<Guid>.Get();
                pathSys.GetChildren(entityGuid, list);
                bool hasChildren = list.Count > 0;
                ListPool<Guid>.Release(list);

                if (hasChildren)
                    return CommandExecutionExitCode.DirectoryIsNotEmpty;
            }

            return DeleteEntity(entityGuid);
        }

        private CommandExecutionExitCode DeleteRecursive(Guid entityGuid)
        {
            var querySystem = ECS.Systems.Get<FileExtensionSystem>();
            if (querySystem.IsReadonly(entityGuid))
                return CommandExecutionExitCode.AccessDenied;

            var entity = ECS.Entities.GetReference(entityGuid);
            bool isDirectory = entity is DirectoryEntity;

            if (isDirectory)
            {
                var pathSystem = ECS.Systems.Get<FilePathSystem>();
                var children = ListPool<Guid>.Get();
                pathSystem.GetChildren(entityGuid, children);

                foreach (var child in children)
                {
                    var code = DeleteRecursive(child);
                    if (code != CommandExecutionExitCode.Success)
                    {
                        ListPool<Guid>.Release(children);
                        return code;
                    }
                }
                ListPool<Guid>.Release(children);
            }

            return DeleteEntity(entityGuid);
        }

        private CommandExecutionExitCode DeleteEntity(Guid entityGuid)
        {
            ECS.Components.RemoveAllComponentsFast(entityGuid);
            ECS.Entities.Remove(entityGuid);

            return CommandExecutionExitCode.Success;
        }
    }
}