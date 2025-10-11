using System.Collections.Generic;
using System.Windows.Input;
using HOS.Machines;
using HOS.Utils;
using UnityEngine;

namespace HOS.Commands
{
    public class CommandController
    {
        public CommandController(Computer playerComputer, GlobalVars globalVars)
        {
            this.playerComputer = playerComputer;
            this.globalVars = globalVars;
            commandDict = new();
        }

        private GlobalVars globalVars;
        private Computer playerComputer;
        private Dictionary<string, ICommand> commandDict;

        public void RegisterCommand(string name, ICommand command)
        {
            commandDict.Add(name, command);
        }

        // check & execute:
        // 1. os commands
        // 2. folder bin exe
        // 3. current folder bin exe - if this our computer
        // 4. error

        public CommandExecutionExitCode TryExecute(string name, string[] args)
        {
            if (commandDict.TryGetValue(name, out var command))
            {
                command.Execute(args);
                return CommandExecutionExitCode.Success;
            }
            else if (HOSUtils.TryToFindByName(name, globalVars.GetDirectoryGuid(GlobalVars.BasicDirectories.Bin), playerComputer.FileSystem, out var fileGuid))
            {
                // todo execute command with lua if file contains IFileContent & extension is lua
            }
            else // todo if current computer is playerComputer we can execute programs from current folder
            {
                return CommandExecutionExitCode.CommandNotExist;
            }

            return CommandExecutionExitCode.UnknownError;
        }

        public enum CommandExecutionExitCode
        {
            Success,
            FileNotExecutable,
            CommandNotExist,
            UnknownError,
        }
    }
}