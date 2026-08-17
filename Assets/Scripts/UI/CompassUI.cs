using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Willowstead.Player;

namespace Willowstead.UI
{
    /// <summary>
    /// Premium Top-HUD Compass bar with rich fantasy-rpg aesthetics.
    /// Displays cardinal & intercardinal direction markers (N, NE, E, SE, S, SW, W, NW),
    /// notch ticks, dynamic heading badge, and tracked world POIs relative to player heading.
    /// </summary>
    public class CompassUI : MonoBehaviour
    {
        public static CompassUI Instance { get; private set; }

        private GameObject _compassRootGo;
        private RectTransform _tickerTransform;
        private TextMeshProUGUI _headingDegreeText;
        private Button _mapButton;

        private struct DirectionMarker
        {
            public string label;
            public float bearing;
            public TextMeshProUGUI textElement;
            public RectTransform rectTransform;
            public bool isMajor;
            public Color baseColor;
        }

        private readonly List<DirectionMarker> _directionMarkers = new List<DirectionMarker>();

        private struct TickMarker
        {
            public float bearing;
            public RectTransform rectTransform;
            public Image image;
            public bool isMedium;
        }

        private readonly List<TickMarker> _tickMarkers = new List<TickMarker>();

        public class POIInfo
        {
            public string id;
            public string name;
            public Vector3 worldPosition;
            public Color iconColor;
            public GameObject uiGo;
            public RectTransform rectTransform;
            public TextMeshProUGUI distanceText;
            public Image iconImage;
        }

        private readonly List<POIInfo> _poiList = new List<POIInfo>();

        private const float PixelsPerDegree = 3.5f;
        private const float TickerWidth = 420f;

        private float _currentBearing = 0f;

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

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;

            _compassRootGo = new GameObject("CompassPanel", typeof(RectTransform));
            _compassRootGo.transform.SetParent(canvas.transform, false);

            CanvasGroup cg = _compassRootGo.AddComponent<CanvasGroup>();
            if (MainMenuUI.Instance != null && !MainMenuUI.HasGameStarted)
            {
                cg.alpha = 0f;
            }

            RectTransform mainRect = (RectTransform)_compassRootGo.transform;
            mainRect.anchorMin = new Vector2(0.5f, 1f);
            mainRect.anchorMax = new Vector2(0.5f, 1f);
            mainRect.pivot = new Vector2(0.5f, 1f);
            mainRect.anchoredPosition = new Vector2(0f, -14f);
            mainRect.sizeDelta = new Vector2(520f, 48f);

            GameObject shadowGo = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            shadowGo.transform.SetParent(_compassRootGo.transform, false);
            RectTransform shadowRt = (RectTransform)shadowGo.transform;
            shadowRt.anchorMin = Vector2.zero; shadowRt.anchorMax = Vector2.one;
            shadowRt.offsetMin = new Vector2(-4f, -4f);
            shadowRt.offsetMax = new Vector2(4f, 2f);
            Image shadowImg = shadowGo.GetComponent<Image>();
            shadowImg.sprite = UIResourceHelper.GetBackgroundSprite();
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = new Color(0f, 0f, 0f, 0.5f);

            GameObject outerFrameGo = new GameObject("OuterFrame", typeof(RectTransform), typeof(Image));
            outerFrameGo.transform.SetParent(_compassRootGo.transform, false);
            RectTransform outerRt = (RectTransform)outerFrameGo.transform;
            outerRt.anchorMin = Vector2.zero; outerRt.anchorMax = Vector2.one;
            outerRt.offsetMin = Vector2.zero; outerRt.offsetMax = Vector2.zero;
            Image outerImg = outerFrameGo.GetComponent<Image>();
            outerImg.sprite = UIResourceHelper.GetBackgroundSprite();
            outerImg.type = Image.Type.Sliced;
            outerImg.color = new Color(0.85f, 0.65f, 0.48f, 1f); // Warm peach-gold framing outline

