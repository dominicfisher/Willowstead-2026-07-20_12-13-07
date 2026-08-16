using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Willowstead.Player;
using Willowstead.World;

namespace Willowstead.UI
{
    /// <summary>
    /// Redesigned World Map Modal with rich cartographic aesthetics,
    /// smooth zoom/pan controls, real-time procedural biome & water rendering,
    /// dynamic animated player pin with heading arrow, and POI markers.
    /// </summary>
    public class FullMapUI : MonoBehaviour, IDragHandler, IScrollHandler, IPointerDownHandler
    {
        public static FullMapUI Instance { get; private set; }

        public bool IsMapOpen => _rootGo != null && _rootGo.activeSelf;

        private GameObject _rootGo;
        private GameObject _windowPanel;
        private RectTransform _viewportRect;
        private RectTransform _contentRect;
        private RawImage _mapRawImage;

        private RectTransform _playerPinRect;
        private RectTransform _playerArrowRect;
        private Image _playerPulseImg;

        private TextMeshProUGUI _playerCoordsText;
        private TextMeshProUGUI _cursorCoordsText;
        private TextMeshProUGUI _zoomLevelText;

        private Texture2D _mapTexture;
        public const int MapWorldRadius = 500; // Bounds -500 to +500 world tiles (1000x1000 world bounds = 62x62 chunks!)
        private const int TileRes = 4; // Compact 4x4 micro-texture per tile for lightweight 4000x4000 full world caching
        private const int TotalTiles = MapWorldRadius * 2;
        private const int MapTexSize = TotalTiles * TileRes;

        private readonly HashSet<Vector2Int> _renderedChunks = new HashSet<Vector2Int>();
        private readonly Dictionary<Sprite, Color[]> _spritePixelCache = new Dictionary<Sprite, Color[]>();

        private float _currentZoom = 1.0f;
        private const float MinZoom = 0.5f;
        private const float MaxZoom = 12.0f;
        private Vector2 _targetPanPos = Vector2.zero;
        private Vector2 _currentPanPos = Vector2.zero;

        private readonly List<GameObject> _spawnedPoiPins = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[FullMapUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<FullMapUI>();
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitMapTexture();
        }

        private void Start()
        {
            CreateFullMapUI();
            if (_rootGo != null) _rootGo.SetActive(false);

            if (ProceduralGridGenerator.Instance != null)
            {
                ProceduralGridGenerator.Instance.OnChunkGenerated += HandleChunkGenerated;
            }
        }

        private void OnDestroy()
        {
            if (ProceduralGridGenerator.Instance != null)
            {
                ProceduralGridGenerator.Instance.OnChunkGenerated -= HandleChunkGenerated;
            }
        }

        private void HandleChunkGenerated(Vector2Int chunkCoord)
        {
            BakeChunkToMap(chunkCoord);
            if (IsMapOpen && _mapTexture != null)
            {
                _mapTexture.Apply();
            }
        }

        private void InitMapTexture()
        {
            if (_mapTexture == null)
            {
                _mapTexture = new Texture2D(MapTexSize, MapTexSize, TextureFormat.RGBA32, false);
                _mapTexture.filterMode = FilterMode.Point;
                _mapTexture.wrapMode = TextureWrapMode.Clamp;

                Color fogColor = new Color(0.08f, 0.08f, 0.09f, 1f);
                Color[] clearPixels = new Color[MapTexSize * MapTexSize];
                for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = fogColor;
                _mapTexture.SetPixels(clearPixels);
                _mapTexture.Apply();
            }
        }

        private void Update()
        {
            bool mapPressed = Input.KeyRebindingManager.WasPressedThisFrame(Input.KeyAction.Map) ||
                              (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame);

            if (mapPressed)
            {
                bool blockByOtherModals = Input.InputReader.BlockGameplayInput ||
                                          (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsOpen) ||
                                          (WorldSetupUI.Instance != null && WorldSetupUI.Instance.IsVisible);
                if (!blockByOtherModals)
                {
                    ToggleMap();
                }
            }
            else if (IsMapOpen && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseMap();
            }

            if (!IsMapOpen) return;

            _currentPanPos = Vector2.Lerp(_currentPanPos, _targetPanPos, Time.unscaledDeltaTime * 16f);
            if (_contentRect != null)
            {
                _contentRect.anchoredPosition = _currentPanPos;
                _contentRect.localScale = new Vector3(_currentZoom, _currentZoom, 1f);

                // Counter-scale pins and labels so they don't blow up or overlap when zoomed in
                float pinScale = Mathf.Clamp(1f / Mathf.Sqrt(_currentZoom), 0.35f, 1.2f);
                if (_playerPinRect != null) _playerPinRect.localScale = new Vector3(pinScale, pinScale, 1f);
                foreach (var pin in _spawnedPoiPins)
                {
                    if (pin != null) pin.transform.localScale = new Vector3(pinScale, pinScale, 1f);
                }
            }

            UpdatePlayerPin();
            UpdateCoordinateBadges();
        }

