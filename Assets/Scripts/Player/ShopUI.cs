using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Willowstead.Input;

namespace Willowstead.Player
{
    /// <summary>
    /// Data-driven Merchant Shop UI using 'Item or quest book1.png' as an open ledger book:
    ///   • Left Page: Player Backpack Inventory (16 slots) with real-time stock & instant sell capability.
    ///   • Right Page: Merchant Catalogue with BUY and SELL tabs.
    ///       - BUY Tab: Buy seeds, wood, and fertilizer.
    ///       - SELL Tab: Direct bulk/item list to sell crops and rotten variations.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        [System.Serializable]
        public class ShopBuyItem
        {
            public string itemName;
            public Sprite icon;
            public int price;
        }

        [System.Serializable]
        public class ShopSellItem
        {
            public string itemName;
            public Sprite icon;
            public int price;
        }

        private List<ShopBuyItem> _buyItems = new List<ShopBuyItem>();
        private List<ShopSellItem> _sellItems = new List<ShopSellItem>();

        private void EnsureDefaultShopEntries()
        {
            _buyItems = new List<ShopBuyItem>
            {
                new ShopBuyItem { itemName = "Fertilizer", icon = UIResourceHelper.GetItemIconSprite("Fertilizer"), price = 10 },
                new ShopBuyItem { itemName = "Carrot Seeds", icon = UIResourceHelper.GetItemIconSprite("Carrot Seeds"), price = 15 },
                new ShopBuyItem { itemName = "Potato Seeds", icon = UIResourceHelper.GetItemIconSprite("Potato Seeds"), price = 20 },
                new ShopBuyItem { itemName = "Tomato Seeds", icon = UIResourceHelper.GetItemIconSprite("Tomato Seeds"), price = 25 },
                new ShopBuyItem { itemName = "Corn Seeds", icon = UIResourceHelper.GetItemIconSprite("Corn Seeds"), price = 30 },
                new ShopBuyItem { itemName = "Straw Seeds", icon = UIResourceHelper.GetItemIconSprite("Straw Seeds"), price = 10 },
                new ShopBuyItem { itemName = "Wood", icon = UIResourceHelper.GetItemIconSprite("Wood"), price = 15 }
            };

            _sellItems = new List<ShopSellItem>
            {
                new ShopSellItem { itemName = "Carrot", icon = UIResourceHelper.GetItemIconSprite("Carrot"), price = 25 },
                new ShopSellItem { itemName = "Potato", icon = UIResourceHelper.GetItemIconSprite("Potato"), price = 35 },
                new ShopSellItem { itemName = "Tomato", icon = UIResourceHelper.GetItemIconSprite("Tomato"), price = 40 },
                new ShopSellItem { itemName = "Corn", icon = UIResourceHelper.GetItemIconSprite("Corn"), price = 50 },
                new ShopSellItem { itemName = "Rotten Carrot", icon = UIResourceHelper.GetItemIconSprite("Rotten Carrot"), price = 5 },
                new ShopSellItem { itemName = "Rotten Potato", icon = UIResourceHelper.GetItemIconSprite("Rotten Potato"), price = 7 },
                new ShopSellItem { itemName = "Rotten Tomato", icon = UIResourceHelper.GetItemIconSprite("Rotten Tomato"), price = 8 },
                new ShopSellItem { itemName = "Rotten Corn", icon = UIResourceHelper.GetItemIconSprite("Rotten Corn"), price = 10 },
                new ShopSellItem { itemName = "Wood", icon = UIResourceHelper.GetItemIconSprite("Wood"), price = 8 },
                new ShopSellItem { itemName = "Fertilizer", icon = UIResourceHelper.GetItemIconSprite("Fertilizer"), price = 5 }
            };
        }

        [Header("Assets")]
        [SerializeField] private Sprite _coinSprite;

        private InventoryManager _inventory;
        private GameObject _canvasGo;
        private GameObject _panelGo;
        private bool _isOpen = false;
        private Coroutine _bounceCoroutine;

        private readonly List<RectTransform> _buyButtonRects = new List<RectTransform>();
        private readonly List<RectTransform> _sellCatalogButtonRects = new List<RectTransform>();
        private readonly List<Text> _sellCatalogOwnedTexts = new List<Text>();

        // Left Page Inventory Slots
        private Image[] _invSlotIcons = new Image[16];
        private Text[] _invSlotCountTexts = new Text[16];

        private Text _leftGoldText;
        private Text _hudGoldText;
        private RectTransform _hudGoldRect;
        private Coroutine _hudPulseCoroutine;
        private int _lastGoldAmount = -1;

        // Right Page Tab Switching
        private int _currentRightTab = 0; // 0 = Buy, 1 = Sell
        private GameObject _buyScrollViewGo;
        private GameObject _sellScrollViewGo;
        private Image _buyTabBtnImg;
        private Image _sellTabBtnImg;
        private Text _buyTabBtnTxt;
        private Text _sellTabBtnTxt;

        public bool IsOpen => _isOpen;
        public static ShopUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            _inventory = GetComponent<InventoryManager>();
            if (_inventory == null) _inventory = FindAnyObjectByType<InventoryManager>();

            if (_coinSprite == null) _coinSprite = UIResourceHelper.GetItemIconSprite("Gold Coin");

