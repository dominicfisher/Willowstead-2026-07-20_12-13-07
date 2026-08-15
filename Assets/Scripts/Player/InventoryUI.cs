using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Willowstead.Input;

namespace Willowstead.Player
{
    /// <summary>
    /// Programmatically constructs and manages the 2x8 Inventory UI panel (slots 8-23).
    /// Pure code-built UI: 100% self-contained, requires zero inspector prefabs or external scene wiring.
    /// Toggles via Tab or I key with an overshoot spring bounce animation.
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
        private bool _isOpen = false;
        private Coroutine _bounceCoroutine;

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
            if (Keyboard.current != null &&
                (Keyboard.current.iKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame))
            {
                if (InputReader.BlockGameplayInput)
                {
                    Debug.Log("[InputDebug] Tab/I key pressed, but BlockGameplayInput is TRUE (blocked by UI/Console).");
                }
                else
                {
                    Debug.Log("[InputDebug] Tab/I key pressed -> Toggling Inventory UI.");
                    ToggleUI();
                }
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
            Debug.Log($"[InputDebug] Inventory UI state changed. IsOpen: {_isOpen}");

            if (_isOpen)
            {
                ShopUI shopUI = FindAnyObjectByType<ShopUI>();
                if (shopUI != null) shopUI.CloseUI();

                RefreshUI();

                if (_panelGo != null)
                {
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
        }

        public void RefreshUI()
        {
            if (_inventory == null || _slotIcons == null) return;

            int goldCount = _inventory.GetItemCount("Gold");
            if (_goldText != null) _goldText.text = $"Gold: {goldCount}";

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

            _panelGo = new GameObject("InventoryPanel");
            _panelGo.transform.SetParent(_canvasGo.transform, false);

            RectTransform panelRect = _panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;

            float slotW = 56f;
            float slotH = 56f;
            float spacingX = 8f;
            float spacingY = 8f;
            float panelW = (8 * slotW) + (7 * spacingX) + 36f; // ~540px
            float panelH = (2 * slotH) + (1 * spacingY) + 110f; // ~238px
            panelRect.sizeDelta = new Vector2(panelW, panelH);

            Image panelBg = _panelGo.AddComponent<Image>();
            panelBg.sprite = UIResourceHelper.GetBackgroundSprite();
            panelBg.type = Image.Type.Sliced;
            panelBg.color = new Color(0.10f, 0.08f, 0.06f, 0.96f);

            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject titleGo = new GameObject("InventoryTitle");
            titleGo.transform.SetParent(_panelGo.transform, false);
            Text titleText = titleGo.AddComponent<Text>();
            titleText.text = "INVENTORY";
            titleText.font = legacyFont;
            titleText.fontSize = 20;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1.0f, 0.82f, 0.0f, 1f); // Gold title

            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -14f);
            titleRect.sizeDelta = new Vector2(200f, 30f);

            GameObject goldGo = new GameObject("InventoryGoldText");
            goldGo.transform.SetParent(_panelGo.transform, false);
            _goldText = goldGo.AddComponent<Text>();
            _goldText.text = "Gold: 100";
            _goldText.font = legacyFont;
            _goldText.fontSize = 15;
            _goldText.fontStyle = FontStyle.Bold;
            _goldText.alignment = TextAnchor.MiddleRight;
            _goldText.color = new Color(0.95f, 0.88f, 0.55f, 1f);

            RectTransform goldRect = goldGo.GetComponent<RectTransform>();
            goldRect.anchorMin = new Vector2(1f, 1f);
            goldRect.anchorMax = new Vector2(1f, 1f);
            goldRect.pivot = new Vector2(1f, 1f);
            goldRect.anchoredPosition = new Vector2(-18f, -14f);
            goldRect.sizeDelta = new Vector2(150f, 30f);

            GameObject gridGo = new GameObject("SlotsContainer");
            gridGo.transform.SetParent(_panelGo.transform, false);
            RectTransform gridRect = gridGo.AddComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.anchoredPosition = new Vector2(0f, -4f);
            gridRect.sizeDelta = new Vector2((8 * slotW) + (7 * spacingX), (2 * slotH) + (1 * spacingY));

            _slotIcons = new Image[16];
            _slotCountTexts = new Text[16];

            float startX = -(((8 * slotW) + (7 * spacingX)) * 0.5f) + (slotW * 0.5f);
            float startY = (((2 * slotH) + (1 * spacingY)) * 0.5f) - (slotH * 0.5f);

            int slotIdx = 0;
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    GameObject slotGo = new GameObject($"InventorySlot_{slotIdx}");
                    slotGo.transform.SetParent(gridGo.transform, false);

                    RectTransform slotRect = slotGo.AddComponent<RectTransform>();
                    slotRect.sizeDelta = new Vector2(slotW, slotH);
                    float posX = startX + c * (slotW + spacingX);
                    float posY = startY - r * (slotH + spacingY);
                    slotRect.anchoredPosition = new Vector2(posX, posY);

                    Image slotBg = slotGo.AddComponent<Image>();
                    slotBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                    slotBg.type = Image.Type.Sliced;
                    slotBg.color = new Color(0.20f, 0.17f, 0.14f, 1.0f);

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
                    _slotIcons[slotIdx] = iconImg;

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
                    countText.fontSize = 14;
                    countText.fontStyle = FontStyle.Bold;
                    countText.alignment = TextAnchor.LowerRight;
                    countText.color = Color.white;
                    countText.enabled = false;
                    countText.raycastTarget = false;
                    countGo.AddComponent<Outline>().effectColor = Color.black;
                    _slotCountTexts[slotIdx] = countText;

                    UIDragSlot dragSlot = slotGo.AddComponent<UIDragSlot>();
                    dragSlot.slotIndex = 8 + slotIdx;

                    slotIdx++;
                }
            }

            GameObject footerGo = new GameObject("InventoryFooterText");
            footerGo.transform.SetParent(_panelGo.transform, false);
            Text footerText = footerGo.AddComponent<Text>();
            footerText.text = "Press 'I' or 'Tab' to close";
            footerText.font = legacyFont;
            footerText.fontSize = 13;
            footerText.color = new Color(0.75f, 0.70f, 0.62f, 1.0f);
            footerText.alignment = TextAnchor.MiddleCenter;

            RectTransform footerRect = footerGo.GetComponent<RectTransform>();
            footerRect.anchorMin = new Vector2(0.5f, 0f);
            footerRect.anchorMax = new Vector2(0.5f, 0f);
            footerRect.pivot = new Vector2(0.5f, 0f);
            footerRect.anchoredPosition = new Vector2(0f, 12f);
            footerRect.sizeDelta = new Vector2(300f, 25f);

            Debug.Log("[InventoryUI] Programmatically constructed 2x8 Inventory UI panel successfully.");
        }

        private IEnumerator PlayBounceAnimation(Transform target)
        {
            float duration = 0.22f;
            float elapsed = 0f;
            Vector3 startScale = new Vector3(0.7f, 0.7f, 1f);
            Vector3 targetScale = Vector3.one;

            target.localScale = startScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scaleT = Mathf.Sin(t * Mathf.PI * 0.75f) * 1.08f;
                target.localScale = Vector3.LerpUnclamped(startScale, targetScale, scaleT);
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
