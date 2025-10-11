using System.IO;
using System;
using HOS.ECS;
using UnityEngine;

namespace HOS.FileSystem
{
    public class FileSystem
    {
        public FileSystem(FileSystemECS ecs)
        {
            this.ecs = ecs;
            CurrentDirectory = Guid.Empty;
        }

        private FileSystemECS ecs;

        public Guid CurrentDirectory { private get; set; }

        
    }

    public struct ParentDirectoryComponent
    {
        public Guid directoryGuid;
    }
}