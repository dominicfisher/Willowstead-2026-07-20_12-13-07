using UnityEngine;

namespace Willowstead.Farming
{
    /// <summary>
    /// Attached to the player to handle farming interactions.
    /// Uses active tools (Hoe, Watering Can, Seeds) and harvests mature crops.
    /// </summary>
    public class FarmingController : MonoBehaviour
    {
        /// <summary>Singleton accessor for SaveGameManager + UI.</summary>
        public static FarmingController Instance { get; private set; }

        public enum FarmTool
        {
            Hoe,
            WateringCan,
            Seeds,
            Axe,
            Fertilizer,
            None
        }

        [Header("References")]
        [Tooltip("The ScriptableObject input reader channels events to this controller.")]
        [SerializeField] private Input.InputReader _inputReader;

        [Tooltip("The grid selector visualizer in the scene.")]
        [SerializeField] private World.GridSelector _gridSelector;

        [Header("Farming Config")]
        [Tooltip("One shared prefab used for every crop type.")]
        [SerializeField] private GameObject _cropPrefab;

        [Tooltip("Maps each seed item name to its CropData. Add one entry per crop type.")]
        [SerializeField] private SeedEntry[] _seedMappings = new SeedEntry[0];

        [Header("Audio")]
        [Tooltip("Audio clip played when tilling soil with a hoe.")]
        [SerializeField] private AudioClip _tillingAudioClip;

        [Tooltip("Audio clip played when planting seeds.")]
        [SerializeField] private AudioClip _plantingAudioClip;

        [Range(0f, 1f)]
        [SerializeField] private float _farmingAudioVolume = 0.85f;

        private AudioSource _audioSource;

        /// <summary>Resolved at equip-time from _seedMappings.</summary>
        private CropData _currentCropData;

        [Header("Status (Read Only)")]
        [SerializeField] private FarmTool _currentTool = FarmTool.None;

        public FarmTool CurrentTool => _currentTool;

        private Player.InventoryManager _inventory;
        private int _selectedSlotIndex = 0;

        public int SelectedSlotIndex => _selectedSlotIndex;

        /// <summary>
        /// Restores the hotbar-selected index from a save file.
        /// Validates against the current InventoryManager slot count so a
        /// save from a 24-slot session into a runtime with fewer slots
        /// never blows up.
        /// </summary>
        public void SetSelectedSlotIndex(int idx)
        {
            int max = 0;
            if (Player.InventoryManager.Instance != null &&
                Player.InventoryManager.Instance.slots != null)
            {
                max = Player.InventoryManager.Instance.slots.Length - 1;
            }
            _selectedSlotIndex = Mathf.Clamp(idx, 0, Mathf.Max(0, max));
        }

        private bool _isAttackHeld;
        private Vector3Int _lastDragHoeCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

        [System.Serializable]
        public class SeedEntry
        {
            [Tooltip("Item name that appears in the player's inventory (e.g. 'Carrot Seeds').")]
            public string seedItemName;

            [Tooltip("The CropData asset for this seed type.")]
            public CropData cropData;

            [Tooltip("Icon shown during the planting animation. Optional — falls back to the global seed icon.")]
            public Sprite seedIcon;

            /// <summary>
            /// Falls back to 'CropName + Seeds' when seedItemName is left blank.
            /// </summary>
            public string SeedItemName =>
                string.IsNullOrWhiteSpace(seedItemName) && cropData != null
                    ? cropData.CropName + " Seeds"
                    : seedItemName;
        }

        private void Awake()
        {
            EnsureFarmingConfig();
        }

        private void Start()
        {
            EnsureFarmingConfig();

            if (_gridSelector == null)
            {
                _gridSelector = FindAnyObjectByType<World.GridSelector>();
                if (_gridSelector == null)
                {
                    Debug.LogError("[FarmingController] GridSelector is missing and could not be automatically found in the scene!", this);
                }
            }

            _inventory = GetComponent<Player.InventoryManager>();
            if (_inventory == null)
            {
                _inventory = FindAnyObjectByType<Player.InventoryManager>();
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
            }

#if UNITY_EDITOR
            Debug.Log($"[FarmingController] Initialized. Default Tool: {_currentTool}. Use 1-8 or scroll wheel to switch slots. Press G to advance day.");
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureFarmingConfig();
        }
#endif

