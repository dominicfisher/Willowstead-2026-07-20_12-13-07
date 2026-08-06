// Drives a Unity ParticleSystem as the rain field, mirroring WeatherCycle's
// live rain intensity onto the Shuriken emitter and syncing wind direction.
// Use this if you prefer Shuriken particles over the sprite-pool RainRenderer.
//
// Setup:
//   1. Right-click your WeatherCycle GameObject > Create Empty Child. Name it RainParticles.
//   2. Add Component > Effects > Particle System (Unity adds it + Renderer).
//   3. Author the particle system in the Inspector (sprite, shape, lifetime, etc.).
//   4. Add Component > RainParticleDriver on the same GameObject.
//   5. (Optional) live-edit the Emission Curve and Wind Strength on the driver.
//   6. Press Play, type `weather rain` in the dev console (`) to test.

using UnityEngine;

namespace Willowstead.World
{
    [RequireComponent(typeof(ParticleSystem))]
    [DisallowMultipleComponent]
    public sealed class RainParticleDriver : MonoBehaviour
    {
        public static RainParticleDriver Instance { get; private set; }

        [Header("Emission")]
        [Tooltip("Map of rain intensity (0..1) -> emission rate (particles/sec). Default ramps 0 -> 800 across the curve.")]
        [SerializeField] private AnimationCurve _emissionByIntensity = AnimationCurve.EaseInOut(0f, 0f, 1f, 800f);

        [Tooltip("Multiplier applied on top of the curve read, so the whole curve scales without re-editing keyframes.")]
        [SerializeField] private float _emissionMultiplier = 1f;

        [Header("Wind")]
        [Tooltip("Magnitude of horizontal wind drift applied via velocityOverLifetime.x. WindDirection.Left -> -_windStrength, Right -> +_windStrength. At default ~5 with PS startSpeed 12 over a 1.5s lifetime, drops lean ~23\u00b0 from vertical \u2014 matching RainRenderer's _dropTiltDegrees default.")]
        [SerializeField] private float _windStrength = 5f;

        [Header("Camera")]
        [Tooltip("If unassigned, uses Camera.main at Start. The driver's transform stays put; only the wind bias is in world space.")]
        [SerializeField] private Camera _camera;

        // Cached references — particle modules require module struct reads, not GetComponent-style calls per frame.
        private ParticleSystem _ps;
        private ParticleSystem.EmissionModule _emission;
        private ParticleSystem.VelocityOverLifetimeModule _velocity;
        private ParticleSystem.MainModule _main;

        private float _currentIntensity;
        private bool _subscribed;
        // Out-of-range sentinel so first Update always syncs. WindDirection.Left is the
        // first enum value AND the actual default in WeatherCycle, so default(WindDirection)
        // would falsely match and skip the first ApplyWind call.
        private WindDirection _lastWind = (WindDirection)(-1);

        // ─── Lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }

            _ps = GetComponent<ParticleSystem>();
            _emission = _ps.emission;
            _velocity = _ps.velocityOverLifetime;
            _main = _ps.main;

            // World-space so velocityOverLifetime.x is in world horizontal terms,
            // not "downstream from emitter local-X", which would skew with the
            // camera-relative emitter position.
            _main.simulationSpace = ParticleSystemSimulationSpace.World;
            _emission.enabled = false;
        }

        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();
        private void Update()
        {
            // Late subscribe handles the WeatherCycle-bootstrap-after-driver case.
            if (!_subscribed) TrySubscribe();

            if (WeatherCycle.Instance == null) return;

            WindDirection wind = WeatherCycle.Instance.CurrentWindDirection;
            if (wind != _lastWind)
            {
                _lastWind = wind;
                ApplyWind(wind);
            }
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        // ─── Subscription ─────────────────────────────────────────────

        private void TrySubscribe()
        {
            if (_subscribed) return;
            WeatherCycle wc = WeatherCycle.Instance;
            if (wc == null) return;
            wc.OnIntensityChanged -= HandleIntensityChanged; // defensive: no double-fire
            wc.OnIntensityChanged += HandleIntensityChanged;
            _subscribed = true;
            HandleIntensityChanged(wc.RainIntensity);
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            WeatherCycle wc = WeatherCycle.Instance;
            if (wc != null) wc.OnIntensityChanged -= HandleIntensityChanged;
            _subscribed = false;
        }

        // ─── Live update ──────────────────────────────────────────────

        private void HandleIntensityChanged(float intensity)
        {
            _currentIntensity = Mathf.Clamp01(intensity);

            if (_currentIntensity <= 0f)
            {
                _emission.enabled = false;
                if (_ps.isPlaying)
                {
                    _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                return;
            }

            float rate = Mathf.Max(0f, _emissionByIntensity.Evaluate(_currentIntensity) * _emissionMultiplier);
            _emission.rateOverTime = rate;
            _emission.enabled = rate > 0f;

            if (rate > 0f && !_ps.isPlaying) _ps.Play();
        }

        private void ApplyWind(WindDirection wind)
        {
            float xBias = (wind == WindDirection.Left) ? -_windStrength : +_windStrength;
            // Constant MinMaxCurve (single-float constructor) -> same value every particle,
            // every emission. Override OnEmit logic done by user via ParticleSystem modules.
            _velocity.x = new ParticleSystem.MinMaxCurve(xBias);
            _velocity.enabled = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _emissionMultiplier = Mathf.Max(0f, _emissionMultiplier);
            _windStrength = Mathf.Max(0f, _windStrength);
        }
#endif
    }
}
