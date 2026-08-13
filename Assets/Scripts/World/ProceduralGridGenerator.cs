using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Willowstead.World
{
    /// <summary>
    /// Pairs a grass Rule Tile asset with a relative spawn weight.
    /// Higher weight = more common biome. Weights are normalised automatically.
    /// </summary>
    [System.Serializable]
    public struct GrassBiomeEntry
    {
        [Tooltip("The Rule Tile asset for this grass biome.")]
        public TileBase tile;
        [Tooltip("Relative frequency of this biome. Higher = more common. All weights are normalised automatically.")]
        [Min(0f)]
        public float weight;
    }

    /// <summary>
    /// Generates a procedural terrain grid using a chunk-based infinite-world system.
    /// Uses Unity Rule Tiles for grass, dirt, and water — edge blending is handled automatically.
    /// </summary>
    [ExecuteAlways]
    public class ProceduralGridGenerator : MonoBehaviour
    {
        [Header("Tilemap References")]
        [SerializeField] private Tilemap _grassTilemap;
        [SerializeField] private Tilemap _dirtTilemap;
        // Separate tilemap above grass so water tiles don't replace/remove grass tiles.
        // Set its Sorting Order above the grass tilemap in the Inspector (e.g. grass=1, water=2).
        [SerializeField] private Tilemap _waterTilemap;

        [Header("Tilemap Assets")]
        [SerializeField] private TileBase _baseDirtTile;
        [Tooltip("Each entry is a grass Rule Tile and its relative spawn weight. " +
                 "E.g. weights 60/30/10 give a 60 % / 30 % / 10 % split.")]
        [SerializeField] private GrassBiomeEntry[] _grassBiomes;
        [SerializeField] private TileBase _waterRuleTile;
        [SerializeField] private float _biomeNoiseScale = 0.015f;

        // Cached cumulative-weight table rebuilt whenever biome entries change.
        // Index i holds the sum of weights[0..i], normalised to [0,1].
        private float[] _biomeCumulativeWeights;

        [Header("Dirt Patches")]
        [Tooltip("Enable scattered bare-dirt areas within grass regions.")]
        [SerializeField] private bool _generateDirtPatches = true;
        [Tooltip("Noise scale for dirt patch shapes. Smaller = larger blobs.")]
        [SerializeField] private float _dirtPatchNoiseScale = 0.06f;
        [Range(0f, 1f)]
        [Tooltip("Noise value below which a grass tile becomes bare dirt. Higher = more dirt.")]
        [SerializeField] private float _dirtPatchThreshold = 0.38f;

        [Header("Chunk Settings")]
        [SerializeField] private int _chunkSize = 16;
        [SerializeField] private int _renderRadius = 3;

        [Header("Player Reference")]
        [SerializeField] private Transform _playerTransform;

        [Header("Noise Settings")]
        [SerializeField] private float _noiseScale = 0.04f;
        [Range(0f, 1f)]
        [SerializeField] private float _grassThreshold = 0.55f;

        [Header("Pre-Generated Farm Plot")]
        [SerializeField] private bool _preGenerateFarmPlot = true;
        [SerializeField] private int _farmPlotWidth = 5;
        [SerializeField] private int _farmPlotHeight = 3;

        [Header("Decor & Trees")]
        [SerializeField] private Sprite[] _treeSprites;
        [SerializeField] private Sprite[] _objectSprites;

        [Header("Puddles / Ponds")]
        [SerializeField] private Sprite[] _puddleFillSprites;
        [Range(0f, 0.05f)] [SerializeField] private float _puddleSeedDensity = 0.01f;
        [SerializeField] private int _puddleMinRadius = 2;
        [SerializeField] private int _puddleMaxRadius = 5;
        [SerializeField] private int _maxPondsPerChunk = 4;
        [Tooltip("Minimum tile distance required between the edge of any existing pond/river and a new pond.")]
        [Range(1, 20)] [SerializeField] private int _minPondSeparation = 4;
        [SerializeField] private bool _puddlesOnlyOnGrass = true;
        [SerializeField] private bool _puddlesBlockMovement = true;
        [Range(0,3)] [SerializeField] private int _puddleGrassBuffer = 1;
        [SerializeField] private bool _enforceGrassBufferForPuddles = true;

        // Organic blob shape: Perlin noise distorts the pond radius so ponds look
        // natural rather than perfect circles. 0 = perfect disc, 1 = very jagged.
        [Range(0f, 1f)] [SerializeField] private float _pondBlobStrength = 0.5f;
        // Frequency of the noise used for blob distortion. Higher = more jagged edges.
        [SerializeField] private float _pondBlobNoiseScale = 0.35f;

        [Header("Rivers")]
        [SerializeField] private bool _generateRivers = true;
        // Chance per chunk that a river originates from it. 0.05 = ~5% of chunks.
        [Range(0f, 1f)] [SerializeField] private float _riverChancePerChunk = 0.05f;
        // Minimum distance in chunks between river starting points (e.g., 3 means no other river can start within 3 chunks).
        [Range(1, 10)] [SerializeField] private int _minRiverChunkSeparation = 3;
        // Half-width of the river in tiles (e.g. 1 = 3 tiles wide, 2 = 5 tiles wide).
        [Range(1, 6)] [SerializeField] private int _riverHalfWidth = 1;
        // How many steps the river takes before stopping.
        [SerializeField] private int _riverLength = 60;
        // Maximum vertical drift per step (0 = perfectly straight, higher = more winding).
        [Range(0f, 2f)] [SerializeField] private float _riverWanderStrength = 0.8f;
        // Noise scale for the river's wander curve. Smaller = smoother turns.
        [SerializeField] private float _riverWanderNoiseScale = 0.08f;

        [Header("Decor Density")]
        [Range(0f, 0.2f)] [SerializeField] private float _treeDensity = 0.03f;
        [Range(0f, 0.2f)] [SerializeField] private float _objectDensity = 0.01f;
        [SerializeField] private int _maxTreesPerChunk = 32;
        [SerializeField] private int _maxObjectsPerChunk = 32;
        [SerializeField] private float _jitterRange = 0.18f;
        [SerializeField] private float _minObjectTreeSeparation = 0.6f;
        [Range(0,3)] [SerializeField] private int _minDecorDistanceFromPuddle = 1;

        [Header("Collider Tuning")]
        [Range(0.05f, 1f)]   [SerializeField] private float _treeColliderWidthPct   = 0.32f;
        [Range(0.05f, 0.5f)] [SerializeField] private float _treeColliderHeightPct  = 0.22f;
        [SerializeField] private float _treeColliderUpOffset = 0.02f;
        [Range(0.05f, 1f)]   [SerializeField] private float _objectColliderWidthPct  = 0.7f;
        [Range(0.05f, 1f)]   [SerializeField] private float _objectColliderHeightPct = 0.4f;

        [Header("Spawn Rules")]
        [SerializeField] private bool _objectsOnlyOnGrass = false;
        [SerializeField] private int _clearRadiusAroundOrigin = 4;
        [SerializeField] private bool _restrictSpawnsToSpecificGrass = false;
        [SerializeField] private string[] _spawnOnlyOnGrassSpriteNames;

        [Header("Editor Preview")]
        [SerializeField] private bool _generateInEditMode = false;

        // ─── Singleton ────────────────────────────────────────────────────────────
        public static ProceduralGridGenerator Instance { get; private set; }

        // ─── Seed offset (driven by WorldSeedService) ─────────────────────────────
        // Mixed into every Perlin sample and integer hash function in this file.
        // Defaults to 0 so a world with no user-supplied seed reproduces the
        // exact pre-seed-generation terrain.
        private int _seedOffset;

        // ─── Persistent world data ────────────────────────────────────────────────
        // World-tile position → is grass? Never cleared; grows as player explores.
        private readonly Dictionary<Vector2Int, bool> _grassData = new Dictionary<Vector2Int, bool>();

        // World-tile position → grass GameObject (only for grass tiles).
        private readonly Dictionary<Vector2Int, GameObject> _grassTileObjects = new Dictionary<Vector2Int, GameObject>();

        // Puddles: set of world tiles occupied by water and their visual objects
        private readonly HashSet<Vector2Int> _puddleCells = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, GameObject> _puddleObjects = new Dictionary<Vector2Int, GameObject>();

        // World-tile position → decor GameObject (tree or small object). One per tile max.
        private readonly Dictionary<Vector2Int, GameObject> _decorObjects = new Dictionary<Vector2Int, GameObject>();

        // Which chunk coordinates have been generated (never removed).
        private readonly HashSet<Vector2Int> _generatedChunks = new HashSet<Vector2Int>();

        // Parent GameObjects per chunk for a clean hierarchy.
        private readonly Dictionary<Vector2Int, GameObject> _chunkContainers = new Dictionary<Vector2Int, GameObject>();

        // Track the chunk the player was in last frame to avoid redundant checks.
        private Vector2Int _lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);

        private bool _farmPlotGenerated = false;


        // ─── Unity lifecycle ──────────────────────────────────────────────────────

#if UNITY_EDITOR
        /// <summary>
        /// Returns true if the generator should be allowed to spawn chunks this frame.
        /// Always true during Play mode. In Edit mode, only true when the user has flipped
        /// _generateInEditMode on AND a Scene view is open — this prevents silent tile-spawn
        /// when someone opens the project for the first time and stares at an empty Hierarchy.
        /// </summary>
        private bool ShouldGenerateNow()
        {
            if (Application.isPlaying) return true;
            if (!_generateInEditMode) return false;
            return UnityEditor.SceneView.lastActiveSceneView != null;
        }
#else
        // Outside the editor the only "mode" is Play, so no gate is needed.
        private bool ShouldGenerateNow() => true;
#endif

#if UNITY_EDITOR
        /// <summary>
        /// Inspector-only hook. When the user toggles _generateInEditMode we either fan out
        /// an initial chunk ring right away (off → on) or tear down every spawned chunk
        /// (on → off), so the preview is responsive without waiting for the next player move.
        /// OnValidate runs in Edit mode only; in Play mode this is a no-op.
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            // Always rebuild weight cache first so edit-mode preview uses fresh values.
            RebuildBiomeWeights();
            if (_generateInEditMode)
            {
                if (WorldSeedService.Instance != null) _seedOffset = WorldSeedService.Instance.SeedOffset;
                if (_playerTransform == null) return;
                Vector2Int startChunk = WorldToChunk(_playerTransform.position);
                GenerateChunksAround(startChunk);
                _lastPlayerChunk = startChunk;
            }
            else
            {
                // NOTE: must be Destroy, NOT DestroyImmediate. Unity's runtime safety
                // check throws if you call DestroyImmediate inside OnValidate. In Edit
                // mode (the only mode this branch runs in, due to the isPlaying guard
                // above) Destroy tears the GameObject down synchronously anyway, so
                // behaviour matches DestroyImmediate. A future reader: please don't
                // "modernise" this back to DestroyImmediate — Unity will throw.
                foreach (var kvp in _chunkContainers)
                {
                    if (kvp.Value != null) Destroy(kvp.Value);
                }
                _chunkContainers.Clear();
                _grassData.Clear();
                _grassTileObjects.Clear();
                _puddleCells.Clear();
                _puddleObjects.Clear();
                _decorObjects.Clear();
                _generatedChunks.Clear();
                _lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
                _farmPlotGenerated = false;
            }
        }
