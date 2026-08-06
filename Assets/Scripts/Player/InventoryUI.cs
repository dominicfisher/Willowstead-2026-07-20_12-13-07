using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Willowstead.Input;

namespace Willowstead.Player
{
    /// <summary>
    /// Handles the visual display of the player's inventory using a premium 2x8 grid UI panel.
    /// Programmatically constructs an overlay grid, rendering items from slots 8-23,
    /// and plays an overshoot spring opening bounce animation.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
            [Header("Prefab Mode")] 
            [Tooltip("If true (default), the script binds to scene/prefab UI that you author — drag your canvas/prefab GameObjects into _prefabPanelRoot and _prefabSlotsRoot below. If false, the script builds the inventory panel in code at runtime (the previous behaviour).")]
            [SerializeField] private bool _usePrefabLayout = true;
            [Tooltip("Root panel RectTransform for the Inventory UI (active/inactive toggled here).")]
            [SerializeField] private RectTransform _prefabPanelRoot;
            [Tooltip("Container whose children are the 16 inventory slot RectTransforms (order = slot 8..23).")]
            [SerializeField] private Transform _prefabSlotsRoot;

            [Header("Theme Sprites")] 
            [Tooltip("Optional: Panel frame/background sprite. If 9-sliced, set borders in Sprite Editor.")]
            [SerializeField] private Sprite _panelFrameSprite;
            [Tooltip("Optional: Slot background sprite. If 9-sliced, set borders in Sprite Editor.")]
            [SerializeField] private Sprite _slotBackgroundSprite;
            [Tooltip("Optional: Slot selection/highlight sprite.")]
            [SerializeField] private Sprite _slotSelectSprite;

	                [System.Serializable]
	                public class ItemIconEntry
	                {
	                    public string itemName;
	                    public Sprite icon;
	                }
	                [Header("Item Icons")] 
	                [Tooltip("Custom mapping from item name to icon sprite.")]
	                [SerializeField] private ItemIconEntry[] _itemIcons;
	                [Tooltip("The icon sprite to represent the Hoe.")]
	                [SerializeField] private Sprite _hoeIcon;

        [Tooltip("The icon sprite to represent the Watering Can.")]
        [SerializeField] private Sprite _wateringCanIcon;

        [Tooltip("The icon sprite to represent Carrot Seeds.")]
        [SerializeField] private Sprite _seedIcon;

        [Tooltip("The icon sprite to represent Carrots.")]
        [SerializeField] private Sprite _carrotIcon;

        [Tooltip("The icon sprite to represent the Axe tool.")]
        [SerializeField] private Sprite _axeIcon;

        [Tooltip("The icon sprite to represent a stack of Logs.")]
        [SerializeField] private Sprite _logIcon;

        private InventoryManager _inventory;
        private GameObject _canvasGo;
        private GameObject _panelGo;

        private Text _goldText;
        private Image[] _slotIcons;
        private Text[] _slotCountTexts;
        private bool _isOpen = false;
        private Coroutine _bounceCoroutine;

        public bool IsOpen => _isOpen;

	        #if UNITY_EDITOR
	                private void OnValidate()
	                {
	                    // Helper to pick the largest sub-sprite from a multi-sprite texture
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
	                    if (_panelFrameSprite == null)
	                    {
	                        _panelFrameSprite = LoadLargestSprite("Assets/Sprites/Inventory & chests/2/inventory frame.png");
	                        if (_panelFrameSprite == null)
	                            _panelFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Inventory & chests/1/inventory1.png");
	                    }
	                    if (_slotBackgroundSprite == null)
	                    {
	                        _slotBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Inventory & chests/2/brown slot.png");
	                        if (_slotBackgroundSprite == null)
	                            _slotBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Inventory & chests/1/slot1.png");
	                    }
	                    if (_slotSelectSprite == null)
	                    {
	                        // Prefer the more elaborate overlay if present
	                        var sel = UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath("Assets/Sprites/Slot select/slot select.png");
	                        for (int i = 0; i < sel.Length; i++) { if (sel[i] is Sprite s) { _slotSelectSprite = s; break; } }
	                        if (_slotSelectSprite == null)
	                            _slotSelectSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Slot select/slot select.png");
	                    }

                    if (_hoeIcon == null) _hoeIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Hoe.png");
                    if (_wateringCanIcon == null) _wateringCanIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Watering can.png");
                    if (_seedIcon == null) _seedIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/CarrotSeed.png");
                    if (_carrotIcon == null) _carrotIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Carrot.png");
                    if (_axeIcon == null) _axeIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Axe.png");
                    if (_logIcon == null) _logIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/log.png");
	                    // Try auto-fill for a few common inventory icon sheets
	                    if ((_itemIcons == null || _itemIcons.Length == 0))
	                    {
	                        // no-op: leave for user to fill in Inspector
	                    }
	                }
	        #endif

