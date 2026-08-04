using System;
using System.Collections.Generic;
using System.Text;
using HOS.Application.Execution;
using HOS.Application.Shell;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HOS.Unity.Terminal
{
    public sealed class TerminalView : MonoBehaviour, IPointerClickHandler
    {
        private const int MaximumHistoryLines = 256;

        [SerializeField] private TextMeshProUGUI consoleText;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform caret;
        [SerializeField] private float caretBlinkPeriod = 0.55f;

        private readonly Queue<string> transcript = new Queue<string>();
        private readonly StringBuilder rendered = new StringBuilder();
        private TerminalController controller;
        private float nextBlinkAt;
        private bool caretVisible = true;
        private int renderedCaretIndex;

        public event Action Clicked;
        public bool IsInitialized => controller != null && controller.IsInitialized && consoleText != null;
        public string RenderedText => consoleText != null ? consoleText.text : string.Empty;

        private void Awake()
        {
            controller = GetComponent<TerminalController>();
            EnsureScrollView();
            EnsureCaret();
        }

        private void LateUpdate()
        {
            if (consoleText == null || caret == null)
                return;

            if (Time.unscaledTime >= nextBlinkAt)
            {
                caretVisible = !caretVisible;
                caret.gameObject.SetActive(caretVisible);
                nextBlinkAt = Time.unscaledTime + caretBlinkPeriod;
            }

            PositionCaret();
        }

        public CommandResult SubmitCommand(string input) =>
            controller != null
                ? controller.SubmitCommand(input)
                : CommandResult.Failure(1, "terminal is not initialized");

        public string BuildPrompt() => controller != null ? controller.BuildPrompt() : "$";

        public void Initialize()
        {
            transcript.Clear();
            AppendLine("HOS terminal ready. Type 'ls /bin' to list installed commands.");
        }

        public void AppendBlock(string block)
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

        public void AppendLine(string line)
        {
            if (transcript.Count >= MaximumHistoryLines)
                transcript.Dequeue();
            transcript.Enqueue(line ?? string.Empty);
        }

        public void ClearTranscript() => transcript.Clear();

        public void Render(string prompt, string input, int caretIndex, bool focused)
        {
            if (consoleText == null)
                return;

            rendered.Clear();
            foreach (var line in transcript)
                rendered.AppendLine(line);
            rendered.Append(prompt ?? "$");
            rendered.Append(' ');
            var inputStart = rendered.Length;
            rendered.Append(input ?? string.Empty);
            renderedCaretIndex = inputStart + Mathf.Clamp(caretIndex, 0, (input ?? string.Empty).Length);
            consoleText.text = rendered.ToString();
            consoleText.ForceMeshUpdate();

            if (caret != null)
            {
                caret.gameObject.SetActive(focused);
                caretVisible = focused;
                nextBlinkAt = Time.unscaledTime + caretBlinkPeriod;
                PositionCaret();
            }

            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        public void RenderApplication(TerminalFrame frame, bool focused)
        {
            if (consoleText == null)
                return;

            frame = frame ?? new TerminalFrame(string.Empty, 0);
            consoleText.text = frame.Text;
            renderedCaretIndex = Mathf.Clamp(frame.CaretIndex, 0, frame.Text.Length);
            consoleText.ForceMeshUpdate();

            if (caret != null)
            {
                caret.gameObject.SetActive(focused);
                caretVisible = focused;
                nextBlinkAt = Time.unscaledTime + caretBlinkPeriod;
                PositionCaret();
            }

            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();

        private void EnsureCaret()
        {
            if (caret != null || consoleText == null)
                return;

            var caretObject = new GameObject("Caret", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            caret = caretObject.GetComponent<RectTransform>();
            caret.SetParent(consoleText.rectTransform, false);
            caret.anchorMin = new Vector2(0f, 0f);
            caret.anchorMax = new Vector2(0f, 0f);
            caret.pivot = new Vector2(0f, 0f);
            caret.sizeDelta = new Vector2(2f, consoleText.fontSize);
            caretObject.GetComponent<Image>().color = consoleText.color;
        }

        private void EnsureScrollView()
        {
            if (scrollRect != null || consoleText == null)
                return;

            var originalParent = consoleText.rectTransform.parent as RectTransform;
            if (originalParent == null)
                return;
            consoleText.richText = false;

            var viewportObject = new GameObject(
                "TerminalViewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect));
            var viewport = viewportObject.GetComponent<RectTransform>();
            viewport.SetParent(originalParent, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;

            var background = viewportObject.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.01f);
            background.raycastTarget = true;

            var textRect = consoleText.rectTransform;
            textRect.SetParent(viewport, false);
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            var fitter = consoleText.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = consoleText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect = viewportObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = textRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;
        }

        private void PositionCaret()
        {
            consoleText.ForceMeshUpdate();
            var info = consoleText.textInfo;
            Vector2 position;
            float height = consoleText.fontSize;

            if (renderedCaretIndex < info.characterCount)
            {
                var character = info.characterInfo[renderedCaretIndex];
                position = new Vector2(character.origin, character.descender);
                height = Mathf.Max(1f, character.ascender - character.descender);
            }
            else if (info.characterCount > 0)
            {
                var previous = info.characterInfo[info.characterCount - 1];
                if (previous.character == '\n' && info.lineCount > 0)
                {
                    var line = info.lineInfo[info.lineCount - 1];
                    position = new Vector2(line.lineExtents.min.x, line.descender);
                    height = Mathf.Max(1f, line.ascender - line.descender);
                }
                else
                {
                    position = new Vector2(previous.xAdvance, previous.descender);
                    height = Mathf.Max(1f, previous.ascender - previous.descender);
                }
            }
            else
            {
                position = Vector2.zero;
            }

            caret.localPosition = position;
            caret.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }
}
