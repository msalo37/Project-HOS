using System;
using System.Collections.Generic;

namespace HOS.Machines
{
    public class GlobalVars
    {
        private static Dictionary<BasicDirectories, Guid> folderDict;

        public GlobalVars()
        {
            folderDict = new();
            foreach (BasicDirectories directory in Enum.GetValues(typeof(BasicDirectories)))
            {
                folderDict.Add(directory, Guid.NewGuid());
            }
        }

        public static IEnumerable<KeyValuePair<BasicDirectories, Guid>> EnumerateDirectories()
        {
            foreach (var kvp in folderDict)
                yield return kvp;
        }

        public static Guid GetDirectoryGuid(BasicDirectories directory)
        {
            return folderDict[directory];
        }

        private static readonly List<string> ReservedNames = new() { ".", "..", "/", "*" };

        public static bool IsNameReserved(string name)
        {
            return ReservedNames.Contains(name);
        }
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