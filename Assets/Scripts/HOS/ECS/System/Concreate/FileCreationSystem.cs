using System;
using HOS.Commands;
using HOS.ECS.Component;
using HOS.ECS.Entity;
using HOS.Machines;

namespace HOS.ECS.Systems
{
    public class FileCreationSystem : SystemBase
    {
        public CommandExecutionExitCode CreateDirectoryRecursive(string path, Guid currentDir)
        {
            if (string.IsNullOrEmpty(path)) return CommandExecutionExitCode.InvalidArguments;

            var namingSystem = ECS.Systems.Get<FileNamingSystem>();
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (path.StartsWith("/"))
                currentDir = GlobalVars.GetDirectoryGuid(BasicDirectories.Main);
            
            var childContainer = ECS.Components.GetContainer<DirectoryChildComponent>();

            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part == ".") continue;

                if (part == "..")
                {
                    if (childContainer.TryGet(currentDir, out var child))
                        currentDir = child.parentGuid;
                    continue;
                }

                if (namingSystem.TryFindByName(part, currentDir, out Guid foundGuid))
                {
                    currentDir = foundGuid;

                    if (i == parts.Length - 1)
                    {
                        return CommandExecutionExitCode.DirectoryAlreadyExists;
                    }
                    
                    continue;
                }

                var exitCode = TryCreateDirectory(part, currentDir, out currentDir);
                if (exitCode != CommandExecutionExitCode.Success)
                {
                    return exitCode;
                }
            }

            return CommandExecutionExitCode.Success;
        }
        
        public CommandExecutionExitCode TryCreateDirectory(string directoryName, Guid directory, out Guid createdDirectory)
        {
            createdDirectory = Guid.Empty;

            if (ECS.Systems.Get<FileNamingSystem>().DirectoryExists(directoryName, directory))
                return CommandExecutionExitCode.DirectoryAlreadyExists;

            if (GlobalVars.IsNameReserved(directoryName))
                return CommandExecutionExitCode.FileOrDirectoryNameReserved;
            
            createdDirectory = CreateDirectory(directoryName, directory);
            return CommandExecutionExitCode.Success;
        }

        private Guid CreateDirectory(string directoryName, Guid directory)
        {
            var newDir = new DirectoryEntity();
            ECS.Entities.Add(newDir);
            
            ECS.Components.AddComponent(newDir.GUID, new NameComponent(directoryName));
            ECS.Components.AddComponent(newDir.GUID, new DirectoryChildComponent(directory));
            
            return newDir.GUID;
        }
    }
}