// World-creation UI. Compiled into all builds — even shipping — because this
// is part of the player-facing new-game UX, not a dev tool.
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Willowstead.Player;
using Willowstead.World;

namespace Willowstead.UI
{
    /// <summary>
    /// Modal "Create New World" panel. Auto-presents on first launch
    /// (when <see cref="WorldSeedService.LastSeedWasUserProvided"/> is false)
    /// and any time the player explicitly requests a new seed through the
    /// dev console or another reroll path.
    ///
    /// Subscribes to <see cref="WorldSeedService.OnSeedChanged"/> so external
    /// seed flips (dev console, etc.) refresh the input field rather than
    /// leaving stale text on screen.
    /// </summary>
    public class WorldSetupUI : MonoBehaviour
    {
        public static WorldSetupUI Instance { get; private set; }

        private GameObject _panelGo;
        private TMP_InputField _seedInput;
        private TextMeshProUGUI _statusLabel;
        private Button _createButton;
        private Button _randomButton;

        private bool _suppressShow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[WorldSetupUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<WorldSetupUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas");
            UIResourceHelper.EnsureEventSystem();
            BuildPanel(canvas);

            if (WorldSeedService.Instance != null)
            {
                WorldSeedService.Instance.OnSeedChanged += HandleSeedChanged;
            }
        }

        private void Start()
        {
            // First-launch heuristic: only auto-present if no seed has ever been
            // stored. The PlayerPrefs persistence in WorldSeedService is the
            // source of truth for "has the player made a choice yet".
            if (WorldSeedService.Instance == null) return;
            ShowIfFirstLaunch();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (WorldSeedService.Instance != null)
            {
                WorldSeedService.Instance.OnSeedChanged -= HandleSeedChanged;
            }
        }

        private void HandleSeedChanged(int newSeed)
        {
            // If the panel is open while an external source flips the seed (the
            // dev console, for instance), keep the input field in sync and hide
            // the panel — the player has nothing more to decide here.
            if (_seedInput != null && !_seedInput.isFocused)
            {
                _seedInput.SetTextWithoutNotify(newSeed.ToString());
            }
            if (_panelGo != null && _panelGo.activeSelf)
            {
                Hide();
            }
        }

        // ─── Public API ─────────────────────────────────────────────────

        /// <summary>
        /// Shows the panel if and only if no seed has ever been user-provided.
        /// Called once from <see cref="Start"/>.
        /// </summary>
        public void ShowIfFirstLaunch()
        {
            if (WorldSeedService.Instance == null) return;
            if (WorldSeedService.Instance.LastSeedWasUserProvided)
            {
                Hide();
                return;
            }
            // Random roll for the very first launch so simply running the game
            // doesn't drop the player into identical worlds on repeat boots.
            int randomSeed = WorldSeedService.Instance.GenerateRandomSeed();
            if (_seedInput != null) _seedInput.text = randomSeed.ToString();
            Show();
        }

        /// <summary>Forcibly show the panel (e.g. from the Main Menu's "New World" button).</summary>
        public void Show()
        {
            if (_panelGo == null) return;
            _panelGo.SetActive(true);
            // Hard-block gameplay input while open, like DevConsole does — this
            // prevents hotbar digits / walk keys firing while the player types.
            Willowstead.Input.InputReader.BlockGameplayInput = true;
            if (_seedInput != null)
            {
                // Pre-fill a fresh random seed only when the field is empty. A
                // player who typed a specific seed earlier and is returning
                // keeps their entry; a player who flows in cold sees a new
                // auto-picked seed rather than the previous world. Combined with
                // OnCreateClicked's empty-input auto-roll, this means the modal
                // is never silent: "open → Create (no edits)" always yields a
                // different world than the one currently on screen.
                if (string.IsNullOrEmpty(_seedInput.text) && WorldSeedService.Instance != null)
                    _seedInput.text = WorldSeedService.Instance.GenerateRandomSeed().ToString();
                _seedInput.ActivateInputField();
            }
            EnsureStatusRefresh();
        }

        /// <summary>Hides the panel and releases gameplay input.</summary>
        public void Hide()
        {
            if (_panelGo == null) return;
            _panelGo.SetActive(false);
            Willowstead.Input.InputReader.BlockGameplayInput = false;
        }

        /// <summary>True when the seed-setup modal is on screen. Pause menu bails on ESC while this is true.</summary>
        public bool IsVisible => _panelGo != null && _panelGo.activeSelf;

        // ─── Panel construction ────────────────────────────────────────

        private void BuildPanel(Canvas canvas)
        {
            // Root panel: centred modal frame, sits above any HUD layer.
            _panelGo = new GameObject("WorldSetupPanel", typeof(RectTransform), typeof(Image));
            _panelGo.transform.SetParent(canvas.transform, false);
            RectTransform rt = (RectTransform)_panelGo.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(620f, 360f);
            rt.anchoredPosition = Vector2.zero;
            Image bg = _panelGo.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.10f, 0.08f, 0.94f);
            bg.raycastTarget = true;
            bg.sprite = UIResourceHelper.GetBackgroundSprite();

