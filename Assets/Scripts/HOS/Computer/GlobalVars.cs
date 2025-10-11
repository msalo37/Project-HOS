using System;
using System.Collections.Generic;

namespace HOS.Machines
{
    public class GlobalVars
    {
        private Dictionary<BasicDirectories, Guid> folderDict;

        public GlobalVars()
        {
            folderDict = new();
            foreach (BasicDirectories directory in Enum.GetValues(typeof(BasicDirectories)))
            {
                folderDict.Add(directory, Guid.NewGuid());
            }
        }

        public IEnumerable<KeyValuePair<BasicDirectories, Guid>> EnumerateDirectories()
        {
            foreach (var kvp in folderDict)
                yield return kvp;
        }

        public Guid GetDirectoryGuid(BasicDirectories directory)
        {
            return folderDict[directory];
        }

        public enum BasicDirectories
        {
            Main = 0,
            Bin = 1,
            Home = 2,
            Log = 3,
            WWW = 4,
        }
    }
}