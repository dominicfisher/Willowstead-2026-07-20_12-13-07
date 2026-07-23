using UnityEngine;
using UnityEngine.UI;

namespace Willowstead.Player
{
    /// <summary>
    /// Handles the visual display of the permanent Hotbar HUD at the bottom of the screen.
    /// Programmatically constructs a clean, rounded Hotbar panel and slots using Unity's built-in sprites,
    /// with smooth selection zoom scale animations and real-time count updates.
    /// </summary>
    public class HotbarUI : MonoBehaviour
    {
        [Header("Assets")]
        [Tooltip("The icon to represent the Hoe.")]
        [SerializeField] private Sprite _hoeIcon;

        [Tooltip("The icon to represent the Watering Can.")]
        [SerializeField] private Sprite _wateringCanIcon;

        [Tooltip("The icon to represent the seed item (e.g. CarrotSeed.png).")]
        [SerializeField] private Sprite _seedIcon;

        [Tooltip("The icon to represent the harvested crop item (e.g. Carrot.png).")]
        [SerializeField] private Sprite _carrotIcon;

        private InventoryManager _inventory;
        private Farming.FarmingController _farmingController;
        
        private GameObject _canvasGo;
        private GameObject _hotbarGo;
        
        private RectTransform[] _slotTransforms;
        private Image[] _slotHighlights;
        private Text _seedCountText;
        private Text _carrotCountText;

        /// <summary>
        /// Returns the RectTransform of the Carrot Seeds slot (Slot index 2).
        /// </summary>
        public RectTransform SeedSlotRect => (_slotTransforms != null && _slotTransforms.Length > 2) ? _slotTransforms[2] : null;

        /// <summary>
        /// Returns the RectTransform of the Carrot crop slot (Slot index 3).
        /// </summary>
        public RectTransform CarrotSlotRect => (_slotTransforms != null && _slotTransforms.Length > 3) ? _slotTransforms[3] : null;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_hoeIcon == null) _hoeIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Hoe.png");
            if (_wateringCanIcon == null) _wateringCanIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Watering can.png");
            if (_seedIcon == null) _seedIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/CarrotSeed.png");
            if (_carrotIcon == null) _carrotIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Carrot.png");
        }