        private void Start()
        {
            _inventory = GetComponent<InventoryManager>();
            if (_inventory == null) _inventory = FindAnyObjectByType<InventoryManager>();

        #if UNITY_EDITOR
            // Ensure themed sprites are assigned even if domain reload is disabled
            if (_panelFrameSprite == null)
            {
                Sprite LoadLargest(string path)
                {
                    var all = UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                    Sprite best = null; float area = -1f;
                    for (int i = 0; i < all.Length; i++) if (all[i] is Sprite s)
                    { var r = s.rect; float a = r.width * r.height; if (a > area) { best = s; area = a; } }
                    return best != null ? best : UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                _panelFrameSprite = LoadLargest("Assets/Sprites/Inventory & chests/2/inventory frame.png");
                if (_panelFrameSprite == null)
                    _panelFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Inventory & chests/1/inventory1.png");
            }
            if (_slotBackgroundSprite == null)
            {
                _slotBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Inventory & chests/2/brown slot.png");
                if (_slotBackgroundSprite == null)
                    _slotBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Inventory & chests/1/slot1.png");
            }
            if (_slotSelectSprite == null)
            {
                var all = UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath("Assets/Sprites/Slot select/slot select.png");
                for (int i = 0; i < all.Length; i++) if (all[i] is Sprite s) { _slotSelectSprite = s; break; }
                if (_slotSelectSprite == null)
                    _slotSelectSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Slot select/slot select.png");
            }
        #endif

            if (_usePrefabLayout)
            {
                BindPrefabUI();
            }
            else
            {
                CreateUI();
            }
            SetUIActive(false);
        }

        private void Update()
        {
            // Toggle inventory on Tab or I key — gated so the background trigger
            // doesn't open a competing UI panel while the dev console has focus.
            // RefreshUI below runs unconditionally so the inventory stays current
            // even when the developer is typing into the console.
            if (!InputReader.BlockGameplayInput &&
                Keyboard.current != null &&
                (Keyboard.current.iKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame))
            {
                ToggleUI();
            }

            if (_isOpen)
            {
                RefreshUI();
            }
        }

        public void CloseUI()
        {
            if (_isOpen)
            {
                _isOpen = false;
                SetUIActive(false);
            }
        }

        public void ToggleUI()
        {
            _isOpen = !_isOpen;
            SetUIActive(_isOpen);

            if (_isOpen)
            {
                // Close Shop UI to prevent overlap
                ShopUI shopUI = FindAnyObjectByType<ShopUI>();
                if (shopUI != null) shopUI.CloseUI();

                RefreshUI();

                // Play opening bounce animation
                if (_panelGo != null)
                {
                    if (_bounceCoroutine != null) StopCoroutine(_bounceCoroutine);
                    _bounceCoroutine = StartCoroutine(PlayBounceAnimation(_panelGo.transform));
                }
            }
        }

        private System.Collections.IEnumerator PlayBounceAnimation(Transform panelTransform)
        {
            float duration = 0.22f;
            float elapsed = 0f;
            panelTransform.localScale = new Vector3(0.75f, 0.75f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Overshoot bounce curve
                float scale = Mathf.Lerp(0.75f, 1.08f, t);
                if (t > 0.7f)
                {
                    scale = Mathf.Lerp(1.08f, 1.0f, (t - 0.7f) / 0.3f);
                }
                panelTransform.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            panelTransform.localScale = Vector3.one;
            _bounceCoroutine = null;
        }

        private void SetUIActive(bool active)
        {
            if (_panelGo != null)
            {
                _panelGo.SetActive(active);
            }
            else if (_usePrefabLayout && _prefabPanelRoot != null)
            {
                _prefabPanelRoot.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// Rebuilds slot icons and quantity text indicators dynamically based on slots 8-23.
        /// </summary>
        public void RefreshUI()
        {
            // Don't try to render to allocated-but-unwired slot arrays when prefab
            // refs are still null — the breadcrumb in BindPrefabUI() already logged
            // the fix instructions to the developer.
            if (_usePrefabLayout && (_prefabPanelRoot == null || _prefabSlotsRoot == null)) return;
            if (_inventory == null || _slotIcons == null) return;

            // 1. Update Gold Display Text
            int goldCount = _inventory.GetItemCount("Gold");
            if (_goldText != null) _goldText.text = $"Gold: {goldCount}";

            // 2. Bind item data to the 16 slots (slots 8 to 23)
            int slotCount = _slotIcons.Length;
            for (int i = 0; i < slotCount; i++)
            {
                int inventorySlotIndex = 8 + i;
                InventorySlot slotData = _inventory.GetSlotItem(inventorySlotIndex);

                if (slotData == null || slotData.IsEmpty)
                {
                    _slotIcons[i].sprite = null;
                    _slotIcons[i].enabled = false;
                    if (_slotCountTexts[i] != null) _slotCountTexts[i].enabled = false;
                }
	                else
	                {
	                    // Find correct sprite based on item name
	                    Sprite sprite = GetIconForItem(slotData.itemName);
	
	                    _slotIcons[i].sprite = sprite;
	                    _slotIcons[i].enabled = (sprite != null);
	
	                    if (sprite == null)
	                    {
	                        _slotIcons[i].enabled = true;
	                        _slotIcons[i].color = new Color(0.65f, 0.55f, 0.40f, 0.85f);
	                    }
	                    else
	                    {
	                        _slotIcons[i].color = Color.white;
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
            if (itemName == "Hoe") return _hoeIcon;
            if (itemName == "Watering Can") return _wateringCanIcon;
            if (itemName == "Axe") return _axeIcon;
            if (_itemIcons != null)
            {
                foreach (var e in _itemIcons)
                {
                    if (e != null && e.itemName == itemName && e.icon != null)
                        return e.icon;
                }
            }
            if (itemName == "Carrot Seeds") return _seedIcon;
            if (itemName == "Carrot") return _carrotIcon;
            if (itemName == "Log") return _logIcon;
            return null;
        }
	
        private void CreateUI()
        {
            // Canvas + EventSystem setup (shared helper normalises CanvasScaler + GraphicRaycaster + EventSystem InputModule)
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            _canvasGo = canvas != null ? canvas.gameObject : null;
            UIResourceHelper.EnsureEventSystem();

	                // Choose theme sprites or fall back to built-ins if not assigned
	                Sprite roundedBg = _panelFrameSprite != null ? _panelFrameSprite : UIResourceHelper.GetBackgroundSprite();
	                Sprite slotBg = _slotBackgroundSprite != null ? _slotBackgroundSprite : UIResourceHelper.GetInputFieldBackgroundSprite();

	            // Main Panel Parent (sized to match sprite when available)
	            _panelGo = new GameObject("InventoryPanel");
	            _panelGo.transform.SetParent(_canvasGo.transform, false);

	            RectTransform panelRect = _panelGo.AddComponent<RectTransform>();
	            panelRect.anchoredPosition = Vector2.zero; // Center Screen

	            // Background Panel image
	            GameObject bgGo = new GameObject("BackgroundPanel");
	            bgGo.transform.SetParent(_panelGo.transform, false);
	            Image bgImage = bgGo.AddComponent<Image>();
	                bgImage.sprite = roundedBg;
	                // Simple for full-frame art without borders; Sliced if sprite has borders
	                bgImage.type = (roundedBg != null && roundedBg.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
	                // Always show provided art at full color; only tint if no sprite assigned
	                bgImage.color = roundedBg != null ? Color.white : new Color(0.14f, 0.12f, 0.1f, 0.95f);

	            RectTransform bgRect = bgGo.GetComponent<RectTransform>();
	            float panelW, panelH;
	            if (roundedBg != null)
	            {
	                // Use the sprite's UI-native size (handles PPU and Canvas reference pixels)
	                bgImage.SetNativeSize();
	                panelW = bgRect.sizeDelta.x;
	                panelH = bgRect.sizeDelta.y;
	            }
	            else
	            {
	                panelW = 680f;
	                panelH = 280f;
	                bgRect.sizeDelta = new Vector2(panelW, panelH);
	            }
	            // Match panel rect to background's native size
	            panelRect.sizeDelta = new Vector2(panelW, panelH);
	            bgRect.anchoredPosition = Vector2.zero;

	            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

	            // Header Title
	            GameObject titleGo = new GameObject("InventoryTitle");
	            titleGo.transform.SetParent(_panelGo.transform, false);
	            Text titleText = titleGo.AddComponent<Text>();
	            titleText.text = "INVENTORY";
	            titleText.font = legacyFont;
	            titleText.fontSize = 20;
	            titleText.fontStyle = FontStyle.Bold;
	            titleText.color = new Color(1.0f, 0.82f, 0.0f, 1f); // Gold title
	            titleText.alignment = TextAnchor.MiddleCenter;

	            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
	            // Place ~25 px below top edge regardless of panel height
	            titleRect.anchoredPosition = new Vector2(0f, (panelH * 0.5f) - 25f);
	            titleRect.sizeDelta = new Vector2(200f, 30f);

	            // Gold counter
	            GameObject goldGo = new GameObject("InventoryGoldText");
	            goldGo.transform.SetParent(_panelGo.transform, false);
	            _goldText = goldGo.AddComponent<Text>();
	            _goldText.text = "Gold: 100";
	            _goldText.font = legacyFont;
	            _goldText.fontSize = 15;
	            _goldText.fontStyle = FontStyle.Bold;
	            _goldText.color = new Color(0.9f, 0.9f, 0.9f, 1.0f);
	            _goldText.alignment = TextAnchor.MiddleCenter;

	            RectTransform goldRect = goldGo.GetComponent<RectTransform>();
	            // Place ~60 px below top edge
	            goldRect.anchoredPosition = new Vector2(0f, (panelH * 0.5f) - 60f);
	            goldRect.sizeDelta = new Vector2(120f, 25f);

	            // 16 Grid slots setup: 2 rows of 8 columns (matching 8-slot hotbar)
	            int columns = 8;
	            int rows = 2;
	            int totalSlots = columns * rows;

	            _slotIcons = new Image[totalSlots];
	            _slotCountTexts = new Text[totalSlots];

	            float slotWidth = 60f;
	            float slotHeight = 60f;
	            float spacingX = 80f;
	            float totalWidth = (columns - 1) * spacingX + slotWidth;
	            float startX = -totalWidth * 0.5f; // center horizontally regardless of panel width
	            
	            float startY = 15f;    // Row 1 Y coordinate (center-ish)
	            float spacingY = 75f;   // Row spacing

	            int index = 0;
	            for (int r = 0; r < rows; r++)
	            {
	                float currentY = startY - r * spacingY;

	                for (int c = 0; c < columns; c++)
	                {
	                    float currentX = startX + c * spacingX;

	                    // Slot container
	                    GameObject slotGo = new GameObject($"InventorySlot_{index}");
	                    slotGo.transform.SetParent(_panelGo.transform, false);
	                    RectTransform slotRect = slotGo.AddComponent<RectTransform>();
	                    slotRect.anchoredPosition = new Vector2(currentX, currentY);
	                    slotRect.sizeDelta = new Vector2(slotWidth, slotHeight);

	                    // Add drag-and-drop capability (slot index ranges from 8 to 23)
	                    UIDragSlot dragComponent = slotGo.AddComponent<UIDragSlot>();
	                    dragComponent.slotIndex = 8 + index;

	                    // Slot highlight/shadow border
	                    GameObject shadowGo = new GameObject("SlotShadow");
	                    shadowGo.transform.SetParent(slotRect, false);
	                    Image shadowImg = shadowGo.AddComponent<Image>();
	                        shadowImg.sprite = roundedBg;
	                        shadowImg.type = (roundedBg != null && roundedBg.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
	                        shadowImg.color = (shadowImg.type == Image.Type.Sliced) ? new Color(1f,1f,1f,1f) : new Color(0f, 0f, 0f, 0.45f);
	                    RectTransform shadowRect = shadowGo.GetComponent<RectTransform>();
	                    shadowRect.anchoredPosition = Vector2.zero;
	                    shadowRect.sizeDelta = new Vector2(slotWidth + 8f, slotHeight + 8f);

	                    // Slot inner panel background
	                    GameObject innerGo = new GameObject("SlotInnerBackground");
	                    innerGo.transform.SetParent(slotRect, false);
	                    Image innerImg = innerGo.AddComponent<Image>();
	                        innerImg.sprite = slotBg;
	                        innerImg.type = (slotBg != null && slotBg.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
	                        // Keep slot art colors as-authored; tint only if no sprite
	                        innerImg.color = slotBg != null ? Color.white : new Color(0.24f, 0.2f, 0.16f, 0.95f);
	                    RectTransform innerRect = innerGo.GetComponent<RectTransform>();
	                    innerRect.anchoredPosition = Vector2.zero;
	                    innerRect.sizeDelta = new Vector2(slotWidth, slotHeight);

	                    // Centered Item Icon Image
	                    GameObject iconGo = new GameObject("SlotIconImage");
	                    iconGo.transform.SetParent(slotRect, false);
	                    _slotIcons[index] = iconGo.AddComponent<Image>();
	                    _slotIcons[index].enabled = false;
	                    _slotIcons[index].raycastTarget = false; // Let drag events fall to slot
	                    RectTransform iconRect = iconGo.GetComponent<RectTransform>();
	                    iconRect.anchoredPosition = Vector2.zero;
	                    iconRect.sizeDelta = new Vector2(slotWidth - 16f, slotHeight - 16f);

	                        // Optional slot select overlay (disabled by default)
	                        if (_slotSelectSprite != null)
	                        {
	                            GameObject selGo = new GameObject("SlotSelectOverlay");
	                            selGo.transform.SetParent(slotRect, false);
	                Image selImg = selGo.AddComponent<Image>();
	                                selImg.sprite = _slotSelectSprite;
	                                selImg.type = (_slotSelectSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
	                                selImg.color = new Color(1f,1f,1f,0.0f);
	                                RectTransform selRect = selGo.GetComponent<RectTransform>();
	                                selRect.anchoredPosition = Vector2.zero;
	                                selRect.sizeDelta = new Vector2(slotWidth, slotHeight);
	                        }

	                        // Bottom-Right Quantity Counter Text
	                    GameObject countGo = new GameObject("SlotCountText");
	                    countGo.transform.SetParent(slotRect, false);
	                    _slotCountTexts[index] = countGo.AddComponent<Text>();
	                    _slotCountTexts[index].font = legacyFont;
	                    _slotCountTexts[index].fontSize = 14;
	                    _slotCountTexts[index].fontStyle = FontStyle.Bold;
	                    _slotCountTexts[index].alignment = TextAnchor.LowerRight;
	                    _slotCountTexts[index].color = Color.white;
	                    _slotCountTexts[index].enabled = false;
	                    _slotCountTexts[index].raycastTarget = false;

	                    // Black text outline for readability
	                    countGo.AddComponent<Outline>().effectColor = Color.black;

	                    RectTransform countRect = countGo.GetComponent<RectTransform>();
	                    countRect.anchoredPosition = new Vector2(12f, -12f);
	                    countRect.sizeDelta = new Vector2(25f, 20f);

	                    index++;
	                }
	            }

	            // Footer instructions
	            GameObject footerGo = new GameObject("InventoryFooterText");
	            footerGo.transform.SetParent(_panelGo.transform, false);
	            Text footerText = footerGo.AddComponent<Text>();
	            footerText.text = "Press 'I' or 'Tab' to close";
	            footerText.font = legacyFont;
	            footerText.fontSize = 12;
	            footerText.color = new Color(0.7f, 0.7f, 0.7f, 1.0f);
	            footerText.alignment = TextAnchor.MiddleCenter;

	                RectTransform footerRect = footerGo.GetComponent<RectTransform>();
	                footerRect.anchoredPosition = new Vector2(0f, -(panelH * 0.5f) + 26f);
	                footerRect.sizeDelta = new Vector2(250f, 25f);
	            }

	            // ─────────────────────────────────────────────────────────────────────
	            // Prefab binding (no programmatic layout)
	            // ─────────────────────────────────────────────────────────────────────
            private void BindPrefabUI()
            {
                if (_prefabPanelRoot == null || _prefabSlotsRoot == null)
                {
                    Debug.LogWarning("[InventoryUI] Prefab mode is on but _prefabPanelRoot or _prefabSlotsRoot is unset. " +
                        "The inventory panel will not render. Either assign a canvas/prefab root to _prefabPanelRoot and a container with 16 child slot RectTransforms (order = slots 8..23) to _prefabSlotsRoot, " +
                        "or uncheck _usePrefabLayout to fall back to runtime code-built UI.");
                    _slotIcons = new Image[16];
                    _slotCountTexts = new Text[16];
                    return;
                }

                _panelGo = _prefabPanelRoot != null ? _prefabPanelRoot.gameObject : null;

                // Gold text: try a few common names, else pick the first Text under the panel root
                _goldText = UIResourceHelper.FindChildComponentByName<Text>(_prefabPanelRoot,
                    new [] { "InventoryGoldText", "GoldText", "Gold", "Text_Gold" });

	                int totalSlots = _prefabSlotsRoot != null ? _prefabSlotsRoot.childCount : 0;
	                if (totalSlots <= 0)
	                {
	                    Debug.LogWarning("[InventoryUI] Prefab Slots Root has no children. Expected 16 for slots 8-23.");
	                    totalSlots = 16;
	                }

	                _slotIcons = new Image[totalSlots];
	                _slotCountTexts = new Text[totalSlots];

	                for (int i = 0; i < totalSlots; i++)
	                {
	                    Transform child = i < _prefabSlotsRoot.childCount ? _prefabSlotsRoot.GetChild(i) : null;
	                    if (child == null) { _slotIcons[i] = null; _slotCountTexts[i] = null; continue; }

	                    // Ensure UIDragSlot exists with correct index (8..23)
	                    var drag = child.GetComponent<UIDragSlot>();
	                    if (drag == null) drag = child.gameObject.AddComponent<UIDragSlot>();
	                    drag.slotIndex = 8 + i;

                    // Icon image: prefer named children
                    Image icon = UIResourceHelper.FindChildComponentByName<Image>(child,
                        new [] { "SlotIconImage", "Icon", "ItemIcon" });
	                    if (icon == null)
	                    {
	                        var imgs = child.GetComponentsInChildren<Image>(true);
	                        if (imgs.Length > 0) icon = imgs[imgs.Length - 1];
	                    }
	                    if (icon != null) icon.raycastTarget = false;
	                    _slotIcons[i] = icon;

                    // Count text: prefer named children
                    Text count = UIResourceHelper.FindChildComponentByName<Text>(child,
                        new [] { "SlotCountText", "CountText", "Qty", "Amount" });
	                    _slotCountTexts[i] = count;

	                    // Ensure bottom-right placement so user doesn't need to set anchors manually
	                    if (count != null)
	                    {
	                        var rt = count.GetComponent<RectTransform>();
	                        if (rt != null)
	                        {
	                            // Set anchors/pivot to bottom-right, with a small inset
	                            rt.anchorMin = new Vector2(1f, 0f);
	                            rt.anchorMax = new Vector2(1f, 0f);
	                            rt.pivot     = new Vector2(1f, 0f);
	                            if (rt.sizeDelta == Vector2.zero) rt.sizeDelta = new Vector2(25f, 20f);
	                            rt.anchoredPosition = new Vector2(-12f, 12f);
	                        }
	                        // Ensure readability
	                        if (count.GetComponent<Outline>() == null)
	                        {
	                            var ol = count.gameObject.AddComponent<Outline>();
	                            ol.effectColor = Color.black;
	                        }
	                    }
	                }
	            }

        }
	    }
