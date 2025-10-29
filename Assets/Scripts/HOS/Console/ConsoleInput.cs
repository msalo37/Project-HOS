using System.Collections.Generic;
using System.Text.RegularExpressions;
using HOS.Commands;
using UnityEngine;

namespace HOS.Console
{
    public class ConsoleInput
    {  
        public ConsoleInput(PlayerConsole playerConsole, CommandController commandController)
        {
            this.playerConsole = playerConsole;
            this.commandController = commandController;
        }

        private PlayerConsole playerConsole;
        private CommandController commandController;
        private List<string> tokens = new();

        private const string RegexPattern = @"(?:""([^""]*)""|'([^']*)'|(\S+))";

        public void ProcessInput(string input)
        {
            playerConsole.AddLine(input);
            
            if (string.IsNullOrWhiteSpace(input))
            {
                playerConsole.AddLine(string.Empty);
                return;
            }

            var matches = Regex.Matches(input, RegexPattern);
            tokens.Clear();

            foreach (Match m in matches)
            {
                // Берём первую непустую группу
                if (m.Groups[1].Success)
                    tokens.Add(m.Groups[1].Value);
                else if (m.Groups[2].Success)
                    tokens.Add(m.Groups[2].Value);
                else
                    tokens.Add(m.Groups[3].Value);
            }

            string command = tokens[0];

            string[] args = null;

            if (tokens.Count > 1)
            {
                args = new string[tokens.Count - 1];
                for (int i = 1; i < tokens.Count; i++)
                    args[i - 1] = tokens[i];
            }

            var exitCode = commandController.TryExecute(command, args);

            if (exitCode is not CommandExecutionExitCode.Success)
            {
                playerConsole.AddLine($"Command execution error: {exitCode}");
            }
            
            Debug.Log(exitCode);
        }
    }
}