            GameObject goldBorderGo = new GameObject("GoldBorder", typeof(RectTransform), typeof(Image));
            goldBorderGo.transform.SetParent(outerFrameGo.transform, false);
            RectTransform goldRt = (RectTransform)goldBorderGo.transform;
            goldRt.anchorMin = Vector2.zero; goldRt.anchorMax = Vector2.one;
            goldRt.offsetMin = new Vector2(2f, 2f);
            goldRt.offsetMax = new Vector2(-2f, -2f);
            Image goldImg = goldBorderGo.GetComponent<Image>();
            goldImg.sprite = UIResourceHelper.GetBackgroundSprite();
            goldImg.type = Image.Type.Sliced;
            goldImg.color = new Color(0.48f, 0.35f, 0.32f, 0.98f); // Soft cozy mauve-brown slate

            GameObject innerBgGo = new GameObject("InnerBackground", typeof(RectTransform), typeof(Image));
            innerBgGo.transform.SetParent(goldBorderGo.transform, false);
            RectTransform innerRt = (RectTransform)innerBgGo.transform;
            innerRt.anchorMin = Vector2.zero; innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(3f, 3f);
            innerRt.offsetMax = new Vector2(-104f, -3f); // Leaves space on right for Skills + Map buttons
            Image innerImg = innerBgGo.GetComponent<Image>();
            innerImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            innerImg.type = Image.Type.Sliced;
            innerImg.color = new Color(0.32f, 0.22f, 0.20f, 0.95f);

            GameObject maskGo = new GameObject("TickerMask", typeof(RectTransform), typeof(RectMask2D));
            maskGo.transform.SetParent(innerBgGo.transform, false);
            RectTransform maskRect = (RectTransform)maskGo.transform;
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = new Vector2(10f, 2f);
            maskRect.offsetMax = new Vector2(-10f, -2f);

            GameObject tickerGo = new GameObject("Ticker", typeof(RectTransform));
            tickerGo.transform.SetParent(maskGo.transform, false);
            _tickerTransform = (RectTransform)tickerGo.transform;
            _tickerTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _tickerTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _tickerTransform.pivot = new Vector2(0.5f, 0.5f);
            _tickerTransform.anchoredPosition = Vector2.zero;
            _tickerTransform.sizeDelta = new Vector2(TickerWidth, 34f);

            _tickMarkers.Clear();
            for (int deg = 0; deg < 360; deg += 15)
            {
                if (deg % 45 == 0) continue; // Major/intercardinals have labels

                bool isMedium = (deg % 30 == 0);
                GameObject tickGo = new GameObject($"Tick_{deg}", typeof(RectTransform), typeof(Image));
                tickGo.transform.SetParent(_tickerTransform, false);
                RectTransform trt = (RectTransform)tickGo.transform;
                trt.anchorMin = new Vector2(0.5f, 0.5f);
                trt.anchorMax = new Vector2(0.5f, 0.5f);
                trt.pivot = new Vector2(0.5f, 0.5f);
                trt.sizeDelta = isMedium ? new Vector2(2f, 12f) : new Vector2(1.5f, 7f);

                Image tImg = tickGo.GetComponent<Image>();
                tImg.color = isMedium ? new Color(0.88f, 0.76f, 0.48f, 0.65f) : new Color(0.68f, 0.68f, 0.68f, 0.40f);

                _tickMarkers.Add(new TickMarker
                {
                    bearing = deg,
                    rectTransform = trt,
                    image = tImg,
                    isMedium = isMedium
                });
            }

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
                GameObject lblGo = new GameObject($"Dir_{lbl}", typeof(RectTransform));
                lblGo.transform.SetParent(_tickerTransform, false);

                RectTransform rt = (RectTransform)lblGo.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(40f, 30f);

                var txt = lblGo.AddComponent<TextMeshProUGUI>();
                if (font != null) txt.font = font;
                txt.fontSize = isMaj ? 16 : 12;
                txt.fontStyle = isMaj ? FontStyles.Bold : FontStyles.Normal;
                txt.alignment = TextAlignmentOptions.Center;
                txt.richText = false;

                Color baseCol;
                if (lbl == "N")
                    baseCol = new Color(1f, 0.35f, 0.35f, 1f); // Vibrant Ruby Red for True North
                else if (isMaj)
                    baseCol = new Color(1f, 0.88f, 0.48f, 1f); // Warm Gold for Major Cardinals (E, S, W)
                else
                    baseCol = new Color(0.88f, 0.88f, 0.85f, 0.85f); // Soft Silver for Intercardinals (NE, SE, SW, NW)

