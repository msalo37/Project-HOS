using System.Collections.Generic;

namespace HOS.Unity.Terminal
{
    public sealed class TerminalCommandHistory
    {
        private readonly List<string> entries = new List<string>();
        private readonly int capacity;
        private int index;
        private string draft = string.Empty;

        public TerminalCommandHistory(int capacity = 128)
        {
            this.capacity = capacity > 0 ? capacity : 1;
        }

        public void Add(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                ResetNavigation();
                return;
            }

            if (entries.Count == 0 || entries[entries.Count - 1] != command)
                entries.Add(command);
            if (entries.Count > capacity)
                entries.RemoveAt(0);
            ResetNavigation();
        }

        public bool TryPrevious(string currentText, out string value)
        {
            if (entries.Count == 0)
            {
                value = currentText;
                return false;
            }

            if (index == entries.Count)
                draft = currentText ?? string.Empty;
            if (index > 0)
                index--;
            value = entries[index];
            return true;
        }

        public bool TryNext(out string value)
        {
            if (index >= entries.Count)
            {
                value = draft;
                return false;
            }

            index++;
            value = index == entries.Count ? draft : entries[index];
            return true;
        }

        public void ResetNavigation()
        {
            index = entries.Count;
            draft = string.Empty;
        }
    }
}
