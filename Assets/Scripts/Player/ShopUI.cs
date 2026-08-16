using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Willowstead.Input;

namespace Willowstead.Player
{
    /// <summary>
    /// Data-driven Seed Shop UI.
    /// Add one ShopEntry per crop type in the Inspector — the layout builds itself.
    ///
    /// Features:
    ///   • 700-px wide panel with larger icons, fonts, and generous padding
    ///   • Permanent gold HUD anchored top-right (always visible, updates every frame)
    ///   • Buy animations: scale-pop + sparkle burst on success; shake + red flash on failure
    ///   • Sell animations: coin flies to HUD gold display on success; shake + red flash on failure
    ///   • Sell buttons show live item count (×N) updated on open and after every sale
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        [System.Serializable]
        public class ShopEntry
        {
            [Header("Seeds — Buy")]
            [Tooltip("Inventory item name for the seeds, e.g. 'Carrot Seeds'.")]
            public string seedItemName;
            public Sprite seedSprite;
            [Tooltip("Gold cost to buy one packet of seeds. 0 = free.")]
            public int seedBuyPrice = 0;

            [Header("Yield — Sell")]
            [Tooltip("Inventory item name of the harvested crop, e.g. 'Carrot'.")]
            public string yieldItemName;
            public Sprite yieldSprite;
            [Tooltip("Gold earned when selling one unit of this crop.")]
            public int yieldSellPrice = 25;
        }

        [Header("Shop Catalogue")]
        [Tooltip("One entry per crop type. Add as many as you like.")]
        [SerializeField] private ShopEntry[] _shopEntries = new ShopEntry[0];

        [Header("Prefab Mode")]
        [Tooltip("If true (default), the script binds to scene/prefab UI that you author — drag your canvas/prefab GameObjects into the matching _prefab* fields below. If false, the script builds the shop panel in code at runtime (the previous behaviour).")]
#pragma warning disable 0414
        [SerializeField] private bool _usePrefabLayout = false;
#pragma warning restore 0414
        [Tooltip("Root panel RectTransform for the Shop UI (active/inactive toggled here).")]
        [SerializeField] private RectTransform _prefabPanelRoot;
        [Tooltip("Container whose children are the Buy rows in order, one per ShopEntry.")]
        [SerializeField] private Transform _buyContentRoot;
        [Tooltip("Container whose children are the Sell rows in order, one per ShopEntry.")]
        [SerializeField] private Transform _sellContentRoot;
        [Tooltip("Optional: Gold HUD Text (e.g., 'HUDGoldText'). If not set, will try to find by name.")]
        [SerializeField] private Text _prefabHudGoldText;

	        [Header("Theme Sprites")] 
	        [Tooltip("Optional: Panel frame/background sprite (set 9-slice borders if you want it to scale).")]
	        [SerializeField] private Sprite _shopPanelFrameSprite;
	        [Tooltip("Optional: Generic button background sprite for Buy/Sell/Tab buttons.")]
	        [SerializeField] private Sprite _buttonSprite;
	        [Tooltip("Optional: Plus/Minus icons for quantity buttons (if you add them later).")]
	        [SerializeField] private Sprite _plusIcon;
	        [SerializeField] private Sprite _minusIcon;

	        [Header("Assets")] 
	        [SerializeField] private Sprite _coinSprite;

        private InventoryManager _inventory;
        private GameObject       _canvasGo;
        private GameObject       _panelGo;
        private bool             _isOpen           = false;
        private Coroutine        _bounceCoroutine;

        private readonly List<RectTransform> _buyButtonRects  = new List<RectTransform>();
        private readonly List<RectTransform> _sellButtonRects = new List<RectTransform>();

        private readonly List<Text> _sellCountLabels = new List<Text>();

        private GameObject _buyContent;
        private GameObject _sellContent;
        private int _activeTab = 0;          // 0 = Buy, 1 = Sell
        private Image _buyTabImg;
        private Image _sellTabImg;
        private readonly List<Text> _ownedCountLabels = new List<Text>();

        private Text          _hudGoldText;
        private RectTransform _hudGoldRect;
        private Coroutine     _hudPulseCoroutine;
        private int           _lastGoldAmount = -1;

        public bool IsOpen => _isOpen;

        private const float PANEL_WIDTH = 700f;
        private const float ROW_HEIGHT  = 72f;
        private const float HEADER_H    = 150f;
        private const float FOOTER_H    = 60f;

	#if UNITY_EDITOR
	        private void OnValidate()
	        {
	            if (_shopPanelFrameSprite == null)
	                _shopPanelFrameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Inventory & chests/1/shop1.png");
	            if (_buttonSprite == null)
	                _buttonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Inventory & chests/2/brown slot.png");
	            if (_plusIcon == null)
	                _plusIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Inventory & chests/2/+.png");
	            if (_minusIcon == null)
	                _minusIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Inventory & chests/2/-.png");
	            if (_coinSprite == null)
	                _coinSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/gold coin.png");
	        }
	#endif


