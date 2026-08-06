using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Willowstead.Debugging;
using Willowstead.Input;
using Willowstead.Persistence;
using Willowstead.Player;

namespace Willowstead.UI
{
    /// <summary>
    /// In-world pause menu. Hooks <c>Esc</c> and offers Save / Options / Gameplay / Exit.
    ///
    /// Stacked panels so a Back button on Options or Gameplay returns to the
    /// Main panel; an additional Esc press closes the menu entirely.
    ///
    /// Coexistence with the dev console: if the console is open, this menu
    /// defers to it (the console already handles its own Escape-to-close first).
    /// If the Main Menu or World Setup modal is on screen, this menu ignores Esc
    /// entirely so the player doesn't accidentally open two modals at once.
    ///
    /// Time and input:
    ///   • <c>Time.timeScale = 0f</c> while shown so weather/day tick freeze.
    ///   • <c>InputReader.BlockGameplayInput = true</c> so WASD / hotbar
    ///     digits / interact keys cannot fire behind the panel.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        public static PauseMenuUI Instance { get; private set; }

        // ─── Hierarchy ─────────────────────────────────────────────────
        private GameObject _rootGo;            // dimmer + click-blocker
        private GameObject _mainPanel;
        private GameObject _optionsPanel;
        private GameObject _gameplayPanel;

        private readonly Stack<GameObject> _panelBackstack = new Stack<GameObject>();

        // Snapshot of Time.timeScale at the moment we paused, so a future
        // slow-mo effect (cinematics, weather reveal) can restore to 0.5 not 1.
        private float _priorTimeScale = 1f;

        // Cached so the gameplay preset buttons can refresh their active state
        // without re-walking the SaveGameManager every time the panel opens.
        private readonly List<Button> _presetButtons = new List<Button>();
        private readonly List<float>  _presetSeconds = new List<float>();

        // ─── Bootstrap ─────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[PauseMenuUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<PauseMenuUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas", "UIRoot");
            UIResourceHelper.EnsureEventSystem();
            BuildHierarchy(canvas);

            _rootGo.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                // Restore time + input if we're torn down mid-pause so a domain reload
                // (or scene swap mid-pause) can't leave the world frozen.
                Time.timeScale = _priorTimeScale <= 0f ? 1f : _priorTimeScale;
                InputReader.BlockGameplayInput = false;
            }
        }

        // ─── Public API ────────────────────────────────────────────────

        public bool IsOpen => _rootGo != null && _rootGo.activeSelf;

        public void Show()
        {
            if (_rootGo == null) return;
            if (IsOpen) return;
            _rootGo.SetActive(true);

            EnterMainPanel();

            _priorTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            InputReader.BlockGameplayInput = true;
        }

        public void Hide()
        {
            if (_rootGo == null) return;
            if (!IsOpen) return;
            _rootGo.SetActive(false);
            Time.timeScale = _priorTimeScale <= 0f ? 1f : _priorTimeScale;
            InputReader.BlockGameplayInput = false;
            _panelBackstack.Clear();
        }

        public void Toggle() { if (IsOpen) Hide(); else Show(); }

        public void EnterMainPanel()
        {
            _panelBackstack.Clear();
            SwitchPanel(_mainPanel);
        }

        public void EnterOptionsPanel()
        {
            _panelBackstack.Push(_mainPanel);
            SwitchPanel(_optionsPanel);
            RefreshPresetHighlights();
        }

        public void EnterGameplayPanel()
        {
            _panelBackstack.Push(_mainPanel);
            SwitchPanel(_gameplayPanel);
            RefreshPresetHighlights();
        }

        public void Back()
        {
            if (_panelBackstack.Count == 0) { Hide(); return; }
            SwitchPanel(_panelBackstack.Pop());
        }

        private void SwitchPanel(GameObject panel)
        {
            if (_mainPanel     != null) _mainPanel.SetActive(panel == _mainPanel);
            if (_optionsPanel  != null) _optionsPanel.SetActive(panel == _optionsPanel);
            if (_gameplayPanel != null) _gameplayPanel.SetActive(panel == _gameplayPanel);
        }

        // ─── Update / input ────────────────────────────────────────────
        private void Update()
        {
            // Don't even run when in-world state isn't established.
            if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsVisible) return;
            if (WorldSetupUI.Instance != null && WorldSetupUI.Instance.IsVisible) return;

            // If the dev console is open it consumes Escape to close itself first.
            if (DevConsole.Instance != null && DevConsole.Instance.IsOpen) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (_panelBackstack.Count > 0)
                {
                    // Inside Options or Gameplay: first Esc backs out instead of closing.
                    Back();
                }
                else if (IsOpen)
                {
                    Hide();
                }
                else
                {
                    Show();
                }
            }
            else if (IsOpen && kb.f5Key.wasPressedThisFrame)
            {
                // Quick-save convenience: snap a silent save and close the menu
                // so the player returns to the world immediately.
                SaveCurrentWorld();
                Hide();
            }
        }

        // ─── Actions ───────────────────────────────────────────────────

        private void SaveCurrentWorld()
        {
            if (SaveGameManager.Instance == null) return;
            if (SaveGameManager.Instance.SaveToAutosave())
            {
                if (ItemNotificationManager.Instance != null)
                {
                    ItemNotificationManager.Instance.TriggerNotification("Game saved", UIResourceHelper.GetSaveIconSprite(), new Color(0.4f, 1.0f, 0.4f));
                }
                PushToast("Game saved");
            }
        }

        private void ExitToMainMenu()
        {
            Hide();
            if (SaveGameManager.Instance != null)
                SaveGameManager.Instance.SetInWorld(false);
            if (MainMenuUI.Instance != null)
                MainMenuUI.Instance.Show();
        }

        private static void PushToast(string msg)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PauseMenu] {msg}");