        public void ToggleMap()
        {
            if (IsMapOpen) CloseMap();
            else OpenMap();
        }

        public void OpenMap()
        {
            if (_rootGo == null) CreateFullMapUI();

            _rootGo.SetActive(true);
            Input.InputReader.BlockGameplayInput = true;

            GenerateMapTexture();
            RebuildPOIPins();
            CenterOnPlayerInstant();
        }

        public void CloseMap()
        {
            if (_rootGo != null) _rootGo.SetActive(false);
            Input.InputReader.BlockGameplayInput = false;
        }

        private void CreateFullMapUI()
        {
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            if (canvas == null) return;
            UIResourceHelper.EnsureEventSystem();

            Transform existing = canvas.transform.Find("FullMapOverlay");
            if (existing != null) DestroyImmediate(existing.gameObject);

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;

            _rootGo = new GameObject("FullMapOverlay", typeof(RectTransform), typeof(Image));
            _rootGo.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = (RectTransform)_rootGo.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            Image dimImg = _rootGo.GetComponent<Image>();
            dimImg.color = new Color(0.03f, 0.04f, 0.05f, 0.88f);

            _windowPanel = new GameObject("WindowPanel", typeof(RectTransform));
            _windowPanel.transform.SetParent(_rootGo.transform, false);

            RectTransform winRect = (RectTransform)_windowPanel.transform;
            winRect.anchorMin = new Vector2(0.5f, 0.5f);
            winRect.anchorMax = new Vector2(0.5f, 0.5f);
            winRect.pivot = new Vector2(0.5f, 0.5f);
            winRect.sizeDelta = new Vector2(960f, 700f);

            GameObject winShadowGo = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            winShadowGo.transform.SetParent(_windowPanel.transform, false);
            RectTransform winShadowRt = (RectTransform)winShadowGo.transform;
            winShadowRt.anchorMin = Vector2.zero; winShadowRt.anchorMax = Vector2.one;
            winShadowRt.offsetMin = new Vector2(-12f, -12f);
            winShadowRt.offsetMax = new Vector2(12f, 12f);
            Image winShadowImg = winShadowGo.GetComponent<Image>();
            winShadowImg.sprite = UIResourceHelper.GetBackgroundSprite();
            winShadowImg.type = Image.Type.Sliced;
            winShadowImg.color = new Color(0f, 0f, 0f, 0.6f);

            GameObject winWoodGo = new GameObject("WoodFrame", typeof(RectTransform), typeof(Image));
            winWoodGo.transform.SetParent(_windowPanel.transform, false);
            RectTransform winWoodRt = (RectTransform)winWoodGo.transform;
            winWoodRt.anchorMin = Vector2.zero; winWoodRt.anchorMax = Vector2.one;
            winWoodRt.offsetMin = Vector2.zero; winWoodRt.offsetMax = Vector2.zero;
            Image winWoodImg = winWoodGo.GetComponent<Image>();
            winWoodImg.sprite = UIResourceHelper.GetBackgroundSprite();
            winWoodImg.type = Image.Type.Sliced;
            winWoodImg.color = new Color(0.22f, 0.16f, 0.11f, 0.98f);

            GameObject winTrimGo = new GameObject("GoldTrim", typeof(RectTransform), typeof(Image));
            winTrimGo.transform.SetParent(winWoodGo.transform, false);
            RectTransform winTrimRt = (RectTransform)winTrimGo.transform;
            winTrimRt.anchorMin = Vector2.zero; winTrimRt.anchorMax = Vector2.one;
            winTrimRt.offsetMin = new Vector2(4f, 4f);
            winTrimRt.offsetMax = new Vector2(-4f, -4f);
            Image winTrimImg = winTrimGo.GetComponent<Image>();
            winTrimImg.sprite = UIResourceHelper.GetBackgroundSprite();
            winTrimImg.type = Image.Type.Sliced;
            winTrimImg.color = new Color(0.72f, 0.58f, 0.32f, 0.65f);

            GameObject winInnerGo = new GameObject("InnerBacking", typeof(RectTransform), typeof(Image));
            winInnerGo.transform.SetParent(winTrimGo.transform, false);
            RectTransform winInnerRt = (RectTransform)winInnerGo.transform;
            winInnerRt.anchorMin = Vector2.zero; winInnerRt.anchorMax = Vector2.one;
            winInnerRt.offsetMin = new Vector2(3f, 3f);
            winInnerRt.offsetMax = new Vector2(-3f, -3f);
            Image winInnerImg = winInnerGo.GetComponent<Image>();
            winInnerImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            winInnerImg.type = Image.Type.Sliced;
            winInnerImg.color = new Color(0.10f, 0.09f, 0.08f, 0.98f);

            GameObject headerGo = new GameObject("Header", typeof(RectTransform), typeof(Image));
            headerGo.transform.SetParent(winInnerGo.transform, false);

            RectTransform headRect = (RectTransform)headerGo.transform;
            headRect.anchorMin = new Vector2(0f, 1f);
            headRect.anchorMax = new Vector2(1f, 1f);
            headRect.pivot = new Vector2(0.5f, 1f);
            headRect.sizeDelta = new Vector2(0f, 52f);

            Image headBg = headerGo.GetComponent<Image>();
            headBg.sprite = UIResourceHelper.GetBackgroundSprite();
            headBg.type = Image.Type.Sliced;
            headBg.color = new Color(0.18f, 0.13f, 0.09f, 1f);

            GameObject titleGo = new GameObject("TitleText", typeof(RectTransform));
            titleGo.transform.SetParent(headerGo.transform, false);
            RectTransform titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(0.35f, 1f);
            titleRt.offsetMin = new Vector2(20f, 0f);
            titleRt.offsetMax = Vector2.zero;

            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            if (font != null) titleTxt.font = font;
            titleTxt.fontSize = 20;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.alignment = TextAlignmentOptions.MidlineLeft;
            titleTxt.color = new Color(1f, 0.88f, 0.52f, 1f);
            titleTxt.text = "✦ WORLD MAP";

            GameObject pCoordsGo = new GameObject("PlayerCoords", typeof(RectTransform));
            pCoordsGo.transform.SetParent(headerGo.transform, false);
            RectTransform pCoordsRt = (RectTransform)pCoordsGo.transform;
            pCoordsRt.anchorMin = new Vector2(0.35f, 0f);
            pCoordsRt.anchorMax = new Vector2(0.65f, 1f);
            pCoordsRt.offsetMin = Vector2.zero; pCoordsRt.offsetMax = Vector2.zero;

            _playerCoordsText = pCoordsGo.AddComponent<TextMeshProUGUI>();
            if (font != null) _playerCoordsText.font = font;
            _playerCoordsText.fontSize = 13;
            _playerCoordsText.alignment = TextAlignmentOptions.Center;
            _playerCoordsText.color = new Color(0.85f, 0.82f, 0.75f, 0.95f);
            _playerCoordsText.text = "Player: (0, 0)";

            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(headerGo.transform, false);
            RectTransform closeRt = (RectTransform)closeGo.transform;
            closeRt.anchorMin = new Vector2(1f, 0.5f);
            closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-12f, 0f);
            closeRt.sizeDelta = new Vector2(34f, 34f);

