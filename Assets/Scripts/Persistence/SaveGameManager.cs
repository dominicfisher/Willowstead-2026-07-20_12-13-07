using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Willowstead.Farming;
using Willowstead.Player;
using Willowstead.World;

namespace Willowstead.Persistence
{
    /// <summary>
    /// Central save/load orchestrator. Self-bootstraps at scene load via
    /// <c>BeforeSceneLoad</c> so it's available before gameplay singletons
    /// finish Awaking. Drives the capture/restore sequence in the right
    /// dependency order after consulting each sub-system's public API.
    ///
    /// Save layout: <c>Application.persistentDataPath/Saves/</c>
    ///   • <c>slot_1.json</c>, <c>slot_2.json</c>, <c>slot_3.json</c> — manual
    ///   • <c>autosave.json</c> — background rolling save (every N seconds)
    ///
    /// Restore sequence (so each step finds its dependencies already in
    /// place): seed → regenerate world → grid inject → crops → trees
    /// felled → player position → inventory → time/weather → hotbar.
    /// </summary>
    public class SaveGameManager : MonoBehaviour
    {
        public static SaveGameManager Instance { get; private set; }

        /// <summary>
        /// True while <see cref="RestoreFromData"/> is running. Sub-systems
        /// check this and skip side-effects that should only fire during
        /// gameplay (player receives starting items, day-tick fires, etc.).
        /// </summary>
        public static bool IsLoadingFromSave { get; private set; }

        public const int SlotCount = 3;
        public const string SlotFilePrefix = "slot_";
        public const string AutosaveFileName = "autosave.json";

        [Header("Autosave")]
        [Tooltip("If true, an autosave is written every _autosaveIntervalSeconds seconds while in-world.")]
        [SerializeField] private bool _enableAutosave = true;
        [Tooltip("Seconds between autosaves. 0 or negative disables autosave even if the toggle is on. Default 5 minutes")]
        [SerializeField] private float _autosaveIntervalSeconds = 300f;

        private float _autosaveTimer;
        private bool _inWorld; // True once Start has completed; autosave only runs then.

        public event Action<int> OnSaveCompleted;
        public event Action<int> OnLoadStarted;
        public event Action<int> OnLoadCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[SaveGameManager]");
            DontDestroyOnLoad(go);
            go.AddComponent<SaveGameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureSaveDirectory();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_inWorld) return;
            if (!_enableAutosave) return;
            if (_autosaveIntervalSeconds <= 0f) return;

            _autosaveTimer += Time.unscaledDeltaTime;
            if (_autosaveTimer < _autosaveIntervalSeconds) return;

