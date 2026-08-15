using UnityEngine;
using UnityEngine.EventSystems;

namespace Willowstead.World
{
    /// <summary>
    /// Snaps a visual selection box to the grid cell under the mouse cursor.
    /// Performs range checking relative to the player to show if the cell is interactable.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class GridSelector : MonoBehaviour
    {
        [Header("Targeting Settings")]
        [Tooltip("Maximum distance from the player where interactions are allowed.")]
        [SerializeField] private float _maxInteractionDistance = 7.5f;

        [Tooltip("The player transform used to measure interaction range.")]
        [SerializeField] private Transform _playerTransform;

        [Header("Visual Feedback Colors")]
        [SerializeField] private Color _inRangeColor = new Color(0.2f, 1f, 0.2f, 0.8f);
        [SerializeField] private Color _outOfRangeColor = new Color(1f, 0.2f, 0.2f, 0.5f);

        private LineRenderer _lineRenderer;
        private Camera _mainCamera;
        private Vector3Int _currentCell;
        private bool _isCellInRange;

        /// <summary>
        /// Exposes the currently targeted grid cell.
        /// </summary>
        public Vector3Int CurrentCell => _currentCell;

        /// <summary>
        /// Exposes whether the targeted cell is within the player's interaction range.
        /// </summary>
        public bool IsCellInRange => _isCellInRange;

        private void Start()
        {
            _mainCamera = Camera.main;
            
            // Auto-boost reach range for developer convenience if still using legacy default
            if (_maxInteractionDistance <= 2.5f)
            {
                _maxInteractionDistance = 7.5f;
            }
            
            if (_playerTransform == null)
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    _playerTransform = playerObj.transform;
                }
            }

            SetupLineRenderer();
        }

        private void SetupLineRenderer()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.positionCount = 5;
            _lineRenderer.useWorldSpace = false;
            _lineRenderer.loop = true;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;

            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

            UpdateLineRendererPoints();
        }

        private void UpdateLineRendererPoints()
        {
            if (_lineRenderer == null) return;

            Vector3 cellSize = Vector3.one;
            if (GridManager.Instance != null)
            {
                cellSize = GridManager.Instance.CellSize;
            }

            float halfX = cellSize.x * 0.5f;
            float halfY = cellSize.y * 0.5f;

            float width = 0.05f * cellSize.x;
            _lineRenderer.startWidth = width;
            _lineRenderer.endWidth = width;

            _lineRenderer.SetPosition(0, new Vector3(-halfX, -halfY, 0f));
            _lineRenderer.SetPosition(1, new Vector3(halfX, -halfY, 0f));
            _lineRenderer.SetPosition(2, new Vector3(halfX, halfY, 0f));
            _lineRenderer.SetPosition(3, new Vector3(-halfX, halfY, 0f));
            _lineRenderer.SetPosition(4, new Vector3(-halfX, -halfY, 0f));
        }

        private void Update()
        {
            // Block grid selector updates and hide box when mouse is over UI elements (Shop/Inventory)
            // Checks EventSystem raycast first, and uses direct RectTransform bounding box calculations as a bulletproof fallback.
            if (Player.UIResourceHelper.IsPointerOverAnyUI())
            {
                if (_lineRenderer != null) _lineRenderer.enabled = false;
                _isCellInRange = false;
                return;
            }

            if (_lineRenderer != null && !_lineRenderer.enabled)
            {
                _lineRenderer.enabled = true;
            }

            if (GridManager.Instance == null) return;

            UpdateLineRendererPoints();

            Vector3 mouseWorldPos = Vector3.zero;
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                Vector2 mouseScreenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
            }
            mouseWorldPos.z = 0f; // Lock to 2D plane

            _currentCell = GridManager.Instance.WorldToCell(mouseWorldPos);
            transform.position = GridManager.Instance.CellToWorldCenter(_currentCell);

            if (_playerTransform != null)
            {
                float distance = Vector2.Distance(_playerTransform.position, transform.position);
                _isCellInRange = distance <= _maxInteractionDistance;
            }
            else
            {
                _isCellInRange = true;
            }

            Color targetColor = _isCellInRange ? _inRangeColor : _outOfRangeColor;
            _lineRenderer.startColor = targetColor;
            _lineRenderer.endColor = targetColor;
        }

    }
}
