using UnityEngine;

namespace Willowstead.Player
{
    /// <summary>
    /// Handles player movement, physics interactions, and updates the facing direction.
    /// Decoupled from direct inputs; listens to events from the InputReader ScriptableObject.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The ScriptableObject input reader channels input events to this controller.")]
        [SerializeField] private Input.InputReader _inputReader;

        [Header("Movement Settings")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _sprintSpeedMultiplier = 1.6f;
        [SerializeField] private float _acceleration = 12f;
        [SerializeField] private float _deceleration = 16f;

        private Rigidbody2D _rb;
        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private Vector2 _moveInput;
        private Vector2 _currentVelocity;
        private bool _isSprinting;
        private Vector2 _lastMoveDirection = Vector2.down; // Default to facing down

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            
            // Basic Rigidbody2D configurations for top-down games:
            // - No gravity because it's top-down.
            // - Freeze rotation so the player doesn't spin when colliding with walls.
            // - Enable interpolation to smooth out physics updates at high framerates
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            Debug.Log($"[PlayerController] Awake: Rigidbody2D found = {_rb != null}, Animator found = {_animator != null}", this);
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
                Debug.Log($"[PlayerController] OnEnable: Subscribing to InputReader '{_inputReader.name}'", this);
                _inputReader.MoveEvent += OnMoveInput;
                _inputReader.SprintEvent += OnSprintStart;
                _inputReader.SprintCanceledEvent += OnSprintEnd;
            }
            else
            {
                Debug.LogWarning($"[PlayerController] InputReader reference is missing!", this);
            }
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
        }

        private void OnMoveInput(Vector2 direction)
        {
            Debug.Log($"[PlayerController] Received direction event: {direction}", this);
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
