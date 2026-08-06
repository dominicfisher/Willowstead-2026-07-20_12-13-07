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
        // ─── Static command registry ─────────────────────────────────────
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

        // ─── Bootstrap ───────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return; // idempotent across scene reloads
            GameObject go = new GameObject("DevConsole");
            DontDestroyOnLoad(go);
            go.AddComponent<DevConsole>();

            // Register default commands. Plugins / mods may call Register() too.
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

        // ─── Instance state ──────────────────────────────────────────────
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

        // FPS overlay state
        private TextMeshProUGUI _fpsText;
        private bool _fpsOn;
        private float _fpsAccum;
        private int _fpsFrames;

        // ─── Lifecycle ───────────────────────────────────────────────────
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
            // ── Root panel (bottom-left text box) ────────────────────────
            _panelGo = new GameObject("DevConsolePanel");
            _panelRt = _panelGo.AddComponent<RectTransform>();
            _panelRt.SetParent(canvas.transform, false);
            _panelRt.anchorMin = new Vector2(0f, 0f);
            _panelRt.anchorMax = new Vector2(0f, 0f);
            _panelRt.pivot = new Vector2(0f, 0f);
            _panelRt.anchoredPosition = new Vector2(16f, 16f);
            _panelRt.sizeDelta = new Vector2(640f, 240f);

            Image bg = _panelGo.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0f);
            bg.raycastTarget = true;
            bg.sprite = UIResourceHelper.GetBackgroundSprite();

            // No panel border or dark background by request; keep the Image so the
            // console still blocks pointer raycasts behind the text.

            // ── History viewport (ScrollRect with a Mask) ──────────────────
            _historyRoot = BuildHistory(_panelRt);

            // ── Prompt + input bar ────────────────────────────────────────
            BuildInputBar(_panelRt);

            _panelGo.SetActive(false);
            Print("[DevConsole] Press ` to toggle. Type 'help' for commands.");
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
            srRt.offsetMin = new Vector2(10f, 56f);   // leave room for input bar
            srRt.offsetMax = new Vector2(-10f, -10f); // leave room for top border

            Image srImg = srGo.GetComponent<Image>();
            // Solid dark-navy scrollview background so the "shadow" doesn't read as
            // fading/transparent when the panel toggles in/out. The panel itself still
            // flips via SetActive, but the shadow itself is now ~opaque instead of
            // 25%-alpha black. RGB matches the existing panel palette for visual
            // continuity; alpha 0.96 instead of 1.0 keeps a hair of softness so it
            // doesn't read as cheap matte-black under the white history text.
            srImg.color = new Color(0.04f, 0.05f, 0.08f, 0.96f);
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
            textRt.offsetMin = new Vector2(2f, 0f);
            textRt.offsetMax = new Vector2(-2f, 0f);

            _historyText = textGo.AddComponent<TextMeshProUGUI>();
            _historyText.fontSize = 16f;
            _historyText.color = new Color(0.92f, 0.94f, 0.96f, 1f);
            // TMP 4.x renamed `enableWordWrapping` (obsolete) to `textWrappingMode`.
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
            // "> " prompt label
            GameObject promptGo = new GameObject("PromptLabel");
            RectTransform promptRt = promptGo.AddComponent<RectTransform>();
            promptRt.SetParent(parent, false);
            promptRt.anchorMin = new Vector2(0f, 0f);
            promptRt.anchorMax = new Vector2(0f, 0f);
            promptRt.pivot = new Vector2(0f, 0f);
            promptRt.anchoredPosition = new Vector2(14f, 12f);
            promptRt.sizeDelta = new Vector2(28f, 32f);
            var promptText = promptGo.AddComponent<TextMeshProUGUI>();
            promptText.text = ">";
            promptText.fontSize = 22f;
            promptText.color = new Color(0.95f, 0.78f, 0.32f, 1f);
            promptText.fontStyle = FontStyles.Bold;
            promptText.alignment = TextAlignmentOptions.MidlineLeft;

            // Input field
            GameObject inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image));
            RectTransform inputRt = inputGo.GetComponent<RectTransform>();
            inputRt.SetParent(parent, false);
            inputRt.anchorMin = new Vector2(0f, 0f);
            inputRt.anchorMax = new Vector2(1f, 0f);
            inputRt.pivot = new Vector2(0f, 0f);
            inputRt.offsetMin = new Vector2(40f, 8f);
            inputRt.offsetMax = new Vector2(-8f, 44f);

            Image inputBg = inputGo.GetComponent<Image>();
            inputBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            inputBg.color = new Color(0.06f, 0.06f, 0.10f, 0.95f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            RectTransform textAreaRt = textArea.GetComponent<RectTransform>();
            textAreaRt.SetParent(inputGo.transform, false);
            textAreaRt.anchorMin = new Vector2(0f, 0f);
            textAreaRt.anchorMax = new Vector2(1f, 1f);
            textAreaRt.pivot = new Vector2(0.5f, 0.5f);
            textAreaRt.offsetMin = new Vector2(8f, 6f);
            textAreaRt.offsetMax = new Vector2(-8f, -6f);

            // Placeholder (shown when input is empty)
            GameObject placeholderGo = new GameObject("Placeholder");
            RectTransform phRt = placeholderGo.AddComponent<RectTransform>();
            phRt.SetParent(textArea.transform, false);
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;
            var phText = placeholderGo.AddComponent<TextMeshProUGUI>();
            phText.text = "type 'help' and press Enter…";
            phText.fontSize = 16f;
            phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(1f, 1f, 1f, 0.34f);
            phText.alignment = TextAlignmentOptions.MidlineLeft;

            // Real input text (overlays the placeholder when content present)
            GameObject inputTextGo = new GameObject("Text");
            RectTransform itRt = inputTextGo.AddComponent<RectTransform>();
            itRt.SetParent(textArea.transform, false);
            itRt.anchorMin = Vector2.zero;
            itRt.anchorMax = Vector2.one;
            itRt.offsetMin = Vector2.zero;
            itRt.offsetMax = Vector2.zero;
            var itText = inputTextGo.AddComponent<TextMeshProUGUI>();
            itText.fontSize = 18f;
            itText.color = new Color(0.92f, 0.94f, 0.96f, 1f);
            // TMP uses `richText`, not the UnityEngine.UI.Text-style `supportRichText`.
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
            // Disable keyboard focus traversal — otherwise Tab would move focus
            // off the input field before our completion logic ever saw it.
            _inputField.navigation = new Navigation { mode = Navigation.Mode.None };
            _inputField.onSubmit.AddListener(OnSubmit);
        }

        private static void AddBorder(RectTransform parent, float thicknessPx, Color color)
        {
            Vector2 size = parent.sizeDelta; // already-set explicit size
            float h = Mathf.Max(1, size.y);
            float w = Mathf.Max(1, size.x);
            float yFrac = thicknessPx / h;
            float xFrac = thicknessPx / w;

            AddBorderEdge(parent, color, new Vector2(0f, 1f - yFrac), new Vector2(1f, 1f));           // top
            AddBorderEdge(parent, color, new Vector2(0f, 0f),         new Vector2(1f, yFrac));       // bottom
            AddBorderEdge(parent, color, new Vector2(0f, 0f),         new Vector2(xFrac, 1f));      // left
            AddBorderEdge(parent, color, new Vector2(1f - xFrac, 0f), new Vector2(1f, 1f));          // right
        }

        private static void AddBorderEdge(RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject edge = new GameObject("BorderEdge", typeof(RectTransform), typeof(Image));
            RectTransform rt = edge.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = edge.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        // ─── Open/close ──────────────────────────────────────────────────
        // Backquote cycles: Closed -> Mini (input only) -> Full -> Closed.
        public void Toggle()
        {
            if (_panelGo == null) return;

            if (_currentView == ConsoleView.Closed)
                SetView(ConsoleView.Mini);
            else if (_currentView == ConsoleView.Mini)
                SetView(ConsoleView.Full);
            else
                SetView(ConsoleView.Closed);
        }

        private void SetView(ConsoleView view)
        {
            _currentView = view;

            if (_panelGo != null)
                _panelGo.SetActive(view != ConsoleView.Closed);

            // Block gameplay input while any console view is open so background WASD /
            // interact / hotbar digits don't move the character or open other UI.
            InputReader.BlockGameplayInput = view != ConsoleView.Closed;

            if (_historyRoot != null)
                _historyRoot.gameObject.SetActive(view == ConsoleView.Full);

            if (_panelRt != null)
                _panelRt.sizeDelta = view == ConsoleView.Full ? new Vector2(640f, 240f) : new Vector2(480f, 60f);

            if (view != ConsoleView.Closed && _inputField != null)
                _inputField.ActivateInputField();
        }

        // ─── Update loop ─────────────────────────────────────────────────
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
            // Defence-in-depth: if TMP's input module managed to inject a '\t'
            // character after our Tab handler ran (Unity's input phase order is
            // not strictly defined versus MonoBehaviour Update), strip it now so
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
            // Reset Tab-completion cycling state — the next command starts fresh.
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

        // ─── Print API (commands call these) ─────────────────────────────
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

        // ─── FPS overlay (static so commands can call it) ────────────────
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
            // Clear the gameplay-input gate so movement resumes after the console's
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
