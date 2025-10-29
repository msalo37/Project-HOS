using System;
using Unity.Collections;
using UnityEngine;

namespace HOS.Commands
{
    public interface ICommand
    {
        public CommandExecutionExitCode Execute(string[] args);
    }
    
    public enum CommandExecutionExitCode
    {
        Success = 0,
        FileNotExecutable = 1,
        CommandNotExist = 2,
        UnknownError = 3,
        InvalidArguments = 4,
        FileOrDirectoryNotExist = 5,
        DirectoryAlreadyExists = 6,
        FileOrDirectoryNameReserved = 7,
        DirectoryIsNotEmpty = 8,
        AccessDenied = 9,
        RecursiveDeleteFailed = 10,
    }

    public delegate CommandExecutionExitCode CommandExecution(string[] args);
    
    public class FunctionCommand : ICommand
    {
        public FunctionCommand(CommandExecution func)
        {
            this.func = func;
        }

        private CommandExecution func;

        public CommandExecutionExitCode Execute(string[] args)
        {
            return func(args);
        }
    }
}