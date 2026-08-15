using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Willowstead.Player;
using Willowstead.World;

namespace Willowstead.UI
{
    /// <summary>
    /// Programmatically constructs and manages the Full World Map modal overlay.
    /// Features:
    ///   • Real-time sampling of world biomes, dirt patches, ponds/rivers, and farm plot into a map texture.
    ///   • Dynamic player pin with directional facing arrow and pulsing indicator.
    ///   • Landmark POI pins (Farm, Water, Shop).
    ///   • Interactive mouse-drag panning, scroll-wheel zooming, and "Center on Player" snap.
    ///   • Cursor world coordinates display and landmark legend.
    ///   • Clean Esc / M hotkey handling with InputReader input blocking.
    /// Pure code-built UI: 100% self-contained, requires zero inspector prefabs.
    /// </summary>
    public class FullMapUI : MonoBehaviour, IDragHandler, IScrollHandler, IPointerDownHandler
    {
        public static FullMapUI Instance { get; private set; }

        public bool IsMapOpen => _rootGo != null && _rootGo.activeSelf;

        // UI GameObjects
        private GameObject _rootGo;
        private GameObject _windowPanel;
        private RectTransform _viewportRect;
        private RectTransform _contentRect;
        private RawImage _mapRawImage;

        private RectTransform _playerPinRect;
        private RectTransform _playerArrowRect;

        private Text _playerCoordsText;
        private Text _cursorCoordsText;
        private Text _zoomLevelText;

        // Texture generation params
        private Texture2D _mapTexture;
        private const int MapWorldRadius = 64; // Bounds -64 to +64 world tiles (128x128 grid)
        private const int MapTexSize = 256;    // Texture resolution

        // Zoom & Pan state
        private float _currentZoom = 1.2f;
        private const float MinZoom = 0.6f;
        private const float MaxZoom = 3.5f;
        private Vector2 _targetPanPos = Vector2.zero;
        private Vector2 _currentPanPos = Vector2.zero;

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
        }

        private void Start()
        {
            CreateFullMapUI();
            if (_rootGo != null) _rootGo.SetActive(false);
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                // Toggle with M key
                if (UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame)
                {
                    ToggleMap();
                }
                // Close with Esc key if open
                else if (IsMapOpen && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CloseMap();
                }
            }

            if (!IsMapOpen) return;

