using UnityEngine;
using UnityEngine.Tilemaps;

namespace Willowstead.World
{
    /// <summary>
    /// Auto-tiling tile for plowed farmland that reads moisture level from GridManager
    /// and selects the correct connecting sprite from the PlowedDirt_tiles sheet.
    ///
    /// SPRITE SHEET LAYOUT (PlowedDirt_tiles.png — 12 cols × 4 rows = 48 sprites):
    ///
    ///   Blue  block (cols 0–3,  indices  0–3 / 12–15 / 24–27 / 36–39): DRY
    ///   Green block (cols 4–7,  indices  4–7 / 16–19 / 28–31 / 40–43): MOIST (day after watering)
    ///   Yellow block (cols 8–11, indices  8–11 / 20–23 / 32–35 / 44–47): WET  (freshly watered)
    ///
    ///   Within each 4×4 block:
    ///     Row 0 = top cap    (no N neighbour, has S neighbour)
    ///     Row 1 = middle     (N and S neighbours)
    ///     Row 2 = bottom cap (has N neighbour, no S neighbour)
    ///     Row 3 = isolated   (no N or S neighbours)
    ///
    ///     Col +0 = isolated horizontally (no W, no E)
    ///     Col +1 = left edge             (no W, has E)
    ///     Col +2 = centre                (has W and E)
    ///     Col +3 = right edge            (has W, no E)
    ///
    ///   Formula: spriteIndex = row * 12 + baseCol + colOffset
    ///            where baseCol = moistureLevel * 4   (0, 4 or 8)
    /// </summary>
    [CreateAssetMenu(fileName = "PlowedDirtAutoTile", menuName = "Willowstead/Plowed Dirt Auto Tile")]
    public class PlowedDirtAutoTile : TileBase
    {
        [Header("Sprite Sheet — All 48 sprites from PlowedDirt_tiles.png")]
        [Tooltip("Assign all 48 sprites here (indices 0–47 matching the sprite names). " +
                 "Click the asset in the Project window and press the 'Auto-Populate' button, " +
                 "or let OnValidate fill them automatically.")]
        [SerializeField] private Sprite[] _sprites = new Sprite[48];


        /// <summary>Returns the isolated dry sprite (index 36) for use in hoe-pop animations.</summary>
        public Sprite DryIsolatedSprite =>
            (_sprites != null && _sprites.Length > 36) ? _sprites[36] : null;


#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-populate sprites the first time (or if they were cleared).
            if (_sprites == null || _sprites.Length != 48 || _sprites[0] == null)
            {
                AutoPopulateSprites();
            }
        }

        [UnityEditor.MenuItem("CONTEXT/PlowedDirtAutoTile/Auto-Populate Sprites")]
        private static void AutoPopulateMenuItem(UnityEditor.MenuCommand cmd)
        {
            if (cmd.context is PlowedDirtAutoTile tile)
            {
                tile.AutoPopulateSprites();
            }
        }

        private void AutoPopulateSprites()
        {
            const string path = "Assets/Sprites/PlowedDirt_tiles.png";
            Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning($"[PlowedDirtAutoTile] Could not find sprite sheet at {path}");
                return;
            }

            _sprites = new Sprite[48];
            const string prefix = "PlowedDirt_tiles_";
            foreach (Object obj in assets)
            {
                if (obj is Sprite s && s.name.StartsWith(prefix))
                {
                    if (int.TryParse(s.name.Substring(prefix.Length), out int idx)
                        && idx >= 0 && idx < 48)
                    {
                        _sprites[idx] = s;
                    }
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[PlowedDirtAutoTile] Auto-populated 48 sprites from PlowedDirt_tiles.png");
        }
#endif


        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockTransform;
            tileData.colliderType = Tile.ColliderType.None;

            if (_sprites == null || _sprites.Length < 48)
            {
                return;
            }

            GridManager gm = GridManager.Instance;
            if (gm == null)
            {
                tileData.sprite = _sprites[36]; // dry isolated fallback
                return;
            }

            int moisture = gm.GetMoistureLevel(position);
            int baseCol  = moisture * 4; // 0, 4, or 8

            bool n = gm.IsCellTilled(position + Vector3Int.up);
            bool s = gm.IsCellTilled(position + Vector3Int.down);
            bool e = gm.IsCellTilled(position + Vector3Int.right);
            bool w = gm.IsCellTilled(position + Vector3Int.left);

            int row;
            if      (!n &&  s) row = 0; // top cap
            else if ( n &&  s) row = 1; // middle
            else if ( n && !s) row = 2; // bottom cap
            else               row = 3; // isolated

            int col;
            if      (!w && !e) col = 0; // isolated
            else if (!w &&  e) col = 1; // left edge
            else if ( w &&  e) col = 2; // centre
            else               col = 3; // right edge

            int idx = row * 12 + baseCol + col;
            tileData.sprite = (idx < _sprites.Length) ? _sprites[idx] : null;
        }

        public override bool StartUp(Vector3Int position, ITilemap tilemap, GameObject go) => true;
    }
}
