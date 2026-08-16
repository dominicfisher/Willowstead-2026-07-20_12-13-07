using UnityEngine;
using Willowstead.Player;

namespace Willowstead.World
{

    public enum WeatherType
    {
        Clear,
        Windy,
        Rainy,
    }

    public enum WindIntensity
    {
        Light,
        Moderate,
        Strong,
    }

    public enum WindDirection
    {
        Left,
        Right,
    }


    /// <summary>
    /// Lightweight weather cycle that runs alongside DayNightCycle. Weather state
    /// rolls at noon and midnight; wind is shown as multiple drifting gust
    /// sprites whose count depends on intensity. Also auto-bootstraps the rain
    /// sub-system (RainRenderer / RainSplash / RainAudio) so designers don't
    /// have to wire those manually.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeatherCycle : MonoBehaviour
    {
        public static WeatherCycle Instance { get; private set; }

        [Header("References")]
        [Tooltip("The day/night cycle to sync weather changes with. If unassigned, WeatherCycle still works but rolls on its own timer.")]
        [SerializeField] private DayNightCycle _dayNightCycle;

        [Header("Weather Timing")]
        [Tooltip("Chance (0-1) to roll for a new weather state each half-day (noon/midnight).")]
        [Range(0f, 1f)] [SerializeField] private float _weatherChangeChance = 0.35f;

        [Tooltip("If a weather change happens, the chance the new state will be Windy. The remainder splits between Clear and Rainy via RollRainWeather().")]
        [Range(0f, 1f)] [SerializeField] private float _windChance = 0.5f;

        [Tooltip("Fallback duration (in real seconds) between weather checks if DayNightCycle is not assigned.")]
        [SerializeField] private float _fallbackChangeInterval = 120f;

        [Header("Wind Intensity")]
        [Tooltip("Maximum concurrent gusts for each intensity level.")]
        [SerializeField] private int _lightGustCount = 3;

        [SerializeField] private int _moderateGustCount = 8;

        [SerializeField] private int _strongGustCount = 15;

        [Tooltip("When the weather rolls to Windy, this is the chance it becomes Strong vs Moderate (Light is the remainder).")]
        [Range(0f, 1f)] [SerializeField] private float _strongWindChance = 0.25f;

        [Header("Wind Direction")]
        [Tooltip("Direction the gusts blow across the screen. SetWindDirection() flips already-active gusts so the change is immediate.")]
        [SerializeField] private WindDirection _windDirection = WindDirection.Left;

        [Header("Wind Boost")]
        [Tooltip("How much to multiply gust count and speed while boosted via ToggleWindBoost().")]
        [SerializeField] private float _boostMultiplier = 2f;

        [Header("Wind Visuals")]
        [Tooltip("If true, gust sprites also appear during a Storm (Rainy + Strong intensity) so storms look as dramatic as a windy day. Disable if you want storms to feel clean and wind-free (rain only).")]
        [SerializeField] private bool _showWindDuringStorm = true;
        [Tooltip("Drag the wind animation frames here (e.g., 16-frame cycle).")]
        [SerializeField] private Sprite[] _windFrames;

        [Tooltip("Frames per second for the wind animation.")]
        [SerializeField] private float _windFramerate = 12f;

        [Tooltip("Tint/color multiplier for each gust. Lower alpha makes it more subtle.")]
        [SerializeField] private Color _windTint = new Color(1f, 1f, 1f, 0.35f);

        [Tooltip("Scale applied to each gust sprite. X is multiplied by -1 when WindDirection = Right so the sprite flips.")]
        [SerializeField] private float _windScale = 1f;

        [Tooltip("Minimum speed gusts drift across the screen (world units per second).")]
        [SerializeField] private float _gustSpeedMin = 3f;

        [Tooltip("Maximum speed gusts drift across the screen (world units per second).")]
        [SerializeField] private float _gustSpeedMax = 8f;

        [Tooltip("Seconds between spawn attempts when below the target gust count.")]
        [SerializeField] private float _spawnInterval = 0.4f;

        [Tooltip("Horizontal padding beyond the screen edge where gusts spawn/despawn.")]
        [SerializeField] private float _gustSpawnPadding = 1f;

        [Header("Lightning")]
        [Tooltip("If true, lightning strikes can fire while a storm is active (Rainy + Strong wind). Disable for calmer worlds where rain and thunder never coincide.")]
        [SerializeField] private bool _canLightning = true;

        [Tooltip("Minimum seconds between consecutive lightning strikes during a storm.")]
        [SerializeField] private float _lightningMinInterval = 8f;

        [Tooltip("Maximum seconds between consecutive lightning strikes during a storm.")]
        [SerializeField] private float _lightningMaxInterval = 25f;

        [Tooltip("Time, in seconds, until the first lightning can strike after rain starts. Slightly higher than the loop interval so players see the storm escalate.")]
        [SerializeField] private float _lightningFirstStrikeDelay = 6f;

        [Header("Rain Visuals (Auto-Spawn)")]
        [Tooltip("Variety sheet for falling raindrops. Each drop picks one at random on activate so the rainfield isn't uniform. The RainRenderer component is auto-created at Start, then Configure() is called with this array.")]
        [SerializeField] private Sprite[] _rainDropFrames;

        [Tooltip("Sprite for the small splash burst when a raindrop crosses the ground row.")]
        [SerializeField] private Sprite _rainSplashSprite;

        [Header("Weather Audio")]
        [Tooltip("Master volume of the weather ambience loops (rain + wind) at peak intensity. Default 0.22 (22%) so ambient sound sits gently in the background. WeatherCycle pushes this value into RainAudio.")]
        [Range(0f, 1f)] [SerializeField] private float _maxAmbienceVolume = 0.22f;

        [Tooltip("Looping outdoor rain ambience clips. (Auto-discovered from Elements/WE Light Outside Rain).")]
        [SerializeField] private AudioClip[] _rainAmbienceLoops;

        [Tooltip("Single looping indoor rain ambience clip. Cross-faded in when SetIndoors(true) is called.")]
        [SerializeField] private AudioClip _indoorAmbienceClip;

        [Tooltip("One or more thunder SFX clips. Randomly picked per strike so consecutive strikes don't sound identical.")]
        [SerializeField] private AudioClip[] _thunderClips;

        [Tooltip("Looping heavy storm ambience clips with thunder rumble. (Auto-discovered from Elements/WE Heavy Outside Rain).")]
        [SerializeField] private AudioClip[] _stormAmbienceLoops;

        [Tooltip("Looping light wind whistle clips for windy weather.")]
        [SerializeField] private AudioClip[] _windAmbienceLoops;

        [Tooltip("Looping heavy wind whistle clips for strong wind and storms.")]
        [SerializeField] private AudioClip[] _heavyWindAmbienceLoops;

        [Header("Weather Transitions")]
        [Tooltip("Time in seconds for rain and wind intensity to smoothly fade between weather states.")]
        [SerializeField] private float _weatherTransitionDuration = 5f;

        private WeatherType _currentWeather = WeatherType.Clear;
        private WindIntensity _currentIntensity = WindIntensity.Light;
        private float _currentRainIntensityLerped = 0f;
        private float _lastTime01;
        private float _fallbackTimer;
        private float _spawnTimer;
        private float _lightningTimer;
        private bool _isWindBoosted;

        private Camera _mainCamera;
        private GameObject _gustContainer;
        private WindGust[] _gustPool;

        public WeatherType CurrentWeather => _currentWeather;

        public WindIntensity CurrentIntensity => _currentIntensity;

        public WindDirection CurrentWindDirection => _windDirection;

        public bool IsPaused { get; set; } = false;

        /// <summary>
        /// Target 0..1 rain intensity based on current state settings.
        /// </summary>
        public float TargetRainIntensity
        {
            get
            {
                if (_currentWeather != WeatherType.Rainy) return 0f;
                switch (_currentIntensity)
                {
                    case WindIntensity.Light: return 0.33f;
                    case WindIntensity.Moderate: return 0.66f;
                    case WindIntensity.Strong: return 1.0f;
                    default: return 0f;
                }
            }
        }

        /// <summary>
        /// Returns the smoothly interpolated 0..1 rain intensity.
        /// Used by RainRenderer / RainAudio / RainSplash to scale drops and audio smoothly.
        /// </summary>
        public float RainIntensity => _currentRainIntensityLerped;

        /// <summary>
        /// Fires whenever the rain intensity crosses a meaningful threshold
        /// (starting a storm, ending rain, or jumping from Light -> Strong).
        /// Subscribers should re-query RainIntensity inside the handler rather
        /// than caching the value.
        /// </summary>
        public event System.Action<float> OnIntensityChanged;

        /// <summary>
        /// Fires when a lightning strike rolls during a Rainy+Strong storm.
        /// Sourced by TickLightning on a randomised interval. DayNightCycle
        /// listens for the visual flash; RainAudio listens for the delayed
        /// thunder. Apps that don't subscribe simply won't react.
        /// </summary>
        public event System.Action OnLightningStrike;

        private void Start()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            if (_dayNightCycle != null)
            {
                _lastTime01 = _dayNightCycle.Time01;
            }

            // storm doesn't immediately hammer the player with thunder.
            _lightningTimer = _lightningFirstStrikeDelay;

            EnsureOverlay();
            EnsureRainSystem();
        }