            // Title
            GameObject titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(_panelGo.transform, false);
            RectTransform titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.offsetMin = new Vector2(24f, -68f);
            titleRt.offsetMax = new Vector2(-24f, -16f);
            TextMeshProUGUI title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "Create a New World";
            title.fontSize = 30f;
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.95f, 0.88f, 0.62f, 1f);
            title.alignment = TextAlignmentOptions.Center;
            title.richText = false;

            // Subtitle
            GameObject subGo = new GameObject("Subtitle", typeof(RectTransform));
            subGo.transform.SetParent(_panelGo.transform, false);
            RectTransform subRt = (RectTransform)subGo.transform;
            subRt.anchorMin = new Vector2(0f, 1f);
            subRt.anchorMax = new Vector2(1f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.offsetMin = new Vector2(24f, -120f);
            subRt.offsetMax = new Vector2(-24f, -72f);
            TextMeshProUGUI sub = subGo.AddComponent<TextMeshProUGUI>();
            sub.text = "Pick a seed. Worlds with different seeds share nothing — terrain, decor, ponds — but identical seeds reproduce the same world bit-for-bit.";
            sub.fontSize = 16f;
            sub.color = new Color(0.86f, 0.82f, 0.74f, 1f);
            sub.alignment = TextAlignmentOptions.Top;
            // TMP 4.x renamed enableWordWrapping to textWrappingMode. The older
            // property still exists for source-compat but emits CS0618 here.
            sub.textWrappingMode = TextWrappingModes.Normal;
            sub.richText = false;

            // Seed input row
            _seedInput = BuildSeedInputRow(_panelGo.transform);

            // Status label
            GameObject stGo = new GameObject("Status", typeof(RectTransform));
            stGo.transform.SetParent(_panelGo.transform, false);
            RectTransform stRt = (RectTransform)stGo.transform;
            stRt.anchorMin = new Vector2(0f, 0f);
            stRt.anchorMax = new Vector2(1f, 0f);
            stRt.pivot = new Vector2(0.5f, 0f);
            stRt.offsetMin = new Vector2(24f, 92f);
            stRt.offsetMax = new Vector2(-24f, 124f);
            _statusLabel = stGo.AddComponent<TextMeshProUGUI>();
            _statusLabel.text = string.Empty;
            _statusLabel.fontSize = 14f;
            _statusLabel.color = new Color(0.92f, 0.78f, 0.42f, 1f);
            _statusLabel.alignment = TextAlignmentOptions.Center;
            _statusLabel.richText = false;

            // Button row
            _randomButton = BuildButton(_panelGo.transform, "Randomize",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0f),
                new Vector2(24f, 16f), new Vector2(-12f, 76f), OnRandomizeClicked);

            _createButton = BuildButton(_panelGo.transform, "Create World",
                new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(12f, 16f), new Vector2(-24f, 76f), OnCreateClicked);

            // Enter on the input field = same as Create.
            // NOTE: We deliberately do NOT also wire _seedInput.onSubmit here.
            // Pressing Enter while the input is focused would otherwise fire
            // OnCreateClicked twice (Unity's input routing bubbles Enter to
            // both the onSubmit listeners AND the active button's onClick).
            // The Create button above already covers the Enter case exactly once.

