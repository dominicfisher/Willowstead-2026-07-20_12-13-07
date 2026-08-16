using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Willowstead.Player;

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

        public Tilemap FarmingTilemap => _farmingTilemap;

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

        // Tracks which cells have tilled dirt, their watered state, and 3-stage moisture levels (0=Dry, 1=Moist, 2=Wet).
        /// <summary>Day counter; bumped inside AdvanceDay() whenever a midnight rollover happens.</summary>
        public int CurrentDay { get; private set; }

        private HashSet<Vector3Int> _tilledCells = new HashSet<Vector3Int>();
        private HashSet<Vector3Int> _wateredCells = new HashSet<Vector3Int>();
        private HashSet<Vector3Int> _fertilizedCells = new HashSet<Vector3Int>();
        private Dictionary<Vector3Int, int> _moistureLevels = new Dictionary<Vector3Int, int>();

        [Header("Farmland Edges")]
        [Tooltip("The grass sprites used to decorate and soften the clean edges of tilled soil.")]
        [SerializeField] private Sprite[] _grassFringeSprites;

        [Header("Farmland Edge Settings")]
        [Tooltip("Minimum scale for the grass fringe sprigs.")]
        [SerializeField] private float _fringeScaleMin = 0.22f;

        [Tooltip("Maximum scale for the grass fringe sprigs.")]
        [SerializeField] private float _fringeScaleMax = 0.38f;

        // Tracks the spawned edge fringe GameObjects, keyed by the cell position that owns them.
        private Dictionary<Vector3Int, System.Collections.Generic.List<GameObject>> _edgeFringes = new Dictionary<Vector3Int, System.Collections.Generic.List<GameObject>>();

        public List<Willowstead.Persistence.Vector3IntRecord> CaptureTilledCells()
        {
            var list = new List<Willowstead.Persistence.Vector3IntRecord>(_tilledCells.Count);
            foreach (var c in _tilledCells) list.Add(new Willowstead.Persistence.Vector3IntRecord(c));
            return list;
        }

        public List<Willowstead.Persistence.Vector3IntRecord> CaptureWateredCells()
        {
            var list = new List<Willowstead.Persistence.Vector3IntRecord>(_wateredCells.Count);
            foreach (var c in _wateredCells) list.Add(new Willowstead.Persistence.Vector3IntRecord(c));
            return list;
        }

        public List<Willowstead.Persistence.Vector3IntRecord> CaptureFertilizedCells()
        {
            var list = new List<Willowstead.Persistence.Vector3IntRecord>(_fertilizedCells.Count);
            foreach (var c in _fertilizedCells) list.Add(new Willowstead.Persistence.Vector3IntRecord(c));
            return list;
        }

        public List<Willowstead.Persistence.SavedMoisture> CaptureMoistureLevels()
        {
            var list = new List<Willowstead.Persistence.SavedMoisture>(_moistureLevels.Count);
            foreach (var kvp in _moistureLevels)
            {
                list.Add(new Willowstead.Persistence.SavedMoisture
                {
                    cell = new Willowstead.Persistence.Vector3IntRecord(kvp.Key),
                    level = kvp.Value,
                });
            }
            return list;
        }

        public List<Willowstead.Persistence.SavedCrop> CaptureCrops()
        {
            var list = new List<Willowstead.Persistence.SavedCrop>();
            foreach (var kvp in _activeCrops)
            {
                Farming.Crop crop = kvp.Value;
                if (crop == null || crop.Data == null) continue;
                list.Add(new Willowstead.Persistence.SavedCrop
                {
                    cell = new Willowstead.Persistence.Vector3IntRecord(kvp.Key),
                    cropDataName = crop.Data.name,
                    currentStage = crop.CurrentStage,
                    daysInCurrentStage = 0,
                    visualsCount = crop.VisualsCount,
                    isFertilized = crop.IsFertilized,
                });
            }
            return list;
        }

        /// <summary>
        /// Apply all grid state from a save. Bypasses SoilPopAnimator and
        /// sets the tilemap tile instantly so a load doesn't replay
        /// animations the player has already seen. Crops are respawned
        /// directly to their saved stage via PlantCropFromSave.
        /// </summary>
        public void RestoreGridState(Willowstead.Persistence.SaveData data)
        {
            if (data == null) return;
            if (data.currentDay > 0) CurrentDay = data.currentDay;

            for (int i = 0; i < data.tilledCells.Count; i++)
            {
                Vector3Int cell = data.tilledCells[i].ToVector3Int();
                if (ProceduralGridGenerator.Instance != null)
                    ProceduralGridGenerator.Instance.ClearGrassAt(cell);
                _tilledCells.Add(cell);
                _moistureLevels[cell] = 0;
                if (_farmingTilemap != null && _tilledTile != null)
                    _farmingTilemap.SetTile(cell, _tilledTile);
            }

            for (int i = 0; i < data.wateredCells.Count; i++)
            {
                Vector3Int cell = data.wateredCells[i].ToVector3Int();
                _wateredCells.Add(cell);
                _moistureLevels[cell] = 2;
            }

            for (int i = 0; i < data.moistureLevels.Count; i++)
            {
                var m = data.moistureLevels[i];
                if (m == null || m.cell == null) continue;
                Vector3Int cell = m.cell.ToVector3Int();
                _moistureLevels[cell] = Mathf.Clamp(m.level, 0, 2);
                if (m.level > 0) _wateredCells.Add(cell);
            }

            if (_farmingTilemap != null)
                foreach (var cell in _tilledCells) RefreshFarmingNeighbours(cell);

            foreach (var cell in _tilledCells) UpdateEdgeFringesAround(cell);

            for (int i = 0; i < data.fertilizedCells.Count; i++)
            {
                Vector3Int cell = data.fertilizedCells[i].ToVector3Int();
                _fertilizedCells.Add(cell);
            }

            for (int i = 0; i < data.crops.Count; i++)
            {
                var sc = data.crops[i];
                if (sc == null || sc.cell == null) continue;
                Vector3Int cell = sc.cell.ToVector3Int();
                Farming.CropData data2 = FindCropDataByName(sc.cropDataName);
                if (data2 == null) continue;
                PlantCropFromSave(cell, data2, sc.currentStage, sc.visualsCount, sc.isFertilized);
            }
        }

        private static Farming.CropData FindCropDataByName(string dataName)
        {
            if (string.IsNullOrEmpty(dataName)) return null;
            var all = Resources.FindObjectsOfTypeAll<Farming.CropData>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == dataName) return all[i];
            }
            return null;
        }

        /// <summary>
        /// Respawn a saved crop straight to its visual stage without
        /// retriggering growth pop-ups. Synthesizes a fresh GameObject if
        /// no prefab reference exists, so a save always loads cleanly.
        /// </summary>
        private void PlantCropFromSave(Vector3Int cell, Farming.CropData cropData, int currentStage, int visualsCount, bool isFertilized = false)
        {
            if (HasCrop(cell)) return;
            GameObject cropGo = new GameObject($"Crop_{cell.x}_{cell.y}");
            cropGo.transform.position = CellToWorldCenter(cell);
            Farming.Crop crop = cropGo.AddComponent<Farming.Crop>();
            crop.Initialize(cropData, cell);
            crop.IsFertilized = isFertilized || _fertilizedCells.Contains(cell);
            _activeCrops.Add(cell, crop);
            crop.ForceStage(currentStage, visualsCount);
        }

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
        /// Exposes the cell size of the Grid component.
        /// </summary>
        public Vector3 CellSize
        {
            get
            {
                if (_grid != null) return _grid.cellSize;
                return Vector3.one;
            }
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
        /// Gets the moisture level for a grid cell (0 = Dry, 1 = Moist, 2 = Wet).
        /// </summary>
        public int GetMoistureLevel(Vector3Int cellPosition)
        {
            if (_moistureLevels.TryGetValue(cellPosition, out int moisture))
            {
                return moisture;
            }
            return 0;
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

            if (ProceduralGridGenerator.Instance != null && ProceduralGridGenerator.Instance.HasPuddleAt(cellPosition))
            {
#if UNITY_EDITOR
                Debug.Log($"[GridManager] Cannot hoe here: water/puddle occupies this tile at {cellPosition}.");
#endif
                return;
            }

            if (ProceduralGridGenerator.Instance != null)
            {
                ProceduralGridGenerator.Instance.ClearGrassAt(cellPosition);
            }

            _tilledCells.Add(cellPosition);
            _moistureLevels[cellPosition] = 0; // 0 = Dry (Blue block)

            Sprite soilSprite = null;
            if (_tilledTile is PlowedDirtAutoTile autoTile)
            {
                soilSprite = autoTile.DryIsolatedSprite;
            }
            else if (_tilledTile is Tile standardTile)
            {
                soilSprite = standardTile.sprite;
            }

            if (soilSprite != null)
            {
                GameObject animGo = new GameObject("SoilPopAnimation");
                animGo.transform.position = CellToWorldCenter(cellPosition);

                SoilPopAnimator animator = animGo.AddComponent<SoilPopAnimator>();
                animator.Initialize(soilSprite, _dirtParticleSprite, _dirtColor, () =>
                {
                    if (_farmingTilemap != null && _tilledTile != null)
                    {
                        _farmingTilemap.SetTile(cellPosition, _tilledTile);
                        RefreshFarmingNeighbours(cellPosition);
                    }
                });
            }
            else
            {
                if (_farmingTilemap != null && _tilledTile != null)
                {
                    _farmingTilemap.SetTile(cellPosition, _tilledTile);
                    RefreshFarmingNeighbours(cellPosition);
                }
            }

            UpdateEdgeFringesAround(cellPosition);
            UpdateEdgeFringesAround(cellPosition + new Vector3Int(1, 0, 0));
            UpdateEdgeFringesAround(cellPosition + new Vector3Int(-1, 0, 0));
            UpdateEdgeFringesAround(cellPosition + new Vector3Int(0, 1, 0));
            UpdateEdgeFringesAround(cellPosition + new Vector3Int(0, -1, 0));

#if UNITY_EDITOR
            Debug.Log($"[GridManager] Tilled tile at: {cellPosition}");
#endif
        }

        private void UpdateEdgeFringesAround(Vector3Int cell)
        {
            ClearFringesForCell(cell);

            if (!_tilledCells.Contains(cell)) return;

            bool tilledN = _tilledCells.Contains(cell + new Vector3Int(0, 1, 0));
            bool tilledS = _tilledCells.Contains(cell + new Vector3Int(0, -1, 0));
            bool tilledE = _tilledCells.Contains(cell + new Vector3Int(1, 0, 0));
            bool tilledW = _tilledCells.Contains(cell + new Vector3Int(-1, 0, 0));

            System.Collections.Generic.List<GameObject> cellFringes = new System.Collections.Generic.List<GameObject>();
            Vector3 cellWorldPos = CellToWorldCenter(cell);

            Vector3 cellSize = CellSize;
            float scaleX = cellSize.x;
            float scaleY = cellSize.y;

            System.Action<Vector3, float> spawnTuft = (pos, sizeMult) =>
            {
                if (_grassFringeSprites == null || _grassFringeSprites.Length == 0) return;

                GameObject tuftGo = new GameObject("GrassEdgeFringe");
                tuftGo.transform.parent = transform;
                tuftGo.transform.position = pos;

                float scale = Random.Range(_fringeScaleMin, _fringeScaleMax) * sizeMult * scaleX;
                tuftGo.transform.localScale = new Vector3(scale, scale, 1f);

                SpriteRenderer sr = tuftGo.AddComponent<SpriteRenderer>();
                sr.sprite = _grassFringeSprites[Random.Range(0, _grassFringeSprites.Length)];

                // Dynamic sorting order so grass draws in front of the tilemap but correctly sorts with player
                sr.sortingOrder = Mathf.RoundToInt(-pos.y * 100) - 5;
                sr.color = Color.white;
                sr.flipX = Random.value > 0.5f;

                cellFringes.Add(tuftGo);
            };

            bool isIsolated = !tilledN && !tilledS && !tilledE && !tilledW;

            if (isIsolated)
            {
                for (int angleDeg = 0; angleDeg < 360; angleDeg += 45)
                {
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    float currentOffset = 0.44f;

                    if (angleDeg == 225 || angleDeg == 270 || angleDeg == 315)
                    {
                        currentOffset = 0.68f;
                    }

                    Vector3 pos = cellWorldPos + new Vector3(Mathf.Cos(angleRad) * currentOffset * scaleX, Mathf.Sin(angleRad) * currentOffset * scaleY, 0f);
                    spawnTuft(pos, 0.95f);
                }
            }
            else
            {
                if (!tilledN && tilledE && tilledW)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float t = (i / 2.0f) - 0.5f;
                        Vector3 pos = cellWorldPos + new Vector3(t * 0.7f * scaleX, 0.45f * scaleY, 0f);
                        spawnTuft(pos, 1.0f);
                    }
                }
                if (!tilledS && tilledE && tilledW)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float t = (i / 2.0f) - 0.5f;
                        Vector3 pos = cellWorldPos + new Vector3(t * 0.7f * scaleX, -0.68f * scaleY, 0f);
                        spawnTuft(pos, 1.0f);
                    }
                }
                if (!tilledE && tilledN && tilledS)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float t = (i / 2.0f) - 0.5f;
                        Vector3 pos = cellWorldPos + new Vector3(0.42f * scaleX, t * 0.7f * scaleY, 0f);
                        spawnTuft(pos, 1.0f);
                    }
                }
                if (!tilledW && tilledN && tilledS)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float t = (i / 2.0f) - 0.5f;
                        Vector3 pos = cellWorldPos + new Vector3(-0.42f * scaleX, t * 0.7f * scaleY, 0f);
                        spawnTuft(pos, 1.0f);
                    }
                }

                if (!tilledN && !tilledW)
                {
                    for (int angleDeg = 90; angleDeg <= 180; angleDeg += 45)
                    {
                        float rad = angleDeg * Mathf.Deg2Rad;
                        float offset = (angleDeg == 90) ? 0.45f : ((angleDeg == 180) ? 0.42f : 0.44f);
                        Vector3 pos = cellWorldPos + new Vector3(Mathf.Cos(rad) * offset * scaleX, Mathf.Sin(rad) * offset * scaleY, 0f);
                        spawnTuft(pos, 1.0f);
                    }
                }
                if (!tilledN && !tilledE)
                {
                    for (int angleDeg = 0; angleDeg <= 90; angleDeg += 45)
                    {
                        float rad = angleDeg * Mathf.Deg2Rad;
                        float offset = (angleDeg == 90) ? 0.45f : ((angleDeg == 0) ? 0.42f : 0.44f);
                        Vector3 pos = cellWorldPos + new Vector3(Mathf.Cos(rad) * offset * scaleX, Mathf.Sin(rad) * offset * scaleY, 0f);
                        spawnTuft(pos, 1.0f);
                    }
                }
                if (!tilledS && !tilledW)
                {
                    for (int angleDeg = 180; angleDeg <= 270; angleDeg += 45)
                    {
                        float rad = angleDeg * Mathf.Deg2Rad;
                        float offset = (angleDeg == 180) ? 0.42f : ((angleDeg == 270) ? 0.68f : 0.55f);
                        Vector3 pos = cellWorldPos + new Vector3(Mathf.Cos(rad) * offset * scaleX, Mathf.Sin(rad) * offset * scaleY, 0f);
                        spawnTuft(pos, 1.0f);
                    }
                }
                if (!tilledS && !tilledE)
                {
                    for (int angleDeg = 270; angleDeg <= 360; angleDeg += 45)
                    {
                        float rad = angleDeg * Mathf.Deg2Rad;
                        float offset = (angleDeg == 360 || angleDeg == 0) ? 0.42f : ((angleDeg == 270) ? 0.68f : 0.55f);
                        Vector3 pos = cellWorldPos + new Vector3(Mathf.Cos(rad) * offset * scaleX, Mathf.Sin(rad) * offset * scaleY, 0f);
                        spawnTuft(pos, 1.0f);
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

        private void RefreshFarmingNeighbours(Vector3Int cellPosition)
        {
            if (_farmingTilemap == null) return;
            _farmingTilemap.RefreshTile(cellPosition);
            _farmingTilemap.RefreshTile(cellPosition + Vector3Int.up);
            _farmingTilemap.RefreshTile(cellPosition + Vector3Int.down);
            _farmingTilemap.RefreshTile(cellPosition + Vector3Int.left);
            _farmingTilemap.RefreshTile(cellPosition + Vector3Int.right);
        }

        /// <summary>
        /// Waters the ground at the specified cell.
        /// </summary>
        public void WaterTile(Vector3Int cellPosition)
        {
            if (!_tilledCells.Contains(cellPosition)) return; // Only tilled soil can be watered

            _wateredCells.Add(cellPosition);
            _moistureLevels[cellPosition] = 2; // 2 = Wet (Yellow block)

            if (_farmingTilemap != null)
            {
                RefreshFarmingNeighbours(cellPosition);
            }

#if UNITY_EDITOR
            Debug.Log($"[GridManager] Watered tile at: {cellPosition}");
#endif
        }

        /// <summary>
        /// Increments the moisture level of a tilled tile by 1 (capped at Wet=2).
        /// Called by RainSplash to nudge soil wetness toward Wet during a
        /// storm without bypassing the player's manual watering action.
        /// Non-tilled cells are silently ignored.
        /// </summary>
        public void IncreaseMoistureFromRain(Vector3Int cellPosition)
        {
            if (!_tilledCells.Contains(cellPosition)) return;

            int current = 0;
            _moistureLevels.TryGetValue(cellPosition, out current);
            int next = Mathf.Min(2, current + 1);

            // Avoid the RefreshTile syscall every drop — only when crossing into Wet
            // does the visual state really change. (Dry=0, Moist=1, Wet=2).
            if (next == current) return;

            _moistureLevels[cellPosition] = next;

            if (next >= 2 && !_wateredCells.Contains(cellPosition))
            {
                _wateredCells.Add(cellPosition);
                if (_farmingTilemap != null)
                {
                    RefreshFarmingNeighbours(cellPosition);
                }
            }
            else if (_farmingTilemap != null && _tilledTile is PlowedDirtAutoTile)
            {
                // PlowedDirtAutoTile can re-pick its Moist variant. Cheap enough.
                RefreshFarmingNeighbours(cellPosition);
            }
        }

        /// <summary>
        /// Enriches the specified tilled soil or growing crop with fertilizer.
        /// </summary>
        public bool FertilizeCell(Vector3Int cellPosition)
        {
            if (!_tilledCells.Contains(cellPosition))
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[GridManager] Cannot fertilize cell at {cellPosition}: must be tilled first.");
#endif
                return false;
            }

            if (_fertilizedCells.Contains(cellPosition))
            {
                return false; // Already fertilized
            }

            _fertilizedCells.Add(cellPosition);

            if (_activeCrops.TryGetValue(cellPosition, out Farming.Crop activeCrop) && activeCrop != null)
            {
                activeCrop.IsFertilized = true;
            }

            SpawnFertilizerSparkle(cellPosition);

#if UNITY_EDITOR
            Debug.Log($"[GridManager] Fertilized cell at {cellPosition}.");
#endif
            return true;
        }

        public bool IsCellFertilized(Vector3Int cellPosition)
        {
            return _fertilizedCells.Contains(cellPosition);
        }

        private void SpawnFertilizerSparkle(Vector3Int cell)
        {
            Vector3 worldPos = CellToWorldCenter(cell);
            GameObject sparkleGo = new GameObject($"FertilizerSparkle_{cell.x}_{cell.y}");
            sparkleGo.transform.position = worldPos;
            StartCoroutine(AnimateFertilizerSparkle(sparkleGo));
        }

        private System.Collections.IEnumerator AnimateFertilizerSparkle(GameObject sparkleGo)
        {
            int sparkCount = 8;
            List<GameObject> sparks = new List<GameObject>();
            for (int i = 0; i < sparkCount; i++)
            {
                GameObject s = new GameObject("Spark");
                s.transform.SetParent(sparkleGo.transform, false);
                SpriteRenderer sr = s.AddComponent<SpriteRenderer>();
                sr.sprite = _dirtParticleSprite != null ? _dirtParticleSprite : UIResourceHelper.GetBackgroundSprite();
                // Rich golden sparkle burst
                sr.color = (i % 2 == 0) ? new Color(1f, 0.88f, 0.35f, 0.95f) : new Color(1f, 0.96f, 0.65f, 0.95f);
                sr.sortingLayerName = "Foreground";
                sr.sortingOrder = 500;
                s.transform.localScale = Vector3.one * 0.14f;
                sparks.Add(s);
            }

            float duration = 0.55f;
            float elapsed = 0f;
            Vector3[] directions = new Vector3[sparkCount];
            for (int i = 0; i < sparkCount; i++)
            {
                float angle = i * (360f / sparkCount) * Mathf.Deg2Rad;
                directions[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(0.30f, 0.55f);
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                for (int i = 0; i < sparkCount; i++)
                {
                    if (sparks[i] == null) continue;
                    sparks[i].transform.localPosition = Vector3.Lerp(Vector3.zero, directions[i], t) + new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.18f, 0f);
                    SpriteRenderer sr = sparks[i].GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        float alpha = Mathf.Sin(t * Mathf.PI);
                        sr.color = (i % 2 == 0) ? new Color(1f, 0.88f, 0.35f, alpha) : new Color(1f, 0.96f, 0.65f, alpha);
                    }
                }
                yield return null;
            }

            Destroy(sparkleGo);
        }

        /// <summary>
        /// Plants a crop on a tilled cell.
        /// </summary>
        public bool PlantCrop(Vector3Int cellPosition, Farming.CropData cropData, GameObject cropPrefab)
        {
            if (!IsCellTilled(cellPosition))
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[GridManager] Cannot plant crop at {cellPosition}: ground must be tilled first.");
#endif
                return false;
            }

            if (HasCrop(cellPosition))
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[GridManager] Cannot plant crop at {cellPosition}: a crop already exists here.");
#endif
                return false;
            }

            Vector3 worldPos = CellToWorldCenter(cellPosition);
            GameObject cropGo = Instantiate(cropPrefab, worldPos, Quaternion.identity, transform);

            Farming.Crop cropComponent = cropGo.GetComponent<Farming.Crop>();
            if (cropComponent == null)
            {
                cropComponent = cropGo.AddComponent<Farming.Crop>();
            }

            cropComponent.Initialize(cropData, cellPosition);
            cropComponent.IsFertilized = _fertilizedCells.Contains(cellPosition);
            _activeCrops.Add(cellPosition, cropComponent);

