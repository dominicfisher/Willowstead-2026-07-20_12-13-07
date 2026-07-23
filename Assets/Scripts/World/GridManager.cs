using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Willowstead.World
{
    /// <summary>
    /// Manages the coordinate grid, checks tile states, and tracks crops.
    /// Acts as a central database for world-space coordinates and grid cells.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [Header("References")]
        [Tooltip("The main Unity Grid component in the scene.")]
        [SerializeField] private Grid _grid;

        [Tooltip("The Tilemap representing the farming/soil layer.")]
        [SerializeField] private Tilemap _farmingTilemap;

        [Header("Farming Tiles")]
        [Tooltip("Tile asset used for hoed/tilled soil.")]
        [SerializeField] private TileBase _tilledTile;

        [Tooltip("Tile asset used for watered tilled soil.")]
        [SerializeField] private TileBase _wateredTile;

        [Header("Juice / Effects")]
        [Tooltip("The circular/square sprite to use for flying dirt particles when tilling soil.")]
        [SerializeField] private Sprite _dirtParticleSprite;

        [Tooltip("The color tint applied to dirt particles.")]
        [SerializeField] private Color _dirtColor = new Color(0.4f, 0.28f, 0.18f, 1f);

        // Tracks active crops spawned in the world, keyed by their grid coordinate.
        private Dictionary<Vector3Int, Farming.Crop> _activeCrops = new Dictionary<Vector3Int, Farming.Crop>();
        
        // Tracks which cells have tilled dirt and their watered state.
        private HashSet<Vector3Int> _tilledCells = new HashSet<Vector3Int>();
        private HashSet<Vector3Int> _wateredCells = new HashSet<Vector3Int>();

        [Header("Farmland Edges")]
        [Tooltip("The grass sprites used to decorate and soften the clean edges of tilled soil.")]
        [SerializeField] private Sprite[] _grassFringeSprites;

        [Header("Farmland Edge Settings")]
        [Tooltip("Minimum scale for the grass fringe sprigs.")]
        [SerializeField] private float _fringeScaleMin = 0.22f;

        [Tooltip("Maximum scale for the grass fringe sprigs.")]
        [SerializeField] private float _fringeScaleMax = 0.38f;

        [Tooltip("Distance offset from the tile center to place the fringe. Higher values push grass further outwards.")]
        [SerializeField] private float _fringeBoundaryOffset = 0.55f;

        // Tracks the spawned edge fringe GameObjects, keyed by the cell position that owns them.
        private Dictionary<Vector3Int, System.Collections.Generic.List<GameObject>> _edgeFringes = new Dictionary<Vector3Int, System.Collections.Generic.List<GameObject>>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (_grid == null) _grid = FindAnyObjectByType<Grid>();
        }

        /// <summary>
        /// Translates a world position to the nearest grid cell coordinate.
        /// </summary>
        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            return _grid.WorldToCell(worldPosition);
        }

        /// <summary>
        /// Translates a grid cell coordinate to the center world position.
        /// </summary>
        public Vector3 CellToWorldCenter(Vector3Int cellPosition)
        {
            return _grid.GetCellCenterWorld(cellPosition);
        }

        /// <summary>
        /// Check if a cell is tilled soil.
        /// </summary>
        public bool IsCellTilled(Vector3Int cellPosition)
        {
            return _tilledCells.Contains(cellPosition);
        }

        /// <summary>
        /// Check if a cell is watered.
        /// </summary>
        public bool IsCellWatered(Vector3Int cellPosition)
        {
            return _wateredCells.Contains(cellPosition);
        }

        /// <summary>
        /// Check if a cell contains a crop.
        /// </summary>
        public bool HasCrop(Vector3Int cellPosition)
        {
            return _activeCrops.ContainsKey(cellPosition);
        }

        /// <summary>
        /// Gets the crop component at a specific grid position.
        /// </summary>
        public Farming.Crop GetCrop(Vector3Int cellPosition)
        {
            _activeCrops.TryGetValue(cellPosition, out var crop);
            return crop;
        }

        /// <summary>
        /// Hoes the ground at the specified cell, turning it into tilled dirt.
        /// </summary>
        public void HoeTile(Vector3Int cellPosition)
        {
            if (_tilledCells.Contains(cellPosition)) return;

            _tilledCells.Add(cellPosition);
            
            // Extract the sprite from the Tile asset
            Sprite soilSprite = null;
            if (_tilledTile != null && _tilledTile is Tile tile)
            {
                soilSprite = tile.sprite;
            }

            if (soilSprite != null)
            {
                // Instantiate the visual pop animator at the cell's world position
                GameObject animGo = new GameObject("SoilPopAnimation");
                animGo.transform.position = CellToWorldCenter(cellPosition);
                
                SoilPopAnimator animator = animGo.AddComponent<SoilPopAnimator>();
                animator.Initialize(soilSprite, _dirtParticleSprite, _dirtColor, () =>
                {
                    // Place permanent tile once the pop-up finishes
                    if (_farmingTilemap != null && _tilledTile != null)
                    {
                        _farmingTilemap.SetTile(cellPosition, _tilledTile);
                    }
                });
            }
            else
            {
                // Fallback instant placement
                if (_farmingTilemap != null && _tilledTile != null)
                {
                    _farmingTilemap.SetTile(cellPosition, _tilledTile);
                }
            }
            
            // Update edge fringes for this tile and its 4 neighbors
            UpdateEdgeFringesAround(cellPosition);
            UpdateEdgeFringesAround(cellPosition + new Vector3Int(1, 0, 0));
            UpdateEdgeFringesAround(cellPosition + new Vector3Int(-1, 0, 0));
            UpdateEdgeFringesAround(cellPosition + new Vector3Int(0, 1, 0));
            UpdateEdgeFringesAround(cellPosition + new Vector3Int(0, -1, 0));

            Debug.Log($"[GridManager] Tilled tile at: {cellPosition}");
        }

        private void UpdateEdgeFringesAround(Vector3Int cell)
        {
            // Clear existing fringes first
            ClearFringesForCell(cell);

            // Fringes are only owned by tilled cells
            if (!_tilledCells.Contains(cell)) return;

            Vector3Int[] directions = new Vector3Int[]
            {
                new Vector3Int(1, 0, 0),   // Right
                new Vector3Int(-1, 0, 0),  // Left
                new Vector3Int(0, 1, 0),   // Up
                new Vector3Int(0, -1, 0)   // Down
            };

            System.Collections.Generic.List<GameObject> cellFringes = new System.Collections.Generic.List<GameObject>();

            foreach (var dir in directions)
            {
                Vector3Int neighbor = cell + dir;
                // If neighbor is NOT tilled, we spawn grass edge overlays
                if (!_tilledCells.Contains(neighbor))
                {
                    int tuftCount = Random.Range(2, 4); // 2 or 3 tufts
                    Vector3 cellWorldPos = CellToWorldCenter(cell);
                    Vector3 boundaryCenter = cellWorldPos + (Vector3)dir * _fringeBoundaryOffset;

                    for (int i = 0; i < tuftCount; i++)
                    {
                        GameObject tuftGo = new GameObject("GrassEdgeFringe");
                        tuftGo.transform.parent = transform;

                        // Space out along the boundary line
                        float offsetPercent = (tuftCount > 1) ? ((float)i / (tuftCount - 1) - 0.5f) : 0f;
                        float distOffset = offsetPercent * 0.45f;

                        Vector3 position = boundaryCenter;
                        if (dir.x != 0) // Vertical boundary
                        {
                            position.y += distOffset + Random.Range(-0.06f, 0.06f);
                            position.x += Random.Range(-0.05f, 0.05f);
                        }
                        else // Horizontal boundary
                        {
                            position.x += distOffset + Random.Range(-0.06f, 0.06f);
                            position.y += Random.Range(-0.05f, 0.05f);
                        }

                        tuftGo.transform.position = position;

                        // Scale down slightly so they look like small edge fringes
                        float scale = Random.Range(_fringeScaleMin, _fringeScaleMax);
                        tuftGo.transform.localScale = new Vector3(scale, scale, 1f);

                        SpriteRenderer sr = tuftGo.AddComponent<SpriteRenderer>();
                        if (_grassFringeSprites != null && _grassFringeSprites.Length > 0)
                        {
                            sr.sprite = _grassFringeSprites[Random.Range(0, _grassFringeSprites.Length)];
                        }
                        
                        // Draw on top of soil (-100) but below crops/player
                        sr.sortingOrder = -90;
                        sr.color = Color.white;
                        sr.flipX = Random.value > 0.5f;

                        cellFringes.Add(tuftGo);
                    }
                }
            }

            if (cellFringes.Count > 0)
            {
                _edgeFringes[cell] = cellFringes;
            }
        }

        private void ClearFringesForCell(Vector3Int cell)
        {
            if (_edgeFringes.TryGetValue(cell, out var list))
            {
                if (list != null)
                {
                    foreach (var go in list)
                    {
                        if (go != null) Destroy(go);
                    }
                }
                _edgeFringes.Remove(cell);
            }
        }

        /// <summary>
        /// Waters the ground at the specified cell.
        /// </summary>
        public void WaterTile(Vector3Int cellPosition)
        {
            if (!_tilledCells.Contains(cellPosition)) return; // Only tilled soil can be watered
            if (_wateredCells.Contains(cellPosition)) return;

            _wateredCells.Add(cellPosition);

            if (_farmingTilemap != null && _wateredTile != null)
            {
                _farmingTilemap.SetTile(cellPosition, _wateredTile);
            }
            else if (_farmingTilemap != null)
            {
                // Fallback: darken the tile color to show it is wet
                _farmingTilemap.SetTileFlags(cellPosition, TileFlags.None);
                _farmingTilemap.SetColor(cellPosition, new Color(0.5f, 0.5f, 0.5f, 1f));
            }

            Debug.Log($"[GridManager] Watered tile at: {cellPosition}");
        }

        /// <summary>
        /// Plants a crop on a tilled cell.
        /// </summary>
        public bool PlantCrop(Vector3Int cellPosition, Farming.CropData cropData, GameObject cropPrefab)
        {
            if (!IsCellTilled(cellPosition))
            {
                Debug.LogWarning("[GridManager] Cannot plant crop: Ground must be tilled first.");
                return false;
            }

            if (HasCrop(cellPosition))
            {
                Debug.LogWarning("[GridManager] Cannot plant crop: A crop already exists here.");
                return false;
            }

            // Spawn the crop prefab at the center of the cell
            Vector3 worldPos = CellToWorldCenter(cellPosition);
            GameObject cropGo = Instantiate(cropPrefab, worldPos, Quaternion.identity, transform);
            
            Farming.Crop cropComponent = cropGo.GetComponent<Farming.Crop>();
            if (cropComponent == null)
            {
                cropComponent = cropGo.AddComponent<Farming.Crop>();
            }

            cropComponent.Initialize(cropData, cellPosition);
            _activeCrops.Add(cellPosition, cropComponent);

            Debug.Log($"[GridManager] Planted {cropData.CropName} at: {cellPosition}. Total individual crops growing in the world: {GetTotalCropsPlanted()}");
            return true;
        }

        /// <summary>
        /// Gets the total count of individual crop instances currently growing in the world.
        /// </summary>
        public int GetTotalCropsPlanted()
        {
            int total = 0;
            foreach (var crop in _activeCrops.Values)
            {
                if (crop != null)
                {
                    total += Mathf.Max(1, crop.VisualsCount);
                }
            }
            return total;
        }

        /// <summary>
        /// Simulates a day passing, advancing crop growth and drying out watered soil.
        /// </summary>
        public void AdvanceDay()
        {
            Debug.Log("[GridManager] A new day begins!");

            // Grow crops
            foreach (var kvp in _activeCrops)
            {
                Vector3Int cell = kvp.Key;
                Farming.Crop crop = kvp.Value;

                bool isWatered = _wateredCells.Contains(cell);
                crop.Grow(isWatered);
            }

            // Dry the soil
            _wateredCells.Clear();

            if (_farmingTilemap != null)
            {
                foreach (Vector3Int cell in _tilledCells)
                {
                    // Revert tile back to dry tilled tile
                    if (_tilledTile != null)
                    {
                        _farmingTilemap.SetTile(cell, _tilledTile);
                    }
                    else
                    {
                        // Reset fallback color
                        _farmingTilemap.SetColor(cell, Color.white);
                    }
                }
            }
        }

        /// <summary>
        /// Cleans up a harvested crop from the system.
        /// </summary>
        public void RemoveCrop(Vector3Int cellPosition)
        {
            if (_activeCrops.ContainsKey(cellPosition))
            {
                _activeCrops.Remove(cellPosition);
                Debug.Log($"[GridManager] Crop harvested from: {cellPosition}. Total individual crops remaining in the world: {GetTotalCropsPlanted()}");
            }
        }
    }
}
