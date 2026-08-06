using UnityEngine;
using UnityEngine.UI;

namespace Willowstead.Player
{
    /// <summary>
    /// Handles the visual display of the permanent Hotbar HUD at the bottom of the screen.
    /// Programmatically constructs an 8-slot Hotbar panel, rendering items from slots 0-7,
    /// and updates highlights based on the FarmingController selection.
    /// </summary>
    public class HotbarUI : MonoBehaviour
    {
        [Header("Prefab Mode")]
        [Tooltip("If true (default), the script binds to scene/prefab UI that you author — drag your canvas/prefab GameObjects into the matching _prefab* fields below. If false, the script builds the hotbar in code at runtime (the previous behaviour).")]
        [SerializeField] private bool _usePrefabLayout = true;
        [Tooltip("Root panel RectTransform for the Hotbar (already placed in scene/prefab).")]
        [SerializeField] private RectTransform _prefabHotbarRoot;
        [Tooltip("Container whose children are the 8 hotbar slot RectTransforms (order = slots 0..7).")]
        [SerializeField] private Transform _prefabSlotsRoot;

        [Header("Theme Sprites")]
        [Tooltip("Optional: Hotbar frame/background sprite. If 9-sliced, set borders in Sprite Editor.")]
        [SerializeField] private Sprite _hotbarFrameSprite;
        [Tooltip("Optional: Slot background sprite. If 9-sliced, set borders in Sprite Editor.")]
        [SerializeField] private Sprite _slotBackgroundSprite;

        [Header("Assets")]
        [Tooltip("The icon to represent the Hoe.")]
        [SerializeField] private Sprite _hoeIcon;

        [Tooltip("The icon to represent the Watering Can.")]
        [SerializeField] private Sprite _wateringCanIcon;

        [Tooltip("The icon to represent the seed item (e.g. CarrotSeed.png).")]
        [SerializeField] private Sprite _seedIcon;        [Tooltip("The icon to represent the harvested crop item (e.g. Carrot.png).")]
        [SerializeField] private Sprite _carrotIcon;

        [Tooltip("The icon to represent the Axe tool.")]
        [SerializeField] private Sprite _axeIcon;

        [Tooltip("The icon to represent a stack of Logs.")]
        [SerializeField] private Sprite _logIcon;

        [System.Serializable] public class HotbarIconEntry
        {
            [Tooltip("Inventory item name, e.g. 'Potato Seeds' or 'Potato'.")]
            public string itemName;
            public Sprite icon;
        }

        [Header("Crop & Seed Icons")]
        [Tooltip("Map every seed and harvested crop item name to its icon. Add one entry per item.")]
        [SerializeField] private HotbarIconEntry[] _itemIcons = new HotbarIconEntry[0];

        private InventoryManager _inventory;
        private Farming.FarmingController _farmingController;

        private GameObject _canvasGo;
        private GameObject _hotbarGo;

        private RectTransform[] _slotTransforms;
        private Image[] _slotHighlights;
        private Image[] _slotIconImages;
        private Text[] _slotCountTexts;

        /// <summary>
        /// Returns the RectTransform of the slot currently holding Carrot Seeds.
        /// Falls back to Slot 2 if not found.
        /// </summary>
        public RectTransform SeedSlotRect
        {
            get
            {
                if (_inventory != null && _slotTransforms != null)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        var slot = _inventory.GetSlotItem(i);
                        if (slot != null && slot.itemName == "Carrot Seeds") return _slotTransforms[i];
                    }
                }
                return (_slotTransforms != null && _slotTransforms.Length > 2) ? _slotTransforms[2] : null;
            }
        }

        /// Returns the RectTransform of the hotbar slot that holds the given item name.
        /// Falls back to slot 3 if not found.
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

        /// Backward-compat alias.
        public RectTransform CarrotSlotRect => GetSlotRectForItem("Carrot");

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Helper: pick the largest sub-sprite from a multi-sprite texture (e.g., frames atlases)
        Sprite LoadLargestSprite(string path)
        {
            var all = UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            Sprite best = null; float bestArea = -1f;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is Sprite s)
                {
                    var r = s.rect; float a = r.width * r.height;
                    if (a > bestArea) { best = s; bestArea = a; }
                }
            }
            if (best != null) return best;
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Auto-fill theme sprites from your new folders if present
        if (_hotbarFrameSprite == null)
            _hotbarFrameSprite = LoadLargestSprite("Assets/Sprites/Inventory & chests/2/hotbar frame.png");
        if (_slotBackgroundSprite == null)
            _slotBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Inventory & chests/2/brown slot.png");

        if (_hoeIcon == null) _hoeIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Hoe.png");
        if (_wateringCanIcon == null) _wateringCanIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Watering can.png");
        if (_seedIcon == null) _seedIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/CarrotSeed.png");
        if (_carrotIcon == null) _carrotIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Carrot.png");
        if (_axeIcon == null) _axeIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Axe.png");
        if (_logIcon == null) _logIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/log.png");
    }
