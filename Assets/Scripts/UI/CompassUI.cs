using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Willowstead.Player;

namespace Willowstead.UI
{
    /// <summary>
    /// Programmatically constructs and manages the Top HUD Compass bar.
    /// Displays cardinal / intercardinal direction markers and tracked world POIs
    /// (Home / Farm, Water sources, Shop) relative to player position and heading.
    /// Pure code-built UI: 100% self-contained, requires zero inspector wiring.
    /// </summary>
    public class CompassUI : MonoBehaviour
    {
        public static CompassUI Instance { get; private set; }

        private GameObject _compassRootGo;
        private RectTransform _tickerTransform;
        private Text _headingDegreeText;
        private Button _mapButton;

        // Direction markers (Degrees relative to North: 0=N, 45=NE, 90=E, 135=SE, 180=S, 225=SW, 270=W, 315=NW)
        private struct DirectionMarker
        {
            public string label;
            public float bearing;
            public Text textElement;
            public RectTransform rectTransform;
            public bool isMajor;
        }

        private readonly List<DirectionMarker> _directionMarkers = new List<DirectionMarker>();

        // POI Marker Data
        public class POIInfo
        {
            public string id;
            public string name;
            public Vector3 worldPosition;
            public Color iconColor;
            public GameObject uiGo;
            public RectTransform rectTransform;
            public Text distanceText;
        }

        private readonly List<POIInfo> _poiList = new List<POIInfo>();

        // Pixels per degree on the horizontal compass ticker
        private const float PixelsPerDegree = 3.2f;
        private const float TickerWidth = 380f;

        private float _currentBearing = 0f; // 0 = North

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[CompassUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<CompassUI>();
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
            CreateCompassUI();
            InitializeDefaultPOIs();
        }

        private void Update()
        {
            // Listen for 'M' key to toggle Full Map
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame)
            {
                if (!Input.InputReader.BlockGameplayInput || (FullMapUI.Instance != null && FullMapUI.Instance.IsMapOpen))
                {
                    if (FullMapUI.Instance != null)
                    {
                        FullMapUI.Instance.ToggleMap();
                    }
                }
            }

            UpdateCompassHeading();
            UpdatePOIMarkers();
        }

