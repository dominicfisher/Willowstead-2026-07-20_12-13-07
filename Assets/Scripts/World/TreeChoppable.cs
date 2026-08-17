using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Willowstead.World
{
    /// <summary>
    /// Adds chopping/shake-and-fell behavior to a procedurally spawned tree. Trees
    /// take a randomized number of chops to fall, then drop a randomized stack of
    /// logs into the player's inventory. Each chop plays a brief Z-axis tilt shake
    /// on the local rotation, which preserves the deterministic world position used
    /// by ProceduralGridGenerator and the color logic in TreeOccluder.
    ///
    /// Cross-chunk persistence: a static HashSet remembers which tile-grid coords
    /// have already been felled, so chunks that re-load after the player walks far
    /// away and back don't re-spawn those trees.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class TreeChoppable : MonoBehaviour
    {
        [Header("Chop Settings")]
        [Tooltip("Min number of chops required for a fresh tree to fall (inclusive).")]
        [SerializeField, Range(1, 8)] private int _minChopsRequired = 2;
        [Tooltip("Max number of chops required for a fresh tree to fall (inclusive).")]
        [SerializeField, Range(1, 8)] private int _maxChopsRequired = 4;
        [Tooltip("Min logs a fallen tree drops.")]
        [SerializeField, Range(1, 12)] private int _minLogsDropped = 2;
        [Tooltip("Max logs a fallen tree drops.")]
        [SerializeField, Range(1, 12)] private int _maxLogsDropped = 5;
        [Tooltip("Inventory item name for the dropped logs.")]
        [SerializeField] private string _logItemName = "Log";

        [Header("Shake Animation")]
        [Tooltip("Seconds of shake applied per chop.")]
        [SerializeField, Range(0.05f, 1.0f)] private float _shakeDuration = 0.32f;
        [Tooltip("Peak Z-axis tilt in degrees per shake.")]
        [SerializeField, Range(0.5f, 12f)] private float _shakeAngle = 4.0f;
        [Tooltip("Shake oscillation frequency (Hz). Higher feels shorter/snappier.")]
        [SerializeField, Range(3f, 25f)] private float _shakeFrequency = 18f;

        // World-grid coord of this tree's tile. Set by ProceduralGridGenerator on spawn.
        private Vector2Int _tileGridCoord;

        private SpriteRenderer _sr;
        private BoxCollider2D _collider;
        private Quaternion _originalRotation;
        private int _chopsRemaining;
        private bool _felled;
        private Coroutine _activeShake;

        // Cross-chunk persistence: felled tiles stay empty when chunks re-load.
        private static readonly HashSet<Vector2Int> s_felledTiles = new HashSet<Vector2Int>();

        public static bool IsTileFelled(Vector2Int tileGridCoord) => s_felledTiles.Contains(tileGridCoord);

        /// <summary>Read every felled tile back out for serialization.</summary>
        public static List<Willowstead.Persistence.Vector2IntRecord> CaptureFelledTiles()
        {
            var list = new List<Willowstead.Persistence.Vector2IntRecord>();
            foreach (var tile in s_felledTiles)
                list.Add(new Willowstead.Persistence.Vector2IntRecord(tile));
            return list;
        }

        /// <summary>Add a batch of felled tiles from a save. Idempotent — duplicates are harmless.</summary>
        public static void RestoreFelledTiles(IEnumerable<Willowstead.Persistence.Vector2IntRecord> records)
        {
            if (records == null) return;
            foreach (var r in records)
            {
                if (r == null) continue;
                s_felledTiles.Add(r.ToVector2Int());
            }
        }

        /// <summary>
        /// Wipes the cross-chunk felled-tile memory. Called by
        /// ProceduralGridGenerator.Regenerate() on a world-seed change so a tile
        /// the player cut under the previous seed isn't still marked as empty
        /// under the new seed (otherwise a tree at the same coord in the new
        /// world would be silently skipped).
        /// </summary>
        public static void ResetFelledTiles() => s_felledTiles.Clear();

        /// <summary>
        /// Called by ProceduralGridGenerator after the component is added in SpawnTree().
        /// </summary>
        public void Initialize(Vector2Int tileGridCoord)
        {
            _tileGridCoord = tileGridCoord;
        }

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _collider = GetComponent<BoxCollider2D>();
            _originalRotation = transform.localRotation;
            _chopsRemaining = Random.Range(_minChopsRequired, _maxChopsRequired + 1);
        }

        /// <summary>
        /// Apply one chop. No-ops if the tree is already felled. Triggers a shake each
        /// call. On the final chop, the tree yields a random stack of logs and is
        /// removed from the world.
        /// </summary>
        public void Chop()
        {
            if (_felled) return;

            StartShake();

            if (Player.SkillsManager.Instance != null)
            {
                Player.SkillsManager.Instance.AddXP(Player.SkillType.Woodcutting, 5);
            }

            _chopsRemaining--;
            if (_chopsRemaining > 0) return;

            Fell();
        }

        private void StartShake()
        {
            if (!isActiveAndEnabled) return;
            if (_activeShake != null) StopCoroutine(_activeShake);
            _activeShake = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            float t = 0f;
            while (t < _shakeDuration)
            {
                t += Time.deltaTime;
                float phase = t * _shakeFrequency * 2f * Mathf.PI;
                // settles naturally instead of chopping the rotation back at the last frame.
                float envelope = 1f - (t / _shakeDuration);
                float angle = Mathf.Sin(phase) * _shakeAngle * envelope;
                transform.localRotation = _originalRotation * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }
            transform.localRotation = _originalRotation;
            _activeShake = null;
        }

        private void Fell()
        {
            _felled = true;
            s_felledTiles.Add(_tileGridCoord);

            if (Player.SkillsManager.Instance != null)
            {
                Player.SkillsManager.Instance.AddXP(Player.SkillType.Woodcutting, 25);
            }

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.ReportProgress(ObjectiveId.ChopTree, 1);
            }

            int logs = Random.Range(_minLogsDropped, _maxLogsDropped + 1);

            // InventoryManager.AddItem handles slot placement + ItemNotificationManager pickup cue.
            Player.InventoryManager inv = Player.InventoryManager.Instance
                ?? Object.FindAnyObjectByType<Player.InventoryManager>();
            if (inv != null)
            {
                inv.AddItem(_logItemName, logs);
            }
#if UNITY_EDITOR
            else
            {
                Debug.LogWarning("[TreeChoppable] InventoryManager missing; logs cannot be added.", this);
            }
#endif

            // Disable interaction so the player can't keep chopping while the FX plays.
            if (_collider != null) _collider.enabled = false;

            // Disable the occluder so the tree no longer fades the player.
            var occluder = GetComponent<TreeOccluder>();
            if (occluder != null) occluder.enabled = false;

            // ought to commit the change visually; the persistence layer keeps it gone
            // across reloads.
            if (_sr != null) _sr.enabled = false;
            Destroy(gameObject);
        }

        /// <summary>
        /// Finds the nearest non-felled TreeChoppable within <paramref name="radius"/>
        /// world units of <paramref name="worldPos"/>. Checks both physics colliders and visual sprite bounds.
        /// Returns null when none are in range.
        /// </summary>
        public static TreeChoppable FindNearest(Vector2 worldPos, float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, radius);
            TreeChoppable best = null;
            float bestSqr = radius * radius;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;
                TreeChoppable chop = hit.GetComponent<TreeChoppable>() ?? hit.GetComponentInParent<TreeChoppable>();
                if (chop == null || chop._felled) continue;
                float sqr = ((Vector2)chop.transform.position - worldPos).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = chop;
                }
            }

            // Fallback: check active TreeChoppable objects directly within bounds or generous radius
            if (best == null)
            {
                var allChoppables = Object.FindObjectsByType<TreeChoppable>(FindObjectsSortMode.None);
                for (int i = 0; i < allChoppables.Length; i++)
                {
                    var tree = allChoppables[i];
                    if (tree == null || tree._felled) continue;
                    if (tree._sr != null && tree._sr.bounds.Contains(worldPos))
                    {
                        return tree;
                    }
                    float sqr = ((Vector2)tree.transform.position - worldPos).sqrMagnitude;
                    if (sqr <= (radius * 1.5f) * (radius * 1.5f) && sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = tree;
                    }
                }
            }

            return best;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.7f);
            Gizmos.DrawWireCube(new Vector3(_tileGridCoord.x + 0.5f, _tileGridCoord.y + 0.5f, 0f), Vector3.one);
        }
#endif
    }
}