            Image closeImg = closeGo.GetComponent<Image>();
            closeImg.sprite = UIResourceHelper.GetBackgroundSprite();
            closeImg.type = Image.Type.Sliced;
            closeImg.color = new Color(0.52f, 0.20f, 0.20f, 0.95f);

            Button closeBtn = closeGo.GetComponent<Button>();
            ColorBlock closeCb = closeBtn.colors;
            closeCb.normalColor = new Color(0.52f, 0.20f, 0.20f, 0.95f);
            closeCb.highlightedColor = new Color(0.72f, 0.25f, 0.25f, 1f);
            closeCb.pressedColor = new Color(0.35f, 0.12f, 0.12f, 1f);
            closeBtn.colors = closeCb;
            closeBtn.onClick.AddListener(CloseMap);

            GameObject closeTxtGo = new GameObject("X", typeof(RectTransform));
            closeTxtGo.transform.SetParent(closeGo.transform, false);
            RectTransform closeTxtRt = (RectTransform)closeTxtGo.transform;
            closeTxtRt.anchorMin = Vector2.zero; closeTxtRt.anchorMax = Vector2.one;
            closeTxtRt.offsetMin = Vector2.zero; closeTxtRt.offsetMax = Vector2.zero;

            var closeTxt = closeTxtGo.AddComponent<TextMeshProUGUI>();
            if (font != null) closeTxt.font = font;
            closeTxt.fontSize = 16;
            closeTxt.fontStyle = FontStyles.Bold;
            closeTxt.alignment = TextAlignmentOptions.Center;
            closeTxt.color = Color.white;
            closeTxt.text = "X";

