using System;
using HOS.ECS;
using HOS.ECS.Component;
using HOS.ECS.Entity;
using Zenject;

namespace HOS.Machines
{
    public class Computer
    {
        public Computer(GlobalVars globalVars)
        {
            this.globalVars = globalVars;

            FileSystem = new();

            var nameContainer = FileSystem.ComponentRegistry.GetContainer<NameComponent>();
            var readonlyContainer = FileSystem.ComponentRegistry.GetContainer<ReadonlyComponent>();
            var folderContainer = FileSystem.ComponentRegistry.GetContainer<DirectoryChildComponent>();

            foreach (var kvp in globalVars.EnumerateDirectories())
            {
                FileSystem.EntityContainer.Add(new DirectoryEntity(kvp.Value));
                readonlyContainer.Add(kvp.Value, new());

                if (kvp.Key == GlobalVars.BasicDirectories.Main)
                {
                    nameContainer.Add(kvp.Value, new NameComponent("/"));
                }
                else
                {
                    nameContainer.Add(kvp.Value, new NameComponent(Enum.GetName(typeof(GlobalVars.BasicDirectories), kvp.Key).ToLower()));
                    folderContainer.Add(kvp.Value, new DirectoryChildComponent(globalVars.GetDirectoryGuid(GlobalVars.BasicDirectories.Main)));
                }
            }

            CurrentDirectory = globalVars.GetDirectoryGuid(GlobalVars.BasicDirectories.Main);
        }

        public void OpenDirectory(Guid newDirectory)
        {
            CurrentDirectory = newDirectory;
        }

        public FileSystemECS FileSystem { private set; get; }
        public Guid CurrentDirectory {private set; get; }
    
        private GlobalVars globalVars;
    }
}