        private void Update()
        {
            if (!IsPaused)
            {
                CheckForWeatherTransition();
            }
            UpdateIntensityTransition();
            UpdateWindVisuals();
            TickLightning();
        }

        private void UpdateIntensityTransition()
        {
            float target = TargetRainIntensity;
            if (Mathf.Abs(_currentRainIntensityLerped - target) > 0.001f)
            {
                float rate = 1f / Mathf.Max(0.1f, _weatherTransitionDuration);
                _currentRainIntensityLerped = Mathf.MoveTowards(_currentRainIntensityLerped, target, rate * Time.deltaTime);
                EmitIntensityChanged();
            }
            else
            {
                _currentRainIntensityLerped = target;
            }
        }

        private void OnDestroy()
        {
            CleanupGusts();
        }

        // Inspector OR when the script reloads. Without this hook, dragging the
        // SerializeField but RainAudio would never hear about it, so the user
        // Play-time validation so the slider feels live.
        private void OnValidate()
        {
            if (!Application.isPlaying) return;       // skip during edit-mode deserialisation
            if (Instance != this) return;             // don't push from a duplicate / pending swap
            if (RainAudio.Instance == null) return;   // RainAudio not yet auto-spawned; EnsureRainSystem will push on Start
            RainAudio.Instance.SetAmbienceVolume(_maxAmbienceVolume);
        }

