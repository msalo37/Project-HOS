using System;
using System.Collections.Generic;
using System.Text;
using HOS.Application.Execution;
using HOS.Application.Shell;
using TMPro;
using UnityEngine;
using Zenject;

namespace HOS.Unity.Terminal
{
    public sealed class TerminalView : MonoBehaviour
    {
        private const int MaximumHistoryLines = 256;

        [SerializeField] private TextMeshProUGUI consoleText;
        [SerializeField] private TMP_InputField inputField;

        [Inject] private ShellEngine shellEngine;
        [Inject] private PlayerShellContext shellContext;

        private readonly Queue<string> history = new Queue<string>();
        private readonly StringBuilder outputBuilder = new StringBuilder();

        public bool IsInitialized =>
            shellEngine != null &&
            shellContext != null &&
            consoleText != null &&
            inputField != null;

        public string RenderedText => consoleText != null ? consoleText.text : string.Empty;

        private void OnEnable()
        {
            if (inputField != null)
                inputField.onSubmit.AddListener(OnInputSubmitted);
        }

        private void Start()
        {
            if (!IsInitialized)
            {
                Debug.LogError("TerminalView is missing UI references or injected services.", this);
                return;
            }

            history.Clear();
            AppendLine("HOS terminal ready. Type 'ls /bin' to list installed commands.");
            RefreshInputMode();
            inputField.ActivateInputField();
        }

        private void OnDisable()
        {
            if (inputField != null)
                inputField.onSubmit.RemoveListener(OnInputSubmitted);
        }

        public CommandResult SubmitCommand(string input)
        {
            if (!IsInitialized)
                return CommandResult.Failure(1, "terminal is not initialized");

            input = input ?? string.Empty;
            var displayedInput = shellEngine.IsSecretInput ? new string('*', input.Length) : input;
            AppendLine(BuildPrompt() + " " + displayedInput);

            CommandResult result;
            if (string.IsNullOrWhiteSpace(input))
            {
                result = CommandResult.Success();
            }
            else
            {
                result = shellEngine.Execute(input);
                AppendBlock(result.StandardOutput);
                AppendBlock(result.StandardError);
            }

            RefreshInputMode();
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

        private void OnInputSubmitted(string value)
        {
            SubmitCommand(value);
            inputField.text = string.Empty;
            inputField.ActivateInputField();
            inputField.Select();
        }

        private void RefreshInputMode()
        {
            if (inputField == null || shellEngine == null)
                return;
            inputField.contentType = shellEngine.IsSecretInput
                ? TMP_InputField.ContentType.Password
                : TMP_InputField.ContentType.Standard;
            inputField.ForceLabelUpdate();
        }

        private void AppendBlock(string block)
        {
            if (string.IsNullOrEmpty(block))
                return;

            var normalized = block.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = normalized.Split('\n');
            var lineCount = lines.Length;
            if (lineCount > 0 && lines[lineCount - 1].Length == 0)
                lineCount--;

            for (var i = 0; i < lineCount; i++)
                AppendLine(lines[i]);
        }

        private void AppendLine(string line)
        {
            if (history.Count >= MaximumHistoryLines)
                history.Dequeue();
            history.Enqueue(line ?? string.Empty);

            outputBuilder.Clear();
            foreach (var historyLine in history)
                outputBuilder.AppendLine(historyLine);

            if (consoleText != null)
                consoleText.text = outputBuilder.ToString();
        }
    }
}