#endif

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

            // Pull the active world seed offset before any field settle so Start()
            // is already driving off it. WorldSeedService self-bootstraps earlier
            // in the load order, so its Instance is always populated here; the null
            // guard is paranoia for hot-reload after domain reloads.
            if (WorldSeedService.Instance != null) _seedOffset = WorldSeedService.Instance.SeedOffset;

            // Pre-build the biome weight lookup so the first chunk generation is ready.
            RebuildBiomeWeights();

            // Default to grass_tile_19 if user enabled restriction but left list empty
            if (_restrictSpawnsToSpecificGrass && (_spawnOnlyOnGrassSpriteNames == null || _spawnOnlyOnGrassSpriteNames.Length == 0))
            {
                _spawnOnlyOnGrassSpriteNames = new[] { "grass_tile_19" };
            }
	
	            // Auto-locate the player if not assigned in the Inspector.
	            if (_playerTransform == null)
	            {
                // 1. Try by tag
                GameObject player = GameObject.FindWithTag("Player");

                // 2. Fall back to finding by name
                if (player == null)
                    player = GameObject.Find("Player");

                // 3. Fall back to finding the PlayerController component anywhere in the scene
                if (player == null)
                {
                    var controller = FindAnyObjectByType<Player.PlayerController>();
                    if (controller != null) player = controller.gameObject;
                }

                if (player != null)
                    _playerTransform = player.transform;
                else
                    Debug.LogWarning("[ProceduralGridGenerator] Could not find the player. " +
                                     "Assign _playerTransform in the Inspector or tag/name the player 'Player'.", this);
            }
        }

        private void OnEnable()
        {
            // Subscribe AFTER any FirstInstanceInstance checks so a duplicate
            // generator that gets destroyed in Awake never adds a zombie listener.
            if (Instance == this && WorldSeedService.Instance != null)
            {
                WorldSeedService.Instance.OnSeedChanged += HandleSeedChanged;
            }
        }

        private void OnDisable()
        {
            if (WorldSeedService.Instance != null)
            {
                WorldSeedService.Instance.OnSeedChanged -= HandleSeedChanged;
            }
        }

        private void HandleSeedChanged(int newSeed)
        {
            if (Instance != this) return;
            WorldSeedService service = WorldSeedService.Instance;
            if (service != null) _seedOffset = service.SeedOffset;
            Regenerate();
        }

        private void EnsurePlayerTransform()
        {
            if (_playerTransform != null) return;
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Player");
            if (player == null)
            {
                var controller = FindAnyObjectByType<Player.PlayerController>();
                if (controller != null) player = controller.gameObject;
            }
            if (player != null) _playerTransform = player.transform;
        }

        /// <summary>
        /// Destroys every generated chunk and rebuilds the world around the player
        /// using the currently-active seed. Called when the player picks a new seed
        /// at the World Setup panel or via the dev-console `seed <int>` command.
        /// </summary>
        public void Regenerate()
        {
            EnsurePlayerTransform();

            // Tear down the existing world so the new seed has something to write onto.
            if (_grassTilemap != null) _grassTilemap.ClearAllTiles();
            if (_dirtTilemap != null) _dirtTilemap.ClearAllTiles();

            foreach (var kvp in _chunkContainers)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            _chunkContainers.Clear();
            _grassData.Clear();
            _grassTileObjects.Clear();
            _puddleCells.Clear();
            _puddleObjects.Clear();
            _decorObjects.Clear();
            _generatedChunks.Clear();
            _lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
            _farmPlotGenerated = false;

            // Wipe the cross-chunk felled-tile memory so the new seed doesn't see
            // tiles the player cut under the previous seed.
            Willowstead.World.TreeChoppable.ResetFelledTiles();

            // Note: GridManager crops/tiled cells are intentionally NOT cleared on
            // regenerate — losing crops mid-play is hostile UX. The dev-console
            // `seed` command accepts this for testing; the World Setup panel only
            // appears on first launch (when no value is stored in PlayerPrefs), so
            // a regular player never sees their crops vanish.

            // Re-run the initial chunk fan-out + starter farm plot around player (or world center fallback).
            Vector3 centerPos = _playerTransform != null ? _playerTransform.position : Vector3.zero;
            Vector2Int startChunk = WorldToChunk(centerPos);
            GenerateChunksAround(startChunk);
            _lastPlayerChunk = startChunk;

            if (_preGenerateFarmPlot && !_farmPlotGenerated && GridManager.Instance != null)
            {
                _farmPlotGenerated = true;
                for (int x = -_farmPlotWidth / 2; x <= _farmPlotWidth / 2; x++)
                {
                    for (int y = -_farmPlotHeight / 2; y <= _farmPlotHeight / 2; y++)
                    {
                        GridManager.Instance.HoeTile(new Vector3Int(x, y, 0));
                    }
                }
            }
        }

        private void Start()
        {
            // Re-validate the offset in case OnEnable ran before WorldSeedService
            // finished its own Awake (e.g. domain-reloaded play-mode entry).
            if (WorldSeedService.Instance != null) _seedOffset = WorldSeedService.Instance.SeedOffset;

            // Edit-mode gate. If the user hasn't opted in (or no Scene view is open)
            // we exit early so opening the project for the first time doesn't spam
            // hundreds of preview tiles into the Hierarchy.
            if (!ShouldGenerateNow()) return;

            // Generate the initial batch of chunks around the player's starting position.
            if (_playerTransform != null)
            {
                Vector2Int startChunk = WorldToChunk(_playerTransform.position);
                GenerateChunksAround(startChunk);
                _lastPlayerChunk = startChunk;
            }

            // Pre-till the starting farm plot (once) — Play-mode only. GridManager
            // isn't initialised in Edit mode and writing hoed cells there would
            // persist into the saved scene file (which would be hostile UX).
            if (Application.isPlaying
                && _preGenerateFarmPlot
                && !_farmPlotGenerated
                && GridManager.Instance != null)
            {
                _farmPlotGenerated = true;
                for (int x = -_farmPlotWidth / 2; x <= _farmPlotWidth / 2; x++)
                {
                    for (int y = -_farmPlotHeight / 2; y <= _farmPlotHeight / 2; y++)
                    {
                        GridManager.Instance.HoeTile(new Vector3Int(x, y, 0));
                    }
                }
            }

            // Setup warnings only during Play mode — at edit time the user is
            // already looking at the Inspector and these lines are noise. They
            // also survive across recompiles via Application.isPlaying guard.
            if (Application.isPlaying)
            {
                if ((_treeSprites == null || _treeSprites.Length == 0) && (_objectSprites == null || _objectSprites.Length == 0))
                {
                    Debug.Log("[ProceduralGridGenerator] No decor sprites assigned. Drag slices from Assets/Sprites/Trees.png and Assets/Sprites/Objects.png into the Decor & Trees arrays on this component.");
                }
                else
                {
                    if (_treeSprites == null || _treeSprites.Length == 0)
                        Debug.Log("[ProceduralGridGenerator] Tree sprites array is empty. Assign sprites from your Trees.png tilesheet.");
                    if (_objectSprites == null || _objectSprites.Length == 0)
                        Debug.Log("[ProceduralGridGenerator] Object sprites array is empty. Assign sprites from your Objects.png tilesheet.");
                }
            }
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            // Edit-mode gate: never fan out new chunks unless the user opted in
            // AND a Scene view is present, so [ExecuteAlways] doesn't continuously
            // expand the preview just because someone re-imported the sprite atlas.
            if (!ShouldGenerateNow()) return;

            Vector2Int currentChunk = WorldToChunk(_playerTransform.position);
            if (currentChunk == _lastPlayerChunk) return;

            _lastPlayerChunk = currentChunk;
            GenerateChunksAround(currentChunk);
        }

        // ─── Chunk management ─────────────────────────────────────────────────────

        /// <summary>Converts a world-space position to a chunk coordinate.</summary>
        private Vector2Int WorldToChunk(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / _chunkSize),
                Mathf.FloorToInt(worldPos.y / _chunkSize)
            );
        }

        /// <summary>Generates all chunks within <see cref="_renderRadius"/> of <paramref name="center"/> that do not yet exist.</summary>
        private void GenerateChunksAround(Vector2Int center)
        {
            for (int cx = center.x - _renderRadius; cx <= center.x + _renderRadius; cx++)
            {
                for (int cy = center.y - _renderRadius; cy <= center.y + _renderRadius; cy++)
                {
                    Vector2Int chunkCoord = new Vector2Int(cx, cy);
                    if (!_generatedChunks.Contains(chunkCoord))
                    {
                        GenerateChunk(chunkCoord);
                    }
                }
            }
        }

        /// <summary>
        /// Generates a single chunk: samples noise, spawns dirt + grass tiles, and
        /// computes grass edge-transition sprites for every grass tile in the chunk.
        /// </summary>
        private void GenerateChunk(Vector2Int chunkCoord)
        {
            _generatedChunks.Add(chunkCoord);

            // Create a parent container for the chunk's tile objects.
            GameObject container = new GameObject($"Chunk_{chunkCoord.x}_{chunkCoord.y}");
            container.transform.parent = transform;
            _chunkContainers[chunkCoord] = container;

            // Edit-mode preview: mark the container non-persistent so all spawned
            // tiles (dirt, grass, puddles, trees, objects) are excluded from the
            // saved scene file. The chunks stay visible in the Hierarchy while
            // previewing, and an OnValidate toggle-off cleans them up.
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                container.hideFlags = HideFlags.DontSave;
            }
