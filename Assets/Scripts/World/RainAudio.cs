using UnityEngine;
using System.Collections;

namespace Willowstead.World
{
    /// <summary>
    /// Plays a looped rain ambience track whose volume tracks
    /// <c>WeatherCycle.RainIntensity</c>. On lightning strikes, schedules a
    /// one-shot thunder SFX with a randomised delay (the gap is what makes
    /// thunder sound distant). Sources are camera-parented so spatial audio
    /// follows the player without needing listener logic.
    /// </summary>
    public class RainAudio : MonoBehaviour
    {
        public static RainAudio Instance { get; private set; }

        [Header("Audio Sources")]
        [Tooltip("Multiple looping rain-ambience tracks that play simultaneously. Layer a base bed with finer droplet or wind variation for a richer soundscape. Each clip gets its own AudioSource; their volumes are summed to _maxAmbienceVolume at intensity = 1.0.")]
        [SerializeField] private AudioClip[] _rainAmbienceLoops;
        [Tooltip("Single ambient track played in place of the outdoor loops when the player is indoors. Cross-faded in/out via SetIndoors().")]
        [SerializeField] private AudioClip _indoorAmbienceClip;
        [Tooltip("One or more thunder SFX clips. Randomly picked per strike so consecutive strikes don't sound identical.")]
        [SerializeField] private AudioClip[] _thunderClips;

        [Tooltip("Looping storm ambience clips with baked-in thunder rumble — fades in proportional to rain intensity so a Light rain has no rumble but a Storm has continuous rumbling thunder. Drag files like 'Rain & Thunder.mp3' and 'Rain & Thunder (Variation).mp3' here. Storm loops are muted when SetIndoors(true).")]
        [SerializeField] private AudioClip[] _stormAmbienceLoops;

        [Header("Volume")]

        /// <summary>
        /// Volume of EVERY ambience loop (outdoor + indoor) at intensity = 1.0.
        /// <b>Not</b> a SerializeField — the canonical slider lives on
        /// <c>WeatherCycle._maxAmbienceVolume</c>, which pushes the value down
        /// via <see cref="SetAmbienceVolume"/> at Start and on every Inspector
        /// edit during play (via WeatherCycle.OnValidate). Keeping this private
        /// prevents a second Inspector knob that would be silently overwritten
        /// by WeatherCycle at runtime. Default 0.30 is the fallback when
        /// RainAudio is instantiated without a parent WeatherCycle.
        /// </summary>
        private float _maxAmbienceVolume = 0.30f;
        [Tooltip("Volume of the loudest thunder one-shot.")]
        [Range(0f, 1f)] [SerializeField] private float _maxThunderVolume = 1f;
        [Tooltip("Smoothing speed for the ambience volume lerp (per second). Higher = faster response to intensity changes.")]
        [SerializeField] private float _ambienceLerpSpeed = 2.5f;

        [Header("Behaviour")]
        [Tooltip("If true, the audio sources are parented to Camera.main so spatial audio follows the player.")]
        [SerializeField] private bool _attachToCamera = true;
        [Tooltip("Minimum seconds between a lightning strike event and the audible thunder. Short delays = nearby strikes.")]
        [SerializeField] private float _thunderMinDelay = 0.4f;
        [Tooltip("Maximum seconds between a lightning strike event and the audible thunder. Long delays = distant strikes.")]
        [SerializeField] private float _thunderMaxDelay = 2.5f;
        [Tooltip("Spatial blend of every ambience source. 0 = full 2D (typical for 2D games), 1 = full 3D.")]
        [Range(0f, 1f)] [SerializeField] private float _ambienceSpatialBlend = 0f;
        [Tooltip("Current inside/outside state. Cross-fades between the outdoor loop array and the indoor loop. Toggled via inspector, dev console, or WeatherCycle.SetIndoors.")]
        [SerializeField] private bool _isIndoors;

        private AudioSource[] _ambiences;
        private AudioSource[] _stormSources;
        private AudioSource _indoor;
        private AudioSource _thunder;
        private Camera _mainCamera;
        private float _currentIntensity;
        private bool _hasStarted;