            GameObject vpGo = new GameObject("MapViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            vpGo.transform.SetParent(winInnerGo.transform, false);

            _viewportRect = (RectTransform)vpGo.transform;
            _viewportRect.anchorMin = new Vector2(0f, 0f);
            _viewportRect.anchorMax = new Vector2(1f, 1f);
            _viewportRect.offsetMin = new Vector2(16f, 48f); // Left, Bottom
            _viewportRect.offsetMax = new Vector2(-16f, -58f); // Right, Top

            Image vpBg = vpGo.GetComponent<Image>();
            vpBg.color = new Color(0.05f, 0.07f, 0.08f, 1f);

            EventTrigger trigger = vpGo.AddComponent<EventTrigger>();
            EventTrigger.Entry dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            dragEntry.callback.AddListener((data) => OnDrag((PointerEventData)data));
            trigger.triggers.Add(dragEntry);

            EventTrigger.Entry scrollEntry = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
            scrollEntry.callback.AddListener((data) => OnScroll((PointerEventData)data));
            trigger.triggers.Add(scrollEntry);

            GameObject contentGo = new GameObject("MapContent", typeof(RectTransform), typeof(RawImage));
            contentGo.transform.SetParent(vpGo.transform, false);

            _contentRect = (RectTransform)contentGo.transform;
            _contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            _contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            _contentRect.pivot = new Vector2(0.5f, 0.5f);
            _contentRect.sizeDelta = new Vector2(680f, 680f);

            _mapRawImage = contentGo.GetComponent<RawImage>();
            _mapRawImage.color = Color.white;

            // ── Player Pin Marker (Scale adjusted to look clean and proportional) ──
            GameObject playerPinGo = new GameObject("PlayerPin", typeof(RectTransform));
            playerPinGo.transform.SetParent(contentGo.transform, false);

            _playerPinRect = (RectTransform)playerPinGo.transform;
            _playerPinRect.anchorMin = new Vector2(0.5f, 0.5f);
            _playerPinRect.anchorMax = new Vector2(0.5f, 0.5f);
            _playerPinRect.pivot = new Vector2(0.5f, 0.5f);
            _playerPinRect.sizeDelta = new Vector2(10f, 10f);

            GameObject pulseGo = new GameObject("PulseRing", typeof(RectTransform), typeof(Image));
            pulseGo.transform.SetParent(playerPinGo.transform, false);
            RectTransform pulseRt = (RectTransform)pulseGo.transform;
            pulseRt.anchorMin = Vector2.zero; pulseRt.anchorMax = Vector2.one;
            pulseRt.offsetMin = new Vector2(-4f, -4f); pulseRt.offsetMax = new Vector2(4f, 4f);
            _playerPulseImg = pulseGo.GetComponent<Image>();
            _playerPulseImg.sprite = UIResourceHelper.GetCircleSprite();
            _playerPulseImg.color = new Color(0.2f, 0.85f, 1f, 0.4f);

            GameObject coreDotGo = new GameObject("CoreDot", typeof(RectTransform), typeof(Image));
            coreDotGo.transform.SetParent(playerPinGo.transform, false);
            RectTransform coreDotRt = (RectTransform)coreDotGo.transform;
            coreDotRt.anchorMin = Vector2.zero; coreDotRt.anchorMax = Vector2.one;
            coreDotRt.offsetMin = Vector2.zero; coreDotRt.offsetMax = Vector2.zero;
            Image coreImg = coreDotGo.GetComponent<Image>();
            coreImg.sprite = UIResourceHelper.GetCircleSprite();
            coreImg.color = new Color(0.2f, 0.9f, 1f, 1f);

            GameObject arrowGo = new GameObject("FacingArrow", typeof(RectTransform), typeof(Image));
            arrowGo.transform.SetParent(playerPinGo.transform, false);
            _playerArrowRect = (RectTransform)arrowGo.transform;
            _playerArrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            _playerArrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            _playerArrowRect.pivot = new Vector2(0.5f, 0f);
            _playerArrowRect.anchoredPosition = new Vector2(0f, 0f);
            _playerArrowRect.sizeDelta = new Vector2(3.5f, 8f);

            Image arrowImg = arrowGo.GetComponent<Image>();
            arrowImg.sprite = UIResourceHelper.GetCircleSprite();
            arrowImg.color = new Color(1f, 0.95f, 0.45f, 0.95f);

            CreateBottomControls(winInnerGo, font);
        }

