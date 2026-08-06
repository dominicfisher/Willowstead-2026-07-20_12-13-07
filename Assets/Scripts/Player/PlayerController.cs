using UnityEngine;
using Willowstead.World;

namespace Willowstead.Player
{
    /// <summary>
    /// Handles player movement, physics interactions, and updates the facing direction.
    /// Decoupled from direct inputs; listens to events from the InputReader ScriptableObject.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(AudioSource))]
    public class PlayerController : MonoBehaviour
    {
        /// <summary>Singleton hook used by SaveGameManager + dev console + UI.</summary>
        public static PlayerController Instance { get; private set; }


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
        private Vector2 _lastMoveDirection = Vector2.down; // Default to facing down
        private float _footstepTimer;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }
            _rb = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            // Ensure an AudioSource exists for footstep sounds. If the scene already
            // has one, use it; otherwise create it at runtime so footstep audio works
            // without manual scene setup.
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
            }

            // Basic Rigidbody2D configurations for top-down games:
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
                // Dynamic Y-sorting: objects lower on the screen (more negative Y) render in front of objects higher up
                _spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100);
            }
        }

        private void OnEnable()
        {
            if (_inputReader != null)
            {
                _inputReader.EnableGameplayInput(); // Force initialization to bypass Unity Editor domain reload bugs
                _inputReader.MoveEvent += OnMoveInput;
                _inputReader.SprintEvent += OnSprintStart;
                _inputReader.SprintCanceledEvent += OnSprintEnd;
            }
            else
            {
                Debug.LogWarning($"[PlayerController] InputReader reference is missing!", this);
            }
        }

        // ─── Save / load hooks ─────────────────────────────────────────
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
            // Calculate target velocity based on input and settings
            float targetSpeed = _moveSpeed * (_isSprinting ? _sprintSpeedMultiplier : 1f);
            Vector2 targetVelocity = _moveInput * targetSpeed;

            // Apply smooth acceleration and deceleration
            float lerpRate = _moveInput.magnitude > 0.01f ? _acceleration : _deceleration;
            _currentVelocity = Vector2.MoveTowards(_currentVelocity, targetVelocity, lerpRate * Time.fixedDeltaTime);

            // Set the rigidbody velocity
            _rb.linearVelocity = _currentVelocity;

            // Update Animator parameters
            UpdateAnimator();

            // Play footstep sounds while moving
            UpdateFootstepAudio();
        }

        private void OnMoveInput(Vector2 direction)
        {
            if (Input.InputReader.BlockGameplayInput) return;
            // Normalize direction to prevent moving faster diagonally
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
                // Reset so the next movement triggers a step quickly.
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
