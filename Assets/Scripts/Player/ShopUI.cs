using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace Willowstead.Player
{
    /// <summary>
    /// Handles the visual display and transaction logic of the Seed Shop.
    /// Programmatically constructs a Canvas overlay with options to buy seeds (free & infinite)
    /// and sell carrots (for 25 gold), styled to match the dark slate-brown aesthetic.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        [Header("Assets")]
        [Tooltip("The sprite to represent Carrot Seeds.")]
        [SerializeField] private Sprite _seedSprite;

        [Tooltip("The sprite to represent Carrots.")]
        [SerializeField] private Sprite _carrotSprite;

        [Tooltip("The circle/coin sprite to represent Gold Coins.")]
        [SerializeField] private Sprite _coinSprite;

        private InventoryManager _inventory;
        private GameObject _canvasGo;
        private GameObject _panelGo;
        
        private Text _goldText;
        private RectTransform _goldTextRect;
        private RectTransform _buyButtonRect;

        private bool _isOpen = false;
        private Coroutine _goldPulseCoroutine;

        public bool IsOpen => _isOpen;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_seedSprite == null) _seedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/CarrotSeed.png");
            if (_carrotSprite == null) _carrotSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Carrot.png");
            if (_coinSprite == null) _coinSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/gold coin.png");
        }
