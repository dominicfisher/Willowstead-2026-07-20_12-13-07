// Drives a full-screen blue Canvas Image tint that fades in proportional to
// WeatherCycle.RainIntensity. The panel sits behind world objects but above
// most HUD elements, blocking nothing. Sub/unsubscribe follows WeatherCycle
// the same way RainRenderer/RainSplash/RainAudio do.
//
// Auto-bootstraps via RuntimeInitializeOnLoadMethod so the tint exists from
// the first frame without any scene authoring. If the scene already has a
// RainScreenTint on a GameObject, that one wins (Instance singleton pattern).

using UnityEngine;
using UnityEngine.UI;
using Willowstead.Player;

namespace Willowstead.World
{
    [DisallowMultipleComponent]
    public sealed class RainScreenTint : MonoBehaviour
    {
        public static RainScreenTint Instance { get; private set; }

        [Header("Tint")]
        [Tooltip("Base RGB colour of the rain tint. Alpha is ignored here — driven by intensity instead. Default is a soft sky-blue.")]
        [SerializeField] private Color _tintColor = new Color(0.55f, 0.75f, 1f, 1f);

        [Tooltip("Maximum alpha at rain intensity = 1.0. Try 0.30 for a noticeable stormy look; 0.15 for a subtle wash; 0 for no tint at all.")]
        [Range(0f, 0.7f)] [SerializeField] private float _maxAlpha = 0.30f;

        [Tooltip("Tint fade speed in alpha units per second. Slower = cinematic / weather-bookmark feel; faster = snappy response to weather changes. 1.2 fades 0 -> 0.30 in ~0.25s.")]
        [SerializeField] private float _fadeSpeed = 1.2f;

        [Header("Render")]
        [Tooltip("The overlay image is parented to the shared HUD canvas and pushed to the last sibling so it renders on top of other HUD panels (DebugOverlay, dev hints, etc.). Image has no per-instance sortingOrder in Unity UI; sibling index is the in-canvas sort. If you need finer control, swap UIResourceHelper for a dedicated Canvas here.")] 
        [SerializeField] private bool _renderOnTop = true;

        private Image _image;
        private float _currentAlpha;
        private float _targetAlpha;
        private bool _subscribed;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            // bootstrap and spawn a duplicate on top.
            if (Object.FindAnyObjectByType<RainScreenTint>(FindObjectsInactive.Include) != null) return;
            GameObject go = new GameObject("RainScreenTint");
            DontDestroyOnLoad(go);
            go.AddComponent<RainScreenTint>();
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }

            BuildOverlay();
            ApplyAlpha(0f);
        }

        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();

        private void OnDisable() => Unsubscribe();
        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_subscribed) TrySubscribe();
            // frame post-fade.
            float dt = Time.unscaledDeltaTime;
            float nextAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, _fadeSpeed * dt);
            if (nextAlpha == _currentAlpha) return;
            _currentAlpha = nextAlpha;
            ApplyAlpha(_currentAlpha);
        }


        private void BuildOverlay()
        {
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("RainTintCanvas");

            GameObject panelGo = new GameObject("RainTintOverlay",
                typeof(RectTransform), typeof(Image));
            RectTransform rt = panelGo.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            // Stretch to full canvas rect (no anchor stretch math, no offset accumulation).
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _image = panelGo.GetComponent<Image>();
            _image.color = new Color(_tintColor.r, _tintColor.g, _tintColor.b, 0f);
            _image.raycastTarget = false;          // never eat clicks meant for the world / HUD
            _image.sprite = null;
            // Image has no sortingOrder property. Sibling index within the canvas
            // every existing HUD panel for the duration of the rain.
            if (_renderOnTop) rt.SetAsLastSibling();
        }


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

        private void HandleIntensityChanged(float intensity)
        {
            _targetAlpha = Mathf.Clamp01(intensity) * _maxAlpha;
        }


        private void ApplyAlpha(float a)
        {
            if (_image == null) return;
            _image.color = new Color(_tintColor.r, _tintColor.g, _tintColor.b, a);
        }
    }
}