        private void EnsureFarmingConfig()
        {
#if UNITY_EDITOR
            if (_tillingAudioClip == null)
            {
                _tillingAudioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Farming/Tilling.mp3");
            }

            if (_plantingAudioClip == null)
            {
                _plantingAudioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Farming/Planting.mp3");
            }

            if (_cropPrefab == null)
            {
                _cropPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CropPrefab.prefab");
            }

            if (_seedMappings == null || _seedMappings.Length == 0)
            {
                var carrotData = UnityEditor.AssetDatabase.LoadAssetAtPath<CropData>("Assets/Prefabs/Carrot.asset");
                var potatoData = UnityEditor.AssetDatabase.LoadAssetAtPath<CropData>("Assets/Prefabs/Potato.asset");
                var tomatoData = UnityEditor.AssetDatabase.LoadAssetAtPath<CropData>("Assets/Prefabs/Tomato.asset");
                var cornData   = UnityEditor.AssetDatabase.LoadAssetAtPath<CropData>("Assets/Prefabs/Corn.asset");
                var strawData  = UnityEditor.AssetDatabase.LoadAssetAtPath<CropData>("Assets/Prefabs/Straw.asset");

                var list = new System.Collections.Generic.List<SeedEntry>();
                if (carrotData != null) list.Add(new SeedEntry { seedItemName = "Carrot Seeds", cropData = carrotData });
                if (potatoData != null) list.Add(new SeedEntry { seedItemName = "Potato Seeds", cropData = potatoData });
                if (tomatoData != null) list.Add(new SeedEntry { seedItemName = "Tomato Seeds", cropData = tomatoData });
                if (cornData != null)   list.Add(new SeedEntry { seedItemName = "Corn Seeds", cropData = cornData });
                if (strawData != null)  list.Add(new SeedEntry { seedItemName = "Straw Seeds", cropData = strawData });

                _seedMappings = list.ToArray();
            }
#endif
        }

        private void OnEnable()
        {
            if (_inputReader == null)
            {
                _inputReader = Resources.Load<Input.InputReader>("InputReader");
                if (_inputReader == null)
                {
                    var readers = Resources.FindObjectsOfTypeAll<Input.InputReader>();
                    if (readers != null && readers.Length > 0) _inputReader = readers[0];
                }
            }

            if (_inputReader != null)
            {
                _inputReader.AttackEvent += OnUseToolInput;
                _inputReader.AttackCanceledEvent += OnAttackCanceled;
                _inputReader.InteractEvent += OnInteractInput;
            }
            else
            {
                Debug.LogError("[FarmingController] Input Reader reference is null! Please assign the Input Reader asset in the Inspector.", this);
            }
        }

        private void OnDisable()
        {
            if (_inputReader != null)
            {
                _inputReader.AttackEvent -= OnUseToolInput;
                _inputReader.AttackCanceledEvent -= OnAttackCanceled;
                _inputReader.InteractEvent -= OnInteractInput;
            }
        }

        private void Update()
        {
            if (Input.InputReader.BlockGameplayInput)
            {
                return;
            }

            HandleToolSelectionInput();
            HandleDebugTimeInput();

            // Direct left-click fallback for tool usage (hoe, watering can, planting seeds, harvesting)
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (!Player.UIResourceHelper.IsPointerOverAnyUI())
                {
                    Debug.Log($"[FarmingController] Left Click detected. Using tool: {_currentTool}");
                    OnUseToolInput();
                }
            }

            if (!Player.UIResourceHelper.IsPointerOverAnyUI())
            {
                HandleDragFarmingActions();
            }
        }