#endif

        private void Start()
        {
            _inventory = GetComponent<InventoryManager>();
            if (_inventory == null) _inventory = FindAnyObjectByType<InventoryManager>();

            // Load default coin sprite if not assigned
            if (_coinSprite == null) _coinSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/InputFieldBackground.psd");

            CreateShopUI();
            SetUIActive(false);
        }

        private void Update()
        {
            // Toggle Shop UI on P key press
            if (Keyboard.current != null)
            {
                if (Keyboard.current.pKey.wasPressedThisFrame)
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
                // Close Inventory UI to prevent overlapping
                InventoryUI invUI = FindAnyObjectByType<InventoryUI>();
                if (invUI != null) invUI.CloseUI();

                UpdateGoldDisplay();
            }
        }

        private void SetUIActive(bool active)
        {
            if (_panelGo != null)
            {
                _panelGo.SetActive(active);
            }
        }

        private void UpdateGoldDisplay()
        {
            if (_inventory == null || _goldText == null) return;
            int goldCount = _inventory.GetItemCount("Gold");
            _goldText.text = $"Gold: {goldCount}";
        }

        private void BuySeeds()
        {
            if (_inventory == null) return;

            // Seeds are Free & Infinite for dev purposes!
            // No gold checks or gold deduction.
            
            // Get Hotbar Seed Slot target
            HotbarUI hotbar = FindAnyObjectByType<HotbarUI>();
            RectTransform targetSlot = (hotbar != null) ? hotbar.SeedSlotRect : null;

            // Spawn flying seed starting from the Buy button and traveling to the hotbar slot
            Vector3 startWorldPos = Vector3.zero;
            if (_buyButtonRect != null)
            {
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, _buyButtonRect.position);
                startWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
                startWorldPos.z = 0f;
            }
            else
            {
                startWorldPos = transform.position; // Player position fallback
            }

            // Spawn seed icon (or fallback to coin sprite if no seed sprite assigned)
            Sprite icon = (_seedSprite != null) ? _seedSprite : _coinSprite;

            World.FlyingItemAnimation.Spawn(icon, startWorldPos, targetSlot, () =>
            {
                _inventory.AddItem("Carrot Seeds", 1);
                if (hotbar != null)
                {
                    hotbar.PulseSlot(2); // Pulse Carrot Seeds slot (index 2)
                }
            });
        }

        private void SellCarrots()
        {
            if (_inventory == null) return;

            // Deduct 1 carrot if available
            if (_inventory.GetItemCount("Carrot") > 0)
            {
                _inventory.RemoveItem("Carrot", 1);

                // Get Hotbar Carrot Slot (start point)
                HotbarUI hotbar = FindAnyObjectByType<HotbarUI>();
                RectTransform startSlot = (hotbar != null) ? hotbar.CarrotSlotRect : null;

                Vector3 startWorldPos = Vector3.zero;
                if (startSlot != null)
                {
                    Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, startSlot.position);
                    startWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
                    startWorldPos.z = 0f;
                }
                else
                {
                    startWorldPos = new Vector3(0f, -3f, 0f);
                }

                // Spawn flying coin sprite colored Gold flying towards the shop balance display
                Sprite coin = _coinSprite;
                Color goldColor = new Color(1.0f, 0.82f, 0.0f, 1f); // Vibrant Gold coin tint

                World.FlyingItemAnimation.Spawn(coin, startWorldPos, _goldTextRect, () =>
                {
                    _inventory.AddItem("Gold", 25);
                    UpdateGoldDisplay();
                    
                    // Bounce gold balance text on arrival
                    if (_goldPulseCoroutine != null) StopCoroutine(_goldPulseCoroutine);
                    _goldPulseCoroutine = StartCoroutine(PulseGoldText());
                }, goldColor);
            }
        }

        private System.Collections.IEnumerator PulseGoldText()
        {
            float duration = 0.16f;
            float elapsed = 0f;
            Vector3 originalScale = Vector3.one;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percent = elapsed / duration;
                float scaleFactor = 1.0f + Mathf.Sin(percent * Mathf.PI) * 0.28f;
                if (_goldTextRect != null) _goldTextRect.localScale = originalScale * scaleFactor;
                yield return null;
            }

            if (_goldTextRect != null) _goldTextRect.localScale = originalScale;
            _goldPulseCoroutine = null;
        }

        private void CreateShopUI()
        {
            // Canvas
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
            Sprite btnBg = Resources.GetBuiltinResource<Sprite>("UI/Skin/InputFieldBackground.psd");

            // Panel parent
            _panelGo = new GameObject("ShopPanel");
            _panelGo.transform.SetParent(_canvasGo.transform, false);

            RectTransform panelRect = _panelGo.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(380f, 260f);
            panelRect.anchoredPosition = Vector2.zero; // Centered

            // Background panel image
            GameObject bgGo = new GameObject("BackgroundPanel");
            bgGo.transform.SetParent(_panelGo.transform, false);
            Image bgImage = bgGo.AddComponent<Image>();
            bgImage.sprite = roundedBg;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.14f, 0.12f, 0.1f, 0.95f); // Slate dark brown matching Inventory

            RectTransform bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(380f, 260f);
            bgRect.anchoredPosition = Vector2.zero;

            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Header Title
            GameObject titleGo = new GameObject("ShopTitle");
            titleGo.transform.SetParent(_panelGo.transform, false);
            Text titleText = titleGo.AddComponent<Text>();
            titleText.text = "Meadow Shop";
            titleText.font = legacyFont;
            titleText.fontSize = 20;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(1f, 0.84f, 0f, 1f); // Gold title
            titleText.alignment = TextAnchor.MiddleCenter;

            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 100f);
            titleRect.sizeDelta = new Vector2(200f, 30f);

            // Gold Balance Text
            GameObject goldGo = new GameObject("GoldText");
            goldGo.transform.SetParent(_panelGo.transform, false);
            _goldText = goldGo.AddComponent<Text>();
            _goldText.text = "Gold: 100";
            _goldText.font = legacyFont;
            _goldText.fontSize = 15;
            _goldText.fontStyle = FontStyle.Bold;
            _goldText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            _goldText.alignment = TextAnchor.MiddleRight;
            _goldTextRect = goldGo.GetComponent<RectTransform>();
            _goldTextRect.anchoredPosition = new Vector2(100f, 100f);
            _goldTextRect.sizeDelta = new Vector2(120f, 30f);

            // ROW 1: Buy Seeds (Free & Infinite)
            GameObject row1Go = new GameObject("Row1_BuySeeds");
            row1Go.transform.SetParent(_panelGo.transform, false);
            RectTransform row1Rect = row1Go.AddComponent<RectTransform>();
            row1Rect.anchoredPosition = new Vector2(0f, 30f);
            row1Rect.sizeDelta = new Vector2(340f, 50f);

            // Seed Icon
            GameObject seedIconGo = new GameObject("SeedIcon");
            seedIconGo.transform.SetParent(row1Rect, false);
            Image seedIconImg = seedIconGo.AddComponent<Image>();
            seedIconImg.sprite = _seedSprite;
            if (_seedSprite == null) seedIconImg.color = new Color(0.7f, 0.6f, 0.45f, 0.85f);
            RectTransform seedIconRect = seedIconGo.GetComponent<RectTransform>();
            seedIconRect.anchoredPosition = new Vector2(-130f, 0f);
            seedIconRect.sizeDelta = new Vector2(35f, 35f);

            // Seed Text Label
            GameObject seedLabelGo = new GameObject("SeedLabel");
            seedLabelGo.transform.SetParent(row1Rect, false);
            Text seedLabelText = seedLabelGo.AddComponent<Text>();
            seedLabelText.text = "Carrot Seeds (Free)";
            seedLabelText.font = legacyFont;
            seedLabelText.fontSize = 14;
            seedLabelText.color = Color.white;
            seedLabelText.alignment = TextAnchor.MiddleLeft;
            RectTransform seedLabelRect = seedLabelGo.GetComponent<RectTransform>();
            seedLabelRect.anchoredPosition = new Vector2(-30f, 0f);
            seedLabelRect.sizeDelta = new Vector2(150f, 30f);

            // Buy Button
            GameObject buyBtnGo = new GameObject("BuyButton");
            buyBtnGo.transform.SetParent(row1Rect, false);
            Image buyBtnImg = buyBtnGo.AddComponent<Image>();
            buyBtnImg.sprite = btnBg;
            buyBtnImg.type = Image.Type.Sliced;
            buyBtnImg.color = new Color(0.75f, 0.48f, 0.22f, 1f); // Warm Orange Brown
            _buyButtonRect = buyBtnGo.GetComponent<RectTransform>();
            _buyButtonRect.anchoredPosition = new Vector2(100f, 0f);
            _buyButtonRect.sizeDelta = new Vector2(70f, 30f);

            Button buyBtn = buyBtnGo.AddComponent<Button>();
            buyBtn.onClick.AddListener(BuySeeds);

            GameObject buyBtnTxtGo = new GameObject("BuyText");
            buyBtnTxtGo.transform.SetParent(_buyButtonRect, false);
            Text buyBtnTxt = buyBtnTxtGo.AddComponent<Text>();
            buyBtnTxt.text = "Buy";
            buyBtnTxt.font = legacyFont;
            buyBtnTxt.fontSize = 13;
            buyBtnTxt.fontStyle = FontStyle.Bold;
            buyBtnTxt.color = Color.white;
            buyBtnTxt.alignment = TextAnchor.MiddleCenter;
            RectTransform buyBtnTxtRect = buyBtnTxtGo.GetComponent<RectTransform>();
            buyBtnTxtRect.anchoredPosition = Vector2.zero;
            buyBtnTxtRect.sizeDelta = new Vector2(70f, 30f);


            // ROW 2: Sell Carrots (+25 Gold)
            GameObject row2Go = new GameObject("Row2_SellCarrots");
            row2Go.transform.SetParent(_panelGo.transform, false);
            RectTransform row2Rect = row2Go.AddComponent<RectTransform>();
            row2Rect.anchoredPosition = new Vector2(0f, -40f);
            row2Rect.sizeDelta = new Vector2(340f, 50f);

            // Carrot Icon
            GameObject carrotIconGo = new GameObject("CarrotIcon");
            carrotIconGo.transform.SetParent(row2Rect, false);
            Image carrotIconImg = carrotIconGo.AddComponent<Image>();
            carrotIconImg.sprite = _carrotSprite;
            if (_carrotSprite == null) carrotIconImg.color = new Color(0.95f, 0.5f, 0.15f, 0.85f);
            RectTransform carrotIconRect = carrotIconGo.GetComponent<RectTransform>();
            carrotIconRect.anchoredPosition = new Vector2(-130f, 0f);
            carrotIconRect.sizeDelta = new Vector2(35f, 35f);

            // Carrot Text Label
            GameObject carrotLabelGo = new GameObject("CarrotLabel");
            carrotLabelGo.transform.SetParent(row2Rect, false);
            Text carrotLabelText = carrotLabelGo.AddComponent<Text>();
            carrotLabelText.text = "Sell Carrots (+25g)";
            carrotLabelText.font = legacyFont;
            carrotLabelText.fontSize = 14;
            carrotLabelText.color = Color.white;
            carrotLabelText.alignment = TextAnchor.MiddleLeft;
            RectTransform carrotLabelRect = carrotLabelGo.GetComponent<RectTransform>();
            carrotLabelRect.anchoredPosition = new Vector2(-30f, 0f);
            carrotLabelRect.sizeDelta = new Vector2(150f, 30f);

            // Sell Button
            GameObject sellBtnGo = new GameObject("SellButton");
            sellBtnGo.transform.SetParent(row2Rect, false);
            Image sellBtnImg = sellBtnGo.AddComponent<Image>();
            sellBtnImg.sprite = btnBg;
            sellBtnImg.type = Image.Type.Sliced;
            sellBtnImg.color = new Color(0.35f, 0.58f, 0.32f, 1f); // Warm Grass Green
            RectTransform sellBtnRect = sellBtnGo.GetComponent<RectTransform>();
            sellBtnRect.anchoredPosition = new Vector2(100f, 0f);
            sellBtnRect.sizeDelta = new Vector2(70f, 30f);

            Button sellBtn = sellBtnGo.AddComponent<Button>();
            sellBtn.onClick.AddListener(SellCarrots);

            GameObject sellBtnTxtGo = new GameObject("SellText");
            sellBtnTxtGo.transform.SetParent(sellBtnRect, false);
            Text sellBtnTxt = sellBtnTxtGo.AddComponent<Text>();
            sellBtnTxt.text = "Sell";
            sellBtnTxt.font = legacyFont;
            sellBtnTxt.fontSize = 13;
            sellBtnTxt.fontStyle = FontStyle.Bold;
            sellBtnTxt.color = Color.white;
            sellBtnTxt.alignment = TextAnchor.MiddleCenter;
            RectTransform sellBtnTxtRect = sellBtnTxtGo.GetComponent<RectTransform>();
            sellBtnTxtRect.anchoredPosition = Vector2.zero;
            sellBtnTxtRect.sizeDelta = new Vector2(70f, 30f);


            // Footer instructions text
            GameObject helpGo = new GameObject("HelpInstructionsText");
            helpGo.transform.SetParent(_panelGo.transform, false);
            Text helpText = helpGo.AddComponent<Text>();
            helpText.text = "Press 'P' again to close the Shop";
            helpText.font = legacyFont;
            helpText.fontSize = 12;
            helpText.color = new Color(0.7f, 0.7f, 0.7f, 1.0f);
            helpText.alignment = TextAnchor.MiddleCenter;
            RectTransform helpRect = helpGo.GetComponent<RectTransform>();
            helpRect.anchoredPosition = new Vector2(0f, -105f);
            helpRect.sizeDelta = new Vector2(250f, 25f);
        }
    }
}
