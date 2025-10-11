using HOS.Commands;
using UnityEngine;

namespace HOS.Console
{
    public class ConsoleInput
    {  
        public CommandController()
        {
            
        }

        private Console console;

        public void ProcessInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                console.AddLine(string.Empty);
                return;
            }

            string[] arr = input.Split(' ');

            string command = arr[0];
            string[] args = null;

            if (arr.Length > 1)
            {
                args = new string[arr.Length - 1];
                for (int i = 1; i < arr.Length; i++)
                    args[i - 1] = arr[i];
            }
        }
    }
}