            _panelGo.SetActive(false);
        }

        private TMP_InputField BuildSeedInputRow(Transform parent)
        {
            GameObject rowGo = new GameObject("SeedRow", typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.offsetMin = new Vector2(40f, -180f);
            rowRt.offsetMax = new Vector2(-40f, -132f);

            Image rowBg = rowGo.GetComponent<Image>();
            rowBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            rowBg.color = new Color(0.06f, 0.06f, 0.10f, 0.95f);
            rowBg.raycastTarget = true;

            // Inline "Seed:" label on the left side of the row
            GameObject lblGo = new GameObject("SeedLabel", typeof(RectTransform));
            lblGo.transform.SetParent(rowGo.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = new Vector2(0f, 0f);
            lblRt.anchorMax = new Vector2(0f, 1f);
            lblRt.pivot = new Vector2(0f, 0.5f);
            lblRt.offsetMin = new Vector2(12f, 0f);
            lblRt.offsetMax = new Vector2(78f, 0f);
            TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = "Seed:";
            lbl.fontSize = 18f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = new Color(0.95f, 0.88f, 0.62f, 1f);
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.richText = false;

            // TextArea wrapper for the input field (obligatory for TMP_InputField masking)
            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(rowGo.transform, false);
            RectTransform textAreaRt = (RectTransform)textArea.transform;
            textAreaRt.anchorMin = new Vector2(0f, 0f);
            textAreaRt.anchorMax = new Vector2(1f, 1f);
            textAreaRt.pivot = new Vector2(0.5f, 0.5f);
            textAreaRt.offsetMin = new Vector2(90f, 6f);
            textAreaRt.offsetMax = new Vector2(-12f, -6f);

            // Live text
            GameObject inputTextGo = new GameObject("Text", typeof(RectTransform));
            inputTextGo.transform.SetParent(textArea.transform, false);
            RectTransform itRt = (RectTransform)inputTextGo.transform;
            itRt.anchorMin = Vector2.zero;
            itRt.anchorMax = Vector2.one;
            itRt.offsetMin = Vector2.zero;
            itRt.offsetMax = Vector2.zero;
            TextMeshProUGUI itText = inputTextGo.AddComponent<TextMeshProUGUI>();
            itText.fontSize = 20f;
            itText.color = new Color(0.95f, 0.93f, 0.85f, 1f);
            itText.alignment = TextAlignmentOptions.MidlineLeft;
            itText.richText = false;

            // Placeholder
            GameObject phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(textArea.transform, false);
            RectTransform phRt = (RectTransform)phGo.transform;
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;
            TextMeshProUGUI phText = phGo.AddComponent<TextMeshProUGUI>();
            phText.text = "type a number, or click Randomize";
            phText.fontSize = 16f;
            phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(1f, 1f, 1f, 0.32f);
            phText.alignment = TextAlignmentOptions.MidlineLeft;
            phText.richText = false;

            TMP_InputField field = rowGo.AddComponent<TMP_InputField>();
            field.targetGraphic = rowBg;
            field.textViewport = textAreaRt;
            field.textComponent = itText;
            field.placeholder = phText;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 24;
            field.restoreOriginalTextOnEscape = false;
            // Null navigation so Tab is consumed by the dev console's Tab completion
            // when both panels are open at the same time (rare but possible from the
            // sandbox menu); keeps focus from walking off mid-typing.
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            field.contentType = TMP_InputField.ContentType.IntegerNumber;

            return field;
        }

        private Button BuildButton(Transform parent, string label,
                                   Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                   Vector2 offsetMin, Vector2 offsetMax,
                                   UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            Image img = go.GetComponent<Image>();
            img.sprite = UIResourceHelper.GetBackgroundSprite();
            img.color = new Color(0.20f, 0.18f, 0.14f, 1f);
            img.raycastTarget = true;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            // Hover / press tints so the button feels responsive.
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.20f, 0.18f, 0.14f, 1f);
            cb.highlightedColor = new Color(0.34f, 0.30f, 0.22f, 1f);
            cb.pressedColor = new Color(0.10f, 0.09f, 0.07f, 1f);
            cb.selectedColor = cb.highlightedColor;
            cb.disabledColor = new Color(0.18f, 0.16f, 0.12f, 0.6f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero;
            lblRt.offsetMax = Vector2.zero;
            TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = label;
            lbl.fontSize = 20f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = new Color(0.96f, 0.92f, 0.78f, 1f);
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.richText = false;

            return btn;
        }

        private void EnsureStatusRefresh()
        {
            if (_statusLabel == null || WorldSeedService.Instance == null) return;
            _statusLabel.text = $"Current seed: {WorldSeedService.Instance.CurrentSeed}";
        }

        private void OnRandomizeClicked()
        {
            if (_seedInput == null || WorldSeedService.Instance == null) return;
            int fresh = WorldSeedService.Instance.GenerateRandomSeed();
            _seedInput.SetTextWithoutNotify(fresh.ToString());
            _seedInput.caretPosition = _seedInput.text.Length;
            if (_seedInput.isFocused == false) _seedInput.ActivateInputField();
        }

        private void OnCreateClicked()
        {
            if (_seedInput == null || WorldSeedService.Instance == null) return;

            string raw = _seedInput.text == null ? string.Empty : _seedInput.text.Trim();
            int parsed;
            if (string.IsNullOrEmpty(raw))
            {
                // Empty input → re-roll automatically so the player can't get stuck.
                parsed = WorldSeedService.Instance.GenerateRandomSeed();
            }
            else if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                                    System.Globalization.CultureInfo.InvariantCulture, out parsed))
            {
                // Bad input. Refuse and ping the status label rather than silently
                // dropping the player into an unintended world.
                if (_statusLabel != null)
                {
                    _statusLabel.text = $"'{raw}' is not a valid 32-bit integer.";
                    _statusLabel.color = new Color(0.95f, 0.55f, 0.45f, 1f);
                }
                return;
            }

            // Apply + regenerate. SetSeed fires the OnSeedChanged event which
            // ProceduralGridGenerator listens to and uses as the reroll trigger.
            WorldSeedService.Instance.SetSeed(parsed, userProvided: true);

            if (_statusLabel != null) _statusLabel.color = new Color(0.92f, 0.78f, 0.42f, 1f);
            Hide();
            if (MainMenuUI.Instance != null) MainMenuUI.Instance.Hide();
        }
    }
}