            _autosaveTimer = 0f;
            // Catch and log so a missing singleton never crashes the autosave loop.
            try 
            { 
                if (SaveToAutosave())
                {
                    if (ItemNotificationManager.Instance != null)
                    {
                        ItemNotificationManager.Instance.TriggerNotification("Autosaving...", UIResourceHelper.GetSaveIconSprite(), new Color(0.4f, 0.8f, 1.0f));
                    }
                }
            }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }

        /// <summary>
        /// Mark the manager as "in world" so autosave can run. The Main
        /// menu calls this when the player commits to a session (New World
        /// → Create, or Continue → Load).
        /// </summary>
        public void SetInWorld(bool inWorld)
        {
            _inWorld = inWorld;
            _autosaveTimer = 0f;
        }


        /// <summary>Current seconds between autosaves (0 / negative = off).</summary>
        public float AutosaveIntervalSeconds => _autosaveIntervalSeconds;

        /// <summary>Currently allowed to autosave.</summary>
        public bool AutosaveEnabled => _enableAutosave;

        /// <summary>Current active world name.</summary>
        public string ActiveSaveName { get; private set; } = "My Willowstead";

        /// <summary>Current active save slot index (1..3, 0 for autosave, -1 for unsaved).</summary>
        public int ActiveSlotIndex { get; private set; } = -1;

        /// <summary>Updates the active world name (can be changed by host in settings).</summary>
        public void SetActiveSaveName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            ActiveSaveName = newName.Trim();
            if (ActiveSlotIndex > 0)
            {
                SaveToSlot(ActiveSlotIndex, ActiveSaveName);
            }
            else if (ActiveSlotIndex == 0)
            {
                SaveToAutosave(ActiveSaveName);
            }
        }

        /// <summary>
        /// Update the autosave interval from gameplay UI (PauseMenu Gameplay panel).
        /// Clamps to ≥ 0 and resets the rolling timer so the next save doesn't
        /// fire the same frame the slider was just dragged.
        /// </summary>
        public void SetAutosaveIntervalSeconds(float seconds)
        {
            _autosaveIntervalSeconds = Mathf.Max(0f, seconds);
            _autosaveTimer = 0f;
        }

        /// <summary>Toggle autosave on/off from gameplay UI.</summary>
        public void SetAutosaveEnabled(bool enabled)
        {
            _enableAutosave = enabled;
            _autosaveTimer = 0f;
        }

        public static string SaveDirectoryPath
        {
            get
            {
                string root = Application.persistentDataPath;
                return Path.Combine(root, "Saves");
            }
        }

        public static string GetSlotPath(int slotIndex)
        {
            if (slotIndex < 1 || slotIndex > SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot must be 1..{SlotCount}");
            return Path.Combine(SaveDirectoryPath, $"{SlotFilePrefix}{slotIndex}.json");
        }

        public static string GetAutosavePath()
            => Path.Combine(SaveDirectoryPath, AutosaveFileName);

        private void EnsureSaveDirectory()
        {
            try { Directory.CreateDirectory(SaveDirectoryPath); }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveGameManager] Could not create {SaveDirectoryPath}: {ex.Message}", this);
            }
        }


        /// <summary>Capture current world state and write JSON to <paramref name="path"/>.</summary>
        public bool SaveToPath(string path, string saveName = null)
        {
            try
            {
                SaveData data = Capture();
                if (string.IsNullOrEmpty(saveName))
                {
                    saveName = IsLoadingFromSave ? data.saveName : $"World {DateTime.Now:yyyy-MM-dd HH:mm}";
                }
                data.saveName = saveName;
                if (string.IsNullOrEmpty(data.saveTimestampUtc))
                    data.saveTimestampUtc = DateTime.UtcNow.ToString("o");
                if (data.playTimeSeconds <= 0f)
                    data.playTimeSeconds = Time.realtimeSinceStartup;

                string json = JsonUtility.ToJson(data, prettyPrint: false);
                File.WriteAllText(path, json);

                ActiveSaveName = saveName;
                int slotNum = InferSlotFromPath(path);
                ActiveSlotIndex = slotNum;
                if (slotNum >= 0) OnSaveCompleted?.Invoke(slotNum);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveGameManager] Save failed for {path}: {ex.Message}", this);
                return false;
            }
        }

        /// <summary>Save into the named manual slot (1..3).</summary>
        public bool SaveToSlot(int slotIndex, string saveName = null)
            => SaveToPath(GetSlotPath(slotIndex), saveName);

        /// <summary>Save into the autosave slot.</summary>
        public bool SaveToAutosave(string saveName = null) =>
            SaveToPath(GetAutosavePath(), string.IsNullOrEmpty(saveName) ? "Autosave" : saveName);


        /// <summary>Restore the world from <paramref name="path"/>. Returns false on any failure.</summary>
        public bool LoadFromPath(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SaveGameManager] No save at {path}.");
                return false;
            }

            SaveData data;
            try
            {
                string json = File.ReadAllText(path);
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveGameManager] Failed to parse {path}: {ex.Message}", this);
                return false;
            }
            if (data == null)
            {
                Debug.LogError($"[SaveGameManager] FromJson returned null for {path}");
                return false;
            }

            int slotNum = InferSlotFromPath(path);
            ActiveSlotIndex = slotNum;
            ActiveSaveName = !string.IsNullOrEmpty(data.saveName) ? data.saveName : "Untitled";
            OnLoadStarted?.Invoke(slotNum <= 0 ? -1 : slotNum);
            try
            {
                RestoreFromData(data);
                OnLoadCompleted?.Invoke(slotNum <= 0 ? -1 : slotNum);
                _inWorld = true;
                _autosaveTimer = 0f;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveGameManager] Restore failed: {ex.Message}", this);
                return false;
            }
        }

        public bool LoadFromSlot(int slotIndex) => LoadFromPath(GetSlotPath(slotIndex));
        public bool LoadFromAutosave() => LoadFromPath(GetAutosavePath());

        public bool DeleteSlot(int slotIndex)
        {
            string path = GetSlotPath(slotIndex);
            if (!File.Exists(path)) return false;
            try { File.Delete(path); return true; }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveGameManager] Delete failed for {path}: {ex.Message}", this);
                return false;
            }
        }

        public bool DeleteAutosave()
        {
            string path = GetAutosavePath();
            if (!File.Exists(path)) return false;
            try { File.Delete(path); return true; }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveGameManager] Delete failed for {path}: {ex.Message}", this);
                return false;
            }
        }


        /// <summary>Returns summaries for all manual slots plus the autosave.</summary>
        public List<SaveSlotSummary> ListSlots()
        {
            var result = new List<SaveSlotSummary>();
            for (int i = 1; i <= SlotCount; i++)
            {
                result.Add(BuildSummary(GetSlotPath(i), i, $"{SlotFilePrefix}{i}.json"));
            }
            result.Add(BuildSummary(GetAutosavePath(), -1, AutosaveFileName));
            return result;
        }

        /// <summary>Returns the most recent save (latest timestamp), or null if no saves exist.</summary>
        public SaveSlotSummary FindMostRecent()
        {
            SaveSlotSummary best = null;
            foreach (var slot in ListSlots())
            {
                if (!slot.exists) continue;
                if (best == null ||
                    string.CompareOrdinal(slot.saveTimestampUtc, best.saveTimestampUtc) > 0)
                {
                    best = slot;
                }
            }
            return best;
        }


        /// <summary>
        /// Pulls the current state out of every gameplay system into a
        /// fresh <see cref="SaveData"/>. Tolerant of missing systems
        /// (returns empty defaults for that subsystem) so a brand-new
        /// game with partial setup still serialises cleanly.
        /// </summary>
        public static SaveData Capture()
        {
            SaveData d = new SaveData();
            d.saveTimestampUtc = DateTime.UtcNow.ToString("o");

            if (WorldSeedService.Instance != null) d.worldSeed = WorldSeedService.Instance.CurrentSeed;
            if (PlayerController.Instance != null) d.playerPosition = PlayerController.Instance.transform.position;
            if (PlayerStats.Instance != null)
            {
                d.currentHealth = PlayerStats.Instance.CurrentHealth;
                d.maxHealth = PlayerStats.Instance.MaxHealth;
                d.currentStamina = PlayerStats.Instance.CurrentStamina;
                d.maxStamina = PlayerStats.Instance.MaxStamina;
            }
            if (FarmingController.Instance != null) d.selectedHotbarIndex = FarmingController.Instance.SelectedSlotIndex;
            if (DayNightCycle.Instance != null) d.timeOfDay01 = DayNightCycle.Instance.Time01;
            if (WeatherCycle.Instance != null)
            {
                var w = WeatherCycle.Instance;
                d.weatherType = (int)w.CurrentWeather;
                d.windIntensity = (int)w.CurrentIntensity;
                d.windDirection = (int)w.CurrentWindDirection;
            }
            if (GridManager.Instance != null)
            {
                d.currentDay = GridManager.Instance.CurrentDay;
                d.tilledCells.AddRange(GridManager.Instance.CaptureTilledCells());
                d.wateredCells.AddRange(GridManager.Instance.CaptureWateredCells());
                d.fertilizedCells.AddRange(GridManager.Instance.CaptureFertilizedCells());
                d.moistureLevels.AddRange(GridManager.Instance.CaptureMoistureLevels());
                d.crops.AddRange(GridManager.Instance.CaptureCrops());
            }
            if (InventoryManager.Instance != null)
            {
                d.gold = InventoryManager.Instance.GetItemCount("Gold");
                d.inventory.AddRange(InventoryManager.Instance.CaptureInventory());
            }
            d.felledTrees.AddRange(TreeChoppable.CaptureFelledTiles());

            if (Building.BuildingManager.Instance != null)
            {
                d.structures.AddRange(Building.BuildingManager.Instance.CaptureStructures());
            }
            return d;
        }


        /// <summary>
        /// Push <paramref name="data"/> into every gameplay system in the
        /// right order. Order matters because later steps depend on
        /// earlier ones being already in place.
        /// </summary>
        public static void RestoreFromData(SaveData data)
        {
            if (data == null) return;
            IsLoadingFromSave = true;
            try
            {
                //    Regenerate() reproduces the right terrain.
                //    chunk-spawn pass on regen honours previously-felled tiles.
                TreeChoppable.RestoreFelledTiles(FelledFromData(data));

                //    once; do not call Regenerate() again from this method.
                if (WorldSeedService.Instance != null)
                    WorldSeedService.Instance.SetSeed(data.worldSeed, userProvided: true);


                //    grass on top of the tile mid-load).
                if (GridManager.Instance != null)
                    GridManager.Instance.RestoreGridState(data);

                if (Building.BuildingManager.Instance != null)
                    Building.BuildingManager.Instance.RestoreStructures(data.structures);

                if (PlayerController.Instance != null)
                    PlayerController.Instance.RestorePosition(data.playerPosition);

                if (PlayerStats.Instance != null)
                {
                    float maxH = data.maxHealth > 0f ? data.maxHealth : 100f;
                    float curH = data.currentHealth > 0f ? data.currentHealth : maxH;
                    float maxS = data.maxStamina > 0f ? data.maxStamina : 100f;
                    float curS = data.currentStamina >= 0f ? data.currentStamina : maxS;
                    PlayerStats.Instance.SetHealth(curH, maxH);
                    PlayerStats.Instance.SetStamina(curS, maxS);
                }

                if (InventoryManager.Instance != null)
                    InventoryManager.Instance.RestoreInventory(data.inventory, data.gold);

                //    may differ for newly-created vs loaded sessions).
                if (FarmingController.Instance != null)
                    FarmingController.Instance.SetSelectedSlotIndex(data.selectedHotbarIndex);

                if (DayNightCycle.Instance != null)
                    DayNightCycle.Instance.RestoreTime(data.timeOfDay01, data.currentDay);
                if (WeatherCycle.Instance != null)
                {
                    WeatherCycle.Instance.RestoreWeather(
                        (WeatherType)data.weatherType,
                        (WindIntensity)data.windIntensity,
                        (WindDirection)data.windDirection);
                    // continues from the snapshot without immediate re-roll.
                }
            }
            finally
            {
                IsLoadingFromSave = false;
            }
        }

        private static IEnumerable<Vector2IntRecord> FelledFromData(SaveData data)
        {
            return data != null && data.felledTrees != null ? data.felledTrees : new List<Vector2IntRecord>();
        }


        private static SaveSlotSummary BuildSummary(string path, int slotIndex, string fileName)
        {
            var s = new SaveSlotSummary
            {
                slotIndex = slotIndex,
                slotFileName = fileName,
                fullPath = path,
                exists = false,
            };
            if (!File.Exists(path)) { s.failureReason = "empty"; return s; }

            try
            {
                string json = File.ReadAllText(path);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) { s.failureReason = "parse"; return s; }
                s.exists = true;
                s.saveName = data.saveName;
                s.saveTimestampUtc = data.saveTimestampUtc;
                s.playTimeSeconds = data.playTimeSeconds;
                s.worldSeed = data.worldSeed;
                return s;
            }
            catch (Exception ex)
            {
                s.failureReason = ex.Message;
                return s;
            }
        }

        private static int InferSlotFromPath(string path)
        {
            string name = Path.GetFileName(path);
            if (name.Equals(AutosaveFileName, StringComparison.OrdinalIgnoreCase)) return 0;
            if (name.StartsWith(SlotFilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                string n = name.Substring(SlotFilePrefix.Length);
                int idx = n.IndexOf('.');
                string numStr = idx > 0 ? n.Substring(0, idx) : n;
                if (int.TryParse(numStr, out int slot)) return slot;
            }
            return -1;
        }
    }
}
