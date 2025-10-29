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

            var nameContainer = FileSystem.Components.GetContainer<NameComponent>();
            var readonlyContainer = FileSystem.Components.GetContainer<ReadonlyComponent>();
            var folderContainer = FileSystem.Components.GetContainer<DirectoryChildComponent>();

            foreach (var kvp in GlobalVars.EnumerateDirectories())
            {
                FileSystem.Entities.Add(new DirectoryEntity(kvp.Value));
                readonlyContainer.Add(kvp.Value, new());

                if (kvp.Key == BasicDirectories.Main)
                {
                    nameContainer.Add(kvp.Value, new NameComponent("/"));
                }
                else
                {
                    nameContainer.Add(kvp.Value, new NameComponent(Enum.GetName(typeof(BasicDirectories), kvp.Key)?.ToLower()));
                    folderContainer.Add(kvp.Value, new DirectoryChildComponent(GlobalVars.GetDirectoryGuid(BasicDirectories.Main)));
                }
            }

            CurrentDirectory = GlobalVars.GetDirectoryGuid(BasicDirectories.Main);
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