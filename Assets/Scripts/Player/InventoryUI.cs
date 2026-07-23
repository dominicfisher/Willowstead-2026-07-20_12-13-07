using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace Willowstead.Player
{
    /// <summary>
    /// Handles the visual display of the player's inventory using a premium grid-based UI panel.
    /// Programmatically constructs an overlay consisting of a 2x4 slot grid, matching the
    /// visual design and dark slate-brown aesthetic of the Hotbar HUD and Shop UI.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("Assets")]
        [Tooltip("The icon sprite to represent Carrot Seeds.")]
        [SerializeField] private Sprite _seedIcon;

        [Tooltip("The icon sprite to represent Carrots.")]
        [SerializeField] private Sprite _carrotIcon;

        private InventoryManager _inventory;
        private GameObject _canvasGo;
        private GameObject _panelGo;

        private Text _goldText;
        private Image[] _slotIcons;
        private Text[] _slotCountTexts;
        private bool _isOpen = false;

        public bool IsOpen => _isOpen;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_seedIcon == null) _seedIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/CarrotSeed.png");
            if (_carrotIcon == null) _carrotIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Carrot.png");
        }
#endif

        private void Start()
        {
            _inventory = GetComponent<InventoryManager>();
            if (_inventory == null) _inventory = FindAnyObjectByType<InventoryManager>();

            CreateUI();
            SetUIActive(false);
        }

        private void Update()
        {
            // Toggle inventory on Tab or I key using the new Input System
            if (Keyboard.current != null)
            {
                if (Keyboard.current.iKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    ToggleUI();
                }
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

                UpdateUIValues();
            }
        }

        private void SetUIActive(bool active)
        {
            if (_panelGo != null)
            {
                _panelGo.SetActive(active);
            }
        }

        private void UpdateUIValues()
        {
            if (_inventory == null) return;

            // 1. Update Gold Display Text
            int goldCount = _inventory.GetItemCount("Gold");
            if (_goldText != null) _goldText.text = $"Gold: {goldCount}";

            // 2. Fetch all inventory items (excluding Gold)
            var itemsDict = _inventory.GetInventoryData();
            System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>> displayItems =
                new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>();

            foreach (var kvp in itemsDict)
            {
                if (kvp.Key == "Gold") continue; // Gold has its own display
                if (kvp.Value > 0)
                {
                    displayItems.Add(kvp);
                }
            }

            // 3. Bind item data to the 2x4 grid slots
            int slotCount = _slotIcons != null ? _slotIcons.Length : 0;
            for (int i = 0; i < slotCount; i++)
            {
                if (_slotIcons[i] == null) continue;

                if (i < displayItems.Count)
                {
                    string itemName = displayItems[i].Key;
                    int quantity = displayItems[i].Value;

                    // Match item sprite
                    Sprite itemSprite = null;
                    if (itemName == "Carrot Seeds") itemSprite = _seedIcon;
                    else if (itemName == "Carrot") itemSprite = _carrotIcon;

                    _slotIcons[i].sprite = itemSprite;
                    _slotIcons[i].enabled = (itemSprite != null);

                    if (itemSprite == null)
                    {
                        // Fallback block color if texture isn't loaded/assigned
                        _slotIcons[i].color = new Color(0.6f, 0.5f, 0.4f, 0.85f);
                        _slotIcons[i].enabled = true;
                    }
                    else
                    {
                        _slotIcons[i].color = Color.white;
                    }

                    // Display quantity overlay
                    if (_slotCountTexts[i] != null)
                    {
                        _slotCountTexts[i].text = quantity.ToString();
                        _slotCountTexts[i].enabled = true;
                    }
                }
                else
                {
                    // Empty slot display
                    _slotIcons[i].sprite = null;
                    _slotIcons[i].enabled = false;
                    if (_slotCountTexts[i] != null)
                    {
                        _slotCountTexts[i].enabled = false;
                    }
                }
            }
        }

        private void CreateUI()
        {
            // Canvas Setup
            _canvasGo = GameObject.Find("HUDCanvas");
            if (_canvasGo == null)
            {
                _canvasGo = new GameObject("HUDCanvas");
                Canvas canvas = _canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvasGo.AddComponent<CanvasScaler>();
                _canvasGo.AddComponent<GraphicRaycaster>();
            }
            else
            {
                if (_canvasGo.GetComponent<GraphicRaycaster>() == null)
                {
                    _canvasGo.AddComponent<GraphicRaycaster>();
                }
            }

            // Ensure EventSystem exists in the scene so UI buttons can receive click events
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Load built-in assets directly for visual styling consistency
            Sprite roundedBg = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
            Sprite slotBg = Resources.GetBuiltinResource<Sprite>("UI/Skin/InputFieldBackground.psd");

            // Main Panel Parent
            _panelGo = new GameObject("InventoryPanel");
            _panelGo.transform.SetParent(_canvasGo.transform, false);

            RectTransform panelRect = _panelGo.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(380f, 280f);
            panelRect.anchoredPosition = Vector2.zero; // Center Screen

            // Background Panel sliced image
            GameObject bgGo = new GameObject("BackgroundPanel");
            bgGo.transform.SetParent(_panelGo.transform, false);
            Image bgImage = bgGo.AddComponent<Image>();
            bgImage.sprite = roundedBg;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.14f, 0.12f, 0.1f, 0.95f); // Slate dark brown matching hotbar

            RectTransform bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(380f, 280f);
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
            titleRect.anchoredPosition = new Vector2(0f, 115f);
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
            goldRect.anchoredPosition = new Vector2(0f, 80f);
            goldRect.sizeDelta = new Vector2(120f, 25f);

            // 8 Grid slots setup: 2 rows of 4 columns
            int columns = 4;
            int rows = 2;
            int totalSlots = columns * rows;

            _slotIcons = new Image[totalSlots];
            _slotCountTexts = new Text[totalSlots];

            float slotWidth = 60f;
            float slotHeight = 60f;
            float startX = -120f; // Centers 4 slots inside 380px panel
            float spacingX = 80f;
            
            float startY = 15f;   // Row 1 Y coordinate
            float spacingY = 75f;  // Row spacing

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

                    // Slot highlight/shadow border
                    GameObject shadowGo = new GameObject("SlotShadow");
                    shadowGo.transform.SetParent(slotRect, false);
                    Image shadowImg = shadowGo.AddComponent<Image>();
                    shadowImg.sprite = roundedBg;
                    shadowImg.type = Image.Type.Sliced;
                    shadowImg.color = new Color(0f, 0f, 0f, 0.35f);
                    RectTransform shadowRect = shadowGo.GetComponent<RectTransform>();
                    shadowRect.anchoredPosition = Vector2.zero;
                    shadowRect.sizeDelta = new Vector2(slotWidth + 8f, slotHeight + 8f);

                    // Slot inner panel background
                    GameObject innerGo = new GameObject("SlotInnerBackground");
                    innerGo.transform.SetParent(slotRect, false);
                    Image innerImg = innerGo.AddComponent<Image>();
                    innerImg.sprite = slotBg;
                    innerImg.type = Image.Type.Sliced;
                    innerImg.color = new Color(0.24f, 0.2f, 0.16f, 0.95f); // Slate interior
                    RectTransform innerRect = innerGo.GetComponent<RectTransform>();
                    innerRect.anchoredPosition = Vector2.zero;
                    innerRect.sizeDelta = new Vector2(slotWidth, slotHeight);

                    // Centered Item Icon Image
                    GameObject iconGo = new GameObject("SlotIconImage");
                    iconGo.transform.SetParent(slotRect, false);
                    _slotIcons[index] = iconGo.AddComponent<Image>();
                    _slotIcons[index].enabled = false; // Hidden when empty
                    RectTransform iconRect = iconGo.GetComponent<RectTransform>();
                    iconRect.anchoredPosition = Vector2.zero;
                    iconRect.sizeDelta = new Vector2(slotWidth - 16f, slotHeight - 16f);

                    // Bottom-Right Quantity Counter Text
                    GameObject countGo = new GameObject("SlotCountText");
                    countGo.transform.SetParent(slotRect, false);
                    _slotCountTexts[index] = countGo.AddComponent<Text>();
                    _slotCountTexts[index].font = legacyFont;
                    _slotCountTexts[index].fontSize = 14;
                    _slotCountTexts[index].fontStyle = FontStyle.Bold;
                    _slotCountTexts[index].alignment = TextAnchor.LowerRight;
                    _slotCountTexts[index].color = Color.white;
                    _slotCountTexts[index].enabled = false; // Hidden when empty

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
            footerRect.anchoredPosition = new Vector2(0f, -115f);
            footerRect.sizeDelta = new Vector2(250f, 25f);
        }
    }
}