                txt.color = baseCol;
                txt.text = lbl;

                _directionMarkers.Add(new DirectionMarker
                {
                    label = lbl,
                    bearing = brg,
                    textElement = txt,
                    rectTransform = rt,
                    isMajor = isMaj,
                    baseColor = baseCol
                });
            }

            GameObject caretGo = new GameObject("CenterCaret", typeof(RectTransform), typeof(Image));
            caretGo.transform.SetParent(innerBgGo.transform, false);
            RectTransform caretRect = (RectTransform)caretGo.transform;
            caretRect.anchorMin = new Vector2(0.5f, 1f);
            caretRect.anchorMax = new Vector2(0.5f, 1f);
            caretRect.pivot = new Vector2(0.5f, 1f);
            caretRect.anchoredPosition = new Vector2(0f, 1f);
            caretRect.sizeDelta = new Vector2(10f, 12f);

            Image caretImg = caretGo.GetComponent<Image>();
            caretImg.sprite = UIResourceHelper.GetCircleSprite();
            caretImg.color = new Color(1f, 0.84f, 0.28f, 1f);

            GameObject degGo = new GameObject("HeadingBadge", typeof(RectTransform));
            degGo.transform.SetParent(_compassRootGo.transform, false);
            RectTransform degRect = (RectTransform)degGo.transform;
            degRect.anchorMin = new Vector2(0.5f, 0f);
            degRect.anchorMax = new Vector2(0.5f, 0f);
            degRect.pivot = new Vector2(0.5f, 1f);
            degRect.anchoredPosition = new Vector2(-48f, -2f);
            degRect.sizeDelta = new Vector2(100f, 18f);

            _headingDegreeText = degGo.AddComponent<TextMeshProUGUI>();
            if (font != null) _headingDegreeText.font = font;
            _headingDegreeText.fontSize = 11;
            _headingDegreeText.fontStyle = FontStyles.Bold;
            _headingDegreeText.alignment = TextAlignmentOptions.Center;
            _headingDegreeText.color = new Color(0.92f, 0.80f, 0.55f, 0.95f);
            _headingDegreeText.text = "0° N";