        private void CreateCompassUI()
        {
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            if (canvas == null) return;
            UIResourceHelper.EnsureEventSystem();

            Transform existing = canvas.transform.Find("CompassPanel");
            if (existing != null) DestroyImmediate(existing.gameObject);

            // ── Main Compass Panel Box ──
            _compassRootGo = new GameObject("CompassPanel");
            _compassRootGo.transform.SetParent(canvas.transform, false);

            RectTransform mainRect = _compassRootGo.AddComponent<RectTransform>();
            mainRect.anchorMin = new Vector2(0.5f, 1f);
            mainRect.anchorMax = new Vector2(0.5f, 1f);
            mainRect.pivot = new Vector2(0.5f, 1f);
            mainRect.anchoredPosition = new Vector2(0f, -16f);
            mainRect.sizeDelta = new Vector2(460f, 44f);

            // Background frame
            Image bgImage = _compassRootGo.AddComponent<Image>();
            bgImage.sprite = UIResourceHelper.GetBackgroundSprite();
            bgImage.type = Image.Type.Sliced;
            bgImage.color = new Color(0.10f, 0.08f, 0.07f, 0.90f);

            // Subtle border outline panel
            GameObject borderGo = new GameObject("Border");
            borderGo.transform.SetParent(_compassRootGo.transform, false);
            RectTransform borderRect = borderGo.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            Image borderImg = borderGo.AddComponent<Image>();
            borderImg.sprite = UIResourceHelper.GetBackgroundSprite();
            borderImg.type = Image.Type.Sliced;
            borderImg.color = new Color(0.82f, 0.68f, 0.38f, 0.35f); // Soft gold frame tint

            // ── Center Pointer / Caret ──
            GameObject caretGo = new GameObject("CenterCaret");
            caretGo.transform.SetParent(_compassRootGo.transform, false);
            RectTransform caretRect = caretGo.AddComponent<RectTransform>();
            caretRect.anchorMin = new Vector2(0.5f, 1f);
            caretRect.anchorMax = new Vector2(0.5f, 1f);
            caretRect.pivot = new Vector2(0.5f, 1f);
            caretRect.anchoredPosition = new Vector2(-25f, -2f);
            caretRect.sizeDelta = new Vector2(10f, 10f);

            Image caretImg = caretGo.AddComponent<Image>();
            caretImg.sprite = UIResourceHelper.GetCircleSprite();
            caretImg.color = new Color(1f, 0.85f, 0.3f, 0.95f); // Bright golden indicator

            // ── Ticker Mask Viewport ──
            GameObject maskGo = new GameObject("TickerMask");
            maskGo.transform.SetParent(_compassRootGo.transform, false);
            RectTransform maskRect = maskGo.AddComponent<RectTransform>();
            maskRect.anchorMin = new Vector2(0.5f, 0.5f);
            maskRect.anchorMax = new Vector2(0.5f, 0.5f);
            maskRect.pivot = new Vector2(0.5f, 0.5f);
            maskRect.anchoredPosition = new Vector2(-25f, -1f);
            maskRect.sizeDelta = new Vector2(TickerWidth, 36f);

            maskGo.AddComponent<RectMask2D>();

            // Ticker container
            GameObject tickerGo = new GameObject("Ticker");
            tickerGo.transform.SetParent(maskGo.transform, false);
            _tickerTransform = tickerGo.AddComponent<RectTransform>();
            _tickerTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _tickerTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _tickerTransform.pivot = new Vector2(0.5f, 0.5f);
            _tickerTransform.anchoredPosition = Vector2.zero;
            _tickerTransform.sizeDelta = new Vector2(TickerWidth, 36f);

            // Font setup
            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ── Create Direction Labels (N, NE, E, SE, S, SW, W, NW) ──
            _directionMarkers.Clear();
            (string label, float bearing, bool major)[] directions = new[]
            {
                ("N", 0f, true),
                ("NE", 45f, false),
                ("E", 90f, true),
                ("SE", 135f, false),
                ("S", 180f, true),
                ("SW", 225f, false),
                ("W", 270f, true),
                ("NW", 315f, false)
            };

            foreach (var (lbl, brg, isMaj) in directions)
            {
                GameObject lblGo = new GameObject($"Dir_{lbl}");
                lblGo.transform.SetParent(_tickerTransform, false);

                RectTransform rt = lblGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(40f, 30f);

                Text txt = lblGo.AddComponent<Text>();
                txt.font = legacyFont;
                txt.fontSize = isMaj ? 16 : 12;
                txt.fontStyle = isMaj ? FontStyle.Bold : FontStyle.Normal;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = isMaj ? new Color(1f, 0.92f, 0.65f, 1f) : new Color(0.85f, 0.85f, 0.85f, 0.85f);
                if (lbl == "N") txt.color = new Color(0.95f, 0.35f, 0.35f, 1f); // Red N for North

                _directionMarkers.Add(new DirectionMarker
                {
                    label = lbl,
                    bearing = brg,
                    textElement = txt,
                    rectTransform = rt,
                    isMajor = isMaj
                });
            }

            // ── Heading Degree Text Badge ──
            GameObject degGo = new GameObject("HeadingBadge");
            degGo.transform.SetParent(_compassRootGo.transform, false);
            RectTransform degRect = degGo.AddComponent<RectTransform>();
            degRect.anchorMin = new Vector2(0.5f, 0f);
            degRect.anchorMax = new Vector2(0.5f, 0f);
            degRect.pivot = new Vector2(0.5f, 1f);
            degRect.anchoredPosition = new Vector2(-25f, 0f);
            degRect.sizeDelta = new Vector2(60f, 14f);

            _headingDegreeText = degGo.AddComponent<Text>();
            _headingDegreeText.font = legacyFont;
            _headingDegreeText.fontSize = 10;
            _headingDegreeText.alignment = TextAnchor.MiddleCenter;
            _headingDegreeText.color = new Color(0.75f, 0.70f, 0.60f, 0.9f);
            _headingDegreeText.text = "0° N";

            // ── Open Map Button (Right side of compass) ──
            GameObject btnGo = new GameObject("OpenMapButton");
            btnGo.transform.SetParent(_compassRootGo.transform, false);
            RectTransform btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.anchoredPosition = new Vector2(-6f, 0f);
            btnRect.sizeDelta = new Vector2(40f, 32f);

            Image btnImg = btnGo.AddComponent<Image>();
            btnImg.sprite = UIResourceHelper.GetBackgroundSprite();
            btnImg.type = Image.Type.Sliced;
            btnImg.color = new Color(0.22f, 0.26f, 0.32f, 0.95f);

            _mapButton = btnGo.AddComponent<Button>();
            ColorBlock colors = _mapButton.colors;
            colors.highlightedColor = new Color(0.35f, 0.42f, 0.55f, 1f);
            colors.pressedColor = new Color(0.15f, 0.18f, 0.25f, 1f);
            _mapButton.colors = colors;

            _mapButton.onClick.AddListener(() =>
            {
                if (FullMapUI.Instance != null)
                {
                    FullMapUI.Instance.ToggleMap();
                }
            });

            // Map button icon label
            GameObject btnTxtGo = new GameObject("MapText");
            btnTxtGo.transform.SetParent(btnGo.transform, false);
            RectTransform btnTxtRect = btnTxtGo.AddComponent<RectTransform>();
            btnTxtRect.anchorMin = Vector2.zero;
            btnTxtRect.anchorMax = Vector2.one;
            btnTxtRect.sizeDelta = Vector2.zero;

            Text btnTxt = btnTxtGo.AddComponent<Text>();
            btnTxt.font = legacyFont;
            btnTxt.fontSize = 11;
            btnTxt.fontStyle = FontStyle.Bold;
            btnTxt.alignment = TextAnchor.MiddleCenter;
            btnTxt.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            btnTxt.text = "MAP\n[M]";
        }

