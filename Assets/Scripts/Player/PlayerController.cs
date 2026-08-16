using UnityEngine;
using Willowstead.World;

namespace Willowstead.Player
{
    /// <summary>
    /// Handles player movement, physics interactions, and updates the facing direction.
    /// Decoupled from direct inputs; listens to events from the InputReader ScriptableObject.
    ///
    /// SINGLETON DESIGN: The scene Player registers itself in Awake(). Nothing outside this
    /// class should ever create a Player object. EnsurePlayerInstance() is a last-resort
    /// fallback only; it will never run if the scene has a Player with this component.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(AudioSource))]
    public class PlayerController : MonoBehaviour
    {
        private static PlayerController _instance;

        /// <summary>Singleton hook used by SaveGameManager + dev console + UI.</summary>
        public static PlayerController Instance => _instance;

#if UNITY_EDITOR
        [ContextMenu("Setup Permanent Player Components")]
        private void SetupComponentsInEditor()
        {
            if (GetComponent<PlayerStats>() == null) gameObject.AddComponent<PlayerStats>();
            if (GetComponent<UI.PlayerStatusUI>() == null) gameObject.AddComponent<UI.PlayerStatusUI>();
            if (GetComponent<InventoryManager>() == null) gameObject.AddComponent<InventoryManager>();
            if (GetComponent<Farming.FarmingController>() == null) gameObject.AddComponent<Farming.FarmingController>();
            if (GetComponent<HotbarUI>() == null) gameObject.AddComponent<HotbarUI>();
            if (GetComponent<InventoryUI>() == null) gameObject.AddComponent<InventoryUI>();
            if (GetComponent<ShopUI>() == null) gameObject.AddComponent<ShopUI>();
            if (GetComponent<UI.CompassUI>() == null) gameObject.AddComponent<UI.CompassUI>();
            if (GetComponent<UI.FullMapUI>() == null) gameObject.AddComponent<UI.FullMapUI>();
            Debug.Log("[PlayerController] All player components attached to GameObject in Editor! Press Ctrl+S to save.");
        }
#endif

