using System;
using System.Collections.Generic;
using UnityEngine;

namespace Willowstead.Persistence
{
    // ─── Serializable core root ────────────────────────────────────────────
    /// <summary>
    /// Root persistence record for one world save. Serialized via Unity's
    /// JsonUtility to plain JSON (no Newtonsoft dependency). One file per
    /// slot under <c>Application.persistentDataPath/Saves/</c>.
    ///
    /// JsonUtility only sees fields, not properties; everything below is
    /// intentionally public-fields-only. Bump <see cref="SchemaVersion"/>
    /// when the shape changes and add a migration branch in
    /// <c>SaveGameManager.RestoreFromData</c>.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public string schemaVersion = "willowstead-save-v1";
        public string saveName;            // Free-text label for the slot.
        public string saveTimestampUtc;    // ISO 8601, e.g. 2026-07-30T13:45:00Z
        public float playTimeSeconds;      // Total real-time played since this save.

        // World core (drives deterministic chunks on load).
        public int worldSeed;

        // Player & UI state.
        public Vector3 playerPosition;
        public int selectedHotbarIndex;    // FarmingController._selectedSlotIndex
        public int gold;                   // InventoryManager.GetItemCount("Gold")

        // Time / weather / day counter (GridManager.AdvanceDay() bumps the day count).
        public float timeOfDay01;          // DayNightCycle.Time01, [0,1)
        public int currentDay;
        public int weatherType;            // Cast to Willowstead.World.WeatherType
        public int windIntensity;          // Cast to Willowstead.World.WindIntensity
        public int windDirection;          // Cast to Willowstead.World.WindDirection

        // Inventory (24 slots).
        public List<SavedInventorySlot> inventory = new List<SavedInventorySlot>();

        // Grid state (player-modified cells).
        public List<Vector3IntRecord> tilledCells = new List<Vector3IntRecord>();
        public List<Vector3IntRecord> wateredCells = new List<Vector3IntRecord>();
        public List<SavedMoisture> moistureLevels = new List<SavedMoisture>();

        // Crops alive in the world.
        public List<SavedCrop> crops = new List<SavedCrop>();

        // Trees the player cut down.
        public List<Vector2IntRecord> felledTrees = new List<Vector2IntRecord>();
    }

    [Serializable]
    public class SavedInventorySlot
    {
        public string itemName;
        public int quantity;
    }

    /// <summary>JsonUtility doesn't natively serialize UnityEngine.Vector3Int.</summary>
    [Serializable]
    public class Vector3IntRecord
    {
        public int x;
        public int y;
        public int z;
        public Vector3IntRecord() { }
        public Vector3IntRecord(Vector3Int v) { x = v.x; y = v.y; z = v.z; }
        public Vector3Int ToVector3Int() => new Vector3Int(x, y, z);
    }

    /// <summary>JsonUtility doesn't natively serialize UnityEngine.Vector2Int.</summary>
    [Serializable]
    public class Vector2IntRecord
    {
        public int x;
        public int y;
        public Vector2IntRecord() { }
        public Vector2IntRecord(Vector2Int v) { x = v.x; y = v.y; }
        public Vector2Int ToVector2Int() => new Vector2Int(x, y);
    }

    [Serializable]
    public class SavedMoisture
    {
        public Vector3IntRecord cell;
        public int level; // 0=Dry, 1=Moist, 2=Wet — matches GridManager's encoding.
    }

    [Serializable]
    public class SavedCrop
    {
        public Vector3IntRecord cell;
        // The CropData ScriptableObject name property (e.g. "Carrot"). The
        // restored loader looks this up via Resources.FindObjectsOfTypeAll.
        public string cropDataName;
        public int currentStage;
        public int daysInCurrentStage;
        public int visualsCount;
    }

    /// <summary>
    /// Lightweight per-slot summary used to populate a slot-card UI without
    /// deserializing the full SaveData. Safe to read on a save list screen
    /// (cheap header-only read) and fall back to the full file when the
    /// player picks a card.
    /// </summary>
    public class SaveSlotSummary
    {
        public int slotIndex;                 // -1 for autosave
        public string slotFileName;           // e.g. "slot_1.json" or "autosave.json"
        public string fullPath;               // Application.persistentDataPath/Saves/...
        public string saveName;               // Free-text label (first line of file)
        public string saveTimestampUtc;      // ISO 8601
        public float playTimeSeconds;         // Header field
        public int worldSeed;                 // Header field
        public bool exists;                   // True if file is present and parseable
        public string failureReason;          // Non-null if exists == false
    }
}
