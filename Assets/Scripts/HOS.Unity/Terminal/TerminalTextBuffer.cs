using System;
using System.Text;

namespace HOS.Unity.Terminal
{
    public sealed class TerminalTextBuffer
    {
        private readonly StringBuilder text = new StringBuilder();
        private int preferredColumn = -1;

        public string Text => text.ToString();
        public int Length => text.Length;
        public int CaretIndex { get; private set; }

        public void SetText(string value, int? caretIndex = null)
        {
            text.Clear();
            text.Append(value ?? string.Empty);
            CaretIndex = Math.Max(0, Math.Min(caretIndex ?? text.Length, text.Length));
            preferredColumn = -1;
        }

        public void Clear() => SetText(string.Empty);

        public void Insert(char character)
        {
            text.Insert(CaretIndex, character);
            CaretIndex++;
            preferredColumn = -1;
        }

        public void Insert(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            text.Insert(CaretIndex, value);
            CaretIndex += value.Length;
            preferredColumn = -1;
        }

        public bool Backspace()
        {
            if (CaretIndex == 0)
                return false;

            text.Remove(CaretIndex - 1, 1);
            CaretIndex--;
            preferredColumn = -1;
            return true;
        }

        public bool Delete()
        {
            if (CaretIndex >= text.Length)
                return false;

            text.Remove(CaretIndex, 1);
            preferredColumn = -1;
            return true;
        }

        public bool MoveLeft()
        {
            if (CaretIndex == 0)
                return false;
            CaretIndex--;
            preferredColumn = -1;
            return true;
        }

        public bool MoveRight()
        {
            if (CaretIndex >= text.Length)
                return false;
            CaretIndex++;
            preferredColumn = -1;
            return true;
        }

        public void MoveToLineStart()
        {
            CaretIndex = FindLineStart(CaretIndex);
            preferredColumn = -1;
        }

        public void MoveToLineEnd()
        {
            CaretIndex = FindLineEnd(CaretIndex);
            preferredColumn = -1;
        }

        public bool MoveUp() => MoveVertical(-1);
        public bool MoveDown() => MoveVertical(1);

        public bool IsOnFirstLine => FindLineStart(CaretIndex) == 0;
        public bool IsOnLastLine => FindLineEnd(CaretIndex) == text.Length;

        private bool MoveVertical(int direction)
        {
            var currentStart = FindLineStart(CaretIndex);
            var column = CaretIndex - currentStart;
            if (preferredColumn < 0)
                preferredColumn = column;

            int targetStart;
            if (direction < 0)
            {
                if (currentStart == 0)
                    return false;
                targetStart = FindLineStart(currentStart - 1);
            }
            else
            {
                var currentEnd = FindLineEnd(CaretIndex);
                if (currentEnd == text.Length)
                    return false;
                targetStart = currentEnd + 1;
            }

            var targetEnd = FindLineEnd(targetStart);
            CaretIndex = Math.Min(targetStart + preferredColumn, targetEnd);
            return true;
        }

        private int FindLineStart(int index)
        {
            for (var i = Math.Min(index, text.Length) - 1; i >= 0; i--)
            {
                if (text[i] == '\n')
                    return i + 1;
            }
            return 0;
        }

        private int FindLineEnd(int index)
        {
            for (var i = Math.Max(0, index); i < text.Length; i++)
            {
                if (text[i] == '\n')
                    return i;
            }
            return text.Length;
        }
    }
}