            EnsureDefaultShopEntries();
            CreateShopUI();
            SetUIActive(false);
        }

        private void Update()
        {
            bool shopPressed = Input.KeyRebindingManager.WasPressedThisFrame(Input.KeyAction.Shop) ||
                               (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame);

            if (!InputReader.BlockGameplayInput && shopPressed)
            {
                Debug.Log("[InputDebug] Shop key pressed -> Toggling Shop UI.");
                ToggleUI();
            }

            if (_isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseUI();
            }

            UpdateHudGold();
            if (_isOpen)
            {
                RefreshLeftInventoryPage();
                RefreshSellCatalogOwnedCounts();
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
                CreateShopUI();
            }

            _isOpen = !_isOpen;
            SetUIActive(_isOpen);

            if (_isOpen)
            {
                InventoryUI invUI = FindAnyObjectByType<InventoryUI>();
                if (invUI != null && invUI.IsOpen) invUI.CloseUI();

                SkillsUI skillsUI = FindAnyObjectByType<SkillsUI>();
                if (skillsUI != null && skillsUI.IsOpen) skillsUI.CloseUI();

                SwitchRightTab(0);
                RefreshLeftInventoryPage();
                RefreshSellCatalogOwnedCounts();

                if (Willowstead.World.ObjectiveManager.Instance != null)
                {
                    Willowstead.World.ObjectiveManager.Instance.ReportProgress(Willowstead.World.ObjectiveId.VisitShop, 1);
                }

                if (_panelGo != null)
                {
                    _panelGo.transform.SetAsLastSibling();
                    if (_bounceCoroutine != null) StopCoroutine(_bounceCoroutine);
                    _bounceCoroutine = StartCoroutine(PlayBounceAnimation(_panelGo.transform));
                }
            }
        }

        private IEnumerator PlayBounceAnimation(Transform panelTransform)
        {
            float duration = 0.22f;
            float elapsed = 0f;
            panelTransform.localScale = new Vector3(0.75f, 0.75f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = t > 0.7f
                    ? Mathf.Lerp(1.08f, 1.0f, (t - 0.7f) / 0.3f)
                    : Mathf.Lerp(0.75f, 1.08f, t);
                panelTransform.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            panelTransform.localScale = Vector3.one;
            _bounceCoroutine = null;
        }

        private void SetUIActive(bool active)
        {
            if (_panelGo != null) _panelGo.SetActive(active);
            InputReader.BlockGameplayInput = active;
        }

        private void UpdateHudGold()
        {
            if (_inventory == null) return;
            int gold = _inventory.GetItemCount("Gold");
            if (gold == _lastGoldAmount) return;

            _lastGoldAmount = gold;
            if (_hudGoldText != null) _hudGoldText.text = $"{gold:N0}";
            if (_leftGoldText != null) _leftGoldText.text = $"{gold:N0}g";

            if (_hudPulseCoroutine != null) StopCoroutine(_hudPulseCoroutine);
            _hudPulseCoroutine = StartCoroutine(PulseHudGold());
        }

        private IEnumerator PulseHudGold()
        {
            float duration = 0.22f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float s = 1f + Mathf.Sin((elapsed / duration) * Mathf.PI) * 0.25f;
                if (_hudGoldRect != null) _hudGoldRect.localScale = Vector3.one * s;
                yield return null;
            }

            if (_hudGoldRect != null) _hudGoldRect.localScale = Vector3.one;
            _hudPulseCoroutine = null;
        }

        public void SwitchRightTab(int tabIndex)
        {
            _currentRightTab = tabIndex;
            if (_buyScrollViewGo != null) _buyScrollViewGo.SetActive(tabIndex == 0);
            if (_sellScrollViewGo != null) _sellScrollViewGo.SetActive(tabIndex == 1);

            Color activeBg = new Color(0.96f, 0.90f, 0.82f, 1f);
            Color inactiveBg = new Color(0.80f, 0.70f, 0.60f, 0.85f);
            Color activeTxt = new Color(0.32f, 0.20f, 0.14f, 1f);
            Color inactiveTxt = new Color(0.48f, 0.38f, 0.30f, 1f);

            if (_buyTabBtnImg != null) _buyTabBtnImg.color = tabIndex == 0 ? activeBg : inactiveBg;
            if (_sellTabBtnImg != null) _sellTabBtnImg.color = tabIndex == 1 ? activeBg : inactiveBg;
            if (_buyTabBtnTxt != null) _buyTabBtnTxt.color = tabIndex == 0 ? activeTxt : inactiveTxt;
            if (_sellTabBtnTxt != null) _sellTabBtnTxt.color = tabIndex == 1 ? activeTxt : inactiveTxt;

            RefreshSellCatalogOwnedCounts();
        }

        public void RefreshLeftInventoryPage()
        {
            if (_inventory == null || _invSlotIcons == null) return;

            for (int i = 0; i < 16; i++)
            {
                if (i >= _invSlotIcons.Length || _invSlotIcons[i] == null) continue;

                int slotIndex = 8 + i;
                InventorySlot slot = _inventory.GetSlotItem(slotIndex);

                if (slot == null || slot.IsEmpty)
                {
                    _invSlotIcons[i].sprite = null;
                    _invSlotIcons[i].enabled = false;
                    if (_invSlotCountTexts[i] != null) _invSlotCountTexts[i].enabled = false;
                }
                else
                {
                    Sprite icon = UIResourceHelper.GetItemIconSprite(slot.itemName);
                    _invSlotIcons[i].sprite = icon;
                    _invSlotIcons[i].enabled = true;
                    _invSlotIcons[i].color = icon != null ? Color.white : new Color(0.70f, 0.60f, 0.45f, 0.9f);

                    if (_invSlotCountTexts[i] != null)
                    {
                        if (slot.itemName == "Hoe" || slot.itemName == "Watering Can" || slot.itemName == "Axe")
                        {
                            _invSlotCountTexts[i].enabled = false;
                        }
                        else
                        {
                            _invSlotCountTexts[i].text = slot.quantity.ToString();
                            _invSlotCountTexts[i].enabled = true;
                        }
                    }
                }
            }
        }

        public void RefreshSellCatalogOwnedCounts()
        {
            if (_inventory == null) return;

            for (int i = 0; i < _sellItems.Count; i++)
            {
                if (i < _sellCatalogOwnedTexts.Count && _sellCatalogOwnedTexts[i] != null)
                {
                    int count = _inventory.GetItemCount(_sellItems[i].itemName);
                    _sellCatalogOwnedTexts[i].text = $"Have: {count}";
                    _sellCatalogOwnedTexts[i].color = count > 0 ? new Color(0.18f, 0.42f, 0.15f, 1f) : new Color(0.55f, 0.45f, 0.38f, 1f);
                }
            }
        }

        private void BuyItem(ShopBuyItem item, RectTransform buyBtnRect)
        {
            if (_inventory == null) return;

            if (item.price > 0)
            {
                if (_inventory.GetItemCount("Gold") < item.price)
                {
                    Debug.LogWarning($"[ShopUI] Not enough gold to buy {item.itemName} (need {item.price}g).");
                    StartCoroutine(ShakeAndFlashRed(buyBtnRect, buyBtnRect.GetComponent<Image>(),
                        new Color(0.96f, 0.90f, 0.82f, 0.95f)));
                    return;
                }

                _inventory.RemoveItem("Gold", item.price);
            }

            StartCoroutine(ScalePopAnimation(buyBtnRect));
            SpawnSparkleBurst(buyBtnRect.position);

            Vector3 startPos = GetWorldPos(buyBtnRect);
            Sprite icon = item.icon != null ? item.icon : _coinSprite;

            World.FlyingItemAnimation.Spawn(icon, startPos, null, () =>
            {
                _inventory.AddItem(item.itemName, 1);
                RefreshLeftInventoryPage();
                RefreshSellCatalogOwnedCounts();
            });
        }

        private void SellItemFromBackpackSlot(int slotIndex, RectTransform slotRect)
        {
            if (_inventory == null) return;

            InventorySlot slot = _inventory.GetSlotItem(slotIndex);
            if (slot == null || slot.IsEmpty) return;

            string itemName = slot.itemName;
            if (itemName == "Hoe" || itemName == "Watering Can" || itemName == "Axe")
            {
                // Protect tools from accidental sales
                StartCoroutine(ShakeAndFlashRed(slotRect, slotRect.GetComponent<Image>(), new Color(0.96f, 0.90f, 0.82f, 0.95f)));
                return;
            }

            int sellPrice = GetSellPriceForItem(itemName);
            _inventory.RemoveItemFromSlot(slotIndex, 1);

            StartCoroutine(ScalePopAnimation(slotRect));
            Vector3 startPos = GetWorldPos(slotRect);
            Color goldColor = new Color(1f, 0.82f, 0f, 1f);

            World.FlyingItemAnimation.Spawn(_coinSprite, startPos, _hudGoldRect, () =>
            {
                _inventory.AddItem("Gold", sellPrice);
                RefreshLeftInventoryPage();
                RefreshSellCatalogOwnedCounts();
            }, goldColor);
        }

        private void SellItemFromCatalogue(ShopSellItem item, RectTransform sellCardRt)
        {
            if (_inventory == null) return;

            if (_inventory.GetItemCount(item.itemName) <= 0)
            {
                StartCoroutine(ShakeAndFlashRed(sellCardRt, sellCardRt.GetComponent<Image>(), new Color(0.96f, 0.90f, 0.82f, 0.95f)));
                return;
            }

            _inventory.RemoveItem(item.itemName, 1);
            StartCoroutine(ScalePopAnimation(sellCardRt));
            Vector3 startPos = GetWorldPos(sellCardRt);
            Color goldColor = new Color(1f, 0.82f, 0f, 1f);

            World.FlyingItemAnimation.Spawn(_coinSprite, startPos, _hudGoldRect, () =>
            {
                _inventory.AddItem("Gold", item.price);
                RefreshLeftInventoryPage();
                RefreshSellCatalogOwnedCounts();
            }, goldColor);
        }

        private int GetSellPriceForItem(string itemName)
        {
            foreach (var s in _sellItems)
            {
                if (string.Equals(s.itemName, itemName, StringComparison.OrdinalIgnoreCase))
                    return s.price;
            }

            if (itemName.Contains("Seed")) return 8;
            if (itemName.Contains("Wood") || itemName.Contains("Log")) return 8;
            if (itemName.Contains("Rotten") || itemName.Contains("Bad")) return 5;
            return 15;
        }

        private Vector3 GetWorldPos(RectTransform rect)
        {
            if (rect == null) return Vector3.zero;
            Camera cam = Camera.main;
            if (cam == null) return rect.position;
            Vector3 sp = RectTransformUtility.WorldToScreenPoint(null, rect.position);
            sp.z = -cam.transform.position.z;
            return cam.ScreenToWorldPoint(sp);
        }

        private IEnumerator ScalePopAnimation(RectTransform rt)
        {
            if (rt == null) yield break;
            Vector3 original = Vector3.one;
            float popDuration = 0.14f;
            float dropDuration = 0.10f;

            for (float t = 0f; t < popDuration; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                rt.localScale = original * Mathf.Lerp(1f, 1.25f, t / popDuration);
                yield return null;
            }
            for (float t = 0f; t < dropDuration; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                rt.localScale = original * Mathf.Lerp(1.25f, 1f, t / dropDuration);
                yield return null;
            }

            if (rt != null) rt.localScale = original;
        }

        private IEnumerator ShakeAndFlashRed(RectTransform rt, Image img, Color originalColor)
        {
            Vector2 originalPos = rt.anchoredPosition;
            if (img != null) img.color = new Color(0.95f, 0.40f, 0.40f, 1f);

            float duration = 0.35f;
            float elapsed = 0f;
            float freq = 35f;
            float amp = 8f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rt.anchoredPosition = originalPos + new Vector2(Mathf.Sin(elapsed * freq) * amp * (1f - t), 0f);
                yield return null;
            }

            rt.anchoredPosition = originalPos;
            if (img != null) img.color = originalColor;
        }

        private void SpawnSparkleBurst(Vector2 screenPos)
        {
            if (_canvasGo == null) return;
            RectTransform canvasRect = _canvasGo.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out Vector2 localPos);

            for (int i = 0; i < 5; i++)
            {
                GameObject sparkleGo = new GameObject("Sparkle", typeof(RectTransform), typeof(Image));
                sparkleGo.transform.SetParent(_canvasGo.transform, false);
                Image img = sparkleGo.GetComponent<Image>();
                img.sprite = UIResourceHelper.GetSparkleStarSprite();
                img.color = new Color(1f, 0.88f, 0.35f, 1f);

                RectTransform sparkleRt = sparkleGo.GetComponent<RectTransform>();
                sparkleRt.sizeDelta = new Vector2(16f, 16f);
                sparkleRt.anchoredPosition = localPos;

                float angle = i * 72f * Mathf.Deg2Rad + 0.30f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                StartCoroutine(AnimateSparkle(sparkleRt, img, dir));
            }
        }

        private IEnumerator AnimateSparkle(RectTransform rt, Image img, Vector2 dir)
        {
            float duration = 0.45f;
            float elapsed = 0f;
            Vector2 startPos = rt.anchoredPosition;
            Color startColor = img.color;
            float dist = 55f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rt.anchoredPosition = startPos + dir * (dist * t);
                img.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
                yield return null;
            }

            Destroy(rt.gameObject);
        }

        private void CreateGoldHud(Font font)
        {
            if (_canvasGo == null) return;
            Transform existing = _canvasGo.transform.Find("HUDGoldContainer");
            if (existing != null) DestroyImmediate(existing.gameObject);

            GameObject goldHudGo = new GameObject("HUDGoldContainer", typeof(RectTransform), typeof(Image));
            goldHudGo.transform.SetParent(_canvasGo.transform, false);
            _hudGoldRect = (RectTransform)goldHudGo.transform;
            _hudGoldRect.anchorMin = new Vector2(1f, 1f);
            _hudGoldRect.anchorMax = new Vector2(1f, 1f);
            _hudGoldRect.pivot = new Vector2(1f, 1f);
            _hudGoldRect.anchoredPosition = new Vector2(-16f, -16f);
            _hudGoldRect.sizeDelta = new Vector2(120f, 38f);

            Image hudBg = goldHudGo.GetComponent<Image>();
            hudBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            hudBg.type = Image.Type.Sliced;
            hudBg.color = new Color(0.96f, 0.90f, 0.82f, 0.95f);

            GameObject coinGo = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
            coinGo.transform.SetParent(goldHudGo.transform, false);
            RectTransform coinRt = (RectTransform)coinGo.transform;
            coinRt.anchorMin = new Vector2(0f, 0.5f); coinRt.anchorMax = new Vector2(0f, 0.5f);
            coinRt.pivot = new Vector2(0f, 0.5f);
            coinRt.anchoredPosition = new Vector2(10f, 0f);
            coinRt.sizeDelta = new Vector2(22f, 22f);
            Image coinImg = coinGo.GetComponent<Image>();
            coinImg.sprite = _coinSprite;
            coinImg.preserveAspect = true;

            GameObject textGo = new GameObject("HUDGoldText", typeof(RectTransform));
            textGo.transform.SetParent(goldHudGo.transform, false);
            RectTransform textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(36f, 0f); textRt.offsetMax = new Vector2(-10f, 0f);
            _hudGoldText = textGo.AddComponent<Text>();
            _hudGoldText.font = font;
            _hudGoldText.text = "0";
            _hudGoldText.fontSize = 15;
            _hudGoldText.fontStyle = FontStyle.Bold;
            _hudGoldText.alignment = TextAnchor.MiddleRight;
            _hudGoldText.color = new Color(0.35f, 0.22f, 0.16f, 1f);
        }

        private void CreateShopUI()
        {
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            _canvasGo = canvas != null ? canvas.gameObject : null;
            UIResourceHelper.EnsureEventSystem();

            if (_canvasGo == null) return;

            Font font = UIResourceHelper.GetPixelFont();

            CreateGoldHud(font);

            Transform existing = _canvasGo.transform.Find("ShopPanel");
            if (existing != null) DestroyImmediate(existing.gameObject);

            float panelW = 760f;
            float panelH = 540f;

            _panelGo = new GameObject("ShopPanel", typeof(RectTransform), typeof(Image));
            _panelGo.transform.SetParent(_canvasGo.transform, false);
            RectTransform panelRect = (RectTransform)_panelGo.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(panelW, panelH);
            panelRect.anchoredPosition = Vector2.zero;

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

            // ── LEFT PAGE: YOUR INVENTORY (48px left, 28px right, 38px top, 74px bottom) ──
            GameObject leftPageGo = new GameObject("LeftPageContainer", typeof(RectTransform));
            leftPageGo.transform.SetParent(_panelGo.transform, false);
            RectTransform leftRt = (RectTransform)leftPageGo.transform;
            leftRt.anchorMin = new Vector2(0f, 0f); leftRt.anchorMax = new Vector2(0.5f, 1f);
            leftRt.offsetMin = new Vector2(48f, 74f); leftRt.offsetMax = new Vector2(-28f, -38f);

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
            _leftGoldText = goldTxtGo.AddComponent<Text>();
            _leftGoldText.font = font;
            _leftGoldText.text = "0g";
            _leftGoldText.fontSize = 17;
            _leftGoldText.fontStyle = FontStyle.Bold;
            _leftGoldText.alignment = TextAnchor.MiddleRight;
            _leftGoldText.color = new Color(0.40f, 0.26f, 0.12f, 1f);

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

            // 4x4 Backpack Grid inside Left Page
            GameObject gridGo = new GameObject("InvGrid", typeof(RectTransform));
            gridGo.transform.SetParent(leftPageGo.transform, false);
            RectTransform gridRt = (RectTransform)gridGo.transform;
            gridRt.anchorMin = new Vector2(0.5f, 0f); gridRt.anchorMax = new Vector2(0.5f, 1f);
            gridRt.pivot = new Vector2(0.5f, 0.5f);
            gridRt.anchoredPosition = new Vector2(0f, -34f);
            gridRt.sizeDelta = new Vector2(280f, 280f);

            _invSlotIcons = new Image[16];
            _invSlotCountTexts = new Text[16];

            float slotSize = 62f;
            float slotGap = 8f;
            float gridOriginX = -((4 * slotSize + 3 * slotGap) * 0.5f) + (slotSize * 0.5f);
            float gridOriginY = ((4 * slotSize + 3 * slotGap) * 0.5f) - (slotSize * 0.5f);

            for (int i = 0; i < 16; i++)
            {
                int r = i / 4;
                int c = i % 4;
                int slotIndex = 8 + i;

                GameObject slotGo = new GameObject($"ShopInvSlot_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
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
                _invSlotIcons[i] = iconImg;

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
                _invSlotCountTexts[i] = cntTxt;

                UIDragSlot dragSlot = slotGo.AddComponent<UIDragSlot>();
                dragSlot.slotIndex = slotIndex;

                slotGo.GetComponent<Button>().onClick.AddListener(() => SellItemFromBackpackSlot(slotIndex, sRt));
            }

            // ── RIGHT PAGE: MERCHANT CATALOGUE (28px left, 48px right, 38px top, 74px bottom) ──
            GameObject rightPageGo = new GameObject("RightPageContainer", typeof(RectTransform));
            rightPageGo.transform.SetParent(_panelGo.transform, false);
            RectTransform rightRt = (RectTransform)rightPageGo.transform;
            rightRt.anchorMin = new Vector2(0.5f, 0f); rightRt.anchorMax = new Vector2(1f, 1f);
            rightRt.offsetMin = new Vector2(28f, 74f); rightRt.offsetMax = new Vector2(-48f, -38f);

            // Right Page Header: BUY / SELL TABS
            GameObject tabsHeaderGo = new GameObject("TabsHeader", typeof(RectTransform));
            tabsHeaderGo.transform.SetParent(rightPageGo.transform, false);
            RectTransform thRt = (RectTransform)tabsHeaderGo.transform;
            thRt.anchorMin = new Vector2(0.5f, 1f); thRt.anchorMax = new Vector2(0.5f, 1f);
            thRt.pivot = new Vector2(0.5f, 1f);
            thRt.anchoredPosition = new Vector2(0f, 0f);
            thRt.sizeDelta = new Vector2(280f, 28f);

            // Buy Tab Button
            GameObject buyTabBtnGo = new GameObject("BuyTabBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
            buyTabBtnGo.transform.SetParent(tabsHeaderGo.transform, false);
            RectTransform btbRt = (RectTransform)buyTabBtnGo.transform;
            btbRt.anchorMin = new Vector2(0f, 0f); btbRt.anchorMax = new Vector2(0.5f, 1f);
            btbRt.offsetMin = new Vector2(0f, 0f); btbRt.offsetMax = new Vector2(-4f, 0f);
            _buyTabBtnImg = buyTabBtnGo.GetComponent<Image>();
            _buyTabBtnImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            _buyTabBtnImg.type = Image.Type.Sliced;
            _buyTabBtnImg.color = new Color(0.96f, 0.90f, 0.82f, 1f);

            GameObject buyTabTxtGo = new GameObject("Text", typeof(RectTransform));
            buyTabTxtGo.transform.SetParent(buyTabBtnGo.transform, false);
            RectTransform bttRt = (RectTransform)buyTabTxtGo.transform;
            bttRt.anchorMin = Vector2.zero; bttRt.anchorMax = Vector2.one;
            bttRt.offsetMin = Vector2.zero; bttRt.offsetMax = Vector2.zero;
            _buyTabBtnTxt = buyTabTxtGo.AddComponent<Text>();
            _buyTabBtnTxt.font = font;
            _buyTabBtnTxt.text = "BUY SEEDS";
            _buyTabBtnTxt.fontSize = 16;
            _buyTabBtnTxt.fontStyle = FontStyle.Bold;
            _buyTabBtnTxt.color = new Color(0.32f, 0.20f, 0.14f, 1f);
            _buyTabBtnTxt.alignment = TextAnchor.MiddleCenter;
            _buyTabBtnTxt.raycastTarget = false;
            buyTabBtnGo.GetComponent<Button>().onClick.AddListener(() => SwitchRightTab(0));

            // Sell Tab Button
            GameObject sellTabBtnGo = new GameObject("SellTabBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
            sellTabBtnGo.transform.SetParent(tabsHeaderGo.transform, false);
            RectTransform stbRt = (RectTransform)sellTabBtnGo.transform;
            stbRt.anchorMin = new Vector2(0.5f, 0f); stbRt.anchorMax = new Vector2(1f, 1f);
            stbRt.offsetMin = new Vector2(4f, 0f); stbRt.offsetMax = new Vector2(0f, 0f);
            _sellTabBtnImg = sellTabBtnGo.GetComponent<Image>();
            _sellTabBtnImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            _sellTabBtnImg.type = Image.Type.Sliced;
            _sellTabBtnImg.color = new Color(0.80f, 0.70f, 0.60f, 0.85f);

            GameObject sellTabTxtGo = new GameObject("Text", typeof(RectTransform));
            sellTabTxtGo.transform.SetParent(sellTabBtnGo.transform, false);
            RectTransform sttRt = (RectTransform)sellTabTxtGo.transform;
            sttRt.anchorMin = Vector2.zero; sttRt.anchorMax = Vector2.one;
            sttRt.offsetMin = Vector2.zero; sttRt.offsetMax = Vector2.zero;
            _sellTabBtnTxt = sellTabTxtGo.AddComponent<Text>();
            _sellTabBtnTxt.font = font;
            _sellTabBtnTxt.text = "SELL CROPS";
            _sellTabBtnTxt.fontSize = 16;
            _sellTabBtnTxt.fontStyle = FontStyle.Bold;
            _sellTabBtnTxt.color = new Color(0.48f, 0.38f, 0.30f, 1f);
            _sellTabBtnTxt.alignment = TextAnchor.MiddleCenter;
            _sellTabBtnTxt.raycastTarget = false;
            sellTabBtnGo.GetComponent<Button>().onClick.AddListener(() => SwitchRightTab(1));

            // ── Scroll View 1: BUY ITEMS ──────────────────────────────────────
            _buyScrollViewGo = new GameObject("BuyScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            _buyScrollViewGo.transform.SetParent(rightPageGo.transform, false);
            RectTransform buyScrollRt = (RectTransform)_buyScrollViewGo.transform;
            buyScrollRt.anchorMin = new Vector2(0f, 0f); buyScrollRt.anchorMax = new Vector2(1f, 1f);
            buyScrollRt.offsetMin = new Vector2(0f, 0f); buyScrollRt.offsetMax = new Vector2(0f, -34f);

            Image bsMask = _buyScrollViewGo.GetComponent<Image>();
            bsMask.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            bsMask.type = Image.Type.Sliced;
            bsMask.color = new Color(0.48f, 0.35f, 0.32f, 0.01f);

            Mask buyMask = _buyScrollViewGo.GetComponent<Mask>();
            buyMask.showMaskGraphic = false;

            ScrollRect bScroll = _buyScrollViewGo.GetComponent<ScrollRect>();
            bScroll.horizontal = false;
            bScroll.vertical = true;
            bScroll.scrollSensitivity = 25f;
            bScroll.movementType = ScrollRect.MovementType.Clamped;

            int buyColumns = 2;
            float buyCardW = 132f;
            float buyCardH = 92f;
            float buyGapX = 12f;
            float buyGapY = 10f;
            int totalBuyRows = Mathf.CeilToInt((float)_buyItems.Count / buyColumns);
            float buyTotalContentH = Mathf.Max(370f, (totalBuyRows * (buyCardH + buyGapY)) + 16f);

            GameObject buyContentGo = new GameObject("BuyContent", typeof(RectTransform));
            buyContentGo.transform.SetParent(_buyScrollViewGo.transform, false);
            RectTransform buyContRt = (RectTransform)buyContentGo.transform;
            buyContRt.anchorMin = new Vector2(0f, 1f); buyContRt.anchorMax = new Vector2(1f, 1f);
            buyContRt.pivot = new Vector2(0.5f, 1f);
            buyContRt.anchoredPosition = Vector2.zero;
            buyContRt.sizeDelta = new Vector2(0f, buyTotalContentH);
            bScroll.content = buyContRt;

            _buyButtonRects.Clear();

            for (int i = 0; i < _buyItems.Count; i++)
            {
                ShopBuyItem buyItem = _buyItems[i];
                int col = i % buyColumns;
                int row = i / buyColumns;

                float posX = (col == 0 ? -1f : 1f) * ((buyCardW * 0.5f) + (buyGapX * 0.5f));
                float posY = -8f - (row * (buyCardH + buyGapY)) - (buyCardH * 0.5f);

                GameObject bCardGo = new GameObject($"BuyCard_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
                bCardGo.transform.SetParent(buyContentGo.transform, false);
                RectTransform bCardRt = (RectTransform)bCardGo.transform;
                bCardRt.anchorMin = new Vector2(0.5f, 1f); bCardRt.anchorMax = new Vector2(0.5f, 1f);
                bCardRt.pivot = new Vector2(0.5f, 0.5f);
                bCardRt.sizeDelta = new Vector2(buyCardW, buyCardH);
                bCardRt.anchoredPosition = new Vector2(posX, posY);

                Image bCardBg = bCardGo.GetComponent<Image>();
                bCardBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                bCardBg.type = Image.Type.Sliced;
                bCardBg.color = new Color(0.96f, 0.90f, 0.82f, 0.95f);

                // Icon
                GameObject bIconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                bIconGo.transform.SetParent(bCardGo.transform, false);
                RectTransform biRt = (RectTransform)bIconGo.transform;
                biRt.anchorMin = new Vector2(0.5f, 1f); biRt.anchorMax = new Vector2(0.5f, 1f);
                biRt.pivot = new Vector2(0.5f, 1f);
                biRt.anchoredPosition = new Vector2(0f, -6f);
                biRt.sizeDelta = new Vector2(38f, 38f);
                Image biImg = bIconGo.GetComponent<Image>();
                biImg.sprite = buyItem.icon != null ? buyItem.icon : _coinSprite;
                biImg.preserveAspect = true;
                biImg.color = biImg.sprite != null ? Color.white : new Color(0.6f, 0.5f, 0.3f, 0.6f);
                biImg.raycastTarget = false;

                // Name
                GameObject bNameGo = new GameObject("Name", typeof(RectTransform));
                bNameGo.transform.SetParent(bCardGo.transform, false);
                RectTransform bnRt = (RectTransform)bNameGo.transform;
                bnRt.anchorMin = new Vector2(0f, 1f); bnRt.anchorMax = new Vector2(1f, 1f);
                bnRt.pivot = new Vector2(0.5f, 1f);
                bnRt.anchoredPosition = new Vector2(0f, -44f);
                bnRt.sizeDelta = new Vector2(0f, 20f);
                Text bnTxt = bNameGo.AddComponent<Text>();
                bnTxt.font = font;
                bnTxt.text = buyItem.itemName;
                bnTxt.fontSize = 11;
                bnTxt.fontStyle = FontStyle.Bold;
                bnTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                bnTxt.verticalOverflow = VerticalWrapMode.Overflow;
                bnTxt.color = new Color(0.32f, 0.22f, 0.16f, 1f);
                bnTxt.alignment = TextAnchor.MiddleCenter;
                bnTxt.raycastTarget = false;

                // Price Badge
                GameObject bPriceGo = new GameObject("PriceBadge", typeof(RectTransform), typeof(Image));
                bPriceGo.transform.SetParent(bCardGo.transform, false);
                RectTransform bpRt = (RectTransform)bPriceGo.transform;
                bpRt.anchorMin = new Vector2(0.5f, 0f); bpRt.anchorMax = new Vector2(0.5f, 0f);
                bpRt.pivot = new Vector2(0.5f, 0f);
                bpRt.anchoredPosition = new Vector2(0f, 6f);
                bpRt.sizeDelta = new Vector2(buyCardW - 16f, 20f);
                Image bpBg = bPriceGo.GetComponent<Image>();
                bpBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                bpBg.type = Image.Type.Sliced;
                bpBg.color = new Color(0.88f, 0.78f, 0.68f, 1f);

                GameObject bpTxtGo = new GameObject("PriceText", typeof(RectTransform));
                bpTxtGo.transform.SetParent(bPriceGo.transform, false);
                RectTransform bptRt = (RectTransform)bpTxtGo.transform;
                bptRt.anchorMin = Vector2.zero; bptRt.anchorMax = Vector2.one;
                bptRt.offsetMin = Vector2.zero; bptRt.offsetMax = Vector2.zero;
                Text bpTxt = bpTxtGo.AddComponent<Text>();
                bpTxt.font = font;
                bpTxt.text = buyItem.price <= 0 ? "Free" : $"{buyItem.price}g";
                bpTxt.fontSize = 13;
                bpTxt.fontStyle = FontStyle.Bold;
                bpTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                bpTxt.verticalOverflow = VerticalWrapMode.Overflow;
                bpTxt.color = new Color(0.24f, 0.16f, 0.10f, 1f);
                bpTxt.alignment = TextAnchor.MiddleCenter;
                bpTxt.raycastTarget = false;

                _buyButtonRects.Add(bCardRt);
                int capturedIdx = i;
                bCardGo.GetComponent<Button>().onClick.AddListener(() => BuyItem(_buyItems[capturedIdx], bCardRt));
            }

            // ── Scroll View 2: SELL CROPS & HARVESTS ─────────────────────────
            _sellScrollViewGo = new GameObject("SellScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            _sellScrollViewGo.transform.SetParent(rightPageGo.transform, false);
            RectTransform sellScrollRt = (RectTransform)_sellScrollViewGo.transform;
            sellScrollRt.anchorMin = new Vector2(0f, 0f); sellScrollRt.anchorMax = new Vector2(1f, 1f);
            sellScrollRt.offsetMin = new Vector2(0f, 0f); sellScrollRt.offsetMax = new Vector2(0f, -34f);

            Image ssMask = _sellScrollViewGo.GetComponent<Image>();
            ssMask.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            ssMask.type = Image.Type.Sliced;
            ssMask.color = new Color(0.48f, 0.35f, 0.32f, 0.01f);

            Mask sellMask = _sellScrollViewGo.GetComponent<Mask>();
            sellMask.showMaskGraphic = false;

            ScrollRect sScroll = _sellScrollViewGo.GetComponent<ScrollRect>();
            sScroll.horizontal = false;
            sScroll.vertical = true;
            sScroll.scrollSensitivity = 25f;
            sScroll.movementType = ScrollRect.MovementType.Clamped;

            int totalSellRows = Mathf.CeilToInt((float)_sellItems.Count / buyColumns);
            float sellTotalContentH = Mathf.Max(370f, (totalSellRows * (buyCardH + buyGapY)) + 16f);

            GameObject sellContentGo = new GameObject("SellContent", typeof(RectTransform));
            sellContentGo.transform.SetParent(_sellScrollViewGo.transform, false);
            RectTransform sellContRt = (RectTransform)sellContentGo.transform;
            sellContRt.anchorMin = new Vector2(0f, 1f); sellContRt.anchorMax = new Vector2(1f, 1f);
            sellContRt.pivot = new Vector2(0.5f, 1f);
            sellContRt.anchoredPosition = Vector2.zero;
            sellContRt.sizeDelta = new Vector2(0f, sellTotalContentH);
            sScroll.content = sellContRt;

            _sellCatalogButtonRects.Clear();
            _sellCatalogOwnedTexts.Clear();

            for (int i = 0; i < _sellItems.Count; i++)
            {
                ShopSellItem sellItem = _sellItems[i];
                int col = i % buyColumns;
                int row = i / buyColumns;

                float posX = (col == 0 ? -1f : 1f) * ((buyCardW * 0.5f) + (buyGapX * 0.5f));
                float posY = -8f - (row * (buyCardH + buyGapY)) - (buyCardH * 0.5f);

                GameObject sCardGo = new GameObject($"SellCard_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
                sCardGo.transform.SetParent(sellContentGo.transform, false);
                RectTransform sCardRt = (RectTransform)sCardGo.transform;
                sCardRt.anchorMin = new Vector2(0.5f, 1f); sCardRt.anchorMax = new Vector2(0.5f, 1f);
                sCardRt.pivot = new Vector2(0.5f, 0.5f);
                sCardRt.sizeDelta = new Vector2(buyCardW, buyCardH);
                sCardRt.anchoredPosition = new Vector2(posX, posY);

                Image sCardBg = sCardGo.GetComponent<Image>();
                sCardBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                sCardBg.type = Image.Type.Sliced;
                sCardBg.color = new Color(0.96f, 0.90f, 0.82f, 0.95f);

                // Icon
                GameObject sIconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                sIconGo.transform.SetParent(sCardGo.transform, false);
                RectTransform siRt = (RectTransform)sIconGo.transform;
                siRt.anchorMin = new Vector2(0.5f, 1f); siRt.anchorMax = new Vector2(0.5f, 1f);
                siRt.pivot = new Vector2(0.5f, 1f);
                siRt.anchoredPosition = new Vector2(0f, -6f);
                siRt.sizeDelta = new Vector2(38f, 38f);
                Image siImg = sIconGo.GetComponent<Image>();
                siImg.sprite = sellItem.icon != null ? sellItem.icon : _coinSprite;
                siImg.preserveAspect = true;
                siImg.color = siImg.sprite != null ? Color.white : new Color(0.6f, 0.5f, 0.3f, 0.6f);
                siImg.raycastTarget = false;

                // Name
                GameObject sNameGo = new GameObject("Name", typeof(RectTransform));
                sNameGo.transform.SetParent(sCardGo.transform, false);
                RectTransform snRt = (RectTransform)sNameGo.transform;
                snRt.anchorMin = new Vector2(0f, 1f); snRt.anchorMax = new Vector2(1f, 1f);
                snRt.pivot = new Vector2(0.5f, 1f);
                snRt.anchoredPosition = new Vector2(0f, -44f);
                snRt.sizeDelta = new Vector2(0f, 20f);
                Text snTxt = sNameGo.AddComponent<Text>();
                snTxt.font = font;
                snTxt.text = sellItem.itemName;
                snTxt.fontSize = 11;
                snTxt.fontStyle = FontStyle.Bold;
                snTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                snTxt.verticalOverflow = VerticalWrapMode.Overflow;
                snTxt.color = new Color(0.32f, 0.22f, 0.16f, 1f);
                snTxt.alignment = TextAnchor.MiddleCenter;
                snTxt.raycastTarget = false;

                // Price Badge
                GameObject sPriceGo = new GameObject("PriceBadge", typeof(RectTransform), typeof(Image));
                sPriceGo.transform.SetParent(sCardGo.transform, false);
                RectTransform spRt = (RectTransform)sPriceGo.transform;
                spRt.anchorMin = new Vector2(0.5f, 0f); spRt.anchorMax = new Vector2(0.5f, 0f);
                spRt.pivot = new Vector2(0.5f, 0f);
                spRt.anchoredPosition = new Vector2(0f, 6f);
                spRt.sizeDelta = new Vector2(buyCardW - 16f, 20f);
                Image spBg = sPriceGo.GetComponent<Image>();
                spBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                spBg.type = Image.Type.Sliced;
                spBg.color = new Color(0.82f, 0.88f, 0.78f, 1f);

                GameObject spTxtGo = new GameObject("PriceText", typeof(RectTransform));
                spTxtGo.transform.SetParent(sPriceGo.transform, false);
                RectTransform sptRt = (RectTransform)spTxtGo.transform;
                sptRt.anchorMin = new Vector2(0f, 0f); sptRt.anchorMax = new Vector2(0.55f, 1f);
                sptRt.offsetMin = Vector2.zero; sptRt.offsetMax = Vector2.zero;
                Text spTxt = spTxtGo.AddComponent<Text>();
                spTxt.font = font;
                spTxt.text = $"+{sellItem.price}g";
                spTxt.fontSize = 14;
                spTxt.fontStyle = FontStyle.Bold;
                spTxt.color = new Color(0.18f, 0.35f, 0.15f, 1f);
                spTxt.alignment = TextAnchor.MiddleCenter;
                spTxt.raycastTarget = false;

                GameObject sOwnedGo = new GameObject("OwnedText", typeof(RectTransform));
                sOwnedGo.transform.SetParent(sPriceGo.transform, false);
                RectTransform soRt = (RectTransform)sOwnedGo.transform;
                soRt.anchorMin = new Vector2(0.55f, 0f); soRt.anchorMax = new Vector2(1f, 1f);
                soRt.offsetMin = Vector2.zero; soRt.offsetMax = new Vector2(-4f, 0f);
                Text soTxt = sOwnedGo.AddComponent<Text>();
                soTxt.font = font;
                soTxt.text = "Have: 0";
                soTxt.fontSize = 13;
                soTxt.fontStyle = FontStyle.Bold;
                soTxt.color = new Color(0.35f, 0.25f, 0.18f, 1f);
                soTxt.alignment = TextAnchor.MiddleRight;
                soTxt.raycastTarget = false;
                _sellCatalogOwnedTexts.Add(soTxt);

                _sellCatalogButtonRects.Add(sCardRt);
                int capturedIdx = i;
                sCardGo.GetComponent<Button>().onClick.AddListener(() => SellItemFromCatalogue(_sellItems[capturedIdx], sCardRt));
            }

            SwitchRightTab(0);
            RefreshLeftInventoryPage();
        }
    }
}