        /// <summary>
        /// Reads keyboard inputs (1-8) or mouse scroll wheel to change selected hotbar slot,
        /// and equips the tool located in that slot.
        /// </summary>
        private void HandleToolSelectionInput()
        {
            if (Input.InputReader.BlockGameplayInput) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                int prev = _selectedSlotIndex;
                if (keyboard.digit1Key.wasPressedThisFrame) _selectedSlotIndex = 0;
                else if (keyboard.digit2Key.wasPressedThisFrame) _selectedSlotIndex = 1;
                else if (keyboard.digit3Key.wasPressedThisFrame) _selectedSlotIndex = 2;
                else if (keyboard.digit4Key.wasPressedThisFrame) _selectedSlotIndex = 3;
                else if (keyboard.digit5Key.wasPressedThisFrame) _selectedSlotIndex = 4;
                else if (keyboard.digit6Key.wasPressedThisFrame) _selectedSlotIndex = 5;
                else if (keyboard.digit7Key.wasPressedThisFrame) _selectedSlotIndex = 6;
                else if (keyboard.digit8Key.wasPressedThisFrame) _selectedSlotIndex = 7;

                if (prev != _selectedSlotIndex)
                {
#if UNITY_EDITOR
                    Debug.Log($"[FarmingController] Selected Hotbar Slot {_selectedSlotIndex}");
#endif
                }
            }

