using UnityEngine;
using UnityEngine.UI;

namespace Willowstead.World
{
    /// <summary>
    /// Lightweight day/night cycle that darkens/brightens the scene using a world-space
    /// SpriteRenderer quad parented to the main camera and stretched over the frustum.
    /// - TimeOfDay loops [0,1): 0 = midnight, 0.25 = dawn, 0.5 = noon, 0.75 = dusk
    /// - Brightness is sampled from an AnimationCurve; color comes from a Gradient
    /// - On midnight wrap, optionally calls GridManager.AdvanceDay()
    ///
    /// Render-path notes:
    /// • The tint lives in the SpriteRenderer queue, NOT the UI Canvas queue. Even if
    ///   the HUDCanvas sortingOrder is later tweaked, this quad cannot bleed into any
    ///   UI panel because the two queues never cross.
    /// • The quad is parented to Camera.main so it follows camera movement for free.
    /// • sortingOrder is intentionally very negative so dirt/grass/puddles draw on top
    ///   of it; for dynamic-order trees/objects, the parented localZ (near plane + ε)
    ///   ensures the tint still draws first.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }
        public int CurrentDay => _dayCount;
        private int _dayCount;

        [Header("Time Settings")]
        [Tooltip("How long a full day lasts, in real-time seconds.")]
        [SerializeField] private float _dayLengthSeconds = 480f; // 8 minutes

        [Tooltip("Global time scale multiplier for the cycle (1 = normal).")]
        [SerializeField] private float _timeScale = 1f;

        [Tooltip("Start time in [0,1). 0 = midnight, 0.25 = dawn, 0.5 = noon, 0.75 = dusk")]
        [Range(0f, 1f)] [SerializeField] private float _startTimeNormalized = 0.2f; // early morning

        [Header("Visuals")]
        [Tooltip("Brightness curve over the day. Value 0 = brightest (no overlay), 1 = darkest.")]
        [SerializeField] private AnimationCurve _brightnessCurve = new AnimationCurve(
            new Keyframe(0.00f, 1.00f),   // midnight
            new Keyframe(0.20f, 0.35f),   // pre-dawn
            new Keyframe(0.25f, 0.15f),   // dawn
            new Keyframe(0.50f, 0.00f),   // noon
            new Keyframe(0.75f, 0.15f),   // dusk
            new Keyframe(1.00f, 1.00f)    // midnight
        );