            // ── 1. SKILLS BUTTON [K] ──────────────────────────────────────────
            GameObject skillsBtnGo = new GameObject("OpenSkillsButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Player.UIHoverScale));
            skillsBtnGo.transform.SetParent(_compassRootGo.transform, false);
            RectTransform sBtnRect = (RectTransform)skillsBtnGo.transform;
            sBtnRect.anchorMin = new Vector2(1f, 0.5f);
            sBtnRect.anchorMax = new Vector2(1f, 0.5f);
            sBtnRect.pivot = new Vector2(1f, 0.5f);
            sBtnRect.anchoredPosition = new Vector2(-54f, 0f);
            sBtnRect.sizeDelta = new Vector2(46f, 36f);

            Image sBtnImg = skillsBtnGo.GetComponent<Image>();
            sBtnImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            sBtnImg.type = Image.Type.Sliced;
            sBtnImg.color = new Color(0.96f, 0.90f, 0.82f, 1f); // Warm parchment

            Button sBtn = skillsBtnGo.GetComponent<Button>();
            ColorBlock scb = sBtn.colors;
            scb.normalColor = new Color(0.96f, 0.90f, 0.82f, 1f);
            scb.highlightedColor = new Color(1.0f, 0.96f, 0.90f, 1f);
            scb.pressedColor = new Color(0.85f, 0.78f, 0.68f, 1f);
            sBtn.colors = scb;

            sBtn.onClick.AddListener(() =>
            {
                if (Player.SkillsUI.Instance != null)
                {
                    Player.SkillsUI.Instance.ToggleUI();
                }
                else
                {
                    var skillsUI = Object.FindAnyObjectByType<Player.SkillsUI>();
                    if (skillsUI != null) skillsUI.ToggleUI();
                }
            });

            GameObject sBtnTxtGo = new GameObject("SkillsText", typeof(RectTransform));
            sBtnTxtGo.transform.SetParent(skillsBtnGo.transform, false);
            RectTransform sBtnTxtRt = (RectTransform)sBtnTxtGo.transform;
            sBtnTxtRt.anchorMin = Vector2.zero; sBtnTxtRt.anchorMax = Vector2.one;
            sBtnTxtRt.offsetMin = Vector2.zero; sBtnTxtRt.offsetMax = Vector2.zero;

            var sBtnTxt = sBtnTxtGo.AddComponent<TextMeshProUGUI>();
            if (font != null) sBtnTxt.font = font;
            sBtnTxt.fontSize = 9.5f;
            sBtnTxt.fontStyle = FontStyles.Bold;
            sBtnTxt.alignment = TextAlignmentOptions.Center;
            sBtnTxt.color = new Color(0.32f, 0.22f, 0.16f, 1f);
            sBtnTxt.text = "BOOK\n<size=7.5>[K]</size>";

            // ── 2. MAP BUTTON [M] ─────────────────────────────────────────────
            GameObject mapBtnGo = new GameObject("OpenMapButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Player.UIHoverScale));
            mapBtnGo.transform.SetParent(_compassRootGo.transform, false);
            RectTransform btnRect = (RectTransform)mapBtnGo.transform;
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.anchoredPosition = new Vector2(-6f, 0f);
            btnRect.sizeDelta = new Vector2(46f, 36f);

            Image btnImg = mapBtnGo.GetComponent<Image>();
            btnImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            btnImg.type = Image.Type.Sliced;
            btnImg.color = new Color(0.96f, 0.90f, 0.82f, 1f); // Warm parchment

            _mapButton = mapBtnGo.GetComponent<Button>();
            ColorBlock cb = _mapButton.colors;
            cb.normalColor = new Color(0.96f, 0.90f, 0.82f, 1f);
            cb.highlightedColor = new Color(1.0f, 0.96f, 0.90f, 1f);
            cb.pressedColor = new Color(0.85f, 0.78f, 0.68f, 1f);
            _mapButton.colors = cb;

            _mapButton.onClick.AddListener(() =>
            {
                if (FullMapUI.Instance != null)
                {
                    FullMapUI.Instance.ToggleMap();
                }
            });

            GameObject btnTxtGo = new GameObject("MapText", typeof(RectTransform));
            btnTxtGo.transform.SetParent(mapBtnGo.transform, false);
            RectTransform btnTxtRt = (RectTransform)btnTxtGo.transform;
            btnTxtRt.anchorMin = Vector2.zero; btnTxtRt.anchorMax = Vector2.one;
            btnTxtRt.offsetMin = Vector2.zero; btnTxtRt.offsetMax = Vector2.zero;

            var btnTxt = btnTxtGo.AddComponent<TextMeshProUGUI>();
            if (font != null) btnTxt.font = font;
            btnTxt.fontSize = 9.5f;
            btnTxt.fontStyle = FontStyles.Bold;
            btnTxt.alignment = TextAlignmentOptions.Center;
            btnTxt.color = new Color(0.32f, 0.22f, 0.16f, 1f);
            btnTxt.text = "MAP\n<size=7.5>[M]</size>";
        }

        private void InitializeDefaultPOIs()
        {
            _poiList.Clear();
            AddPOI("home", "Farm Plot", Vector3.zero, new Color(0.35f, 0.85f, 0.35f, 1f));
            AddPOI("shop", "Merchant Shop", new Vector3(8f, 4f, 0f), new Color(0.95f, 0.75f, 0.25f, 1f));
        }

        public void AddPOI(string id, string name, Vector3 worldPos, Color iconColor)
        {
            RemovePOI(id);

            GameObject poiGo = new GameObject($"POI_{id}", typeof(RectTransform));
            poiGo.transform.SetParent(_tickerTransform, false);

            RectTransform rt = (RectTransform)poiGo.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(18f, 18f);

            GameObject glowGo = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(poiGo.transform, false);
            RectTransform glowRt = (RectTransform)glowGo.transform;
            glowRt.anchorMin = Vector2.zero; glowRt.anchorMax = Vector2.one;
            glowRt.offsetMin = new Vector2(-2f, -2f); glowRt.offsetMax = new Vector2(2f, 2f);
            Image glowImg = glowGo.GetComponent<Image>();
            glowImg.sprite = UIResourceHelper.GetCircleSprite();
            glowImg.color = new Color(0f, 0f, 0f, 0.6f);

            GameObject dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dotGo.transform.SetParent(poiGo.transform, false);
            RectTransform dotRt = (RectTransform)dotGo.transform;
            dotRt.anchorMin = Vector2.zero; dotRt.anchorMax = Vector2.one;
            dotRt.offsetMin = Vector2.zero; dotRt.offsetMax = Vector2.zero;
            Image iconImg = dotGo.GetComponent<Image>();
            iconImg.sprite = UIResourceHelper.GetCircleSprite();
            iconImg.color = iconColor;

            GameObject distGo = new GameObject("DistText", typeof(RectTransform));
            distGo.transform.SetParent(poiGo.transform, false);
            RectTransform distRt = (RectTransform)distGo.transform;
            distRt.anchorMin = new Vector2(0.5f, 0f);
            distRt.anchorMax = new Vector2(0.5f, 0f);
            distRt.pivot = new Vector2(0.5f, 1f);
            distRt.anchoredPosition = new Vector2(0f, -2f);
            distRt.sizeDelta = new Vector2(70f, 12f);

            var distTxt = distGo.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) distTxt.font = TMP_Settings.defaultFontAsset;
            distTxt.fontSize = 8.5f;
            distTxt.alignment = TextAlignmentOptions.Center;
            distTxt.color = new Color(0.92f, 0.92f, 0.92f, 0.9f);
            distTxt.text = name;

            _poiList.Add(new POIInfo
            {
                id = id,
                name = name,
                worldPosition = worldPos,
                iconColor = iconColor,
                uiGo = poiGo,
                rectTransform = rt,
                distanceText = distTxt,
                iconImage = iconImg
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

            float angle = PlayerController.Instance.FacingAngle; // 0=East, 90=North, 180=West, 270=South
            float bearing = 90f - angle;
            if (bearing < 0f) bearing += 360f;

            _currentBearing = Mathf.LerpAngle(_currentBearing, bearing, Time.deltaTime * 14f);

            foreach (var marker in _directionMarkers)
            {
                float deltaAngle = Mathf.DeltaAngle(_currentBearing, marker.bearing);
                float xPos = deltaAngle * PixelsPerDegree;

                marker.rectTransform.anchoredPosition = new Vector2(xPos, 0f);

                float edgeFactor = Mathf.Clamp01(1f - (Mathf.Abs(xPos) / (TickerWidth * 0.48f)));
                float alpha = Mathf.SmoothStep(0f, 1f, edgeFactor);
                Color col = marker.baseColor;
                col.a = alpha * marker.baseColor.a;
                marker.textElement.color = col;
            }

            foreach (var tick in _tickMarkers)
            {
                float deltaAngle = Mathf.DeltaAngle(_currentBearing, tick.bearing);
                float xPos = deltaAngle * PixelsPerDegree;

                tick.rectTransform.anchoredPosition = new Vector2(xPos, 0f);

                float edgeFactor = Mathf.Clamp01(1f - (Mathf.Abs(xPos) / (TickerWidth * 0.48f)));
                float alpha = Mathf.SmoothStep(0f, 1f, edgeFactor);
                Color col = tick.image.color;
                col.a = alpha * (tick.isMedium ? 0.7f : 0.4f);
                tick.image.color = col;
            }

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

                float poiAngleRad = Mathf.Atan2(diff.x, diff.y);
                float poiBearing = poiAngleRad * Mathf.Rad2Deg;
                if (poiBearing < 0f) poiBearing += 360f;

                float deltaAngle = Mathf.DeltaAngle(_currentBearing, poiBearing);
                float xPos = deltaAngle * PixelsPerDegree;

                poi.rectTransform.anchoredPosition = new Vector2(xPos, 2f);

                float edgeFactor = Mathf.Clamp01(1f - (Mathf.Abs(xPos) / (TickerWidth * 0.48f)));
                float alpha = Mathf.SmoothStep(0f, 1f, edgeFactor);

                if (poi.iconImage != null)
                {
                    Color c = poi.iconColor;
                    c.a = alpha;
                    poi.iconImage.color = c;
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
