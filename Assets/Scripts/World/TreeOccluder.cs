using UnityEngine;

namespace Willowstead.World
{
    /// <summary>
    /// Fades tree sprites transparent when the player walks "behind" them
    /// so the character remains visible. Designed for top-down 2D with Y-sort.
    /// Attach to a GameObject that has a SpriteRenderer. Tall sprites should use
    /// bottom-center pivot (0.5, 0) so occlusion checks use the trunk/base.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class TreeOccluder : MonoBehaviour
    {
        [SerializeField] private float _occludedAlpha = 0.35f;
        [SerializeField] private float _fadeSpeed = 6f;
        [SerializeField] private float _horizontalFactor = 0.55f; // Width fraction for occlusion band
        [SerializeField] private float _yOffset = 0.12f; // Small offset above base before occluding

        private SpriteRenderer _sr;
        private Transform _player;
        private float _currentTargetAlpha = 1f;
        private float _findCooldown;

        private void Awake()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            TryFindPlayerImmediate();
        }

        public void InitializeAuto()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            TryFindPlayerImmediate();
        }

        private void TryFindPlayerImmediate()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Player");
            if (player == null)
            {
                var controller = FindAnyObjectByType<Player.PlayerController>();
                if (controller != null) player = controller.gameObject;
            }
            if (player != null) _player = player.transform;
        }

        private void Update()
        {
            if (_sr == null) return;

            if (_player == null)
            {
                _findCooldown -= Time.deltaTime;
                if (_findCooldown <= 0f)
                {
                    TryFindPlayerImmediate();
                    _findCooldown = 1.0f;
                }
                return;
            }

            bool occluded = ShouldOcclude();
            _currentTargetAlpha = occluded ? _occludedAlpha : 1f;

            Color c = _sr.color;
            float nextA = Mathf.MoveTowards(c.a, _currentTargetAlpha, _fadeSpeed * Time.deltaTime);
            if (!Mathf.Approximately(nextA, c.a))
            {
                c.a = nextA;
                _sr.color = c;
            }
        }

        private bool ShouldOcclude()
        {
            // horizontally within a band of the trunk AND vertically within a band
            // of the trunk. Comparing against transform.position (the recommended
            // would trigger for trees that were nowhere near the player.
            float px = _player.position.x;
            float py = _player.position.y;

            if (_sr == null || _sr.sprite == null)
                return false;

            Bounds b = _sr.bounds;
            float trunkY = transform.position.y;
            float trunkX = transform.position.x;

            float halfWidth = Mathf.Max(0.05f, b.size.x * 0.5f);
            float halfHeight = Mathf.Max(0.05f, b.size.y * 0.5f);
            float verticalBand = Mathf.Max(_yOffset, halfHeight * _horizontalFactor);
            float horizontalBand = Mathf.Max(0.05f, halfWidth * _horizontalFactor);

            return Mathf.Abs(py - trunkY) <= verticalBand
                && Mathf.Abs(px - trunkX) <= horizontalBand;
        }
    }
}
