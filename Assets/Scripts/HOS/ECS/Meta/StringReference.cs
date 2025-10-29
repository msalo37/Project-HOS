using System;

namespace HOS.ECS.Meta
{
    [Serializable]
    public class StringReference : IMetaData
    {
        public StringReference(string value)
        {
            this.value = value;
        }

        public string value;

        public override string ToString()
        {
            return value;
        }
    }
}