using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Willowstead.Input;
using Willowstead.Persistence;
using Willowstead.Player;

namespace Willowstead.UI
{
    /// <summary>
    /// "Loading Saves" screen — shows a list of the 3 manual slots
    /// plus the autosave. Each slot card briefly summarises its
    /// contents (name, seed, playtime, save date) and offers Load +
    /// Delete. The same UI doubles as "save the current world here"
    /// when the player enters from the in-game quick-save hotkey.
    /// </summary>
    public class SaveSlotsUI : MonoBehaviour
    {
        public static SaveSlotsUI Instance { get; private set; }

        public enum Mode { Load, Save }
        private Mode _mode = Mode.Load;

        private GameObject _panelGo;
        private GameObject _cardContainer;
        private TMP_Text _headerLabel;
        private List<GameObject> _cards = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[SaveSlotsUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<SaveSlotsUI>();
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

        public void ShowLoadMode()
        {
            _mode = Mode.Load;
            Refresh();
            Show();
        }

        public void ShowSaveMode()
        {
            _mode = Mode.Save;
            Refresh();
            Show();
        }

        public void Show()
        {
            if (_panelGo == null) return;
            InputReader.BlockGameplayInput = true;
            Refresh();
            _panelGo.SetActive(true);
        }

        public void Hide()
        {
            if (_panelGo == null) return;
            _panelGo.SetActive(false);
            InputReader.BlockGameplayInput = false;
        }


        private void BuildPanel(Canvas canvas)
        {
            _panelGo = new GameObject("SaveSlotsPanel", typeof(RectTransform), typeof(Image));
            _panelGo.transform.SetParent(canvas.transform, false);
            RectTransform rt = (RectTransform)_panelGo.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 600f);
            rt.anchoredPosition = Vector2.zero;
            Image bg = _panelGo.GetComponent<Image>();
            bg.color = new Color(0.09f, 0.07f, 0.05f, 0.97f);
            bg.raycastTarget = true;

            GameObject headerGo = new GameObject("Header", typeof(RectTransform));
            headerGo.transform.SetParent(_panelGo.transform, false);
            RectTransform headerRt = (RectTransform)headerGo.transform;
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.offsetMin = new Vector2(24f, -72f);
            headerRt.offsetMax = new Vector2(-24f, -16f);
            _headerLabel = headerGo.AddComponent<TextMeshProUGUI>();
            _headerLabel.text = "Loading Saves";
            _headerLabel.fontSize = 32f;
            _headerLabel.fontStyle = FontStyles.Bold;
            _headerLabel.color = new Color(0.96f, 0.88f, 0.62f, 1f);
            _headerLabel.alignment = TextAlignmentOptions.Center;
            _headerLabel.richText = false;

            GameObject cardBg = new GameObject("CardContainer", typeof(RectTransform));
            cardBg.transform.SetParent(_panelGo.transform, false);
            RectTransform cardBgRt = (RectTransform)cardBg.transform;
            cardBgRt.anchorMin = new Vector2(0f, 0f);
            cardBgRt.anchorMax = new Vector2(1f, 1f);
            cardBgRt.pivot = new Vector2(0.5f, 0.5f);
            cardBgRt.offsetMin = new Vector2(24f, 80f);
            cardBgRt.offsetMax = new Vector2(-24f, -84f);
            _cardContainer = cardBg;

            BuildBackButton(_panelGo.transform);
        }

        private void BuildBackButton(Transform parent)
        {
            GameObject go = new GameObject("BackButton", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(160f, 48f);
            rt.anchoredPosition = new Vector2(24f, 16f);
            Image img = go.GetComponent<Image>();
            img.color = new Color(0.18f, 0.16f, 0.12f, 1f);

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.20f, 0.18f, 0.14f, 1f);
            cb.highlightedColor = new Color(0.36f, 0.30f, 0.22f, 1f);
            cb.pressedColor = new Color(0.10f, 0.09f, 0.07f, 1f);
            btn.colors = cb;
            btn.onClick.AddListener(OnBackClicked);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = "Back";
            lbl.fontSize = 22f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = new Color(0.95f, 0.90f, 0.74f, 1f);
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.richText = false;
        }


        private void Refresh()
        {
            if (_cardContainer == null || SaveGameManager.Instance == null) return;

            foreach (var c in _cards) { if (c != null) Destroy(c); }
            _cards.Clear();

            if (_headerLabel != null)
                _headerLabel.text = _mode == Mode.Load ? "Loading Saves" : "Save to a Slot";

            List<SaveSlotSummary> slots = SaveGameManager.Instance.ListSlots();

            // Lay out 4 cards in a 2x2 grid filling the container.
            int n = slots.Count;
            for (int i = 0; i < n; i++)
            {
                int row = i / 2;
                int col = i % 2;
                GameObject card = BuildSlotCard(_cardContainer.transform, slots[i], row, col);
                _cards.Add(card);
            }
        }

