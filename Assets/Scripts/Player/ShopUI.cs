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
        // ─── Shop catalogue entry ─────────────────────────────────────────────
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

        // ─── Inspector fields ─────────────────────────────────────────────────
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

        // ─── Private state ────────────────────────────────────────────────────
        private InventoryManager _inventory;
        private GameObject       _canvasGo;
        private GameObject       _panelGo;
        private bool             _isOpen           = false;
        private Coroutine        _bounceCoroutine;

        // Button rects — kept in entry order for fly-animation origins
        private readonly List<RectTransform> _buyButtonRects  = new List<RectTransform>();
        private readonly List<RectTransform> _sellButtonRects = new List<RectTransform>();

        // Sell-count Text references; index matches _shopEntries index
        private readonly List<Text> _sellCountLabels = new List<Text>();

        // Tab UI state
        private GameObject _buyContent;
        private GameObject _sellContent;
        private int _activeTab = 0;          // 0 = Buy, 1 = Sell
        private Image _buyTabImg;
        private Image _sellTabImg;
        private readonly List<Text> _ownedCountLabels = new List<Text>();

        // Permanent gold HUD
        private Text          _hudGoldText;
        private RectTransform _hudGoldRect;
        private Coroutine     _hudPulseCoroutine;
        private int           _lastGoldAmount = -1;

        public bool IsOpen => _isOpen;

        // ─── Layout constants ─────────────────────────────────────────────────
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

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void EnsureDefaultShopEntries()
        {
            if (_shopEntries != null && _shopEntries.Length > 0) return;

            _shopEntries = new ShopEntry[]
            {
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
                    seedItemName = "",
                    seedSprite = null,
                    seedBuyPrice = 0,
                    yieldItemName = "Wood",
                    yieldSprite = UIResourceHelper.GetItemIconSprite("Wood"),
                    yieldSellPrice = 10
                }
            };
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
            // Toggle shop on P or B key — gated so the background trigger doesn't open a
            // competing UI panel while the dev console has focus. UpdateHudGold
            // below runs unconditionally so the gold readout stays current.
            if (!InputReader.BlockGameplayInput &&
                Keyboard.current != null &&
                (Keyboard.current.pKey.wasPressedThisFrame || Keyboard.current.bKey.wasPressedThisFrame))
            {
                Debug.Log("[InputDebug] P/B key pressed -> Toggling Shop UI.");
                ToggleUI();
            }

            UpdateHudGold();
        }

        // ─── Open / Close ─────────────────────────────────────────────────────

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

        // ─── Permanent gold HUD ───────────────────────────────────────────────

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

        // ─── Sell-count labels ────────────────────────────────────────────────

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

        // ─── Transactions ─────────────────────────────────────────────────────

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

            // Success: scale-pop + sparkle burst
            StartCoroutine(ScalePopAnimation(buyBtnRect));
            SpawnSparkleBurst(buyBtnRect.position);

            // Fly seed icon from buy button toward inventory
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

            // Update sell label immediately so it stays in sync
            if (sellCountLabel != null)
                sellCountLabel.text = FormatSellLabel(entry.yieldSellPrice, remaining);

            // Update owned count label for this entry
            int idx = Array.IndexOf(_shopEntries, entry);
            if (idx >= 0 && idx < _ownedCountLabels.Count && _ownedCountLabels[idx] != null)
                _ownedCountLabels[idx].text = $"\u00d7{remaining}";

            // Fly coin from sell button to the permanent HUD gold display
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

        // ─── Button animations ────────────────────────────────────────────────

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

                // Fade back to original color in the second half of the shake
                if (img != null && t > 0.5f)
                    img.color = Color.Lerp(Color.red, originalColor, (t - 0.5f) / 0.5f);

                yield return null;
            }

            rt.anchoredPosition = originalPos;
            if (img != null) img.color = originalColor;
        }

        // ─── Sparkle burst ────────────────────────────────────────────────────

        /// Spawns 5 small golden squares that fly outward and fade from the given screen position.
        private void SpawnSparkleBurst(Vector2 screenPos)
        {
            RectTransform canvasRect = _canvasGo.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, null, out Vector2 localPos);

            // Five distinct gold/yellow tints for variety
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

                // Evenly distribute outward directions with a small phase offset
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

        private void SwitchTab(int tab)
        {
            _activeTab = tab;
            if (_buyContent  != null) _buyContent.SetActive(tab == 0);
            if (_sellContent != null) _sellContent.SetActive(tab == 1);

            Color buyActive  = new Color(0.75f, 0.48f, 0.22f, 1f);
            Color sellActive = new Color(0.35f, 0.58f, 0.32f, 1f);
            Color dimmed     = new Color(0.18f, 0.18f, 0.18f, 0.80f);

            if (_buyTabImg  != null) _buyTabImg.color  = tab == 0 ? buyActive  : dimmed;
            if (_sellTabImg != null) _sellTabImg.color = tab == 1 ? sellActive : dimmed;
        }

        //  Prefab binding (no programmatic layout) 
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

            // Tabs (optional)
            var buyTabBtn = UIResourceHelper.FindChildComponentByName<Button>(_prefabPanelRoot, new[] { "BuyTab", "Buy Seeds", "Buy" });
            var sellTabBtn = UIResourceHelper.FindChildComponentByName<Button>(_prefabPanelRoot, new[] { "SellTab", "Sell Crops", "Sell" });
            _buyTabImg = buyTabBtn != null ? buyTabBtn.GetComponent<Image>() : null;
            _sellTabImg = sellTabBtn != null ? sellTabBtn.GetComponent<Image>() : null;
            if (buyTabBtn != null) buyTabBtn.onClick.AddListener(() => SwitchTab(0));
            if (sellTabBtn != null) sellTabBtn.onClick.AddListener(() => SwitchTab(1));

            // Permanent HUD gold binding
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

                // ---- Buy row ----
                Transform buyRow = (_buyContentRoot != null && i < _buyContentRoot.childCount) ? _buyContentRoot.GetChild(i) : null;
                if (buyRow == null)
                {
                    Debug.LogWarning($"[ShopUI] Missing Buy row {i} under prefab root. Expected child index {i}.");
                }
                else
                {
                    // Icon
                    var buyIcon = UIResourceHelper.FindChildComponentByName<Image>(buyRow, new[] { "BuyIcon", "Icon", "SeedIcon" });
                    if (buyIcon != null)
                    {
                        buyIcon.sprite = entry.seedSprite != null ? entry.seedSprite : entry.yieldSprite;
                        buyIcon.preserveAspect = true;
                        buyIcon.color = buyIcon.sprite != null ? Color.white : new Color(0.6f, 0.5f, 0.3f, 0.6f);
                    }

                    // Name
                    var buyName = UIResourceHelper.FindChildComponentByName<Text>(buyRow, new[] { "BuyName", "Name", "ItemName" });
                    if (buyName != null) buyName.text = entry.seedItemName;

                    // Button
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

                // ---- Sell row ----
                Transform sellRow = (_sellContentRoot != null && i < _sellContentRoot.childCount) ? _sellContentRoot.GetChild(i) : null;
                if (sellRow == null)
                {
                    Debug.LogWarning($"[ShopUI] Missing Sell row {i} under prefab root. Expected child index {i}.");
                }
                else
                {
                    // Icon
                    var sellIcon = UIResourceHelper.FindChildComponentByName<Image>(sellRow, new[] { "SellIcon", "Icon", "YieldIcon" });
                    if (sellIcon != null)
                    {
                        sellIcon.sprite = entry.yieldSprite != null ? entry.yieldSprite : entry.seedSprite;
                        sellIcon.preserveAspect = true;
                        sellIcon.color = sellIcon.sprite != null ? Color.white : new Color(0.6f, 0.5f, 0.3f, 0.6f);
                    }

                    // Name
                    var sellName = UIResourceHelper.FindChildComponentByName<Text>(sellRow, new[] { "SellName", "Name", "ItemName" });
                    if (sellName != null) sellName.text = entry.yieldItemName;

                    // Owned count
                    int initCount = _inventory != null ? _inventory.GetItemCount(entry.yieldItemName) : 0;
                    var owned = UIResourceHelper.FindChildComponentByName<Text>(sellRow, new[] { "OwnedCount", "Owned", "Count" });
                    if (owned != null) owned.text = $"\u00d7{initCount}";
                    _ownedCountLabels.Add(owned);

                    // Button
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

            // Default to Buy tab visible
            SwitchTab(0);
        }


        //  UI construction 

        private void CreateShopUI()
        {
            // ── Canvas + EventSystem (shared helper handles scaler + raycaster + input module) ──
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            _canvasGo = canvas != null ? canvas.gameObject : null;
            UIResourceHelper.EnsureEventSystem();

	            Sprite bgSprite = _shopPanelFrameSprite != null ? _shopPanelFrameSprite : UIResourceHelper.GetBackgroundSprite();
	            Font   font     = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int    entries  = _shopEntries != null ? _shopEntries.Length : 0;
            float  panelH   = HEADER_H + entries * ROW_HEIGHT + FOOTER_H;
            float  halfH    = panelH * 0.5f;

            // ── Permanent gold HUD (always visible, even when shop is closed) ─
            CreateGoldHud(bgSprite, font);

            // ── Panel ─────────────────────────────────────────────────────────
            _panelGo = new GameObject("ShopPanel");
            _panelGo.transform.SetParent(_canvasGo.transform, false);
            RectTransform panelRect   = _panelGo.AddComponent<RectTransform>();
            panelRect.sizeDelta       = new Vector2(PANEL_WIDTH, panelH);
            panelRect.anchoredPosition = Vector2.zero;

            // Panel background
            Image bg = CreateGO<Image>("Background", _panelGo.transform);
	            bg.sprite = bgSprite;
	            bg.type   = (bgSprite != null && bgSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
	            bg.color  = bg.type == Image.Type.Sliced ? Color.white : new Color(0.14f, 0.12f, 0.10f, 0.96f);
            Rect(bg, 0f, 0f, PANEL_WIDTH, panelH);

            // ── Header ────────────────────────────────────────────────────────
            Text title = CreateGO<Text>("ShopTitle", _panelGo.transform);
            title.text      = "Meadow Shop";
            title.font      = font;
            title.fontSize  = 26;
            title.fontStyle = FontStyle.Bold;
            title.color     = new Color(1f, 0.84f, 0f, 1f);
            title.alignment = TextAnchor.MiddleCenter;
            Rect(title, 0f, halfH - 38f, 300f, 44f);   // top of panel

            // ── Tab buttons ───────────────────────────────────────────────────
	            RectTransform buyTabRect = CreateButton(
	                "BuyTab", _panelGo.transform,
	                "Buy Seeds", font,
	                new Color(0.75f, 0.48f, 0.22f, 1f),
	                -100f, halfH - 85f, 160f, 38f, _buttonSprite != null ? _buttonSprite : bgSprite);
            _buyTabImg = buyTabRect.GetComponent<Image>();
            buyTabRect.GetComponent<Button>().onClick.AddListener(() => SwitchTab(0));

	            RectTransform sellTabRect = CreateButton(
	                "SellTab", _panelGo.transform,
	                "Sell Crops", font,
	                new Color(0.35f, 0.58f, 0.32f, 1f),
	                100f, halfH - 85f, 160f, 38f, _buttonSprite != null ? _buttonSprite : bgSprite);
            _sellTabImg = sellTabRect.GetComponent<Image>();
            sellTabRect.GetComponent<Button>().onClick.AddListener(() => SwitchTab(1));

            // Divider — sits below the tab buttons
            Image divider = CreateGO<Image>("Divider", _panelGo.transform);
            divider.color = new Color(1f, 1f, 1f, 0.10f);
            Rect(divider, 0f, halfH - 118f, PANEL_WIDTH - 40f, 2f);

            // Content containers
            _buyContent = new GameObject("BuyContent");
            _buyContent.transform.SetParent(_panelGo.transform, false);
            _buyContent.AddComponent<RectTransform>();

            _sellContent = new GameObject("SellContent");
            _sellContent.transform.SetParent(_panelGo.transform, false);
            _sellContent.AddComponent<RectTransform>();

            // ── Rows (one per ShopEntry) ──────────────────────────────────────
            _buyButtonRects.Clear();
            _sellButtonRects.Clear();
            _sellCountLabels.Clear();
            _ownedCountLabels.Clear();

            for (int i = 0; i < entries; i++)
            {
                ShopEntry entry = _shopEntries[i];
                float rowY = halfH - HEADER_H - i * ROW_HEIGHT - ROW_HEIGHT * 0.5f;

                // ── Buy row ───────────────────────────────────────────────────
                if (i % 2 == 0)
                {
                    Image rowBg = CreateGO<Image>($"BuyRowBg_{i}", _buyContent.transform);
                    rowBg.color = new Color(1f, 1f, 1f, 0.04f);
                    Rect(rowBg, 0f, rowY, PANEL_WIDTH - 30f, ROW_HEIGHT - 4f);
                }

                Image buyIcon = CreateGO<Image>($"BuyIcon_{i}", _buyContent.transform);
                buyIcon.sprite = entry.seedSprite != null ? entry.seedSprite : entry.yieldSprite;
                buyIcon.color  = buyIcon.sprite != null ? Color.white : new Color(0.6f, 0.5f, 0.3f, 0.6f);
                buyIcon.preserveAspect = true;
                Rect(buyIcon, -290f, rowY, 48f, 48f);

                Text buyNameLabel = CreateGO<Text>($"BuyName_{i}", _buyContent.transform);
                buyNameLabel.text      = entry.seedItemName;
                buyNameLabel.font      = font;
                buyNameLabel.fontSize  = 16;
                buyNameLabel.color     = Color.white;
                buyNameLabel.alignment = TextAnchor.MiddleLeft;
                Rect(buyNameLabel, -130f, rowY, 260f, ROW_HEIGHT);

	                string buyBtnLabel = entry.seedBuyPrice <= 0 ? "Free" : $"{entry.seedBuyPrice}g";
	                RectTransform buyRect = CreateButton(
	                    $"BuyBtn_{i}", _buyContent.transform,
	                    buyBtnLabel, font,
	                    new Color(0.75f, 0.48f, 0.22f, 1f),
	                    195f, rowY, 160f, 42f,
	                    _buttonSprite != null ? _buttonSprite : bgSprite);
                _buyButtonRects.Add(buyRect);

                int capturedI = i;
                buyRect.GetComponent<Button>().onClick.AddListener(
                    () => BuySeed(_shopEntries[capturedI], _buyButtonRects[capturedI]));

                // ── Sell row ──────────────────────────────────────────────────
                if (i % 2 == 0)
                {
                    Image sellRowBg = CreateGO<Image>($"SellRowBg_{i}", _sellContent.transform);
                    sellRowBg.color = new Color(1f, 1f, 1f, 0.04f);
                    Rect(sellRowBg, 0f, rowY, PANEL_WIDTH - 30f, ROW_HEIGHT - 4f);
                }

                Image sellIcon = CreateGO<Image>($"SellIcon_{i}", _sellContent.transform);
                sellIcon.sprite = entry.yieldSprite != null ? entry.yieldSprite : entry.seedSprite;
                sellIcon.color  = sellIcon.sprite != null ? Color.white : new Color(0.6f, 0.5f, 0.3f, 0.6f);
                sellIcon.preserveAspect = true;
                Rect(sellIcon, -290f, rowY, 48f, 48f);

                Text sellNameLabel = CreateGO<Text>($"SellName_{i}", _sellContent.transform);
                sellNameLabel.text      = entry.yieldItemName;
                sellNameLabel.font      = font;
                sellNameLabel.fontSize  = 16;
                sellNameLabel.color     = Color.white;
                sellNameLabel.alignment = TextAnchor.MiddleLeft;
                Rect(sellNameLabel, -130f, rowY, 200f, ROW_HEIGHT);

                int initCount = _inventory != null ? _inventory.GetItemCount(entry.yieldItemName) : 0;
                Text ownedLabel = CreateGO<Text>($"OwnedCount_{i}", _sellContent.transform);
                ownedLabel.text      = $"\u00d7{initCount}";
                ownedLabel.font      = font;
                ownedLabel.fontSize  = 14;
                ownedLabel.color     = new Color(0.75f, 0.75f, 0.75f, 1f);
                ownedLabel.alignment = TextAnchor.MiddleRight;
                Rect(ownedLabel, 50f, rowY, 80f, ROW_HEIGHT);
                _ownedCountLabels.Add(ownedLabel);

	                RectTransform sellRect = CreateButton(
	                    $"SellBtn_{i}", _sellContent.transform,
	                    $"+{entry.yieldSellPrice}g", font,
	                    new Color(0.35f, 0.58f, 0.32f, 1f),
	                    220f, rowY, 145f, 42f,
	                    _buttonSprite != null ? _buttonSprite : bgSprite);
                _sellButtonRects.Add(sellRect);

                Text sellLabelText = sellRect.GetComponentInChildren<Text>();
                _sellCountLabels.Add(sellLabelText);

                int j = i;
                sellRect.GetComponent<Button>().onClick.AddListener(
                    () => SellYield(_shopEntries[j], _sellButtonRects[j], _sellCountLabels[j]));
            }

            SwitchTab(0);

            // ── Footer ────────────────────────────────────────────────────────
            Text help = CreateGO<Text>("HelpText", _panelGo.transform);
            help.text      = "Press  'P'  to close";
            help.font      = font;
            help.fontSize  = 13;
            help.color     = new Color(0.55f, 0.55f, 0.55f, 1f);
            help.alignment = TextAnchor.MiddleCenter;
            Rect(help, 0f, -halfH + 26f, 240f, 26f);
        }

        /// Builds the permanent gold HUD anchored to the top-right corner of the screen.
        private void CreateGoldHud(Sprite bgSprite, Font font)
        {
            GameObject hudGo = new GameObject("GoldHUD");
            hudGo.transform.SetParent(_canvasGo.transform, false);

            // Image component auto-creates RectTransform when none exists
            Image hudBg = hudGo.AddComponent<Image>();
            hudBg.sprite = bgSprite;
            hudBg.type   = Image.Type.Sliced;
            hudBg.color  = new Color(0.14f, 0.12f, 0.10f, 0.92f);

            // Anchor top-right; pivot top-right; 20 px inset from corner
            RectTransform hudRect = hudGo.GetComponent<RectTransform>();
            hudRect.anchorMin        = new Vector2(1f, 1f);
            hudRect.anchorMax        = new Vector2(1f, 1f);
            hudRect.pivot            = new Vector2(1f, 1f);
            hudRect.anchoredPosition = new Vector2(-20f, -20f);
            hudRect.sizeDelta        = new Vector2(200f, 54f);

            // Children use the default center anchor (0.5, 0.5) relative to the HUD's centre.
            // HUD is 200×54, so centre-relative offsets below place elements inside its bounds.

            // Coin icon (left side of HUD)
            if (_coinSprite != null)
            {
                Image coinImg = CreateGO<Image>("HUDCoin", hudGo.transform);
                coinImg.sprite = _coinSprite;
                coinImg.preserveAspect = true;
                Rect(coinImg, -72f, 0f, 36f, 36f);
            }

            // Gold amount text (right of coin)
            _hudGoldText = CreateGO<Text>("HUDGoldText", hudGo.transform);
            _hudGoldText.font      = font;
            _hudGoldText.fontSize  = 20;
            _hudGoldText.fontStyle = FontStyle.Bold;
            _hudGoldText.color     = new Color(1f, 0.84f, 0f, 1f);
            _hudGoldText.alignment = TextAnchor.MiddleLeft;
            _hudGoldText.text      = "0";
            _hudGoldRect = _hudGoldText.GetComponent<RectTransform>();
            Rect(_hudGoldText, 22f, 0f, 110f, 40f);
        }

        // ─── UI helpers ───────────────────────────────────────────────────────

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