        /// <summary>
        /// Restore weather + wind intensity + wind direction from a save.
        /// Resetting intensity for non-windy weather is intentional —
        /// non-windy weather simply doesn't show wind.
        /// </summary>
        public void RestoreWeather(WeatherType weather, WindIntensity intensity, WindDirection direction)
        {
            _currentWeather = weather;
            _currentIntensity = weather == WeatherType.Windy ? intensity : WindIntensity.Light;
            _windDirection = direction;
            _fallbackTimer = Time.time + _fallbackChangeInterval;
            EmitIntensityChanged();
        }

        /// <summary>
        /// Public helper to force a specific weather state immediately.
        /// If no intensity is provided, it randomizes one for Windy.
        /// </summary>
        public void SetWeather(WeatherType weather)
        {
            _currentWeather = weather;
            _currentIntensity = (_currentWeather == WeatherType.Windy)
                ? RollIntensity()
                : WindIntensity.Light;
            EmitIntensityChanged();
        }

        /// <summary>
        /// Public helper to force a specific weather state and intensity immediately.
        /// </summary>
        public void SetWeather(WeatherType weather, WindIntensity intensity)
        {
            _currentWeather = weather;
            _currentIntensity = (weather == WeatherType.Windy) ? intensity : WindIntensity.Light;
            EmitIntensityChanged();
        }

        /// <summary>
        /// Public helper to change which way the wind blows.
        /// Already-active gusts are flipped so the change is immediate.
        /// </summary>
        public void SetWindDirection(WindDirection direction)
        {
            _windDirection = direction;
            if (_gustPool == null) return;
            for (int i = 0; i < _gustPool.Length; i++)
            {
                ApplyGustFlip(_gustPool[i]);
            }
        }