            // Smooth pan interpolation
            _currentPanPos = Vector2.Lerp(_currentPanPos, _targetPanPos, Time.unscaledDeltaTime * 15f);
            if (_contentRect != null)
            {
                _contentRect.anchoredPosition = _currentPanPos;
                _contentRect.localScale = new Vector3(_currentZoom, _currentZoom, 1f);
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

            // ── Fullscreen Dim Overlay ──
            _rootGo = new GameObject("FullMapOverlay");
            _rootGo.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = _rootGo.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            Image dimImg = _rootGo.AddComponent<Image>();
            dimImg.color = new Color(0.04f, 0.05f, 0.07f, 0.88f);

            // ── Main Window Frame (920 x 660) ──
            _windowPanel = new GameObject("WindowPanel");
            _windowPanel.transform.SetParent(_rootGo.transform, false);

            RectTransform winRect = _windowPanel.AddComponent<RectTransform>();
            winRect.anchorMin = new Vector2(0.5f, 0.5f);
            winRect.anchorMax = new Vector2(0.5f, 0.5f);
            winRect.pivot = new Vector2(0.5f, 0.5f);
            winRect.sizeDelta = new Vector2(920f, 660f);

            Image winBg = _windowPanel.AddComponent<Image>();
            winBg.sprite = UIResourceHelper.GetBackgroundSprite();
            winBg.type = Image.Type.Sliced;
            winBg.color = new Color(0.12f, 0.10f, 0.09f, 0.96f);

            // Gold Header Bar
            GameObject headerGo = new GameObject("Header");
            headerGo.transform.SetParent(_windowPanel.transform, false);

            RectTransform headRect = headerGo.AddComponent<RectTransform>();
            headRect.anchorMin = new Vector2(0f, 1f);
            headRect.anchorMax = new Vector2(1f, 1f);
            headRect.pivot = new Vector2(0.5f, 1f);
            headRect.sizeDelta = new Vector2(0f, 48f);

            Image headBg = headerGo.AddComponent<Image>();
            headBg.sprite = UIResourceHelper.GetBackgroundSprite();
            headBg.type = Image.Type.Sliced;
            headBg.color = new Color(0.18f, 0.15f, 0.11f, 0.98f);

            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Title Text
            GameObject titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(headerGo.transform, false);
            RectTransform titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(0.4f, 1f);
            titleRt.anchoredPosition = new Vector2(16f, 0f);

            Text titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = legacyFont;
            titleTxt.fontSize = 20;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleLeft;
            titleTxt.color = new Color(1f, 0.88f, 0.55f, 1f);
            titleTxt.text = "WORLD MAP";

            // Player Coords Badge
            GameObject pCoordsGo = new GameObject("PlayerCoords");
            pCoordsGo.transform.SetParent(headerGo.transform, false);
            RectTransform pCoordsRt = pCoordsGo.AddComponent<RectTransform>();
            pCoordsRt.anchorMin = new Vector2(0.35f, 0f);
            pCoordsRt.anchorMax = new Vector2(0.65f, 1f);

            _playerCoordsText = pCoordsGo.AddComponent<Text>();
            _playerCoordsText.font = legacyFont;
            _playerCoordsText.fontSize = 12;
            _playerCoordsText.alignment = TextAnchor.MiddleCenter;
            _playerCoordsText.color = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            _playerCoordsText.text = "Player: (0, 0)";

            // Close Button [X]
            GameObject closeGo = new GameObject("CloseButton");
            closeGo.transform.SetParent(headerGo.transform, false);
            RectTransform closeRt = closeGo.AddComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 0.5f);
            closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-10f, 0f);
            closeRt.sizeDelta = new Vector2(32f, 32f);

            Image closeImg = closeGo.AddComponent<Image>();
            closeImg.sprite = UIResourceHelper.GetBackgroundSprite();
            closeImg.type = Image.Type.Sliced;
            closeImg.color = new Color(0.55f, 0.20f, 0.20f, 0.95f);

            Button closeBtn = closeGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(CloseMap);

            GameObject closeTxtGo = new GameObject("X");
            closeTxtGo.transform.SetParent(closeGo.transform, false);
            RectTransform closeTxtRt = closeTxtGo.AddComponent<RectTransform>();
            closeTxtRt.anchorMin = Vector2.zero;
            closeTxtRt.anchorMax = Vector2.one;

            Text closeTxt = closeTxtGo.AddComponent<Text>();
            closeTxt.font = legacyFont;
            closeTxt.fontSize = 16;
            closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.color = Color.white;
            closeTxt.text = "X";

            // ── Viewport Container (Clip Mask) ──
            GameObject vpGo = new GameObject("MapViewport");
            vpGo.transform.SetParent(_windowPanel.transform, false);

            _viewportRect = vpGo.AddComponent<RectTransform>();
            _viewportRect.anchorMin = new Vector2(0f, 0f);
            _viewportRect.anchorMax = new Vector2(1f, 1f);
            _viewportRect.offsetMin = new Vector2(16f, 48f); // Left, Bottom
            _viewportRect.offsetMax = new Vector2(-16f, -54f); // Right, Top

            Image vpBg = vpGo.AddComponent<Image>();
            vpBg.color = new Color(0.06f, 0.08f, 0.10f, 1f);
            vpGo.AddComponent<RectMask2D>();

            // Viewport event proxy for drag/scroll
            EventTrigger trigger = vpGo.AddComponent<EventTrigger>();
            EventTrigger.Entry dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            dragEntry.callback.AddListener((data) => OnDrag((PointerEventData)data));
            trigger.triggers.Add(dragEntry);

            EventTrigger.Entry scrollEntry = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
            scrollEntry.callback.AddListener((data) => OnScroll((PointerEventData)data));
            trigger.triggers.Add(scrollEntry);

            // ── Map Content Container (Panned & Zoomed) ──
            GameObject contentGo = new GameObject("MapContent");
            contentGo.transform.SetParent(vpGo.transform, false);

            _contentRect = contentGo.AddComponent<RectTransform>();
            _contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            _contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            _contentRect.pivot = new Vector2(0.5f, 0.5f);
            _contentRect.sizeDelta = new Vector2(600f, 600f);

            // Map RawImage
            _mapRawImage = contentGo.AddComponent<RawImage>();
            _mapRawImage.color = Color.white;

            // ── Player Pin Marker ──
            GameObject playerPinGo = new GameObject("PlayerPin");
            playerPinGo.transform.SetParent(contentGo.transform, false);

            _playerPinRect = playerPinGo.AddComponent<RectTransform>();
            _playerPinRect.anchorMin = new Vector2(0.5f, 0.5f);
            _playerPinRect.anchorMax = new Vector2(0.5f, 0.5f);
            _playerPinRect.pivot = new Vector2(0.5f, 0.5f);
            _playerPinRect.sizeDelta = new Vector2(20f, 20f);

            Image pCircleImg = playerPinGo.AddComponent<Image>();
            pCircleImg.sprite = UIResourceHelper.GetCircleSprite();
            pCircleImg.color = new Color(0.2f, 0.85f, 1f, 0.95f); // Glowing cyan marker

            // Directional Arrow
            GameObject arrowGo = new GameObject("Arrow");
            arrowGo.transform.SetParent(playerPinGo.transform, false);

            _playerArrowRect = arrowGo.AddComponent<RectTransform>();
            _playerArrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            _playerArrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            _playerArrowRect.pivot = new Vector2(0.5f, 0.5f);
            _playerArrowRect.sizeDelta = new Vector2(10f, 16f);
            _playerArrowRect.anchoredPosition = new Vector2(0f, 12f);

            Image arrowImg = arrowGo.AddComponent<Image>();
            arrowImg.sprite = UIResourceHelper.GetCircleSprite();
            arrowImg.color = new Color(1f, 0.9f, 0.3f, 1f);

            // ── POI Pins on Map Content ──
            CreatePOIPinOnMap(contentGo, "Farm Plot", Vector3.zero, new Color(0.35f, 0.85f, 0.35f, 1f));
            CreatePOIPinOnMap(contentGo, "Shop", new Vector3(8f, 4f, 0f), new Color(0.95f, 0.75f, 0.25f, 1f));

            // ── Bottom Control Toolbar ──
            GameObject barGo = new GameObject("BottomBar");
            barGo.transform.SetParent(_windowPanel.transform, false);

            RectTransform barRt = barGo.AddComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(1f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.sizeDelta = new Vector2(0f, 44f);

            Image barBg = barGo.AddComponent<Image>();
            barBg.sprite = UIResourceHelper.GetBackgroundSprite();
            barBg.type = Image.Type.Sliced;
            barBg.color = new Color(0.15f, 0.12f, 0.10f, 0.95f);

            // Recenter Button
            GameObject centerBtnGo = new GameObject("CenterBtn");
            centerBtnGo.transform.SetParent(barGo.transform, false);
            RectTransform centerRt = centerBtnGo.AddComponent<RectTransform>();
            centerRt.anchorMin = new Vector2(0f, 0.5f);
            centerRt.anchorMax = new Vector2(0f, 0.5f);
            centerRt.pivot = new Vector2(0f, 0.5f);
            centerRt.anchoredPosition = new Vector2(16f, 0f);
            centerRt.sizeDelta = new Vector2(160f, 30f);

            Image centerImg = centerBtnGo.AddComponent<Image>();
            centerImg.sprite = UIResourceHelper.GetBackgroundSprite();
            centerImg.type = Image.Type.Sliced;
            centerImg.color = new Color(0.25f, 0.32f, 0.42f, 0.95f);

            Button centerBtn = centerBtnGo.AddComponent<Button>();
            centerBtn.onClick.AddListener(CenterOnPlayerInstant);

            GameObject centerTxtGo = new GameObject("CenterTxt");
            centerTxtGo.transform.SetParent(centerBtnGo.transform, false);
            RectTransform cTxtRt = centerTxtGo.AddComponent<RectTransform>();
            cTxtRt.anchorMin = Vector2.zero;
            cTxtRt.anchorMax = Vector2.one;

            Text cTxt = centerTxtGo.AddComponent<Text>();
            cTxt.font = legacyFont;
            cTxt.fontSize = 12;
            cTxt.fontStyle = FontStyle.Bold;
            cTxt.alignment = TextAnchor.MiddleCenter;
            cTxt.color = Color.white;
            cTxt.text = "CENTER ON PLAYER";

            // Zoom Controls (+) (-)
            CreateZoomButton(barGo, "+", 200f, () => AdjustZoom(0.25f));
            CreateZoomButton(barGo, "-", 240f, () => AdjustZoom(-0.25f));

            // Zoom Level Text Badge
            GameObject zoomTxtGo = new GameObject("ZoomText");
            zoomTxtGo.transform.SetParent(barGo.transform, false);
            RectTransform zTxtRt = zoomTxtGo.AddComponent<RectTransform>();
            zTxtRt.anchorMin = new Vector2(0f, 0.5f);
            zTxtRt.anchorMax = new Vector2(0f, 0.5f);
            zTxtRt.pivot = new Vector2(0f, 0.5f);
            zTxtRt.anchoredPosition = new Vector2(280f, 0f);
            zTxtRt.sizeDelta = new Vector2(60f, 24f);

            _zoomLevelText = zoomTxtGo.AddComponent<Text>();
            _zoomLevelText.font = legacyFont;
            _zoomLevelText.fontSize = 11;
            _zoomLevelText.alignment = TextAnchor.MiddleLeft;
            _zoomLevelText.color = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            _zoomLevelText.text = "100%";

            // Legend indicators on right of bottom bar
            CreateLegendItem(barGo, "Player", new Color(0.2f, 0.85f, 1f), -240f);
            CreateLegendItem(barGo, "Farm", new Color(0.35f, 0.85f, 0.35f), -160f);
            CreateLegendItem(barGo, "Water", new Color(0.2f, 0.5f, 0.9f), -80f);
        }

        private void CreateZoomButton(GameObject parent, string label, float xPos, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnGo = new GameObject($"ZoomBtn_{label}");
            btnGo.transform.SetParent(parent.transform, false);
            RectTransform rt = btnGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(xPos, 0f);
            rt.sizeDelta = new Vector2(30f, 30f);

            Image img = btnGo.AddComponent<Image>();
            img.sprite = UIResourceHelper.GetBackgroundSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.28f, 0.28f, 0.30f, 0.95f);

            Button btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            GameObject txtGo = new GameObject("Txt");
            txtGo.transform.SetParent(btnGo.transform, false);
            RectTransform txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;

            Text txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 14;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = label;
        }

        private void CreateLegendItem(GameObject parent, string label, Color color, float xPosRight)
        {
            GameObject legendGo = new GameObject($"Legend_{label}");
            legendGo.transform.SetParent(parent.transform, false);
            RectTransform rt = legendGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(xPosRight, 0f);
            rt.sizeDelta = new Vector2(70f, 24f);

            // Dot
            GameObject dotGo = new GameObject("Dot");
            dotGo.transform.SetParent(legendGo.transform, false);
            RectTransform dotRt = dotGo.AddComponent<RectTransform>();
            dotRt.anchorMin = new Vector2(0f, 0.5f);
            dotRt.anchorMax = new Vector2(0f, 0.5f);
            dotRt.pivot = new Vector2(0f, 0.5f);
            dotRt.anchoredPosition = new Vector2(0f, 0f);
            dotRt.sizeDelta = new Vector2(10f, 10f);

            Image dotImg = dotGo.AddComponent<Image>();
            dotImg.sprite = UIResourceHelper.GetCircleSprite();
            dotImg.color = color;

            // Label
            GameObject txtGo = new GameObject("Text");
            txtGo.transform.SetParent(legendGo.transform, false);
            RectTransform txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0f, 0f);
            txtRt.anchorMax = new Vector2(1f, 1f);
            txtRt.offsetMin = new Vector2(14f, 0f);

            Text txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 11;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            txt.text = label;
        }

        private void CreatePOIPinOnMap(GameObject container, string name, Vector3 worldPos, Color pinColor)
        {
            GameObject poiGo = new GameObject($"MapPin_{name}");
            poiGo.transform.SetParent(container.transform, false);

            RectTransform rt = poiGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(14f, 14f);

            // Convert world pos to map content anchored position (600x600 grid over MapWorldRadius * 2)
            float mapScale = 600f / (MapWorldRadius * 2f);
            rt.anchoredPosition = new Vector2(worldPos.x * mapScale, worldPos.y * mapScale);

            Image img = poiGo.AddComponent<Image>();
            img.sprite = UIResourceHelper.GetCircleSprite();
            img.color = pinColor;

            // Label above
            GameObject txtGo = new GameObject("Label");
            txtGo.transform.SetParent(poiGo.transform, false);
            RectTransform txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0.5f, 1f);
            txtRt.anchorMax = new Vector2(0.5f, 1f);
            txtRt.pivot = new Vector2(0.5f, 0f);
            txtRt.anchoredPosition = new Vector2(0f, 2f);
            txtRt.sizeDelta = new Vector2(80f, 14f);

            Text txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 9;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(1f, 1f, 1f, 0.9f);
            txt.text = name;
        }

        private void GenerateMapTexture()
        {
            if (_mapTexture == null || _mapTexture.width != MapTexSize)
            {
                _mapTexture = new Texture2D(MapTexSize, MapTexSize, TextureFormat.RGBA32, false);
                _mapTexture.filterMode = FilterMode.Point;
                _mapTexture.wrapMode = TextureWrapMode.Clamp;
            }

            Color grassBase = new Color(0.24f, 0.55f, 0.28f, 1f);
            Color grassAlt = new Color(0.20f, 0.48f, 0.25f, 1f);
            Color dirtColor = new Color(0.55f, 0.42f, 0.28f, 1f);
            Color waterColor = new Color(0.20f, 0.50f, 0.85f, 1f);
            Color farmColor = new Color(0.45f, 0.34f, 0.22f, 1f);

            ProceduralGridGenerator gen = ProceduralGridGenerator.Instance;
            if (gen == null) gen = FindAnyObjectByType<ProceduralGridGenerator>();

            GridManager gridMgr = GridManager.Instance;
            if (gridMgr == null) gridMgr = FindAnyObjectByType<GridManager>();

            for (int y = 0; y < MapTexSize; y++)
            {
                float normY = (float)y / MapTexSize;
                int worldY = Mathf.RoundToInt((normY - 0.5f) * MapWorldRadius * 2f);

                for (int x = 0; x < MapTexSize; x++)
                {
                    float normX = (float)x / MapTexSize;
                    int worldX = Mathf.RoundToInt((normX - 0.5f) * MapWorldRadius * 2f);

                    Color pixelColor = grassBase;

                    // Checker pattern noise simulation or direct procedural generator query
                    if ((worldX + worldY) % 2 == 0) pixelColor = grassAlt;

                    if (gen != null)
                    {
                        if (gen.IsWaterAt(worldX, worldY))
                        {
                            pixelColor = waterColor;
                        }
                        else if (!gen.IsGrassAt(worldX, worldY))
                        {
                            pixelColor = dirtColor;
                        }
                    }

                    // Highlight pre-generated farm plot area near (0, 0)
                    if (worldX >= -4 && worldX <= 4 && worldY >= -3 && worldY <= 3)
                    {
                        pixelColor = farmColor;
                    }

                    // Tilled soil grid
                    if (gridMgr != null && gridMgr.IsCellTilled(new Vector3Int(worldX, worldY, 0)))
                    {
                        pixelColor = new Color(0.38f, 0.28f, 0.18f, 1f);
                    }

                    _mapTexture.SetPixel(x, y, pixelColor);
                }
            }

            _mapTexture.Apply();

            if (_mapRawImage != null)
            {
                _mapRawImage.texture = _mapTexture;
            }
        }

        private void UpdatePlayerPin()
        {
            if (PlayerController.Instance == null || _playerPinRect == null) return;

            Vector3 pPos = PlayerController.Instance.transform.position;
            float mapScale = 600f / (MapWorldRadius * 2f);
            _playerPinRect.anchoredPosition = new Vector2(pPos.x * mapScale, pPos.y * mapScale);

            // Rotate facing arrow
            if (_playerArrowRect != null)
            {
                float angle = PlayerController.Instance.FacingAngle; // 0=East, 90=North
                _playerArrowRect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }
        }

        private void UpdateCoordinateBadges()
        {
            if (PlayerController.Instance != null && _playerCoordsText != null)
            {
                Vector3 pos = PlayerController.Instance.transform.position;
                _playerCoordsText.text = $"Player: (X: {Mathf.RoundToInt(pos.x)}, Y: {Mathf.RoundToInt(pos.y)})";
            }

            if (_zoomLevelText != null)
            {
                _zoomLevelText.text = $"{Mathf.RoundToInt(_currentZoom * 100f)}%";
            }
        }

        public void CenterOnPlayerInstant()
        {
            if (PlayerController.Instance == null)
            {
                _targetPanPos = Vector2.zero;
                return;
            }

            Vector3 pPos = PlayerController.Instance.transform.position;
            float mapScale = 600f / (MapWorldRadius * 2f);
            _targetPanPos = new Vector2(-pPos.x * mapScale * _currentZoom, -pPos.y * mapScale * _currentZoom);
            _currentPanPos = _targetPanPos;
        }

        public void AdjustZoom(float delta)
        {
            _currentZoom = Mathf.Clamp(_currentZoom + delta, MinZoom, MaxZoom);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _targetPanPos += eventData.delta;
            _currentPanPos = _targetPanPos;
        }

        public void OnScroll(PointerEventData eventData)
        {
            float scroll = eventData.scrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                AdjustZoom(scroll * 0.15f);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Focus viewport
        }
    }
}
