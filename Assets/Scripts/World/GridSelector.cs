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
        [SerializeField] private float _maxInteractionDistance = 1.85f;

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
            
            if (_maxInteractionDistance > 2.0f)
            {
                _maxInteractionDistance = 1.85f;
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

        private HoverOutline _activeHovered;

        private void Update()
        {
            // Block grid selector updates and hide box when mouse is over UI elements (Shop/Inventory)
            if (Player.UIResourceHelper.IsPointerOverAnyUI())
            {
                if (_lineRenderer != null) _lineRenderer.enabled = false;
                if (_activeHovered != null)
                {
                    _activeHovered.SetHovered(false);
                    _activeHovered = null;
                }
                _isCellInRange = false;
                return;
            }

            Vector3 mouseWorldPos = Vector3.zero;
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                Vector2 mouseScreenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                if (_mainCamera != null)
                {
                    mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
                }
            }
            mouseWorldPos.z = 0f; // Lock to 2D plane

            // Full-sprite visual bounds check for trees or objects with HoverOutline
            HoverOutline hoveredOutline = null;
            Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorldPos);
            for (int i = 0; i < hits.Length; i++)
            {
                var outline = hits[i].GetComponent<HoverOutline>() ?? hits[i].GetComponentInParent<HoverOutline>();
                if (outline != null)
                {
                    hoveredOutline = outline;
                    break;
                }
            }

            // If not directly over the foot collider, check if cursor is anywhere within the full sprite bounds (e.g. tree foliage/top)
            if (hoveredOutline == null)
            {
                Collider2D[] areaHits = Physics2D.OverlapCircleAll(mouseWorldPos, 3.5f);
                for (int i = 0; i < areaHits.Length; i++)
                {
                    var outline = areaHits[i].GetComponent<HoverOutline>() ?? areaHits[i].GetComponentInParent<HoverOutline>();
                    if (outline != null && outline.ContainsWorldPoint(mouseWorldPos))
                    {
                        hoveredOutline = outline;
                        break;
                    }
                }
            }

            if (hoveredOutline != _activeHovered)
            {
                if (_activeHovered != null) _activeHovered.SetHovered(false);
                _activeHovered = hoveredOutline;
                if (_activeHovered != null) _activeHovered.SetHovered(true);
            }

            if (GridManager.Instance == null) return;

            UpdateLineRendererPoints();

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

            // When hovering over a tree or object, outline the object and hide the grid line square
            if (_activeHovered != null)
            {
                if (_lineRenderer != null) _lineRenderer.enabled = false;
                return;
            }

            if (_lineRenderer != null && !_lineRenderer.enabled)
            {
                _lineRenderer.enabled = true;
            }

            Color targetColor = _isCellInRange ? _inRangeColor : _outOfRangeColor;
            _lineRenderer.startColor = targetColor;
            _lineRenderer.endColor = targetColor;
        }

    }
}