        public float CurrentIntensity => _currentIntensity;
        public bool IsIndoors => _isIndoors;

        // ─── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void Start()
        {
            _mainCamera = Camera.main;

            Transform parent = _attachToCamera && _mainCamera != null
                ? (Transform)_mainCamera.transform
                : (Transform)transform;

            // Each outdoor ambience clip gets its own AudioSource so volumes
            // can be summed/balanced individually. The sources are kept alive
            // at all times (volume = 0 when not active) so crossfade transitions
            // never restart playback on either side.
            BuildOutdoorAmbienceSources(parent);
            BuildStormAmbienceSources(parent);
            BuildIndoorAmbienceSource(parent);

            _hasStarted = true;

            _thunder = CreateAudioSource("ThunderOneShot", loop: false, parent: parent);
            // Thunder is a one-shot — keep loop off (CreateAudioSource already
            // sets loop = false here, but make intent explicit).

            if ((_rainAmbienceLoops == null || _rainAmbienceLoops.Length == 0) && _indoorAmbienceClip == null)
            {
                Debug.LogWarning("[RainAudio] No ambience clips assigned — rain will be silent. Drag looping AudioClips into Rain Ambience Loops and/or Indoor Ambience Clip in the Inspector.", this);
            }

            // Subscribe to the weather cycle. Subscription is forgiving: if
            // WeatherCycle hasn't booted yet, we'll catch it when it does
            // because WeatherCycle.Instance is set inside its own Start.
            SubscribeToWeather();
        }

        private void BuildOutdoorAmbienceSources(Transform parent)
        {
            if (_rainAmbienceLoops == null || _rainAmbienceLoops.Length == 0)
            {
                _ambiences = System.Array.Empty<AudioSource>();
                return;
            }
            _ambiences = new AudioSource[_rainAmbienceLoops.Length];
            for (int i = 0; i < _rainAmbienceLoops.Length; i++)
            {
                AudioSource src = CreateAudioSource($"RainAmbience_{i}", loop: true, parent: parent);
                src.spatialBlend = _ambienceSpatialBlend;
                AudioClip clip = _rainAmbienceLoops[i];
                if (clip != null)
                {
                    src.clip = clip;
                    src.volume = 0f;
                    src.Play();
                }
                _ambiences[i] = src;
            }
        }

        private void BuildIndoorAmbienceSource(Transform parent)
        {
            _indoor = CreateAudioSource("RainAmbience_Indoor", loop: true, parent: parent);
            _indoor.spatialBlend = _ambienceSpatialBlend;
            if (_indoorAmbienceClip != null)
            {
                _indoor.clip = _indoorAmbienceClip;
                _indoor.volume = 0f;
                _indoor.Play();
            }
        }

        /// <summary>
        /// Mirror of <see cref="BuildOutdoorAmbienceSources"/> for the storm
        /// pool. Same parent / spatial-blend / volume-0-at-start conventions so
        /// fades in <see cref="Update"/> are smooth lerps, not start/stop clicks.
        /// </summary>
        private void BuildStormAmbienceSources(Transform parent)
        {
            if (_stormAmbienceLoops == null || _stormAmbienceLoops.Length == 0)
            {
                _stormSources = System.Array.Empty<AudioSource>();
                return;
            }
            _stormSources = new AudioSource[_stormAmbienceLoops.Length];
            for (int i = 0; i < _stormAmbienceLoops.Length; i++)
            {
                AudioSource src = CreateAudioSource($"RainStormAmbience_{i}", loop: true, parent: parent);
                src.spatialBlend = _ambienceSpatialBlend;
                AudioClip clip = _stormAmbienceLoops[i];
                if (clip != null)
                {
                    src.clip = clip;
                    src.volume = 0f;
                    src.Play();
                }
                _stormSources[i] = src;
            }
        }