        private GameObject BuildSlotCard(Transform parent, SaveSlotSummary summary, int row, int col)
        {
            GameObject card = new GameObject($"Card_{summary.slotFileName}", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);
            RectTransform cardRt = (RectTransform)card.transform;
            cardRt.anchorMin = new Vector2(0f, 1f);
            cardRt.anchorMax = new Vector2(0f, 1f);
            cardRt.pivot = new Vector2(0f, 1f);
            // 2 columns, 2 rows; each card ~half the container width, ~half the height.
            cardRt.sizeDelta = new Vector2(395f, 175f);
            float x = 8f + col * 405f;
            float y = -8f - row * 185f;
            cardRt.anchoredPosition = new Vector2(x, y);
            Image bg = card.GetComponent<Image>();
            bg.color = new Color(0.18f, 0.14f, 0.10f, 1f);

            string titleText = summary.slotIndex < 0
                ? "Autosave"
                : $"Slot {summary.slotIndex}";
            if (summary.exists) titleText += "  •  " + (string.IsNullOrEmpty(summary.saveName) ? "Untitled" : summary.saveName);

            TMP_Text title = AddLabel(card.transform, "Title", titleText,
                anchoredPos: new Vector2(0f, -16f), size: new Vector2(380f, 36f),
                color: new Color(0.95f, 0.85f, 0.55f, 1f), fontSize: 22, style: FontStyles.Bold,
                alignment: TextAlignmentOptions.MidlineLeft);

            string details = summary.exists
                ? BuildSummaryLine(summary)
                : "<i>(empty slot)</i>";
            TMP_Text body = AddLabel(card.transform, "Body", details,
                anchoredPos: new Vector2(0f, -56f), size: new Vector2(380f, 70f),
                color: new Color(0.92f, 0.84f, 0.72f, 1f), fontSize: 14, style: FontStyles.Normal,
                alignment: TextAlignmentOptions.TopLeft);

            // Action buttons: primary + delete (or only primary when slot is empty in Load mode).
            BuildCardButton(card.transform, summary, _mode == Mode.Save ? "Save Here" : "Load",
                anchoredPos: new Vector2(0f, -134f), size: new Vector2(160f, 38f),
                onClick: () => OnPrimaryClicked(summary));
            if (summary.exists)
            {
                BuildCardButton(card.transform, summary, "Delete",
                    anchoredPos: new Vector2(220f, -134f), size: new Vector2(140f, 38f),
                    onClick: () => OnDeleteClicked(summary));
            }

            return card;
        }

        private static string BuildSummaryLine(SaveSlotSummary s)
        {
            string seedText = $"Seed: {s.worldSeed}";
            string playMin = $"Playtime: {(int)(s.playTimeSeconds / 60f)} min";
            string dateText = string.IsNullOrEmpty(s.saveTimestampUtc)
                ? string.Empty
                : $"Saved: {FormatTimestamp(s.saveTimestampUtc)}";
            return seedText + "\n" + playMin + "\n" + dateText;
        }

        private static string FormatTimestamp(string iso)
        {
            if (DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dt))
            {
                return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }
            return iso;
        }

        private static TMP_Text AddLabel(Transform parent, string name, string text,
            Vector2 anchoredPos, Vector2 size, Color color, float fontSize, FontStyles style,
            TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = alignment;
            t.richText = true;
            return t;
        }

        private static void BuildCardButton(Transform parent, SaveSlotSummary summary,
            string label, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            Image img = go.GetComponent<Image>();
            img.color = new Color(0.24f, 0.20f, 0.16f, 1f);

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.24f, 0.20f, 0.16f, 1f);
            cb.highlightedColor = new Color(0.40f, 0.32f, 0.22f, 1f);
            cb.pressedColor = new Color(0.12f, 0.10f, 0.08f, 1f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = label;
            lbl.fontSize = 18f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = new Color(0.95f, 0.90f, 0.74f, 1f);
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.richText = false;
        }


        private void OnPrimaryClicked(SaveSlotSummary summary)
        {
            if (SaveGameManager.Instance == null) return;
            if (_mode == Mode.Load)
            {
                if (!summary.exists) return;
                SaveGameManager.Instance.LoadFromPath(summary.fullPath);
                if (MainMenuUI.Instance != null) MainMenuUI.Instance.Hide();
                Hide();
                MainMenuUI.StartGameSession();
            }
            else
            {
                bool success = false;
                if (summary.slotIndex < 0)
                {
                    success = SaveGameManager.Instance.SaveToAutosave();
                }
                else
                {
                    success = SaveGameManager.Instance.SaveToSlot(summary.slotIndex);
                }

                if (success && ItemNotificationManager.Instance != null)
                {
                    ItemNotificationManager.Instance.TriggerNotification("Game saved", UIResourceHelper.GetSaveIconSprite(), new Color(0.4f, 1.0f, 0.4f));
                }
                Refresh();
            }
        }

        private void OnDeleteClicked(SaveSlotSummary summary)
        {
            if (SaveGameManager.Instance == null || !summary.exists) return;
            if (summary.slotIndex < 0) SaveGameManager.Instance.DeleteAutosave();
            else SaveGameManager.Instance.DeleteSlot(summary.slotIndex);
            Refresh();
        }

        private void OnBackClicked()
        {
            Hide();
            if (MainMenuUI.Instance != null) MainMenuUI.Instance.Show();
        }
    }
}
