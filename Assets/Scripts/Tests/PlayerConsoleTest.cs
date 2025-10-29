using System;
using HOS.Console;
using TMPro;
using UnityEngine;
using Zenject;

public class PlayerConsoleTest : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI consoleText;
    [SerializeField] private TMP_InputField inputField;
    
    [Inject] private PlayerConsole playerConsole;
    [Inject] private ConsoleInput consoleInput;

    private void Start()
    {
        inputField.onSubmit.AddListener(OnConsoleSubmit);
        consoleText.text = string.Empty;
    }

    private void OnConsoleSubmit(string value)
    {
        consoleInput.ProcessInput(value);
        inputField.text = string.Empty;
    }

    private void OnEnable()
    {
        playerConsole.ConsoleUpdated += OnPlayerConsoleUpdated;
    }

    private void OnDisable()
    {
        playerConsole.ConsoleUpdated -= OnPlayerConsoleUpdated;
    }

    private void OnPlayerConsoleUpdated()
    {
        consoleText.text = playerConsole.ToString();
    }
}
