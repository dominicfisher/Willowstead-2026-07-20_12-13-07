using UnityEngine;

namespace Willowstead.World
{
    /// <summary>
    /// Renders falling raindrops via a camera-relative sprite pool. The number
    /// of concurrent drops scales linearly with <c>WeatherCycle.RainIntensity</c>
    /// (0 = none, 1 = storm). Drops fall diagonally (sprite rotation + per-drop
    /// velocity drift) and recycle to the top of the camera frustum when they
    /// reach the ground or fly off-screen.
    /// </summary>
    public class RainRenderer : MonoBehaviour
    {
        public static RainRenderer Instance { get; private set; }

        /// <summary>
        /// Fires once per drop when its position crosses the ground row along
        /// the bottom of the camera frustum. World-space hit pos is delivered.
        /// Subscribers (e.g. RainSplash) treat this as a visual-only signal;
        /// grid-cell mechanics are throttled separately so we don't hammer the
        /// GridManager hundreds of times per second.
        /// </summary>
        public event System.Action<Vector3> OnDropHitGround;

        [Header("Drops")]
        [Tooltip("Variety sheet of raindrop sprites. Each drop picks one at random on activate so the rainfield isn't uniform. Empty array → magenta squares (intentional so missing asset is obvious).")]
        [SerializeField] private Sprite[] _raindropFrames;
        [Tooltip("Maximum concurrent drops at intensity = 1.0. The runtime count is intensity * this.")]
        [SerializeField] private int _maxDrops = 256;
        [Tooltip("Tint for the drops. Lower alpha = more atmospheric / less opaque.")]
        [SerializeField] private Color _dropTint = new Color(0.85f, 0.92f, 1f, 0.55f);
        [Tooltip("Per-drop uniform size multiplier so the variety sheet reads as a coherent rainfall instead of mixed scales. 1 = sprite.bounds preserved.")]
        [Range(0.25f, 3f)] [SerializeField] private float _dropSizeMultiplier = 1f;

        [Header("Movement")]
        [Tooltip("Base fall speed in world units / sec. Storm intensity scales this by up to +50% to feel angrier.")]
        [SerializeField] private float _fallSpeed = 12f;
        [Tooltip("Per-drop random multiplier on the base fall speed, applied per flight cycle. Higher = more staggered timing instead of all drops reaching ground at once. Range 0..1 = ±50% jitter; 0 = uniform fall speed (the bug).")]
        [Range(0f, 1f)] [SerializeField] private float _dropFallSpeedJitter = 0.5f;
        [Tooltip("Base horizontal drift in world units / sec applied globally. With the default 0, drops are symmetrically scattered around vertical (some lean left, some right, some near-straight). Raise to introduce a prevailing wind: 0 = pure scatter, 1 = light steady wind, 4 = storm-bias right.")]
        [SerializeField] private float _windDrift = 0f;
        [Tooltip("Maximum random horizontal drift added per drop (world units / sec). Drop drift = _windDrift + Random.Range(-this, +this). 0 = uniform drift (drops march in lockstep); 2.5 = scattered diagonal field; 5 = wide scatter.")] 
        [SerializeField] private float _dropWindDriftJitter = 2.5f;
        [Tooltip("Base sprite Z-rotation in degrees. Purely visual — does NOT drive velocity (that's _dropMotionAngleDegrees). Default 0 keeps the sprite upright (its authored drop shape); tilt to add visual flair. Per-drop offset of ±_dropTiltJitterDegrees adds variety on top.")]
        [SerializeField] private float _dropTiltDegrees = 0f;
        [Tooltip("Maximum random rotation added per drop (degrees). Drop tilt = _dropTiltDegrees + Random.Range(-this, +this). 0 = uniform rotation.")] 
        [SerializeField] private float _dropTiltJitterDegrees = 25f;

        [Tooltip("Drop fall direction angle in degrees, decomposed via sin/cos into velocity (sin = horizontal component, -cos = vertical). Negative = drops drift leftward as they fall (top-right -> bottom-left). Positive = rightward drift. INDEPENDENT from _dropTiltDegrees — controls motion only, sprite remains independently rotated. Default -22 matches the original top-right -> bottom-left request.")]
        [SerializeField] private float _dropMotionAngleDegrees = -22f;

        [Header("Spawn Frustum")]
        [Tooltip("Extra horizontal padding beyond the camera edges where drops are spawned/despawned.")]
        [SerializeField] private float _spawnPaddingX = 2f;
        [Tooltip("Vertical padding above the camera top where drops spawn off-screen and below the camera bottom where they recycle.")]
        [SerializeField] private float _spawnPaddingY = 2.5f;

