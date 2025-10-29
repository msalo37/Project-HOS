using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HOS.Console
{
    public class PlayerConsole
    {
        private const int MaxLines = 256;

        private Queue<string> consoleQueue = new();
        private StringBuilder stringBuilder = new();
        private bool isDirty;

        public event Action ConsoleUpdated;

        public void Clear()
        {
            consoleQueue.Clear();
            SetDirty();
        }

        public void AddLine(string line)
        {
            if (consoleQueue.Count >= MaxLines)
                consoleQueue.Dequeue();
            consoleQueue.Enqueue(line);
            SetDirty();
        }

        private void SetDirty()
        {
            isDirty = true;
            ConsoleUpdated?.Invoke();
        }

        public override string ToString()
        {
            if (isDirty)
            {
                stringBuilder.Clear();

                foreach (var str in consoleQueue)
                {
                    stringBuilder.Append(str);
                    stringBuilder.Append("\n");
                }
                isDirty = false;
            }

            return stringBuilder.ToString();
        }
    }
}
