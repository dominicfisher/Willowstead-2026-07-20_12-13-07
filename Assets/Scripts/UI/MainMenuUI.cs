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
        private TMP_Text _continueLabel;
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
            Show();
        }

        public void Show()
        {
            if (_panelGo == null) return;
            // Block gameplay input while the menu is open.
            InputReader.BlockGameplayInput = true;
            RefreshContinue();
            _panelGo.SetActive(true);
        }

        public void Hide()
        {
            if (_panelGo == null) return;
            _panelGo.SetActive(false);
            InputReader.BlockGameplayInput = false;
        }

        /// <summary>True when the Play menu panel is on screen. Pause menu bails on ESC while this is true.</summary>
        public bool IsVisible => _panelGo != null && _panelGo.activeSelf;

        // ─── Panel construction ────────────────────────────────────────

        private void BuildPanel(Canvas canvas)
        {
            _panelGo = new GameObject("MainMenuPanel", typeof(RectTransform), typeof(Image));
            _panelGo.transform.SetParent(canvas.transform, false);
            RectTransform rt = (RectTransform)_panelGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image bg = _panelGo.GetComponent<Image>();
            bg.color = new Color(0.10f, 0.08f, 0.06f, 0.97f);
            bg.raycastTarget = true;

            // Title
            GameObject titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(_panelGo.transform, false);
            RectTransform titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(700f, 90f);
            titleRt.anchoredPosition = new Vector2(0f, -120f);
            var title = BuildText(titleGo, "Willowstead",
                new Color(0.95f, 0.88f, 0.62f, 1f), fontSize: 64, style: FontStyles.Bold);

            GameObject subGo = new GameObject("Subtitle", typeof(RectTransform));
            subGo.transform.SetParent(_panelGo.transform, false);
            RectTransform subRt = (RectTransform)subGo.transform;
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.sizeDelta = new Vector2(600f, 36f);
            subRt.anchoredPosition = new Vector2(0f, -180f);
            var sub = BuildText(subGo, "a little farm in a deterministic world",
                new Color(0.86f, 0.80f, 0.72f, 1f), fontSize: 22, style: FontStyles.Italic);

            // Buttons: New World, Continue, Load Saves, Quit (4 stacked).
            _continueButton = BuildMenuButton(_panelGo.transform, "Continue (Most Recent)",
                new Vector2(0f, -270f), OnContinueClicked);
            var newWorldBtn = BuildMenuButton(_panelGo.transform, "New World",
                new Vector2(0f, -360f), OnNewWorldClicked);
            var loadBtn = BuildMenuButton(_panelGo.transform, "Load Saves",
                new Vector2(0f, -450f), OnLoadSavesClicked);
            var quitBtn = BuildMenuButton(_panelGo.transform, "Quit",
                new Vector2(0f, -540f), OnQuitClicked);
            _continueLabel = _continueButton.GetComponentInChildren<TMP_Text>();

            // Helper-line "Press F5 to save the current world anywhere"
            GameObject hintGo = new GameObject("Hint", typeof(RectTransform));
            hintGo.transform.SetParent(_panelGo.transform, false);
            RectTransform hintRt = (RectTransform)hintGo.transform;
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.sizeDelta = new Vector2(700f, 36f);
            hintRt.anchoredPosition = new Vector2(0f, 36f);
            BuildText(hintGo, "Tip: press F5 anywhere in-game to save current world to slot 1.",
                new Color(0.78f, 0.72f, 0.62f, 0.85f), fontSize: 16, style: FontStyles.Italic);
        }

        private static TMP_Text BuildText(GameObject parent, string text, Color color, float fontSize, FontStyles style)
        {
            var t = parent.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.richText = false;
            return t;
        }

        private static Button BuildMenuButton(Transform parent, string label, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(480f, 60f);
            rt.anchoredPosition = anchoredPos;
            Image img = go.GetComponent<Image>();
            img.color = new Color(0.18f, 0.16f, 0.12f, 1f);
            img.raycastTarget = true;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.20f, 0.18f, 0.14f, 1f);
            cb.highlightedColor = new Color(0.36f, 0.30f, 0.22f, 1f);
            cb.pressedColor = new Color(0.10f, 0.09f, 0.07f, 1f);
            cb.selectedColor = cb.highlightedColor;
            cb.disabledColor = new Color(0.18f, 0.16f, 0.12f, 0.6f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
            BuildText(lblGo, label, new Color(0.96f, 0.92f, 0.78f, 1f), 24f, FontStyles.Bold);
            return btn;
        }

        // ─── Button handlers ───────────────────────────────────────────

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

        private void OnContinueClicked()
        {
            PlayerController.EnsurePlayerInstance();
            if (SaveGameManager.Instance == null) return;
            SaveSlotSummary best = SaveGameManager.Instance.FindMostRecent();
            if (best == null || !best.exists) return;
            SaveGameManager.Instance.LoadFromPath(best.fullPath);
            Hide();
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