        private void OnEnable()
        {
            // If we were disabled mid-game, the Subscription dedupes itself
            // on re-enable so we don't double-register.
            SubscribeToWeather();
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
            WeatherCycle.Instance.OnIntensityChanged    -= HandleIntensity;
            WeatherCycle.Instance.OnIntensityChanged    += HandleIntensity;
            WeatherCycle.Instance.OnLightningStrike     -= HandleLightningStrike;
            WeatherCycle.Instance.OnLightningStrike     += HandleLightningStrike;
            HandleIntensity(WeatherCycle.Instance.RainIntensity);
        }

        private void UnsubscribeFromWeather()
        {
            if (WeatherCycle.Instance == null) return;
            WeatherCycle.Instance.OnIntensityChanged -= HandleIntensity;
            WeatherCycle.Instance.OnLightningStrike  -= HandleLightningStrike;
        }

        private AudioSource CreateAudioSource(string name, bool loop, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f; // 2D world — full volume regardless of camera distance
            return src;
        }

        // ─── Per-frame logic ───────────────────────────────────────────

        private void Update()
        {
            // Outdoor volume scales with intensity; indoor volume is independent
            // and only fades in when SetIndoors(true) is called. Both groups are
            // always alive so the crossfade is a smooth lerp, not a start/stop.
            // Single ceiling = _maxAmbienceVolume (Inspector slider); no runtime
            // multiplier so a developer tweak in the Inspector is the only knob.
            float ceiling = _maxAmbienceVolume;
            float outdoorTarget = _isIndoors ? 0f : _currentIntensity * ceiling;
            float indoorTarget = _isIndoors ? _currentIntensity * ceiling : 0f;

            if (_ambiences != null)
            {
                for (int i = 0; i < _ambiences.Length; i++)
                {
                    AudioSource src = _ambiences[i];
                    if (src == null || src.clip == null) continue;
                    src.volume = Mathf.MoveTowards(src.volume, outdoorTarget, _ambienceLerpSpeed * Time.deltaTime);
                    if (src.volume < 0.005f) src.volume = 0f;
                }
            }

            if (_indoor != null && _indoor.clip != null)
            {
                _indoor.volume = Mathf.MoveTowards(_indoor.volume, indoorTarget, _ambienceLerpSpeed * Time.deltaTime);
                if (_indoor.volume < 0.005f) _indoor.volume = 0f;
            }

            // Storm loops crossfade in only as rain intensity climbs from
            // Moderate (0.66) toward Storm (1.0). Smooth ramp instead of a
            // sharp threshold so the rumble grows gradually as a storm rolls
            // in. Below Moderate the rumble is silent; muted indoors because
            // outdoor thunder doesn't bleed inside buildings.
            //
            // Magic numbers 0.66 and 0.34 mirror the literal bin values
            // WeatherCycle.RainIntensity emits for WindIntensity.Moderate (0.66)
            // and the Storm end (1.00). If the bins in WeatherCycle change,
            // update these too — or refactor to expose them as public constants.
            float stormWeight = Mathf.Clamp01((_currentIntensity - 0.66f) / 0.34f);
            float stormTarget = _isIndoors ? 0f : stormWeight * ceiling;
            if (_stormSources != null)
            {
                for (int i = 0; i < _stormSources.Length; i++)
                {
                    AudioSource src = _stormSources[i];
                    if (src == null || src.clip == null) continue;
                    src.volume = Mathf.MoveTowards(src.volume, stormTarget, _ambienceLerpSpeed * Time.deltaTime);
                    if (src.volume < 0.005f) src.volume = 0f;
                }
            }
        }

        // ─── Weather hooks ─────────────────────────────────────────────

        private void HandleIntensity(float intensity)
        {
            _currentIntensity = Mathf.Clamp01(intensity);
        }

        /// <summary>
        /// Toggle inside/outside context. Cross-fades are handled by the per-frame
        /// volume lerp, so this just sets the flag and the audio follows within
        /// ~0.4 seconds. Safe to call before <see cref="Start"/> (the flag is
        /// stored and the first Update tick handles the fade).
        /// </summary>
        public void SetIndoors(bool indoors)
        {
            _isIndoors = indoors;
        }

