using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Willowstead.Input;

namespace Willowstead.Player
{
    /// <summary>
    /// Programmatically constructs and manages the Book-Themed Inventory UI (slots 8-23).
    /// Uses 'Item or quest book1.png' as the open book background with:
    ///   • Left Page: 4x4 backpack slot grid + gold display.
    ///   • Right Page: Interactive item inspection pane displaying item icon, title, type, and lore/description.
    /// Hovering or clicking over any slot immediately updates the inspection pane.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("Theme Icons (Optional Overrides)")]
        [SerializeField] private Sprite _hoeIcon;
        [SerializeField] private Sprite _wateringCanIcon;
        [SerializeField] private Sprite _seedIcon;
        [SerializeField] private Sprite _carrotIcon;
        [SerializeField] private Sprite _axeIcon;
        [SerializeField] private Sprite _logIcon;

        private InventoryManager _inventory;
        private GameObject _canvasGo;
        private GameObject _panelGo;

        private Text _goldText;
        private Image[] _slotIcons;
        private Text[] _slotCountTexts;
        private GameObject[] _slotObjects;
        private bool _isOpen = false;
        private Coroutine _bounceCoroutine;

        // ── Right Page Item Details ──
        private Image _inspectIcon;
        private Text _inspectTitle;
        private Text _inspectType;
        private Text _inspectDescription;
        private Text _inspectQuantity;

        public bool IsOpen => _isOpen;
        public static InventoryUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            _inventory = GetComponent<InventoryManager>();
            if (_inventory == null) _inventory = FindAnyObjectByType<InventoryManager>();

