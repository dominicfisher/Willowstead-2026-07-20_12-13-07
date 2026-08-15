// Developer debug overlay — compiled out of release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Willowstead.Player;
using Willowstead.World;

namespace Willowstead.Debugging
{
    /// <summary>
    /// Right-side debug overlay panel that pops up when <c>=</c> (or <c>-</c> — same
    /// physical key on most layouts) is pressed. Press again to hide.
    ///
    /// While visible it shows:
    ///   • Day / Night cycle phase + HH:MM clock + day-progress
    ///   • Current weather state (tinted) + intensity
    ///   • Player position
    ///   • Main camera ortho size + world position
    ///   • FPS + Time.timeScale
    ///
    /// The overlay updates on a 0.5s cadence (not every frame) so the TMP mesh
    /// rebuild cost stays negligible even with the panel open for hours.
    /// Compiled out of release builds via the same #if guard as DevConsole.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        private static DebugOverlay _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return; // idempotent across scene reloads
            GameObject go = new GameObject("DebugOverlay");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DebugOverlay>();
        }

        private GameObject _panelGo;
        private TextMeshProUGUI _text;

        private float _updateTimer;
        private const float UPDATE_INTERVAL = 0.5f;

        // Cached references - refreshed lazily so re-creating systems (e.g. on
        // scene reload) doesn't leave the overlay pointing at destroyed objects.
        private DayNightCycle _cachedDayNight;
        private WeatherCycle _cachedWeather;
        private Transform _cachedPlayer;
        private Camera _cachedCamera;

        private void Awake()
        {
            _instance = this;
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas");
            BuildPanel(canvas);
            // Start hidden. Press = to open.
            _panelGo.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void BuildPanel(Canvas canvas)
        {
            _panelGo = new GameObject("DebugOverlayPanel", typeof(RectTransform), typeof(Image));
            RectTransform rt = _panelGo.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            // *exact* extent (no anchor-stretch). Without anchorMin.y == anchorMax.y
            // the rect would inherit `(anchorMax - anchorMin) * parent.height` and
            // (Day/Night + Weather) above the visible canvas.
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(16f, 0f);
            rt.sizeDelta = new Vector2(280f, 520f);

            Image bg = _panelGo.GetComponent<Image>();
            // Fully transparent backing — no visible box by user request. The Image
            // component is kept so the panel still occupies its RectTransform bounds
            // (deterministic layout) but contributes no visible color or sprite.
            // raycastTarget=false so the overlay never eats clicks meant for the world.
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = false;
            bg.sprite = null;

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(_panelGo.transform, false);
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = new Vector2(10f, 10f);
            textRt.offsetMax = new Vector2(-10f, -10f);

            _text = textGo.AddComponent<TextMeshProUGUI>();
            // Larger + bold + pure white so the text reads over the live world with
            // without a dark backdrop; FontStyles.Bold and pure white give enough
            // contrast on grass/dirt/trees to remain scannable.
            _text.fontSize = 22f;
            _text.fontStyle = FontStyles.Bold;
            _text.color = new Color(1f, 1f, 1f, 1f);
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.richText = true;
            _text.text = string.Empty;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.equalsKey.wasPressedThisFrame || kb.minusKey.wasPressedThisFrame))
            {
                Toggle();
            }

            if (_panelGo == null || !_panelGo.activeSelf) return;

            _updateTimer += Time.unscaledDeltaTime;
            if (_updateTimer < UPDATE_INTERVAL) return;
            _updateTimer = 0f;
            RefreshText();
        }

        /// <summary>
        /// Toggle visibility. Called from the input handler and exposed publicly so
        /// the dev console (or any future devtool) can drive the same panel.
        /// </summary>
        public void Toggle()
        {
            if (_panelGo == null) return;
            bool willShow = !_panelGo.activeSelf;
            _panelGo.SetActive(willShow);
            if (willShow)
            {
                _updateTimer = UPDATE_INTERVAL; // force an immediate refresh on first show
                RefreshText();
            }
        }

        private void RefreshText()
        {
            // Lazy cache refreshes (cheap because cached objects avoid the second call).
            if (_cachedDayNight == null) _cachedDayNight = FindAnyObjectByType<DayNightCycle>();
            if (_cachedWeather == null)  _cachedWeather  = FindAnyObjectByType<WeatherCycle>();
            if (_cachedCamera == null)   _cachedCamera   = Camera.main;

            string s = string.Empty;

            if (_cachedDayNight != null)
            {
                float t01 = _cachedDayNight.Time01;
                int totalMinutes = Mathf.FloorToInt(t01 * 24f * 60f);
                int h = (totalMinutes / 60) % 24;
                int m = totalMinutes % 60;
                string phase = DescribeDayPhase(t01);

                s += "<b>Day / Night</b>\n";
                s += $"  Phase: {phase}\n";
                s += $"  Time: <b>{h:00}:{m:00}</b>\n";
                s += $"  Day progress: {t01 * 24f:F1}h\n\n";
            }

            if (_cachedWeather != null)
            {
                s += "<b>Weather</b>\n";
                s += $"  State: {Tinted(_cachedWeather.CurrentWeather.ToString())}\n";
                s += $"  Intensity: {_cachedWeather.CurrentIntensity}\n\n";
            }

            if (_cachedPlayer == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null) _cachedPlayer = p.transform;
            }
            if (_cachedPlayer != null)
            {
                Vector3 pos = _cachedPlayer.position;
                s += "<b>Player</b>\n";
                s += $"  Pos: ({pos.x:F1}, {pos.y:F1})\n\n";
            }

            if (_cachedCamera != null)
            {
                Vector3 camPos = _cachedCamera.transform.position;
                s += "<b>Camera</b>\n";
                s += $"  Ortho size: {_cachedCamera.orthographicSize:F1}\n";
                s += $"  Pos: ({camPos.x:F1}, {camPos.y:F1})\n\n";
            }

            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            s += "<b>Runtime</b>\n";
            s += $"  FPS: {fps:F0}\n";
            s += $"  Time scale: {Time.timeScale:F2}\n";

            _text.text = s;
        }

        private static string DescribeDayPhase(float t01)
        {
            // Mirrors the brightness curve in DayNightCycle.cs so the labels stay in sync
            // with whatever curve the user has tuned.
            if (t01 < 0.20f) return "Night";
            if (t01 < 0.30f) return "Dawn";
            if (t01 < 0.70f) return "Day";
            if (t01 < 0.80f) return "Dusk";
            return "Night";
        }

        private static string Tinted(string text)
        {
            // Visual cue: Clear green, Windy amber, anything unknown red.
            if (text == "Clear") return $"<color=#80d27f>{text}</color>";
            if (text == "Windy") return $"<color=#e8c878>{text}</color>";
            return $"<color=#ff8585>{text}</color>";
        }
    }
}
#endif