#endif

            int startX = chunkCoord.x * _chunkSize;
            int startY = chunkCoord.y * _chunkSize;

            // ── Pass 1: sample noise and populate _grassData ───────────────────
            for (int x = startX; x < startX + _chunkSize; x++)
            {
                for (int y = startY; y < startY + _chunkSize; y++)
                {
                    float noiseVal = Mathf.PerlinNoise((x + 1000 + _seedOffset) * _noiseScale, (y + 1000 + _seedOffset) * _noiseScale);
                    _grassData[new Vector2Int(x, y)] = noiseVal < _grassThreshold;
                }
            }

            // ── Pass 2: draw tiles to Tilemap (or fallback GameObjects) ───────
            bool useTilemaps = _grassTilemap != null || _dirtTilemap != null;

            for (int x = startX; x < startX + _chunkSize; x++)
            {
                for (int y = startY; y < startY + _chunkSize; y++)
                {
                    Vector2Int tilePos = new Vector2Int(x, y);
                    Vector3Int cellPos = new Vector3Int(x, y, 0);

                    if (useTilemaps)
                    {
                        // Dirt base layer
                        if (_dirtTilemap != null && _baseDirtTile != null)
                        {
                            _dirtTilemap.SetTile(cellPos, _baseDirtTile);
                        }

                        // Grass layer (with multi-shade/biome Rule Tile selection).
                        // Dirt-patch noise can override a grass tile back to bare dirt.
                        if (_grassData[tilePos] && _grassTilemap != null)
                        {
                            if (IsDirtPatchAt(x, y))
                            {
                                // Leave this tile as bare dirt — update grassData so
                                // adjacency checks (IsGrassAt, decor guards) stay consistent.
                                _grassData[tilePos] = false;
                            }
                            else
                            {
                                TileBase ruleTile = SelectGrassRuleTile(x, y);
                                if (ruleTile != null)
                                {
                                    _grassTilemap.SetTile(cellPos, ruleTile);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Legacy GameObject fallback
                        GameObject dirtTile = new GameObject($"DirtTile_{x}_{y}");
                        dirtTile.transform.parent = container.transform;
                        dirtTile.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f);
                        SpriteRenderer dirtSr = dirtTile.AddComponent<SpriteRenderer>();
                        dirtSr.sprite = GetDirtSprite(x, y);
                        dirtSr.sortingOrder = -32000;

                        if (_grassData[tilePos])
                        {
                            SpawnGrassTile(tilePos, container.transform);
                        }
                    }
                }
            }

            // ── Pass 3: resolve legacy grass edge-transition sprites (only when not using Tilemaps)
            if (!useTilemaps)
            {
                for (int x = startX; x < startX + _chunkSize; x++)
                {
                    for (int y = startY; y < startY + _chunkSize; y++)
                    {
                        Vector2Int tilePos = new Vector2Int(x, y);
                        if (_grassData[tilePos])
                        {
                            UpdateGrassTileVisual(tilePos);
                        }
                    }
                }
            }

	            // ── Pass 4: puddles/ponds first so decor can avoid them ────────────
	            SpawnPuddlesForChunk(chunkCoord, container.transform);

	            // ── Pass 5: rivers (before decor so trees don't land on water) ──────
	            if (_generateRivers) SpawnRiversForChunk(chunkCoord, container.transform);

	            // ── Pass 6: decor/trees ────────────────────────────────────────────
	            SpawnDecorForChunk(chunkCoord, container.transform);
	        }

        // ─── Tile visual helpers ──────────────────────────────────────────────────

        private void SpawnGrassTile(Vector2Int tilePos, Transform parent)
        {
            GameObject grassTile = new GameObject($"GrassTile_{tilePos.x}_{tilePos.y}");
            grassTile.transform.parent = parent;
            grassTile.transform.position = new Vector3(tilePos.x + 0.5f, tilePos.y + 0.5f, 0f);
            SpriteRenderer sr = grassTile.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -31900; // Always behind every world object, in front of dirt
            _grassTileObjects[tilePos] = grassTile;
        }

        private void UpdateGrassTileVisual(Vector2Int tilePos)
        {
            if (!_grassTileObjects.TryGetValue(tilePos, out GameObject grassTile) || grassTile == null)
                return;

            SpriteRenderer sr = grassTile.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            int x = tilePos.x;
            int y = tilePos.y;

            bool n  = IsGrassAt(x,     y + 1);
            bool s  = IsGrassAt(x,     y - 1);
            bool e  = IsGrassAt(x + 1, y    );
            bool w  = IsGrassAt(x - 1, y    );
            bool nw = IsGrassAt(x - 1, y + 1);
            bool ne = IsGrassAt(x + 1, y + 1);
            bool sw = IsGrassAt(x - 1, y - 1);
            bool se = IsGrassAt(x + 1, y - 1);

            sr.sprite = GetGrassSprite(sr, tilePos, out float rotation,
                                       n, s, e, w, nw, ne, sw, se);
            grassTile.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        }

        /// <summary>
        /// Returns whether a world tile is grass, sampling noise directly for tiles
        /// whose chunk has not been generated yet so borders are always seamless.
        /// </summary>
        public bool IsGrassAt(int worldX, int worldY)
        {
            if (_grassData.TryGetValue(new Vector2Int(worldX, worldY), out bool isGrass))
                return isGrass;

            // Chunk not generated yet — sample the same noise formula so the result
            // will match exactly when that chunk is eventually generated.
            float noiseVal = Mathf.PerlinNoise((worldX + 1000 + _seedOffset) * _noiseScale, (worldY + 1000 + _seedOffset) * _noiseScale);
            return noiseVal < _grassThreshold;
        }



	        // ─── Puddles / Ponds ────────────────────────────────────────────────────

	        private void SpawnPuddlesForChunk(Vector2Int chunkCoord, Transform parent)
	        {
	            // Determine active target tilemap (prefer dedicated water tilemap, fall back to grass tilemap if unassigned)
	            Tilemap targetTilemap = _waterTilemap != null ? _waterTilemap : _grassTilemap;
	            bool useRuleTile = targetTilemap != null && _waterRuleTile != null;
	            bool useFillSprites = _puddleFillSprites != null && _puddleFillSprites.Length > 0;
	            if (!useRuleTile && !useFillSprites) return;
	            int startX = chunkCoord.x * _chunkSize;
	            int startY = chunkCoord.y * _chunkSize;
	            GameObject puddleContainer = new GameObject($"Puddles_{chunkCoord.x}_{chunkCoord.y}");
	            puddleContainer.transform.parent = parent;

	            int pondsSeeded = 0;
	            for (int x = startX; x < startX + _chunkSize; x++)
	            {
	                for (int y = startY; y < startY + _chunkSize; y++)
	                {
	                    if (pondsSeeded >= _maxPondsPerChunk) return;
	                    Vector2Int pos = new Vector2Int(x, y);
	                    if (_puddleCells.Contains(pos)) continue; // Already water
	
	                    // Respect clear radius and tilled cells
	                    if (Mathf.Abs(x) <= _clearRadiusAroundOrigin && Mathf.Abs(y) <= _clearRadiusAroundOrigin) continue;
	                    if (GridManager.Instance != null && GridManager.Instance.IsCellTilled(new Vector3Int(x, y, 0))) continue;

	                    bool isGrass = _grassData.TryGetValue(pos, out bool g) && g;
	                    if (_puddlesOnlyOnGrass && !isGrass) continue;

	                    float chance = Deterministic01(x, y, 5557);
	                    if (chance < _puddleSeedDensity)
	                    {
	                        // Enforce minimum separation distance from any existing water tile (ponds or rivers)
	                        if (IsNearPuddle(pos, _minPondSeparation)) continue;

	                        int radius = Mathf.Clamp(_puddleMinRadius + DeterministicIndex(x, y, 7717, _puddleMaxRadius - _puddleMinRadius + 1), _puddleMinRadius, _puddleMaxRadius);
	                        // Enforce grass buffer: the (radius + buffer) disc must be entirely grass
	                        if (_enforceGrassBufferForPuddles && _puddleGrassBuffer > 0)
	                        {
	                            if (!DiscWithinGrass(pos, radius + _puddleGrassBuffer))
	                                continue;
	                        }
	                        FillPondDisc(pos, radius, puddleContainer.transform);
	                        pondsSeeded++;
	                    }
	                }
	            }
	        }

	        // ─── Rivers ─────────────────────────────────────────────────────────────

	        /// <summary>
	        /// Attempts to start a river originating in this chunk using a Perlin-steered
	        /// random walk. Rivers are stored in _puddleCells so decor avoids them.
	        /// </summary>
	        private void SpawnRiversForChunk(Vector2Int chunkCoord, Transform parent)
	        {
	            bool useRuleTile    = _waterTilemap != null && _waterRuleTile != null;
	            bool useFillSprites = _puddleFillSprites != null && _puddleFillSprites.Length > 0;
	            if (!useRuleTile && !useFillSprites) return;

	            // Deterministic per-chunk roll.
	            float chance = Deterministic01(chunkCoord.x, chunkCoord.y, 31337);
	            if (chance >= _riverChancePerChunk) return;

	            // Separation Check: Ensure no chunk within _minRiverChunkSeparation radius has also rolled to spawn a river.
	            // If another chunk in radius also rolled < _riverChancePerChunk, compare their chance values (lowest chance wins).
	            for (int dx = -_minRiverChunkSeparation; dx <= _minRiverChunkSeparation; dx++)
	            {
	                for (int dy = -_minRiverChunkSeparation; dy <= _minRiverChunkSeparation; dy++)
	                {
	                    if (dx == 0 && dy == 0) continue;
	                    Vector2Int neighborChunk = new Vector2Int(chunkCoord.x + dx, chunkCoord.y + dy);
	                    float neighborChance = Deterministic01(neighborChunk.x, neighborChunk.y, 31337);
	                    if (neighborChance < _riverChancePerChunk)
	                    {
	                        // Neighboring chunk also qualifies. If its chance is lower (or tied with lower hash), let neighbor take priority.
	                        if (neighborChance < chance) return;
	                    }
	                }
	            }

	            int startX = chunkCoord.x * _chunkSize + DeterministicIndex(chunkCoord.x, chunkCoord.y, 1009, _chunkSize);
	            int startY = chunkCoord.y * _chunkSize + DeterministicIndex(chunkCoord.x, chunkCoord.y, 2003, _chunkSize);

	            // Pick one of 4 cardinal directions (0=E, 1=W, 2=N, 3=S) for this river.
	            int dir = DeterministicIndex(chunkCoord.x, chunkCoord.y, 4441, 4);

	            // Primary step delta (one tile per step in the chosen direction).
	            int pdx = dir == 0 ? 1 : dir == 1 ? -1 : 0;
	            int pdy = dir == 2 ? 1 : dir == 3 ? -1 : 0;

	            // Width is painted in the perpendicular axis.
	            bool horizontal = (dir == 0 || dir == 1); // E or W → width goes N/S

	            float noiseBase = chunkCoord.x * 1.73f + chunkCoord.y * 2.41f + _seedOffset * 0.11f;
	            float cx = startX;
	            float cy = startY;

	            for (int step = 0; step < _riverLength; step++)
	            {
	                // Perlin drift in the perpendicular axis.
	                float noiseVal = Mathf.PerlinNoise(noiseBase + step * _riverWanderNoiseScale, 0.5f);
	                float drift    = (noiseVal * 2f - 1f) * _riverWanderStrength;

	                // Advance one tile in primary direction, drift in perpendicular.
	                if (horizontal)
	                {
	                    cx += pdx;
	                    cy += drift;
	                }
	                else
	                {
	                    cy += pdy;
	                    cx += drift;
	                }

	                int tx = Mathf.RoundToInt(cx);
	                int ty = Mathf.RoundToInt(cy);

	                if (Mathf.Abs(tx) <= _clearRadiusAroundOrigin && Mathf.Abs(ty) <= _clearRadiusAroundOrigin) continue;

	                // Paint width perpendicular to travel direction.
	                for (int w = -_riverHalfWidth; w <= _riverHalfWidth; w++)
	                {
	                    Vector2Int cell = horizontal
	                        ? new Vector2Int(tx, ty + w)   // E/W river — width goes North/South
	                        : new Vector2Int(tx + w, ty);  // N/S river — width goes East/West
	                    PlaceWaterCell(cell, parent, useRuleTile);
	                }
	            }
	        }

	        /// <summary>
	        /// Shared helper: registers a tile as water and either places the Water Rule
	        /// Tile on the water tilemap, or spawns a legacy sprite GameObject.
	        /// </summary>
	        private void PlaceWaterCell(Vector2Int p, Transform parent, bool useRuleTile)
	        {
	            if (_puddleCells.Contains(p)) return;
	            if (GridManager.Instance != null && GridManager.Instance.IsCellTilled(new Vector3Int(p.x, p.y, 0))) return;

	            _puddleCells.Add(p);

	            Tilemap targetTilemap = _waterTilemap != null ? _waterTilemap : _grassTilemap;
	            if (useRuleTile && targetTilemap != null)
	            {
	                targetTilemap.SetTile(new Vector3Int(p.x, p.y, 0), _waterRuleTile);
	                if (_puddlesBlockMovement)
	                {
	                    GameObject go = new GameObject($"River_{p.x}_{p.y}");
	                    go.transform.parent = parent;
	                    go.transform.position = new Vector3(p.x + 0.5f, p.y + 0.5f, 0f);
	                    _puddleObjects[p] = go;
	                    var bc = go.AddComponent<BoxCollider2D>();
	                    bc.size = new Vector2(0.92f, 0.92f);
	                    bc.isTrigger = false;
	                }
	            }
	            else if (_puddleFillSprites != null && _puddleFillSprites.Length > 0)
	            {
	                int idx = DeterministicIndex(p.x, p.y, 8839, _puddleFillSprites.Length);
	                GameObject go = new GameObject($"River_{p.x}_{p.y}");
	                go.transform.parent = parent;
	                go.transform.position = new Vector3(p.x + 0.5f, p.y + 0.5f, 0f);
	                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
	                sr.sprite = _puddleFillSprites[idx];
	                sr.sortingOrder = -31850;
	                _puddleObjects[p] = go;
	                if (_puddlesBlockMovement)
	                {
	                    var bc = go.AddComponent<BoxCollider2D>();
	                    bc.size = new Vector2(0.92f, 0.92f);
	                    bc.isTrigger = false;
	                }
	            }
	        }

	        private void FillPondDisc(Vector2Int center, int radius, Transform parent)
	        {
	            // First collect all cells in this pond using a noise-distorted radius
	            // to produce natural organic blob shapes instead of perfect circles.
	            System.Collections.Generic.List<Vector2Int> pondCells = new System.Collections.Generic.List<Vector2Int>();
	            int searchRadius = Mathf.CeilToInt(radius * (1f + _pondBlobStrength));
	            float noiseOffsetX = center.x * 0.317f + _seedOffset * 0.073f;
	            float noiseOffsetY = center.y * 0.317f + _seedOffset * 0.073f;

	            for (int dx = -searchRadius; dx <= searchRadius; dx++)
	            {
	                for (int dy = -searchRadius; dy <= searchRadius; dy++)
	                {
	                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
	                    if (dist < 0.001f)
	                    {
	                        // Always include center cell
	                    }
	                    else
	                    {
	                        // Sample Perlin noise in the direction of (dx, dy) to distort radius
	                        float angle = Mathf.Atan2(dy, dx);
	                        float noiseX = noiseOffsetX + Mathf.Cos(angle) * _pondBlobNoiseScale * radius;
	                        float noiseY = noiseOffsetY + Mathf.Sin(angle) * _pondBlobNoiseScale * radius;
	                        float noise = Mathf.PerlinNoise(noiseX, noiseY); // 0..1
	                        float effectiveRadius = radius * (1f + _pondBlobStrength * (noise * 2f - 1f));
	                        if (dist > effectiveRadius) continue;
	                    }

	                    Vector2Int p = new Vector2Int(center.x + dx, center.y + dy);
	                    if (_puddleCells.Contains(p)) continue;

	                    // Skip tilled cells and keep within grass if required
	                    if (GridManager.Instance != null && GridManager.Instance.IsCellTilled(new Vector3Int(p.x, p.y, 0))) continue;
	                    bool isGrass = _grassData.TryGetValue(p, out bool g) && g;
	                    if (_puddlesOnlyOnGrass && !isGrass) continue;
	                    pondCells.Add(p);
	                }
	            }

	            // Smooth the pond to avoid 1-tile spikes and 1-tile corridors that look bad with shoreline tiles
	            var pondSet = new System.Collections.Generic.HashSet<Vector2Int>(pondCells);
	            System.Collections.Generic.List<Vector2Int> toRemove = new System.Collections.Generic.List<Vector2Int>();
	            foreach (var p in pondSet)
	            {
	                bool n = pondSet.Contains(new Vector2Int(p.x, p.y + 1));
	                bool s = pondSet.Contains(new Vector2Int(p.x, p.y - 1));
	                bool e = pondSet.Contains(new Vector2Int(p.x + 1, p.y));
	                bool w = pondSet.Contains(new Vector2Int(p.x - 1, p.y));
	                int count = (n?1:0) + (s?1:0) + (e?1:0) + (w?1:0);
	                // Remove tips (<=1 neighbour) and single-tile corridors (NS only or EW only)
	                if (count <= 1 || (n && s && !e && !w) || (e && w && !n && !s))
	                {
	                    toRemove.Add(p);
	                }
	            }
	            for (int i = 0; i < toRemove.Count; i++) pondSet.Remove(toRemove[i]);
	            pondCells = new System.Collections.Generic.List<Vector2Int>(pondSet);

	            // Build a lookup that includes existing water plus this (smoothed) pond
	            System.Collections.Generic.HashSet<Vector2Int> combined = new System.Collections.Generic.HashSet<Vector2Int>(_puddleCells);
	            for (int i = 0; i < pondCells.Count; i++) combined.Add(pondCells[i]);

	            // Now spawn visuals for each new cell
	            Tilemap targetTilemap = _waterTilemap != null ? _waterTilemap : _grassTilemap;
	            bool useRuleTileInner = targetTilemap != null && _waterRuleTile != null;
	            for (int i = 0; i < pondCells.Count; i++)
	            {
	                Vector2Int p = pondCells[i];

	                _puddleCells.Add(p);

	                if (useRuleTileInner)
	                {
	                    targetTilemap.SetTile(new Vector3Int(p.x, p.y, 0), _waterRuleTile);

	                    if (_puddlesBlockMovement)
	                    {
	                        // Spawn a minimal collider-only GO — no SpriteRenderer needed.
	                        GameObject go = new GameObject($"Puddle_{p.x}_{p.y}");
	                        go.transform.parent = parent;
	                        go.transform.position = new Vector3(p.x + 0.5f, p.y + 0.5f, 0f);
	                        _puddleObjects[p] = go;
	                        var bc = go.AddComponent<BoxCollider2D>();
	                        bc.size = new Vector2(0.92f, 0.92f);
	                        bc.isTrigger = false;
	                    }
	                }
	                else
	                {
	                    // Legacy sprite path: compute neighbour flags and pick the best fill sprite.
	                    bool n = combined.Contains(new Vector2Int(p.x, p.y + 1));
	                    bool s = combined.Contains(new Vector2Int(p.x, p.y - 1));
	                    bool e = combined.Contains(new Vector2Int(p.x + 1, p.y));
	                    bool w = combined.Contains(new Vector2Int(p.x - 1, p.y));
	                    bool nw = combined.Contains(new Vector2Int(p.x - 1, p.y + 1));
	                    bool ne = combined.Contains(new Vector2Int(p.x + 1, p.y + 1));
	                    bool sw = combined.Contains(new Vector2Int(p.x - 1, p.y - 1));
	                    bool se = combined.Contains(new Vector2Int(p.x + 1, p.y - 1));

	                    Sprite sprite = SelectPuddleSprite(p, n, s, e, w, nw, ne, sw, se);
	                    GameObject go = new GameObject($"Puddle_{p.x}_{p.y}");
	                    go.transform.parent = parent;
	                    go.transform.position = new Vector3(p.x + 0.5f, p.y + 0.5f, 0f);
	                    SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
	                    sr.sprite = sprite;
	                    sr.sortingOrder = -31850;
	                    _puddleObjects[p] = go;

	                    if (_puddlesBlockMovement)
	                    {
	                        var bc = go.AddComponent<BoxCollider2D>();
	                        bc.size = new Vector2(0.92f, 0.92f);
	                        bc.isTrigger = false;
	                    }
	                }
	            }
	        }

	        private Sprite SelectPuddleSprite(Vector2Int pos, bool n, bool s, bool e, bool w, bool nw, bool ne, bool sw, bool se)
	        {
	            if (_puddleFillSprites == null || _puddleFillSprites.Length == 0) return null;
	            int idx = DeterministicIndex(pos.x, pos.y, 8839, _puddleFillSprites.Length);
	            return _puddleFillSprites[idx];
	        }

	        private bool IsPuddleAt(Vector2Int pos) => _puddleCells.Contains(pos);
	        public bool HasPuddleAt(Vector3Int cellPos)
	        {
	            return _puddleCells.Contains(new Vector2Int(cellPos.x, cellPos.y));
	        }
	        private bool IsNearPuddle(Vector2Int pos, int radius)
	        {
	            for (int dx = -radius; dx <= radius; dx++)
	            {
	                for (int dy = -radius; dy <= radius; dy++)
	                {
	                    if (_puddleCells.Contains(new Vector2Int(pos.x + dx, pos.y + dy))) return true;
	                }
	            }
	            return false;
	        }
	        private bool DiscWithinGrass(Vector2Int center, int radius)
	        {
	            int r2 = radius * radius;
	            for (int dx = -radius; dx <= radius; dx++)
	            {
	                for (int dy = -radius; dy <= radius; dy++)
	                {
	                    if (dx * dx + dy * dy > r2) continue;
	                    int wx = center.x + dx;
	                    int wy = center.y + dy;
	                    if (!IsGrassAt(wx, wy)) return false;
	                }
	            }
	            return true;
	        }

	        // ─── Decor / Trees spawning ──────────────────────────────────────────────

	        private void SpawnDecorForChunk(Vector2Int chunkCoord, Transform parent)
	        {
	            int startX = chunkCoord.x * _chunkSize;
	            int startY = chunkCoord.y * _chunkSize;

	            // Keep a neat hierarchy
	            GameObject decorContainer = new GameObject($"Decor_{chunkCoord.x}_{chunkCoord.y}");
	            decorContainer.transform.parent = parent;

	            int treesSpawned = 0;
	            int objectsSpawned = 0;

	            // First pass: decide and spawn trees; also collect their world positions for separation
	            System.Collections.Generic.List<Vector2> treePositions = new System.Collections.Generic.List<Vector2>();

	            for (int x = startX; x < startX + _chunkSize; x++)
	            {
	                for (int y = startY; y < startY + _chunkSize; y++)
	                {
	                    Vector2Int tilePos = new Vector2Int(x, y);
	                    Vector3Int cellPos = new Vector3Int(x, y, 0);

	                    // Reserve the starter farm zone
	                    if (Mathf.Abs(x) <= _clearRadiusAroundOrigin && Mathf.Abs(y) <= _clearRadiusAroundOrigin)
	                        continue;

	                    // Skip if something already occupies this cell or is a puddle
	                    if (_decorObjects.ContainsKey(tilePos) || IsPuddleAt(tilePos)) continue;

                    // Skip tilled soil (or about-to-be tilled) areas
                    if (GridManager.Instance != null && GridManager.Instance.IsCellTilled(cellPos))
                        continue;

                    // Double-check: trees/objects can NEVER land on a grass-edge transition tile.
                    if (!IsInteriorGrassTile(tilePos)) continue;

                    // Use already-sampled grass data for this chunk to avoid any mismatch
                    bool isGrass = _grassData.TryGetValue(tilePos, out bool g) && g;
                    bool allowedGrassSprite = !_restrictSpawnsToSpecificGrass || IsAllowedGrassSpriteAt(tilePos);                    // Trees: only on allowed grass tiles and not too close to puddles
                    if (isGrass && allowedGrassSprite && _treeSprites != null && _treeSprites.Length > 0)
                    {
                        if (treePositions.Count >= _maxTreesPerChunk) continue;
                        if (IsNearPuddle(tilePos, _minDecorDistanceFromPuddle)) continue;
                        if (TreeChoppable.IsTileFelled(tilePos)) continue; // Player cut this tree already; stay empty
                        float tChance = Deterministic01(x, y, 8617);
	                        if (tChance < _treeDensity)
	                        {
	                            // Note: do not spawn yet; collect position first
	                            Vector2 jitter = DeterministicJitter(x, y, 7331, _jitterRange);
	                            Vector2 pos = new Vector2(x + 0.5f + jitter.x, y + 0.5f + jitter.y);
	                            treePositions.Add(pos);
	                        }
	                    }
	                }
	            }

	            // Now actually spawn trees (to keep deterministic separation predictable within chunk)
	            int idx = 0;
	            for (int x = startX; x < startX + _chunkSize; x++)
	            {
	                for (int y = startY; y < startY + _chunkSize; y++)
	                {                    Vector2Int tilePos = new Vector2Int(x, y);
                    if (_decorObjects.ContainsKey(tilePos) || IsPuddleAt(tilePos)) continue;
                    if (!IsInteriorGrassTile(tilePos)) continue;

                    bool isGrass = _grassData.TryGetValue(tilePos, out bool g) && g;
                    bool allowedGrassSprite = !_restrictSpawnsToSpecificGrass || IsAllowedGrassSpriteAt(tilePos);                    if (!isGrass || !allowedGrassSprite) continue;
                    if (IsNearPuddle(tilePos, _minDecorDistanceFromPuddle)) continue;
                    if (TreeChoppable.IsTileFelled(tilePos)) continue; // Mirror the first-loop skip

                    float tChance = Deterministic01(x, y, 8617);
                    if (tChance < _treeDensity && treesSpawned < _maxTreesPerChunk)
                    {
                        if (idx >= treePositions.Count) break;
                        Vector2 treePos = treePositions[idx++];
                        var go = SpawnTree(treePos, x, y, decorContainer.transform);
                        _decorObjects[tilePos] = go;
                        treesSpawned++;
                    }
	                }
	            }

	            // Second pass: objects with separation from trees
	            for (int x = startX; x < startX + _chunkSize; x++)
	            {
	                for (int y = startY; y < startY + _chunkSize; y++)
	                {
	                    Vector2Int tilePos = new Vector2Int(x, y);
	                    Vector3Int cellPos = new Vector3Int(x, y, 0);

                    if (Mathf.Abs(x) <= _clearRadiusAroundOrigin && Mathf.Abs(y) <= _clearRadiusAroundOrigin)
                        continue;

                    // Belt-and-braces: objects can NEVER be placed on water (puddle)
                    // — loop 1 and loop 2 already guard this, but loop 3 was missing it.
                    if (_decorObjects.ContainsKey(tilePos) || IsPuddleAt(tilePos)) continue;

                    if (GridManager.Instance != null && GridManager.Instance.IsCellTilled(cellPos))
                        continue;

                    // Double-check: trees/objects can NEVER land on a grass-edge transition tile.
                    if (!IsInteriorGrassTile(tilePos)) continue;

                    bool isGrass = _grassData.TryGetValue(tilePos, out bool g) && g;
                    bool allowedGrassSprite = !_restrictSpawnsToSpecificGrass || IsAllowedGrassSpriteAt(tilePos);
                    // _objectsOnlyOnGrass toggles the grass requirement: when OFF, small
                    // object props may also appear on bare-dirt tiles. The grass-sprite-
                    // name whitelist still gates grass spawns; dirt has no sprite to compare
                    // against so it bypasses the whitelist (intentional).
                    bool objectsPassTile = !_objectsOnlyOnGrass || isGrass;
                    bool objectsPassSprite = isGrass
                        ? allowedGrassSprite
                        : !_restrictSpawnsToSpecificGrass;

                    if (objectsPassTile && objectsPassSprite && _objectSprites != null && _objectSprites.Length > 0 && objectsSpawned < _maxObjectsPerChunk)
	                    {
	                        if (IsNearPuddle(tilePos, _minDecorDistanceFromPuddle)) continue;
	                        float oChance = Deterministic01(x, y, 19211);
	                        if (oChance < _objectDensity)
	                        {
	                            // Check separation from any tree in this chunk
	                            Vector2 jitter = DeterministicJitter(x, y, 1129, _jitterRange * 0.7f);
	                            Vector2 objPos = new Vector2(x + 0.5f + jitter.x, y + 0.5f + jitter.y);
	                            Vector2Int objCell = new Vector2Int(Mathf.FloorToInt(objPos.x), Mathf.FloorToInt(objPos.y));
	                            if (IsPuddleAt(objCell) || IsNearPuddle(objCell, _minDecorDistanceFromPuddle)) continue;
	                            bool tooClose = false;
	                            for (int i = 0; i < treePositions.Count; i++)
	                            {
	                                if (Vector2.SqrMagnitude(objPos - treePositions[i]) < _minObjectTreeSeparation * _minObjectTreeSeparation)
	                                {
	                                    tooClose = true;
	                                    break;
	                                }
	                            }
	                            if (tooClose) continue;
	                            var go = SpawnObject(objPos, x, y, decorContainer.transform);
	                            _decorObjects[tilePos] = go;
	                            objectsSpawned++;
	                        }
	                    }
	                }
	            }
	        }

	        private GameObject SpawnTree(Vector2 position, int wx, int wy, Transform parent)
	        {
	            int index = DeterministicIndex(wx, wy, 4243, _treeSprites.Length);
	            Sprite sprite = _treeSprites[index];

	            GameObject go = new GameObject($"Tree_{wx}_{wy}");
	            go.transform.parent = parent;
	            go.transform.position = new Vector3(position.x, position.y, 0f);

	            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
	            sr.sprite = sprite;
	            // Sort by the base of the sprite (bounds.min.y) to keep layering correct even if pivot isn't set
	            float baseY = (sr != null && sr.sprite != null) ? sr.bounds.min.y : position.y;
	            sr.sortingOrder = Mathf.RoundToInt(-baseY * 100) - 1; // Slightly behind player at same Y

	            // Add a small base collider so the player collides with trunk, not canopy
	            var bc = go.AddComponent<BoxCollider2D>();
	            if (sr != null && sr.sprite != null)
	            {
	                // Use renderer bounds (world space) so pivot differences don't matter
	                Bounds b = sr.bounds;
	                float w = Mathf.Max(0.06f, b.size.x * _treeColliderWidthPct);
	                float h = Mathf.Max(0.06f, b.size.y * _treeColliderHeightPct);
	                float centerWorldY = b.min.y + h * 0.5f + _treeColliderUpOffset;
	                bc.size = new Vector2(w, h);
	                // Convert world Y to local offset
	                bc.offset = new Vector2(0f, centerWorldY - go.transform.position.y);
	            }
	            bc.isTrigger = false;

            // Occlusion fade when player walks "behind"
            var occluder = go.AddComponent<TreeOccluder>();
            occluder.InitializeAuto();

            // Chop-down behavior: shakes on hit, yields randomised logs after N chops,
            // and remembers this tile as felled so chunk reloads don't respawn it.
            var choppable = go.AddComponent<TreeChoppable>();
            choppable.Initialize(new Vector2Int(wx, wy));

            return go;
	        }

	        private GameObject SpawnObject(Vector2 position, int wx, int wy, Transform parent)
	        {
	            int index = DeterministicIndex(wx, wy, 991, _objectSprites.Length);
	            Sprite sprite = _objectSprites[index];

	            GameObject go = new GameObject($"Obj_{wx}_{wy}");
	            go.transform.parent = parent;
	            go.transform.position = new Vector3(position.x, position.y, 0f);

	            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
	            sr.sprite = sprite;
	            float baseY = (sr != null && sr.sprite != null) ? sr.bounds.min.y : position.y;
	            sr.sortingOrder = Mathf.RoundToInt(-baseY * 100) - 2; // A touch further back than trees

	            // Add collider for physical collision
            var bc = go.AddComponent<BoxCollider2D>();
            if (sr != null && sr.sprite != null)
            {
                Bounds b = sr.bounds; // world space
                float w = Mathf.Max(0.06f, b.size.x * _objectColliderWidthPct);
                float h = Mathf.Max(0.06f, b.size.y * _objectColliderHeightPct);
                float centerWorldY = b.min.y + h * 0.5f;
                bc.size = new Vector2(w, h);
                bc.offset = new Vector2(0f, centerWorldY - go.transform.position.y);
            }
            bc.isTrigger = false;

            return go;
        }

        private bool IsAllowedGrassSpriteAt(Vector2Int tilePos)
        {
            if (_spawnOnlyOnGrassSpriteNames == null || _spawnOnlyOnGrassSpriteNames.Length == 0) return true;

            Sprite tileSprite = null;
            if (_grassTilemap != null)
            {
                tileSprite = _grassTilemap.GetSprite(new Vector3Int(tilePos.x, tilePos.y, 0));
            }
            else if (_grassTileObjects.TryGetValue(tilePos, out var grassGo) && grassGo != null)
            {
                var sr = grassGo.GetComponent<SpriteRenderer>();
                if (sr != null) tileSprite = sr.sprite;
            }

            if (tileSprite == null) return false;

            string name = tileSprite.name;
            for (int i = 0; i < _spawnOnlyOnGrassSpriteNames.Length; i++)
            {
                if (name == _spawnOnlyOnGrassSpriteNames[i]) return true;
            }
            return false;
        }

        /// <summary>
        /// Belt-and-braces edge-tile filter: returns true only when the tile is grass
        /// AND every cardinal neighbour (N, S, E, W) is also grass. This guarantees
        /// trees and objects can never land on grass-edge transition tiles (where
        /// grass meets dirt on at least one side), independent of sprite-name lists
        /// or the `_restrictSpawnsToSpecificGrass` toggle. <see cref="IsGrassAt"/>
        /// samples noise directly when a neighbour chunk hasn't been generated yet,
        /// so the result stays accurate as the world expands.
        /// </summary>
        private bool IsInteriorGrassTile(Vector2Int tilePos)
        {
            if (!_grassData.TryGetValue(tilePos, out bool isGrass) || !isGrass)
                return false;

            return IsGrassAt(tilePos.x,     tilePos.y + 1)
                && IsGrassAt(tilePos.x,     tilePos.y - 1)
                && IsGrassAt(tilePos.x + 1, tilePos.y    )
                && IsGrassAt(tilePos.x - 1, tilePos.y    );
        }

	        // All three hash helpers read _seedOffset so two seeds produce visibly
	        // different decor / densities for the same (x,y). They remain private
	        // but are instance methods now; nothing outside this file called them.

	        private float Deterministic01(int x, int y, int salt)
	        {
	            // Fast 2D int hash -> [0,1). Add the seed offset into BOTH position
	            // components so flipping the seed has equal effect on every tile.
	            uint h = (uint)((x + _seedOffset) * 374761393
	                          + (y + _seedOffset) * 668265263
	                          + salt * 1442695040888963407L);
	            h = (h ^ (h >> 13)) * 1274126177u;
	            h ^= (h >> 16);
	            return (h & 0xFFFFFF) / 16777216f; // 24-bit mantissa
	        }

	        private int DeterministicIndex(int x, int y, int salt, int length)
	        {
	            if (length <= 0) return 0;
	            uint h = (uint)((x + _seedOffset) * 1103515245
	                          + (y + _seedOffset) * 12345
	                          + salt * 2654435761u);
	            h ^= (h >> 16);
	            return (int)(h % (uint)length);
	        }

	        private Vector2 DeterministicJitter(int x, int y, int salt, float range)
	        {
	            float jx = Deterministic01(x, y, salt) * 2f - 1f;
	            float jy = Deterministic01(x, y, salt * 3 + 17) * 2f - 1f;
	            return new Vector2(jx, jy) * range;
	        }

	        private void RemoveDecorAt(Vector2Int pos)
	        {
	            if (_decorObjects.TryGetValue(pos, out var go) && go != null)
	            {
	                Destroy(go);
	            }
	            _decorObjects.Remove(pos);
	        }

        /// <summary>
        /// Rebuilds the cumulative-weight lookup table from <see cref="_grassBiomes"/>.
        /// Called once in Awake/OnValidate — O(N) where N = number of biome entries.
        /// </summary>
        private void RebuildBiomeWeights()
        {
            if (_grassBiomes == null || _grassBiomes.Length == 0)
            {
                _biomeCumulativeWeights = null;
                return;
            }

            float total = 0f;
            for (int i = 0; i < _grassBiomes.Length; i++)
                total += Mathf.Max(0f, _grassBiomes[i].weight);

            // Guard: if all weights are zero treat them as equal.
            if (total <= 0f)
            {
                total = _grassBiomes.Length;
                _biomeCumulativeWeights = new float[_grassBiomes.Length];
                for (int i = 0; i < _grassBiomes.Length; i++)
                    _biomeCumulativeWeights[i] = (i + 1f) / _grassBiomes.Length;
                return;
            }

            _biomeCumulativeWeights = new float[_grassBiomes.Length];
            float running = 0f;
            for (int i = 0; i < _grassBiomes.Length; i++)
            {
                running += Mathf.Max(0f, _grassBiomes[i].weight);
                _biomeCumulativeWeights[i] = running / total;
            }
            // Clamp last entry to exactly 1 to avoid float drift.
            _biomeCumulativeWeights[_grassBiomes.Length - 1] = 1f;
        }

        private TileBase SelectGrassRuleTile(int x, int y)
        {
            if (_grassBiomes == null || _grassBiomes.Length == 0) return null;
            if (_grassBiomes.Length == 1) return _grassBiomes[0].tile;

            // Secondary biomes (e.g. GrassRuleTile 3) are restricted to interior grass tiles.
            // Edge tiles that border dirt or water must always use the primary base grass (element 0)
            // so Rule Tile edge transitions remain clean and don't mix tile set borders.
            if (!IsInteriorGrassTile(new Vector2Int(x, y)))
            {
                return _grassBiomes[0].tile;
            }

            // Ensure the weight table is ready (may be null after domain reload).
            if (_biomeCumulativeWeights == null || _biomeCumulativeWeights.Length != _grassBiomes.Length)
                RebuildBiomeWeights();

            // Sample secondary noise for biome selection.
            // Perlin noise clusters around 0.4–0.6 so remap [0.15, 0.85] → [0, 1]
            // for a more uniform distribution before walking the CDF.
            float raw = Mathf.PerlinNoise((x + 5000 + _seedOffset) * _biomeNoiseScale,
                                          (y + 5000 + _seedOffset) * _biomeNoiseScale);
            float t = Mathf.Clamp01(Mathf.InverseLerp(0.15f, 0.85f, raw));

            // Walk the cumulative-weight table to find which biome owns this value.
            for (int i = 0; i < _biomeCumulativeWeights.Length; i++)
            {
                if (t <= _biomeCumulativeWeights[i])
                    return _grassBiomes[i].tile;
            }
            return _grassBiomes[_grassBiomes.Length - 1].tile;
        }

        /// <summary>
        /// Returns true if a grass tile should be suppressed and left as bare dirt
        /// due to the dirt-patch noise layer. Sampled with a different offset and
        /// scale from the main grass noise so the two patterns are independent.
        /// </summary>
        private bool IsDirtPatchAt(int x, int y)
        {
            if (!_generateDirtPatches) return false;
            float val = Mathf.PerlinNoise((x + 3700 + _seedOffset) * _dirtPatchNoiseScale,
                                          (y + 3700 + _seedOffset) * _dirtPatchNoiseScale);
            return val < _dirtPatchThreshold;
        }

        // ─── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Clears the grass at <paramref name="cellPos"/>, converting it to bare dirt
        /// and updating adjacent grass edge transitions. Called by GridManager when
        /// the player hoes a tile.
        /// </summary>
        public bool ClearGrassAt(Vector3Int cellPos)
        {
            Vector2Int pos = new Vector2Int(cellPos.x, cellPos.y);

            if (!_grassData.TryGetValue(pos, out bool isGrass) || !isGrass)
                return false;

            _grassData[pos] = false;

            // Remove any decor on this tile now that it's dirt/tilled
            RemoveDecorAt(pos);

            // Clear modern Tilemap tile
            if (_grassTilemap != null)
            {
                _grassTilemap.SetTile(cellPos, null);
            }

            // Clear legacy GameObject tile if present
            if (_grassTileObjects.TryGetValue(pos, out GameObject grassTile))
            {
                if (grassTile != null) Destroy(grassTile);
                _grassTileObjects.Remove(pos);
            }
	
	            // Refresh the 8 surrounding neighbours so their edge sprites blend correctly
            // around the newly exposed dirt tile.
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Vector2Int neighbor = new Vector2Int(pos.x + dx, pos.y + dy);
                    if (_grassData.TryGetValue(neighbor, out bool neighborGrass) && neighborGrass)
                    {
                        UpdateGrassTileVisual(neighbor);
                    }
                }
            }

            return true;
        }

        // ─── Sprite selection (legacy GameObject fallback — unused when Tilemaps are assigned) ──

        /// <summary>
        /// Legacy fallback: selects a grass sprite for the old GameObject-based renderer.
        /// Not called when a Tilemap is assigned; Rule Tiles handle blending automatically.
        /// </summary>
        private Sprite GetGrassSprite(SpriteRenderer sr, Vector2Int pos, out float rotation,
                                      bool n, bool s, bool e, bool w,
                                      bool nw, bool ne, bool sw, bool se)
        {
            sr.flipX = false;
            sr.flipY = false;
            rotation = 0f;
            return null;
        }

        /// <summary>Legacy fallback: returns null — Rule Tiles handle all grass visuals.</summary>
        private Sprite BaseGrass(Vector2Int pos) => null;

        /// <summary>Legacy fallback: returns null — Rule Tiles handle all dirt visuals.</summary>
        private Sprite GetDirtSprite(int worldX, int worldY) => null;
    }
}