        [Tooltip("Overlay tint over the day (alpha is controlled by the brightness curve).")]
        [SerializeField] private Gradient _tintGradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.05f, 0.09f, 0.18f), 0.00f), // deep blue midnight
                new GradientColorKey(new Color(0.20f, 0.18f, 0.30f), 0.20f), // pre-dawn purple
                new GradientColorKey(new Color(0.25f, 0.25f, 0.28f), 0.25f), // dawn neutral
                new GradientColorKey(new Color(0.00f, 0.00f, 0.00f), 0.50f), // noon clear
                new GradientColorKey(new Color(0.25f, 0.20f, 0.18f), 0.75f), // dusk warm
                new GradientColorKey(new Color(0.05f, 0.09f, 0.18f), 1.00f), // midnight deep blue
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        };

        [Tooltip("Maximum overlay opacity at the darkest point.")]
        [Range(0f, 1f)] [SerializeField] private float _maxDarkAlpha = 1.0f;        [Header("Day Hooks")]
        [Tooltip("Advance crops and other systems at midnight when a new day begins.")]
        [SerializeField] private bool _advanceGridDayOnMidnight = true;
        [Tooltip("Trigger an extra growth tick at noon for half-day growth pacing.")]
        [SerializeField] private bool _halfDayGrowthAtNoon = true;

        [Tooltip("Show a simple optional clock (HH:MM) in the top-left.")]
        [SerializeField] private bool _showClock = false;

        [Header("Lightning")]
        [Tooltip("Seconds spent ramping the overlay color to full white when FlashLightning is called.")]
        [Range(0.01f, 0.20f)] [SerializeField] private float _lightningRampSeconds = 0.02f;
        [Tooltip("Seconds spent fading the overlay back to its natural tint after a lightning flash.")]
        [Range(0.05f, 0.50f)] [SerializeField] private float _lightningFadeSeconds = 0.08f;
        [Tooltip("Color of the lightning flash overlay (defaults to white).")]
        [SerializeField] private Color _lightningColor = new Color(1f, 1f, 1f, 1f);
        [Tooltip("Maximum alpha the lightning-flash can reach (0..1). Use lower values for softer, more distant flashes.")]
        [Range(0f, 1f)] [SerializeField] private float _lightningMaxAlpha = 0.85f;

        private float _time01;
        private SpriteRenderer _overlay;          // world-space tint quad
        private Text _clockText;                  // UI clock (still on HUDCanvas or Canvas fallback)
        private Camera _tintCamera;               // cached Camera.main
        private Coroutine _lightningRoutine;      // active lightning-flash coroutine (null when no flash in progress)

        // Camera-state cache; re-stretch only when frustum *size* changes.
        // Position changes are auto-tracked by the parent—transformation, so we don't cache them.
        private float _lastCameraOrthoSize;
        private float _lastCameraAspect;

        // Shared 1x1 white sprite used as the quad's texture (lazy-built once).
        private static Sprite _whiteSquareSprite;

        public float Time01 => _time01; // [0,1)
        public float SecondsPerDay { get => _dayLengthSeconds; set => _dayLengthSeconds = Mathf.Max(1f, value); }
        public float TimeScale { get => _timeScale; set => _timeScale = Mathf.Max(0f, value); }
        public bool IsPaused { get; set; } = false;


        private void Start()
        {
            _time01 = Mathf.Repeat(_startTimeNormalized, 1f);
            EnsureOverlay();
            UpdateVisuals();
            if (Instance == null) Instance = this;
        }

        private void Update()
        {
            if (IsPaused)
            {
                UpdateVisuals();
                return;
            }

            if (_dayLengthSeconds <= 0.01f) return;
            float prev = _time01;
            _time01 = Mathf.Repeat(_time01 + (Time.deltaTime * _timeScale) / _dayLengthSeconds, 1f);

            if (_halfDayGrowthAtNoon && GridManager.Instance != null)
            {
                if (prev < 0.5f && _time01 >= 0.5f)
                {
                    GridManager.Instance.AdvanceHalfDayGrowthTick();
                }
            }

            if (_time01 < prev && _advanceGridDayOnMidnight && GridManager.Instance != null)
            {
                GridManager.Instance.AdvanceDay();
                _dayCount++;
            }

            UpdateVisuals();
        }

        private void LateUpdate()
        {
            // Tint quad lives in the world queue but is parented to the camera so
            // it follows movement. Here we re-stretch only when the camera's frustum
            // *size* changes (different orthoSize / aspect).
            EnsureTintFollowCamera();
        }

        private void OnDestroy()
        {
            // Be tidy: destroy the quad we created. Without this, toggling DayNightCycle
            // on/off in the Editor leaves orphan nodes parented to Camera.main.
            if (_overlay != null)
            {
                if (Application.isPlaying) Destroy(_overlay.gameObject);
                else DestroyImmediate(_overlay.gameObject);
                _overlay = null;
            }
            _tintCamera = null;
        }


        private void EnsureTintFollowCamera()
        {
            if (_overlay == null) return;
            Camera cam = _tintCamera != null ? _tintCamera : Camera.main;
            if (cam == null) return;
            _tintCamera = cam;

            if (!CameraFrustumChanged(cam)) return;

            StretchToCameraFrustum(_overlay.transform, cam);
            CacheCameraState(cam);
        }

        private bool CameraFrustumChanged(Camera cam)
        {
            // Position is irrelevant: the quad is parented to the camera, so any camera
            // movement propagates automatically. We only re-stretch when the *size* of
            // the visible area changes (different orthoSize / aspect).
            return !Mathf.Approximately(_lastCameraOrthoSize, cam.orthographicSize)
                || !Mathf.Approximately(_lastCameraAspect, cam.aspect);
        }

        private void CacheCameraState(Camera cam)
        {
            _lastCameraOrthoSize  = cam.orthographicSize;
            _lastCameraAspect     = cam.aspect;
        }

        private static void StretchToCameraFrustum(Transform t, Camera cam)
        {
            // IMPORTANT: this assumes the tint sprite has pixelsPerUnit = 1 (see
            // sprite.pixelsPerUnit to keep the same world coverage.

            // frustum. The Default-layer sortingOrder of -32767 further guarantees
            // we draw before any world sprite regardless of Z.
            float z = cam.nearClipPlane + 0.01f;

            float h, w;
            if (cam.orthographic)
            {
                h = cam.orthographicSize * 2f;
                w = h * cam.aspect;
            }
            else
            {
                // Sized to fully cover the visible frustum at near plane distance.
                h = 2f * cam.nearClipPlane * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                w = h * cam.aspect;
            }

            t.localPosition = new Vector3(0f, 0f, z);
            t.localScale    = new Vector3(w, h, 1f);
            t.localRotation = Quaternion.identity;
        }


        private void UpdateVisuals()
        {
            if (_overlay == null) return;

            float darkness = Mathf.Clamp01(_brightnessCurve.Evaluate(_time01));
            Color tint = _tintGradient.Evaluate(_time01);
            tint.a = darkness * _maxDarkAlpha;
            _overlay.color = tint;

            if (_showClock && _clockText != null)
            {
                float totalMinutes = _time01 * 24f * 60f;
                int hh = Mathf.FloorToInt(totalMinutes / 60f) % 24;
                int mm = Mathf.FloorToInt(totalMinutes % 60f);
                _clockText.text = string.Format("{0:00}:{1:00}", hh, mm);
            }
        }


        public void SetTime01(float t)
        {
            _time01 = Mathf.Repeat(t, 1f);
            UpdateVisuals();
        }

        /// <summary>
        /// Briefly flashes the world tint overlay to white, simulating a lightning
        /// strike. Safe to call from any frame. Uses a coroutine for the
        /// ramp-up + fade so the flash interpolates smoothly back into the
        /// natural tint. Sourced by WeatherCycle.OnLightningStrike.
        /// </summary>
        public void FlashLightning(float durationSeconds = 0.08f)
        {
            if (_overlay == null) return;

            // Snapshot the color we'd be at if the flash weren't happening.
            // This is the tint the overlay will fade BACK to.
            float darkness = Mathf.Clamp01(_brightnessCurve.Evaluate(_time01));
            Color baseTint = _tintGradient.Evaluate(_time01);
            baseTint.a = darkness * _maxDarkAlpha;

            if (_lightningRoutine != null)
            {
                StopCoroutine(_lightningRoutine);
            }
            _lightningRoutine = StartCoroutine(FlashLightningRoutine(baseTint, Mathf.Max(0.01f, durationSeconds)));
        }

        private System.Collections.IEnumerator FlashLightningRoutine(Color baseTint, float totalDuration)
        {
            // doesn't spend longer flashing than fading.
            float ramp = Mathf.Min(_lightningRampSeconds, totalDuration * 0.5f);
            Color target = _lightningColor;
            float rampStartAlpha = _overlay.color.a;
            float t = 0f;
            while (t < ramp)
            {
                t += Time.unscaledDeltaTime;
                if (_overlay == null) yield break;
                float k = Mathf.Clamp01(t / Mathf.Max(0.001f, ramp));
                Color c = Color.Lerp(new Color(baseTint.r, baseTint.g, baseTint.b, rampStartAlpha), target, k);
                c.a = k * _lightningMaxAlpha;
                _overlay.color = c;
                yield return null;
            }

            // we always finish within the requested total.
            float fade = Mathf.Max(_lightningFadeSeconds, totalDuration - ramp);
            float fadeStartAlpha = _overlay.color.a;
            float fadeStartR = _overlay.color.r;
            float fadeStartG = _overlay.color.g;
            float fadeStartB = _overlay.color.b;
            t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                if (_overlay == null) yield break;
                float k = Mathf.Clamp01(t / Mathf.Max(0.001f, fade));
                Color c;
                c.r = Mathf.Lerp(fadeStartR, baseTint.r, k);
                c.g = Mathf.Lerp(fadeStartG, baseTint.g, k);
                c.b = Mathf.Lerp(fadeStartB, baseTint.b, k);
                c.a = Mathf.Lerp(fadeStartAlpha, baseTint.a, k);
                _overlay.color = c;
                yield return null;
            }

            // Snap exactly to base to avoid lerp drift.
            if (_overlay != null) _overlay.color = baseTint;
            _lightningRoutine = null;
        }

        /// <summary>
        /// Restore time-of-day and the day counter from a save file. Called
        /// by SaveGameManager.RestoreFromData after the world has
        /// regenerated so deterministic systems are already in place.
        /// </summary>
        public void RestoreTime(float time01, int dayCount)
        {
            _time01 = Mathf.Repeat(time01, 1f);
            _dayCount = Mathf.Max(0, dayCount);
            UpdateVisuals();
        }


        private void EnsureOverlay()
        {
            // itself does not live on any canvas.
            Player.UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas", "Canvas", "UIRoot");

            _tintCamera = Camera.main;
            if (_tintCamera == null)
            {
                Debug.LogWarning(
                    "[DayNightCycle] No Camera.main found; the day/night tint will not render. " +
                    "Tag your camera with the MainCamera tag in the Editor.", this);
                return;
            }

            GameObject tintGo = new GameObject("WorldTintQuad");
            _overlay = tintGo.AddComponent<SpriteRenderer>();
            _overlay.sprite                = GetOrCreateWhiteSquareSprite();
            _overlay.color                 = new Color(0f, 0f, 0f, 0f);
            _overlay.sortingLayerName      = "Default";
            // int.MinValue is unambiguously less than any sane world sprite (dirt at -32000,
            // dynamic-Y trees that go to ±10000) — guarantees we draw first even on the same X/Y.
            _overlay.sortingOrder          = int.MinValue;
            _overlay.lightProbeUsage       = UnityEngine.Rendering.LightProbeUsage.Off;
            _overlay.reflectionProbeUsage  = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _overlay.shadowCastingMode     = UnityEngine.Rendering.ShadowCastingMode.Off;
            _overlay.receiveShadows        = false;

            // Parent to camera so it follows camera movement for free; setting parent
            // resets local transform to identity, then we stretch + position.
            tintGo.transform.SetParent(_tintCamera.transform, false);
            StretchToCameraFrustum(tintGo.transform, _tintCamera);
            CacheCameraState(_tintCamera);

            if (_showClock)
            {
                Canvas hudCanvas = Player.UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas", "Canvas", "UIRoot");

                GameObject clockGo = new GameObject("ClockText");
                clockGo.transform.SetParent(hudCanvas.transform, false);
                _clockText = clockGo.AddComponent<Text>();
                _clockText.raycastTarget = false;
                _clockText.alignment     = TextAnchor.UpperLeft;
                _clockText.font          = Player.UIResourceHelper.GetPixelFont();
                _clockText.fontSize      = 20;
                _clockText.color         = new Color(1f, 1f, 1f, 0.85f);
                var ct = _clockText.rectTransform;
                ct.anchorMin        = new Vector2(0f, 1f); ct.anchorMax = new Vector2(0f, 1f);
                ct.pivot            = new Vector2(0f, 1f);
                ct.anchoredPosition = new Vector2(16f, -12f);
                ct.sizeDelta        = new Vector2(120f, 30f);
            }
        }

        //
        // IMPORTANT: pixelsPerUnit = 1 so 1 localScale unit maps to 1 world unit.
        // would shrink a 2x2 sprite to 0.02 world units — the quad would barely
        // cover a single tile and the tint would render as "a little square on
        // the character" right at the camera origin. Using a 1x1 texture with
        // ppu = 1 keeps the math in StretchToCameraFrustum honest: localScale
        // values *are* world sizes.

        private static Sprite GetOrCreateWhiteSquareSprite()
        {
            if (_whiteSquareSprite != null) return _whiteSquareSprite;

            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name       = "DayNightTint_WhiteSquare",
                hideFlags  = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point
            };
            Color[] pix = new Color[] { Color.white };
            tex.SetPixels(pix);
            tex.Apply(false, false);

            // pixelsPerUnit = 1 → 1 local unit == 1 world unit.
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            sprite.name      = "DayNightTint_WhiteSquare";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _whiteSquareSprite = sprite;
            return sprite;
        }
    }
}