        /// <summary>
        /// Toggles the wind boost on/off. When on, more gusts spawn and they move faster.
        /// If the weather is not windy, boosting also forces windy weather.
        /// </summary>
        public void ToggleWindBoost()
        {
            _isWindBoosted = !_isWindBoosted;

            if (_isWindBoosted && _currentWeather != WeatherType.Windy)
            {
                SetWeather(WeatherType.Windy, WindIntensity.Moderate);
            }

#if UNITY_EDITOR
            string status = _isWindBoosted ? "ON" : "OFF";
            Debug.Log($"[WeatherCycle] Wind boost {status}");
#endif
        }

        /// <summary>
        /// Toggles inside/outside context. Delegates to RainAudio.Instance which
        /// cross-fades between the outdoor loop array and the indoor clip. Safe
        /// to call when RainAudio hasn't booted yet — the audio source reads the
        /// flag on its next volume tick.
        /// </summary>
        public void SetIndoors(bool indoors)
        {
            if (RainAudio.Instance != null)
            {
                RainAudio.Instance.SetIndoors(indoors);
            }
#if UNITY_EDITOR
            string label = indoors ? "indoors" : "outdoors";
            Debug.Log($"[WeatherCycle] Indoor state: {label}");
#endif
        }

        // every mutation site so listeners always get a fresh sample.
        private void EmitIntensityChanged()
        {
            OnIntensityChanged?.Invoke(RainIntensity);
        }

        /// <summary>
        /// Checks whether enough time has passed to roll for a new weather state.
        /// When DayNightCycle is present, changes happen at noon and midnight.
        /// Otherwise, a simple timer is used.
        /// </summary>
        private void CheckForWeatherTransition()
        {
            if (_dayNightCycle != null)
            {
                float time01 = _dayNightCycle.Time01;
                bool crossedNoon = _lastTime01 < 0.5f && time01 >= 0.5f;
                bool crossedMidnight = time01 < _lastTime01;
                if (crossedNoon || crossedMidnight)
                {
                    RollWeather();
                }
                _lastTime01 = time01;
            }
            else
            {
                _fallbackTimer += Time.deltaTime;
                if (_fallbackTimer >= _fallbackChangeInterval)
                {
                    _fallbackTimer = 0f;
                    RollWeather();
                }
            }
        }

        /// <summary>
        /// Rolls for a new weather state based on the configured chances.
        /// </summary>
        private void RollWeather()
        {
            if (Random.value >= _weatherChangeChance)
            {
#if UNITY_EDITOR
                Debug.Log($"[WeatherCycle] Weather stayed {_currentWeather}");
#endif
                return;
            }

            _currentWeather = (Random.value < _windChance) ? WeatherType.Windy : RollRainWeather();
            if (_currentWeather == WeatherType.Windy)
            {
                _currentIntensity = RollIntensity();
            }
            else if (_currentWeather == WeatherType.Rainy)
            {
                // never get there within the player's session.
                _currentIntensity = WindIntensity.Strong;
            }
#if UNITY_EDITOR
            Debug.Log($"[WeatherCycle] Weather changed to: {_currentWeather} ({_currentIntensity})");
#endif
            EmitIntensityChanged();
        }

        /// <summary>
        /// When the wind-chance roll loses, decide whether the alternative
        /// weather is a Rainy storm instead of plain Clear. Returns WeatherType
        /// (no side effects). Kept separate so it can be tuned in isolation.
        /// </summary>
        private WeatherType RollRainWeather()
        {
            return (Random.value < 0.30f) ? WeatherType.Rainy : WeatherType.Clear;
        }

        private WindIntensity RollIntensity()
        {
            float roll = Random.value;
            if (roll < _strongWindChance) return WindIntensity.Strong;
            if (roll < _strongWindChance + (1f - _strongWindChance) * 0.5f) return WindIntensity.Moderate;
            return WindIntensity.Light;
        }

