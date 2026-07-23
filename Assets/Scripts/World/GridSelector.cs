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
            
            // Try to find the player if not manually assigned in inspector
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
            _lineRenderer.useWorldSpace = false; // Draw relative to this GameObject's position
            _lineRenderer.loop = true;
            _lineRenderer.startWidth = 0.05f;
            _lineRenderer.endWidth = 0.05f;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;

            // Draw a 1x1 square border centered on the cell
            _lineRenderer.SetPosition(0, new Vector3(-0.5f, -0.5f, 0f));
            _lineRenderer.SetPosition(1, new Vector3(0.5f, -0.5f, 0f));
            _lineRenderer.SetPosition(2, new Vector3(0.5f, 0.5f, 0f));
            _lineRenderer.SetPosition(3, new Vector3(-0.5f, 0.5f, 0f));
            _lineRenderer.SetPosition(4, new Vector3(-0.5f, -0.5f, 0f));

            // Use the standard Sprites shader for clean coloring
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        private void Update()
        {
            // Block grid selector updates and hide box when mouse is over UI elements (Shop/Inventory)
            // Checks EventSystem raycast first, and uses direct RectTransform bounding box calculations as a bulletproof fallback.
            if ((EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) || IsMouseOverUIPanel())
            {
                if (_lineRenderer != null) _lineRenderer.enabled = false;
                _isCellInRange = false;
                return;
            }

            // Restore line renderer if it was disabled by UI hover
            if (_lineRenderer != null && !_lineRenderer.enabled)
            {
                _lineRenderer.enabled = true;
            }

            if (GridManager.Instance == null) return;

            // 1. Get mouse position in world coordinates
            Vector3 mouseWorldPos = Vector3.zero;
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                Vector2 mouseScreenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);
            }
            mouseWorldPos.z = 0f; // Lock to 2D plane

            // 2. Convert to cell coordinates and update selector position
            _currentCell = GridManager.Instance.WorldToCell(mouseWorldPos);
            transform.position = GridManager.Instance.CellToWorldCenter(_currentCell);

            // 3. Perform range check against player
            if (_playerTransform != null)
            {
                float distance = Vector2.Distance(_playerTransform.position, transform.position);
                _isCellInRange = distance <= _maxInteractionDistance;
            }
            else
            {
                // Fallback if player doesn't exist yet
                _isCellInRange = true;
            }

            // 4. Update the color of the outline
            Color targetColor = _isCellInRange ? _inRangeColor : _outOfRangeColor;
            _lineRenderer.startColor = targetColor;
            _lineRenderer.endColor = targetColor;
        }

        private bool IsMouseOverUIPanel()
        {
            if (UnityEngine.InputSystem.Mouse.current == null) return false;
            Vector2 mouseScreenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

            // Check ShopPanel bounds
            GameObject shopPanel = GameObject.Find("ShopPanel");
            if (shopPanel != null && shopPanel.activeInHierarchy)
            {
                RectTransform rect = shopPanel.GetComponent<RectTransform>();
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, mouseScreenPos, null))
                {
                    return true;
                }
            }

            // Check InventoryPanel bounds
            GameObject invPanel = GameObject.Find("InventoryPanel");
            if (invPanel != null && invPanel.activeInHierarchy)
            {
                RectTransform rect = invPanel.GetComponent<RectTransform>();
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, mouseScreenPos, null))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
