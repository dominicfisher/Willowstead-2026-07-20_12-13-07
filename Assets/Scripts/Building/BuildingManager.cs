using System;
using System.Collections.Generic;
using UnityEngine;
using Willowstead.Player;

namespace Willowstead.Building
{
    public enum StructureType
    {
        None = 0,
        WoodWall = 1,
        WoodFloor = 2,
        WoodDoor = 3,
        StoneWall = 4,
        StoneFloor = 5
    }

    [Serializable]
    public class PlacedStructure
    {
        public Vector3Int cell;
        public StructureType structureType;
        public int maxHealth = 100;
        public int currentHealth = 100;
        public bool isOpen = false; // For doors
    }

    /// <summary>
    /// RimWorld-style modular grid building system.
    /// Supports placing walls, floors, and doors on the world coordinate grid.
    /// Connects with save/load, inventory material costs (e.g. Logs/Stone), and collision.
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        private readonly Dictionary<Vector3Int, PlacedStructure> _structures = new Dictionary<Vector3Int, PlacedStructure>();
        private readonly Dictionary<Vector3Int, GameObject> _structureObjects = new Dictionary<Vector3Int, GameObject>();

        public event Action OnStructuresChanged;

        private static Sprite _woodWallSprite;
        private static Sprite _woodFloorSprite;
        private static Sprite _woodDoorClosedSprite;
        private static Sprite _woodDoorOpenSprite;
        private static Sprite _stoneWallSprite;
        private static Sprite _stoneFloorSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[BuildingManager]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<BuildingManager>();
        }

        private void Awake()
        {
            if (Instance == null || Instance == this)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            GeneratePlaceholderSprites();
        }

        private void GeneratePlaceholderSprites()
        {
            if (_woodWallSprite == null) _woodWallSprite = CreatePixelTileSprite(new Color(0.48f, 0.32f, 0.20f), new Color(0.32f, 0.20f, 0.12f), true);
            if (_woodFloorSprite == null) _woodFloorSprite = CreatePixelTileSprite(new Color(0.72f, 0.54f, 0.38f), new Color(0.58f, 0.42f, 0.28f), false);
            if (_woodDoorClosedSprite == null) _woodDoorClosedSprite = CreateDoorSprite(false);
            if (_woodDoorOpenSprite == null) _woodDoorOpenSprite = CreateDoorSprite(true);
            if (_stoneWallSprite == null) _stoneWallSprite = CreatePixelTileSprite(new Color(0.55f, 0.58f, 0.62f), new Color(0.38f, 0.40f, 0.44f), true);
            if (_stoneFloorSprite == null) _stoneFloorSprite = CreatePixelTileSprite(new Color(0.75f, 0.78f, 0.80f), new Color(0.60f, 0.62f, 0.65f), false);
        }

        private Sprite CreatePixelTileSprite(Color main, Color border, bool isWall)
        {
            int size = 16;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = (x == 0 || x == size - 1 || y == 0 || y == size - 1);
                    if (isWall)
                    {
                        // Planks / brick pattern
                        bool plankLine = (y == 5 || y == 10);
                        tex.SetPixel(x, y, (isBorder || plankLine) ? border : main);
                    }
                    else
                    {
                        // Clean Floor tiles
                        bool floorLine = (x == 0 || y == 0);
                        tex.SetPixel(x, y, floorLine ? border : main);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }

        private Sprite CreateDoorSprite(bool open)
        {
            int size = 16;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;
            Color wood = new Color(0.62f, 0.42f, 0.28f);
            Color darkWood = new Color(0.35f, 0.22f, 0.12f);
            Color brass = new Color(0.95f, 0.82f, 0.35f);
            Color transparent = new Color(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (open)
                    {
                        // Door open on side
                        if (x <= 3 || x >= size - 4)
                            tex.SetPixel(x, y, darkWood);
                        else
                            tex.SetPixel(x, y, transparent);
                    }
                    else
                    {
                        // Closed door with handle
                        bool border = (x == 0 || x == size - 1 || y == 0 || y == size - 1);
                        if (x == 11 && y == 7) tex.SetPixel(x, y, brass);
                        else tex.SetPixel(x, y, border ? darkWood : wood);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }

        public bool HasStructureAt(Vector3Int cell) => _structures.ContainsKey(cell);

        public PlacedStructure GetStructureAt(Vector3Int cell)
        {
            _structures.TryGetValue(cell, out var s);
            return s;
        }

        public bool CanPlaceStructure(Vector3Int cell, StructureType type)
        {
            if (type == StructureType.None) return false;
            if (_structures.ContainsKey(cell)) return false;

            // Cannot build on top of puddles or water
            if (World.ProceduralGridGenerator.Instance != null && World.ProceduralGridGenerator.Instance.HasPuddleAt(cell))
                return false;

            // Check if player has required material
            int cost = GetMaterialCost(type, out string itemReq);
            if (cost > 0 && InventoryManager.Instance != null)
            {
                if (InventoryManager.Instance.GetItemCount(itemReq) < cost) return false;
            }

            return true;
        }

        public int GetMaterialCost(StructureType type, out string itemName)
        {
            switch (type)
            {
                case StructureType.WoodWall:
                    itemName = "Log";
                    return 2;
                case StructureType.WoodFloor:
                    itemName = "Log";
                    return 1;
                case StructureType.WoodDoor:
                    itemName = "Log";
                    return 3;
                case StructureType.StoneWall:
                    itemName = "Stone";
                    return 2;
                case StructureType.StoneFloor:
                    itemName = "Stone";
                    return 1;
                default:
                    itemName = "";
                    return 0;
            }
        }

        public bool BuildStructure(Vector3Int cell, StructureType type)
        {
            if (!CanPlaceStructure(cell, type)) return false;

            int cost = GetMaterialCost(type, out string itemReq);
            if (cost > 0 && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.RemoveItem(itemReq, cost);
            }

            PlacedStructure structure = new PlacedStructure
            {
                cell = cell,
                structureType = type,
                maxHealth = 100,
                currentHealth = 100,
                isOpen = false
            };

            _structures[cell] = structure;
            SpawnStructureObject(structure);

            OnStructuresChanged?.Invoke();
            return true;
        }

        public bool DemolishStructure(Vector3Int cell)
        {
            if (!_structures.TryGetValue(cell, out var structure)) return false;

            // Refund 50% materials
            int cost = GetMaterialCost(structure.structureType, out string itemReq);
            int refund = Mathf.Max(1, cost / 2);
            if (refund > 0 && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddItem(itemReq, refund);
            }

            if (_structureObjects.TryGetValue(cell, out var go) && go != null)
            {
                Destroy(go);
            }
            _structureObjects.Remove(cell);
            _structures.Remove(cell);

            OnStructuresChanged?.Invoke();
            return true;
        }

        public bool InteractStructure(Vector3Int cell)
        {
            if (!_structures.TryGetValue(cell, out var structure)) return false;

            if (structure.structureType == StructureType.WoodDoor)
            {
                structure.isOpen = !structure.isOpen;
                UpdateStructureVisual(cell);
                return true;
            }

            return false;
        }

        private void SpawnStructureObject(PlacedStructure structure)
        {
            Vector3 worldPos = new Vector3(structure.cell.x + 0.5f, structure.cell.y + 0.5f, 0f);
            GameObject go = new GameObject($"Structure_{structure.structureType}_{structure.cell.x}_{structure.cell.y}");
            go.transform.position = worldPos;
            go.transform.SetParent(transform, true);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSpriteFor(structure);

            // Sorting layers: Floors are behind player/decor (-31800), Walls and Doors are sorted by Y coordinate
            if (structure.structureType == StructureType.WoodFloor || structure.structureType == StructureType.StoneFloor)
            {
                sr.sortingOrder = -31800;
            }
            else
            {
                sr.sortingOrder = Mathf.RoundToInt(-worldPos.y * 100);
                var bc = go.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(1.0f, 1.0f);
                bc.isTrigger = structure.isOpen;
            }

            _structureObjects[structure.cell] = go;
        }

        private void UpdateStructureVisual(Vector3Int cell)
        {
            if (!_structures.TryGetValue(cell, out var structure)) return;
            if (!_structureObjects.TryGetValue(cell, out var go) || go == null) return;

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = GetSpriteFor(structure);

            BoxCollider2D bc = go.GetComponent<BoxCollider2D>();
            if (bc != null) bc.isTrigger = structure.isOpen;
        }

        public static Sprite GetSpriteForType(StructureType structureType, bool isOpen = false)
        {
            if (Instance != null) Instance.GeneratePlaceholderSprites();
            switch (structureType)
            {
                case StructureType.WoodWall: return _woodWallSprite;
                case StructureType.WoodFloor: return _woodFloorSprite;
                case StructureType.WoodDoor: return isOpen ? _woodDoorOpenSprite : _woodDoorClosedSprite;
                case StructureType.StoneWall: return _stoneWallSprite;
                case StructureType.StoneFloor: return _stoneFloorSprite;
                default: return _woodWallSprite;
            }
        }

        private Sprite GetSpriteFor(PlacedStructure s)
        {
            return GetSpriteForType(s.structureType, s.isOpen);
        }

        public void ClearAllStructures()
        {
            foreach (var kvp in _structureObjects)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            _structureObjects.Clear();
            _structures.Clear();
            OnStructuresChanged?.Invoke();
        }

        public List<Persistence.SavedStructureRecord> CaptureStructures()
        {
            var list = new List<Persistence.SavedStructureRecord>();
            foreach (var kvp in _structures)
            {
                list.Add(new Persistence.SavedStructureRecord
                {
                    cell = new Persistence.Vector3IntRecord(kvp.Key),
                    structureType = (int)kvp.Value.structureType,
                    currentHealth = kvp.Value.currentHealth,
                    isOpen = kvp.Value.isOpen
                });
            }
            return list;
        }

        public void RestoreStructures(List<Persistence.SavedStructureRecord> records)
        {
            ClearAllStructures();
            if (records == null) return;

            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                if (r == null || r.cell == null) continue;
                Vector3Int cell = r.cell.ToVector3Int();
                PlacedStructure s = new PlacedStructure
                {
                    cell = cell,
                    structureType = (StructureType)r.structureType,
                    currentHealth = r.currentHealth,
                    isOpen = r.isOpen
                };
                _structures[cell] = s;
                SpawnStructureObject(s);
            }
            OnStructuresChanged?.Invoke();
        }
    }
}