        /// <summary>
        /// Counts down until the next lightning strike while a storm is active.
        /// Only runs when RainIntensity == 1 (rain + Strong wind). When the
        /// timer hits zero a strike is rolled and the timer is reset to a new
        /// randomised interval. Designers can disable strikes entirely by
        /// toggling _canLightning.
        /// </summary>
        private void TickLightning()
        {
            if (!_canLightning) return;
            if (RainIntensity < 1f) return;

            _lightningTimer -= Time.deltaTime;
            if (_lightningTimer > 0f) return;

            // immediately re-fire.
            _lightningTimer = Random.Range(_lightningMinInterval, _lightningMaxInterval);
            OnLightningStrike?.Invoke();
        }

        /// <summary>
        /// Runtime state of a single wind gust sprite.
        /// </summary>
        private class WindGust
        {
            public GameObject gameObject;
            public Transform transform;
            public SpriteRenderer renderer;
            public float speed;
            public float animTimer;
            public int currentFrame;
            public bool isActive;
        }

        /// <summary>
        /// True when wind visuals (gust sprites) should be active: Windy weather
        /// at any intensity, OR (when <c>_showWindDuringStorm</c> is enabled) a
        /// Storm (Rainy + Strong intensity) so storms look as dramatic as a windy
        /// day. Single source of truth used by both <see cref="UpdateWindVisuals"/>
        /// and <see cref="GetTargetGustCount"/>.
        /// </summary>
        private bool IsWindActive()
        {
            return _currentWeather == WeatherType.Windy
                || (_showWindDuringStorm && RainIntensity >= 1f);
        }

        /// <summary>
        /// Spawns and drifts wind gusts based on the current weather/intensity.
        /// </summary>
        private void UpdateWindVisuals()
        {
            if (_gustPool == null || _windFrames == null || _windFrames.Length == 0) return;

            int targetCount = GetTargetGustCount();
            int activeCount = CountActiveGusts();

            // When neither Windy nor Storm, hide all gusts immediately.
            if (!IsWindActive())
            {
                for (int i = 0; i < _gustPool.Length; i++)
                {
                    if (_gustPool[i].isActive)
                    {
                        DeactivateGust(_gustPool[i]);
                    }
                }
                return;
            }

            _spawnTimer += Time.deltaTime;
            if (activeCount < targetCount && _spawnTimer >= _spawnInterval)
            {
                _spawnTimer = 0f;
                WindGust gust = FindInactiveGust();
                if (gust != null)
                {
                    ActivateGust(gust);
                }
            }

            float frameDuration = 1f / Mathf.Max(_windFramerate, 0.01f);
            for (int i = 0; i < _gustPool.Length; i++)
            {
                WindGust gust = _gustPool[i];
                if (!gust.isActive) continue;

                Vector3 pos = gust.transform.localPosition;
                float driftDir = (_windDirection == WindDirection.Left) ? 1f : -1f;
                pos.x += gust.speed * Time.deltaTime * driftDir;
                gust.transform.localPosition = pos;

                gust.animTimer += Time.deltaTime;
                if (gust.animTimer >= frameDuration)
                {
                    gust.animTimer -= frameDuration;
                    gust.currentFrame++;
                    if (gust.currentFrame >= _windFrames.Length)
                    {
                        gust.currentFrame = 0;
                    }
                    gust.renderer.sprite = _windFrames[gust.currentFrame];
                }

                float halfWidth = GetCameraHalfWidth() + _gustSpawnPadding;
                bool offScreen = (_windDirection == WindDirection.Left)
                    ? pos.x > halfWidth
                    : pos.x < -halfWidth;
                if (offScreen)
                {
                    DeactivateGust(gust);
                }
            }
        }

        private int GetTargetGustCount()
        {
            // Wind visuals only spawn when IsWindActive() is true; during a clear
            // immediately deactivate.
            if (!IsWindActive()) return 0;

            int baseCount = _currentIntensity switch
            {
                WindIntensity.Light => _lightGustCount,
                WindIntensity.Moderate => _moderateGustCount,
                WindIntensity.Strong => _strongGustCount,
                _ => _lightGustCount,
            };
            return _isWindBoosted ? Mathf.RoundToInt(baseCount * _boostMultiplier) : baseCount;
        }

