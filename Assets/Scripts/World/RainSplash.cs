using UnityEngine;

namespace Willowstead.World
{
    /// <summary>
    /// Plays brief ground-level splash sprites whenever a raindrop crosses the
    /// ground row. Also publishes an <see cref="OnSoilWet"/> event that other
    /// systems (crop auto-watering, ambient audio, etc.) can subscribe to. The
    /// soil-wetness effect is throttled by a configurable timer so a storm
    /// doesn't hammer GridManager WorldToCell every drop.
    /// </summary>
    public class RainSplash : MonoBehaviour
    {
        public static RainSplash Instance { get; private set; }

        /// <summary>
        /// Fires at most once per <see cref="_wateringThrottleSeconds"/> with
        /// the latest splash hit world-space position. Subscribers that need
        /// every drop should listen to <see cref="RainRenderer.OnDropHitGround"/>
        /// directly instead of going through the throttled gate.
        /// </summary>
        public event System.Action<Vector3> OnSoilWet;

        [Header("VFX")]
        [Tooltip("Sprite used per splash burst (typically a small puddle ring or droplet cluster).")]
        [SerializeField] private Sprite _splashSprite;
        [Tooltip("Tint for the splash sprite — slightly brighter / less alpha than the rain itself.")]
        [SerializeField] private Color _splashTint = new Color(0.9f, 0.95f, 1f, 0.85f);
        [Tooltip("Lifetime in seconds before the splash sprite hides. The sprite fades out over this duration.")]
        [SerializeField] private float _splashLifetime = 0.22f;
        [Tooltip("Random Z-rotation range in degrees for splash sprites so they don't all look identical.")]
        [SerializeField] private float _splashJitterDegrees = 25f;
        [Tooltip("Pool size. Simultaneous splashes equal a fraction of the RainRenderer pool; 64 handles normal storms comfortably.")]
        [SerializeField] private int _poolSize = 64;

        [Header("Soil Watering Throttle")]
        [Tooltip("Seconds between actual moisture-nudge calls. Splashes fire continuously; only this many seconds apart do we nudge soil.")]
        [SerializeField] private float _wateringThrottleSeconds = 0.20f;
        [Tooltip("If true, also call GridManager.IncreaseMoistureFromRain on the throttled hit. Disable to make rain purely visual.")]
        [SerializeField] private bool _waterSoilFromRain = true;

        private class Splash
        {
            public GameObject gameObject;
            public Transform transform;
            public SpriteRenderer renderer;
            public float timer;
            public bool isActive;
        }
        private Splash[] _splashPool;
        private Camera _mainCamera;
        private GameObject _container;
        private float _wateringTimer;
        private int _nextPoolIndex;

        public float SoilWateringThrottleSeconds => _wateringThrottleSeconds;

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
                Debug.LogWarning("[RainSplash] No Camera.main found. Splash VFX will not render.", this);
                return;
            }
            EnsureContainer();
            BuildPool();
            Subscribe();
        }

        private void OnEnable()
        {
            if (_splashPool != null) Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void Subscribe()
        {
            if (RainRenderer.Instance == null) return;
            RainRenderer.Instance.OnDropHitGround -= HandleDropHit;
            RainRenderer.Instance.OnDropHitGround += HandleDropHit;
        }

        private void Unsubscribe()
        {
            if (RainRenderer.Instance == null) return;
            RainRenderer.Instance.OnDropHitGround -= HandleDropHit;
        }

        private void EnsureContainer()
        {
            // Camera-parented so splashes follow camera movement.
            _container = new GameObject("RainSplashesContainer");
            _container.transform.SetParent(_mainCamera.transform, false);
        }

        private void BuildPool()
        {
            _splashPool = new Splash[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                GameObject go = new GameObject($"RainSplash_{i}");
                go.transform.SetParent(_container.transform, false);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _splashSprite;
                sr.color = _splashTint;
                sr.sortingLayerName = "Default";
                sr.sortingOrder = -30900; // above raindrops, below world objects
                sr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                sr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
                sr.enabled = false;

                _splashPool[i] = new Splash
                {
                    gameObject = go,
                    transform = go.transform,
                    renderer = sr,
                    isActive = false
                };
            }
        }

        private void Update()
        {
            if (_splashPool == null) return;
            for (int i = 0; i < _splashPool.Length; i++)
            {
                Splash s = _splashPool[i];
                if (!s.isActive) continue;
                s.timer -= Time.deltaTime;
                if (s.timer <= 0f)
                {
                    s.isActive = false;
                    s.renderer.enabled = false;
                }
                else
                {
                    float lifeRatio = Mathf.Clamp01(s.timer / _splashLifetime);
                    Color c = _splashTint;
                    c.a = _splashTint.a * lifeRatio;
                    s.renderer.color = c;
                }
            }
        }

        private void HandleDropHit(Vector3 worldPos)
        {
            SpawnSplashAt(worldPos);

            // hundreds of times per second.
            _wateringTimer -= Time.deltaTime;
            if (_wateringTimer > 0f) return;
            _wateringTimer = _wateringThrottleSeconds;

            // ambient mud audio, wet-footstep tint, custom crop logic).
            OnSoilWet?.Invoke(worldPos);

            // Only nudge GridManager if the developer opted in.
            if (_waterSoilFromRain && GridManager.Instance != null)
            {
                Vector3Int cell = GridManager.Instance.WorldToCell(worldPos);
                GridManager.Instance.IncreaseMoistureFromRain(cell);
            }
        }

        private void SpawnSplashAt(Vector3 worldPos)
        {
            Vector3 local = _mainCamera.transform.InverseTransformPoint(worldPos);

            Splash s = _splashPool[_nextPoolIndex];
            _nextPoolIndex = (_nextPoolIndex + 1) % _splashPool.Length;

            s.isActive = true;
            s.timer = _splashLifetime;
            s.transform.localPosition = local;
            s.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-_splashJitterDegrees, _splashJitterDegrees));
            s.renderer.enabled = true;
            s.renderer.color = _splashTint;
        }

        /// <summary>
        /// Late-binding setter for the splash sprite. Safe to call either before
        /// or after <see cref="Start"/>; live splashes are re-skinned in-place.
        /// Use this when a parent system (e.g. <c>WeatherCycle.EnsureRainSystem</c>)
        /// wires assets via code instead of a static Inspector reference.
        /// </summary>
        public void Configure(Sprite splashSprite)
        {
            _splashSprite = splashSprite;
            if (_splashPool != null)
            {
                for (int i = 0; i < _splashPool.Length; i++)
                {
                    if (_splashPool[i] != null && _splashPool[i].renderer != null)
                    {
                        _splashPool[i].renderer.sprite = splashSprite;
                    }
                }
            }
        }
    }
}