        private void EnsureDefaultShopEntries()
        {
            var defaultList = new List<ShopEntry>
            {
                new ShopEntry
                {
                    seedItemName = "Fertilizer",
                    seedSprite = UIResourceHelper.GetItemIconSprite("Fertilizer"),
                    seedBuyPrice = 0,
                    yieldItemName = "Fertilizer",
                    yieldSprite = UIResourceHelper.GetItemIconSprite("Fertilizer"),
                    yieldSellPrice = 5
                },
                new ShopEntry
                {
                    seedItemName = "Carrot Seeds",
                    seedSprite = UIResourceHelper.GetItemIconSprite("Carrot Seeds"),
                    seedBuyPrice = 15,
                    yieldItemName = "Carrot",
                    yieldSprite = UIResourceHelper.GetItemIconSprite("Carrot"),
                    yieldSellPrice = 25
                },
                new ShopEntry
                {
                    seedItemName = "Potato Seeds",
                    seedSprite = UIResourceHelper.GetItemIconSprite("Potato Seeds"),
                    seedBuyPrice = 20,
                    yieldItemName = "Potato",
                    yieldSprite = UIResourceHelper.GetItemIconSprite("Potato"),
                    yieldSellPrice = 35
                },
                new ShopEntry
                {
                    seedItemName = "Tomato Seeds",
                    seedSprite = UIResourceHelper.GetItemIconSprite("Tomato Seeds"),
                    seedBuyPrice = 25,
                    yieldItemName = "Tomato",
                    yieldSprite = UIResourceHelper.GetItemIconSprite("Tomato"),
                    yieldSellPrice = 45
                },
                new ShopEntry
                {
                    seedItemName = "Corn Seeds",
                    seedSprite = UIResourceHelper.GetItemIconSprite("Corn Seeds"),
                    seedBuyPrice = 30,
                    yieldItemName = "Corn",
                    yieldSprite = UIResourceHelper.GetItemIconSprite("Corn"),
                    yieldSellPrice = 55
                },
                new ShopEntry
                {
                    seedItemName = "Straw Seeds",
                    seedSprite = UIResourceHelper.GetItemIconSprite("Straw Seeds"),
                    seedBuyPrice = 10,
                    yieldItemName = "Straw",
                    yieldSprite = UIResourceHelper.GetItemIconSprite("Straw"),
                    yieldSellPrice = 18
                },
                new ShopEntry
                {
                    seedItemName = "Rotten Crop",
                    seedSprite = UIResourceHelper.GetItemIconSprite("Rotten Crop"),
                    seedBuyPrice = 0,
                    yieldItemName = "Rotten Crop",
                    yieldSprite = UIResourceHelper.GetItemIconSprite("Rotten Crop"),
                    yieldSellPrice = 4
                },
                new ShopEntry
                {
                    seedItemName = "Wood",
                    seedSprite = UIResourceHelper.GetItemIconSprite("Wood"),
                    seedBuyPrice = 0,
                    yieldItemName = "Wood",
                    yieldSprite = UIResourceHelper.GetItemIconSprite("Wood"),
                    yieldSellPrice = 10
                }
            };

            // Deduplicate and normalize by canonical item name (e.g. ignoring case and underscores like carrot_seeds vs Carrot Seeds)
            var cleanList = new List<ShopEntry>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // First add from defaultList to guarantee proper ordering & names
            foreach (var item in defaultList)
            {
                string key = item.seedItemName.Replace("_", " ").Trim();
                if (seenKeys.Add(key))
                {
                    cleanList.Add(item);
                }
            }

            // If there were any extra custom items from inspector, append them if not duplicate
            if (_shopEntries != null)
            {
                foreach (var item in _shopEntries)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.seedItemName)) continue;
                    string key = item.seedItemName.Replace("_", " ").Trim();
                    if (seenKeys.Add(key))
                    {
                        cleanList.Add(item);
                    }
                }
            }

            _shopEntries = cleanList.ToArray();
        }

        private void Start()
        {
            _inventory = GetComponent<InventoryManager>();
            if (_inventory == null) _inventory = FindAnyObjectByType<InventoryManager>();

            if (_coinSprite == null) _coinSprite = UIResourceHelper.GetBackgroundSprite();

            EnsureDefaultShopEntries();

            _usePrefabLayout = false;
            CreateShopUI();
            SetUIActive(false);
        }

        private void Update()
        {
            // below runs unconditionally so the gold readout stays current.
            bool shopPressed = Input.KeyRebindingManager.WasPressedThisFrame(Input.KeyAction.Shop) ||
                               (Keyboard.current != null && (Keyboard.current.pKey.wasPressedThisFrame || Keyboard.current.bKey.wasPressedThisFrame));

            if (!InputReader.BlockGameplayInput && shopPressed)
            {
                Debug.Log("[InputDebug] Shop key pressed -> Toggling Shop UI.");
                ToggleUI();
            }

            UpdateHudGold();
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
                InventoryUI invUI = FindAnyObjectByType<InventoryUI>();
                if (invUI != null) invUI.CloseUI();

                UpdateSellLabels();

                if (_panelGo != null)
                {
                    if (_bounceCoroutine != null) StopCoroutine(_bounceCoroutine);
                    _bounceCoroutine = StartCoroutine(PlayBounceAnimation(_panelGo.transform));
                }
            }
        }

        private IEnumerator PlayBounceAnimation(Transform panelTransform)
        {
            float duration = 0.22f;
            float elapsed  = 0f;
            panelTransform.localScale = new Vector3(0.75f, 0.75f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t     = elapsed / duration;
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
        }


        /// Called every frame; updates the HUD label and triggers a pulse only when the value changes.
        private void UpdateHudGold()
        {
            if (_inventory == null || _hudGoldText == null) return;

            int gold = _inventory.GetItemCount("Gold");
            if (gold == _lastGoldAmount) return;

            _lastGoldAmount      = gold;
            _hudGoldText.text    = gold.ToString("N0");

            if (_hudPulseCoroutine != null) StopCoroutine(_hudPulseCoroutine);
            _hudPulseCoroutine = StartCoroutine(PulseHudGold());
        }

        private IEnumerator PulseHudGold()
        {
            float duration = 0.22f;
            float elapsed  = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float s = 1f + Mathf.Sin((elapsed / duration) * Mathf.PI) * 0.30f;
                if (_hudGoldRect != null) _hudGoldRect.localScale = Vector3.one * s;
                yield return null;
            }

            if (_hudGoldRect != null) _hudGoldRect.localScale = Vector3.one;
            _hudPulseCoroutine = null;
        }


        /// Refreshes every sell button's label with the current inventory count.
        /// Called when the shop opens and after each successful sale.
        private void UpdateSellLabels()
        {
            if (_inventory == null) return;
            for (int i = 0; i < _shopEntries.Length && i < _sellCountLabels.Count; i++)
            {
                int count = _inventory.GetItemCount(_shopEntries[i].yieldItemName);
                if (_sellCountLabels[i] != null)
                    _sellCountLabels[i].text = $"+{_shopEntries[i].yieldSellPrice}g";
                if (i < _ownedCountLabels.Count && _ownedCountLabels[i] != null)
                    _ownedCountLabels[i].text = $"\u00d7{count}";
            }
        }

        private static string FormatSellLabel(int price, int count) => $"+{price}g  \u00d7{count}";


        private void BuySeed(ShopEntry entry, RectTransform buyBtnRect)
        {
            if (_inventory == null) return;

            if (entry.seedBuyPrice > 0)
            {
                if (_inventory.GetItemCount("Gold") < entry.seedBuyPrice)
                {
                    Debug.LogWarning($"[ShopUI] Not enough gold to buy {entry.seedItemName} (need {entry.seedBuyPrice}g).");
                    StartCoroutine(ShakeAndFlashRed(buyBtnRect, buyBtnRect.GetComponent<Image>(),
                        new Color(0.75f, 0.48f, 0.22f, 1f)));
                    return;
                }

                _inventory.RemoveItem("Gold", entry.seedBuyPrice);
            }

            StartCoroutine(ScalePopAnimation(buyBtnRect));
            SpawnSparkleBurst(buyBtnRect.position);

            Vector3 startPos = GetWorldPos(buyBtnRect);
            Sprite  icon     = entry.seedSprite != null ? entry.seedSprite : _coinSprite;

            World.FlyingItemAnimation.Spawn(icon, startPos, null, () =>
            {
                _inventory.AddItem(entry.seedItemName, 1);
            });
        }

        private void SellYield(ShopEntry entry, RectTransform sellBtnRect, Text sellCountLabel)
        {
            if (_inventory == null) return;

            if (_inventory.GetItemCount(entry.yieldItemName) <= 0)
            {
                Debug.LogWarning($"[ShopUI] No {entry.yieldItemName} to sell.");
                StartCoroutine(ShakeAndFlashRed(sellBtnRect, sellBtnRect.GetComponent<Image>(),
                    new Color(0.35f, 0.58f, 0.32f, 1f)));
                return;
            }

            _inventory.RemoveItem(entry.yieldItemName, 1);

            int remaining = _inventory.GetItemCount(entry.yieldItemName);

            if (sellCountLabel != null)
                sellCountLabel.text = FormatSellLabel(entry.yieldSellPrice, remaining);

            int idx = Array.IndexOf(_shopEntries, entry);
            if (idx >= 0 && idx < _ownedCountLabels.Count && _ownedCountLabels[idx] != null)
                _ownedCountLabels[idx].text = $"\u00d7{remaining}";

            Vector3 startPos  = GetWorldPos(sellBtnRect);
            Color   goldColor = new Color(1f, 0.82f, 0f, 1f);

            World.FlyingItemAnimation.Spawn(_coinSprite, startPos, _hudGoldRect, () =>
            {
                _inventory.AddItem("Gold", entry.yieldSellPrice);
                // HUD auto-updates + pulses via Update() when _lastGoldAmount changes
            }, goldColor);
        }

        /// Converts a RectTransform screen position to a world position usable by FlyingItemAnimation.
        private Vector3 GetWorldPos(RectTransform rect)
        {
            if (rect == null) return transform.position;
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, rect.position);
            Vector3 world     = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
            world.z = 0f;
            return world;
        }


        /// Quick scale-pop: grows to 1.35× then snaps back. Signals a successful action.
        private IEnumerator ScalePopAnimation(RectTransform rt)
        {
            if (rt == null) yield break;
            Vector3 original = Vector3.one;
            float popDuration = 0.15f;
            float dropDuration = 0.10f;

            for (float t = 0f; t < popDuration; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                rt.localScale = original * Mathf.Lerp(1f, 1.35f, t / popDuration);
                yield return null;
            }
            for (float t = 0f; t < dropDuration; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                rt.localScale = original * Mathf.Lerp(1.35f, 1f, t / dropDuration);
                yield return null;
            }

            if (rt != null) rt.localScale = original;
        }

        /// Horizontal shake + red flash. Signals a failed action.
        private IEnumerator ShakeAndFlashRed(RectTransform rt, Image img, Color originalColor)
        {
            Vector2 originalPos = rt.anchoredPosition;
            if (img != null) img.color = Color.red;

            float duration = 0.40f;
            float elapsed  = 0f;
            float freq     = 35f;
            float amp      = 10f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rt.anchoredPosition = originalPos + new Vector2(Mathf.Sin(elapsed * freq) * amp * (1f - t), 0f);

                if (img != null && t > 0.5f)
                    img.color = Color.Lerp(Color.red, originalColor, (t - 0.5f) / 0.5f);

                yield return null;
            }

            rt.anchoredPosition = originalPos;
            if (img != null) img.color = originalColor;
        }


        /// Spawns 5 small golden squares that fly outward and fade from the given screen position.
        private void SpawnSparkleBurst(Vector2 screenPos)
        {
            RectTransform canvasRect = _canvasGo.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, null, out Vector2 localPos);

            Color[] palette =
            {
                new Color(1.00f, 0.84f, 0.00f, 1f),
                new Color(1.00f, 1.00f, 0.60f, 1f),
                new Color(1.00f, 0.65f, 0.00f, 1f),
                new Color(1.00f, 0.92f, 0.20f, 1f),
                new Color(0.90f, 0.75f, 0.10f, 1f),
            };

            for (int i = 0; i < 5; i++)
            {
                GameObject sparkleGo = new GameObject("Sparkle");
                sparkleGo.transform.SetParent(_canvasGo.transform, false);

                Image img = sparkleGo.AddComponent<Image>();
                img.color = palette[i];

                RectTransform sparkleRt = sparkleGo.GetComponent<RectTransform>();
                sparkleRt.sizeDelta       = new Vector2(9f, 9f);
                sparkleRt.anchoredPosition = localPos;

                float angle = i * 72f * Mathf.Deg2Rad + 0.30f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                StartCoroutine(AnimateSparkle(sparkleRt, img, dir));
            }
        }

        private IEnumerator AnimateSparkle(RectTransform rt, Image img, Vector2 dir)
        {
            float   duration  = 0.50f;
            float   elapsed   = 0f;
            Vector2 startPos  = rt.anchoredPosition;
            Color   startColor = img.color;
            float   dist      = 65f;

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

        private ScrollRect _scrollRect;

        private void SwitchTab(int tab)
        {
            _activeTab = tab;
            if (_buyContent  != null) _buyContent.SetActive(tab == 0);
            if (_sellContent != null) _sellContent.SetActive(tab == 1);

            if (_scrollRect != null)
            {
                if (tab == 0 && _buyContent != null)
                {
                    _scrollRect.content = (RectTransform)_buyContent.transform;
                    _scrollRect.verticalNormalizedPosition = 1f;
                }
                else if (tab == 1 && _sellContent != null)
                {
                    _scrollRect.content = (RectTransform)_sellContent.transform;
                    _scrollRect.verticalNormalizedPosition = 1f;
                }
            }

            Color tabActive = new Color(0.48f, 0.35f, 0.32f, 1f); // Matches the cozy inner board
            Color tabDimmed = new Color(0.32f, 0.22f, 0.20f, 0.85f); // Subtly recessed background tab

            if (_buyTabImg  != null) _buyTabImg.color  = tab == 0 ? tabActive : tabDimmed;
            if (_sellTabImg != null) _sellTabImg.color = tab == 1 ? tabActive : tabDimmed;
        }

        private void BindPrefabShopUI()
        {
            if (_prefabPanelRoot == null || _buyContentRoot == null || _sellContentRoot == null)
            {
                Debug.LogWarning("[ShopUI] Prefab mode is on but _prefabPanelRoot / _buyContentRoot / _sellContentRoot are not all assigned. " +
                    "The shop panel will not render. Either assign a canvas/prefab root to _prefabPanelRoot, a Buy-content container to _buyContentRoot, and a Sell-content container to _sellContentRoot in the inspector, " +
                    "or uncheck _usePrefabLayout to fall back to runtime code-built UI.");
                return;
            }

            _panelGo = _prefabPanelRoot != null ? _prefabPanelRoot.gameObject : null;
            _buyContent = _buyContentRoot != null ? _buyContentRoot.gameObject : null;
            _sellContent = _sellContentRoot != null ? _sellContentRoot.gameObject : null;

            var buyTabBtn = UIResourceHelper.FindChildComponentByName<Button>(_prefabPanelRoot, new[] { "BuyTab", "Buy Seeds", "Buy" });
            var sellTabBtn = UIResourceHelper.FindChildComponentByName<Button>(_prefabPanelRoot, new[] { "SellTab", "Sell Crops", "Sell" });
            _buyTabImg = buyTabBtn != null ? buyTabBtn.GetComponent<Image>() : null;
            _sellTabImg = sellTabBtn != null ? sellTabBtn.GetComponent<Image>() : null;
            if (buyTabBtn != null) buyTabBtn.onClick.AddListener(() => SwitchTab(0));
            if (sellTabBtn != null) sellTabBtn.onClick.AddListener(() => SwitchTab(1));

            if (_prefabHudGoldText != null)
            {
                _hudGoldText = _prefabHudGoldText;
                _hudGoldRect = _hudGoldText != null ? _hudGoldText.GetComponent<RectTransform>() : null;
            }
            else
            {
                _hudGoldText = UIResourceHelper.FindChildComponentByName<Text>(_prefabPanelRoot, new[] { "HUDGoldText", "GoldText", "Gold", "Text_Gold" });
                if (_hudGoldText == null)
                {
                    // As a fallback, search globally (e.g., if HUD is elsewhere in the canvas)
                    var allTexts = FindObjectsByType<Text>(FindObjectsInactive.Include);
                    foreach (var t in allTexts)
                    {
                        if (t != null && (t.name == "HUDGoldText" || t.name == "GoldText" || t.name == "Gold"))
                        { _hudGoldText = t; break; }
                    }
                }
                _hudGoldRect = _hudGoldText != null ? _hudGoldText.GetComponent<RectTransform>() : null;
            }

            _buyButtonRects.Clear();
            _sellButtonRects.Clear();
            _sellCountLabels.Clear();
            _ownedCountLabels.Clear();

            int entries = _shopEntries != null ? _shopEntries.Length : 0;
            for (int i = 0; i < entries; i++)
            {
                var entry = _shopEntries[i];

                Transform buyRow = (_buyContentRoot != null && i < _buyContentRoot.childCount) ? _buyContentRoot.GetChild(i) : null;
                if (buyRow == null)
                {
                    Debug.LogWarning($"[ShopUI] Missing Buy row {i} under prefab root. Expected child index {i}.");
                }
                else
                {
                    var buyIcon = UIResourceHelper.FindChildComponentByName<Image>(buyRow, new[] { "BuyIcon", "Icon", "SeedIcon" });
                    if (buyIcon != null)
                    {
                        buyIcon.sprite = entry.seedSprite != null ? entry.seedSprite : entry.yieldSprite;
                        buyIcon.preserveAspect = true;
                        buyIcon.color = buyIcon.sprite != null ? Color.white : new Color(0.6f, 0.5f, 0.3f, 0.6f);
                    }

                    var buyName = UIResourceHelper.FindChildComponentByName<Text>(buyRow, new[] { "BuyName", "Name", "ItemName" });
                    if (buyName != null) buyName.text = entry.seedItemName;

                    var buyBtn = UIResourceHelper.FindChildComponentByName<Button>(buyRow, new[] { "BuyButton", "Buy", "Button" }) ?? buyRow.GetComponentInChildren<Button>(true);
                    if (buyBtn != null)
                    {
                        string priceLabel = entry.seedBuyPrice <= 0 ? "Free" : $"{entry.seedBuyPrice}g";
                        var buyLabel = buyBtn.GetComponentInChildren<Text>(true);
                        if (buyLabel != null) buyLabel.text = priceLabel;

                        int capturedI = i;
                        var rt = buyBtn.GetComponent<RectTransform>();
                        buyBtn.onClick.AddListener(() => BuySeed(_shopEntries[capturedI], rt));
                        _buyButtonRects.Add(rt);
                    }
                    else
                    {
                        _buyButtonRects.Add(null);
                        Debug.LogWarning($"[ShopUI] Buy row {i} has no Button");
                    }
                }

                Transform sellRow = (_sellContentRoot != null && i < _sellContentRoot.childCount) ? _sellContentRoot.GetChild(i) : null;
                if (sellRow == null)
                {
                    Debug.LogWarning($"[ShopUI] Missing Sell row {i} under prefab root. Expected child index {i}.");
                }
                else
                {
                    var sellIcon = UIResourceHelper.FindChildComponentByName<Image>(sellRow, new[] { "SellIcon", "Icon", "YieldIcon" });
                    if (sellIcon != null)
                    {
                        sellIcon.sprite = entry.yieldSprite != null ? entry.yieldSprite : entry.seedSprite;
                        sellIcon.preserveAspect = true;
                        sellIcon.color = sellIcon.sprite != null ? Color.white : new Color(0.6f, 0.5f, 0.3f, 0.6f);
                    }

                    var sellName = UIResourceHelper.FindChildComponentByName<Text>(sellRow, new[] { "SellName", "Name", "ItemName" });
                    if (sellName != null) sellName.text = entry.yieldItemName;

                    int initCount = _inventory != null ? _inventory.GetItemCount(entry.yieldItemName) : 0;
                    var owned = UIResourceHelper.FindChildComponentByName<Text>(sellRow, new[] { "OwnedCount", "Owned", "Count" });
                    if (owned != null) owned.text = $"\u00d7{initCount}";
                    _ownedCountLabels.Add(owned);

                    var sellBtn = UIResourceHelper.FindChildComponentByName<Button>(sellRow, new[] { "SellButton", "Sell", "Button" }) ?? sellRow.GetComponentInChildren<Button>(true);
                    Text sellLabel = null;
                    RectTransform sellRt = null;
                    if (sellBtn != null)
                    {
                        sellLabel = sellBtn.GetComponentInChildren<Text>(true);
                        if (sellLabel != null) sellLabel.text = $"+{entry.yieldSellPrice}g";
                        sellRt = sellBtn.GetComponent<RectTransform>();

                        int capturedJ = i;
                        var capturedLabel = sellLabel;
                        sellBtn.onClick.AddListener(() => SellYield(_shopEntries[capturedJ], sellRt, capturedLabel));
                        _sellButtonRects.Add(sellRt);
                    }
                    else
                    {
                        _sellButtonRects.Add(null);
                        Debug.LogWarning($"[ShopUI] Sell row {i} has no Button");
                    }

                    _sellCountLabels.Add(sellLabel);
                }
            }

            SwitchTab(0);
        }


        private void CreateShopUI()
        {
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            _canvasGo = canvas != null ? canvas.gameObject : null;
            UIResourceHelper.EnsureEventSystem();

            Sprite bgSprite = _shopPanelFrameSprite != null ? _shopPanelFrameSprite : UIResourceHelper.GetBackgroundSprite();
            Font   font     = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int    entries  = _shopEntries != null ? _shopEntries.Length : 0;

            CreateGoldHud(bgSprite, font);

            // ── Cozy Catalog Dimensions (Reference Mockup) ────────────────────────
            float shopWidth = 430f;
            float shopHeight = 580f;
            float halfH = shopHeight * 0.5f;

            _panelGo = new GameObject("ShopPanel");
            _panelGo.transform.SetParent(_canvasGo.transform, false);
            RectTransform panelRect = _panelGo.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(shopWidth, shopHeight);
            panelRect.anchoredPosition = new Vector2(0f, -10f);

            // ── Top Ear Bookmark Tabs (Buy / Sell) ──
            // Left Tab: Buy Seeds
            GameObject buyTabGo = new GameObject("BuyTab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
            buyTabGo.transform.SetParent(_panelGo.transform, false);
            RectTransform buyTabRt = (RectTransform)buyTabGo.transform;
            buyTabRt.anchorMin = new Vector2(0.5f, 1f);
            buyTabRt.anchorMax = new Vector2(0.5f, 1f);
            buyTabRt.pivot = new Vector2(0.5f, 0f);
            buyTabRt.anchoredPosition = new Vector2(-60f, -4f);
            buyTabRt.sizeDelta = new Vector2(92f, 48f);

            _buyTabImg = buyTabGo.GetComponent<Image>();
            _buyTabImg.sprite = UIResourceHelper.GetBackgroundSprite();
            _buyTabImg.type = Image.Type.Sliced;
            _buyTabImg.color = new Color(0.48f, 0.35f, 0.32f, 1f);

            Text buyTabTxt = CreateGO<Text>("BuyTabLabel", buyTabGo.transform);
            buyTabTxt.text = "BUY";
            buyTabTxt.font = font;
            buyTabTxt.fontSize = 15;
            buyTabTxt.fontStyle = FontStyle.Bold;
            buyTabTxt.color = new Color(1f, 0.95f, 0.85f, 1f);
            buyTabTxt.alignment = TextAnchor.MiddleCenter;
            Rect(buyTabTxt, 0f, 4f, 85f, 36f);

            buyTabGo.GetComponent<Button>().onClick.AddListener(() => SwitchTab(0));

            // Right Tab: Sell Crops
            GameObject sellTabGo = new GameObject("SellTab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
            sellTabGo.transform.SetParent(_panelGo.transform, false);
            RectTransform sellTabRt = (RectTransform)sellTabGo.transform;
            sellTabRt.anchorMin = new Vector2(0.5f, 1f);
            sellTabRt.anchorMax = new Vector2(0.5f, 1f);
            sellTabRt.pivot = new Vector2(0.5f, 0f);
            sellTabRt.anchoredPosition = new Vector2(60f, -4f);
            sellTabRt.sizeDelta = new Vector2(92f, 48f);

            _sellTabImg = sellTabGo.GetComponent<Image>();
            _sellTabImg.sprite = UIResourceHelper.GetBackgroundSprite();
            _sellTabImg.type = Image.Type.Sliced;
            _sellTabImg.color = new Color(0.38f, 0.28f, 0.26f, 1f);

            Text sellTabTxt = CreateGO<Text>("SellTabLabel", sellTabGo.transform);
            sellTabTxt.text = "SELL";
            sellTabTxt.font = font;
            sellTabTxt.fontSize = 15;
            sellTabTxt.fontStyle = FontStyle.Bold;
            sellTabTxt.color = new Color(1f, 0.95f, 0.85f, 1f);
            sellTabTxt.alignment = TextAnchor.MiddleCenter;
            Rect(sellTabTxt, 0f, 4f, 85f, 36f);

            sellTabGo.GetComponent<Button>().onClick.AddListener(() => SwitchTab(1));

            // ── Outer Wooden Border & Cozy Rosewood / Cocoa Background ───────────
            Image outerBorder = CreateGO<Image>("OuterBorder", _panelGo.transform);
            outerBorder.sprite = UIResourceHelper.GetBackgroundSprite();
            outerBorder.type = Image.Type.Sliced;
            outerBorder.color = new Color(0.85f, 0.65f, 0.48f, 1f); // Warm peach-gold framing outline
            Rect(outerBorder, 0f, 0f, shopWidth, shopHeight);

            Image innerSlate = CreateGO<Image>("InnerSlate", _panelGo.transform);
            innerSlate.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            innerSlate.type = Image.Type.Sliced;
            innerSlate.color = new Color(0.48f, 0.35f, 0.32f, 0.98f); // Soft cozy mauve-brown slate
            Rect(innerSlate, 0f, 0f, shopWidth - 12f, shopHeight - 12f);

            // ── Parchment Header / Mode Banner ──────────────────────────────────
            Image headerBar = CreateGO<Image>("HeaderParchment", _panelGo.transform);
            headerBar.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            headerBar.type = Image.Type.Sliced;
            headerBar.color = new Color(0.96f, 0.88f, 0.78f, 1f); // Creamy parchment header
            Rect(headerBar, 0f, halfH - 42f, shopWidth - 60f, 34f);

            Text headerTitle = CreateGO<Text>("HeaderTitle", headerBar.transform);
            headerTitle.text = "GARDEN CATALOG";
            headerTitle.font = font;
            headerTitle.fontSize = 15;
            headerTitle.fontStyle = FontStyle.Bold;
            headerTitle.color = new Color(0.35f, 0.22f, 0.16f, 1f);
            headerTitle.alignment = TextAnchor.MiddleCenter;
            Rect(headerTitle, 0f, 0f, shopWidth - 70f, 34f);

            // ── Scroll / Grid Viewport Area ──────────────────────────────────────
            float viewportW = shopWidth - 28f;
            float viewportH = shopHeight - 110f;

            // Scroll View Root
            GameObject scrollViewGo = new GameObject("CatalogScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            scrollViewGo.transform.SetParent(_panelGo.transform, false);
            RectTransform scrollRt = (RectTransform)scrollViewGo.transform;
            scrollRt.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRt.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRt.pivot = new Vector2(0.5f, 0.5f);
            scrollRt.sizeDelta = new Vector2(viewportW, viewportH);
            scrollRt.anchoredPosition = new Vector2(0f, -14f);

            Image scrollMaskImg = scrollViewGo.GetComponent<Image>();
            scrollMaskImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            scrollMaskImg.type = Image.Type.Sliced;
            scrollMaskImg.color = new Color(0.48f, 0.35f, 0.32f, 0.01f); // Transparent raycastable mask

            Mask mask = scrollViewGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            _scrollRect = scrollViewGo.GetComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.scrollSensitivity = 28f;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;

            int columns = 3;
            float cardWidth = 118f;
            float cardHeight = 118f;
            float spacingX = 12f;
            float spacingY = 12f;
            int totalRows = Mathf.CeilToInt((float)entries / columns);
            float totalContentH = Mathf.Max(viewportH, (totalRows * (cardHeight + spacingY)) + 20f);

            // Buy Content
            _buyContent = new GameObject("BuyContent", typeof(RectTransform));
            _buyContent.transform.SetParent(scrollViewGo.transform, false);
            RectTransform buyRt = (RectTransform)_buyContent.transform;
            buyRt.anchorMin = new Vector2(0f, 1f);
            buyRt.anchorMax = new Vector2(1f, 1f);
            buyRt.pivot = new Vector2(0.5f, 1f);
            buyRt.anchoredPosition = Vector2.zero;
            buyRt.sizeDelta = new Vector2(0f, totalContentH);

            // Sell Content
            _sellContent = new GameObject("SellContent", typeof(RectTransform));
            _sellContent.transform.SetParent(scrollViewGo.transform, false);
            RectTransform sellRt = (RectTransform)_sellContent.transform;
            sellRt.anchorMin = new Vector2(0f, 1f);
            sellRt.anchorMax = new Vector2(1f, 1f);
            sellRt.pivot = new Vector2(0.5f, 1f);
            sellRt.anchoredPosition = Vector2.zero;
            sellRt.sizeDelta = new Vector2(0f, totalContentH);

            _scrollRect.content = buyRt;

            _buyButtonRects.Clear();
            _sellButtonRects.Clear();
            _sellCountLabels.Clear();
            _ownedCountLabels.Clear();

            // Build Grid Cards
            for (int i = 0; i < entries; i++)
            {
                ShopEntry entry = _shopEntries[i];
                int col = i % columns;
                int row = i / columns;

                float posX = (col - 1) * (cardWidth + spacingX);
                float posY = -16f - (row * (cardHeight + spacingY)) - (cardHeight * 0.5f);

                // ── BUY CARD (3-Column Grid) ──
                GameObject buyCardGo = new GameObject($"BuyCard_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
                buyCardGo.transform.SetParent(_buyContent.transform, false);
                RectTransform bCardRt = (RectTransform)buyCardGo.transform;
                bCardRt.anchorMin = new Vector2(0.5f, 1f);
                bCardRt.anchorMax = new Vector2(0.5f, 1f);
                bCardRt.pivot = new Vector2(0.5f, 0.5f);
                bCardRt.sizeDelta = new Vector2(cardWidth, cardHeight);
                bCardRt.anchoredPosition = new Vector2(posX, posY);

                Image bCardBg = buyCardGo.GetComponent<Image>();
                bCardBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                bCardBg.type = Image.Type.Sliced;
                bCardBg.color = new Color(0.96f, 0.90f, 0.82f, 1f); // Warm parchment card

                // Item Icon Box
                Image bIcon = CreateGO<Image>("Icon", buyCardGo.transform);
                bIcon.sprite = entry.seedSprite != null ? entry.seedSprite : entry.yieldSprite;
                bIcon.color = bIcon.sprite != null ? Color.white : new Color(0.6f, 0.5f, 0.3f, 0.6f);
                bIcon.preserveAspect = true;
                Rect(bIcon, 0f, 14f, 44f, 44f);

                // Item Name (Crisp mini caption)
                Text bName = CreateGO<Text>("Name", buyCardGo.transform);
                bName.text = !string.IsNullOrEmpty(entry.seedItemName) ? entry.seedItemName : entry.yieldItemName;
                bName.font = font;
                bName.fontSize = 11;
                bName.fontStyle = FontStyle.Bold;
                bName.color = new Color(0.32f, 0.22f, 0.16f, 1f);
                bName.alignment = TextAnchor.MiddleCenter;
                Rect(bName, 0f, -14f, cardWidth - 8f, 16f);

                // Price Bar (Gold coin icon + price tag)
                Image bPriceBar = CreateGO<Image>("PriceBar", buyCardGo.transform);
                bPriceBar.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                bPriceBar.type = Image.Type.Sliced;
                bPriceBar.color = new Color(0.88f, 0.78f, 0.68f, 1f);
                Rect(bPriceBar, 0f, -38f, cardWidth - 16f, 22f);

                if (_coinSprite != null)
                {
                    Image bCoin = CreateGO<Image>("Coin", bPriceBar.transform);
                    bCoin.sprite = _coinSprite;
                    bCoin.preserveAspect = true;
                    Rect(bCoin, -cardWidth * 0.5f + 20f, 0f, 14f, 14f);
                }

                Text bPriceTxt = CreateGO<Text>("Price", bPriceBar.transform);
                bPriceTxt.text = entry.seedBuyPrice <= 0 ? "Free" : $"{entry.seedBuyPrice}";
                bPriceTxt.font = font;
                bPriceTxt.fontSize = 12;
                bPriceTxt.fontStyle = FontStyle.Bold;
                bPriceTxt.color = new Color(0.24f, 0.16f, 0.10f, 1f);
                bPriceTxt.alignment = TextAnchor.MiddleCenter;
                Rect(bPriceTxt, 6f, 0f, 50f, 20f);

                _buyButtonRects.Add(bCardRt);
                int capturedI = i;
                buyCardGo.GetComponent<Button>().onClick.AddListener(
                    () => BuySeed(_shopEntries[capturedI], _buyButtonRects[capturedI]));

                // ── SELL CARD (3-Column Grid) ──
                GameObject sellCardGo = new GameObject($"SellCard_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
                sellCardGo.transform.SetParent(_sellContent.transform, false);
                RectTransform sCardRt = (RectTransform)sellCardGo.transform;
                sCardRt.anchorMin = new Vector2(0.5f, 1f);
                sCardRt.anchorMax = new Vector2(0.5f, 1f);
                sCardRt.pivot = new Vector2(0.5f, 0.5f);
                sCardRt.sizeDelta = new Vector2(cardWidth, cardHeight);
                sCardRt.anchoredPosition = new Vector2(posX, posY);

                Image sCardBg = sellCardGo.GetComponent<Image>();
                sCardBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                sCardBg.type = Image.Type.Sliced;
                sCardBg.color = new Color(0.96f, 0.90f, 0.82f, 1f); // Warm parchment card

                // Item Icon Box
                Image sIcon = CreateGO<Image>("Icon", sellCardGo.transform);
                sIcon.sprite = entry.yieldSprite != null ? entry.yieldSprite : entry.seedSprite;
                sIcon.color = sIcon.sprite != null ? Color.white : new Color(0.6f, 0.5f, 0.3f, 0.6f);
                sIcon.preserveAspect = true;
                Rect(sIcon, 0f, 14f, 44f, 44f);

                // Item Name
                Text sName = CreateGO<Text>("Name", sellCardGo.transform);
                sName.text = entry.yieldItemName;
                sName.font = font;
                sName.fontSize = 11;
                sName.fontStyle = FontStyle.Bold;
                sName.color = new Color(0.32f, 0.22f, 0.16f, 1f);
                sName.alignment = TextAnchor.MiddleCenter;
                Rect(sName, 0f, -14f, cardWidth - 8f, 16f);

                // Price Bar
                Image sPriceBar = CreateGO<Image>("PriceBar", sellCardGo.transform);
                sPriceBar.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                sPriceBar.type = Image.Type.Sliced;
                sPriceBar.color = new Color(0.82f, 0.88f, 0.78f, 1f);
                Rect(sPriceBar, 0f, -38f, cardWidth - 16f, 22f);

                if (_coinSprite != null)
                {
                    Image sCoin = CreateGO<Image>("Coin", sPriceBar.transform);
                    sCoin.sprite = _coinSprite;
                    sCoin.preserveAspect = true;
                    Rect(sCoin, -cardWidth * 0.5f + 20f, 0f, 14f, 14f);
                }

                Text sPriceTxt = CreateGO<Text>("Price", sPriceBar.transform);
                sPriceTxt.text = $"+{entry.yieldSellPrice}";
                sPriceTxt.font = font;
                sPriceTxt.fontSize = 12;
                sPriceTxt.fontStyle = FontStyle.Bold;
                sPriceTxt.color = new Color(0.18f, 0.35f, 0.15f, 1f);
                sPriceTxt.alignment = TextAnchor.MiddleCenter;
                Rect(sPriceTxt, -8f, 0f, 44f, 20f);

                // Owned count badge on card
                int initCount = _inventory != null ? _inventory.GetItemCount(entry.yieldItemName) : 0;
                Text sOwned = CreateGO<Text>("OwnedCount", sPriceBar.transform);
                sOwned.text = $"\u00d7{initCount}";
                sOwned.font = font;
                sOwned.fontSize = 11;
                sOwned.fontStyle = FontStyle.Bold;
                sOwned.color = new Color(0.35f, 0.25f, 0.18f, 1f);
                sOwned.alignment = TextAnchor.MiddleRight;
                Rect(sOwned, 18f, 0f, 30f, 20f);
                _ownedCountLabels.Add(sOwned);

                _sellButtonRects.Add(sCardRt);
                _sellCountLabels.Add(sPriceTxt);

                int capturedJ = i;
                sellCardGo.GetComponent<Button>().onClick.AddListener(
                    () => SellYield(_shopEntries[capturedJ], _sellButtonRects[capturedJ], _sellCountLabels[capturedJ]));
            }

            SwitchTab(0);

            // Bottom hint
            Text help = CreateGO<Text>("HelpText", _panelGo.transform);
            help.text = "Press  'P'  to close";
            help.font = font;
            help.fontSize = 12;
            help.fontStyle = FontStyle.Italic;
            help.color = new Color(0.92f, 0.85f, 0.78f, 0.85f);
            help.alignment = TextAnchor.MiddleCenter;
            Rect(help, 0f, -halfH + 18f, 220f, 22f);
        }

        /// Builds the permanent gold HUD anchored to the top-left corner of the screen.
        private void CreateGoldHud(Sprite bgSprite, Font font)
        {
            GameObject hudGo = new GameObject("GoldHUD", typeof(RectTransform));
            hudGo.transform.SetParent(_canvasGo.transform, false);

            CanvasGroup cg = hudGo.AddComponent<CanvasGroup>();
            if (UI.MainMenuUI.Instance != null && !UI.MainMenuUI.HasGameStarted)
            {
                cg.alpha = 0f;
            }

            RectTransform hudRect = (RectTransform)hudGo.transform;
            hudRect.anchorMin        = new Vector2(0f, 1f);
            hudRect.anchorMax        = new Vector2(0f, 1f);
            hudRect.pivot            = new Vector2(0f, 1f);
            hudRect.anchoredPosition = new Vector2(18f, -14f);
            hudRect.sizeDelta        = new Vector2(154f, 44f);

            GameObject shadowGo = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            shadowGo.transform.SetParent(hudGo.transform, false);
            RectTransform shadowRt = (RectTransform)shadowGo.transform;
            shadowRt.anchorMin = Vector2.zero; shadowRt.anchorMax = Vector2.one;
            shadowRt.offsetMin = new Vector2(-3f, -3f); shadowRt.offsetMax = new Vector2(3f, 2f);
            Image shadowImg = shadowGo.GetComponent<Image>();
            shadowImg.sprite = UIResourceHelper.GetBackgroundSprite();
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = new Color(0f, 0f, 0f, 0.45f);

            GameObject woodGo = new GameObject("WoodFrame", typeof(RectTransform), typeof(Image));
            woodGo.transform.SetParent(hudGo.transform, false);
            RectTransform woodRt = (RectTransform)woodGo.transform;
            woodRt.anchorMin = Vector2.zero; woodRt.anchorMax = Vector2.one;
            woodRt.offsetMin = Vector2.zero; woodRt.offsetMax = Vector2.zero;
            Image woodImg = woodGo.GetComponent<Image>();
            woodImg.sprite = UIResourceHelper.GetBackgroundSprite();
            woodImg.type = Image.Type.Sliced;
            woodImg.color = new Color(0.24f, 0.17f, 0.11f, 0.96f);

            GameObject trimGo = new GameObject("GoldTrim", typeof(RectTransform), typeof(Image));
            trimGo.transform.SetParent(woodGo.transform, false);
            RectTransform trimRt = (RectTransform)trimGo.transform;
            trimRt.anchorMin = Vector2.zero; trimRt.anchorMax = Vector2.one;
            trimRt.offsetMin = new Vector2(2f, 2f); trimRt.offsetMax = new Vector2(-2f, -2f);
            Image trimImg = trimGo.GetComponent<Image>();
            trimImg.sprite = UIResourceHelper.GetBackgroundSprite();
            trimImg.type = Image.Type.Sliced;
            trimImg.color = new Color(0.78f, 0.62f, 0.32f, 0.75f);

            GameObject innerGo = new GameObject("InnerBacking", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(trimGo.transform, false);
            RectTransform innerRt = (RectTransform)innerGo.transform;
            innerRt.anchorMin = Vector2.zero; innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(2f, 2f); innerRt.offsetMax = new Vector2(-2f, -2f);
            Image innerImg = innerGo.GetComponent<Image>();
            innerImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            innerImg.type = Image.Type.Sliced;
            innerImg.color = new Color(0.09f, 0.08f, 0.07f, 0.96f);

            if (_coinSprite != null)
            {
                GameObject coinGlow = new GameObject("CoinGlow", typeof(RectTransform), typeof(Image));
                coinGlow.transform.SetParent(innerGo.transform, false);
                RectTransform cgRt = (RectTransform)coinGlow.transform;
                cgRt.anchorMin = new Vector2(0f, 0.5f);
                cgRt.anchorMax = new Vector2(0f, 0.5f);
                cgRt.pivot = new Vector2(0.5f, 0.5f);
                cgRt.anchoredPosition = new Vector2(20f, 0f);
                cgRt.sizeDelta = new Vector2(30f, 30f);
                Image glowImg = coinGlow.GetComponent<Image>();
                glowImg.sprite = UIResourceHelper.GetCircleSprite();
                glowImg.color = new Color(1f, 0.85f, 0.2f, 0.25f);

                GameObject coinGo = new GameObject("HUDCoin", typeof(RectTransform), typeof(Image));
                coinGo.transform.SetParent(innerGo.transform, false);
                RectTransform coinRt = (RectTransform)coinGo.transform;
                coinRt.anchorMin = new Vector2(0f, 0.5f);
                coinRt.anchorMax = new Vector2(0f, 0.5f);
                coinRt.pivot = new Vector2(0.5f, 0.5f);
                coinRt.anchoredPosition = new Vector2(20f, 0f);
                coinRt.sizeDelta = new Vector2(24f, 24f);

                Image coinImg = coinGo.GetComponent<Image>();
                coinImg.sprite = _coinSprite;
                coinImg.preserveAspect = true;
            }

            GameObject txtGo = new GameObject("HUDGoldText", typeof(RectTransform));
            txtGo.transform.SetParent(innerGo.transform, false);
            RectTransform txtRt = (RectTransform)txtGo.transform;
            txtRt.anchorMin = new Vector2(0f, 0f);
            txtRt.anchorMax = new Vector2(1f, 1f);
            txtRt.pivot = new Vector2(0f, 0.5f);
            txtRt.offsetMin = new Vector2(38f, 0f);
            txtRt.offsetMax = new Vector2(-10f, 0f);

            _hudGoldText = txtGo.AddComponent<Text>();
            _hudGoldText.font = font;
            _hudGoldText.fontSize = 18;
            _hudGoldText.fontStyle = FontStyle.Bold;
            _hudGoldText.color = new Color(1f, 0.88f, 0.45f, 1f);
            _hudGoldText.alignment = TextAnchor.MiddleLeft;
            _hudGoldText.text = "0";
            _hudGoldRect = hudGo.GetComponent<RectTransform>();
        }


        /// Creates a child GameObject with component T and returns T.
        private T CreateGO<T>(string name, Transform parent) where T : Component
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go.AddComponent<T>();
        }

        /// Sets anchored position and size on the RectTransform of a Component.
        private void Rect(Component c, float x, float y, float w, float h)
        {
            RectTransform rt = c.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta        = new Vector2(w, h);
        }

        /// Creates a styled button and returns its RectTransform.
        private RectTransform CreateButton(
            string name, Transform parent,
            string label, Font font,
            Color color,
            float x, float y, float w, float h,
            Sprite bgSprite)
        {
	            Image img = CreateGO<Image>(name, parent);
	            img.sprite = bgSprite;
	            img.type   = (bgSprite != null && bgSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
	            img.color  = color;
            Rect(img, x, y, w, h);

            img.gameObject.AddComponent<Button>();
            img.gameObject.AddComponent<UIHoverScale>();

            Text txt = CreateGO<Text>(name + "_Label", img.transform);
            txt.text      = label;
            txt.font      = font;
            txt.fontSize  = 14;
            txt.fontStyle = FontStyle.Bold;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            Rect(txt, 0f, 0f, w, h);

            return img.GetComponent<RectTransform>();
        }
    }
}