#if UNITY_EDITOR
            Debug.Log($"[GridManager] Planted {cropData.CropName} at: {cellPosition} (Fertilized: {cropComponent.IsFertilized}). Total individual crops growing in the world: {GetTotalCropsPlanted()}");
#endif
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
        /// Also see AdvanceHalfDayGrowthTick for a midday growth-only tick.
        /// </summary>
        public void AdvanceDay()
        {
#if UNITY_EDITOR
            Debug.Log("[GridManager] A new day begins!");
#endif

            foreach (var kvp in _activeCrops)
            {
                Vector3Int cell = kvp.Key;
                Farming.Crop crop = kvp.Value;

                bool isWatered = _wateredCells.Contains(cell);
                crop.Grow(isWatered);
            }

            _wateredCells.Clear();

            List<Vector3Int> keys = new List<Vector3Int>(_moistureLevels.Keys);
            foreach (Vector3Int cell in keys)
            {
                int currentMoisture = _moistureLevels[cell];
                if (currentMoisture > 0)
                {
                    int nextMoisture = currentMoisture - 1;
                    _moistureLevels[cell] = nextMoisture;

                    if (nextMoisture > 0)
                    {
                        _wateredCells.Add(cell);
                    }
                }
            }

            if (_farmingTilemap != null)
            {
            foreach (Vector3Int cell in _tilledCells)
            {
                RefreshFarmingNeighbours(cell);
                _moistureLevels[cell] = Mathf.Max(0, _moistureLevels[cell] - 1);
                if (_moistureLevels[cell] > 0) _wateredCells.Add(cell);
            }
            }

            CurrentDay++;
        }

        /// <summary>
        /// Midday growth-only tick. Advances crops once based on current watered state
        /// but does not change moisture/tiles. Call this around noon for half-day growth.
        /// </summary>
        public void AdvanceHalfDayGrowthTick()
        {
            foreach (var kvp in _activeCrops)
            {
                Vector3Int cell = kvp.Key;
                Farming.Crop crop = kvp.Value;
                bool isWatered = _wateredCells.Contains(cell) || (_moistureLevels.TryGetValue(cell, out int m) && m > 0);
                crop.Grow(isWatered);
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
#if UNITY_EDITOR
                Debug.Log($"[GridManager] Crop harvested from: {cellPosition}. Total individual crops remaining in the world: {GetTotalCropsPlanted()}");
#endif
            }
        }
    }
}
