using System;
using HOS.Application.Shell;

namespace HOS.Application.Execution
{
    public enum ProgramInputKey
    {
        Character,
        Enter,
        Backspace,
        Delete,
        LeftArrow,
        RightArrow,
        UpArrow,
        DownArrow,
        Home,
        End,
        O,
        X
    }

    public readonly struct ProgramInput
    {
        public ProgramInput(
            ProgramInputKey key,
            char character = '\0',
            bool control = false,
            bool shift = false)
        {
            Key = key;
            Character = character;
            Control = control;
            Shift = shift;
        }

        public ProgramInputKey Key { get; }
        public char Character { get; }
        public bool Control { get; }
        public bool Shift { get; }

        public static ProgramInput Text(char character) =>
            new ProgramInput(ProgramInputKey.Character, character);
    }

    public enum ProgramSignal
    {
        Interrupt,
        Terminate
    }

    public sealed class TerminalFrame
    {
        public TerminalFrame(string text, int caretIndex)
        {
            Text = text ?? string.Empty;
            CaretIndex = Math.Max(0, Math.Min(caretIndex, Text.Length));
        }

        public string Text { get; }
        public int CaretIndex { get; }
    }

    public sealed class ProgramSessionUpdate
    {
        private ProgramSessionUpdate(bool hasExited, CommandResult result)
        {
            HasExited = hasExited;
            Result = result;
        }

        public bool HasExited { get; }
        public CommandResult Result { get; }

        public static ProgramSessionUpdate Running() =>
            new ProgramSessionUpdate(false, null);

        public static ProgramSessionUpdate Exited(CommandResult result) =>
            new ProgramSessionUpdate(
                true,
                result ?? throw new ArgumentNullException(nameof(result)));
    }

    public interface IProgramSession
    {
        TerminalFrame CurrentFrame { get; }
        ProgramSessionUpdate HandleInput(ProgramInput input);
        ProgramSessionUpdate HandleSignal(ProgramSignal signal);
    }

    public sealed class ProgramStartResult
    {
        private ProgramStartResult(CommandResult result, IProgramSession session)
        {
            Result = result;
            Session = session;
        }

        public CommandResult Result { get; }
        public IProgramSession Session { get; }
        public bool IsRunning => Session != null;

        public static ProgramStartResult Completed(CommandResult result) =>
            new ProgramStartResult(
                result ?? throw new ArgumentNullException(nameof(result)),
                null);

        public static ProgramStartResult Running(IProgramSession session) =>
            new ProgramStartResult(
                null,
                session ?? throw new ArgumentNullException(nameof(session)));
    }
}
