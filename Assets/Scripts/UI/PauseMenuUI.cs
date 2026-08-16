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

        private GameObject _rootGo;            // dimmer + click-blocker
        private GameObject _mainPanel;
        private GameObject _optionsPanel;
        private GameObject _gameplayPanel;
        private GameObject _controlsPanel;

        private readonly Stack<GameObject> _panelBackstack = new Stack<GameObject>();

        // Rebinding state
        private KeyAction? _listeningAction = null;
        private readonly Dictionary<KeyAction, TextMeshProUGUI> _keyButtonLabels = new Dictionary<KeyAction, TextMeshProUGUI>();

        // slow-mo effect (cinematics, weather reveal) can restore to 0.5 not 1.
        private float _priorTimeScale = 1f;

        // Cached so the gameplay preset buttons can refresh their active state
        // without re-walking the SaveGameManager every time the panel opens.
        private readonly List<Button> _presetButtons = new List<Button>();
        private readonly List<float>  _presetSeconds = new List<float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[PauseMenuUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<PauseMenuUI>();
        }

        private UnityEngine.UIElements.VisualElement _toolkitPauseRoot;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas", "UIRoot");
            UIResourceHelper.EnsureEventSystem();
            BuildHierarchy(canvas);

            if (_rootGo != null)
            {
                Canvas pauseCanvas = _rootGo.AddComponent<Canvas>();
                pauseCanvas.overrideSorting = true;
                pauseCanvas.sortingOrder = 1000;
                _rootGo.AddComponent<GraphicRaycaster>();
            }

            var uiDoc = GetComponent<UnityEngine.UIElements.UIDocument>();
            if (uiDoc == null) uiDoc = FindAnyObjectByType<UnityEngine.UIElements.UIDocument>();
            if (uiDoc != null && uiDoc.rootVisualElement != null)
            {
                _toolkitPauseRoot = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(uiDoc.rootVisualElement, "PauseRoot");
                if (_toolkitPauseRoot != null)
                {
                    var resumeBtn = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(_toolkitPauseRoot, "ResumeBtn");
                    var saveBtn = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(_toolkitPauseRoot, "SaveBtn");
                    var optionsBtn = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(_toolkitPauseRoot, "OptionsBtn");
                    var mainMenuBtn = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(_toolkitPauseRoot, "MainMenuBtn");

                    if (resumeBtn != null) resumeBtn.clicked += Hide;
                    if (saveBtn != null) saveBtn.clicked += SaveCurrentWorld;
                    if (optionsBtn != null) optionsBtn.clicked += EnterOptionsPanel;
                    if (mainMenuBtn != null) mainMenuBtn.clicked += ExitToMainMenu;
                }
            }

            _rootGo.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = _priorTimeScale <= 0f ? 1f : _priorTimeScale;
                InputReader.BlockGameplayInput = false;
            }
        }


        public bool IsOpen => (_rootGo != null && _rootGo.activeSelf) || (_toolkitPauseRoot != null && _toolkitPauseRoot.style.display == UnityEngine.UIElements.DisplayStyle.Flex);

        public void Show()
        {
            if (IsOpen) return;
            if (_rootGo != null) _rootGo.SetActive(true);
            if (_toolkitPauseRoot != null) _toolkitPauseRoot.style.display = UnityEngine.UIElements.DisplayStyle.Flex;

            EnterMainPanel();

            _priorTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            InputReader.BlockGameplayInput = true;
        }

        public void Hide()
        {
            if (!IsOpen) return;
            if (_rootGo != null) _rootGo.SetActive(false);
            if (_toolkitPauseRoot != null) _toolkitPauseRoot.style.display = UnityEngine.UIElements.DisplayStyle.None;

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

        public void EnterControlsPanel()
        {
            _panelBackstack.Push(_optionsPanel);
            SwitchPanel(_controlsPanel);
            CancelRebinding();
            RefreshKeyLabels();
        }

        public void EnterGameplayPanel()
        {
            _panelBackstack.Push(_mainPanel);
            SwitchPanel(_gameplayPanel);
            RefreshPresetHighlights();
        }

        public void Back()
        {
            CancelRebinding();
            if (_panelBackstack.Count == 0) { Hide(); return; }
            SwitchPanel(_panelBackstack.Pop());
        }

        private void SwitchPanel(GameObject panel)
        {
            if (_mainPanel     != null) _mainPanel.SetActive(panel == _mainPanel);
            if (_optionsPanel  != null) _optionsPanel.SetActive(panel == _optionsPanel);
            if (_controlsPanel != null) _controlsPanel.SetActive(panel == _controlsPanel);
            if (_gameplayPanel != null) _gameplayPanel.SetActive(panel == _gameplayPanel);
        }

        private void Update()
        {
            // Don't even run when in-world state isn't established.
            if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsVisible) return;
            if (WorldSetupUI.Instance != null && WorldSetupUI.Instance.IsVisible) return;

            // If the dev console is open it consumes Escape to close itself first.
            if (DevConsole.Instance != null && DevConsole.Instance.IsOpen) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // Handle live key rebinding if a key slot is currently waiting for input
            if (_listeningAction.HasValue)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    CancelRebinding();
                    return;
                }

                foreach (var keyControl in kb.allControls)
                {
                    if (keyControl is UnityEngine.InputSystem.Controls.KeyControl kCtrl && kCtrl.wasPressedThisFrame)
                    {
                        Key pressedKey = kCtrl.keyCode;
                        if (pressedKey != Key.None && pressedKey != Key.Escape && pressedKey != Key.Backquote)
                        {
                            KeyAction act = _listeningAction.Value;
                            KeyRebindingManager.SetKey(act, pressedKey);
                            CancelRebinding();
                            RefreshKeyLabels();
                            return;
                        }
                    }
                }
                return;
            }

            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (_panelBackstack.Count > 0)
                {
                    // Inside Options, Controls, or Gameplay: first Esc backs out instead of closing.
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
                // so the player returns to the world immediately.
                SaveCurrentWorld();
                Hide();
            }
        }

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

        private void BuildHierarchy(Canvas canvas)
        {
            _rootGo = NewFullScreen("PauseMenuRoot", canvas.transform, new Color(0f, 0f, 0f, 0.55f));

            _mainPanel     = NewCenteredPanel(_rootGo.transform, "MainPanel",     new Vector2(500f, 460f));
            _optionsPanel  = NewCenteredPanel(_rootGo.transform, "OptionsPanel",  new Vector2(560f, 420f));
            _controlsPanel = NewCenteredPanel(_rootGo.transform, "ControlsPanel", new Vector2(600f, 560f));
            _gameplayPanel = NewCenteredPanel(_rootGo.transform, "GameplayPanel", new Vector2(560f, 480f));

            BuildMainPanel(_mainPanel.transform);
            BuildOptionsPanel(_optionsPanel.transform);
            BuildControlsPanel(_controlsPanel.transform);
            BuildGameplayPanel(_gameplayPanel.transform);

            SwitchPanel(_mainPanel);
        }

        private static GameObject NewFullScreen(string name, Transform parent, Color tint)
        {
            // Transparent container (no black background image blocking the view)
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        private static GameObject NewCenteredPanel(Transform parent, string name, Vector2 size)
        {
            GameObject cardGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(parent, false);
            RectTransform cardRt = (RectTransform)cardGo.transform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = size;
            cardRt.anchoredPosition = Vector2.zero;
            Image cardBg = cardGo.GetComponent<Image>();
            cardBg.sprite = UIResourceHelper.GetBackgroundSprite();
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.22f, 0.16f, 0.11f, 0.98f); // Warm timber frame
            cardBg.raycastTarget = true;

            GameObject innerGo = new GameObject("InnerBoard", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(cardGo.transform, false);
            RectTransform innerRt = (RectTransform)innerGo.transform;
            innerRt.anchorMin = Vector2.zero; innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(10f, 10f); innerRt.offsetMax = new Vector2(-10f, -10f);
            Image innerBg = innerGo.GetComponent<Image>();
            innerBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            innerBg.type = Image.Type.Sliced;
            innerBg.color = new Color(0.12f, 0.09f, 0.06f, 0.95f);

            return cardGo;
        }

        private static void BuildHeader(Transform parent, string text)
        {
            GameObject bannerGo = new GameObject("HeaderBanner", typeof(RectTransform), typeof(Image));
            bannerGo.transform.SetParent(parent, false);
            RectTransform bannerRt = (RectTransform)bannerGo.transform;
            bannerRt.anchorMin = new Vector2(0.5f, 1f);
            bannerRt.anchorMax = new Vector2(0.5f, 1f);
            bannerRt.pivot = new Vector2(0.5f, 1f);
            bannerRt.sizeDelta = new Vector2(360f, 52f);
            bannerRt.anchoredPosition = new Vector2(0f, -24f);
            Image bannerBg = bannerGo.GetComponent<Image>();
            bannerBg.sprite = UIResourceHelper.GetBackgroundSprite();
            bannerBg.type = Image.Type.Sliced;
            bannerBg.color = new Color(0.35f, 0.24f, 0.15f, 1f);

            GameObject titleTextGo = new GameObject("HeaderTitleText", typeof(RectTransform));
            titleTextGo.transform.SetParent(bannerGo.transform, false);
            RectTransform titleTextRt = (RectTransform)titleTextGo.transform;
            titleTextRt.anchorMin = Vector2.zero; titleTextRt.anchorMax = Vector2.one;
            titleTextRt.offsetMin = Vector2.zero; titleTextRt.offsetMax = Vector2.zero;

            Text txt = titleTextGo.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 24;
            txt.fontStyle = UnityEngine.FontStyle.Bold;
            txt.color = new Color(1f, 0.88f, 0.45f, 1f);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
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
            img.sprite = UIResourceHelper.GetBackgroundSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.38f, 0.26f, 0.16f, 1f);
            img.raycastTarget = true;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor      = new Color(0.38f, 0.26f, 0.16f, 1f);
            cb.highlightedColor = new Color(0.54f, 0.38f, 0.24f, 1f);
            cb.pressedColor     = new Color(0.24f, 0.16f, 0.10f, 1f);
            cb.selectedColor    = cb.highlightedColor;
            cb.disabledColor    = new Color(0.20f, 0.18f, 0.14f, 0.6f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero;
            lblRt.offsetMax = Vector2.zero;

            Text txt = lblGo.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 18;
            txt.fontStyle = UnityEngine.FontStyle.Bold;
            txt.color = new Color(1f, 0.94f, 0.82f, 1f);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;

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

            BuildMenuButton(parent, "Customize Controls", new Vector2(0f, -220f), new Vector2(300f, 52f), EnterControlsPanel);
            BuildMenuButton(parent, "Back", new Vector2(0f, -310f), new Vector2(220f, 50f), Back);
        }

        private void BuildControlsPanel(Transform parent)
        {
            BuildHeader(parent, "Customize Controls");

            // Subtitle hint
            GameObject sub = NewChild(parent, "ControlsSub");
            RectTransform sRt = SetAnchoredTopBand(sub.transform, 56f, 82f);
            TextMeshProUGUI sTxt = sub.AddComponent<TextMeshProUGUI>();
            sTxt.text = "Click a key to remap. Press Esc to cancel.";
            sTxt.fontSize = 13.5f;
            sTxt.fontStyle = FontStyles.Italic;
            sTxt.color = new Color(0.85f, 0.80f, 0.65f, 0.90f);
            sTxt.alignment = TextAlignmentOptions.Center;

            // Scroll / List Area
            GameObject listContainer = new GameObject("ControlsScrollArea", typeof(RectTransform));
            listContainer.transform.SetParent(parent, false);
            RectTransform lcRt = (RectTransform)listContainer.transform;
            lcRt.anchorMin = new Vector2(0f, 0f);
            lcRt.anchorMax = new Vector2(1f, 1f);
            lcRt.pivot = new Vector2(0.5f, 0.5f);
            lcRt.offsetMin = new Vector2(30f, 84f);
            lcRt.offsetMax = new Vector2(-30f, -90f);

            _keyButtonLabels.Clear();
            var actions = (KeyAction[])System.Enum.GetValues(typeof(KeyAction));

            float rowHeight = 36f;
            float rowSpacing = 6f;

            for (int i = 0; i < actions.Length; i++)
            {
                KeyAction act = actions[i];
                float yOffset = -i * (rowHeight + rowSpacing);

                GameObject rowGo = new GameObject($"Row_{act}", typeof(RectTransform), typeof(Image));
                rowGo.transform.SetParent(listContainer.transform, false);
                RectTransform rRt = (RectTransform)rowGo.transform;
                rRt.anchorMin = new Vector2(0f, 1f);
                rRt.anchorMax = new Vector2(1f, 1f);
                rRt.pivot = new Vector2(0.5f, 1f);
                rRt.anchoredPosition = new Vector2(0f, yOffset);
                rRt.sizeDelta = new Vector2(0f, rowHeight);

                Image rowImg = rowGo.GetComponent<Image>();
                rowImg.color = (i % 2 == 0) ? new Color(0.12f, 0.10f, 0.08f, 0.55f) : new Color(0.16f, 0.13f, 0.10f, 0.35f);

                // Action Label
                GameObject labelGo = new GameObject("ActionLabel", typeof(RectTransform));
                labelGo.transform.SetParent(rowGo.transform, false);
                RectTransform lRt = (RectTransform)labelGo.transform;
                lRt.anchorMin = new Vector2(0f, 0f);
                lRt.anchorMax = new Vector2(0.55f, 1f);
                lRt.offsetMin = new Vector2(12f, 0f);
                lRt.offsetMax = Vector2.zero;

                TextMeshProUGUI lTxt = labelGo.AddComponent<TextMeshProUGUI>();
                lTxt.text = KeyRebindingManager.GetActionLabel(act);
                lTxt.fontSize = 15f;
                lTxt.fontStyle = FontStyles.Bold;
                lTxt.color = new Color(0.95f, 0.92f, 0.85f, 1f);
                lTxt.alignment = TextAlignmentOptions.MidlineLeft;

                // Key Button
                GameObject keyBtnGo = new GameObject("KeyButton", typeof(RectTransform), typeof(Image), typeof(Button));
                keyBtnGo.transform.SetParent(rowGo.transform, false);
                RectTransform kbRt = (RectTransform)keyBtnGo.transform;
                kbRt.anchorMin = new Vector2(0.60f, 0.12f);
                kbRt.anchorMax = new Vector2(0.98f, 0.88f);
                kbRt.offsetMin = Vector2.zero;
                kbRt.offsetMax = Vector2.zero;

                Image btnImg = keyBtnGo.GetComponent<Image>();
                btnImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                btnImg.type = Image.Type.Sliced;
                btnImg.color = new Color(0.24f, 0.18f, 0.12f, 0.95f);

                Button btn = keyBtnGo.GetComponent<Button>();
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.2f, 1.15f, 1.05f, 1f);
                cb.pressedColor = new Color(0.8f, 0.75f, 0.65f, 1f);
                btn.colors = cb;

                // Key Text inside button
                GameObject btnTxtGo = new GameObject("KeyText", typeof(RectTransform));
                btnTxtGo.transform.SetParent(keyBtnGo.transform, false);
                RectTransform btRt = (RectTransform)btnTxtGo.transform;
                btRt.anchorMin = Vector2.zero;
                btRt.anchorMax = Vector2.one;
                btRt.offsetMin = Vector2.zero;
                btRt.offsetMax = Vector2.zero;

                TextMeshProUGUI bTxt = btnTxtGo.AddComponent<TextMeshProUGUI>();
                bTxt.fontSize = 14f;
                bTxt.fontStyle = FontStyles.Bold;
                bTxt.color = new Color(1f, 0.88f, 0.45f, 1f);
                bTxt.alignment = TextAlignmentOptions.Center;

                _keyButtonLabels[act] = bTxt;

                KeyAction capturedAct = act;
                btn.onClick.AddListener(() => StartRebinding(capturedAct));
            }

            // Bottom Buttons (Reset Defaults / Back)
            BuildMenuButton(parent, "Reset Defaults", new Vector2(-120f, -485f), new Vector2(180f, 44f), () =>
            {
                KeyRebindingManager.ResetToDefaults();
                CancelRebinding();
                RefreshKeyLabels();
            });

            BuildMenuButton(parent, "Back", new Vector2(120f, -485f), new Vector2(180f, 44f), Back);
        }

        private void StartRebinding(KeyAction action)
        {
            _listeningAction = action;
            RefreshKeyLabels();
        }

        private void CancelRebinding()
        {
            _listeningAction = null;
            RefreshKeyLabels();
        }

        private void RefreshKeyLabels()
        {
            foreach (var kvp in _keyButtonLabels)
            {
                if (kvp.Value == null) continue;
                if (_listeningAction.HasValue && _listeningAction.Value == kvp.Key)
                {
                    kvp.Value.text = "<color=#FF6E6E><b>[PRESS KEY]</b></color>";
                }
                else
                {
                    kvp.Value.text = $"<b>{KeyRebindingManager.GetKeyDisplayName(kvp.Key)}</b>";
                }
            }
        }

        private void BuildGameplayPanel(Transform parent)
        {
            BuildHeader(parent, "Gameplay");

            // --- World Name section (editable by host) ---
            GameObject wnHeader = NewChild(parent, "WorldNameHeader");
            RectTransform whRt = SetAnchoredTopBand(wnHeader.transform, 60f, 95f);
            TextMeshProUGUI whTxt = wnHeader.AddComponent<TextMeshProUGUI>();
            whTxt.text = "World Name";
            whTxt.fontSize = 18f;
            whTxt.color = new Color(0.92f, 0.88f, 0.78f, 1f);
            whTxt.alignment = TextAlignmentOptions.Center;
            whTxt.richText = false;

            GameObject wnRow = new GameObject("WorldNameRow", typeof(RectTransform), typeof(Image));
            wnRow.transform.SetParent(parent, false);
            RectTransform wnRt = (RectTransform)wnRow.transform;
            wnRt.anchorMin = new Vector2(0.5f, 1f);
            wnRt.anchorMax = new Vector2(0.5f, 1f);
            wnRt.pivot = new Vector2(0.5f, 1f);
            wnRt.anchoredPosition = new Vector2(0f, -100f);
            wnRt.sizeDelta = new Vector2(400f, 44f);
            Image wnBg = wnRow.GetComponent<Image>();
            wnBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            wnBg.type = Image.Type.Sliced;
            wnBg.color = new Color(0.06f, 0.05f, 0.05f, 0.98f);

            GameObject wnTextArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            wnTextArea.transform.SetParent(wnRow.transform, false);
            RectTransform wntaRt = (RectTransform)wnTextArea.transform;
            wntaRt.anchorMin = Vector2.zero; wntaRt.anchorMax = Vector2.one;
            wntaRt.offsetMin = new Vector2(14f, 4f); wntaRt.offsetMax = new Vector2(-14f, -4f);

            GameObject wnTextGo = new GameObject("Text", typeof(RectTransform));
            wnTextGo.transform.SetParent(wnTextArea.transform, false);
            RectTransform wntRt = (RectTransform)wnTextGo.transform;
            wntRt.anchorMin = Vector2.zero; wntRt.anchorMax = Vector2.one;
            wntRt.offsetMin = Vector2.zero; wntRt.offsetMax = Vector2.zero;
            TextMeshProUGUI wnTxt = wnTextGo.AddComponent<TextMeshProUGUI>();
            wnTxt.fontSize = 17f;
            wnTxt.fontStyle = FontStyles.Bold;
            wnTxt.color = new Color(0.96f, 0.94f, 0.88f, 1f);
            wnTxt.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_InputField wnField = wnRow.AddComponent<TMP_InputField>();
            wnField.targetGraphic = wnBg;
            wnField.textViewport = wntaRt;
            wnField.textComponent = wnTxt;
            wnField.lineType = TMP_InputField.LineType.SingleLine;
            wnField.characterLimit = 32;
            string currentName = SaveGameManager.Instance != null ? SaveGameManager.Instance.ActiveSaveName : "My Willowstead";
            wnField.text = currentName;
            wnField.onEndEdit.AddListener(newName =>
            {
                if (SaveGameManager.Instance != null && !string.IsNullOrWhiteSpace(newName))
                {
                    SaveGameManager.Instance.SetActiveSaveName(newName);
                    if (ItemNotificationManager.Instance != null)
                    {
                        ItemNotificationManager.Instance.TriggerNotification($"World renamed: {newName.Trim()}", UIResourceHelper.GetSaveIconSprite(), new Color(0.4f, 1f, 0.4f));
                    }
                }
            });

            // --- Autosave section ---
            GameObject asHeader = NewChild(parent, "AutosaveHeader");
            RectTransform ahRt = SetAnchoredTopBand(asHeader.transform, 160f, 195f);
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
                rt.anchoredPosition = new Vector2(xPos, -245f);

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
            fhTxt.text = "Host can rename the active world at any time.\nChanges will save automatically.";
            fhTxt.fontSize = 13f;
            fhTxt.fontStyle = FontStyles.Italic;
            fhTxt.color = new Color(0.78f, 0.72f, 0.62f, 0.85f);
            fhTxt.alignment = TextAlignmentOptions.Center;
            fhTxt.richText = false;
            fhTxt.textWrappingMode = TextWrappingModes.Normal;

            BuildMenuButton(parent, "Back", new Vector2(0f, -440f), new Vector2(220f, 50f), Back);
        }


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
            rt.offsetMin = new Vector2(36f, -toTop);
            rt.offsetMax = new Vector2(-36f, -fromTop);
            return rt;
        }

        private static void BuildSliderVisuals(Slider slider)
        {
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(slider.transform, false);
            RectTransform bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = new Vector2(0f, 0.35f);
            bgRt.anchorMax = new Vector2(1f, 0.65f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.10f, 0.09f, 0.07f, 1f);

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

        // accidentally drift between read and write sites.
        private static class PlayerPrefKeys
        {
            public const string MasterVolume = "master_volume";
        }
    }
}