        private void InitializeDefaultPOIs()
        {
            _poiList.Clear();

            // 1. Home / Farm Plot POI
            AddPOI("home", "Farm Plot", Vector3.zero, new Color(0.35f, 0.85f, 0.35f, 1f));

            // 2. Shop POI
            AddPOI("shop", "Merchant Shop", new Vector3(8f, 4f, 0f), new Color(0.95f, 0.75f, 0.25f, 1f));
        }

        public void AddPOI(string id, string name, Vector3 worldPos, Color iconColor)
        {
            // Remove if existing
            RemovePOI(id);

            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject poiGo = new GameObject($"POI_{id}");
            poiGo.transform.SetParent(_tickerTransform, false);

            RectTransform rt = poiGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(24f, 24f);

            // Icon background dot
            Image iconImg = poiGo.AddComponent<Image>();
            iconImg.sprite = UIResourceHelper.GetCircleSprite();
            iconImg.color = iconColor;

            // Distance / label text below icon
            GameObject distGo = new GameObject("DistText");
            distGo.transform.SetParent(poiGo.transform, false);
            RectTransform distRt = distGo.AddComponent<RectTransform>();
            distRt.anchorMin = new Vector2(0.5f, 0f);
            distRt.anchorMax = new Vector2(0.5f, 0f);
            distRt.pivot = new Vector2(0.5f, 1f);
            distRt.anchoredPosition = new Vector2(0f, -2f);
            distRt.sizeDelta = new Vector2(60f, 12f);

            Text distTxt = distGo.AddComponent<Text>();
            distTxt.font = legacyFont;
            distTxt.fontSize = 9;
            distTxt.alignment = TextAnchor.MiddleCenter;
            distTxt.color = new Color(0.9f, 0.9f, 0.9f, 0.85f);
            distTxt.text = name;

            _poiList.Add(new POIInfo
            {
                id = id,
                name = name,
                worldPosition = worldPos,
                iconColor = iconColor,
                uiGo = poiGo,
                rectTransform = rt,
                distanceText = distTxt
            });
        }

