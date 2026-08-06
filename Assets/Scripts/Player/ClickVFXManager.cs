using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Willowstead.Player
{
    /// <summary>
    /// Detects mouse clicks and plays a short animated effect at the click position.
    /// The effect is rendered on the HUD canvas so it works over both world and UI.
    /// </summary>
    public class ClickVFXManager : MonoBehaviour
    {
        public static ClickVFXManager Instance { get; private set; }

        [Header("Click Effect Frames")]
        [Tooltip("Drag the mouse click animation frames here (frames 1-3).")]
        [SerializeField] private Sprite[] _clickFrames;

        [Tooltip("Seconds each frame is shown.")]
        [SerializeField] private float _frameDuration = 0.08f;

        [Tooltip("Size of the click effect in screen pixels.")]
        [SerializeField] private Vector2 _effectSize = new Vector2(32f, 32f);

        [Tooltip("How many simultaneous click effects can be playing.")]
        [SerializeField] private int _poolSize = 8;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("ClickVFXManager");
            go.AddComponent<ClickVFXManager>();
        }

        private Transform _poolParent;
        private ClickVFX[] _pool;
        private int _nextIndex;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Frame 4 is the resting cursor, frames 1-3 are the click animation.
            EnsureClickFramesLoaded();
        }
#endif

        private void EnsureClickFramesLoaded()
        {
            bool allMissing = _clickFrames == null || _clickFrames.Length == 0
                              || System.Array.TrueForAll(_clickFrames, s => s == null);

            if (allMissing)
            {
                _clickFrames = new Sprite[]
                {
                    Resources.Load<Sprite>("Mouse/UI_TravelBook_MouseCursorClick01a_1"),
                    Resources.Load<Sprite>("Mouse/UI_TravelBook_MouseCursorClick01a_2"),
                    Resources.Load<Sprite>("Mouse/UI_TravelBook_MouseCursorClick01a_3"),
                };
#if UNITY_EDITOR
                // Editor convenience: fall back to AssetDatabase if Resources didn't find them.
                if (_clickFrames[0] == null) _clickFrames[0] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Mouse/UI_TravelBook_MouseCursorClick01a_1.png");
                if (_clickFrames[1] == null) _clickFrames[1] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Mouse/UI_TravelBook_MouseCursorClick01a_2.png");
                if (_clickFrames[2] == null) _clickFrames[2] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Mouse/UI_TravelBook_MouseCursorClick01a_3.png");
#endif
            }

            // Drop any nulls so the animation cleanly skips missing frames.
            if (_clickFrames != null)
                _clickFrames = System.Array.FindAll(_clickFrames, s => s != null);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                EnsureClickFramesLoaded();
                BuildPool();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (Mouse.current == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
                PlayAt(Mouse.current.position.ReadValue());
        }

        /// <summary>
        /// Plays the click effect at the given screen position.
        /// </summary>
        public void PlayAt(Vector2 screenPosition)
        {
            if (_pool == null || _pool.Length == 0 || _clickFrames == null || _clickFrames.Length == 0)
                return;

            ClickVFX vfx = GetNextAvailable();
            if (vfx == null) return;

            RectTransform rt = vfx.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = screenPosition;
                rt.sizeDelta = _effectSize;
                rt.SetAsLastSibling();
            }

            vfx.Play(_clickFrames, _frameDuration);
        }

        private void BuildPool()
        {
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            if (canvas == null) return;

            GameObject poolGo = new GameObject("ClickVFXPool");
            poolGo.transform.SetParent(canvas.transform, false);
            _poolParent = poolGo.transform;

            _pool = new ClickVFX[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                GameObject go = new GameObject($"ClickVFX_{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_poolParent, false);

                Image img = go.GetComponent<Image>();
                img.raycastTarget = false;

                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = _effectSize;

                ClickVFX vfx = go.AddComponent<ClickVFX>();
                go.SetActive(false);
                _pool[i] = vfx;
            }
        }

        private ClickVFX GetNextAvailable()
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                int idx = (_nextIndex + i) % _pool.Length;
                if (_pool[idx] != null && !_pool[idx].gameObject.activeInHierarchy)
                {
                    _nextIndex = (idx + 1) % _pool.Length;
                    return _pool[idx];
                }
            }
            return null;
        }
    }
}
