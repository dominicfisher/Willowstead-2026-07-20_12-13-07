// Developer console — compiled out of release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Willowstead.Input;
using Willowstead.Player;

namespace Willowstead.Debugging
{
    /// <summary>
    /// Drop-in developer console. Self-bootstraps on scene load via
    /// <c>RuntimeInitializeOnLoadMethod</c>; no manual scene wiring required.
    ///
    /// Controls:
    ///   • <c>`</c> (backquote)            — toggle panel open/closed
    ///   • <c>Enter</c>                    — submit command on the input line
    ///   • <c>↑</c> / <c>↓</c>            — recall past commands while focused
    ///
    /// Only present in editor + development builds; the entire class is compiled
    /// out of release builds so there's zero runtime cost and no symbols leak.
    /// </summary>
    public class DevConsole : MonoBehaviour
    {
        private static readonly Dictionary<string, DevConsoleCommand> _commands
            = new Dictionary<string, DevConsoleCommand>(System.StringComparer.OrdinalIgnoreCase);

        // Cached comparer — StringComparer.OrdinalIgnoreCase allocates per call when
        // used directly in Sort(); a static field amortises it across every Tab cycle.
        private static readonly StringComparer IgnoreCaseComparer = StringComparer.OrdinalIgnoreCase;

        /// <summary>Register a command by Id. Idempotent; later register wins.</summary>
        public static void Register(DevConsoleCommand command)
        {
            if (command == null || string.IsNullOrEmpty(command.Id)) return;
            _commands[command.Id] = command;
        }

        /// <summary>Iterate over every registered command, in registration order.</summary>
        public static IEnumerable<DevConsoleCommand> GetAllCommands() => _commands.Values;

        /// <summary>The active console (or null in release builds).</summary>
        public static DevConsole Instance { get; private set; }

        /// <summary>True when the console panel is visible (any view: Mini or Full).</summary>
        public bool IsOpen => _currentView != ConsoleView.Closed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return; // idempotent across scene reloads
            GameObject go = new GameObject("DevConsole");
            DontDestroyOnLoad(go);
            go.AddComponent<DevConsole>();

            Register(new Commands.HelpCommand());
            Register(new Commands.GiveCommand());
            Register(new Commands.GoldCommand());
            Register(new Commands.TimeCommand());
            Register(new Commands.DayCommand());
            Register(new Commands.GrowCommand());
            Register(new Commands.ClearCommand());
            Register(new Commands.TpCommand());
            Register(new Commands.WeatherCommand());
            Register(new Commands.FpsCommand());
            Register(new Commands.SaveCommand());
            Register(new Commands.LoadCommand());
            Register(new Commands.SavesCommand());
            Register(new Commands.DeleteSaveCommand());
            Register(new Commands.SeedCommand());
        }

        private GameObject _panelGo;
        private RectTransform _panelRt;
        private TMP_InputField _inputField;
        private TextMeshProUGUI _historyText;
        private ScrollRect _scrollView;
        private readonly List<string> _history = new List<string>();
        private int _recallIndex = -1;

        private enum ConsoleView { Closed, Mini, Full }
        private ConsoleView _currentView = ConsoleView.Closed;
        private Transform _historyRoot;

        // Tab completion state.
        // • _completionMatches: the sorted candidate list for the current prefix.
        // • _completionIdx:     the currently highlighted candidate.
        // • _completionAnchor:  the first-word value we last WROTE into the input.
        //   If the displayed first word still equals anchor, the next Tab is a cycle;
        //   otherwise it's a fresh compute against the user's new typing. Bash-style.
        private readonly List<string> _completionMatches = new List<string>();
        private int _completionIdx;
        private string _completionAnchor;

        private TextMeshProUGUI _fpsText;
        private bool _fpsOn;
        private float _fpsAccum;
        private int _fpsFrames;

        private void Awake()
        {
            Instance = this;
            // Safety net: Unity's "Enter Play Mode without Domain Reload" (and any future
            // partial-reload config) preserves statics across domain transitions. If a
            // previous console was destroyed mid-frame the flag could be stuck `true`,
            // freezing background movement until something flips it. Clear on every boot.
            InputReader.BlockGameplayInput = false;
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas");
            UIResourceHelper.EnsureEventSystem();
            BuildPanel(canvas);
        }