            CreateUI();
            SetUIActive(false);
        }

        private void Update()
        {
            bool inventoryPressed = Input.KeyRebindingManager.WasPressedThisFrame(Input.KeyAction.Inventory) ||
                                   (Keyboard.current != null && (Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.iKey.wasPressedThisFrame));

            if (inventoryPressed)
            {
                if (InputReader.BlockGameplayInput && !_isOpen)
                {
                    Debug.Log("[InputDebug] Inventory key pressed, but BlockGameplayInput is TRUE (blocked by UI/Console).");
                }
                else
                {
                    Debug.Log("[InputDebug] Inventory key pressed -> Toggling Inventory UI.");
                    ToggleUI();
                }
            }

            if (_isOpen)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CloseUI();
                    return;
                }

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
            if (_panelGo == null)
            {
                CreateUI();
            }

            _isOpen = !_isOpen;
            SetUIActive(_isOpen);
            Debug.Log($"[InputDebug] Inventory UI state changed. IsOpen: {_isOpen}");

            if (_isOpen)
            {
                ShopUI shopUI = FindAnyObjectByType<ShopUI>();
                if (shopUI != null && shopUI.IsOpen) shopUI.CloseUI();

                SkillsUI skillsUI = FindAnyObjectByType<SkillsUI>();
                if (skillsUI != null && skillsUI.IsOpen) skillsUI.CloseUI();

                RefreshUI();
                SelectFirstValidItemOrClear();

                if (_panelGo != null)
                {
                    _panelGo.transform.SetAsLastSibling();
                    if (_bounceCoroutine != null) StopCoroutine(_bounceCoroutine);
                    _bounceCoroutine = StartCoroutine(PlayBounceAnimation(_panelGo.transform));
                }
            }
        }

        private void SetUIActive(bool active)
        {
            if (_panelGo != null)
            {
                _panelGo.SetActive(active);
            }
            InputReader.BlockGameplayInput = active;
        }

        private void SelectFirstValidItemOrClear()
        {
            if (_inventory == null) return;

            for (int i = 0; i < 16; i++)
            {
                InventorySlot slot = _inventory.GetSlotItem(8 + i);
                if (slot != null && !slot.IsEmpty)
                {
                    InspectItem(slot.itemName, slot.quantity);
                    return;
                }
            }

            InspectItem(null, 0);
        }

        public void InspectItem(string itemName, int quantity)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                if (_inspectIcon != null) _inspectIcon.enabled = false;
                if (_inspectTitle != null) _inspectTitle.text = "Empty Slot";
                if (_inspectType != null) _inspectType.text = "Select an item to inspect";
                if (_inspectDescription != null) _inspectDescription.text = "Hover or click on any item in your backpack to view details, lore, and current quantities.";
                if (_inspectQuantity != null) _inspectQuantity.text = "";
                return;
            }

            Sprite icon = GetIconForItem(itemName);
            if (_inspectIcon != null)
            {
                _inspectIcon.sprite = icon;
                _inspectIcon.enabled = icon != null;
                _inspectIcon.color = icon != null ? Color.white : new Color(0.8f, 0.7f, 0.5f, 1f);
            }

            if (_inspectTitle != null) _inspectTitle.text = itemName;
            if (_inspectQuantity != null) _inspectQuantity.text = quantity > 1 ? $"Carrying: {quantity}" : (quantity == 1 ? "Carrying: 1" : "");

            string typeStr = "Item";
            string descStr = "A useful possession from your travels across Willowstead.";

            if (itemName == "Hoe")
            {
                typeStr = "Farm Tool";
                descStr = "Used to till fertile soil ready for planting seeds. Tilling soil awards Farming XP.";
            }
            else if (itemName == "Watering Can")
            {
                typeStr = "Farm Tool";
                descStr = "Provides fresh water to crops to ensure healthy growth. Watering soil awards Farming XP.";
            }
            else if (itemName == "Axe")
            {
                typeStr = "Tool & Weapon";
                descStr = "Chops timber from forest trees. Yields logs and awards Woodcutting XP.";
            }
            else if (itemName == "Fertilizer")
            {
                typeStr = "Soil Enrichment";
                descStr = "Enriches tilled soil. Prevents crops from rotting and yields sparkling bumper harvests.";
            }
            else if (itemName.Contains("Seed"))
            {
                typeStr = "Crop Seed";
                descStr = $"Plant into tilled soil to cultivate vibrant {itemName.Replace(" Seeds", "").Replace(" Seed", "")} crops.";
            }
            else if (itemName.Contains("Rotten") || itemName.Contains("Bad"))
            {
                typeStr = "Withered Harvest";
                descStr = "Unfertilized crop that spoiled in the soil. Can still be sold to the shop for salvage gold.";
            }
            else if (itemName == "Wood" || itemName == "Log")
            {
                typeStr = "Crafting Material";
                descStr = "Sturdy timber harvested from felled trees. Essential for construction and tool crafting.";
            }
            else if (itemName == "Gold" || itemName == "Coin")
            {
                typeStr = "Currency";
                descStr = "Shining gold coins accepted by merchants across the frontier.";
            }
            else
            {
                typeStr = "Harvested Crop";
                descStr = $"Freshly harvested {itemName}. Can be sold at the merchant shop for gold or saved for cooking recipes.";
            }

            if (_inspectType != null) _inspectType.text = typeStr;
            if (_inspectDescription != null) _inspectDescription.text = descStr;
        }

        public void RefreshUI()
        {
            if (_inventory == null || _slotIcons == null) return;

            int goldCount = _inventory.GetItemCount("Gold");
            if (_goldText != null) _goldText.text = $"{goldCount:N0}g";

            for (int i = 0; i < 16; i++)
            {
                if (i >= _slotIcons.Length || _slotIcons[i] == null) continue;

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
                    Sprite iconSprite = GetIconForItem(slotData.itemName);

                    _slotIcons[i].sprite = iconSprite;
                    _slotIcons[i].enabled = true;
                    _slotIcons[i].color = iconSprite != null ? Color.white : new Color(0.70f, 0.60f, 0.45f, 0.9f);

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

        private void CreateUI()
        {
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            _canvasGo = canvas != null ? canvas.gameObject : null;
            UIResourceHelper.EnsureEventSystem();

            if (_canvasGo == null) return;

            Transform existing = _canvasGo.transform.Find("InventoryPanel");
            if (existing != null) DestroyImmediate(existing.gameObject);

            Font font = UIResourceHelper.GetPixelFont();

            float panelW = 760f;
            float panelH = 540f;

            _panelGo = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
            _panelGo.transform.SetParent(_canvasGo.transform, false);

            RectTransform panelRect = (RectTransform)_panelGo.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(panelW, panelH);

            // ── Open Book Background (Item or quest book1) ──
            Image panelBg = _panelGo.GetComponent<Image>();
            panelBg.sprite = UIResourceHelper.GetItemOrQuestBookSprite(1);
            panelBg.type = Image.Type.Sliced;
            panelBg.color = Color.white;
            panelBg.raycastTarget = true;

            // Close 'X' Button on Top Right Corner
            GameObject closeGo = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
            closeGo.transform.SetParent(_panelGo.transform, false);
            RectTransform closeRt = (RectTransform)closeGo.transform;
            closeRt.anchorMin = new Vector2(1f, 1f); closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.sizeDelta = new Vector2(32f, 32f);
            closeRt.anchoredPosition = new Vector2(-28f, -22f);
            Image closeBg = closeGo.GetComponent<Image>();
            closeBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            closeBg.type = Image.Type.Sliced;
            closeBg.color = new Color(0.85f, 0.40f, 0.35f, 1f);

            GameObject closeLbl = new GameObject("Label", typeof(RectTransform));
            closeLbl.transform.SetParent(closeGo.transform, false);
            RectTransform cLblRt = (RectTransform)closeLbl.transform;
            cLblRt.anchorMin = Vector2.zero; cLblRt.anchorMax = Vector2.one;
            cLblRt.offsetMin = Vector2.zero; cLblRt.offsetMax = Vector2.zero;
            var cTxt = closeLbl.AddComponent<Text>();
            cTxt.font = font;
            cTxt.text = "✕";
            cTxt.fontSize = 15;
            cTxt.fontStyle = FontStyle.Bold;
            cTxt.color = Color.white;
            cTxt.alignment = TextAnchor.MiddleCenter;
            cTxt.raycastTarget = false;
            closeGo.GetComponent<Button>().onClick.AddListener(CloseUI);

            // ── LEFT PAGE: BACKPACK SLOTS CONTAINER (48px left, 28px right, 38px top, 74px bottom) ──
            GameObject leftPageGo = new GameObject("LeftPageContainer", typeof(RectTransform));
            leftPageGo.transform.SetParent(_panelGo.transform, false);
            RectTransform leftRt = (RectTransform)leftPageGo.transform;
            leftRt.anchorMin = new Vector2(0f, 0f); leftRt.anchorMax = new Vector2(0.5f, 1f);
            leftRt.offsetMin = new Vector2(48f, 74f); leftRt.offsetMax = new Vector2(-28f, -38f);

            // Left Page Header
            GameObject leftHeadGo = new GameObject("LeftHeader", typeof(RectTransform));
            leftHeadGo.transform.SetParent(leftPageGo.transform, false);
            RectTransform lHeadRt = (RectTransform)leftHeadGo.transform;
            lHeadRt.anchorMin = new Vector2(0f, 1f); lHeadRt.anchorMax = new Vector2(1f, 1f);
            lHeadRt.pivot = new Vector2(0.5f, 1f);
            lHeadRt.anchoredPosition = new Vector2(0f, 0f);
            lHeadRt.sizeDelta = new Vector2(0f, 28f);

            Text lHeadTxt = leftHeadGo.AddComponent<Text>();
            lHeadTxt.font = font;
            lHeadTxt.text = "BACKPACK POUCH";
            lHeadTxt.fontSize = 22;
            lHeadTxt.fontStyle = FontStyle.Bold;
            lHeadTxt.color = new Color(0.35f, 0.22f, 0.16f, 1f);
            lHeadTxt.alignment = TextAnchor.MiddleCenter;
            lHeadTxt.raycastTarget = false;

            // Gold Bar
            GameObject goldBannerGo = new GameObject("GoldBanner", typeof(RectTransform), typeof(Image));
            goldBannerGo.transform.SetParent(leftPageGo.transform, false);
            RectTransform goldBRt = (RectTransform)goldBannerGo.transform;
            goldBRt.anchorMin = new Vector2(0.5f, 1f); goldBRt.anchorMax = new Vector2(0.5f, 1f);
            goldBRt.pivot = new Vector2(0.5f, 1f);
            goldBRt.anchoredPosition = new Vector2(0f, -32f);
            goldBRt.sizeDelta = new Vector2(280f, 26f);
            Image goldBBg = goldBannerGo.GetComponent<Image>();
            goldBBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            goldBBg.type = Image.Type.Sliced;
            goldBBg.color = new Color(0.96f, 0.90f, 0.82f, 0.85f);

            GameObject goldTxtGo = new GameObject("GoldText", typeof(RectTransform));
            goldTxtGo.transform.SetParent(goldBannerGo.transform, false);
            RectTransform gTxtRt = (RectTransform)goldTxtGo.transform;
            gTxtRt.anchorMin = Vector2.zero; gTxtRt.anchorMax = Vector2.one;
            gTxtRt.offsetMin = new Vector2(10f, 0f); gTxtRt.offsetMax = new Vector2(-10f, 0f);
            _goldText = goldTxtGo.AddComponent<Text>();
            _goldText.font = font;
            _goldText.text = "0g";
            _goldText.fontSize = 17;
            _goldText.fontStyle = FontStyle.Bold;
            _goldText.alignment = TextAnchor.MiddleRight;
            _goldText.color = new Color(0.40f, 0.26f, 0.12f, 1f);

            GameObject goldLblGo = new GameObject("GoldLabel", typeof(RectTransform));
            goldLblGo.transform.SetParent(goldBannerGo.transform, false);
            RectTransform gLblRt = (RectTransform)goldLblGo.transform;
            gLblRt.anchorMin = Vector2.zero; gLblRt.anchorMax = Vector2.one;
            gLblRt.offsetMin = new Vector2(10f, 0f); gLblRt.offsetMax = new Vector2(-10f, 0f);
            var gLbl = goldLblGo.AddComponent<Text>();
            gLbl.font = font;
            gLbl.text = "💰 Wealth:";
            gLbl.fontSize = 16;
            gLbl.fontStyle = FontStyle.Bold;
            gLbl.alignment = TextAnchor.MiddleLeft;
            gLbl.color = new Color(0.40f, 0.26f, 0.12f, 1f);

            // 4x4 Grid of 16 Slots
            GameObject gridGo = new GameObject("SlotsGrid", typeof(RectTransform));
            gridGo.transform.SetParent(leftPageGo.transform, false);
            RectTransform gridRt = (RectTransform)gridGo.transform;
            gridRt.anchorMin = new Vector2(0.5f, 0f); gridRt.anchorMax = new Vector2(0.5f, 1f);
            gridRt.pivot = new Vector2(0.5f, 0.5f);
            gridRt.anchoredPosition = new Vector2(0f, -34f);
            gridRt.sizeDelta = new Vector2(280f, 280f);

            _slotIcons = new Image[16];
            _slotCountTexts = new Text[16];
            _slotObjects = new GameObject[16];

            float slotSize = 62f;
            float slotGap = 8f;
            float gridOriginX = -((4 * slotSize + 3 * slotGap) * 0.5f) + (slotSize * 0.5f);
            float gridOriginY = ((4 * slotSize + 3 * slotGap) * 0.5f) - (slotSize * 0.5f);

            for (int i = 0; i < 16; i++)
            {
                int r = i / 4;
                int c = i % 4;
                int slotIndex = i;

                GameObject slotGo = new GameObject($"InvSlot_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
                slotGo.transform.SetParent(gridGo.transform, false);
                RectTransform sRt = (RectTransform)slotGo.transform;
                sRt.sizeDelta = new Vector2(slotSize, slotSize);
                sRt.anchoredPosition = new Vector2(gridOriginX + c * (slotSize + slotGap), gridOriginY - r * (slotSize + slotGap));

                Image sBg = slotGo.GetComponent<Image>();
                sBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                sBg.type = Image.Type.Sliced;
                sBg.color = new Color(0.96f, 0.90f, 0.82f, 0.95f);

                // Slot Icon
                GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(slotGo.transform, false);
                RectTransform iRt = (RectTransform)iconGo.transform;
                iRt.anchorMin = new Vector2(0.12f, 0.12f); iRt.anchorMax = new Vector2(0.88f, 0.88f);
                iRt.offsetMin = Vector2.zero; iRt.offsetMax = Vector2.zero;
                Image iconImg = iconGo.GetComponent<Image>();
                iconImg.enabled = false;
                iconImg.raycastTarget = false;
                _slotIcons[i] = iconImg;

                // Slot Count
                GameObject cntGo = new GameObject("Count", typeof(RectTransform));
                cntGo.transform.SetParent(slotGo.transform, false);
                RectTransform cntRt = (RectTransform)cntGo.transform;
                cntRt.anchorMin = new Vector2(1f, 0f); cntRt.anchorMax = new Vector2(1f, 0f);
                cntRt.pivot = new Vector2(1f, 0f);
                cntRt.anchoredPosition = new Vector2(-4f, 2f);
                cntRt.sizeDelta = new Vector2(36f, 22f);
                Text cntTxt = cntGo.AddComponent<Text>();
                cntTxt.font = font;
                cntTxt.fontSize = 16;
                cntTxt.fontStyle = FontStyle.Bold;
                cntTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                cntTxt.verticalOverflow = VerticalWrapMode.Overflow;
                cntTxt.color = new Color(1f, 0.96f, 0.88f, 1f); // Crisp ivory
                cntTxt.alignment = TextAnchor.LowerRight;

                var outline = cntGo.AddComponent<Outline>();
                outline.effectColor = new Color(0.18f, 0.10f, 0.05f, 0.95f); // Rich dark contrast outline
                outline.effectDistance = new Vector2(1.2f, -1.2f);

                cntTxt.enabled = false;
                cntTxt.raycastTarget = false;
                _slotCountTexts[i] = cntTxt;

                // Drag and Hover triggers
                UIDragSlot dragSlot = slotGo.AddComponent<UIDragSlot>();
                dragSlot.slotIndex = 8 + i;

                Button btn = slotGo.GetComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    if (_inventory != null)
                    {
                        InventorySlot s = _inventory.GetSlotItem(8 + slotIndex);
                        if (s != null && !s.IsEmpty) InspectItem(s.itemName, s.quantity);
                        else InspectItem(null, 0);
                    }
                });

                EventTrigger trigger = slotGo.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener((data) =>
                {
                    if (_inventory != null)
                    {
                        InventorySlot s = _inventory.GetSlotItem(8 + slotIndex);
                        if (s != null && !s.IsEmpty) InspectItem(s.itemName, s.quantity);
                    }
                });
                trigger.triggers.Add(entry);

                _slotObjects[i] = slotGo;
            }

            // ── RIGHT PAGE: ITEM INSPECTION PANE (28px left, 48px right, 38px top, 74px bottom) ──
            GameObject rightPageGo = new GameObject("RightPageContainer", typeof(RectTransform));
            rightPageGo.transform.SetParent(_panelGo.transform, false);
            RectTransform rightRt = (RectTransform)rightPageGo.transform;
            rightRt.anchorMin = new Vector2(0.5f, 0f); rightRt.anchorMax = new Vector2(1f, 1f);
            rightRt.offsetMin = new Vector2(28f, 74f); rightRt.offsetMax = new Vector2(-48f, -38f);

            // Right Page Header
            GameObject rightHeadGo = new GameObject("RightHeader", typeof(RectTransform));
            rightHeadGo.transform.SetParent(rightPageGo.transform, false);
            RectTransform rHeadRt = (RectTransform)rightHeadGo.transform;
            rHeadRt.anchorMin = new Vector2(0f, 1f); rHeadRt.anchorMax = new Vector2(1f, 1f);
            rHeadRt.pivot = new Vector2(0.5f, 1f);
            rHeadRt.anchoredPosition = new Vector2(0f, 0f);
            rHeadRt.sizeDelta = new Vector2(0f, 28f);

            Text rHeadTxt = rightHeadGo.AddComponent<Text>();
            rHeadTxt.font = font;
            rHeadTxt.text = "ITEM DETAILS";
            rHeadTxt.fontSize = 22;
            rHeadTxt.fontStyle = FontStyle.Bold;
            rHeadTxt.color = new Color(0.35f, 0.22f, 0.16f, 1f);
            rHeadTxt.alignment = TextAnchor.MiddleCenter;
            rHeadTxt.raycastTarget = false;

            // Big Item Showcase Frame
            GameObject showcaseFrameGo = new GameObject("ShowcaseFrame", typeof(RectTransform), typeof(Image));
            showcaseFrameGo.transform.SetParent(rightPageGo.transform, false);
            RectTransform sfRt = (RectTransform)showcaseFrameGo.transform;
            sfRt.anchorMin = new Vector2(0.5f, 1f); sfRt.anchorMax = new Vector2(0.5f, 1f);
            sfRt.pivot = new Vector2(0.5f, 1f);
            sfRt.anchoredPosition = new Vector2(0f, -32f);
            sfRt.sizeDelta = new Vector2(74f, 74f);
            Image sfBg = showcaseFrameGo.GetComponent<Image>();
            sfBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            sfBg.type = Image.Type.Sliced;
            sfBg.color = new Color(0.96f, 0.90f, 0.82f, 0.95f);

            GameObject bigIconGo = new GameObject("ShowcaseIcon", typeof(RectTransform), typeof(Image));
            bigIconGo.transform.SetParent(showcaseFrameGo.transform, false);
            RectTransform biRt = (RectTransform)bigIconGo.transform;
            biRt.anchorMin = new Vector2(0.12f, 0.12f); biRt.anchorMax = new Vector2(0.88f, 0.88f);
            biRt.offsetMin = Vector2.zero; biRt.offsetMax = Vector2.zero;
            _inspectIcon = bigIconGo.GetComponent<Image>();
            _inspectIcon.preserveAspect = true;
            _inspectIcon.enabled = false;
            _inspectIcon.raycastTarget = false;

            // Title
            GameObject titleGo = new GameObject("InspectTitle", typeof(RectTransform));
            titleGo.transform.SetParent(rightPageGo.transform, false);
            RectTransform itRt = (RectTransform)titleGo.transform;
            itRt.anchorMin = new Vector2(0f, 1f); itRt.anchorMax = new Vector2(1f, 1f);
            itRt.pivot = new Vector2(0.5f, 1f);
            itRt.anchoredPosition = new Vector2(0f, -104f);
            itRt.sizeDelta = new Vector2(0f, 22f);
            _inspectTitle = titleGo.AddComponent<Text>();
            _inspectTitle.font = font;
            _inspectTitle.text = "Select an item";
            _inspectTitle.fontSize = 18;
            _inspectTitle.fontStyle = FontStyle.Bold;
            _inspectTitle.color = new Color(0.32f, 0.20f, 0.14f, 1f);
            _inspectTitle.alignment = TextAnchor.MiddleCenter;
            _inspectTitle.raycastTarget = false;

            // Type & Category
            GameObject typeGo = new GameObject("InspectType", typeof(RectTransform));
            typeGo.transform.SetParent(rightPageGo.transform, false);
            RectTransform typRt = (RectTransform)typeGo.transform;
            typRt.anchorMin = new Vector2(0f, 1f); typRt.anchorMax = new Vector2(1f, 1f);
            typRt.pivot = new Vector2(0.5f, 1f);
            typRt.anchoredPosition = new Vector2(0f, -126f);
            typRt.sizeDelta = new Vector2(0f, 18f);
            _inspectType = typeGo.AddComponent<Text>();
            _inspectType.font = font;
            _inspectType.text = "Item Category";
            _inspectType.fontSize = 14;
            _inspectType.fontStyle = FontStyle.Bold;
            _inspectType.color = new Color(0.65f, 0.42f, 0.18f, 1f);
            _inspectType.alignment = TextAnchor.MiddleCenter;
            _inspectType.raycastTarget = false;

            // Quantity Tag
            GameObject qtyGo = new GameObject("InspectQuantity", typeof(RectTransform));
            qtyGo.transform.SetParent(rightPageGo.transform, false);
            RectTransform qtyRt = (RectTransform)qtyGo.transform;
            qtyRt.anchorMin = new Vector2(0f, 1f); qtyRt.anchorMax = new Vector2(1f, 1f);
            qtyRt.pivot = new Vector2(0.5f, 1f);
            qtyRt.anchoredPosition = new Vector2(0f, -144f);
            qtyRt.sizeDelta = new Vector2(0f, 18f);
            _inspectQuantity = qtyGo.AddComponent<Text>();
            _inspectQuantity.font = font;
            _inspectQuantity.text = "";
            _inspectQuantity.fontSize = 13;
            _inspectQuantity.fontStyle = FontStyle.Bold;
            _inspectQuantity.color = new Color(0.40f, 0.32f, 0.28f, 1f);
            _inspectQuantity.alignment = TextAnchor.MiddleCenter;
            _inspectQuantity.raycastTarget = false;

            // Description Parchment Slate
            GameObject descParchGo = new GameObject("DescParchment", typeof(RectTransform), typeof(Image));
            descParchGo.transform.SetParent(rightPageGo.transform, false);
            RectTransform dpRt = (RectTransform)descParchGo.transform;
            dpRt.anchorMin = new Vector2(0f, 0f); dpRt.anchorMax = new Vector2(1f, 1f);
            dpRt.offsetMin = new Vector2(4f, 12f);
            dpRt.offsetMax = new Vector2(-4f, -168f);
            Image dpBg = descParchGo.GetComponent<Image>();
            dpBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            dpBg.type = Image.Type.Sliced;
            dpBg.color = new Color(0.96f, 0.90f, 0.82f, 0.90f);

            GameObject descGo = new GameObject("InspectDesc", typeof(RectTransform));
            descGo.transform.SetParent(descParchGo.transform, false);
            RectTransform descRt = (RectTransform)descGo.transform;
            descRt.anchorMin = Vector2.zero; descRt.anchorMax = Vector2.one;
            descRt.offsetMin = new Vector2(10f, 10f); descRt.offsetMax = new Vector2(-10f, -10f);
            _inspectDescription = descGo.AddComponent<Text>();
            _inspectDescription.font = font;
            _inspectDescription.text = "Hover or click on any item in your backpack to view its details, properties, and usage.";
            _inspectDescription.fontSize = 13;
            _inspectDescription.fontStyle = FontStyle.Normal;
            _inspectDescription.color = new Color(0.35f, 0.25f, 0.20f, 1f);
            _inspectDescription.alignment = TextAnchor.UpperLeft;
            _inspectDescription.lineSpacing = 1.15f;
            _inspectDescription.raycastTarget = false;

            SelectFirstValidItemOrClear();
            RefreshUI();
        }

        private IEnumerator PlayBounceAnimation(Transform target)
        {
            float duration = 0.22f;
            float elapsed = 0f;
            Vector3 startScale = new Vector3(0.75f, 0.75f, 1f);
            Vector3 targetScale = Vector3.one;

            target.localScale = startScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scaleT = t > 0.7f
                    ? Mathf.Lerp(1.08f, 1.0f, (t - 0.7f) / 0.3f)
                    : Mathf.Lerp(0.75f, 1.08f, t);
                target.localScale = new Vector3(scaleT, scaleT, 1f);
                yield return null;
            }

            target.localScale = targetScale;
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