            // Only scroll hotbar if Alt is NOT held (Alt + mouse scroll is reserved for camera zoom)
            bool isMapOpen = UI.FullMapUI.Instance != null && UI.FullMapUI.Instance.IsMapOpen;
            bool isAltHeld = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (!isAltHeld && !isMapOpen && mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0f)
                {
                    _selectedSlotIndex = (_selectedSlotIndex - 1 + 8) % 8;
#if UNITY_EDITOR
                    Debug.Log($"[FarmingController] Scrolled Selected Hotbar Slot {_selectedSlotIndex}");
#endif
                }
                else if (scroll < 0f)
                {
                    _selectedSlotIndex = (_selectedSlotIndex + 1) % 8;
#if UNITY_EDITOR
                    Debug.Log($"[FarmingController] Scrolled Selected Hotbar Slot {_selectedSlotIndex}");
#endif
                }
            }

            UpdateEquippedTool();
        }

        private void UpdateEquippedTool()
        {
            if (_inventory == null)
            {
                _currentTool = FarmTool.None;
                return;
            }

            Player.InventorySlot slot = _inventory.GetSlotItem(_selectedSlotIndex);
            if (slot == null || slot.IsEmpty)
            {
                _currentTool = FarmTool.None;
                return;
            }

            if (slot.itemName == "Hoe")
            {
                _currentTool = FarmTool.Hoe;
            }
            else if (slot.itemName == "Watering Can")
            {
                _currentTool = FarmTool.WateringCan;
            }
            else if (slot.itemName == "Axe")
            {
                _currentTool = FarmTool.Axe;
            }
            else if (string.Equals(slot.itemName.Trim(), "Fertilizer", System.StringComparison.OrdinalIgnoreCase))
            {
                _currentTool = FarmTool.Fertilizer;
            }
            else
            {
                CropData matched = null;
                string slotNameTrim = slot.itemName.Trim();

                foreach (SeedEntry entry in _seedMappings)
                {
                    if (entry.cropData == null) continue;

                    string entryName = entry.SeedItemName;
                    if (string.Equals(slotNameTrim, entryName, System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(slotNameTrim, entry.cropData.CropName + " Seed", System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(slotNameTrim, entry.cropData.CropName + " Seeds", System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(slotNameTrim, entry.cropData.CropName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        matched = entry.cropData;
                        break;
                    }
                }

                if (matched != null)
                {
                    _currentTool = FarmTool.Seeds;
                    _currentCropData = matched;
                }
                else
                {
                    _currentTool = FarmTool.None;
                    _currentCropData = null;
                }
            }
        }

        /// <summary>
        /// Reads debug key input (G) to advance time by one day.
        /// </summary>
        private void HandleDebugTimeInput()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.gKey.wasPressedThisFrame)
            {
                if (World.GridManager.Instance != null)
                {
                    World.GridManager.Instance.AdvanceDay();
                }
            }
        }

        private void SwitchTool(FarmTool tool)
        {
            _currentTool = tool;
#if UNITY_EDITOR
            Debug.Log($"[FarmingController] Switched active tool to: {_currentTool}");
#endif
        }

        /// <summary>
        /// Called when the player triggers the tool use action (e.g. Left Click / Attack).
        /// </summary>
        private void OnUseToolInput()
        {
            // NOTE: This used to log a long status dump on every click, which flooded
            // the console during drag-tilling. Intentionally silent now — if you need
            // to debug tool behavior, watch the Inventory/Hotbar UI instead.
            _isAttackHeld = true;

            // Hoe is processed continuously while the button is held (drag-tilling).
            // Other tools are processed once per click.
            if (_currentTool == FarmTool.Hoe) return;

            ProcessToolAtCurrentCell();
        }

        /// <summary>
        /// Called when the player releases the tool use button.
        /// </summary>
        private void OnAttackCanceled()
        {
            _isAttackHeld = false;
            _lastDragHoeCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        }

        /// <summary>
        /// Processes a single click for non-hoe tools (WateringCan / Seeds).
        /// </summary>
        private void ProcessToolAtCurrentCell()
        {
            if (_gridSelector == null || !_gridSelector.IsCellInRange) return;
            if (World.GridManager.Instance == null) return;

            Vector3Int targetCell = _gridSelector.CurrentCell;

            switch (_currentTool)
            {
                case FarmTool.WateringCan:
                    World.GridManager.Instance.WaterTile(targetCell);
                    break;

                case FarmTool.Axe:
                    UseAxeAtCell(targetCell);
                    break;

                case FarmTool.Fertilizer:
                    {
                        Player.InventorySlot fertSlot = _inventory.GetSlotItem(_selectedSlotIndex);
                        if (fertSlot == null || fertSlot.IsEmpty) break;

                        bool fertilized = World.GridManager.Instance.FertilizeCell(targetCell);
                        if (fertilized)
                        {
                            _inventory.RemoveItemFromSlot(_selectedSlotIndex, 1);
                            PlayPlantingAudio();
                        }
                    }
                    break;

                case FarmTool.Seeds:
                    if (_currentCropData != null && _cropPrefab != null)
                    {
                        Player.InventorySlot seedSlot = _inventory.GetSlotItem(_selectedSlotIndex);
                        if (seedSlot == null || seedSlot.IsEmpty)
                        {
#if UNITY_EDITOR
                            Debug.LogWarning("[FarmingController] Cannot plant: seed slot is empty.");
#endif
                            break;
                        }

                        bool success = World.GridManager.Instance.PlantCrop(targetCell, _currentCropData, _cropPrefab);
                        if (success)
                        {
                            _inventory.RemoveItemFromSlot(_selectedSlotIndex, 1);
                            PlayPlantingAudio();
                        }
                    }
                    else
                    {
#if UNITY_EDITOR
                        Debug.LogWarning("[FarmingController] Cannot plant: make sure CropPrefab is assigned and a seed mapping exists.", this);
#endif
                    }
                    break;
            }
        }

        /// <summary>
        /// Tills (or harvests) a single cell with the hoe. Used by drag-tilling.
        /// </summary>
        private void UseHoeAtCell(Vector3Int cell)
        {
            if (World.GridManager.Instance.HasCrop(cell))
            {
                Crop crop = World.GridManager.Instance.GetCrop(cell);
                if (crop != null && crop.IsMature)
                {
                    crop.Harvest();
                    return;
                }
            }

            if (World.GridManager.Instance.IsCellTilled(cell)) return;

            if (Player.PlayerStats.Instance != null && !Player.PlayerStats.Instance.UseStamina(4f))
            {
                return;
            }

            World.GridManager.Instance.HoeTile(cell);
            PlayTillingAudio();
        }

        private void PlayTillingAudio()
        {
            if (_tillingAudioClip == null) return;
            float vol = _farmingAudioVolume * Audio.AudioManager.SfxVolume;
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource != null)
            {
                _audioSource.pitch = Random.Range(0.92f, 1.08f);
                _audioSource.PlayOneShot(_tillingAudioClip, vol);
            }
            else
            {
                AudioSource.PlayClipAtPoint(_tillingAudioClip, transform.position, vol);
            }
        }

        private void PlayPlantingAudio()
        {
            if (_plantingAudioClip == null) return;
            float vol = _farmingAudioVolume * Audio.AudioManager.SfxVolume;
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource != null)
            {
                _audioSource.pitch = Random.Range(0.94f, 1.06f);
                _audioSource.PlayOneShot(_plantingAudioClip, vol);
            }
            else
            {
                AudioSource.PlayClipAtPoint(_plantingAudioClip, transform.position, vol);
            }
        }

        /// <summary>
        /// One-shot chop with the axe at the targeted cell. Looks for the nearest
        /// TreeChoppable within a small radius of the cell center and, if found,
        /// ticks its shake-and-fell counter. Trees aren't on a grid cell, so this
        /// is a forgiving radius query rather than an exact-cell hit.
        /// </summary>
        private void UseAxeAtCell(Vector3Int cell)
        {
            if (World.GridManager.Instance == null) return;
            Vector3 worldCenter = World.GridManager.Instance.CellToWorldCenter(cell);
            // 1.25 cell radius — ProceduralGridGenerator can jitter trees up to ~0.65
            // cells off the cell center, so the adjacent-cell click range has to be
            // generous enough to catch trees near a corner of their tile.
            World.TreeChoppable tree = World.TreeChoppable.FindNearest(worldCenter, 1.25f);
            if (tree != null)
            {
                if (Player.PlayerStats.Instance != null && !Player.PlayerStats.Instance.UseStamina(5f))
                {
                    return;
                }
                tree.Chop();
            }
        }

        private Vector3Int _lastDragCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

        /// <summary>
        /// While left click or attack button is held down, execute continuous drag actions
        /// (tilling with Hoe, watering with Watering Can, or planting with Seeds) across cells.
        /// </summary>
        private void HandleDragFarmingActions()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool isMouseClickHeld = mouse != null && mouse.leftButton.isPressed;
            bool isHeld = (_isAttackHeld || isMouseClickHeld) && !Input.InputReader.BlockGameplayInput;

            if (!isHeld)
            {
                _lastDragCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
                return;
            }

            if (_gridSelector == null || !_gridSelector.IsCellInRange) return;
            if (World.GridManager.Instance == null) return;

            Vector3Int currentCell = _gridSelector.CurrentCell;
            if (currentCell != _lastDragCell)
            {
                _lastDragCell = currentCell;

                switch (_currentTool)
                {
                    case FarmTool.Hoe:
                        UseHoeAtCell(currentCell);
                        break;

                    case FarmTool.WateringCan:
                        WaterCell(currentCell);
                        break;

                    case FarmTool.Seeds:
                        PlantSeedAtCell(currentCell);
                        break;

                    case FarmTool.Axe:
                        UseAxeAtCell(currentCell);
                        break;
                }
            }
        }

        private void WaterCell(Vector3Int cell)
        {
            if (World.GridManager.Instance == null) return;
            if (World.GridManager.Instance.IsCellTilled(cell))
            {
                if (Player.PlayerStats.Instance != null && !Player.PlayerStats.Instance.UseStamina(3f))
                {
                    return;
                }
                World.GridManager.Instance.WaterTile(cell);
            }
        }

        private void PlantSeedAtCell(Vector3Int cell)
        {
            if (World.GridManager.Instance == null) return;
            if (_currentCropData != null && _cropPrefab != null)
            {
                Player.InventorySlot seedSlot = _inventory.GetSlotItem(_selectedSlotIndex);
                if (seedSlot == null || seedSlot.IsEmpty) return;

                bool success = World.GridManager.Instance.PlantCrop(cell, _currentCropData, _cropPrefab);
                if (success)
                {
                    _inventory.RemoveItemFromSlot(_selectedSlotIndex, 1);
                    PlayPlantingAudio();
                }
            }
        }

        /// <summary>
        /// Called when the player triggers the interact action (e.g. E / Interact).
        /// </summary>
        private void OnInteractInput()
        {
            // Crop harvesting has been moved to Left Click via the Hoe tool, so pressing E no longer harvests crops.
#if UNITY_EDITOR
            Debug.Log($"[FarmingController] OnInteractInput called. Interaction bypasses crops.");
#endif
        }
    }
}