        // Pool entry. Cached so we don't AddComponent each spawn / recycle.
        private class Drop
        {
            public GameObject gameObject;
            public Transform transform;
            public SpriteRenderer renderer;
            public Vector3 position;   // local-to-camera, kept on a delegate so UpdateDrops doesn't poke at transform for read
            public Vector3 velocity;
            public bool isActive;
            public float targetY;      // camera-local Y where this drop will splash; randomised per flight cycle
        }
        private Drop[] _dropPool;
        private Camera _mainCamera;
        private GameObject _container;
        private float _currentIntensity;

        public float CurrentIntensity => _currentIntensity;
        public int ActiveDropCount
        {
            get
            {
                if (_dropPool == null) return 0;
                int n = 0;
                for (int i = 0; i < _dropPool.Length; i++) if (_dropPool[i].isActive) n++;
                return n;
            }
        }

        // ─── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogWarning("[RainRenderer] No Camera.main found. Rain will not render.", this);
                return;
            }

            EnsureContainer();
            BuildPool();
            SubscribeToWeather();
        }

        private void OnEnable()
        {
            // If Start() already ran we have a subscription; if not, SubscribeToWeather
            // will fire once Start completes. This branch covers component-disable/enable
            // where Subscription was lost.
            if (_dropPool != null) SubscribeToWeather();
        }

        private void OnDisable()
        {
            UnsubscribeFromWeather();
        }

        private void OnDestroy()
        {
            UnsubscribeFromWeather();
            if (Instance == this) Instance = null;
        }

        private void SubscribeToWeather()
        {
            if (WeatherCycle.Instance == null) return;
            WeatherCycle.Instance.OnIntensityChanged -= HandleIntensityChanged;
            WeatherCycle.Instance.OnIntensityChanged += HandleIntensityChanged;
            HandleIntensityChanged(WeatherCycle.Instance.RainIntensity);
        }

        private void UnsubscribeFromWeather()
        {
            if (WeatherCycle.Instance == null) return;
            WeatherCycle.Instance.OnIntensityChanged -= HandleIntensityChanged;
        }

        private void HandleIntensityChanged(float intensity)
        {
            _currentIntensity = Mathf.Clamp01(intensity);
        }

        private void EnsureContainer()
        {
            // Container is camera-parented so drops inherit camera movement; setting
            // parent resets local transform to identity, which is where we want drops.
            _container = new GameObject("RainDropsContainer");
            _container.transform.SetParent(_mainCamera.transform, false);
        }

        private void BuildPool()
        {
            _dropPool = new Drop[_maxDrops];
            for (int i = 0; i < _maxDrops; i++)
            {
                GameObject dropGo = new GameObject($"RainDrop_{i}");
                dropGo.transform.SetParent(_container.transform, false);

                SpriteRenderer sr = dropGo.AddComponent<SpriteRenderer>();
                sr.sprite = PickRaindropSprite();
                sr.color = _dropTint;
                sr.sortingLayerName = "Default";
                // Drops draw above dirt/grass/puddles (sortOrder -32000 to -31850) but
                // below decorative GameObjects (player at ~0, trees/objects close to 0).
                sr.sortingOrder = -31000;
                sr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                sr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
                sr.enabled = false;

                dropGo.transform.localRotation = Quaternion.Euler(0f, 0f, _dropTiltDegrees);
                ApplyDropScale(dropGo.transform, sr.sprite);

                _dropPool[i] = new Drop
                {
                    gameObject = dropGo,
                    transform = dropGo.transform,
                    renderer = sr,
                    isActive = false
                };
            }
        }

        /// <summary>
        /// Returns a random non-null sprite from <c>_raindropFrames</c>, or null if
        /// the array is empty / all entries are null. Null intentionally surfaces as
        /// a magenta square on the missing-asset drop, which is the freshest possible
        /// debugging signal during iteration.
        /// </summary>
        private Sprite PickRaindropSprite()
        {
            if (_raindropFrames == null || _raindropFrames.Length == 0) return null;
            // Shuffle-style retry: pick a non-null frame up to 4 times before giving up.
            for (int tries = 0; tries < 4; tries++)
            {
                int idx = Random.Range(0, _raindropFrames.Length);
                Sprite s = _raindropFrames[idx];
                if (s != null) return s;
            }
            // Fallback pass: linear scan for the first non-null.
            for (int i = 0; i < _raindropFrames.Length; i++)
            {
                if (_raindropFrames[i] != null) return _raindropFrames[i];
            }
            return null;
        }

        private void ApplyDropScale(Transform dropTransform, Sprite sprite)
        {
            float s = _dropSizeMultiplier;
            if (sprite != null && sprite.pixelsPerUnit > 0f)
            {
                // The sprite sheet was authored at 16 PPU (per the .meta). To make
                // a sprite authored at native pixel size render at its natural world
                // size, we apply a per-sprite compensation factor so the swimmy
                // 43x43 sheet entries and the tiny 6x6 splats don't read at the
                // same on-screen size.
                float ppuComp = 16f / sprite.pixelsPerUnit;
                s *= ppuComp;
            }
            dropTransform.localScale = new Vector3(s, s, 1f);
        }

        // ─── Per-frame logic ───────────────────────────────────────────

        private void Update()
        {
            if (_mainCamera == null || _dropPool == null) return;

            int target = Mathf.RoundToInt(_currentIntensity * _dropPool.Length);
            target = Mathf.Clamp(target, 0, _dropPool.Length);

            // Lower counts deactivate drops from the END of the pool (preserves
            // oldest-active entries, looks less "popping"). Higher counts activate
            // fresh entries from the FIRST inactive slot.
            for (int i = 0; i < _dropPool.Length; i++)
            {
                if (i < target)
                {
                    if (!_dropPool[i].isActive) ActivateDrop(_dropPool[i]);
                }
                else
                {
                    if (_dropPool[i].isActive)
                    {
                        _dropPool[i].isActive = false;
                        _dropPool[i].renderer.enabled = false;
                    }
                }
            }

            UpdateDrops();
        }

        private void ActivateDrop(Drop drop)
        {
            drop.isActive = true;
            drop.renderer.enabled = true;
            // Initialise everything about this drop's flight cycle. Same routine is
            // called on recycle, so recycled drops get a fresh sprite + fresh velocity
            // + fresh rotation — they're never visually identical to their last cycle.
            InitializeFlight(drop);
        }

        /// <summary>
        /// Picks a fresh sprite, scale, rotation, lateral drift, fall speed, and
        /// top spawn position for a single drop's flight cycle. Called both when a
        /// drop is first activated and when it's recycled at the bottom / side /
        /// top. Randomising every cycle is what makes the rainfield feel organic
        /// instead of a synchronised march.
        /// </summary>
        private void InitializeFlight(Drop drop)
        {
            // 1. Sprite + scale: each drop picks from the variety sheet.
            Sprite picked = PickRaindropSprite();
            if (picked != null) drop.renderer.sprite = picked;
            ApplyDropScale(drop.transform, picked);

            // 2. Rotation: per-drop tilt with random jitter so streaks don't all lean the same direction.
            float tilt = _dropTiltDegrees + Random.Range(-_dropTiltJitterDegrees, _dropTiltJitterDegrees);
            drop.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

            // 3. Velocity from MOTION angle (independent from sprite tilt, which
            //    is visual only). Splits sprite visual flair from physics — sprite
            //    can lean one way for visual style while motion drives a different
            //    angle (e.g. gentle -10° sprite tilt with strong -45° motion).
            //    Motion angle is uniform across drops so the rainfield reads as
            //    a coherent diagonal weather pattern rather than random scatter.
            //    `_windDrift` adds an additive horizontal pull on top.
            float fallMult = 1f + _currentIntensity * 0.5f;
            float fallJitter = 1f + Random.Range(-_dropFallSpeedJitter, _dropFallSpeedJitter);
            float driftJitter = Random.Range(-_dropWindDriftJitter, _dropWindDriftJitter);
            float motionRad = _dropMotionAngleDegrees * Mathf.Deg2Rad;
            float speed = _fallSpeed * fallMult * fallJitter;
            drop.velocity = new Vector3(
                Mathf.Sin(motionRad) * speed + _windDrift + driftJitter,
                -Mathf.Cos(motionRad) * speed,
                0f);

            // 4. Spawn position at top of frustum with random X to scatter horizontally.
            float aspect = _mainCamera.aspect;
            float ortho = _mainCamera.orthographicSize;
            float halfWidth = ortho * aspect + _spawnPaddingX;
            float halfHeight = ortho;
            float topEdge = ortho + _spawnPaddingY;
            drop.position = new Vector3(
                Random.Range(-halfWidth, halfWidth),
                topEdge + Random.Range(0f, _spawnPaddingY),
                _mainCamera.nearClipPlane + 0.05f);
            drop.transform.localPosition = drop.position;

            // 5. Pick a per-drop splash target Y so drops rain down to varied points
            //    across the screen rather than all hitting the bottom edge. Bias
            //    toward grass cells via ProceduralGridGenerator.IsGrassAt so the
            //    splash visual reads as "hitting terrain" (the player sees splashes
            //    clustered on grass tiles rather than splashing on water/edge tiles).
            float minY = -halfHeight + 0.5f;
            float maxY = halfHeight - 0.5f;
            drop.targetY = PickSplashTargetY(drop.position.x, minY, maxY);
        }

        /// <summary>
        /// Returns a camera-local Y inside the visible frustum for splash placement.
        /// Biases toward grass cell positions so splashes look like they hit
        /// terrain rather than splashing uniformly everywhere. Falls back to a
        /// uniform random Y if no grass sample is found in 4 retries (rare;
        /// terrain is mostly grass).
        /// </summary>
        private float PickSplashTargetY(float dropXLocal, float minY, float maxY)
        {
            ProceduralGridGenerator gen = ProceduralGridGenerator.Instance;
            if (gen == null || _mainCamera == null) return Random.Range(minY, maxY);

            // World X for this drop's column (constant for the whole flight).
            Vector3 worldAtDropX = _mainCamera.transform.TransformPoint(new Vector3(dropXLocal, 0f, 0f));
            int worldX = Mathf.RoundToInt(worldAtDropX.x);

            // A few retries — terrain is mostly grass so usually one of these lands.
            for (int t = 0; t < 4; t++)
            {
                float testY = Random.Range(minY, maxY);
                Vector3 worldAtTestY = _mainCamera.transform.TransformPoint(new Vector3(0f, testY, 0f));
                if (gen.IsGrassAt(worldX, Mathf.RoundToInt(worldAtTestY.y)))
                {
                    return testY;
                }
            }
            // No grass in 4 tries → splash anywhere in visible range. Slightly worse
            // visual fidelity but functionally correct.
            return Random.Range(minY, maxY);
        }

        private void UpdateDrops()
        {
            float aspect = _mainCamera.aspect;
            float ortho = _mainCamera.orthographicSize;
            float halfHeight = ortho;
            float halfWidth = ortho * aspect + _spawnPaddingX;
            float topRecycleY = halfHeight + _spawnPaddingY + _spawnPaddingY;

            for (int i = 0; i < _dropPool.Length; i++)
            {
                Drop drop = _dropPool[i];
                if (!drop.isActive) continue;

                drop.position += drop.velocity * Time.deltaTime;
                drop.transform.localPosition = drop.position;

                // Splash fires when the drop crosses its per-flight targetY (set in
                // InitializeFlight → PickSplashTargetY). Replaces the old fixed-bottom-edge
                // groundY check so splashes scatter across the entire visible camera
                // frustum, making rain read as hitting terrain at varied altitudes.
                bool reachedSplash = drop.position.y <= drop.targetY;
                bool offHoriz = drop.position.x < -halfWidth || drop.position.x > halfWidth;
                bool offTop = drop.position.y > topRecycleY;

                if (reachedSplash)
                {
                    // Convert camera-local position back to world space for the event.
                    Vector3 worldHit = _mainCamera.transform.TransformPoint(drop.position);
                    OnDropHitGround?.Invoke(worldHit);
                    RecycleDrop(drop);
                    continue;
                }

                if (offHoriz || offTop)
                {
                    RecycleDrop(drop);
                }
            }
        }

        /// <summary>
        /// Re-initialise the drop for its next flight cycle. Sprites/violences get
        /// re-randomised so the recycled drop is not visually identical to its
        /// previous cycle.
        /// </summary>
        private void RecycleDrop(Drop drop)
        {
            InitializeFlight(drop);
        }

        /// <summary>
        /// Late-binding setter for the raindrop sprite variety sheet. Safe to call
        /// either before or after <see cref="Start"/>; live drops are re-skinned
        /// with a fresh random pick per drop so the variety effect takes hold
        /// immediately. Use this when a parent system (e.g.
        /// <c>WeatherCycle.EnsureRainSystem</c>) needs to wire assets via code
        /// instead of a static Inspector reference.
        /// </summary>
        public void Configure(Sprite[] dropFrames)
        {
            _raindropFrames = dropFrames;
            // Re-skin live drops with a fresh random frame each so the variety
            // is visible immediately rather than waiting for natural recycling.
            if (_dropPool != null && dropFrames != null && dropFrames.Length > 0)
            {
                for (int i = 0; i < _dropPool.Length; i++)
                {
                    if (_dropPool[i] != null && _dropPool[i].renderer != null && _dropPool[i].isActive)
                    {
                        Sprite picked = PickRaindropSprite();
                        if (picked != null)
                        {
                            _dropPool[i].renderer.sprite = picked;
                            ApplyDropScale(_dropPool[i].transform, picked);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Optional runtime override for the pool capacity. Cap is clamped to a
        /// reasonable max so a bad caller can't blow up memory.
        /// </summary>
        public void ConfigureDrops(int maxDrops)
        {
            _maxDrops = Mathf.Clamp(maxDrops, 0, 4096);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _maxDrops = Mathf.Max(0, _maxDrops);
            _fallSpeed = Mathf.Max(0.01f, _fallSpeed);
            _windDrift = Mathf.Clamp(_windDrift, -50f, 50f);
        }
#endif
    }
}