#endif

        private System.Collections.Generic.Dictionary<RectTransform, Coroutine> _activePulses = new System.Collections.Generic.Dictionary<RectTransform, Coroutine>();

        private void Start()
        {
            _inventory = GetComponent<InventoryManager>();
            if (_inventory == null) _inventory = FindAnyObjectByType<InventoryManager>();

            _farmingController = GetComponent<Farming.FarmingController>();
            if (_farmingController == null) _farmingController = FindAnyObjectByType<Farming.FarmingController>();

            CreateHotbarUI();
            UpdateCounts();
        }

        private void Update()
        {
            UpdateCounts();

            // Detect active tool and update selection animation targets
            if (_farmingController != null)
            {
                int selectedIndex = 0;
                switch (_farmingController.CurrentTool)
                {
                    case Farming.FarmingController.FarmTool.Hoe:
                        selectedIndex = 0;
                        break;
                    case Farming.FarmingController.FarmTool.WateringCan:
                        selectedIndex = 1;
                        break;
                    case Farming.FarmingController.FarmTool.Seeds:
                        selectedIndex = 2;
                        break;
                }
                UpdateSelection(selectedIndex);
            }
        }

        private void UpdateCounts()
        {
            if (_inventory == null) return;

            int seeds = _inventory.GetItemCount("Carrot Seeds");
            int carrots = _inventory.GetItemCount("Carrot");

            if (_seedCountText != null) _seedCountText.text = seeds.ToString();
            if (_carrotCountText != null) _carrotCountText.text = carrots.ToString();
        }

        private void UpdateSelection(int selectedIndex)
        {
            if (_slotTransforms == null) return;

            for (int i = 0; i < _slotTransforms.Length; i++)
            {
                if (_slotTransforms[i] != null)
                {
                    bool isSelected = (i == selectedIndex);
                    
                    // Juicy Lerp Animation: Selected slot scales up slightly (1.15x), unselected returns to normal (1.0x)
                    Vector3 targetScale = isSelected ? new Vector3(1.15f, 1.15f, 1f) : Vector3.one;
                    _slotTransforms[i].localScale = Vector3.Lerp(_slotTransforms[i].localScale, targetScale, Time.deltaTime * 15f);

                    // Highlights the gold border when active, or drops to a subtle dark shadow when inactive
                    if (_slotHighlights != null && _slotHighlights[i] != null)
                    {
                        _slotHighlights[i].color = isSelected 
                            ? new Color(1f, 0.8f, 0.2f, 0.9f) // Vibrant Gold
                            : new Color(0f, 0f, 0f, 0.35f);    // Subtle Dark Shadow border
                    }
                }
            }
        }

        private void CreateHotbarUI()
        {
            // Spawn Canvas for HUD if it doesn't exist
            _canvasGo = GameObject.Find("HUDCanvas");
            if (_canvasGo == null)
            {
                _canvasGo = new GameObject("HUDCanvas");
                Canvas canvas = _canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvasGo.AddComponent<CanvasScaler>();
                _canvasGo.AddComponent<GraphicRaycaster>();
            }

            // Load Unity's built-in rounded UI panel sprite
            Sprite roundedBg = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
            Sprite slotBg = Resources.GetBuiltinResource<Sprite>("UI/Skin/InputFieldBackground.psd");

            // Create Hotbar Panel at bottom-center of screen
            _hotbarGo = new GameObject("HotbarPanel");
            _hotbarGo.transform.SetParent(_canvasGo.transform, false);
            Image bgImage = _hotbarGo.AddComponent<Image>();
            bgImage.sprite = roundedBg;
            bgImage.type = Image.Type.Sliced; // Slices rounded corners nicely
            bgImage.color = new Color(0.14f, 0.12f, 0.1f, 0.88f); // Warm Dark Slate Brown

            RectTransform bgRect = _hotbarGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0f);
            bgRect.anchorMax = new Vector2(0.5f, 0f);
            bgRect.pivot = new Vector2(0.5f, 0f);
            bgRect.anchoredPosition = new Vector2(0f, 15f); // 15 pixels off the bottom
            bgRect.sizeDelta = new Vector2(350f, 86f);

            // 4 slots: Hoe, Watering Can, Carrot Seeds, Carrots
            int slotCount = 4;
            float slotWidth = 60f;
            float slotHeight = 60f;
            float startX = -120f; // Centers 4 slots inside 350px width perfectly
            float spacing = 80f;

            _slotTransforms = new RectTransform[slotCount];
            _slotHighlights = new Image[slotCount];
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

                // Slot border outline / shadow image
                GameObject highlightGo = new GameObject("HighlightBorder");
                highlightGo.transform.SetParent(slotRect, false);
                Image highlightImg = highlightGo.AddComponent<Image>();
                highlightImg.sprite = roundedBg;
                highlightImg.type = Image.Type.Sliced;
                highlightImg.color = new Color(0f, 0f, 0f, 0.35f);
                RectTransform highRect = highlightGo.GetComponent<RectTransform>();
                highRect.anchoredPosition = Vector2.zero;
                highRect.sizeDelta = new Vector2(slotWidth + 8f, slotHeight + 8f);
                _slotHighlights[i] = highlightImg;

                // Slot Background Panel
                GameObject innerBgGo = new GameObject("InnerBackground");
                innerBgGo.transform.SetParent(slotRect, false);
                Image innerBgImg = innerBgGo.AddComponent<Image>();
                innerBgImg.sprite = slotBg;
                innerBgImg.type = Image.Type.Sliced;
                innerBgImg.color = new Color(0.24f, 0.2f, 0.16f, 0.95f); // Dark warm slot interior
                RectTransform innerBgRect = innerBgGo.GetComponent<RectTransform>();
                innerBgRect.anchoredPosition = Vector2.zero;
                innerBgRect.sizeDelta = new Vector2(slotWidth, slotHeight);

                // Slot Icon
                GameObject iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(slotRect, false);
                Image iconImg = iconGo.AddComponent<Image>();
                RectTransform iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(slotWidth - 16f, slotHeight - 16f);

                // Setup slot content mapping
                if (i == 0)
                {
                    iconImg.sprite = _hoeIcon;
                    if (_hoeIcon == null) iconImg.color = new Color(0.55f, 0.45f, 0.35f, 0.85f); // Hoe fallback color
                }
                else if (i == 1)
                {
                    iconImg.sprite = _wateringCanIcon;
                    if (_wateringCanIcon == null) iconImg.color = new Color(0.3f, 0.55f, 0.85f, 0.85f); // Watering Can fallback color
                }
                else if (i == 2)
                {
                    iconImg.sprite = _seedIcon;
                    if (_seedIcon == null) iconImg.color = new Color(0.75f, 0.65f, 0.45f, 0.85f);

                    // Add count text label
                    GameObject countGo = new GameObject("CountText");
                    countGo.transform.SetParent(slotRect, false);
                    _seedCountText = countGo.AddComponent<Text>();
                    _seedCountText.font = legacyFont;
                    _seedCountText.fontSize = 14;
                    _seedCountText.fontStyle = FontStyle.Bold;
                    _seedCountText.alignment = TextAnchor.LowerRight;
                    _seedCountText.color = Color.white;

                    // Black text outline for readability
                    countGo.AddComponent<Outline>().effectColor = Color.black;

                    RectTransform countRect = countGo.GetComponent<RectTransform>();
                    countRect.anchoredPosition = new Vector2(12f, -12f);
                    countRect.sizeDelta = new Vector2(25f, 20f);
                }
                else if (i == 3)
                {
                    iconImg.sprite = _carrotIcon;
                    if (_carrotIcon == null) iconImg.color = new Color(0.95f, 0.5f, 0.15f, 0.85f);

                    // Add count text label
                    GameObject countGo = new GameObject("CountText");
                    countGo.transform.SetParent(slotRect, false);
                    _carrotCountText = countGo.AddComponent<Text>();
                    _carrotCountText.font = legacyFont;
                    _carrotCountText.fontSize = 14;
                    _carrotCountText.fontStyle = FontStyle.Bold;
                    _carrotCountText.alignment = TextAnchor.LowerRight;
                    _carrotCountText.color = Color.white;

                    // Black text outline for readability
                    countGo.AddComponent<Outline>().effectColor = Color.black;

                    RectTransform countRect = countGo.GetComponent<RectTransform>();
                    countRect.anchoredPosition = new Vector2(12f, -12f);
                    countRect.sizeDelta = new Vector2(25f, 20f);
                }
            }
        }

        /// <summary>
        /// Backward compatibility helper for carrot slot pulsing.
        /// </summary>
        public void PulseCarrotSlot()
        {
            PulseSlot(3);
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
            Vector3 originalScale = Vector3.one;

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