        /// <summary>
        /// True last-resort fallback: only spawns a Player if there is genuinely no
        /// PlayerController anywhere in the scene. Called by MainMenuUI and save system
        /// but will do nothing if the scene already has a Player registered.
        /// </summary>
        public static PlayerController EnsurePlayerInstance()
        {
            if (_instance != null) return _instance;

            PlayerController existing = FindAnyObjectByType<PlayerController>();
            if (existing != null)
            {
                _instance = existing;
                Debug.Log($"[PlayerBootstrap] Found existing scene PlayerController on '{existing.gameObject.name}'.");
                return _instance;
            }

            GameObject playerGo = null;
            try { playerGo = GameObject.FindWithTag("Player"); } catch {}

            if (playerGo == null)
            {
                GameObject[] allGos = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
                foreach (var go in allGos)
                {
                    if (go.name.IndexOf("player", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        playerGo = go;
                        Debug.Log($"[PlayerBootstrap] Found scene object by name: '{go.name}' — will attach PlayerController to it.");
                        break;
                    }
                }
            }

            if (playerGo == null)
            {
                var allGos = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
                var names = new System.Text.StringBuilder();
                names.AppendLine("[PlayerBootstrap] Scene search failed. All GameObjects found in scene:");
                foreach (var go in allGos)
                    names.AppendLine($"  - '{go.name}' (tag='{go.tag}', active={go.activeInHierarchy})");
                Debug.LogWarning(names.ToString());

                Debug.Log("[PlayerBootstrap] No Player GameObject found in scene — creating runtime fallback Player.");
                playerGo = new GameObject("Player");
                playerGo.transform.position = Vector3.zero;
                try { playerGo.tag = "Player"; } catch {}
            }

            // ── Ensure all required physics / audio components ───────────────────
            if (playerGo.GetComponent<Rigidbody2D>() == null)
            {
                var rb = playerGo.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            if (playerGo.GetComponent<CircleCollider2D>() == null)
            {
                var col = playerGo.AddComponent<CircleCollider2D>();
                col.radius = 0.4f;
            }

            if (playerGo.GetComponent<SpriteRenderer>() == null)
            {
                var sr = playerGo.AddComponent<SpriteRenderer>();
                sr.sprite = UIResourceHelper.GetCircleSprite();
                sr.color = new Color(0.95f, 0.85f, 0.55f, 1f);
                sr.sortingOrder = 50;
            }

            if (playerGo.GetComponent<AudioSource>() == null)
            {
                var audio = playerGo.AddComponent<AudioSource>();
                audio.playOnAwake = false;
            }

            if (playerGo.GetComponent<PlayerStats>() == null)
                playerGo.AddComponent<PlayerStats>();

            if (playerGo.GetComponent<UI.PlayerStatusUI>() == null)
                playerGo.AddComponent<UI.PlayerStatusUI>();

            if (playerGo.GetComponent<InventoryManager>() == null)
                playerGo.AddComponent<InventoryManager>();

            if (playerGo.GetComponent<Farming.FarmingController>() == null)
                playerGo.AddComponent<Farming.FarmingController>();

            if (playerGo.GetComponent<HotbarUI>() == null)
                playerGo.AddComponent<HotbarUI>();

            if (playerGo.GetComponent<InventoryUI>() == null)
                playerGo.AddComponent<InventoryUI>();

            if (playerGo.GetComponent<ShopUI>() == null)
                playerGo.AddComponent<ShopUI>();

            if (playerGo.GetComponent<UI.CompassUI>() == null)
                playerGo.AddComponent<UI.CompassUI>();

            if (playerGo.GetComponent<UI.FullMapUI>() == null)
                playerGo.AddComponent<UI.FullMapUI>();

            // ── Attach PlayerController last (its Awake sets _instance) ──────────
            PlayerController pc = playerGo.GetComponent<PlayerController>();
            if (pc == null) pc = playerGo.AddComponent<PlayerController>();

            // ── Auto-resolve InputReader ─────────────────────────────────────────
            if (pc._inputReader == null)
            {
                pc._inputReader = Resources.Load<Input.InputReader>("InputReader");
                if (pc._inputReader == null)
                {
                    var readers = Resources.FindObjectsOfTypeAll<Input.InputReader>();
                    if (readers != null && readers.Length > 0) pc._inputReader = readers[0];
                }
            }

            Debug.Log($"[PlayerBootstrap] Player fully bootstrapped on '{playerGo.name}' with all components.");
            return _instance;
        }


        [Header("References")]
        [Tooltip("The ScriptableObject input reader channels input events to this controller.")]
        [SerializeField] private Input.InputReader _inputReader;

        [Header("Movement Settings")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _sprintSpeedMultiplier = 1.6f;
        [SerializeField] private float _acceleration = 12f;
        [SerializeField] private float _deceleration = 16f;

        [Header("Footstep Audio")]
        [Tooltip("Audio clips played while walking on grass.")]
        [SerializeField] private AudioClip[] _grassFootstepSounds;
        [Tooltip("Audio clips played while walking on dirt or tilled soil.")]
        [SerializeField] private AudioClip[] _dirtFootstepSounds;
        [Tooltip("Seconds between footstep sounds while moving at normal speed.")]
        [SerializeField] private float _footstepInterval = 0.35f;
        [Tooltip("If true, footstep sounds are chosen based on the surface under the player.")]
        [SerializeField] private bool _useSurfaceFootsteps = true;
        [Tooltip("Volume for footstep sounds (0 = silent, 1 = full volume).")]
        [Range(0f, 1f)]
        [SerializeField] private float _footstepVolume = 0.7f;

        private Rigidbody2D _rb;
        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private AudioSource _audioSource;
        private Vector2 _moveInput;
        private Vector2 _currentVelocity;
        private bool _isSprinting;
        private Vector2 _lastMoveDirection = Vector2.down;
        private float _footstepTimer;

        /// <summary>Vector of player's current input direction or last movement direction.</summary>
        public Vector2 LastMoveDirection => _lastMoveDirection;
        public Vector2 MoveInput => _moveInput;

        /// <summary>
        /// Angle in degrees where 0 = East (+X), 90 = North (+Y), 180 = West (-X), 270 = South (-Y).
        /// </summary>
        public float FacingAngle
        {
            get
            {
                Vector2 dir = _moveInput.sqrMagnitude > 0.01f ? _moveInput : _lastMoveDirection;
                if (dir.sqrMagnitude < 0.001f) return 270f;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;
                return angle;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                Debug.Log($"[PlayerController] Scene Player registered: '{gameObject.name}'");
            }
            else if (_instance != this)
            {
                // A second PlayerController tried to register — remove the component only,
                // never destroy the whole GameObject.
                Debug.LogWarning($"[PlayerController] Ignoring duplicate PlayerController on '{gameObject.name}' — scene Player already registered.");
                Destroy(this);
                return;
            }


            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();

            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = gameObject.AddComponent<Animator>();
            }
            _animator.enabled = true;

#if UNITY_EDITOR
            if (_animator.runtimeAnimatorController == null || _animator.runtimeAnimatorController.name != "PlayerAnimatorController")
            {
                var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Scripts/Player/PlayerAnimatorController.controller");
                if (ctrl != null) _animator.runtimeAnimatorController = ctrl;
            }
#endif

            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

#if UNITY_EDITOR
            if (_spriteRenderer.sprite == null || _spriteRenderer.sprite.name.StartsWith("Player_"))
            {
                var fullSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/PlayerFrames/Idle/South/frame_0.png");
                if (fullSprite != null) _spriteRenderer.sprite = fullSprite;
            }
#endif

            if (UI.MainMenuUI.Instance != null && !UI.MainMenuUI.HasGameStarted)
            {
                _spriteRenderer.enabled = false;
            }
            else
            {
                _spriteRenderer.enabled = true;
            }
            _spriteRenderer.color = Color.white;
            _spriteRenderer.sortingOrder = 50;

            if (transform.localScale.sqrMagnitude < 0.01f)
            {
                transform.localScale = Vector3.one;
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
            }

            if (GetComponent<PlayerNameplate>() == null)
            {
                gameObject.AddComponent<PlayerNameplate>();
            }

            // - No gravity because it's top-down.
            // - Freeze rotation so the player doesn't spin when colliding with walls.
            // - Enable interpolation to smooth out physics updates at high framerates
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Footsteps should start almost immediately when the player begins moving.
            _footstepTimer = _footstepInterval * 0.5f;
        }

        private void LateUpdate()
        {
            if (_spriteRenderer != null)
            {
                if (UI.MainMenuUI.Instance != null && UI.MainMenuUI.Instance.IsVisible && !UI.MainMenuUI.HasGameStarted)
                {
                    if (_spriteRenderer.enabled) _spriteRenderer.enabled = false;
                }
                else
                {
                    if (!_spriteRenderer.enabled) _spriteRenderer.enabled = true;
                }

                // Dynamic Y-sorting: objects lower on screen render in front, clamped so player is always above terrain
                _spriteRenderer.sortingOrder = Mathf.Max(10, Mathf.RoundToInt(-transform.position.y * 100) + 10);
            }
        }

        private void OnEnable()
        {
            if (_inputReader == null)
            {
                _inputReader = Resources.Load<Input.InputReader>("InputReader");
                if (_inputReader == null)
                {
                    var readers = Resources.FindObjectsOfTypeAll<Input.InputReader>();
                    if (readers != null && readers.Length > 0) _inputReader = readers[0];
                }
            }

            if (_inputReader != null)
            {
                _inputReader.EnableGameplayInput();
                _inputReader.MoveEvent += OnMoveInput;
                _inputReader.SprintEvent += OnSprintStart;
                _inputReader.SprintCanceledEvent += OnSprintEnd;
            }
            else
            {
                Debug.LogWarning($"[PlayerController] InputReader reference is missing!", this);
            }
        }

        /// <summary>Snaps the player to the given world position via the
        /// Rigidbody2D so physics picks it up the same frame.</summary>
        public void RestorePosition(Vector3 worldPos)
        {
            if (_rb != null) _rb.position = new Vector2(worldPos.x, worldPos.y);
            else transform.position = worldPos;
        }

        private void OnDisable()
        {
            if (_inputReader != null)
            {
                _inputReader.MoveEvent -= OnMoveInput;
                _inputReader.SprintEvent -= OnSprintStart;
                _inputReader.SprintCanceledEvent -= OnSprintEnd;
                _inputReader.DisableGameplayInput();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void FixedUpdate()
        {
            if (_inputReader != null && !Input.InputReader.BlockGameplayInput)
            {
                Vector2 liveInput = _inputReader.GetMoveInput();
                if (liveInput.sqrMagnitude > 0.001f || _moveInput.sqrMagnitude < 0.001f)
                {
                    _moveInput = liveInput.magnitude > 0.01f ? liveInput.normalized : Vector2.zero;
                }
            }
            else if (Input.InputReader.BlockGameplayInput)
            {
                _moveInput = Vector2.zero;
            }
            MovePlayer();
        }

        private void MovePlayer()
        {
            bool wantsToSprint = _isSprinting && _moveInput.sqrMagnitude > 0.01f;
            bool canSprint = false;

            if (wantsToSprint)
            {
                if (PlayerStats.Instance != null)
                {
                    canSprint = PlayerStats.Instance.ConsumeSprintStamina(Time.fixedDeltaTime);
                    if (!canSprint)
                    {
                        _isSprinting = false;
                    }
                }
                else
                {
                    canSprint = true;
                }
            }

            float targetSpeed = _moveSpeed * (canSprint ? _sprintSpeedMultiplier : 1f);
            Vector2 targetVelocity = _moveInput * targetSpeed;

            float lerpRate = _moveInput.magnitude > 0.01f ? _acceleration : _deceleration;
            _currentVelocity = Vector2.MoveTowards(_currentVelocity, targetVelocity, lerpRate * Time.fixedDeltaTime);

            _rb.linearVelocity = _currentVelocity;

            UpdateAnimator();

            UpdateFootstepAudio();
        }

        private void OnMoveInput(Vector2 direction)
        {
            if (Input.InputReader.BlockGameplayInput) return;
            _moveInput = direction.magnitude > 0.01f ? direction.normalized : Vector2.zero;
        }

        private void OnSprintStart()
        {
            _isSprinting = true;
        }

        private void OnSprintEnd()
        {
            _isSprinting = false;
        }

        private void UpdateFootstepAudio()
        {
            bool isMoving = _moveInput.magnitude > 0.01f;
            if (!isMoving)
            {
                _footstepTimer = _footstepInterval * 0.5f;
                return;
            }

            _footstepTimer -= Time.fixedDeltaTime;

            if (_footstepTimer <= 0f)
            {
                float speedMultiplier = _isSprinting ? _sprintSpeedMultiplier : 1f;
                _footstepTimer = _footstepInterval / speedMultiplier;
                PlayFootstepSound();
            }
        }

        private void PlayFootstepSound()
        {
            AudioClip[] clips;

            if (_useSurfaceFootsteps)
            {
                clips = GetCurrentSurface() == SurfaceType.Grass
                    ? _grassFootstepSounds
                    : _dirtFootstepSounds;
            }
            else
            {
                clips = _dirtFootstepSounds;
            }

            if (clips == null || clips.Length == 0 || _audioSource == null) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null)
            {
                _audioSource.PlayOneShot(clip, _footstepVolume);
            }
        }

        private SurfaceType GetCurrentSurface()
        {
            Vector3Int cell;
            if (GridManager.Instance != null)
            {
                cell = GridManager.Instance.WorldToCell(transform.position);

                // Tilled/watered soil counts as dirt.
                if (GridManager.Instance.IsCellTilled(cell))
                    return SurfaceType.Dirt;
            }
            else
            {
                cell = new Vector3Int(
                    Mathf.FloorToInt(transform.position.x),
                    Mathf.FloorToInt(transform.position.y),
                    0);
            }

            if (ProceduralGridGenerator.Instance != null &&
                ProceduralGridGenerator.Instance.IsGrassAt(cell.x, cell.y))
            {
                return SurfaceType.Grass;
            }

            return SurfaceType.Dirt;
        }

        private enum SurfaceType
        {
            Grass,
            Dirt
        }

        private void UpdateAnimator()
        {
            if (_animator == null) return;

            bool isMoving = _moveInput.magnitude > 0.01f;
            _animator.SetBool("IsMoving", isMoving);

            if (isMoving)
            {
                _lastMoveDirection = _moveInput;
                _animator.SetFloat("MoveX", _moveInput.x);
                _animator.SetFloat("MoveY", _moveInput.y);
            }

            _animator.SetFloat("LastMoveX", _lastMoveDirection.x);
            _animator.SetFloat("LastMoveY", _lastMoveDirection.y);
        }
    }
}
