using UnityEngine;
using UnityEngine.UI;
using Willowstead.Farming;

namespace Willowstead.Player
{
    /// <summary>
    /// Programmatically constructs and manages the 8-slot Hotbar HUD panel at the bottom of the screen.
    /// Pure code-built UI: 100% self-contained, requires zero inspector prefabs or external scene wiring.
    /// </summary>
    public class HotbarUI : MonoBehaviour
    {
        [Header("Theme Icons (Optional Overrides)")]
        [SerializeField] private Sprite _hoeIcon;
        [SerializeField] private Sprite _wateringCanIcon;
        [SerializeField] private Sprite _seedIcon;
        [SerializeField] private Sprite _carrotIcon;
        [SerializeField] private Sprite _axeIcon;
        [SerializeField] private Sprite _logIcon;

        private InventoryManager _inventory;
        private FarmingController _farmingController;

        private GameObject _canvasGo;
        private GameObject _hotbarGo;

        private RectTransform[] _slotTransforms;
        private Image[] _slotHighlights;
        private Image[] _slotIconImages;
        private Text[] _slotCountTexts;

        public static HotbarUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            _inventory = GetComponent<InventoryManager>();
            if (_inventory == null) _inventory = FindAnyObjectByType<InventoryManager>();

            _farmingController = GetComponent<FarmingController>();
            if (_farmingController == null) _farmingController = FindAnyObjectByType<FarmingController>();

            CreateHotbarUI();
            RefreshUI();
        }

        private void Update()
        {
            if (_farmingController != null && _slotTransforms != null)
            {
                UpdateSelection(_farmingController.SelectedSlotIndex);
            }
            RefreshUI();
        }

        public void RefreshUI()
        {
            if (_inventory == null || _slotIconImages == null) return;

            for (int i = 0; i < 8; i++)
            {
                if (i >= _slotIconImages.Length || _slotIconImages[i] == null) continue;

                InventorySlot slotData = _inventory.GetSlotItem(i);
                if (slotData == null || slotData.IsEmpty)
                {
                    _slotIconImages[i].sprite = null;
                    _slotIconImages[i].enabled = false;
                    if (_slotCountTexts[i] != null) _slotCountTexts[i].enabled = false;
                }
                else
                {
                    Sprite iconSprite = GetIconForItem(slotData.itemName);

                    _slotIconImages[i].sprite = iconSprite;
                    _slotIconImages[i].enabled = true;
                    _slotIconImages[i].color = iconSprite != null ? Color.white : new Color(0.70f, 0.60f, 0.45f, 0.9f);

                    if (_slotCountTexts[i] != null)
                    {
                        if (slotData.itemName == "Hoe" || slotData.itemName == "Watering Can" || slotData.itemName == "Axe")
                        {
                            _slotCountTexts[i].enabled = false;
                        }
                        else
                        {
                            _slotCountTexts[i].text = slotData.quantity.ToString();
                            _slotCountTexts[i].enabled = true;
                        }
                    }
                }
            }
        }

        private void UpdateSelection(int selectedIndex)
        {
            if (_slotTransforms == null) return;

            for (int i = 0; i < 8; i++)
            {
                if (i >= _slotTransforms.Length || _slotTransforms[i] == null) continue;

                bool isSelected = (i == selectedIndex);
                Vector3 targetScale = isSelected ? new Vector3(1.14f, 1.14f, 1f) : Vector3.one;
                _slotTransforms[i].localScale = Vector3.Lerp(_slotTransforms[i].localScale, targetScale, Time.deltaTime * 15f);

                if (_slotHighlights != null && i < _slotHighlights.Length && _slotHighlights[i] != null)
                {
                    _slotHighlights[i].color = isSelected
                        ? new Color(1f, 0.82f, 0.0f, 0.95f) // Vibrant Gold
                        : new Color(0f, 0f, 0f, 0.40f);     // Dark Shadow border
                }
            }
        }

        private void CreateHotbarUI()
        {
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            _canvasGo = canvas != null ? canvas.gameObject : null;
            UIResourceHelper.EnsureEventSystem();

            if (_canvasGo == null) return;

            Transform existing = _canvasGo.transform.Find("HotbarPanel");
            if (existing != null) DestroyImmediate(existing.gameObject);

            _hotbarGo = new GameObject("HotbarPanel");
            _hotbarGo.transform.SetParent(_canvasGo.transform, false);

            RectTransform panelRect = _hotbarGo.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 32f);

            float slotWidth = 52f;
            float slotHeight = 52f;
            float spacing = 6f;
            float panelWidth = (8 * slotWidth) + (7 * spacing) + 20f;
            float panelHeight = slotHeight + 14f;
            panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

            // ── Outer Border & Cozy Slate ──
            Image panelBg = _hotbarGo.AddComponent<Image>();
            panelBg.sprite = UIResourceHelper.GetBackgroundSprite();
            panelBg.type = Image.Type.Sliced;
            panelBg.color = new Color(0.85f, 0.65f, 0.48f, 1f); // Warm peach-gold framing outline

            GameObject innerSlateGo = new GameObject("InnerSlate", typeof(RectTransform), typeof(Image));
            innerSlateGo.transform.SetParent(_hotbarGo.transform, false);
            RectTransform innerSlateRt = (RectTransform)innerSlateGo.transform;
            innerSlateRt.anchorMin = Vector2.zero; innerSlateRt.anchorMax = Vector2.one;
            innerSlateRt.offsetMin = new Vector2(4f, 4f); innerSlateRt.offsetMax = new Vector2(-4f, -4f);
            Image innerSlateBg = innerSlateGo.GetComponent<Image>();
            innerSlateBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            innerSlateBg.type = Image.Type.Sliced;
            innerSlateBg.color = new Color(0.48f, 0.35f, 0.32f, 0.98f); // Soft cozy mauve-brown slate