#endif
        }

        // ─── Hierarchy construction ────────────────────────────────────

        private void BuildHierarchy(Canvas canvas)
        {
            _rootGo = NewFullScreen("PauseMenuRoot", canvas.transform, new Color(0f, 0f, 0f, 0.55f));

            _mainPanel     = NewCenteredPanel(_rootGo.transform, "MainPanel",     new Vector2(500f, 460f));
            _optionsPanel  = NewCenteredPanel(_rootGo.transform, "OptionsPanel",  new Vector2(560f, 520f));
            _gameplayPanel = NewCenteredPanel(_rootGo.transform, "GameplayPanel", new Vector2(560f, 480f));

            BuildMainPanel(_mainPanel.transform);
            BuildOptionsPanel(_optionsPanel.transform);
            BuildGameplayPanel(_gameplayPanel.transform);

            SwitchPanel(_mainPanel);
        }

        private static GameObject NewFullScreen(string name, Transform parent, Color tint)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = go.GetComponent<Image>();
            img.color = tint;
            img.raycastTarget = true;
            img.sprite = UIResourceHelper.GetBackgroundSprite();
            return go;
        }

        private static GameObject NewCenteredPanel(Transform parent, string name, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            Image img = go.GetComponent<Image>();
            img.color = new Color(0.12f, 0.10f, 0.08f, 0.94f);
            img.raycastTarget = true;
            img.sprite = UIResourceHelper.GetBackgroundSprite();
            return go;
        }

        private static void BuildHeader(Transform parent, string text)
        {
            GameObject go = new GameObject("Header", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(24f, -68f);
            rt.offsetMax = new Vector2(-24f, -16f);
            TextMeshProUGUI txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.fontSize = 30f;
            txt.fontStyle = FontStyles.Bold;
            txt.color = new Color(0.95f, 0.88f, 0.62f, 1f);
            txt.alignment = TextAlignmentOptions.Center;
            txt.richText = false;
        }

        private static Button BuildMenuButton(Transform parent, string label, Vector2 anchoredPos,
                                               Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            Image img = go.GetComponent<Image>();
            img.color = new Color(0.20f, 0.18f, 0.14f, 1f);
            img.sprite = UIResourceHelper.GetBackgroundSprite();
            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor      = new Color(0.20f, 0.18f, 0.14f, 1f);
            cb.highlightedColor = new Color(0.36f, 0.30f, 0.22f, 1f);
            cb.pressedColor     = new Color(0.10f, 0.09f, 0.07f, 1f);
            cb.selectedColor    = cb.highlightedColor;
            cb.disabledColor    = new Color(0.18f, 0.16f, 0.12f, 0.6f);
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
            lbl.fontSize = 22f;
            lbl.color = new Color(0.96f, 0.92f, 0.78f, 1f);
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.richText = false;
            return btn;
        }

        private void BuildMainPanel(Transform parent)
        {
            BuildHeader(parent, "Paused");
            BuildMenuButton(parent, "Save",     new Vector2(0f, -110f), new Vector2(320f, 56f),
                () => { SaveCurrentWorld(); Hide(); });
            BuildMenuButton(parent, "Options",  new Vector2(0f, -180f), new Vector2(320f, 56f),
                EnterOptionsPanel);
            BuildMenuButton(parent, "Gameplay", new Vector2(0f, -250f), new Vector2(320f, 56f),
                EnterGameplayPanel);
            BuildMenuButton(parent, "Exit",     new Vector2(0f, -320f), new Vector2(320f, 56f),
                ExitToMainMenu);

            // Hint footer.
            GameObject hint = new GameObject("Hint", typeof(RectTransform));
            hint.transform.SetParent(parent, false);
            RectTransform hRt = (RectTransform)hint.transform;
            hRt.anchorMin = new Vector2(0f, 0f);
            hRt.anchorMax = new Vector2(1f, 0f);
            hRt.pivot = new Vector2(0.5f, 0f);
            hRt.offsetMin = new Vector2(0f, 12f);
            hRt.offsetMax = new Vector2(0f, 36f);
            TextMeshProUGUI hTxt = hint.AddComponent<TextMeshProUGUI>();
            hTxt.text = "Esc to close · F5 to save";
            hTxt.fontSize = 14f;
            hTxt.fontStyle = FontStyles.Italic;
            hTxt.color = new Color(0.80f, 0.74f, 0.62f, 0.85f);
            hTxt.alignment = TextAlignmentOptions.Center;
            hTxt.richText = false;
        }

        private void BuildOptionsPanel(Transform parent)
        {
            BuildHeader(parent, "Options");

            // ── Master Volume slider ──
            GameObject header = NewChild(parent, "VolumeHeader");
            RectTransform hr = SetAnchoredTopBand(header.transform, 60f, 100f);
            TextMeshProUGUI hTxt = header.AddComponent<TextMeshProUGUI>();
            hTxt.text = "Master Volume";
            hTxt.fontSize = 18f;
            hTxt.color = new Color(0.92f, 0.88f, 0.78f, 1f);
            hTxt.alignment = TextAlignmentOptions.Center;
            hTxt.richText = false;

            GameObject sliderGo = NewChild(parent, "VolumeSlider");
            RectTransform sRt = SetAnchoredTopBand(sliderGo.transform, 110f, 150f);
            Slider slider = sliderGo.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            float saved = PlayerPrefs.GetFloat(PlayerPrefKeys.MasterVolume, AudioListener.volume);
            saved = Mathf.Clamp01(saved);
            slider.value = saved;
            AudioListener.volume = saved;

            slider.onValueChanged.AddListener(v =>
            {
                AudioListener.volume = v;
                PlayerPrefs.SetFloat(PlayerPrefKeys.MasterVolume, v);
                PlayerPrefs.Save();
            });
            BuildSliderVisuals(slider);

            // ── Controls list (read-only — matches the project's InputSystem_Actions) ──
            GameObject ctrlHeader = NewChild(parent, "ControlsHeader");
            RectTransform cRt = SetAnchoredTopBand(ctrlHeader.transform, 200f, 230f);
            TextMeshProUGUI cTxt = ctrlHeader.AddComponent<TextMeshProUGUI>();
            cTxt.text = "Controls";
            cTxt.fontSize = 18f;
            cTxt.color = new Color(0.92f, 0.88f, 0.78f, 1f);
            cTxt.alignment = TextAlignmentOptions.Center;
            cTxt.richText = false;

            GameObject ctrlList = NewChild(parent, "ControlsList");
            RectTransform clRt = (RectTransform)ctrlList.transform;
            clRt.anchorMin = new Vector2(0f, 0f);
            clRt.anchorMax = new Vector2(1f, 1f);
            clRt.pivot = new Vector2(0.5f, 0.5f);
            clRt.offsetMin = new Vector2(36f, 80f);
            clRt.offsetMax = new Vector2(-36f, -200f);
            TextMeshProUGUI clTxt = ctrlList.AddComponent<TextMeshProUGUI>();
            clTxt.text = "Move — WASD / Arrow Keys\n" +
                         "Sprint — Left Shift\n" +
                         "Interact / Tool — Left Mouse\n" +
                         "Inventory — I\n" +
                         "Shop — P\n" +
                         "Toggle Hotbar — Tab\n" +
                         "Pause — Esc\n" +
                         "Console — `  (developer builds)";
            clTxt.fontSize = 16f;
            clTxt.color = new Color(0.86f, 0.84f, 0.78f, 1f);
            clTxt.alignment = TextAlignmentOptions.Center;
            clTxt.richText = false;
            clTxt.textWrappingMode = TextWrappingModes.Normal;

            BuildMenuButton(parent, "Back", new Vector2(0f, -440f), new Vector2(220f, 50f), Back);
        }

        private void BuildGameplayPanel(Transform parent)
        {
            BuildHeader(parent, "Gameplay");

            GameObject asHeader = NewChild(parent, "AutosaveHeader");
            RectTransform ahRt = SetAnchoredTopBand(asHeader.transform, 60f, 100f);
            TextMeshProUGUI ahTxt = asHeader.AddComponent<TextMeshProUGUI>();
            ahTxt.text = "Autosave interval";
            ahTxt.fontSize = 18f;
            ahTxt.color = new Color(0.92f, 0.88f, 0.78f, 1f);
            ahTxt.alignment = TextAlignmentOptions.Center;
            ahTxt.richText = false;

            // 4 horizontally-arranged preset buttons (Disabled / 1 min / 5 min / 30 min)
            string[] labels  = { "Disabled", "1 min", "5 min", "30 min" };
            float[] seconds = { 0f,         60f,    300f,    1800f };

            float bandWidth = 480f;
            float btnWidth  = 110f;
            float bandLeft  = -bandWidth * 0.5f;
            float spacing   = (bandWidth - btnWidth * 4f) / 3f;

            for (int i = 0; i < labels.Length; i++)
            {
                float xPos = bandLeft + btnWidth * (i + 0.5f) + spacing * i;
                GameObject presetGo = new GameObject($"Preset_{labels[i]}",
                    typeof(RectTransform), typeof(Image), typeof(Button));
                presetGo.transform.SetParent(parent, false);
                RectTransform rt = (RectTransform)presetGo.transform;
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(btnWidth, 52f);
                rt.anchoredPosition = new Vector2(xPos, -160f);

                Image img = presetGo.GetComponent<Image>();
                img.color = new Color(0.20f, 0.18f, 0.14f, 1f);
                img.sprite = UIResourceHelper.GetBackgroundSprite();

                Button btn = presetGo.GetComponent<Button>();
                btn.targetGraphic = img;
                int idx = i; // capture for closure
                btn.onClick.AddListener(() => ApplyPreset(idx));

                GameObject lblGo = new GameObject("Label", typeof(RectTransform));
                lblGo.transform.SetParent(presetGo.transform, false);
                RectTransform lblRt = (RectTransform)lblGo.transform;
                lblRt.anchorMin = Vector2.zero;
                lblRt.anchorMax = Vector2.one;
                lblRt.offsetMin = Vector2.zero;
                lblRt.offsetMax = Vector2.zero;
                TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>();
                lbl.text = labels[i];
                lbl.fontSize = 16f;
                lbl.color = new Color(0.96f, 0.92f, 0.78f, 1f);
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.richText = false;

                _presetButtons.Add(btn);
                _presetSeconds.Add(seconds[i]);
            }

            // Footer hint.
            GameObject hint = NewChild(parent, "GameplayHint");
            RectTransform fhRt = (RectTransform)hint.transform;
            fhRt.anchorMin = new Vector2(0f, 0f);
            fhRt.anchorMax = new Vector2(1f, 0f);
            fhRt.pivot = new Vector2(0.5f, 0f);
            fhRt.offsetMin = new Vector2(0f, 60f);
            fhRt.offsetMax = new Vector2(0f, 110f);
            TextMeshProUGUI fhTxt = hint.AddComponent<TextMeshProUGUI>();
            fhTxt.text = "More tunables (day length, weather cadence) coming soon —\nfor now, the dev console `weather` command can reroll the active weather.";
            fhTxt.fontSize = 13f;
            fhTxt.fontStyle = FontStyles.Italic;
            fhTxt.color = new Color(0.78f, 0.72f, 0.62f, 0.85f);
            fhTxt.alignment = TextAlignmentOptions.Center;
            fhTxt.richText = false;
            fhTxt.textWrappingMode = TextWrappingModes.Normal;

            BuildMenuButton(parent, "Back", new Vector2(0f, -420f), new Vector2(220f, 50f), Back);
        }

        // ─── Helpers ───────────────────────────────────────────────────

        private static GameObject NewChild(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform SetAnchoredTopBand(Transform t, float fromTop, float toTop)
        {
            RectTransform rt = (RectTransform)t;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            // offsetMin.y is the distance up from the lower edge; offsetMax.y is
            // the distance up from the lower edge of the top. "Top band of
            // pixels fromTop..toTop measured downward from the panel's top."
            rt.offsetMin = new Vector2(36f, -toTop);
            rt.offsetMax = new Vector2(-36f, -fromTop);
            return rt;
        }

        private static void BuildSliderVisuals(Slider slider)
        {
            // Background
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(slider.transform, false);
            RectTransform bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = new Vector2(0f, 0.35f);
            bgRt.anchorMax = new Vector2(1f, 0.65f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.10f, 0.09f, 0.07f, 1f);

            // Fill area
            GameObject fa = new GameObject("Fill Area", typeof(RectTransform));
            fa.transform.SetParent(slider.transform, false);
            RectTransform faRt = (RectTransform)fa.transform;
            faRt.anchorMin = new Vector2(0f, 0.35f);
            faRt.anchorMax = new Vector2(1f, 0.65f);
            faRt.pivot = new Vector2(0.5f, 0.5f);
            faRt.offsetMin = new Vector2(8f, 0f);
            faRt.offsetMax = new Vector2(-8f, 0f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fa.transform, false);
            RectTransform fRt = (RectTransform)fill.transform;
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.74f, 0.62f, 0.40f, 1f);
            slider.fillRect = fRt;

            // Handle slide area
            GameObject ha = new GameObject("Handle Slide Area", typeof(RectTransform));
            ha.transform.SetParent(slider.transform, false);
            RectTransform haRt = (RectTransform)ha.transform;
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(8f, 0f);
            haRt.offsetMax = new Vector2(-8f, 0f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(ha.transform, false);
            RectTransform hRt = (RectTransform)handle.transform;
            hRt.anchorMin = new Vector2(0f, 0.5f);
            hRt.anchorMax = new Vector2(0f, 0.5f);
            hRt.pivot = new Vector2(0.5f, 0.5f);
            hRt.sizeDelta = new Vector2(20f, 28f);
            handle.GetComponent<Image>().color = new Color(0.94f, 0.88f, 0.62f, 1f);
            slider.handleRect = hRt;
            slider.targetGraphic = handle.GetComponent<Image>();
        }

        private void ApplyPreset(int idx)
        {
            if (idx < 0 || idx >= _presetSeconds.Count) return;
            float seconds = _presetSeconds[idx];
            if (SaveGameManager.Instance != null)
                SaveGameManager.Instance.SetAutosaveIntervalSeconds(seconds);
            RefreshPresetHighlights();
        }

        private void RefreshPresetHighlights()
        {
            if (_presetButtons.Count == 0) return;
            float current = SaveGameManager.Instance != null
                ? SaveGameManager.Instance.AutosaveIntervalSeconds
                : 300f;

            int activeIdx = ClosestPresetIndex(current);
            for (int i = 0; i < _presetButtons.Count; i++)
            {
                Image img = _presetButtons[i].GetComponent<Image>();
                if (img == null) continue;
                img.color = i == activeIdx
                    ? new Color(0.72f, 0.58f, 0.30f, 1f)   // amber for active
                    : new Color(0.20f, 0.18f, 0.14f, 1f);  // neutral for inactive
            }
        }

        private int ClosestPresetIndex(float seconds)
        {
            int best = 0;
            float bestDelta = Mathf.Abs(_presetSeconds[0] - seconds);
            for (int i = 1; i < _presetSeconds.Count; i++)
            {
                float d = Mathf.Abs(_presetSeconds[i] - seconds);
                if (d < bestDelta) { best = i; bestDelta = d; }
            }
            return best;
        }

        // Persisted PlayerPref keys — kept in one nested class so we don't
        // accidentally drift between read and write sites.
        private static class PlayerPrefKeys
        {
            public const string MasterVolume = "master_volume";
        }
    }
}
