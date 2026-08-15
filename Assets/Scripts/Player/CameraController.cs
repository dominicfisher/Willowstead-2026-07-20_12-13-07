using UnityEngine;
using UnityEngine.InputSystem;

namespace Willowstead.Player
{
    /// <summary>
    /// Smoothly follows a target (like the Player) in 2D space.
    /// Uses SmoothDamp to prevent camera jitter.
    /// Also supports mouse-wheel orthographic zoom (clamped + smoothed).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("The Transform the camera should follow.")]
        [SerializeField] private Transform _target;

        [Header("Follow Settings")]
        [Tooltip("How smoothly the camera catches up to the target. Lower values are faster.")]
        [SerializeField] private float _smoothTime = 0.2f;

        [Tooltip("Offset of the camera relative to the target (usually keeping Z negative).")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);

        [Header("Zoom Settings")]
        [Tooltip("Smallest orthographic size allowed (closest zoom).")]
        [SerializeField] private float _minOrthographicSize = 2f;

        [Tooltip("Largest orthographic size allowed (farthest zoom out).")]
        [SerializeField] private float _maxOrthographicSize = 20f;

        [Tooltip("How much the orthographic size changes per unit of mouse-wheel delta. Tween this in the Inspector to taste.")]
        [SerializeField] private float _zoomSensitivity = 2.5f;

        [Tooltip("How smoothly the camera reaches the target zoom level. Higher = snappier.")]
        [SerializeField] private float _zoomSmoothing = 12f;

        [Tooltip("If true, also accept the Q/E keys as zoom shortcuts when the mouse wheel is unavailable (e.g. gamepad-only).")]
        [SerializeField] private bool _allowKeyboardZoom = false;

        private Camera _camera;
        private Vector3 _currentVelocity;
        private float _targetOrthographicSize;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                // dragging a custom orthographic size in the scene stays as-is.
                _targetOrthographicSize = _camera.orthographicSize;
            }
        }

        private void Update()
        {
            HandleZoomInput();
        }

        private Vector3 _menuPanPosition;
        private bool _isMenuPanning;

        private void LateUpdate()
        {
            if (_camera == null) return;

            bool inMainMenu = UI.MainMenuUI.Instance != null && UI.MainMenuUI.Instance.IsVisible;

            if (inMainMenu)
            {
                if (!_isMenuPanning)
                {
                    _menuPanPosition = transform.position;
                    _isMenuPanning = true;
                }

                // Slowly drift the camera horizontally and gently vertically to showcase terrain
                _menuPanPosition += new Vector3(1.2f * Time.unscaledDeltaTime, Mathf.Sin(Time.unscaledTime * 0.35f) * 0.4f * Time.unscaledDeltaTime, 0f);
                _menuPanPosition.z = _offset.z;
                transform.position = _menuPanPosition;
            }
            else
            {
                _isMenuPanning = false;

                if (_target == null && PlayerController.Instance != null)
                {
                    _target = PlayerController.Instance.transform;
                }

                if (_target != null)
                {
                    Vector3 targetPosition = _target.position + _offset;
                    transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, _smoothTime);
                }
            }

            _camera.orthographicSize = Mathf.Lerp(
                _camera.orthographicSize,
                _targetOrthographicSize,
                _zoomSmoothing * Time.deltaTime);
        }

        private void HandleZoomInput()
        {
            float scrollDelta = 0f;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            // Require Left Alt (or Right Alt) to be held while scrolling to zoom the camera,
            // keeping normal mouse scroll free for hotbar slot selection.
            bool isAltHeld = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);

            if (isAltHeld && mouse != null)
            {
                scrollDelta += mouse.scroll.ReadValue().y;
            }

            if (_allowKeyboardZoom && isAltHeld && Mathf.Approximately(scrollDelta, 0f))
            {
                if (keyboard != null)
                {
                    if (keyboard.eKey.isPressed) scrollDelta += 1f;
                    if (keyboard.qKey.isPressed) scrollDelta -= 1f;
                }
            }

            if (Mathf.Approximately(scrollDelta, 0f)) return;

            // Mouse wheel up (positive y) zooms IN (smaller ortho size), mouse wheel down zooms OUT.
            float newSize = _targetOrthographicSize - scrollDelta * _zoomSensitivity;
            _targetOrthographicSize = Mathf.Clamp(newSize, _minOrthographicSize, _maxOrthographicSize);
        }

        /// <summary>
        /// Manually set the camera target at runtime.
        /// </summary>
        public void SetTarget(Transform target)
        {
            _target = target;
        }

        /// <summary>
        /// Reset the zoom to the currently rendered size. Useful for "reset view" hotkeys.
        /// </summary>
        public void ResetZoom()
        {
            if (_camera != null)
            {
                _targetOrthographicSize = _camera.orthographicSize;
            }
        }
    }
}
