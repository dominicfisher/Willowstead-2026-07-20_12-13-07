using UnityEngine;

namespace Willowstead.Farming
{
    /// <summary>
    /// Attached to the player to handle farming interactions.
    /// Uses active tools (Hoe, Watering Can, Seeds) and harvests mature crops.
    /// </summary>
    public class FarmingController : MonoBehaviour
    {
        public enum FarmTool
        {
            Hoe,
            WateringCan,
            Seeds
        }

        [Header("References")]
        [Tooltip("The ScriptableObject input reader channels events to this controller.")]
        [SerializeField] private Input.InputReader _inputReader;

        [Tooltip("The grid selector visualizer in the scene.")]
        [SerializeField] private World.GridSelector _gridSelector;

        [Header("Farming Config")]
        [Tooltip("The crop definition to plant when using the seed tool.")]
        [SerializeField] private CropData _currentCropData;

        [Tooltip("The crop prefab that will be instantiated on tilled soil.")]
        [SerializeField] private GameObject _cropPrefab;

        [Header("Status (Read Only)")]
        [SerializeField] private FarmTool _currentTool = FarmTool.Hoe;

        public FarmTool CurrentTool => _currentTool;

        private Player.InventoryManager _inventory;

        private void Start()
        {
            // Auto-locate GridSelector if not assigned
            if (_gridSelector == null)
            {
                _gridSelector = FindAnyObjectByType<World.GridSelector>();
                if (_gridSelector == null)
                {
                    Debug.LogError("[FarmingController] GridSelector is missing and could not be automatically found in the scene!", this);
                }
            }

            // Cache InventoryManager reference
            _inventory = GetComponent<Player.InventoryManager>();
            if (_inventory == null)
            {
                _inventory = FindAnyObjectByType<Player.InventoryManager>();
            }

            Debug.Log($"[FarmingController] Initialized. Default Tool: {_currentTool}. Press 1, 2, or 3 to switch tools. Press G to advance day.");
        }

        private void OnEnable()
        {
            if (_inputReader != null)
            {
                _inputReader.AttackEvent += OnUseToolInput;
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
                _inputReader.InteractEvent -= OnInteractInput;
            }
        }

        private void Update()
        {
            HandleToolSelectionInput();
            HandleDebugTimeInput();
        }

        /// <summary>
        /// Reads keyboard inputs (1, 2, 3) to switch farming tools.
        /// </summary>
        private void HandleToolSelectionInput()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                SwitchTool(FarmTool.Hoe);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                SwitchTool(FarmTool.WateringCan);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                SwitchTool(FarmTool.Seeds);
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
            Debug.Log($"[FarmingController] Switched active tool to: {_currentTool}");
        }

        /// <summary>
        /// Called when the player triggers the tool use action (e.g. Left Click / Attack).
        /// </summary>
        private void OnUseToolInput()
        {
            Debug.Log($"[FarmingController] OnUseToolInput called. Current Tool: {_currentTool}. GridSelector exists: {_gridSelector != null}, IsCellInRange: {(_gridSelector != null ? _gridSelector.IsCellInRange.ToString() : "N/A")}. GridManager exists: {World.GridManager.Instance != null}");
            if (_gridSelector == null || !_gridSelector.IsCellInRange) return;
            if (World.GridManager.Instance == null) return;

            Vector3Int targetCell = _gridSelector.CurrentCell;

            switch (_currentTool)
            {
                case FarmTool.Hoe:
                    World.GridManager.Instance.HoeTile(targetCell);
                    break;
                case FarmTool.WateringCan:
                    World.GridManager.Instance.WaterTile(targetCell);
                    break;
                case FarmTool.Seeds:
                    if (_currentCropData != null && _cropPrefab != null)
                    {
                        string seedName = _currentCropData.CropName + " Seeds";
                        int seedsNeeded = 1;
                        if (_inventory != null && !_inventory.HasItem(seedName, seedsNeeded))
                        {
                            Debug.LogWarning($"[FarmingController] Cannot plant: Out of {seedName}!");
                            break;
                        }

                        bool success = World.GridManager.Instance.PlantCrop(targetCell, _currentCropData, _cropPrefab);
                        if (success && _inventory != null)
                        {
                            _inventory.RemoveItem(seedName, seedsNeeded);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[FarmingController] Cannot plant seed: Make sure CropData and CropPrefab are assigned in the Inspector.", this);
                    }
                    break;
            }
        }

        /// <summary>
        /// Called when the player triggers the interact action (e.g. E / Interact).
        /// </summary>
        private void OnInteractInput()
        {
            Debug.Log($"[FarmingController] OnInteractInput called. GridSelector exists: {_gridSelector != null}, IsCellInRange: {(_gridSelector != null ? _gridSelector.IsCellInRange.ToString() : "N/A")}. Target Cell: {_gridSelector?.CurrentCell}");
            if (_gridSelector == null || !_gridSelector.IsCellInRange) return;
            if (World.GridManager.Instance == null) return;

            Vector3Int targetCell = _gridSelector.CurrentCell;

            // Harvest the crop if it exists and is mature
            if (World.GridManager.Instance.HasCrop(targetCell))
            {
                Crop crop = World.GridManager.Instance.GetCrop(targetCell);
                if (crop != null)
                {
                    if (crop.IsMature)
                    {
                        crop.Harvest();
                    }
                    else
                    {
                        Debug.Log($"[FarmingController] Crop '{crop.Data.CropName}' at {targetCell} is in growth stage {crop.CurrentStage}. (Requires more watering days to mature).");
                    }
                }
            }
        }
    }
}