        private void BuildPanel(Canvas canvas)
        {
            _panelGo = new GameObject("DevConsolePanel");
            _panelRt = _panelGo.AddComponent<RectTransform>();
            _panelRt.SetParent(canvas.transform, false);
            _panelRt.anchorMin = new Vector2(0f, 0f);
            _panelRt.anchorMax = new Vector2(0f, 0f);
            _panelRt.pivot = new Vector2(0f, 0f);
            _panelRt.anchoredPosition = new Vector2(24f, 24f);
            _panelRt.sizeDelta = new Vector2(680f, 320f);

            // Cozy wood frame container
            Image bg = _panelGo.AddComponent<Image>();
            bg.sprite = UIResourceHelper.GetBackgroundSprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.22f, 0.16f, 0.11f, 0.98f); // Warm dark walnut wood
            bg.raycastTarget = true;

            // Inner dark parchment board
            GameObject innerBoard = new GameObject("InnerBoard", typeof(RectTransform), typeof(Image));
            innerBoard.transform.SetParent(_panelGo.transform, false);
            RectTransform innerRt = (RectTransform)innerBoard.transform;
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(8f, 8f);
            innerRt.offsetMax = new Vector2(-8f, -8f);
            Image innerBg = innerBoard.GetComponent<Image>();
            innerBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            innerBg.type = Image.Type.Sliced;
            innerBg.color = new Color(0.12f, 0.09f, 0.06f, 0.95f); // Deep cozy dark board

