using UnityEngine;

namespace Willowstead.World
{
    /// <summary>
    /// Attach to any world object or tree to enable a smart pixel-art outline/glow when hovered by the mouse cursor.
    /// When hovered, GridSelector hides the tile box and highlights the entity.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class HoverOutline : MonoBehaviour
    {
        [SerializeField] private Color _hoverTint = new Color(1.22f, 1.22f, 1.15f, 1f); // Subtle bright glow
        [SerializeField] private Color _hoverOutlineColor = new Color(1f, 1f, 1f, 0.95f);

        private SpriteRenderer _sr;
        private Color _originalColor;
        private bool _isHovered = false;

        private GameObject _outlineGo;
        private SpriteRenderer _outlineSr;

        public static HoverOutline CurrentHovered { get; private set; }

        public bool ContainsWorldPoint(Vector2 worldPos)
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null || _sr.sprite == null) return false;
            return _sr.bounds.Contains(worldPos);
        }

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null)
            {
                _originalColor = _sr.color;
            }
            CreateOutlineChild();
        }

        private void CreateOutlineChild()
        {
            if (_outlineGo != null || _sr == null || _sr.sprite == null) return;

            _outlineGo = new GameObject("OutlineGlow");
            _outlineGo.transform.SetParent(transform, false);
            _outlineGo.transform.localPosition = Vector3.zero;
            _outlineGo.transform.localScale = Vector3.one * 1.05f;

            _outlineSr = _outlineGo.AddComponent<SpriteRenderer>();
            _outlineSr.sprite = _sr.sprite;
            _outlineSr.color = new Color(1f, 1f, 1f, 0.65f);
            _outlineSr.sortingLayerID = _sr.sortingLayerID;
            _outlineSr.sortingOrder = _sr.sortingOrder - 1;
            _outlineSr.enabled = false;
        }

        public void SetHovered(bool hovered)
        {
            if (_isHovered == hovered) return;
            _isHovered = hovered;

            if (_sr == null) _sr = GetComponent<SpriteRenderer>();

            if (hovered)
            {
                CurrentHovered = this;
                if (_outlineSr == null) CreateOutlineChild();
                if (_outlineSr != null)
                {
                    _outlineSr.sprite = _sr.sprite;
                    _outlineSr.sortingOrder = _sr.sortingOrder - 1;
                    _outlineSr.enabled = true;
                }
                if (_sr != null)
                {
                    _sr.color = _hoverTint;
                }
            }
            else
            {
                if (CurrentHovered == this) CurrentHovered = null;
                if (_outlineSr != null) _outlineSr.enabled = false;
                if (_sr != null)
                {
                    _sr.color = _originalColor;
                }
            }
        }

        private void OnDestroy()
        {
            if (CurrentHovered == this) CurrentHovered = null;
            if (_outlineGo != null) Destroy(_outlineGo);
        }

        private void OnDisable()
        {
            SetHovered(false);
        }
    }
}