#endif

        private System.Collections.Generic.Dictionary<RectTransform, Coroutine> _activePulses = new System.Collections.Generic.Dictionary<RectTransform, Coroutine>();

        private void Start()
        {
            _inventory = GetComponent<InventoryManager>();
            if (_inventory == null) _inventory = FindAnyObjectByType<InventoryManager>();

            _farmingController = GetComponent<Farming.FarmingController>();
            if (_farmingController == null) _farmingController = FindAnyObjectByType<Farming.FarmingController>();

        #if UNITY_EDITOR
            // Ensure themed sprites are assigned even if domain reload is disabled
            if (_hotbarFrameSprite == null)
            {
                Sprite LoadLargest(string path)
                {
                    var all = UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                    Sprite best = null; float area = -1f;
                    for (int i = 0; i < all.Length; i++) if (all[i] is Sprite s)
                    { var r = s.rect; float a = r.width * r.height; if (a > area) { best = s; area = a; } }
                    return best != null ? best : UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                _hotbarFrameSprite = LoadLargest("Assets/Sprites/Inventory & chests/2/hotbar frame.png");
            }
            if (_slotBackgroundSprite == null)
            {
                _slotBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Inventory & chests/2/brown slot.png");
            }
        #endif

        #if UNITY_EDITOR
            // Ensure themed sprites are assigned even if domain reload is disabled
            if (_hotbarFrameSprite == null)
            {
                Sprite LoadLargest(string path)
                {
                    var all = UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                    Sprite best = null; float area = -1f;
                    for (int i = 0; i < all.Length; i++) if (all[i] is Sprite s)
                    { var r = s.rect; float a = r.width * r.height; if (a > area) { best = s; area = a; } }
                    return best != null ? best : UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                _hotbarFrameSprite = LoadLargest("Assets/Sprites/Inventory & chests/2/hotbar frame.png");
            }
            if (_slotBackgroundSprite == null)
            {
                _slotBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Inventory & chests/2/brown slot.png");
            }
        #endif

            if (_usePrefabLayout)
            {
                BindPrefabHotbar();
            }
            else
            {
                CreateHotbarUI();
            }
            RefreshUI();
        }

        private void Update()
        {
            // Sync selection highlight
            if (_farmingController != null)
            {
                UpdateSelection(_farmingController.SelectedSlotIndex);
            }

            // Realtime count check
            RefreshUI();
        }

        /// <summary>
        /// Rebuilds slot icons and quantity text indicators dynamically based on slots 0-7.
        /// </summary>
        public void RefreshUI()
        {
            // Don't try to render to allocated-but-unwired slot arrays when prefab
            // refs are still null - the breadcrumb in BindPrefabHotbar() already
            // logged the fix instructions to the developer.
            if (_usePrefabLayout && (_prefabHotbarRoot == null || _prefabSlotsRoot == null)) return;
            if (_inventory == null || _slotIconImages == null) return;

            for (int i = 0; i < 8; i++)
            {
                InventorySlot slotData = _inventory.GetSlotItem(i);
                if (slotData == null || slotData.IsEmpty)
                {
                    _slotIconImages[i].sprite = null;
                    _slotIconImages[i].enabled = false;
                    if (_slotCountTexts[i] != null) _slotCountTexts[i].enabled = false;
                }
                else
                {
                    // Find correct sprite based on item name
                    Sprite sprite = GetIconForItem(slotData.itemName);

                    _slotIconImages[i].sprite = sprite;
                    _slotIconImages[i].enabled = (sprite != null);

                    // Fallback colors if sprite is missing
                    if (sprite == null)
                    {
                        _slotIconImages[i].enabled = true;
                        _slotIconImages[i].color = new Color(0.65f, 0.55f, 0.40f, 0.85f); // neutral tan
                    }
                    else
                    {
                        _slotIconImages[i].color = Color.white;
                    }

                    // Count display (hide for tools, show for seeds/crops/logs)
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

        private Sprite GetIconForItem(string itemName)
        {
            if (itemName == "Hoe")          return _hoeIcon;
            if (itemName == "Watering Can") return _wateringCanIcon;
            if (itemName == "Axe")          return _axeIcon;

            // Check the configurable mapping first
            if (_itemIcons != null)
            {
                foreach (var entry in _itemIcons)
                    if (entry != null && entry.itemName == itemName && entry.icon != null)
                        return entry.icon;
            }

            // Legacy fallbacks
            if (itemName == "Carrot Seeds") return _seedIcon;
            if (itemName == "Carrot")       return _carrotIcon;
            if (itemName == "Log")          return _logIcon;

            return null;
        }

        private void UpdateSelection(int selectedIndex)
        {
            if (_slotTransforms == null) return;

            for (int i = 0; i < _slotTransforms.Length; i++)
            {
                if (_slotTransforms[i] != null)
                {
                    bool isSelected = (i == selectedIndex);

                    // Zoom active slot slightly
                    Vector3 targetScale = isSelected ? new Vector3(1.14f, 1.14f, 1f) : Vector3.one;
                    _slotTransforms[i].localScale = Vector3.Lerp(_slotTransforms[i].localScale, targetScale, Time.deltaTime * 15f);

                    // Highlights the gold border when active, or drops to a subtle dark shadow when inactive
                    if (_slotHighlights != null && _slotHighlights[i] != null)
                    {
                        _slotHighlights[i].color = isSelected
                            ? new Color(1f, 0.82f, 0.0f, 0.95f) // Vibrant Gold
                            : new Color(0f, 0f, 0f, 0.45f);    // Subtle Dark Shadow border
                    }
                }
            }
        }

        private void BindPrefabHotbar()
        {
            if (_prefabHotbarRoot == null || _prefabSlotsRoot == null)
            {
                Debug.LogWarning("[HotbarUI] Prefab mode is on but _prefabHotbarRoot or _prefabSlotsRoot is unset. " +
                    "The hotbar will not render. Either assign a canvas/prefab root to _prefabHotbarRoot and a container with 8 child slot RectTransforms (order = slot 0..7) to _prefabSlotsRoot, " +
                    "or uncheck _usePrefabLayout to fall back to runtime code-built UI.");
                // Allocate empty arrays so Update / RefreshUI early-return cleanly instead of NRE'ing.
                _slotTransforms = new RectTransform[8];
                _slotHighlights = new Image[8];
                _slotIconImages = new Image[8];
                _slotCountTexts = new Text[8];
                return;
            }

            _hotbarGo = _prefabHotbarRoot != null ? _prefabHotbarRoot.gameObject : null;

            int total = _prefabSlotsRoot != null ? _prefabSlotsRoot.childCount : 0;
            if (total <= 0)
            {
                Debug.LogWarning("[HotbarUI] Prefab Slots Root has no children. Expected 8 for slots 0-7.");
                total = 8;
            }

            _slotTransforms = new RectTransform[total];
            _slotHighlights = new Image[total];
            _slotIconImages = new Image[total];
            _slotCountTexts = new Text[total];

            for (int i = 0; i < total; i++)
            {
                Transform child = i < _prefabSlotsRoot.childCount ? _prefabSlotsRoot.GetChild(i) : null;
                if (child == null) { continue; }

                var rt = child as RectTransform;
                if (rt == null) rt = child.gameObject.AddComponent<RectTransform>();
                _slotTransforms[i] = rt;

                // Ensure UIDragSlot exists with correct index (0..7)
                var drag = child.GetComponent<UIDragSlot>();
                if (drag == null) drag = child.gameObject.AddComponent<UIDragSlot>();
                drag.slotIndex = i;

                // Icon image
                Image icon = UIResourceHelper.FindChildComponentByName<Image>(child,
                    new [] { "SlotIconImage", "Icon", "ItemIcon" });
                if (icon == null)
                {
                    var imgs = child.GetComponentsInChildren<Image>(true);
                    if (imgs.Length > 0) icon = imgs[imgs.Length - 1];
                }
                if (icon != null) icon.raycastTarget = false;
                _slotIconImages[i] = icon;

                // Count text
                Text count = UIResourceHelper.FindChildComponentByName<Text>(child,
                    new [] { "SlotCountText", "CountText", "Qty", "Amount" });
                _slotCountTexts[i] = count;
                if (count != null)
                {
                    var rtCount = count.GetComponent<RectTransform>();
                    if (rtCount != null)
                    {
                        rtCount.anchorMin = new Vector2(1f, 0f);
                        rtCount.anchorMax = new Vector2(1f, 0f);
                        rtCount.pivot     = new Vector2(1f, 0f);
                        if (rtCount.sizeDelta == Vector2.zero) rtCount.sizeDelta = new Vector2(25f, 20f);
                        rtCount.anchoredPosition = new Vector2(-12f, 12f);
                    }
                    if (count.GetComponent<Outline>() == null)
                    {
                        var ol = count.gameObject.AddComponent<Outline>();
                        ol.effectColor = Color.black;
                    }
                }

                // Optional highlight
                Image highlight = UIResourceHelper.FindChildComponentByName<Image>(child, new [] { "HighlightBorder", "Highlight" });
                if (highlight == icon)
                {
                    // If the found highlight is actually the icon, try another
                    var imgs2 = child.GetComponentsInChildren<Image>(true);
                    if (imgs2.Length > 1) highlight = imgs2[0];
                }
                _slotHighlights[i] = highlight;
            }
        }

        private void CreateHotbarUI()
        {
            // Find or create HUDCanvas (with normalised CanvasScaler + GraphicRaycaster)
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            _canvasGo = canvas != null ? canvas.gameObject : null;

            // Choose theme sprites or fall back to built-ins if not assigned
            Sprite roundedBg = _hotbarFrameSprite != null ? _hotbarFrameSprite : UIResourceHelper.GetBackgroundSprite();
            Sprite slotBg = _slotBackgroundSprite != null ? _slotBackgroundSprite : UIResourceHelper.GetInputFieldBackgroundSprite();

            // Create Hotbar Panel at bottom-center of screen (expanded width for 8 slots)
            _hotbarGo = new GameObject("HotbarPanel");
            _hotbarGo.transform.SetParent(_canvasGo.transform, false);
            Image bgImage = _hotbarGo.AddComponent<Image>();
            bgImage.sprite = roundedBg;
            bgImage.type = (roundedBg != null && roundedBg.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
            // Always show provided art at full color; only tint if no sprite
            bgImage.color = (roundedBg != null) ? Color.white : new Color(0.14f, 0.12f, 0.1f, 0.92f);

            RectTransform bgRect = _hotbarGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0f);
            bgRect.anchorMax = new Vector2(0.5f, 0f);
            bgRect.pivot = new Vector2(0.5f, 0f);
            bgRect.anchoredPosition = new Vector2(0f, 15f);
            bgRect.sizeDelta = new Vector2(660f, 86f);

            int slotCount = 8;
            float slotWidth = 60f;
            float slotHeight = 60f;
            float startX = -280f; // Centers 8 slots inside 660px width
            float spacing = 80f;

            _slotTransforms = new RectTransform[slotCount];
            _slotHighlights = new Image[slotCount];
            _slotIconImages = new Image[slotCount];
            _slotCountTexts = new Text[slotCount];

            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < slotCount; i++)
            {
                // Slot Container
                GameObject slotGo = new GameObject($"Slot_{i}");
                slotGo.transform.SetParent(_hotbarGo.transform, false);
                RectTransform slotRect = slotGo.AddComponent<RectTransform>();
                slotRect.anchoredPosition = new Vector2(startX + i * spacing, 0f);
                slotRect.sizeDelta = new Vector2(slotWidth, slotHeight);
                _slotTransforms[i] = slotRect;

                // Add drag-and-drop capability
                UIDragSlot dragComponent = slotGo.AddComponent<UIDragSlot>();
                dragComponent.slotIndex = i;

                // Slot border outline / shadow image
                GameObject highlightGo = new GameObject("HighlightBorder");
                highlightGo.transform.SetParent(slotRect, false);
                Image highlightImg = highlightGo.AddComponent<Image>();
                highlightImg.sprite = roundedBg;
                highlightImg.type = (roundedBg != null && roundedBg.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
                highlightImg.color = (highlightImg.type == Image.Type.Sliced) ? new Color(1f,1f,1f,1f) : new Color(0f, 0f, 0f, 0.45f);
                RectTransform highRect = highlightGo.GetComponent<RectTransform>();
                highRect.anchoredPosition = Vector2.zero;
                highRect.sizeDelta = new Vector2(slotWidth + 8f, slotHeight + 8f);
                _slotHighlights[i] = highlightImg;

                // Slot Background Panel
                GameObject innerBgGo = new GameObject("InnerBackground");
                innerBgGo.transform.SetParent(slotRect, false);
                Image innerBgImg = innerBgGo.AddComponent<Image>();
                innerBgImg.sprite = slotBg;
                innerBgImg.type = (slotBg != null && slotBg.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
                // Keep slot art colors; tint only if no sprite
                innerBgImg.color = (slotBg != null) ? Color.white : new Color(0.24f, 0.2f, 0.16f, 0.95f);
                RectTransform innerBgRect = innerBgGo.GetComponent<RectTransform>();
                innerBgRect.anchoredPosition = Vector2.zero;
                innerBgRect.sizeDelta = new Vector2(slotWidth, slotHeight);

                // Slot Icon
                GameObject iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(slotRect, false);
                _slotIconImages[i] = iconGo.AddComponent<Image>();
                _slotIconImages[i].raycastTarget = false; // Let drags pass to slot
                RectTransform iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(slotWidth - 16f, slotHeight - 16f);

                // Quantity Count Text label
                GameObject countGo = new GameObject("CountText");
                countGo.transform.SetParent(slotRect, false);
                _slotCountTexts[i] = countGo.AddComponent<Text>();
                _slotCountTexts[i].font = legacyFont;
                _slotCountTexts[i].fontSize = 14;
                _slotCountTexts[i].fontStyle = FontStyle.Bold;
                _slotCountTexts[i].alignment = TextAnchor.LowerRight;
                _slotCountTexts[i].color = Color.white;
                _slotCountTexts[i].raycastTarget = false;

                // Black text outline for readability
                countGo.AddComponent<Outline>().effectColor = Color.black;

                RectTransform countRect = countGo.GetComponent<RectTransform>();
                countRect.anchoredPosition = new Vector2(12f, -12f);
                countRect.sizeDelta = new Vector2(25f, 20f);
            }
        }

        public void PulseCarrotSlot() => PulseItemSlot("Carrot");

        public void PulseItemSlot(string itemName)
        {
            if (_inventory == null) return;
            for (int i = 0; i < 8; i++)
            {
                var slot = _inventory.GetSlotItem(i);
                if (slot != null && slot.itemName == itemName)
                {
                    PulseSlot(i);
                    return;
                }
            }
            PulseSlot(3); // fallback
        }

        /// <summary>
        /// Triggers a visual pulse animation on any slot index.
        /// </summary>
        public void PulseSlot(int slotIndex)
        {
            if (_slotTransforms != null && slotIndex >= 0 && slotIndex < _slotTransforms.Length && _slotTransforms[slotIndex] != null)
            {
                RectTransform rect = _slotTransforms[slotIndex];
                if (_activePulses.TryGetValue(rect, out var coroutine) && coroutine != null)
                {
                    StopCoroutine(coroutine);
                }
                _activePulses[rect] = StartCoroutine(PlayPulseAnimation(rect));
            }
        }

        private System.Collections.IEnumerator PlayPulseAnimation(RectTransform rect)
        {
            float duration = 0.18f;
            float elapsed = 0f;
            Vector3 originalScale = rect.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percent = elapsed / duration;

                // Scale up to 1.35x bouncily and settle back
                float scaleFactor = 1.0f + Mathf.Sin(percent * Mathf.PI) * 0.35f;
                rect.localScale = originalScale * scaleFactor;

                yield return null;
            }

            rect.localScale = originalScale;
            _activePulses.Remove(rect);
        }
    }
}
