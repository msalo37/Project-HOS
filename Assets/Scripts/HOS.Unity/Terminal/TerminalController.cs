using System;
using HOS.Application.Execution;
using HOS.Application.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace HOS.Unity.Terminal
{
    public enum TerminalInputMode
    {
        ShellLineEditor,
        FullScreenApplication
    }

    [RequireComponent(typeof(TerminalView))]
    public sealed class TerminalController : MonoBehaviour
    {
        [Inject] private ShellEngine shellEngine;
        [Inject] private PlayerShellContext shellContext;

        private readonly TerminalTextBuffer buffer = new TerminalTextBuffer();
        private readonly TerminalCommandHistory history = new TerminalCommandHistory();
        private TerminalCompletionService completion;
        private TerminalView view;
        private Keyboard subscribedKeyboard;
        private bool focused = true;
        private string lastCompletionInput;

        public bool IsInitialized => shellEngine != null && shellContext != null && view != null;
        public string InputText => buffer.Text;
        public int CaretIndex => buffer.CaretIndex;
        public TerminalInputMode InputMode { get; private set; } = TerminalInputMode.ShellLineEditor;

        private void Awake()
        {
            view = GetComponent<TerminalView>();
        }

        private void OnEnable()
        {
            if (view != null)
                view.Clicked += Focus;
            SubscribeKeyboard();
        }

        private void Start()
        {
            if (!IsInitialized)
            {
                Debug.LogError("TerminalController is missing its view or injected shell services.", this);
                return;
            }

            completion = new TerminalCompletionService(shellContext);
            view.Initialize();
            Refresh();
        }

        private void OnDisable()
        {
            if (view != null)
                view.Clicked -= Focus;
            UnsubscribeKeyboard();
        }

        private void Update()
        {
            SubscribeKeyboard();
            var keyboard = Keyboard.current;
            if (!focused || keyboard == null || !IsInitialized)
                return;

            var control = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            var shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

            if (InputMode == TerminalInputMode.FullScreenApplication)
            {
                HandleApplicationKeyboard(keyboard, control, shift);
                Refresh();
                return;
            }

            if (control && keyboard.aKey.wasPressedThisFrame)
                buffer.MoveToLineStart();
            else if (control && keyboard.eKey.wasPressedThisFrame)
                buffer.MoveToLineEnd();
            else if (control && keyboard.cKey.wasPressedThisFrame)
                buffer.Clear();
            else if (control && keyboard.lKey.wasPressedThisFrame)
                view.ClearTranscript();
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                if (shift)
                    buffer.Insert('\n');
                else
                    SubmitCurrentBuffer();
            }
            else if (keyboard.tabKey.wasPressedThisFrame)
                CompleteInput();
            else if (keyboard.backspaceKey.wasPressedThisFrame)
                buffer.Backspace();
            else if (keyboard.deleteKey.wasPressedThisFrame)
                buffer.Delete();
            else if (keyboard.leftArrowKey.wasPressedThisFrame)
                buffer.MoveLeft();
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                buffer.MoveRight();
            else if (keyboard.homeKey.wasPressedThisFrame)
                buffer.MoveToLineStart();
            else if (keyboard.endKey.wasPressedThisFrame)
                buffer.MoveToLineEnd();
            else if (keyboard.upArrowKey.wasPressedThisFrame)
                MoveUp();
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                MoveDown();

            Refresh();
        }

        public CommandResult SubmitCommand(string input)
        {
            if (!IsInitialized)
                return CommandResult.Failure(1, "terminal is not initialized");

            input = input ?? string.Empty;
            var wasSecretInput = shellEngine.IsSecretInput;
            var displayedInput = wasSecretInput ? new string('*', input.Length) : input;
            view.AppendLine(BuildPrompt() + " " + displayedInput);

            CommandResult result;
            if (string.IsNullOrWhiteSpace(input))
            {
                result = CommandResult.Success();
            }
            else
            {
                result = shellEngine.Execute(input);
                view.AppendBlock(result.StandardOutput);
                view.AppendBlock(result.StandardError);
                if (!wasSecretInput)
                    history.Add(input);
                if (shellEngine.HasForegroundProgram)
                    InputMode = TerminalInputMode.FullScreenApplication;
            }

            Refresh();
            return result;
        }

        public string BuildPrompt()
        {
            if (shellEngine != null && shellEngine.IsAwaitingInput)
                return shellEngine.InteractionPrompt;
            if (shellContext == null)
                return "$";

            var machine = shellContext.CurrentMachine;
            var userName = machine.Users.TryGetUser(shellContext.CurrentUserId, out var user)
                ? user.Name
                : "unknown";
            var path = machine.FileSystem.GetPath(shellContext.WorkingDirectory);
            var workingDirectory = path.IsSuccess ? path.Value : "?";
            return $"{userName}@{machine.Hostname}:{workingDirectory}$";
        }

        public void SetInputMode(TerminalInputMode mode)
        {
            InputMode = mode;
            Refresh();
        }

        public ProgramSessionUpdate SendProgramInput(ProgramInput input)
        {
            var update = shellEngine.SendForegroundInput(input);
            HandleProgramUpdate(update);
            Refresh();
            return update;
        }

        public ProgramSessionUpdate InterruptForegroundProgram()
        {
            var update = shellEngine.SendForegroundSignal(ProgramSignal.Interrupt);
            HandleProgramUpdate(update);
            Refresh();
            return update;
        }

        private void SubmitCurrentBuffer()
        {
            var input = buffer.Text;
            buffer.Clear();
            lastCompletionInput = null;
            SubmitCommand(input);
        }

        private void CompleteInput()
        {
            if (completion == null || shellEngine.IsSecretInput)
                return;

            var before = buffer.Text;
            var result = completion.Complete(before, buffer.CaretIndex);
            if (result.Candidates.Count == 0)
                return;

            var suffix = before.Substring(result.TokenStart + result.TokenLength);
            var prefix = before.Substring(0, result.TokenStart);
            buffer.SetText(prefix + result.Replacement + suffix, prefix.Length + result.Replacement.Length);

            if (lastCompletionInput == before && result.Candidates.Count > 1)
                view.AppendLine(string.Join("  ", result.Candidates));
            lastCompletionInput = buffer.Text;
        }

        private void MoveUp()
        {
            if (buffer.MoveUp())
                return;
            if (history.TryPrevious(buffer.Text, out var value))
                buffer.SetText(value);
        }

        private void MoveDown()
        {
            if (buffer.MoveDown())
                return;
            if (history.TryNext(out var value))
                buffer.SetText(value);
        }

        private void OnTextInput(char character)
        {
            if (!focused)
                return;
            if (InputMode == TerminalInputMode.FullScreenApplication)
            {
                if (!char.IsControl(character))
                    SendProgramInput(ProgramInput.Text(character));
                return;
            }
            if (char.IsControl(character))
                return;
            buffer.Insert(character);
            lastCompletionInput = null;
            Refresh();
        }

        private void Focus()
        {
            focused = true;
            Refresh();
        }

        private void SubscribeKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == subscribedKeyboard)
                return;
            UnsubscribeKeyboard();
            subscribedKeyboard = keyboard;
            if (subscribedKeyboard != null)
                subscribedKeyboard.onTextInput += OnTextInput;
        }

        private void UnsubscribeKeyboard()
        {
            if (subscribedKeyboard != null)
                subscribedKeyboard.onTextInput -= OnTextInput;
            subscribedKeyboard = null;
        }

        private void Refresh()
        {
            if (view == null || shellEngine == null)
                return;
            if (shellEngine.HasForegroundProgram)
            {
                InputMode = TerminalInputMode.FullScreenApplication;
                view.RenderApplication(shellEngine.ForegroundFrame, focused);
                return;
            }

            InputMode = TerminalInputMode.ShellLineEditor;
            var display = shellEngine.IsSecretInput
                ? new string('*', buffer.Length)
                : buffer.Text;
            view.Render(BuildPrompt(), display, buffer.CaretIndex, focused);
        }

        private void HandleApplicationKeyboard(
            Keyboard keyboard,
            bool control,
            bool shift)
        {
            if (control && keyboard.cKey.wasPressedThisFrame)
                InterruptForegroundProgram();
            else if (control && keyboard.oKey.wasPressedThisFrame)
                SendProgramInput(new ProgramInput(ProgramInputKey.O, control: true));
            else if (control && keyboard.xKey.wasPressedThisFrame)
                SendProgramInput(new ProgramInput(ProgramInputKey.X, control: true));
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                SendProgramInput(new ProgramInput(ProgramInputKey.Enter, shift: shift));
            else if (keyboard.backspaceKey.wasPressedThisFrame)
                SendProgramInput(new ProgramInput(ProgramInputKey.Backspace));
            else if (keyboard.deleteKey.wasPressedThisFrame)
                SendProgramInput(new ProgramInput(ProgramInputKey.Delete));
            else if (keyboard.leftArrowKey.wasPressedThisFrame)
                SendProgramInput(new ProgramInput(ProgramInputKey.LeftArrow));
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                SendProgramInput(new ProgramInput(ProgramInputKey.RightArrow));
            else if (keyboard.upArrowKey.wasPressedThisFrame)
                SendProgramInput(new ProgramInput(ProgramInputKey.UpArrow));
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                SendProgramInput(new ProgramInput(ProgramInputKey.DownArrow));
            else if (keyboard.homeKey.wasPressedThisFrame)
                SendProgramInput(new ProgramInput(ProgramInputKey.Home));
            else if (keyboard.endKey.wasPressedThisFrame)
                SendProgramInput(new ProgramInput(ProgramInputKey.End));
        }

        private void HandleProgramUpdate(ProgramSessionUpdate update)
        {
            if (update == null || !update.HasExited)
                return;

            InputMode = TerminalInputMode.ShellLineEditor;
            view.AppendBlock(update.Result.StandardOutput);
            view.AppendBlock(update.Result.StandardError);
        }
    }
}
