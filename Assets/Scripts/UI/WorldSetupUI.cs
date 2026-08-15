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
    /// Redesigned Modal "Create New World" panel with rich fantasy board styling,
    /// metallic brass trim, parchment backing, distinct stylized input field,
    /// and responsive interactive buttons.
    /// </summary>
    public class WorldSetupUI : MonoBehaviour
    {
        public static WorldSetupUI Instance { get; private set; }

        private GameObject _panelGo;
        private TMP_InputField _seedInput;
        private TextMeshProUGUI _statusLabel;
        private Button _createButton;
        private Button _randomButton;
        private Button _closeButton;

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
            if (_seedInput != null && !_seedInput.isFocused)
            {
                _seedInput.SetTextWithoutNotify(newSeed.ToString());
            }
            if (_panelGo != null && _panelGo.activeSelf)
            {
                Hide();
            }
        }


        public void ShowIfFirstLaunch()
        {
            if (WorldSeedService.Instance == null) return;
            if (WorldSeedService.Instance.LastSeedWasUserProvided)
            {
                Hide();
                return;
            }
            int randomSeed = WorldSeedService.Instance.GenerateRandomSeed();
            if (_seedInput != null) _seedInput.text = randomSeed.ToString();
            Show();
        }

        public void Show()
        {
            if (_panelGo == null) return;
            _panelGo.SetActive(true);
            Input.InputReader.BlockGameplayInput = true;

            if (WorldSeedService.Instance != null && _seedInput != null)
            {
                if (string.IsNullOrEmpty(_seedInput.text))
                    _seedInput.text = !string.IsNullOrEmpty(WorldSeedService.Instance.CurrentSeedString)
                        ? WorldSeedService.Instance.CurrentSeedString
                        : WorldSeedService.Instance.CurrentSeed.ToString();
            }
            EnsureStatusRefresh();
        }

        public void Hide()
        {
            if (_panelGo == null) return;
            _panelGo.SetActive(false);
            Input.InputReader.BlockGameplayInput = false;
        }

        /// <summary>True when the Create World panel is active and visible on screen.</summary>
        public bool IsVisible => _panelGo != null && _panelGo.activeSelf;

        private void Update()
        {
            if (IsVisible)
            {
                Input.InputReader.BlockGameplayInput = true;
            }
        }


        private void BuildPanel(Canvas canvas)
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;

            _panelGo = new GameObject("WorldSetupPanel", typeof(RectTransform), typeof(Image));
            _panelGo.transform.SetParent(canvas.transform, false);
            RectTransform rootRt = (RectTransform)_panelGo.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            Image rootDim = _panelGo.GetComponent<Image>();
            rootDim.color = new Color(0.04f, 0.04f, 0.05f, 0.85f);
            rootDim.raycastTarget = true;

            GameObject windowGo = new GameObject("WindowCard", typeof(RectTransform));
            windowGo.transform.SetParent(_panelGo.transform, false);
            RectTransform winRt = (RectTransform)windowGo.transform;
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.pivot = new Vector2(0.5f, 0.5f);
            winRt.sizeDelta = new Vector2(640f, 420f);
            winRt.anchoredPosition = Vector2.zero;

            GameObject shadowGo = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            shadowGo.transform.SetParent(windowGo.transform, false);
            RectTransform shadowRt = (RectTransform)shadowGo.transform;
            shadowRt.anchorMin = Vector2.zero; shadowRt.anchorMax = Vector2.one;
            shadowRt.offsetMin = new Vector2(-10f, -10f); shadowRt.offsetMax = new Vector2(10f, 10f);
            Image shadowImg = shadowGo.GetComponent<Image>();
            shadowImg.sprite = UIResourceHelper.GetBackgroundSprite();
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = new Color(0f, 0f, 0f, 0.6f);

            GameObject woodGo = new GameObject("WoodBoard", typeof(RectTransform), typeof(Image));
            woodGo.transform.SetParent(windowGo.transform, false);
            RectTransform woodRt = (RectTransform)woodGo.transform;
            woodRt.anchorMin = Vector2.zero; woodRt.anchorMax = Vector2.one;
            woodRt.offsetMin = Vector2.zero; woodRt.offsetMax = Vector2.zero;
            Image woodImg = woodGo.GetComponent<Image>();
            woodImg.sprite = UIResourceHelper.GetBackgroundSprite();
            woodImg.type = Image.Type.Sliced;
            woodImg.color = new Color(0.22f, 0.16f, 0.11f, 0.98f);

            GameObject trimGo = new GameObject("GoldTrim", typeof(RectTransform), typeof(Image));
            trimGo.transform.SetParent(woodGo.transform, false);
            RectTransform trimRt = (RectTransform)trimGo.transform;
            trimRt.anchorMin = Vector2.zero; trimRt.anchorMax = Vector2.one;
            trimRt.offsetMin = new Vector2(4f, 4f); trimRt.offsetMax = new Vector2(-4f, -4f);
            Image trimImg = trimGo.GetComponent<Image>();
            trimImg.sprite = UIResourceHelper.GetBackgroundSprite();
            trimImg.type = Image.Type.Sliced;
            trimImg.color = new Color(0.76f, 0.62f, 0.34f, 0.65f);

            GameObject innerGo = new GameObject("InnerBoard", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(trimGo.transform, false);
            RectTransform innerRt = (RectTransform)innerGo.transform;
            innerRt.anchorMin = Vector2.zero; innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(3f, 3f); innerRt.offsetMax = new Vector2(-3f, -3f);
            Image innerBg = innerGo.GetComponent<Image>();
            innerBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            innerBg.type = Image.Type.Sliced;
            innerBg.color = new Color(0.11f, 0.09f, 0.07f, 0.98f);

            GameObject bannerGo = new GameObject("TitleBanner", typeof(RectTransform), typeof(Image));
            bannerGo.transform.SetParent(innerGo.transform, false);
            RectTransform bannerRt = (RectTransform)bannerGo.transform;
            bannerRt.anchorMin = new Vector2(0.5f, 1f);
            bannerRt.anchorMax = new Vector2(0.5f, 1f);
            bannerRt.pivot = new Vector2(0.5f, 1f);
            bannerRt.sizeDelta = new Vector2(360f, 46f);
            bannerRt.anchoredPosition = new Vector2(0f, -16f);
            Image bannerImg = bannerGo.GetComponent<Image>();
            bannerImg.sprite = UIResourceHelper.GetBackgroundSprite();
            bannerImg.type = Image.Type.Sliced;
            bannerImg.color = new Color(0.32f, 0.22f, 0.14f, 1f);

            GameObject titleGo = new GameObject("TitleText", typeof(RectTransform));
            titleGo.transform.SetParent(bannerGo.transform, false);
            RectTransform titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = Vector2.zero; titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = Vector2.zero; titleRt.offsetMax = Vector2.zero;

            TextMeshProUGUI title = titleGo.AddComponent<TextMeshProUGUI>();
            if (font != null) title.font = font;
            title.text = "CREATE NEW WORLD";
            title.fontSize = 20f;
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(1f, 0.88f, 0.48f, 1f);
            title.alignment = TextAlignmentOptions.Center;

            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(innerGo.transform, false);
            RectTransform closeRt = (RectTransform)closeGo.transform;
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-12f, -12f);
            closeRt.sizeDelta = new Vector2(32f, 32f);

            Image closeImg = closeGo.GetComponent<Image>();
            closeImg.sprite = UIResourceHelper.GetBackgroundSprite();
            closeImg.type = Image.Type.Sliced;
            closeImg.color = new Color(0.48f, 0.18f, 0.18f, 0.95f);

            _closeButton = closeGo.GetComponent<Button>();
            ColorBlock closeCb = _closeButton.colors;
            closeCb.normalColor = new Color(0.48f, 0.18f, 0.18f, 0.95f);
            closeCb.highlightedColor = new Color(0.68f, 0.24f, 0.24f, 1f);
            closeCb.pressedColor = new Color(0.30f, 0.12f, 0.12f, 1f);
            _closeButton.colors = closeCb;
            _closeButton.onClick.AddListener(Hide);

            GameObject closeTxtGo = new GameObject("X", typeof(RectTransform));
            closeTxtGo.transform.SetParent(closeGo.transform, false);
            RectTransform closeTxtRt = (RectTransform)closeTxtGo.transform;
            closeTxtRt.anchorMin = Vector2.zero; closeTxtRt.anchorMax = Vector2.one;
            closeTxtRt.offsetMin = Vector2.zero; closeTxtRt.offsetMax = Vector2.zero;
            var closeTxt = closeTxtGo.AddComponent<TextMeshProUGUI>();
            if (font != null) closeTxt.font = font;
            closeTxt.fontSize = 15;
            closeTxt.fontStyle = FontStyles.Bold;
            closeTxt.alignment = TextAlignmentOptions.Center;
            closeTxt.color = Color.white;
            closeTxt.text = "X";

            GameObject subGo = new GameObject("Subtitle", typeof(RectTransform));
            subGo.transform.SetParent(innerGo.transform, false);
            RectTransform subRt = (RectTransform)subGo.transform;
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -72f);
            subRt.sizeDelta = new Vector2(540f, 44f);

            TextMeshProUGUI sub = subGo.AddComponent<TextMeshProUGUI>();
            if (font != null) sub.font = font;
            sub.text = "Enter a custom seed to forge a deterministic world. Identical seeds reproduce the exact same landscape, rivers, and ponds.";
            sub.fontSize = 14f;
            sub.color = new Color(0.85f, 0.82f, 0.75f, 0.95f);
            sub.alignment = TextAlignmentOptions.Top;
            sub.textWrappingMode = TextWrappingModes.Normal;

            _seedInput = BuildSeedInputRow(innerGo.transform, font);

            GameObject stGo = new GameObject("Status", typeof(RectTransform));
            stGo.transform.SetParent(innerGo.transform, false);
            RectTransform stRt = (RectTransform)stGo.transform;
            stRt.anchorMin = new Vector2(0.5f, 0f);
            stRt.anchorMax = new Vector2(0.5f, 0f);
            stRt.pivot = new Vector2(0.5f, 0f);
            stRt.anchoredPosition = new Vector2(0f, 92f);
            stRt.sizeDelta = new Vector2(500f, 26f);

            _statusLabel = stGo.AddComponent<TextMeshProUGUI>();
            if (font != null) _statusLabel.font = font;
            _statusLabel.text = string.Empty;
            _statusLabel.fontSize = 13f;
            _statusLabel.color = new Color(0.92f, 0.80f, 0.52f, 1f);
            _statusLabel.alignment = TextAlignmentOptions.Center;

            _randomButton = BuildButton(innerGo.transform, "Randomize ⚄",
                new Vector2(-130f, 38f), new Vector2(210f, 48f), font,
                new Color(0.30f, 0.22f, 0.15f, 1f), OnRandomizeClicked);

            _createButton = BuildButton(innerGo.transform, "Embark World ✦",
                new Vector2(130f, 38f), new Vector2(210f, 48f), font,
                new Color(0.24f, 0.42f, 0.22f, 1f), OnCreateClicked);

            _panelGo.SetActive(false);
        }

        private TMP_InputField BuildSeedInputRow(Transform parent, TMP_FontAsset font)
        {
            GameObject rowGo = new GameObject("SeedRow", typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = new Vector2(0.5f, 1f);
            rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -145f);
            rowRt.sizeDelta = new Vector2(520f, 52f);

            Image rowBg = rowGo.GetComponent<Image>();
            rowBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            rowBg.type = Image.Type.Sliced;
            rowBg.color = new Color(0.06f, 0.05f, 0.05f, 0.98f);

            GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(rowGo.transform, false);
            RectTransform borderRt = (RectTransform)borderGo.transform;
            borderRt.anchorMin = Vector2.zero; borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero; borderRt.offsetMax = Vector2.zero;
            Image borderImg = borderGo.GetComponent<Image>();
            borderImg.sprite = UIResourceHelper.GetBackgroundSprite();
            borderImg.type = Image.Type.Sliced;
            borderImg.color = new Color(0.72f, 0.58f, 0.32f, 0.55f);

            GameObject lblGo = new GameObject("SeedLabel", typeof(RectTransform));
            lblGo.transform.SetParent(rowGo.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = new Vector2(0f, 0f);
            lblRt.anchorMax = new Vector2(0f, 1f);
            lblRt.pivot = new Vector2(0f, 0.5f);
            lblRt.offsetMin = new Vector2(16f, 0f);
            lblRt.offsetMax = new Vector2(110f, 0f);

            TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>();
            if (font != null) lbl.font = font;
            lbl.text = "World Seed:";
            lbl.fontSize = 15f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = new Color(1f, 0.88f, 0.52f, 1f);
            lbl.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(rowGo.transform, false);
            RectTransform textAreaRt = (RectTransform)textArea.transform;
            textAreaRt.anchorMin = new Vector2(0f, 0f);
            textAreaRt.anchorMax = new Vector2(1f, 1f);
            textAreaRt.pivot = new Vector2(0.5f, 0.5f);
            textAreaRt.offsetMin = new Vector2(120f, 6f);
            textAreaRt.offsetMax = new Vector2(-16f, -6f);

            GameObject inputTextGo = new GameObject("Text", typeof(RectTransform));
            inputTextGo.transform.SetParent(textArea.transform, false);
            RectTransform itRt = (RectTransform)inputTextGo.transform;
            itRt.anchorMin = Vector2.zero; itRt.anchorMax = Vector2.one;
            itRt.offsetMin = Vector2.zero; itRt.offsetMax = Vector2.zero;

            TextMeshProUGUI itText = inputTextGo.AddComponent<TextMeshProUGUI>();
            if (font != null) itText.font = font;
            itText.fontSize = 18f;
            itText.fontStyle = FontStyles.Bold;
            itText.color = new Color(0.96f, 0.94f, 0.88f, 1f);
            itText.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(textArea.transform, false);
            RectTransform phRt = (RectTransform)phGo.transform;
            phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero; phRt.offsetMax = Vector2.zero;

            TextMeshProUGUI phText = phGo.AddComponent<TextMeshProUGUI>();
            if (font != null) phText.font = font;
            phText.text = "Type any seed (e.g. Willowstead or 1234)...";
            phText.fontSize = 15f;
            phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(1f, 1f, 1f, 0.35f);
            phText.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_InputField field = rowGo.AddComponent<TMP_InputField>();
            field.targetGraphic = rowBg;
            field.textViewport = textAreaRt;
            field.textComponent = itText;
            field.placeholder = phText;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 32;
            field.restoreOriginalTextOnEscape = false;
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            field.contentType = TMP_InputField.ContentType.Standard;

            return field;
        }

        private Button BuildButton(Transform parent, string label, Vector2 centerPos, Vector2 size, TMP_FontAsset font, Color btnColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = centerPos;
            rt.sizeDelta = size;

            Image img = go.GetComponent<Image>();
            img.sprite = UIResourceHelper.GetBackgroundSprite();
            img.type = Image.Type.Sliced;
            img.color = btnColor;
            img.raycastTarget = true;

            Button btn = go.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = btnColor;
            cb.highlightedColor = btnColor * 1.35f;
            cb.pressedColor = btnColor * 0.75f;
            cb.selectedColor = cb.highlightedColor;
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;

            TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>();
            if (font != null) lbl.font = font;
            lbl.text = label;
            lbl.fontSize = 17f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = new Color(1f, 0.94f, 0.82f, 1f);
            lbl.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        private void EnsureStatusRefresh()
        {
            if (_statusLabel == null || WorldSeedService.Instance == null) return;
            string display = !string.IsNullOrEmpty(WorldSeedService.Instance.CurrentSeedString)
                ? $"{WorldSeedService.Instance.CurrentSeedString} ({WorldSeedService.Instance.CurrentSeed})"
                : $"{WorldSeedService.Instance.CurrentSeed}";
            _statusLabel.text = $"Current Seed: <color=#FFD670>{display}</color>";
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
            if (string.IsNullOrEmpty(raw))
            {
                int randomSeed = WorldSeedService.Instance.GenerateRandomSeed();
                WorldSeedService.Instance.SetSeed(randomSeed, userProvided: true);
            }
            else
            {
                WorldSeedService.Instance.SetSeed(raw, userProvided: true);
            }

            if (_statusLabel != null) _statusLabel.color = new Color(0.92f, 0.80f, 0.52f, 1f);
            Hide();
            if (MainMenuUI.Instance != null) MainMenuUI.Instance.Hide();
            MainMenuUI.StartGameSession();
        }
    }
}
