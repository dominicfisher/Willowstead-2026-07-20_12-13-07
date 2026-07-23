using UnityEngine;

namespace Willowstead.Player
{
    /// <summary>
    /// Smoothly follows a target (like the Player) in 2D space.
    /// Uses SmoothDamp to prevent camera jitter.
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

        private Vector3 _currentVelocity;

        private void LateUpdate()
        {
            if (_target == null) return;

            // Target position including the offset
            Vector3 targetPosition = _target.position + _offset;

            // Smoothly move the camera towards that target position
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, _smoothTime);
        }

        /// <summary>
        /// Manually set the camera target at runtime.
        /// </summary>
        public void SetTarget(Transform target)
        {
            _target = target;
        }
    }
}
