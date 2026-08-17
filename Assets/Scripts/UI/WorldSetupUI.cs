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
        private TMP_InputField _nameInput;
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

            EnsureStatusRefresh();

            if (_seedInput != null && string.IsNullOrEmpty(_seedInput.text))
            {
                int current = WorldSeedService.Instance != null
                    ? WorldSeedService.Instance.CurrentSeed
                    : 1337;
                _seedInput.text = current.ToString();
            }
            if (_nameInput != null && string.IsNullOrEmpty(_nameInput.text))
            {
                _nameInput.text = "My Willowstead";
            }
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
            Font font = UIResourceHelper.GetPixelFont();

            _panelGo = new GameObject("WorldSetupPanel", typeof(RectTransform), typeof(Image));
            _panelGo.transform.SetParent(canvas.transform, false);
            RectTransform rootRt = (RectTransform)_panelGo.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.sizeDelta = Vector2.zero;

            Image rootDim = _panelGo.GetComponent<Image>();
            rootDim.color = new Color(0.04f, 0.04f, 0.05f, 0.85f);
            rootDim.raycastTarget = true;

            GameObject windowGo = new GameObject("WindowCard", typeof(RectTransform), typeof(Image));
            windowGo.transform.SetParent(_panelGo.transform, false);
            RectTransform winRt = (RectTransform)windowGo.transform;
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.pivot = new Vector2(0.5f, 0.5f);
            winRt.sizeDelta = new Vector2(580f, 440f);
            winRt.anchoredPosition = Vector2.zero;

            Image winBg = windowGo.GetComponent<Image>();
            winBg.sprite = UIResourceHelper.GetQuestBookSprite();
            winBg.type = Image.Type.Sliced;
            winBg.color = Color.white;
            winBg.raycastTarget = true;

            // Content container on top of book/card
            GameObject innerGo = new GameObject("InnerContainer", typeof(RectTransform));
            innerGo.transform.SetParent(windowGo.transform, false);
            RectTransform innerRt = (RectTransform)innerGo.transform;
            innerRt.anchorMin = Vector2.zero; innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(36f, 32f); innerRt.offsetMax = new Vector2(-36f, -32f);

            // Title Banner
            GameObject bannerGo = new GameObject("TitleBanner", typeof(RectTransform));
            bannerGo.transform.SetParent(innerGo.transform, false);
            RectTransform bannerRt = (RectTransform)bannerGo.transform;
            bannerRt.anchorMin = new Vector2(0.5f, 1f);
            bannerRt.anchorMax = new Vector2(0.5f, 1f);
            bannerRt.pivot = new Vector2(0.5f, 1f);
            bannerRt.sizeDelta = new Vector2(400f, 34f);
            bannerRt.anchoredPosition = new Vector2(0f, -4f);

            GameObject titleGo = new GameObject("TitleText", typeof(RectTransform));
            titleGo.transform.SetParent(bannerGo.transform, false);
            RectTransform titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = Vector2.zero; titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = Vector2.zero; titleRt.offsetMax = Vector2.zero;

            Text title = titleGo.AddComponent<Text>();
            title.font = font;
            title.text = "CREATE NEW REALM";
            title.fontSize = 20;
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.35f, 0.22f, 0.16f, 1f);
            title.alignment = TextAnchor.MiddleCenter;
            title.raycastTarget = false;

            // Close 'X' Button
            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
            closeGo.transform.SetParent(innerGo.transform, false);
            RectTransform closeRt = (RectTransform)closeGo.transform;
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(4f, 4f);
            closeRt.sizeDelta = new Vector2(30f, 30f);

            Image closeImg = closeGo.GetComponent<Image>();
            closeImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            closeImg.type = Image.Type.Sliced;
            closeImg.color = new Color(0.85f, 0.40f, 0.35f, 1f);

            _closeButton = closeGo.GetComponent<Button>();
            _closeButton.targetGraphic = closeImg;
            _closeButton.onClick.AddListener(OnCloseClicked);

            GameObject closeTxtGo = new GameObject("X", typeof(RectTransform));
            closeTxtGo.transform.SetParent(closeGo.transform, false);
            RectTransform closeTxtRt = (RectTransform)closeTxtGo.transform;
            closeTxtRt.anchorMin = Vector2.zero; closeTxtRt.anchorMax = Vector2.one;
            closeTxtRt.offsetMin = Vector2.zero; closeTxtRt.offsetMax = Vector2.zero;
            var closeTxt = closeTxtGo.AddComponent<Text>();
            closeTxt.font = font;
            closeTxt.fontSize = 15;
            closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.color = Color.white;
            closeTxt.text = "✕";
            closeTxt.raycastTarget = false;

            // Subtitle
            GameObject subGo = new GameObject("Subtitle", typeof(RectTransform));
            subGo.transform.SetParent(innerGo.transform, false);
            RectTransform subRt = (RectTransform)subGo.transform;
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -44f);
            subRt.sizeDelta = new Vector2(460f, 32f);

            Text sub = subGo.AddComponent<Text>();
            sub.font = font;
            sub.text = "Name your world & choose a seed to shape your homestead.";
            sub.fontSize = 12;
            sub.fontStyle = FontStyle.Normal;
            sub.color = new Color(0.48f, 0.38f, 0.30f, 1f);
            sub.alignment = TextAnchor.MiddleCenter;
            sub.raycastTarget = false;

            // Row 1: World Name Input
            _nameInput = BuildInputRow(innerGo.transform, "World Name:", "My Willowstead", -90f, font);

            // Row 2: World Seed Input
            _seedInput = BuildInputRow(innerGo.transform, "World Seed:", "1337", -156f, font);

            // Status Text
            GameObject stGo = new GameObject("Status", typeof(RectTransform));
            stGo.transform.SetParent(innerGo.transform, false);
            RectTransform stRt = (RectTransform)stGo.transform;
            stRt.anchorMin = new Vector2(0.5f, 0f);
            stRt.anchorMax = new Vector2(0.5f, 0f);
            stRt.pivot = new Vector2(0.5f, 0f);
            stRt.anchoredPosition = new Vector2(0f, 76f);
            stRt.sizeDelta = new Vector2(460f, 22f);

            _statusLabel = stGo.AddComponent<TextMeshProUGUI>();
            _statusLabel.text = string.Empty;
            _statusLabel.fontSize = 12f;
            _statusLabel.color = new Color(0.45f, 0.35f, 0.25f, 1f);
            _statusLabel.alignment = TextAlignmentOptions.Center;

            // Buttons
            _randomButton = BuildButton(innerGo.transform, "🎲 Randomize",
                new Vector2(-120f, 16f), new Vector2(190f, 44f), font,
                new Color(0.96f, 0.90f, 0.82f, 1f), new Color(0.35f, 0.25f, 0.18f, 1f), OnRandomizeClicked);

            _createButton = BuildButton(innerGo.transform, "✦ Embark World",
                new Vector2(120f, 16f), new Vector2(190f, 44f), font,
                new Color(0.45f, 0.68f, 0.38f, 1f), Color.white, OnCreateClicked);

            _panelGo.SetActive(false);
        }

        private TMP_InputField BuildInputRow(Transform parent, string labelText, string placeholderText, float yPos, Font font)
        {
            GameObject rowGo = new GameObject($"Row_{labelText}", typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = new Vector2(0.5f, 1f);
            rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, yPos);
            rowRt.sizeDelta = new Vector2(460f, 48f);

            Image rowBg = rowGo.GetComponent<Image>();
            rowBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            rowBg.type = Image.Type.Sliced;
            rowBg.color = new Color(0.96f, 0.90f, 0.82f, 0.95f);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(rowGo.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = new Vector2(0f, 0f);
            lblRt.anchorMax = new Vector2(0f, 1f);
            lblRt.pivot = new Vector2(0f, 0.5f);
            lblRt.offsetMin = new Vector2(14f, 0f);
            lblRt.offsetMax = new Vector2(130f, 0f);

            Text lbl = lblGo.AddComponent<Text>();
            lbl.font = font;
            lbl.text = labelText;
            lbl.fontSize = 13;
            lbl.fontStyle = FontStyle.Bold;
            lbl.color = new Color(0.35f, 0.22f, 0.16f, 1f);
            lbl.alignment = TextAnchor.MiddleLeft;
            lbl.raycastTarget = false;

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(rowGo.transform, false);
            RectTransform textAreaRt = (RectTransform)textArea.transform;
            textAreaRt.anchorMin = new Vector2(0f, 0f);
            textAreaRt.anchorMax = new Vector2(1f, 1f);
            textAreaRt.pivot = new Vector2(0.5f, 0.5f);
            textAreaRt.offsetMin = new Vector2(134f, 4f);
            textAreaRt.offsetMax = new Vector2(-12f, -4f);

            GameObject inputTextGo = new GameObject("Text", typeof(RectTransform));
            inputTextGo.transform.SetParent(textArea.transform, false);
            RectTransform itRt = (RectTransform)inputTextGo.transform;
            itRt.anchorMin = Vector2.zero; itRt.anchorMax = Vector2.one;
            itRt.offsetMin = Vector2.zero; itRt.offsetMax = Vector2.zero;

            TextMeshProUGUI itText = inputTextGo.AddComponent<TextMeshProUGUI>();
            itText.fontSize = 14f;
            itText.fontStyle = FontStyles.Bold;
            itText.color = new Color(0.25f, 0.16f, 0.10f, 1f);
            itText.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(textArea.transform, false);
            RectTransform phRt = (RectTransform)phGo.transform;
            phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero; phRt.offsetMax = Vector2.zero;

            TextMeshProUGUI phText = phGo.AddComponent<TextMeshProUGUI>();
            phText.text = placeholderText;
            phText.fontSize = 13f;
            phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(0.60f, 0.50f, 0.42f, 0.7f);
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

        private Button BuildButton(Transform parent, string label, Vector2 centerPos, Vector2 size, Font font, Color btnColor, Color textColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = centerPos;
            rt.sizeDelta = size;

            Image img = go.GetComponent<Image>();
            img.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            img.type = Image.Type.Sliced;
            img.color = btnColor;
            img.raycastTarget = true;

            Button btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = btnColor;
            cb.highlightedColor = Color.Lerp(btnColor, Color.white, 0.25f);
            cb.pressedColor = Color.Lerp(btnColor, Color.black, 0.20f);
            cb.selectedColor = cb.highlightedColor;
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;

            Text lbl = lblGo.AddComponent<Text>();
            lbl.font = font;
            lbl.text = label;
            lbl.fontSize = 14;
            lbl.fontStyle = FontStyle.Bold;
            lbl.color = textColor;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.raycastTarget = false;

            return btn;
        }

        private void OnCloseClicked()
        {
            Hide();
            if (!MainMenuUI.HasGameStarted && MainMenuUI.Instance != null)
            {
                MainMenuUI.Instance.Show();
            }
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

            string worldName = _nameInput != null && !string.IsNullOrWhiteSpace(_nameInput.text)
                ? _nameInput.text.Trim()
                : "My Willowstead";

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

            if (Persistence.SaveGameManager.Instance != null)
            {
                Persistence.SaveGameManager.Instance.SetActiveSaveName(worldName);
                Persistence.SaveGameManager.Instance.SaveToSlot(1, worldName);
            }

            if (_statusLabel != null) _statusLabel.color = new Color(0.92f, 0.80f, 0.52f, 1f);
            Hide();
            if (MainMenuUI.Instance != null) MainMenuUI.Instance.Hide();
            MainMenuUI.StartGameSession();
        }
    }
}
