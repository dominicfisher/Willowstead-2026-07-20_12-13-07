using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Willowstead.Input;
using Willowstead.Persistence;
using Willowstead.Player;

namespace Willowstead.UI
{
    /// <summary>
    /// "Play" entry screen — a temporary UI that shows on launch and
    /// covers the world until the player picks New / Continue / Load.
    /// Self-bootstraps at BeforeSceneLoad so the menu is ready the
    /// instant the first scene becomes active.
    ///
    /// Self-deactivates once the player commits to a session; the menu
    /// is re-shown via <see cref="Show"/> from any future pause hook.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }

        private GameObject _panelGo;
        private Text _continueLabel;
        private Button _continueButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[MainMenuUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<MainMenuUI>();
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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Only auto-show when there's no save in progress already.
            if (SaveGameManager.IsLoadingFromSave) return;

            if (!CharacterCreationUI.HasCharacterCreated())
            {
                if (CharacterCreationUI.Instance != null) CharacterCreationUI.Instance.Show();
            }
            else
            {
                Show();
            }
        }

        public static bool HasGameStarted { get; private set; } = false;
        public static event System.Action OnGameStarted;

        private Coroutine _fadeCoroutine;

        public void Show()
        {
            if (_panelGo == null) return;
            // Block gameplay input while the menu is open.
            InputReader.BlockGameplayInput = true;
            RefreshContinue();
            _panelGo.SetActive(true);
            SetGameplayVisualsVisible(false);
        }

        public void Hide()
        {
            if (_panelGo == null) return;
            _panelGo.SetActive(false);
            InputReader.BlockGameplayInput = false;
        }

        private void Update()
        {
            if (!HasGameStarted)
            {
                SetGameplayVisualsVisible(false);
            }
        }

        /// <summary>
        /// Toggles player sprite rendering and HUD visibility while in the main menu.
        /// </summary>
        public static void SetGameplayVisualsVisible(bool visible)
        {
            if (PlayerController.Instance != null)
            {
                var sr = PlayerController.Instance.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = visible;
            }

            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            if (canvas != null)
            {
                Transform hotbar = canvas.transform.Find("HotbarPanel");
                if (hotbar != null)
                {
                    var cg = hotbar.GetComponent<CanvasGroup>() ?? hotbar.gameObject.AddComponent<CanvasGroup>();
                    if (!visible && (Instance == null || Instance._fadeCoroutine == null)) cg.alpha = 0f;
                }

                Transform compass = canvas.transform.Find("CompassPanel");
                if (compass != null)
                {
                    var cg = compass.GetComponent<CanvasGroup>() ?? compass.gameObject.AddComponent<CanvasGroup>();
                    if (!visible && (Instance == null || Instance._fadeCoroutine == null)) cg.alpha = 0f;
                }

                Transform gold = canvas.transform.Find("GoldHUD");
                if (gold != null)
                {
                    var cg = gold.GetComponent<CanvasGroup>() ?? gold.gameObject.AddComponent<CanvasGroup>();
                    if (!visible && (Instance == null || Instance._fadeCoroutine == null)) cg.alpha = 0f;
                }
            }
        }

        /// <summary>
        /// Commits to starting the game session, unhides gameplay HUDs, and plays a smooth fade-in animation.
        /// </summary>
        public static void StartGameSession()
        {
            if (HasGameStarted) return;
            HasGameStarted = true;
            OnGameStarted?.Invoke();

            if (PlayerController.Instance != null)
            {
                var sr = PlayerController.Instance.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = true;
            }

            if (Instance != null)
            {
                if (Instance._fadeCoroutine != null) Instance.StopCoroutine(Instance._fadeCoroutine);
                Instance._fadeCoroutine = Instance.StartCoroutine(Instance.PlayHudFadeInAnimation());
            }
        }

