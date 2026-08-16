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

        [Tooltip("Looping light wind ambience clips for Windy weather and background breeze.")]
        [SerializeField] private AudioClip[] _windAmbienceLoops;

        [Tooltip("Looping heavy wind whistle clips for strong wind and stormy weather.")]
        [SerializeField] private AudioClip[] _heavyWindAmbienceLoops;

        [Header("Volume")]

        /// <summary>
        /// Volume of EVERY ambience loop (outdoor + indoor) at intensity = 1.0.
        /// Lowish atmospheric volume by default (~0.18 - 0.25).
        /// </summary>
        private float _maxAmbienceVolume = 0.22f;
        [Tooltip("Volume of the thunder one-shots (kept atmospheric and low-mix).")]
        [Range(0f, 1f)] [SerializeField] private float _maxThunderVolume = 0.40f;
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
        private AudioSource[] _windSources;
        private AudioSource[] _heavyWindSources;
        private AudioSource _indoor;
        private AudioSource _thunder;
        private Camera _mainCamera;
        private float _currentIntensity;
        private float _currentWindIntensity01;
        private bool _hasStarted;

        public float CurrentIntensity => _currentIntensity;
        public bool IsIndoors => _isIndoors;


        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void Start()
        {
            _mainCamera = Camera.main;

            EnsureElementsAudioClips();

            Transform parent = _attachToCamera && _mainCamera != null
                ? (Transform)_mainCamera.transform
                : (Transform)transform;

            BuildOutdoorAmbienceSources(parent);
            BuildStormAmbienceSources(parent);
            BuildIndoorAmbienceSource(parent);
            BuildWindAmbienceSources(parent);

            _hasStarted = true;

            _thunder = CreateAudioSource("ThunderOneShot", loop: false, parent: parent);

            SubscribeToWeather();
        }

        private void EnsureElementsAudioClips()
        {
#if UNITY_EDITOR
            if (_rainAmbienceLoops == null || _rainAmbienceLoops.Length == 0)
            {
                var list = new System.Collections.Generic.List<AudioClip>();
                for (int i = 1; i <= 10; i++)
                {
                    var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Elements/WE Light Outside Rain {i}.wav");
                    if (clip != null) list.Add(clip);
                }
                if (list.Count > 0) _rainAmbienceLoops = list.ToArray();
            }

            if (_indoorAmbienceClip == null)
            {
                _indoorAmbienceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Elements/WE Light Inside Rain 1.wav");
            }

            if (_stormAmbienceLoops == null || _stormAmbienceLoops.Length == 0)
            {
                var list = new System.Collections.Generic.List<AudioClip>();
                for (int i = 1; i <= 10; i++)
                {
                    var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Elements/WE Heavy Outside Rain {i}.wav");
                    if (clip != null) list.Add(clip);
                }
                if (list.Count > 0) _stormAmbienceLoops = list.ToArray();
            }

            if (_windAmbienceLoops == null || _windAmbienceLoops.Length == 0)
            {
                var list = new System.Collections.Generic.List<AudioClip>();
                for (int i = 1; i <= 10; i++)
                {
                    var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Elements/WE Light Wind Whistle {i}.wav");
                    if (clip != null) list.Add(clip);
                }
                if (list.Count > 0) _windAmbienceLoops = list.ToArray();
            }

            if (_heavyWindAmbienceLoops == null || _heavyWindAmbienceLoops.Length == 0)
            {
                var list = new System.Collections.Generic.List<AudioClip>();
                for (int i = 1; i <= 10; i++)
                {
                    var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Elements/WE Heavy Wind Whistle {i}.wav");
                    if (clip != null) list.Add(clip);
                }
                if (list.Count > 0) _heavyWindAmbienceLoops = list.ToArray();
            }

            if (_thunderClips == null || _thunderClips.Length == 0)
            {
                var list = new System.Collections.Generic.List<AudioClip>();
                for (int i = 1; i <= 31; i++)
                {
                    var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Elements/WE Thunder {i}.wav");
                    if (clip != null) list.Add(clip);
                }
                if (list.Count > 0) _thunderClips = list.ToArray();
            }
#endif
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

        private void BuildWindAmbienceSources(Transform parent)
        {
            if (_windAmbienceLoops != null && _windAmbienceLoops.Length > 0)
            {
                _windSources = new AudioSource[_windAmbienceLoops.Length];
                for (int i = 0; i < _windAmbienceLoops.Length; i++)
                {
                    AudioSource src = CreateAudioSource($"WindAmbience_{i}", loop: true, parent: parent);
                    src.spatialBlend = _ambienceSpatialBlend;
                    AudioClip clip = _windAmbienceLoops[i];
                    if (clip != null)
                    {
                        src.clip = clip;
                        src.volume = 0f;
                        src.Play();
                    }
                    _windSources[i] = src;
                }
            }
            else
            {
                _windSources = System.Array.Empty<AudioSource>();
            }

            if (_heavyWindAmbienceLoops != null && _heavyWindAmbienceLoops.Length > 0)
            {
                _heavyWindSources = new AudioSource[_heavyWindAmbienceLoops.Length];
                for (int i = 0; i < _heavyWindAmbienceLoops.Length; i++)
                {
                    AudioSource src = CreateAudioSource($"HeavyWindAmbience_{i}", loop: true, parent: parent);
                    src.spatialBlend = _ambienceSpatialBlend;
                    AudioClip clip = _heavyWindAmbienceLoops[i];
                    if (clip != null)
                    {
                        src.clip = clip;
                        src.volume = 0f;
                        src.Play();
                    }
                    _heavyWindSources[i] = src;
                }
            }
            else
            {
                _heavyWindSources = System.Array.Empty<AudioSource>();
            }
        }

        private void OnEnable()
        {
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
            src.spatialBlend = 0f;
            return src;
        }

        private void Update()
        {
            float ceiling = _maxAmbienceVolume * Willowstead.Audio.AudioManager.WeatherVolume;
            float outdoorRainTarget = _isIndoors ? 0f : _currentIntensity * ceiling;
            float indoorRainTarget = _isIndoors ? _currentIntensity * ceiling : 0f;

            // Calculate wind volume target from WeatherCycle
            float windTarget = 0f;
            float heavyWindTarget = 0f;
            if (WeatherCycle.Instance != null && !_isIndoors)
            {
                if (WeatherCycle.Instance.CurrentWeather == WeatherType.Windy)
                {
                    switch (WeatherCycle.Instance.CurrentIntensity)
                    {
                        case WindIntensity.Light:
                            windTarget = ceiling * 0.6f;
                            break;
                        case WindIntensity.Moderate:
                            windTarget = ceiling * 0.9f;
                            heavyWindTarget = ceiling * 0.35f;
                            break;
                        case WindIntensity.Strong:
                            windTarget = ceiling * 0.7f;
                            heavyWindTarget = ceiling * 0.95f;
                            break;
                    }
                }
                else if (WeatherCycle.Instance.CurrentWeather == WeatherType.Rainy)
                {
                    // Rainy storms also carry wind whistle
                    float stormWeight = Mathf.Clamp01((_currentIntensity - 0.5f) / 0.5f);
                    windTarget = stormWeight * ceiling * 0.5f;
                    heavyWindTarget = stormWeight * ceiling * 0.75f;
                }
            }

            // 1. Rain ambience loops
            if (_ambiences != null)
            {
                for (int i = 0; i < _ambiences.Length; i++)
                {
                    AudioSource src = _ambiences[i];
                    if (src == null || src.clip == null) continue;
                    src.volume = Mathf.MoveTowards(src.volume, outdoorRainTarget, _ambienceLerpSpeed * Time.deltaTime);
                    if (src.volume < 0.005f) src.volume = 0f;
                }
            }

            // 2. Indoor rain
            if (_indoor != null && _indoor.clip != null)
            {
                _indoor.volume = Mathf.MoveTowards(_indoor.volume, indoorRainTarget, _ambienceLerpSpeed * Time.deltaTime);
                if (_indoor.volume < 0.005f) _indoor.volume = 0f;
            }

            // 3. Storm thunder rumble loops
            float stormWeightRumble = Mathf.Clamp01((_currentIntensity - 0.66f) / 0.34f);
            float stormTarget = _isIndoors ? 0f : stormWeightRumble * ceiling;
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

            // 4. Wind ambience loops
            if (_windSources != null)
            {
                for (int i = 0; i < _windSources.Length; i++)
                {
                    AudioSource src = _windSources[i];
                    if (src == null || src.clip == null) continue;
                    src.volume = Mathf.MoveTowards(src.volume, windTarget, _ambienceLerpSpeed * Time.deltaTime);
                    if (src.volume < 0.005f) src.volume = 0f;
                }
            }

            // 5. Heavy wind whistle loops
            if (_heavyWindSources != null)
            {
                for (int i = 0; i < _heavyWindSources.Length; i++)
                {
                    AudioSource src = _heavyWindSources[i];
                    if (src == null || src.clip == null) continue;
                    src.volume = Mathf.MoveTowards(src.volume, heavyWindTarget, _ambienceLerpSpeed * Time.deltaTime);
                    if (src.volume < 0.005f) src.volume = 0f;
                }
            }
        }

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
            // becomes a real-time wait which is the desired behaviour.
            yield return new WaitForSeconds(delay);
            if (_thunder == null) yield break;
            AudioClip clip = _thunderClips[Random.Range(0, _thunderClips.Length)];
            if (clip != null)
            {
                _thunder.PlayOneShot(clip, _maxThunderVolume * Willowstead.Audio.AudioManager.WeatherVolume);
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
        public void Configure(AudioClip[] outdoorLoops, AudioClip indoorLoop, AudioClip[] thunderClips, AudioClip[] stormLoops, AudioClip[] windLoops = null, AudioClip[] heavyWindLoops = null)
        {
            _rainAmbienceLoops = outdoorLoops;
            _indoorAmbienceClip = indoorLoop;
            _thunderClips = thunderClips;
            _stormAmbienceLoops = stormLoops ?? System.Array.Empty<AudioClip>();
            if (windLoops != null) _windAmbienceLoops = windLoops;
            if (heavyWindLoops != null) _heavyWindAmbienceLoops = heavyWindLoops;

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
            if (_windSources != null)
            {
                for (int i = 0; i < _windSources.Length; i++)
                {
                    if (_windSources[i] != null && _windSources[i].gameObject != null)
                    {
                        Destroy(_windSources[i].gameObject);
                    }
                }
                _windSources = null;
            }
            if (_heavyWindSources != null)
            {
                for (int i = 0; i < _heavyWindSources.Length; i++)
                {
                    if (_heavyWindSources[i] != null && _heavyWindSources[i].gameObject != null)
                    {
                        Destroy(_heavyWindSources[i].gameObject);
                    }
                }
                _heavyWindSources = null;
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
            BuildWindAmbienceSources(parent);
        }
    }
}