        public void RemovePOI(string id)
        {
            for (int i = _poiList.Count - 1; i >= 0; i--)
            {
                if (_poiList[i].id == id)
                {
                    if (_poiList[i].uiGo != null) Destroy(_poiList[i].uiGo);
                    _poiList.RemoveAt(i);
                }
            }
        }

        private void UpdateCompassHeading()
        {
            if (PlayerController.Instance == null) return;

            // Convert facing angle to bearing where North (+Y) = 0°, East (+X) = 90°, South (-Y) = 180°, West (-X) = 270°
            float angle = PlayerController.Instance.FacingAngle; // 0=East, 90=North, 180=West, 270=South
            float bearing = 90f - angle;
            if (bearing < 0f) bearing += 360f;

            _currentBearing = Mathf.LerpAngle(_currentBearing, bearing, Time.deltaTime * 12f);

            // Update direction labels on horizontal ticker
            foreach (var marker in _directionMarkers)
            {
                float deltaAngle = Mathf.DeltaAngle(_currentBearing, marker.bearing);
                float xPos = deltaAngle * PixelsPerDegree;

                marker.rectTransform.anchoredPosition = new Vector2(xPos, 0f);

                // Fade out edges
                float alpha = Mathf.Clamp01(1f - (Mathf.Abs(xPos) / (TickerWidth * 0.48f)));
                Color col = marker.textElement.color;
                col.a = alpha;
                marker.textElement.color = col;
            }

            // Update degree text readout
            if (_headingDegreeText != null)
            {
                int deg = Mathf.RoundToInt(_currentBearing);
                string card = GetCardFromBearing(deg);
                _headingDegreeText.text = $"{deg}° {card}";
            }
        }

        private string GetCardFromBearing(float bearing)
        {
            if (bearing >= 337.5f || bearing < 22.5f) return "N";
            if (bearing >= 22.5f && bearing < 67.5f) return "NE";
            if (bearing >= 67.5f && bearing < 112.5f) return "E";
            if (bearing >= 112.5f && bearing < 157.5f) return "SE";
            if (bearing >= 157.5f && bearing < 202.5f) return "S";
            if (bearing >= 202.5f && bearing < 247.5f) return "SW";
            if (bearing >= 247.5f && bearing < 292.5f) return "W";
            return "NW";
        }

        private void UpdatePOIMarkers()
        {
            if (PlayerController.Instance == null) return;
            Vector3 playerPos = PlayerController.Instance.transform.position;

            foreach (var poi in _poiList)
            {
                if (poi.rectTransform == null) continue;

                Vector3 diff = poi.worldPosition - playerPos;
                diff.z = 0f;
                float dist = diff.magnitude;

                // Calculate bearing from player to POI
                float poiAngleRad = Mathf.Atan2(diff.x, diff.y); // North = 0
                float poiBearing = poiAngleRad * Mathf.Rad2Deg;
                if (poiBearing < 0f) poiBearing += 360f;

                float deltaAngle = Mathf.DeltaAngle(_currentBearing, poiBearing);
                float xPos = deltaAngle * PixelsPerDegree;

                poi.rectTransform.anchoredPosition = new Vector2(xPos, 4f);

                // Fade out edges
                float alpha = Mathf.Clamp01(1f - (Mathf.Abs(xPos) / (TickerWidth * 0.48f)));
                Image iconImg = poi.uiGo.GetComponent<Image>();
                if (iconImg != null)
                {
                    Color c = poi.iconColor;
                    c.a = alpha;
                    iconImg.color = c;
                }

                if (poi.distanceText != null)
                {
                    poi.distanceText.text = $"{poi.name} ({Mathf.RoundToInt(dist)}m)";
                    Color tc = poi.distanceText.color;
                    tc.a = alpha * 0.9f;
                    poi.distanceText.color = tc;
                }
            }
        }
    }
}
