using System;
using Unity.Collections;

namespace HOS.ECS.Component
{
    public struct NameComponent
    {
        public NameComponent(string name)
        {
            this.name = new(name);
        }

        public FixedString128Bytes name;

        public override string ToString()
        {
            return name.ToString();
        }
    }

    public struct ExtensionComponent
    {
        public ExtensionComponent(string extension)
        {
            this.extension = extension;
        }

        public FixedString32Bytes extension;

        public override string ToString()
        {
            return extension.ToString();
        }
    }

    public struct DirectoryChildComponent
    {
        public DirectoryChildComponent(Guid guid)
        {
            parentGuid = guid;
        }

        public Guid parentGuid;
    }

    public struct ReadonlyComponent
    {
        
    }
}