        private void CreateBottomControls(GameObject parent, TMP_FontAsset font)
        {
            GameObject barGo = new GameObject("BottomBar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(parent.transform, false);

            RectTransform barRect = (RectTransform)barGo.transform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(0f, 44f);

            Image barBg = barGo.GetComponent<Image>();
            barBg.sprite = UIResourceHelper.GetBackgroundSprite();
            barBg.type = Image.Type.Sliced;
            barBg.color = new Color(0.14f, 0.11f, 0.08f, 1f);

            CreateToolbarButton(barGo, "Center on Player", 18f, 160f, font, CenterOnPlayer);

            CreateToolbarButton(barGo, "−", 190f, 36f, font, ZoomOut);
            CreateToolbarButton(barGo, "+", 232f, 36f, font, ZoomIn);
            CreateToolbarButton(barGo, "1:1", 274f, 44f, font, ZoomReset);

            GameObject zoomTxtGo = new GameObject("ZoomLabel", typeof(RectTransform));
            zoomTxtGo.transform.SetParent(barGo.transform, false);
            RectTransform zoomRt = (RectTransform)zoomTxtGo.transform;
            zoomRt.anchorMin = new Vector2(0f, 0.5f);
            zoomRt.anchorMax = new Vector2(0f, 0.5f);
            zoomRt.pivot = new Vector2(0f, 0.5f);
            zoomRt.anchoredPosition = new Vector2(326f, 0f);
            zoomRt.sizeDelta = new Vector2(60f, 26f);

            _zoomLevelText = zoomTxtGo.AddComponent<TextMeshProUGUI>();
            if (font != null) _zoomLevelText.font = font;
            _zoomLevelText.fontSize = 14;
            _zoomLevelText.fontStyle = FontStyles.Bold;
            _zoomLevelText.alignment = TextAlignmentOptions.MidlineLeft;
            _zoomLevelText.color = new Color(0.92f, 0.88f, 0.78f, 1f);
            _zoomLevelText.text = "100%";

            CreateLegendItem(barGo, "Player", new Color(0.2f, 0.9f, 1f), -195f, font);
            CreateLegendItem(barGo, "Water", new Color(0.25f, 0.55f, 0.95f), -100f, font);
        }

        private void CreateToolbarButton(GameObject parent, string label, float xPos, float width, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnGo = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent.transform, false);
            RectTransform rt = (RectTransform)btnGo.transform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(xPos, 0f);
            rt.sizeDelta = new Vector2(width, 32f);

            Image img = btnGo.GetComponent<Image>();
            img.sprite = UIResourceHelper.GetBackgroundSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.28f, 0.20f, 0.14f, 1f); // Wood tone

            Button btn = btnGo.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.28f, 0.20f, 0.14f, 1f);
            cb.highlightedColor = new Color(0.42f, 0.30f, 0.20f, 1f);
            cb.pressedColor = new Color(0.18f, 0.12f, 0.08f, 1f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject txtGo = new GameObject("Txt", typeof(RectTransform));
            txtGo.transform.SetParent(btnGo.transform, false);
            RectTransform txtRt = (RectTransform)txtGo.transform;
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;

            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            if (font != null) txt.font = font;
            txt.fontSize = 13.5f;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(1f, 0.92f, 0.78f, 1f);
            txt.text = label;
        }

