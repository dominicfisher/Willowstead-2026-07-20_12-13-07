using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Willowstead.Input;
using Willowstead.Player;

namespace Willowstead.UI
{
    /// <summary>
    /// Character Creation modal shown before loading into the Main Menu.
    /// Lets the player set their display username and customize their character's appearance (skin tone, hair/shirt tint).
    /// </summary>
    public class CharacterCreationUI : MonoBehaviour
    {
        public static CharacterCreationUI Instance { get; private set; }

        public const string PrefUsername = "player_username";
        public const string PrefSkinTone = "player_skin_tone";
        public const string PrefShirtColor = "player_shirt_color";
        public const string PrefCreated = "player_character_created";

        private GameObject _panelGo;
        private TMP_InputField _usernameInput;
        private Image _previewAvatar;
        private int _selectedSkinIndex = 0;
        private int _selectedShirtIndex = 0;

        private readonly Color[] _skinTones = new Color[]
        {
            new Color(1.0f, 0.92f, 0.84f, 1f), // Fair / Pale
            new Color(0.95f, 0.82f, 0.68f, 1f), // Natural Peach
            new Color(0.85f, 0.68f, 0.52f, 1f), // Olive / Warm Tan
            new Color(0.68f, 0.48f, 0.35f, 1f), // Bronze / Caramel
            new Color(0.48f, 0.32f, 0.22f, 1f), // Rich Deep Brown
            new Color(0.32f, 0.20f, 0.14f, 1f)  // Espresso
        };

        private readonly string[] _skinToneNames = new string[]
        {
            "Fair", "Peach", "Tan", "Bronze", "Deep", "Espresso"
        };

        private readonly Color[] _shirtTints = new Color[]
        {
            Color.white,                          // Classic Green Tunic
            new Color(0.70f, 0.85f, 1.00f, 1f),   // Sky Blue
            new Color(1.00f, 0.75f, 0.75f, 1f),   // Crimson / Rose
            new Color(1.00f, 0.90f, 0.65f, 1f),   // Harvest Gold
            new Color(0.85f, 0.70f, 1.00f, 1f),   // Royal Violet
            new Color(0.80f, 0.80f, 0.80f, 1f)    // Shadow Charcoal
        };

