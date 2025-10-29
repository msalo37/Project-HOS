using System;

namespace HOS.Machines
{
    public class Context
    {
        public Context(GlobalVars globalVars, Computer computer)
        {
            this.globalVars = globalVars;
            OpenComputer(computer);
        }
        
        private GlobalVars globalVars;
        
        public Guid CurrentDirectory { private set; get; }
        public Computer CurrentComputer { private set; get; }

        public void OpenComputer(Computer computer)
        {
            CurrentComputer = computer;
            CurrentDirectory = GlobalVars.GetDirectoryGuid(BasicDirectories.Main);
        }
        
        public void OpenDirectory(Guid guid)
        {
            CurrentDirectory = guid;
            //HOSUtils.CreateLogFile($"Admin opened folder {HOSUtils.GetFileName(guid, CurrentComputer.FileSystem)}", CurrentComputer.FileSystem);
        }
    }
}