        private System.Collections.IEnumerator PlayHudFadeInAnimation()
        {
            // Collect all gameplay HUD elements (Hotbar, Compass, GoldHUD)
            var groups = new System.Collections.Generic.List<CanvasGroup>();

            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            if (canvas != null)
            {
                Transform hotbar = canvas.transform.Find("HotbarPanel");
                if (hotbar != null)
                {
                    var cg = hotbar.GetComponent<CanvasGroup>() ?? hotbar.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                    groups.Add(cg);
                }

                Transform compass = canvas.transform.Find("CompassPanel");
                if (compass != null)
                {
                    var cg = compass.GetComponent<CanvasGroup>() ?? compass.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                    groups.Add(cg);
                }

                Transform gold = canvas.transform.Find("GoldHUD");
                if (gold != null)
                {
                    var cg = gold.GetComponent<CanvasGroup>() ?? gold.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                    groups.Add(cg);
                }
            }

            float duration = 0.85f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                float curvedAlpha = 1f - (1f - alpha) * (1f - alpha);

                for (int i = 0; i < groups.Count; i++)
                {
                    if (groups[i] != null) groups[i].alpha = curvedAlpha;
                }
                yield return null;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null) groups[i].alpha = 1f;
            }
            _fadeCoroutine = null;
        }

        /// <summary>True when the Play menu panel is on screen. Pause menu bails on ESC while this is true.</summary>
        public bool IsVisible => _panelGo != null && _panelGo.activeSelf;

        private void BuildPanel(Canvas canvas)
        {
            // Full-screen canvas overlay container (no solid background image blocking the view)
            _panelGo = new GameObject("MainMenuPanel", typeof(RectTransform));
            _panelGo.transform.SetParent(canvas.transform, false);
            RectTransform rt = (RectTransform)_panelGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            GameObject cardGo = new GameObject("MenuCard", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(_panelGo.transform, false);
            RectTransform cardRt = (RectTransform)cardGo.transform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(540f, 690f);
            cardRt.anchoredPosition = Vector2.zero;
            Image cardBg = cardGo.GetComponent<Image>();
            cardBg.sprite = UIResourceHelper.GetBackgroundSprite();
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.22f, 0.16f, 0.11f, 0.98f);
            cardBg.raycastTarget = true;

            GameObject innerGo = new GameObject("InnerBoard", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(cardGo.transform, false);
            RectTransform innerRt = (RectTransform)innerGo.transform;
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(10f, 10f);
            innerRt.offsetMax = new Vector2(-10f, -10f);
            Image innerBg = innerGo.GetComponent<Image>();
            innerBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            innerBg.type = Image.Type.Sliced;
            innerBg.color = new Color(0.12f, 0.09f, 0.06f, 0.95f);

            GameObject bannerGo = new GameObject("TitleBanner", typeof(RectTransform), typeof(Image));
            bannerGo.transform.SetParent(cardGo.transform, false);
            RectTransform bannerRt = (RectTransform)bannerGo.transform;
            bannerRt.anchorMin = new Vector2(0.5f, 1f);
            bannerRt.anchorMax = new Vector2(0.5f, 1f);
            bannerRt.pivot = new Vector2(0.5f, 1f);
            bannerRt.sizeDelta = new Vector2(400f, 60f);
            bannerRt.anchoredPosition = new Vector2(0f, -25f);
            Image bannerBg = bannerGo.GetComponent<Image>();
            bannerBg.sprite = UIResourceHelper.GetBackgroundSprite();
            bannerBg.type = Image.Type.Sliced;
            bannerBg.color = new Color(0.35f, 0.24f, 0.15f, 1f);

            GameObject titleTextGo = new GameObject("TitleText", typeof(RectTransform));
            titleTextGo.transform.SetParent(bannerGo.transform, false);
            RectTransform titleTextRt = (RectTransform)titleTextGo.transform;
            titleTextRt.anchorMin = Vector2.zero; titleTextRt.anchorMax = Vector2.one;
            titleTextRt.offsetMin = Vector2.zero; titleTextRt.offsetMax = Vector2.zero;
            BuildText(titleTextGo, "WILLOWSTEAD",
                new Color(1f, 0.88f, 0.45f, 1f), fontSize: 32, style: FontStyles.Bold);

            GameObject subGo = new GameObject("Subtitle", typeof(RectTransform));
            subGo.transform.SetParent(cardGo.transform, false);
            RectTransform subRt = (RectTransform)subGo.transform;
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.sizeDelta = new Vector2(480f, 30f);
            subRt.anchoredPosition = new Vector2(0f, -95f);
            BuildText(subGo, "a cozy farm in a deterministic world",
                new Color(0.92f, 0.86f, 0.74f, 1f), fontSize: 16, style: FontStyles.Italic);

            _continueButton = BuildMenuButton(cardGo.transform, "Continue (Most Recent)",
                new Vector2(0f, -140f), OnContinueClicked);
            BuildMenuButton(cardGo.transform, "New World",
                new Vector2(0f, -210f), OnNewWorldClicked);
            BuildMenuButton(cardGo.transform, "Host Co-op",
                new Vector2(0f, -280f), OnHostMultiplayerClicked);
            BuildMenuButton(cardGo.transform, "Join Co-op with Code",
                new Vector2(0f, -350f), OnJoinMultiplayerClicked);
            BuildMenuButton(cardGo.transform, "Load Saves",
                new Vector2(0f, -420f), OnLoadSavesClicked);
            BuildMenuButton(cardGo.transform, "Character & Profile",
                new Vector2(0f, -490f), OnCharacterProfileClicked);
            BuildMenuButton(cardGo.transform, "Quit",
                new Vector2(0f, -560f), OnQuitClicked);
            _continueLabel = _continueButton.GetComponentInChildren<Text>();

            GameObject hintGo = new GameObject("Hint", typeof(RectTransform));
            hintGo.transform.SetParent(cardGo.transform, false);
            RectTransform hintRt = (RectTransform)hintGo.transform;
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.sizeDelta = new Vector2(500f, 30f);
            hintRt.anchoredPosition = new Vector2(0f, 18f);
            BuildText(hintGo, "Tip: press Enter in-game to open chat with friends.",
                new Color(0.78f, 0.72f, 0.62f, 0.85f), fontSize: 13, style: FontStyles.Italic);
        }

        private void OnCharacterProfileClicked()
        {
            if (CharacterCreationUI.Instance != null)
                CharacterCreationUI.Instance.Show();
            Hide();
        }

        private static void BuildText(GameObject parent, string text, Color color, float fontSize, FontStyles style)
        {
            var t = parent.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.richText = false;

            if (t.font == null || t.font.material == null)
            {
                var defaultFont = TMP_Settings.defaultFontAsset;
                if (defaultFont != null) t.font = defaultFont;
            }
        }

        private static Button BuildMenuButton(Transform parent, string label, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(380f, 52f);
            rt.anchoredPosition = anchoredPos;
            Image img = go.GetComponent<Image>();
            img.sprite = UIResourceHelper.GetBackgroundSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.38f, 0.26f, 0.16f, 1f); // Warm wood button
            img.raycastTarget = true;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.38f, 0.26f, 0.16f, 1f);
            cb.highlightedColor = new Color(0.54f, 0.38f, 0.24f, 1f);
            cb.pressedColor = new Color(0.24f, 0.16f, 0.10f, 1f);
            cb.selectedColor = cb.highlightedColor;
            cb.disabledColor = new Color(0.20f, 0.18f, 0.14f, 0.6f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;

            Text legacyTxt = lblGo.AddComponent<Text>();
            legacyTxt.text = label;
            legacyTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            legacyTxt.fontSize = 18;
            legacyTxt.fontStyle = UnityEngine.FontStyle.Bold;
            legacyTxt.color = new Color(1f, 0.94f, 0.82f, 1f);
            legacyTxt.alignment = TextAnchor.MiddleCenter;
            legacyTxt.raycastTarget = false;

            return btn;
        }

        private void RefreshContinue()
        {
            if (_continueButton == null) return;
            SaveSlotSummary best = null;
            if (SaveGameManager.Instance != null) best = SaveGameManager.Instance.FindMostRecent();
            bool enabled = best != null && best.exists;
            _continueButton.interactable = enabled;
            if (_continueLabel != null)
            {
                if (enabled)
                    _continueLabel.text = $"Continue ({(string.IsNullOrEmpty(best.saveName) ? "Untitled" : best.saveName)})";
                else
                    _continueLabel.text = "Continue (no saves yet)";
            }
        }

        private void OnNewWorldClicked()
        {
            PlayerController.EnsurePlayerInstance();
            if (WorldSetupUI.Instance != null)
                WorldSetupUI.Instance.Show();
            Hide();
        }

        private void OnHostMultiplayerClicked()
        {
            PlayerController.EnsurePlayerInstance();
            if (SaveSlotsUI.Instance != null)
                SaveSlotsUI.Instance.ShowHostCoopMode();
            Hide();
        }

        private void OnJoinMultiplayerClicked()
        {
            PlayerController.EnsurePlayerInstance();
            if (MultiplayerLobbyUI.Instance != null)
                MultiplayerLobbyUI.Instance.ShowJoinLobby();
            Hide();
        }

        private void OnContinueClicked()
        {
            PlayerController.EnsurePlayerInstance();
            if (SaveGameManager.Instance == null) return;
            SaveSlotSummary best = SaveGameManager.Instance.FindMostRecent();
            if (best == null || !best.exists) return;
            SaveGameManager.Instance.LoadFromPath(best.fullPath);
            Hide();
            StartGameSession();
        }

        private void OnLoadSavesClicked()
        {
            if (SaveSlotsUI.Instance != null)
                SaveSlotsUI.Instance.Show();
            Hide();
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