        private void CreateLegendItem(GameObject parent, string label, Color color, float xPosRight, TMP_FontAsset font)
        {
            GameObject legendGo = new GameObject($"Legend_{label}", typeof(RectTransform));
            legendGo.transform.SetParent(parent.transform, false);
            RectTransform rt = (RectTransform)legendGo.transform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(xPosRight, 0f);
            rt.sizeDelta = new Vector2(85f, 26f);

            GameObject dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dotGo.transform.SetParent(legendGo.transform, false);
            RectTransform dotRt = (RectTransform)dotGo.transform;
            dotRt.anchorMin = new Vector2(0f, 0.5f);
            dotRt.anchorMax = new Vector2(0f, 0.5f);
            dotRt.pivot = new Vector2(0f, 0.5f);
            dotRt.anchoredPosition = new Vector2(0f, 0f);
            dotRt.sizeDelta = new Vector2(12f, 12f);

            Image dotImg = dotGo.GetComponent<Image>();
            dotImg.sprite = UIResourceHelper.GetCircleSprite();
            dotImg.color = color;

            GameObject txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(legendGo.transform, false);
            RectTransform txtRt = (RectTransform)txtGo.transform;
            txtRt.anchorMin = new Vector2(0f, 0f);
            txtRt.anchorMax = new Vector2(1f, 1f);
            txtRt.offsetMin = new Vector2(18f, 0f);
            txtRt.offsetMax = Vector2.zero;

            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            if (font != null) txt.font = font;
            txt.fontSize = 13.5f;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.color = new Color(0.92f, 0.90f, 0.85f, 0.95f);
            txt.text = label;
        }

        private void RebuildPOIPins()
        {
            foreach (var pin in _spawnedPoiPins)
            {
                if (pin != null) Destroy(pin);
            }
            _spawnedPoiPins.Clear();
        }

        private void CreatePOIPinOnMap(GameObject container, string name, Vector3 worldPos, Color pinColor)
        {
            GameObject poiGo = new GameObject($"MapPin_{name}", typeof(RectTransform));
            poiGo.transform.SetParent(container.transform, false);

            RectTransform rt = (RectTransform)poiGo.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(18f, 18f);

            float mapScale = 680f / (MapWorldRadius * 2f);
            rt.anchoredPosition = new Vector2(worldPos.x * mapScale, worldPos.y * mapScale);

            GameObject glowGo = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(poiGo.transform, false);
            RectTransform glowRt = (RectTransform)glowGo.transform;
            glowRt.anchorMin = Vector2.zero; glowRt.anchorMax = Vector2.one;
            glowRt.offsetMin = new Vector2(-4f, -4f); glowRt.offsetMax = new Vector2(4f, 4f);
            Image glowImg = glowGo.GetComponent<Image>();
            glowImg.sprite = UIResourceHelper.GetCircleSprite();
            glowImg.color = new Color(0f, 0f, 0f, 0.75f);

            GameObject dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dotGo.transform.SetParent(poiGo.transform, false);
            RectTransform dotRt = (RectTransform)dotGo.transform;
            dotRt.anchorMin = Vector2.zero; dotRt.anchorMax = Vector2.one;
            dotRt.offsetMin = Vector2.zero; dotRt.offsetMax = Vector2.zero;
            Image img = dotGo.GetComponent<Image>();
            img.sprite = UIResourceHelper.GetCircleSprite();
            img.color = pinColor;

            GameObject txtGo = new GameObject("Label", typeof(RectTransform));
            txtGo.transform.SetParent(poiGo.transform, false);
            RectTransform txtRt = (RectTransform)txtGo.transform;
            txtRt.anchorMin = new Vector2(0.5f, 1f);
            txtRt.anchorMax = new Vector2(0.5f, 1f);
            txtRt.pivot = new Vector2(0.5f, 0f);
            txtRt.anchoredPosition = new Vector2(0f, 4f);
            txtRt.sizeDelta = new Vector2(160f, 24f);

            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) txt.font = TMP_Settings.defaultFontAsset;
            txt.fontSize = 15f;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(1f, 1f, 1f, 0.98f);
            txt.outlineWidth = 0.25f;
            txt.outlineColor = new Color32(0, 0, 0, 255);
            txt.text = name;