        private int CountActiveGusts()
        {
            int count = 0;
            for (int i = 0; i < _gustPool.Length; i++)
            {
                if (_gustPool[i].isActive) count++;
            }
            return count;
        }

        private WindGust FindInactiveGust()
        {
            for (int i = 0; i < _gustPool.Length; i++)
            {
                if (!_gustPool[i].isActive) return _gustPool[i];
            }
            return null;
        }

        /// <summary>
        /// Places a gust just off the screen edge at a random height.
        /// </summary>
        private void ActivateGust(WindGust gust)
        {
            if (_mainCamera == null || _windFrames == null || _windFrames.Length == 0) return;

            float halfWidth = GetCameraHalfWidth() + _gustSpawnPadding;
            float halfHeight = _mainCamera.orthographicSize;

            gust.isActive = true;
            gust.speed = Random.Range(_gustSpeedMin, _gustSpeedMax) * (_isWindBoosted ? _boostMultiplier : 1f);
            gust.animTimer = 0f;
            gust.currentFrame = 0;

            bool comingFromLeft = (_windDirection == WindDirection.Left);
            Vector3 localPos = gust.transform.localPosition;
            localPos.x = comingFromLeft ? -halfWidth : halfWidth;

            // Distribute gusts across the full screen height.
            localPos.y = Random.Range(-halfHeight, halfHeight);
            localPos.z = _mainCamera.nearClipPlane + 0.02f;
            gust.transform.localPosition = localPos;

            gust.renderer.sprite = _windFrames[0];
            gust.renderer.enabled = true;

            ApplyGustFlip(gust);
        }

        private void DeactivateGust(WindGust gust)
        {
            gust.isActive = false;
            gust.renderer.enabled = false;
        }

        private void ApplyGustFlip(WindGust gust)
        {
            if (gust == null || gust.transform == null) return;
            gust.transform.localScale = new Vector3(GetGustScaleX(), _windScale, _windScale);
        }

        private float GetGustScaleX()
        {
            return (_windDirection == WindDirection.Left) ? _windScale : -_windScale;
        }

