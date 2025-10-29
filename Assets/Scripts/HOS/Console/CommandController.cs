using System;
using System.Collections.Generic;
using HOS.ECS.Meta;
using HOS.ECS.Systems;
using HOS.Lua;
using HOS.Machines;

namespace HOS.Commands
{
    public class CommandController
    {
        public CommandController(Computer playerComputer, HOSLuaVM luaVM)
        {
            this.luaVM = luaVM;
            this.playerComputer = playerComputer;
            commandDict = new();
            
            maxExitCode = Enum.GetValues(typeof(CommandExecutionExitCode)).Length;
        }

        private HOSLuaVM luaVM;
        private Computer playerComputer;
        private Dictionary<string, ICommand> commandDict;
        private static readonly string[] executableExtenstions = { "lua", "exe" };
        private readonly int maxExitCode;

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
                return command.Execute(args);
            }
            else if (playerComputer.FileSystem.Systems.Get<FileNamingSystem>().TryFindByNameWithoutExtension(name, GlobalVars.GetDirectoryGuid(BasicDirectories.Bin), out var fileGuid))
            {
                if (playerComputer.FileSystem.Systems.Get<FileExtensionSystem>().HasExtension(fileGuid, executableExtenstions) &&
                    playerComputer.FileSystem.Components.TryGetComponent(fileGuid, out FileContentComponent fileContent))
                {
                    string content = playerComputer.FileSystem.Meta.GetMeta(fileGuid, fileContent.metaKey).ToString();
                    int exitCode = luaVM.RunLua(content, args);
                    return (CommandExecutionExitCode)exitCode;
                }
            }
            else // todo if current computer is playerComputer we can execute programs from current folder
            {
                return CommandExecutionExitCode.CommandNotExist;
            }

            return CommandExecutionExitCode.CommandNotExist;
        }
    }

    public struct FileContentComponent
    {
        public FileContentComponent(MetaKey metaKey)
        {
            this.metaKey = metaKey;
        }

        public MetaKey metaKey;
    }
}