        /// <summary>
        /// Master volume of the ambience loop set (outdoor + indoor) when rain
        /// intensity = 1.0. WeatherCycle pushes its Inspector slider value down
        /// here at Start via <see cref="SetAmbienceVolume"/>, so this method is
        /// the single seam between WeatherCycle's audio configuration and
        /// RainAudio's playback. Pass an invalid value (NaN, &gt; 1, &lt; 0) and
        /// it gets clamped. Thunder volume is unaffected — it's a separate ceiling.
        /// </summary>
        public void SetAmbienceVolume(float volume01)
        {
            _maxAmbienceVolume = Mathf.Clamp01(volume01);
        }

        private void HandleLightningStrike()
        {
            if (_thunder == null) return;
            if (_thunderClips == null || _thunderClips.Length == 0) return;

            float delay = Random.Range(_thunderMinDelay, _thunderMaxDelay);
            StartCoroutine(PlayThunderAfter(delay));
        }

        private IEnumerator PlayThunderAfter(float delay)
        {
            // WaitForSeconds is fine here — timescale=0 normally doesn't apply
            // because the pause menu pauses separately, but if it does this
            // becomes a real-time wait which is the desired behaviour.
            yield return new WaitForSeconds(delay);
            if (_thunder == null) yield break;
            AudioClip clip = _thunderClips[Random.Range(0, _thunderClips.Length)];
            if (clip != null)
            {
                _thunder.PlayOneShot(clip, _maxThunderVolume);
            }
        }

        /// <summary>
        /// Late-binding setter for the ambient + thunder + storm clip set. Safe to
        /// call either before or after <see cref="Start"/>: when called AFTER, the
        /// existing outdoor / storm sources are torn down and rebuilt with the
        /// new clip arrays, and the indoor source is rewired. Use this when a
        /// parent system (e.g. <c>WeatherCycle.EnsureRainSystem</c>) wires assets
        /// via code instead of a static Inspector reference. <paramref name="stormLoops"/>
        /// may be null or empty — the storm layer simply goes silent in that case.
        /// The 4-argument form is required; no default value on <paramref name="stormLoops"/>
        /// so a 3-arg call can't silently null out an Inspector-assigned field.
        /// </summary>
        public void Configure(AudioClip[] outdoorLoops, AudioClip indoorLoop, AudioClip[] thunderClips, AudioClip[] stormLoops)
        {
            _rainAmbienceLoops = outdoorLoops;
            _indoorAmbienceClip = indoorLoop;
            _thunderClips = thunderClips;
            _stormAmbienceLoops = stormLoops ?? System.Array.Empty<AudioClip>();

            // If Start hasn't run yet OR the camera isn't ready, just store the
            // arrays and let Start() build the sources with them. Tearing down
            // AudioSources at this point would leave the playback layer silent
            // if the camera becomes available later.
            if (_mainCamera == null) return;
            if (!_hasStarted) return;

            if (_ambiences != null)
            {
                for (int i = 0; i < _ambiences.Length; i++)
                {
                    if (_ambiences[i] != null && _ambiences[i].gameObject != null)
                    {
                        Destroy(_ambiences[i].gameObject);
                    }
                }
                _ambiences = null;
            }
            if (_stormSources != null)
            {
                for (int i = 0; i < _stormSources.Length; i++)
                {
                    if (_stormSources[i] != null && _stormSources[i].gameObject != null)
                    {
                        Destroy(_stormSources[i].gameObject);
                    }
                }
                _stormSources = null;
            }
            if (_indoor != null && _indoor.gameObject != null)
            {
                Destroy(_indoor.gameObject);
                _indoor = null;
            }

            Transform parent = _attachToCamera
                ? (Transform)_mainCamera.transform
                : (Transform)transform;
            BuildOutdoorAmbienceSources(parent);
            BuildStormAmbienceSources(parent);
            BuildIndoorAmbienceSource(parent);
        }
    }
}