            CanvasGroup cg = _hotbarGo.AddComponent<CanvasGroup>();
            if (UI.MainMenuUI.Instance != null && !UI.MainMenuUI.HasGameStarted)
            {
                cg.alpha = 0f;
            }

            _slotTransforms = new RectTransform[8];
            _slotHighlights = new Image[8];
            _slotIconImages = new Image[8];
            _slotCountTexts = new Text[8];

            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            float startX = -((panelWidth - 20f) * 0.5f) + (slotWidth * 0.5f);

            for (int i = 0; i < 8; i++)
            {
                GameObject slotGo = new GameObject($"HotbarSlot_{i}");
                slotGo.transform.SetParent(_hotbarGo.transform, false);

                RectTransform slotRect = slotGo.AddComponent<RectTransform>();
                slotRect.sizeDelta = new Vector2(slotWidth, slotHeight);
                float posX = startX + i * (slotWidth + spacing);
                slotRect.anchoredPosition = new Vector2(posX, 0f);
                _slotTransforms[i] = slotRect;

                Image slotBg = slotGo.AddComponent<Image>();
                slotBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                slotBg.type = Image.Type.Sliced;
                slotBg.color = new Color(0.96f, 0.90f, 0.82f, 1.0f); // Warm parchment slot

                GameObject highlightGo = new GameObject("SlotHighlight");
                highlightGo.transform.SetParent(slotGo.transform, false);
                RectTransform hlRect = highlightGo.AddComponent<RectTransform>();
                hlRect.anchorMin = Vector2.zero;
                hlRect.anchorMax = Vector2.one;
                hlRect.sizeDelta = Vector2.zero;
                Image hlImage = highlightGo.AddComponent<Image>();
                hlImage.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                hlImage.type = Image.Type.Sliced;
                hlImage.color = new Color(0.85f, 0.65f, 0.48f, 0.25f);
                hlImage.raycastTarget = false;
                _slotHighlights[i] = hlImage;

                GameObject iconGo = new GameObject("SlotIconImage");
                iconGo.transform.SetParent(slotGo.transform, false);
                RectTransform iconRect = iconGo.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.12f, 0.12f);
                iconRect.anchorMax = new Vector2(0.88f, 0.88f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                Image iconImg = iconGo.AddComponent<Image>();
                iconImg.enabled = false;
                iconImg.raycastTarget = false;
                _slotIconImages[i] = iconImg;

                GameObject countGo = new GameObject("SlotCountText");
                countGo.transform.SetParent(slotGo.transform, false);
                RectTransform countRect = countGo.AddComponent<RectTransform>();
                countRect.anchorMin = new Vector2(1f, 0f);
                countRect.anchorMax = new Vector2(1f, 0f);
                countRect.pivot = new Vector2(1f, 0f);
                countRect.anchoredPosition = new Vector2(-4f, 4f);
                countRect.sizeDelta = new Vector2(30f, 20f);
                Text countText = countGo.AddComponent<Text>();
                countText.font = legacyFont;
                countText.fontSize = 13;
                countText.fontStyle = FontStyle.Bold;
                countText.alignment = TextAnchor.LowerRight;
                countText.color = new Color(0.25f, 0.16f, 0.10f, 1f);
                countText.enabled = false;
                countText.raycastTarget = false;
                _slotCountTexts[i] = countText;

                UIDragSlot dragSlot = slotGo.AddComponent<UIDragSlot>();
                dragSlot.slotIndex = i;
            }

            Debug.Log("[HotbarUI] Programmatically constructed 8-slot Hotbar HUD panel successfully.");
        }

        public RectTransform GetSlotRectForItem(string itemName)
        {
            if (_inventory != null && _slotTransforms != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    var slot = _inventory.GetSlotItem(i);
                    if (slot != null && slot.itemName == itemName)
                        return _slotTransforms[i];
                }
            }
            return (_slotTransforms != null && _slotTransforms.Length > 3) ? _slotTransforms[3] : null;
        }

        public void PulseCarrotSlot()
        {
            RectTransform slot = GetSlotRectForItem("Carrot");
            if (slot != null) PulseSlot(slot);
        }

        public void PulseSlot(RectTransform slot)
        {
            if (slot == null) return;
            StartCoroutine(PlayPulseCoroutine(slot));
        }

        private System.Collections.IEnumerator PlayPulseCoroutine(RectTransform slot)
        {
            Vector3 orig = slot.localScale;
            Vector3 target = orig * 1.25f;
            float dur = 0.12f; float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                slot.localScale = Vector3.Lerp(orig, target, elapsed / dur);
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                slot.localScale = Vector3.Lerp(target, orig, elapsed / dur);
                yield return null;
            }
            slot.localScale = orig;
        }

        private Sprite GetIconForItem(string itemName)
        {
            Sprite dbSprite = UIResourceHelper.GetItemIconSprite(itemName);
            if (dbSprite != null) return dbSprite;

            if (itemName == "Hoe" && _hoeIcon != null) return _hoeIcon;
            if (itemName == "Watering Can" && _wateringCanIcon != null) return _wateringCanIcon;
            if (itemName == "Axe" && _axeIcon != null) return _axeIcon;
            if ((itemName == "Carrot Seeds" || itemName == "Seed") && _seedIcon != null) return _seedIcon;
            if (itemName == "Carrot" && _carrotIcon != null) return _carrotIcon;
            if ((itemName == "Wood" || itemName == "Log") && _logIcon != null) return _logIcon;

            return null;
        }
    }
}