            _spawnedPoiPins.Add(poiGo);
        }

        private void GenerateMapTexture()
        {
            ProceduralGridGenerator gen = ProceduralGridGenerator.Instance;
            if (gen == null) gen = FindAnyObjectByType<ProceduralGridGenerator>();

            InitMapTexture();

            if (_mapTexture != null)
            {
                _mapTexture.Apply();
            }

            if (_mapRawImage != null)
            {
                _mapRawImage.texture = _mapTexture;
            }
        }

        private void BakeChunkToMap(Vector2Int chunkCoord)
        {
            if (_mapTexture == null) InitMapTexture();

            ProceduralGridGenerator gen = ProceduralGridGenerator.Instance;
            if (gen == null) gen = FindAnyObjectByType<ProceduralGridGenerator>();

            GridManager gridMgr = GridManager.Instance;
            if (gridMgr == null) gridMgr = FindAnyObjectByType<GridManager>();

            Tilemap waterMap = gen != null ? gen.WaterTilemap : null;
            Tilemap grassMap = gen != null ? gen.GrassTilemap : null;
            Tilemap dirtMap  = gen != null ? gen.DirtTilemap : null;
            Tilemap farmMap  = gridMgr != null ? gridMgr.FarmingTilemap : null;

            Color fallbackGrass = new Color(0.26f, 0.54f, 0.28f, 1f);
            Color fallbackDirt  = new Color(0.58f, 0.44f, 0.28f, 1f);
            Color fallbackWater = new Color(0.20f, 0.48f, 0.78f, 1f);

            int chunkSize = 16;
            int startX = chunkCoord.x * chunkSize;
            int startY = chunkCoord.y * chunkSize;

            Color[] tileBlock = new Color[TileRes * TileRes];

            for (int y = 0; y < chunkSize; y++)
            {
                int worldY = startY + y;
                int ty = worldY + MapWorldRadius;
                if (ty < 0 || ty >= TotalTiles) continue;

                for (int x = 0; x < chunkSize; x++)
                {
                    int worldX = startX + x;
                    int tx = worldX + MapWorldRadius;
                    if (tx < 0 || tx >= TotalTiles) continue;

                    Vector3Int cellPos = new Vector3Int(worldX, worldY, 0);
                    Sprite activeSprite = null;

                    if (waterMap != null && waterMap.HasTile(cellPos)) activeSprite = waterMap.GetSprite(cellPos);
                    else if (farmMap != null && farmMap.HasTile(cellPos)) activeSprite = farmMap.GetSprite(cellPos);
                    else if (grassMap != null && grassMap.HasTile(cellPos)) activeSprite = grassMap.GetSprite(cellPos);
                    else if (dirtMap != null && dirtMap.HasTile(cellPos)) activeSprite = dirtMap.GetSprite(cellPos);

                    if (activeSprite != null)
                    {
                        if (!_spritePixelCache.TryGetValue(activeSprite, out Color[] cachedPixels))
                        {
                            cachedPixels = new Color[TileRes * TileRes];
                            if (activeSprite.texture != null && activeSprite.texture.isReadable)
                            {
                                Rect rect = activeSprite.rect;
                                int rx = Mathf.RoundToInt(rect.x);
                                int ry = Mathf.RoundToInt(rect.y);
                                int rw = Mathf.RoundToInt(rect.width);
                                int rh = Mathf.RoundToInt(rect.height);

                                Color[] rawPixels = activeSprite.texture.GetPixels(rx, ry, rw, rh);
                                for (int py = 0; py < TileRes; py++)
                                {
                                    int srcY = Mathf.Clamp(Mathf.FloorToInt(((float)py / TileRes) * rh), 0, rh - 1);
                                    for (int px = 0; px < TileRes; px++)
                                    {
                                        int srcX = Mathf.Clamp(Mathf.FloorToInt(((float)px / TileRes) * rw), 0, rw - 1);
                                        cachedPixels[py * TileRes + px] = rawPixels[srcY * rw + srcX];
                                    }
                                }
                            }
                            else if (activeSprite.texture != null)
                            {
                                RenderTexture rt = RenderTexture.GetTemporary(TileRes, TileRes, 0, RenderTextureFormat.ARGB32);
                                Texture2D readTex = new Texture2D(TileRes, TileRes, TextureFormat.RGBA32, false);
                                Rect rect = activeSprite.rect;
                                float uMin = rect.x / activeSprite.texture.width;
                                float vMin = rect.y / activeSprite.texture.height;
                                float uMax = (rect.x + rect.width) / activeSprite.texture.width;
                                float vMax = (rect.y + rect.height) / activeSprite.texture.height;

                                RenderTexture prevRT = RenderTexture.active;
                                RenderTexture.active = rt;
                                GL.PushMatrix();
                                GL.LoadPixelMatrix(0, TileRes, 0, TileRes);
                                Graphics.DrawTexture(new Rect(0, 0, TileRes, TileRes), activeSprite.texture, new Rect(uMin, vMin, uMax - uMin, vMax - vMin), 0, 0, 0, 0);
                                GL.PopMatrix();

                                readTex.ReadPixels(new Rect(0, 0, TileRes, TileRes), 0, 0);
                                readTex.Apply();
                                RenderTexture.active = prevRT;

                                cachedPixels = readTex.GetPixels();
                                RenderTexture.ReleaseTemporary(rt);
                                Destroy(readTex);
                            }
                            _spritePixelCache[activeSprite] = cachedPixels;
                        }

                        for (int i = 0; i < tileBlock.Length; i++)
                        {
                            Color c = cachedPixels[i];
                            tileBlock[i] = (c.a < 0.98f) ? Color.Lerp(fallbackDirt, c, c.a) : c;
                        }
                    }
                    else
                    {
                        Color solid = fallbackGrass;
                        if (gen != null)
                        {
                            if (gen.IsWaterAt(worldX, worldY)) solid = fallbackWater;
                            else if (!gen.IsGrassAt(worldX, worldY)) solid = fallbackDirt;
                        }
                        if (gridMgr != null && gridMgr.IsCellTilled(cellPos)) solid = new Color(0.34f, 0.24f, 0.14f, 1f);

                        for (int i = 0; i < tileBlock.Length; i++) tileBlock[i] = solid;
                    }

                    _mapTexture.SetPixels(tx * TileRes, ty * TileRes, TileRes, TileRes, tileBlock);
                }
            }

            _renderedChunks.Add(chunkCoord);
        }

        private void UpdatePlayerPin()
        {
            if (PlayerController.Instance == null || _playerPinRect == null) return;

            Vector3 pPos = PlayerController.Instance.transform.position;
            float mapScale = 680f / (MapWorldRadius * 2f);
            _playerPinRect.anchoredPosition = new Vector2(pPos.x * mapScale, pPos.y * mapScale);

            if (_playerArrowRect != null)
            {
                float facingAngle = PlayerController.Instance.FacingAngle; // 0=East, 90=North, 180=West, 270=South
                _playerArrowRect.localRotation = Quaternion.Euler(0f, 0f, facingAngle - 90f);
            }

            if (_playerPulseImg != null)
            {
                float pulse = 0.35f + Mathf.PingPong(Time.unscaledTime * 1.5f, 0.45f);
                Color c = _playerPulseImg.color;
                c.a = pulse;
                _playerPulseImg.color = c;
            }
        }

        private void UpdateCoordinateBadges()
        {
            if (PlayerController.Instance != null && _playerCoordsText != null)
            {
                Vector3 pPos = PlayerController.Instance.transform.position;
                _playerCoordsText.text = $"Player: ({Mathf.RoundToInt(pPos.x)}, {Mathf.RoundToInt(pPos.y)})";
            }
        }

        public void CenterOnPlayer()
        {
            if (PlayerController.Instance == null) return;
            Vector3 pPos = PlayerController.Instance.transform.position;
            float mapScale = 680f / (MapWorldRadius * 2f);
            _targetPanPos = -new Vector2(pPos.x * mapScale, pPos.y * mapScale) * _currentZoom;
        }

        private void CenterOnPlayerInstant()
        {
            if (PlayerController.Instance == null) return;
            Vector3 pPos = PlayerController.Instance.transform.position;
            float mapScale = 680f / (MapWorldRadius * 2f);
            _targetPanPos = -new Vector2(pPos.x * mapScale, pPos.y * mapScale) * _currentZoom;
            _currentPanPos = _targetPanPos;
            if (_contentRect != null)
            {
                _contentRect.anchoredPosition = _currentPanPos;
            }
        }

        public void ZoomIn() => SetZoom(_currentZoom + 0.3f);
        public void ZoomOut() => SetZoom(_currentZoom - 0.3f);
        public void ZoomReset() => SetZoom(1.0f);

        private void SetZoom(float targetZoom)
        {
            _currentZoom = Mathf.Clamp(targetZoom, MinZoom, MaxZoom);
            if (_zoomLevelText != null)
            {
                _zoomLevelText.text = $"{Mathf.RoundToInt(_currentZoom * 100)}%";
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            _targetPanPos += eventData.delta;
            float maxPan = 340f * _currentZoom;
            _targetPanPos.x = Mathf.Clamp(_targetPanPos.x, -maxPan, maxPan);
            _targetPanPos.y = Mathf.Clamp(_targetPanPos.y, -maxPan, maxPan);
        }

        public void OnScroll(PointerEventData eventData)
        {
            float zoomDelta = eventData.scrollDelta.y * 0.15f;
            SetZoom(_currentZoom + zoomDelta);
        }

        public void OnPointerDown(PointerEventData eventData) { }
    }
}