            // Top Header Banner
            GameObject headerGo = new GameObject("HeaderBanner", typeof(RectTransform), typeof(Image));
            headerGo.transform.SetParent(innerBoard.transform, false);
            RectTransform headerRt = (RectTransform)headerGo.transform;
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0f, 32f);
            headerRt.anchoredPosition = Vector2.zero;
            Image headerBg = headerGo.GetComponent<Image>();
            headerBg.sprite = UIResourceHelper.GetBackgroundSprite();
            headerBg.type = Image.Type.Sliced;
            headerBg.color = new Color(0.35f, 0.24f, 0.15f, 1f); // Warm banner

            GameObject titleTextGo = new GameObject("TitleText", typeof(RectTransform));
            titleTextGo.transform.SetParent(headerGo.transform, false);
            RectTransform titleRt = (RectTransform)titleTextGo.transform;
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = new Vector2(12f, 0f);
            titleRt.offsetMax = new Vector2(-12f, 0f);
            var titleText = titleTextGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "DEVELOPER CONSOLE";
            titleText.fontSize = 14f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(1f, 0.88f, 0.45f, 1f);
            titleText.alignment = TextAlignmentOptions.MidlineLeft;

            _historyRoot = BuildHistory(innerRt);
            BuildInputBar(innerRt);

            _panelGo.SetActive(false);
            Print("<color=#FFD670>[DevConsole]</color> Ready. Press <color=#FFD670>`</color> to toggle. Type <color=#80D27F>help</color> for commands.");
        }

        private Transform BuildHistory(RectTransform parent)
        {
            GameObject srGo = new GameObject("History",
                typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            RectTransform srRt = srGo.GetComponent<RectTransform>();
            srRt.SetParent(parent, false);
            srRt.anchorMin = new Vector2(0f, 0f);
            srRt.anchorMax = new Vector2(1f, 1f);
            srRt.pivot = new Vector2(0.5f, 0.5f);
            srRt.offsetMin = new Vector2(10f, 54f);   // leave room for input bar
            srRt.offsetMax = new Vector2(-10f, -38f); // leave room for header banner

            Image srImg = srGo.GetComponent<Image>();
            srImg.color = new Color(0.08f, 0.06f, 0.04f, 0.80f); // Parchment scroll background
            srImg.raycastTarget = true;

            Mask srMask = srGo.GetComponent<Mask>();
            srMask.showMaskGraphic = false;

            _scrollView = srGo.GetComponent<ScrollRect>();
            _scrollView.horizontal = false;
            _scrollView.vertical = true;
            _scrollView.movementType = ScrollRect.MovementType.Clamped;
            _scrollView.scrollSensitivity = 32f;

            // Content: TMP text with a ContentSizeFitter so it grows downward.
            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
            RectTransform contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.SetParent(srGo.transform, false);
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0f, 0f);
            contentRt.offsetMin = new Vector2(0f, 0f);
            contentRt.offsetMax = new Vector2(0f, 0f);
            ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject textGo = new GameObject("HistoryText", typeof(RectTransform));
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(contentGo.transform, false);
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.pivot = new Vector2(0f, 0f);
            textRt.offsetMin = new Vector2(6f, 4f);
            textRt.offsetMax = new Vector2(-6f, -4f);

            _historyText = textGo.AddComponent<TextMeshProUGUI>();
            _historyText.fontSize = 15f;
            _historyText.color = new Color(0.92f, 0.88f, 0.78f, 1f);
            _historyText.alignment = TextAlignmentOptions.BottomLeft;
            _historyText.textWrappingMode = TextWrappingModes.Normal;
            _historyText.richText = true;
            _historyText.text = string.Empty;

            _scrollView.content = contentRt;
            _scrollView.viewport = srRt;
            return srGo.transform;
        }

        private void BuildInputBar(RectTransform parent)
        {
            GameObject promptGo = new GameObject("PromptLabel");
            RectTransform promptRt = promptGo.AddComponent<RectTransform>();
            promptRt.SetParent(parent, false);
            promptRt.anchorMin = new Vector2(0f, 0f);
            promptRt.anchorMax = new Vector2(0f, 0f);
            promptRt.pivot = new Vector2(0f, 0f);
            promptRt.anchoredPosition = new Vector2(12f, 10f);
            promptRt.sizeDelta = new Vector2(24f, 34f);
            var promptText = promptGo.AddComponent<TextMeshProUGUI>();
            promptText.text = "❯";
            promptText.fontSize = 20f;
            promptText.color = new Color(1f, 0.88f, 0.45f, 1f); // Cozy gold prompt
            promptText.fontStyle = FontStyles.Bold;
            promptText.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image));
            RectTransform inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.SetParent(parent, false);
            inputRt.anchorMin = new Vector2(0f, 0f);
            inputRt.anchorMax = new Vector2(1f, 0f);
            inputRt.pivot = new Vector2(0f, 0f);
            inputRt.offsetMin = new Vector2(36f, 8f);
            inputRt.offsetMax = new Vector2(-10f, 44f);

            Image inputBg = inputGo.GetComponent<Image>();
            inputBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            inputBg.type = Image.Type.Sliced;
            inputBg.color = new Color(0.18f, 0.14f, 0.10f, 0.95f); // Rich warm input box

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            RectTransform textAreaRt = textArea.GetComponent<RectTransform>();
            textAreaRt.SetParent(inputGo.transform, false);
            textAreaRt.anchorMin = new Vector2(0f, 0f);
            textAreaRt.anchorMax = new Vector2(1f, 1f);
            textAreaRt.pivot = new Vector2(0.5f, 0.5f);
            textAreaRt.offsetMin = new Vector2(10f, 4f);
            textAreaRt.offsetMax = new Vector2(-10f, -4f);

            GameObject placeholderGo = new GameObject("Placeholder");
            RectTransform phRt = placeholderGo.AddComponent<RectTransform>();
            phRt.SetParent(textArea.transform, false);
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;
            var phText = placeholderGo.AddComponent<TextMeshProUGUI>();
            phText.text = "type 'help' and press Enter…";
            phText.fontSize = 15f;
            phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(0.80f, 0.74f, 0.64f, 0.45f);
            phText.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject inputTextGo = new GameObject("Text");
            RectTransform itRt = inputTextGo.AddComponent<RectTransform>();
            itRt.SetParent(textArea.transform, false);
            itRt.anchorMin = Vector2.zero;
            itRt.anchorMax = Vector2.one;
            itRt.offsetMin = Vector2.zero;
            itRt.offsetMax = Vector2.zero;
            var itText = inputTextGo.AddComponent<TextMeshProUGUI>();
            itText.fontSize = 16f;
            itText.color = new Color(0.98f, 0.94f, 0.86f, 1f);
            itText.richText = false;
            itText.alignment = TextAlignmentOptions.MidlineLeft;

            _inputField = inputGo.AddComponent<TMP_InputField>();
            _inputField.targetGraphic = inputBg;
            _inputField.textViewport = textAreaRt;
            _inputField.textComponent = itText;
            _inputField.placeholder = phText;
            _inputField.lineType = TMP_InputField.LineType.SingleLine;
            _inputField.characterLimit = 256;
            _inputField.restoreOriginalTextOnEscape = false;
            _inputField.navigation = new Navigation { mode = Navigation.Mode.None };
            _inputField.onSubmit.AddListener(OnSubmit);
        }

        public void Toggle()
        {
            if (_panelGo == null) return;
            if (_currentView == ConsoleView.Closed)
                SetView(ConsoleView.Full);
            else
                SetView(ConsoleView.Closed);
        }

        private void SetView(ConsoleView view)
        {
            _currentView = view;

            if (_panelGo != null)
                _panelGo.SetActive(view != ConsoleView.Closed);

            // Block gameplay input while console is open
            InputReader.BlockGameplayInput = view != ConsoleView.Closed;

            if (view != ConsoleView.Closed && _inputField != null)
            {
                _inputField.ActivateInputField();
                _inputField.Select();
            }
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.backquoteKey.wasPressedThisFrame)
            {
                Toggle();
                return;
            }

            if (kb.escapeKey.wasPressedThisFrame && _currentView != ConsoleView.Closed)
            {
                SetView(ConsoleView.Closed);
                return;
            }

            if (_currentView != ConsoleView.Closed && _inputField != null && _inputField.isFocused)
            {
                if (kb.upArrowKey.wasPressedThisFrame)        Recall(-1);
                else if (kb.downArrowKey.wasPressedThisFrame) Recall(+1);
                else if (kb.tabKey.wasPressedThisFrame)       CycleCompletion(kb.shiftKey.isPressed);
            }

            if (_fpsOn) UpdateFps();
        }

        private void Recall(int direction)
        {
            if (_history.Count == 0 || _inputField == null) return;
            if (_recallIndex < 0) _recallIndex = _history.Count; // start just past newest
            int next = Mathf.Clamp(_recallIndex + direction, 0, _history.Count);
            if (next == _recallIndex) return;
            _recallIndex = next;
            if (_recallIndex >= _history.Count)
            {
                _inputField.SetTextWithoutNotify(string.Empty);
            }
            else
            {
                string recall = _history[_recallIndex];
                // SetTextWithoutNotify updates the field without firing onValueChanged
                // (so we don't mis-fire the recall as a fresh typed command); the TMP
                // label mesh rebuilds inside UpdateLabel() before this method returns.
                _inputField.SetTextWithoutNotify(recall);
                _inputField.caretPosition = recall.Length;
            }
        }

        private void CycleCompletion(bool backward)
        {
            if (_inputField == null) return;
            string text = _inputField.text ?? string.Empty;
            string[] parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            string firstWord = parts[0].ToLowerInvariant();

            // Continue cycling if the displayed first word still equals the value we
            // wrote on the previous Tab; otherwise recompute against new user typing.
            if (firstWord == _completionAnchor && _completionMatches.Count > 0)
            {
                int n = _completionMatches.Count;
                if (n > 1)
                {
                    _completionIdx = backward
                        ? (_completionIdx - 1 + n) % n
                        : (_completionIdx + 1) % n;
                }
                // Single match: cycling is meaningless, _completionIdx stays at 0.
            }
            else
            {
                _completionMatches.Clear();
                foreach (DevConsoleCommand cmd in DevConsole.GetAllCommands())
                {
                    if (cmd.Id.StartsWith(firstWord, System.StringComparison.OrdinalIgnoreCase))
                        _completionMatches.Add(cmd.Id);
                }
                _completionMatches.Sort(IgnoreCaseComparer);
                _completionIdx = _completionMatches.Count > 0 ? 0 : -1;
            }

            if (_completionIdx < 0) return; // no match — silent (no flash)

            string completion = _completionMatches[_completionIdx];
            // Preserve any args the user already typed past the first word; append a
            // single trailing space only if there's nothing after, so the next keypress
            // drops the user straight into the args.
            string rest = parts.Length > 1
                ? " " + string.Join(" ", parts, 1, parts.Length - 1)
                : " ";
            string newText = completion + rest;

            // Skip the write entirely when the text wouldn't change — preserves the
            // user's caret position and avoids an unnecessary TMP label rebuild.
            if (!string.Equals(text, newText, System.StringComparison.Ordinal))
            {
                _inputField.SetTextWithoutNotify(newText);
                _inputField.caretPosition = completion.Length;
            }

            // Remember what we just placed so the next Tab knows to cycle.
            // Store case-folded so cycling matches `firstWord` (also case-folded) on
            // future Tabs regardless of the registered command Id's casing.
            _completionAnchor = completion.ToLowerInvariant();
        }

        private void LateUpdate()
        {
            // it never reaches OnSubmit.
            if (_inputField == null) return;
            string cur = _inputField.text;
            if (string.IsNullOrEmpty(cur) || cur.IndexOf('\t') < 0) return;
            string cleaned = cur.Replace("\t", "");
            _inputField.SetTextWithoutNotify(cleaned);
            int caret = Mathf.Min(_inputField.caretPosition, cleaned.Length);
            _inputField.caretPosition = caret;
        }

        private void OnSubmit(string text)
        {
            string trimmed = text == null ? string.Empty : text.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                _inputField?.ActivateInputField();
                return;
            }

            if (!_history.Contains(trimmed)) _history.Add(trimmed);
            _recallIndex = -1;
            _completionAnchor = null;
            _completionMatches.Clear();
            _completionIdx = 0;
            AppendLine($"<color=#c8a85c>── {Escape(trimmed)} ──</color>");
            ExecuteCommand(trimmed);
            _inputField.SetTextWithoutNotify(string.Empty);
            _inputField.ActivateInputField();
        }

        private void ExecuteCommand(string line)
        {
            string[] parts = line.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            string id = parts[0];
            string[] args = new string[parts.Length - 1];
            for (int i = 1; i < parts.Length; i++) args[i - 1] = parts[i];

            if (_commands.TryGetValue(id, out DevConsoleCommand cmd))
            {
                try { cmd.Run(this, args); }
                catch (Exception ex) { PrintError($"Error in <{id}>: {ex.Message}"); }
            }
            else
            {
                PrintError($"Unknown command '{parts[0]}'. Type 'help' for a list.");
            }
        }

        public void Print(string text)      => AppendLine($"<color=#c8dfe6>{Escape(text)}</color>");
        public void PrintOk(string text)    => AppendLine($"<color=#80d27f>[ok] {Escape(text)}</color>");
        public void PrintError(string text) => AppendLine($"<color=#ff8585>[err] {Escape(text)}</color>");

        private void AppendLine(string richLine)
        {
            if (_historyText == null) return;
            _historyText.text = string.IsNullOrEmpty(_historyText.text)
                ? richLine
                : _historyText.text + "\n" + richLine;

            // Defer scroll until next frame so ContentSizeFitter has measured the new height.
            Canvas.ForceUpdateCanvases();
            if (_scrollView != null) _scrollView.verticalNormalizedPosition = 0f;
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        public static void ToggleFps()
        {
            DevConsole self = Instance;
            if (self == null) return;

            self._fpsOn = !self._fpsOn;

            if (self._fpsOn && self._fpsText == null)
            {
                Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas");
                GameObject fpsGo = new GameObject("DevConsoleFPSLabel");
                var rt = fpsGo.AddComponent<RectTransform>();
                rt.SetParent(canvas.transform, false);
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-12f, -12f);
                rt.sizeDelta = new Vector2(140f, 28f);
                self._fpsText = fpsGo.AddComponent<TextMeshProUGUI>();
                self._fpsText.fontSize = 18f;
                self._fpsText.color = new Color(0.95f, 0.95f, 0.95f, 0.85f);
                // TMP's enum name for top-right corner is `TopRight`, not `UpperRight`.
                self._fpsText.alignment = TextAlignmentOptions.TopRight;
                self._fpsText.text = "…";
            }
            else if (!self._fpsOn && self._fpsText != null)
            {
                Destroy(self._fpsText.gameObject);
                self._fpsText = null;
                self._fpsAccum = 0f;
                self._fpsFrames = 0;
            }
            self.PrintOk("FPS overlay " + (self._fpsOn ? "ON" : "OFF"));
        }

        private void UpdateFps()
        {
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAccum < 0.5f) return;
            float fps = _fpsFrames / _fpsAccum;
            if (_fpsText != null) _fpsText.text = string.Format("{0:0} fps", fps);
            _fpsAccum = 0f;
            _fpsFrames = 0;
        }

        private void OnDestroy()
        {
            // GameObject is destroyed (manual destroy, domain reload, scene tear-down).
            // Without this, a transient Instance could leave BlockGameplayInput stuck
            // true if our pointer was cleared mid-frame, freezing background input.
            if (Instance == this)
            {
                InputReader.BlockGameplayInput = false;
                Instance = null;
            }
        }
    }
}
#endif