        /// <summary>
        /// Creates the gust pool parented to the main camera.
        /// </summary>
        private void EnsureOverlay()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogWarning("[WeatherCycle] No Camera.main found. Wind overlay will not render.", this);
                return;
            }

            int maxGusts = Mathf.Max(_lightGustCount, _moderateGustCount, _strongGustCount);
            maxGusts = Mathf.RoundToInt(maxGusts * _boostMultiplier);
            _gustPool = new WindGust[maxGusts];

            _gustContainer = new GameObject("WeatherWindGusts");
            _gustContainer.transform.SetParent(_mainCamera.transform, false);

            for (int i = 0; i < maxGusts; i++)
            {
                GameObject gustGo = new GameObject($"WindGust_{i}");
                gustGo.transform.SetParent(_gustContainer.transform, false);

                SpriteRenderer sr = gustGo.AddComponent<SpriteRenderer>();
                sr.sprite = GetFirstValidWindSprite();
                sr.color = _windTint;
                sr.sortingLayerName = "Default";
                sr.sortingOrder = int.MinValue + 1;
                sr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                sr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
                sr.enabled = false;

                gustGo.transform.localScale = new Vector3(GetGustScaleX(), _windScale, _windScale);

                _gustPool[i] = new WindGust
                {
                    gameObject = gustGo,
                    transform = gustGo.transform,
                    renderer = sr,
                    isActive = false,
                };
            }
        }

        private void CleanupGusts()
        {
            if (_gustPool != null)
            {
                for (int i = 0; i < _gustPool.Length; i++)
                {
                    if (_gustPool[i] != null && _gustPool[i].gameObject != null)
                    {
                        if (Application.isPlaying)
                        {
                            Destroy(_gustPool[i].gameObject);
                        }
                        else
                        {
                            DestroyImmediate(_gustPool[i].gameObject);
                        }
                    }
                }
                _gustPool = null;
            }

            if (_gustContainer != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_gustContainer);
                }
                else
                {
                    DestroyImmediate(_gustContainer);
                }
                _gustContainer = null;
            }
        }

        private float GetCameraHalfWidth()
        {
            if (_mainCamera == null) return 0f;
            return _mainCamera.orthographicSize * _mainCamera.aspect;
        }

        /// <summary>
        /// Returns the first non-null wind sprite, or null if none are assigned.
        /// </summary>
        private Sprite GetFirstValidWindSprite()
        {
            if (_windFrames == null || _windFrames.Length == 0) return null;
            foreach (var frame in _windFrames)
            {
                if (frame != null) return frame;
            }
            return null;
        }

        /// <summary>
        /// Ensures RainRenderer, RainSplash and RainAudio MonoBehaviours exist in
        /// the scene at startup. If any are missing, they are created on a child
        /// GameObject parented to <c>this.transform</c>. When a designer has
        /// already placed any component in the scene, the auto-spawn skips that
        /// slot (so the manual wiring wins Instance) but the asset-config call
        /// still runs so WeatherCycle's [SerializeField] references reach the
        /// existing component too.
        /// </summary>
        private void EnsureRainSystem()
        {
            // adding a duplicate would nuke the host and any co-existing sibling.
            var existingRenderer = Object.FindAnyObjectByType<RainRenderer>();
            var existingSplash = Object.FindAnyObjectByType<RainSplash>();
            var existingAudio = Object.FindAnyObjectByType<RainAudio>();

            // three rain components are already in the scene under different GOs,
            // pick the renderer as host and parent it under WeatherCycle.
            GameObject host = (existingRenderer != null) ? existingRenderer.gameObject
                            : (existingSplash != null) ? existingSplash.gameObject
                            : (existingAudio != null) ? existingAudio.gameObject
                            : null;

            if (host == null)
            {
                host = new GameObject("RainSystem");
                host.transform.SetParent(transform, false);
            }
            else if (host.transform.parent == null)
            {
                host.transform.SetParent(transform, false);
            }

            if (existingRenderer == null) host.AddComponent<RainRenderer>();
            if (existingSplash == null) host.AddComponent<RainSplash>();
            if (existingAudio == null) host.AddComponent<RainAudio>();

            // references on WeatherCycle win over empty per-component slots.
            var renderer = host.GetComponent<RainRenderer>();
            var splash = host.GetComponent<RainSplash>();
            var audio = host.GetComponent<RainAudio>();
            if (renderer != null) renderer.Configure(_rainDropFrames);
            if (splash != null) splash.Configure(_rainSplashSprite);
            if (audio != null) audio.Configure(_rainAmbienceLoops, _indoorAmbienceClip, _thunderClips, _stormAmbienceLoops, _windAmbienceLoops, _heavyWindAmbienceLoops);

            // stale default RainAudio might still be holding from Awake.
            if (audio != null) audio.SetAmbienceVolume(_maxAmbienceVolume);

#if UNITY_EDITOR
            int dropFrames = (_rainDropFrames != null) ? _rainDropFrames.Length : 0;
            int outdoorLoops = (_rainAmbienceLoops != null) ? _rainAmbienceLoops.Length : 0;
            int indoorClips = (_indoorAmbienceClip != null) ? 1 : 0;
            int thunderClips = (_thunderClips != null) ? _thunderClips.Length : 0;
            int splashHave = (_rainSplashSprite != null) ? 1 : 0;
            bool rendererOk = (renderer != null);
            bool splashOk = (splash != null);
            bool audioOk = (audio != null);
            Debug.Log("[WeatherCycle] Rain system auto-spawn complete: " +
                      $"renderer={BoolLabel(rendererOk)}, splash={BoolLabel(splashOk)}, audio={BoolLabel(audioOk)}; " +
                      $"assets: drop={dropFrames} frames, splash={BoolLabel(splashHave == 1)}, " +
                      $"ambience={outdoorLoops} outdoor + {indoorClips} indoor, " +
                      $"thunder={thunderClips} clips");
#endif
        }

#if UNITY_EDITOR
        private static string BoolLabel(bool value) => value ? "yes" : "no";
#endif
    }
}