        private readonly string[] _shirtNames = new string[]
        {
            "Forest Green", "Sky Blue", "Rosewood", "Harvest Gold", "Royal Violet", "Shadow"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[CharacterCreationUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<CharacterCreationUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas", "UIRoot");
            UIResourceHelper.EnsureEventSystem();
            BuildPanel(canvas);
            _panelGo.SetActive(false);
        }

        private void Start()
        {
            // If the player hasn't created a character yet, prompt them on first launch!
            if (!HasCharacterCreated())
            {
                Show();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static bool HasCharacterCreated()
        {
            return PlayerPrefs.GetInt(PrefCreated, 0) == 1 && !string.IsNullOrWhiteSpace(GetSavedUsername());
        }

        public static string GetSavedUsername()
        {
            return PlayerPrefs.GetString(PrefUsername, "Farmer");
        }

        public static Color GetSavedSkinTone()
        {
            int idx = PlayerPrefs.GetInt(PrefSkinTone, 0);
            return new Color(1.0f, 0.92f, 0.84f, 1f); // default fair
        }

        public static Color GetSavedShirtTint()
        {
            int idx = PlayerPrefs.GetInt(PrefShirtColor, 0);
            return Color.white;
        }

        public void Show()
        {
            if (_panelGo == null) return;
            _panelGo.SetActive(true);
            InputReader.BlockGameplayInput = true;

            if (_usernameInput != null)
            {
                _usernameInput.text = PlayerPrefs.GetString(PrefUsername, "Farmer");
            }
            UpdatePreview();
        }

        public void Hide()
        {
            if (_panelGo == null) return;
            _panelGo.SetActive(false);
            InputReader.BlockGameplayInput = false;
        }

        /// <summary>True when the Character Creation panel is visible on screen.</summary>
        public bool IsVisible => _panelGo != null && _panelGo.activeSelf;

        private void BuildPanel(Canvas canvas)
        {
            _panelGo = new GameObject("CharacterCreationPanel", typeof(RectTransform), typeof(Image));
            _panelGo.transform.SetParent(canvas.transform, false);
            RectTransform rt = (RectTransform)_panelGo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            Image bg = _panelGo.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.05f, 0.92f);
            bg.raycastTarget = true;

            // Card
            GameObject cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(_panelGo.transform, false);
            RectTransform cardRt = (RectTransform)cardGo.transform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(580f, 540f);
            Image cardBg = cardGo.GetComponent<Image>();
            cardBg.sprite = UIResourceHelper.GetBackgroundSprite();
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.22f, 0.16f, 0.11f, 0.98f);

            // Banner
            GameObject bannerGo = new GameObject("Banner", typeof(RectTransform), typeof(Image));
            bannerGo.transform.SetParent(cardGo.transform, false);
            RectTransform bannerRt = (RectTransform)bannerGo.transform;
            bannerRt.anchorMin = new Vector2(0.5f, 1f); bannerRt.anchorMax = new Vector2(0.5f, 1f);
            bannerRt.pivot = new Vector2(0.5f, 1f);
            bannerRt.sizeDelta = new Vector2(420f, 54f);
            bannerRt.anchoredPosition = new Vector2(0f, -20f);
            Image bannerBg = bannerGo.GetComponent<Image>();
            bannerBg.sprite = UIResourceHelper.GetBackgroundSprite();
            bannerBg.type = Image.Type.Sliced;
            bannerBg.color = new Color(0.35f, 0.24f, 0.15f, 1f);

            GameObject titleTextGo = new GameObject("TitleText", typeof(RectTransform));
            titleTextGo.transform.SetParent(bannerGo.transform, false);
            RectTransform titleRt = (RectTransform)titleTextGo.transform;
            titleRt.anchorMin = Vector2.zero; titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = Vector2.zero; titleRt.offsetMax = Vector2.zero;
            var t = titleTextGo.AddComponent<TextMeshProUGUI>();
            t.text = "CHARACTER CREATOR";
            t.fontSize = 22f;
            t.fontStyle = FontStyles.Bold;
            t.color = new Color(1f, 0.88f, 0.45f, 1f);
            t.alignment = TextAlignmentOptions.Center;

            // Character Preview Frame
            GameObject previewFrame = new GameObject("PreviewFrame", typeof(RectTransform), typeof(Image));
            previewFrame.transform.SetParent(cardGo.transform, false);
            RectTransform pfRt = (RectTransform)previewFrame.transform;
            pfRt.anchorMin = new Vector2(0.5f, 1f); pfRt.anchorMax = new Vector2(0.5f, 1f);
            pfRt.pivot = new Vector2(0.5f, 1f);
            pfRt.sizeDelta = new Vector2(110f, 130f);
            pfRt.anchoredPosition = new Vector2(0f, -85f);
            Image pfBg = previewFrame.GetComponent<Image>();
            pfBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            pfBg.type = Image.Type.Sliced;
            pfBg.color = new Color(0.10f, 0.08f, 0.06f, 0.95f);

            GameObject avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            avatarGo.transform.SetParent(previewFrame.transform, false);
            RectTransform avRt = (RectTransform)avatarGo.transform;
            avRt.anchorMin = Vector2.zero; avRt.anchorMax = Vector2.one;
            avRt.offsetMin = new Vector2(8f, 8f); avRt.offsetMax = new Vector2(-8f, -8f);
            _previewAvatar = avatarGo.GetComponent<Image>();
#if UNITY_EDITOR
            _previewAvatar.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/PlayerFrames/Idle/South/frame_0.png");
#endif
            _previewAvatar.preserveAspect = true;

            // Username Input Row
            _usernameInput = BuildInputRow(cardGo.transform, "Player Name:", "Farmer", -235f);

            // Skin Tone Selector Row
            BuildSelectorRow(cardGo.transform, "Skin Tone:", () => _skinToneNames[_selectedSkinIndex], -300f,
                () => { _selectedSkinIndex = (_selectedSkinIndex - 1 + _skinTones.Length) % _skinTones.Length; UpdatePreview(); },
                () => { _selectedSkinIndex = (_selectedSkinIndex + 1) % _skinTones.Length; UpdatePreview(); });

            // Tunic Color Selector Row
            BuildSelectorRow(cardGo.transform, "Tunic Style:", () => _shirtNames[_selectedShirtIndex], -365f,
                () => { _selectedShirtIndex = (_selectedShirtIndex - 1 + _shirtTints.Length) % _shirtTints.Length; UpdatePreview(); },
                () => { _selectedShirtIndex = (_selectedShirtIndex + 1) % _shirtTints.Length; UpdatePreview(); });

            // Embark / Save Character Button
            BuildButton(cardGo.transform, "Confirm & Continue ✦", new Vector2(0f, -445f), new Vector2(300f, 50f), new Color(0.24f, 0.42f, 0.22f, 1f), OnConfirmClicked);
        }

        private void UpdatePreview()
        {
            if (_previewAvatar != null)
            {
                _previewAvatar.color = _shirtTints[_selectedShirtIndex];
            }
        }

        private void OnConfirmClicked()
        {
            string name = _usernameInput != null && !string.IsNullOrWhiteSpace(_usernameInput.text)
                ? _usernameInput.text.Trim()
                : "Farmer";

            PlayerPrefs.SetString(PrefUsername, name);
            PlayerPrefs.SetInt(PrefSkinTone, _selectedSkinIndex);
            PlayerPrefs.SetInt(PrefShirtColor, _selectedShirtIndex);
            PlayerPrefs.SetInt(PrefCreated, 1);
            PlayerPrefs.Save();

            // Apply to local scene Player Nameplate & Color
            PlayerNameplate.UpdateLocalPlayerAppearance();

            Hide();
            if (MainMenuUI.Instance != null)
            {
                MainMenuUI.Instance.Show();
            }
        }

        private TMP_InputField BuildInputRow(Transform parent, string labelText, string placeholderText, float yPos)
        {
            GameObject rowGo = new GameObject($"Row_{labelText}", typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = new Vector2(0.5f, 1f); rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, yPos);
            rowRt.sizeDelta = new Vector2(460f, 46f);

            Image rowBg = rowGo.GetComponent<Image>();
            rowBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            rowBg.type = Image.Type.Sliced;
            rowBg.color = new Color(0.06f, 0.05f, 0.05f, 0.98f);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(rowGo.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = new Vector2(0f, 0f); lblRt.anchorMax = new Vector2(0f, 1f);
            lblRt.pivot = new Vector2(0f, 0.5f);
            lblRt.offsetMin = new Vector2(14f, 0f); lblRt.offsetMax = new Vector2(130f, 0f);
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = labelText;
            lbl.fontSize = 15f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = new Color(1f, 0.88f, 0.52f, 1f);
            lbl.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(rowGo.transform, false);
            RectTransform textAreaRt = (RectTransform)textArea.transform;
            textAreaRt.anchorMin = new Vector2(0f, 0f); textAreaRt.anchorMax = new Vector2(1f, 1f);
            textAreaRt.pivot = new Vector2(0.5f, 0.5f);
            textAreaRt.offsetMin = new Vector2(135f, 4f); textAreaRt.offsetMax = new Vector2(-12f, -4f);

            GameObject inputTextGo = new GameObject("Text", typeof(RectTransform));
            inputTextGo.transform.SetParent(textArea.transform, false);
            RectTransform itRt = (RectTransform)inputTextGo.transform;
            itRt.anchorMin = Vector2.zero; itRt.anchorMax = Vector2.one;
            itRt.offsetMin = Vector2.zero; itRt.offsetMax = Vector2.zero;
            var itText = inputTextGo.AddComponent<TextMeshProUGUI>();
            itText.fontSize = 17f;
            itText.fontStyle = FontStyles.Bold;
            itText.color = new Color(0.96f, 0.94f, 0.88f, 1f);
            itText.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(textArea.transform, false);
            RectTransform phRt = (RectTransform)phGo.transform;
            phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero; phRt.offsetMax = Vector2.zero;
            var phText = phGo.AddComponent<TextMeshProUGUI>();
            phText.text = placeholderText;
            phText.fontSize = 16f;
            phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(0.6f, 0.58f, 0.52f, 0.5f);
            phText.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_InputField field = rowGo.AddComponent<TMP_InputField>();
            field.targetGraphic = rowBg;
            field.textViewport = textAreaRt;
            field.textComponent = itText;
            field.placeholder = phText;
            field.characterLimit = 16;
            return field;
        }

        private void BuildSelectorRow(Transform parent, string labelText, Func<string> getValueText, float yPos, Action onPrev, Action onNext)
        {
            GameObject rowGo = new GameObject($"Row_{labelText}", typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = new Vector2(0.5f, 1f); rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, yPos);
            rowRt.sizeDelta = new Vector2(460f, 46f);

            Image rowBg = rowGo.GetComponent<Image>();
            rowBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            rowBg.type = Image.Type.Sliced;
            rowBg.color = new Color(0.06f, 0.05f, 0.05f, 0.98f);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(rowGo.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = new Vector2(0f, 0f); lblRt.anchorMax = new Vector2(0f, 1f);
            lblRt.pivot = new Vector2(0f, 0.5f);
            lblRt.offsetMin = new Vector2(14f, 0f); lblRt.offsetMax = new Vector2(130f, 0f);
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = labelText;
            lbl.fontSize = 15f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = new Color(1f, 0.88f, 0.52f, 1f);
            lbl.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject valGo = new GameObject("Value", typeof(RectTransform));
            valGo.transform.SetParent(rowGo.transform, false);
            RectTransform valRt = (RectTransform)valGo.transform;
            valRt.anchorMin = Vector2.zero; valRt.anchorMax = Vector2.one;
            valRt.offsetMin = new Vector2(180f, 0f); valRt.offsetMax = new Vector2(-60f, 0f);
            var valTxt = valGo.AddComponent<TextMeshProUGUI>();
            valTxt.text = getValueText();
            valTxt.fontSize = 16f;
            valTxt.fontStyle = FontStyles.Bold;
            valTxt.color = new Color(0.96f, 0.94f, 0.88f, 1f);
            valTxt.alignment = TextAlignmentOptions.Center;

            BuildMiniButton(rowGo.transform, "◀", new Vector2(145f, 0f), () => { onPrev(); valTxt.text = getValueText(); });
            BuildMiniButton(rowGo.transform, "▶", new Vector2(420f, 0f), () => { onNext(); valTxt.text = getValueText(); });
        }

        private void BuildMiniButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(34f, 34f);

            Image img = go.GetComponent<Image>();
            img.sprite = UIResourceHelper.GetBackgroundSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.38f, 0.26f, 0.16f, 1f);

            Button btn = go.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.38f, 0.26f, 0.16f, 1f);
            cb.highlightedColor = new Color(0.54f, 0.38f, 0.24f, 1f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject tGo = new GameObject("Text", typeof(RectTransform));
            tGo.transform.SetParent(go.transform, false);
            RectTransform tr = (RectTransform)tGo.transform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            var t = tGo.AddComponent<TextMeshProUGUI>();
            t.text = label;
            t.fontSize = 16f;
            t.fontStyle = FontStyles.Bold;
            t.color = new Color(1f, 0.94f, 0.82f, 1f);
            t.alignment = TextAlignmentOptions.Center;
        }

        private void BuildButton(Transform parent, string label, Vector2 centerPos, Vector2 size, Color btnColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = centerPos;
            rt.sizeDelta = size;

            Image img = go.GetComponent<Image>();
            img.sprite = UIResourceHelper.GetBackgroundSprite();
            img.type = Image.Type.Sliced;
            img.color = btnColor;

            Button btn = go.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = btnColor;
            cb.highlightedColor = btnColor * 1.35f;
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = label;
            lbl.fontSize = 18f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = new Color(1f, 0.94f, 0.82f, 1f);
            lbl.alignment = TextAlignmentOptions.Center;
        }
    }
}
