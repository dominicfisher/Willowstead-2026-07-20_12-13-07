using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Willowstead.Player;
using Willowstead.World;

namespace Willowstead.Building
{
    /// <summary>
    /// RimWorld-style architect construction bar at the bottom-left of the screen.
    /// Toggleable with the 'B' key (or Build button on HUD).
    /// Allows selecting structure blueprints (Wood Wall, Wood Floor, Wood Door, Stone Wall, Stone Floor, Demolish),
    /// shows material costs, previews placement on the grid with mouse drag, and executes builds.
    /// </summary>
    public class BuildMenuUI : MonoBehaviour
    {
        public static BuildMenuUI Instance { get; private set; }

        private GameObject _barGo;
        private GameObject _blueprintPreviewGo;
        private SpriteRenderer _previewSr;
        private bool _isOpen = false;
        private StructureType _selectedBlueprint = StructureType.None;
        private bool _isDemolishMode = false;

        private readonly List<Button> _structureButtons = new List<Button>();
        private readonly List<Image> _buttonHighlights = new List<Image>();

        public bool IsOpen => _isOpen;
        public StructureType SelectedBlueprint => _selectedBlueprint;
        public bool IsDemolishMode => _isDemolishMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[BuildMenuUI]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<BuildMenuUI>();
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
        }

        private void Start()
        {
            CreateBuildUI();
            CreateBlueprintPreview();
            SetMenuVisible(false);
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.bKey.wasPressedThisFrame &&
                    !Input.InputReader.BlockGameplayInput)
                {
                    ToggleMenu();
                }

                if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame && _isOpen)
                {
                    if (_selectedBlueprint != StructureType.None || _isDemolishMode)
                    {
                        SelectBlueprint(StructureType.None);
                    }
                    else
                    {
                        CloseMenu();
                    }
                }
            }

            UpdateBlueprintGhost();
            HandleBuildInput();
        }

        public void ToggleMenu()
        {
            _isOpen = !_isOpen;
            SetMenuVisible(_isOpen);
            if (!_isOpen)
            {
                SelectBlueprint(StructureType.None);
            }
        }

        public void OpenMenu()
        {
            _isOpen = true;
            SetMenuVisible(true);
        }

        public void CloseMenu()
        {
            _isOpen = false;
            SetMenuVisible(false);
            SelectBlueprint(StructureType.None);
        }

        private void SetMenuVisible(bool visible)
        {
            if (_barGo != null) _barGo.SetActive(visible);
            if (_blueprintPreviewGo != null) _blueprintPreviewGo.SetActive(visible && (_selectedBlueprint != StructureType.None || _isDemolishMode));
        }

        public void SelectBlueprint(StructureType type, bool demolish = false)
        {
            _selectedBlueprint = type;
            _isDemolishMode = demolish;

            if (_blueprintPreviewGo != null)
            {
                _blueprintPreviewGo.SetActive(_isOpen && (_selectedBlueprint != StructureType.None || _isDemolishMode));
            }

            for (int i = 0; i < _buttonHighlights.Count; i++)
            {
                if (_buttonHighlights[i] != null)
                {
                    _buttonHighlights[i].color = new Color(0.85f, 0.70f, 0.55f, 0.35f);
                }
            }

            int activeIdx = demolish ? 5 : ((int)type - 1);
            if (activeIdx >= 0 && activeIdx < _buttonHighlights.Count && _buttonHighlights[activeIdx] != null)
            {
                _buttonHighlights[activeIdx].color = new Color(1f, 0.85f, 0.3f, 0.95f); // Bright active gold border
            }
        }

        private void CreateBlueprintPreview()
        {
            _blueprintPreviewGo = new GameObject("BlueprintGhostPreview");
            _previewSr = _blueprintPreviewGo.AddComponent<SpriteRenderer>();
            _previewSr.sortingOrder = 30000;
            _blueprintPreviewGo.SetActive(false);
        }

        private void UpdateBlueprintGhost()
        {
            if (!_isOpen || (_selectedBlueprint == StructureType.None && !_isDemolishMode))
            {
                if (_blueprintPreviewGo != null) _blueprintPreviewGo.SetActive(false);
                return;
            }

            if (GridManager.Instance == null) return;
            GridSelector selector = FindAnyObjectByType<GridSelector>();
            if (selector == null) return;

            Vector3Int cell = selector.CurrentCell;
            Vector3 worldPos = GridManager.Instance.CellToWorldCenter(cell);
            _blueprintPreviewGo.transform.position = worldPos;
            _blueprintPreviewGo.SetActive(true);

            if (_isDemolishMode)
            {
                _previewSr.sprite = UIResourceHelper.GetBackgroundSprite();
                bool hasTarget = BuildingManager.Instance != null && BuildingManager.Instance.HasStructureAt(cell);
                _previewSr.color = hasTarget ? new Color(1f, 0.2f, 0.2f, 0.65f) : new Color(0.5f, 0.5f, 0.5f, 0.35f);
            }
            else
            {
                bool canBuild = BuildingManager.Instance != null && BuildingManager.Instance.CanPlaceStructure(cell, _selectedBlueprint);
                _previewSr.sprite = UIResourceHelper.GetBackgroundSprite();
                _previewSr.color = canBuild ? new Color(0.2f, 1f, 0.4f, 0.65f) : new Color(1f, 0.2f, 0.2f, 0.65f);
            }
        }

        private void HandleBuildInput()
        {
            if (!_isOpen) return;
            if (_selectedBlueprint == StructureType.None && !_isDemolishMode) return;
            if (UIResourceHelper.IsPointerOverAnyUI()) return;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                GridSelector selector = FindAnyObjectByType<GridSelector>();
                if (selector == null || !selector.IsCellInRange) return;
                Vector3Int cell = selector.CurrentCell;

                if (_isDemolishMode)
                {
                    if (BuildingManager.Instance != null)
                    {
                        BuildingManager.Instance.DemolishStructure(cell);
                    }
                }
                else
                {
                    if (BuildingManager.Instance != null)
                    {
                        bool built = BuildingManager.Instance.BuildStructure(cell, _selectedBlueprint);
                        if (!built && BuildingManager.Instance.GetMaterialCost(_selectedBlueprint, out string req) > 0)
                        {
                            if (ItemNotificationManager.Instance != null && InventoryManager.Instance != null && InventoryManager.Instance.GetItemCount(req) < BuildingManager.Instance.GetMaterialCost(_selectedBlueprint, out _))
                            {
                                ItemNotificationManager.Instance.TriggerNotification($"Need {BuildingManager.Instance.GetMaterialCost(_selectedBlueprint, out _)}x {req}!", UIResourceHelper.GetBackgroundSprite(), new Color(1f, 0.4f, 0.4f));
                            }
                        }
                    }
                }
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                // Right click cancels blueprint tool
                SelectBlueprint(StructureType.None);
            }
        }

        private void CreateBuildUI()
        {
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas");
            if (canvas == null) return;

            Font font = UIResourceHelper.GetPixelFont();

            _barGo = new GameObject("BuildMenuPanel", typeof(RectTransform), typeof(Image));
            _barGo.transform.SetParent(canvas.transform, false);

            RectTransform barRt = (RectTransform)_barGo.transform;
            barRt.anchorMin = new Vector2(0.5f, 0f);
            barRt.anchorMax = new Vector2(0.5f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.anchoredPosition = new Vector2(0f, 96f);
            barRt.sizeDelta = new Vector2(490f, 68f);

            Image barBg = _barGo.GetComponent<Image>();
            barBg.sprite = UIResourceHelper.GetQuestBookSprite();
            barBg.type = Image.Type.Sliced;
            barBg.color = new Color(0.96f, 0.90f, 0.82f, 0.98f);

            // Title Banner
            GameObject titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(_barGo.transform, false);
            RectTransform titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(12f, 20f);
            titleRt.sizeDelta = new Vector2(0f, 20f);

            Text titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = font;
            titleTxt.text = "🔨 ARCHITECT (B)";
            titleTxt.fontSize = 11;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.color = new Color(0.35f, 0.22f, 0.16f, 1f);
            titleTxt.alignment = TextAnchor.MiddleLeft;
            titleTxt.raycastTarget = false;

            // Structure options
            StructureType[] types = {
                StructureType.WoodWall,
                StructureType.WoodFloor,
                StructureType.WoodDoor,
                StructureType.StoneWall,
                StructureType.StoneFloor
            };

            string[] names = { "W. Wall", "W. Floor", "Door", "S. Wall", "S. Floor" };
            string[] costs = { "2 Log", "1 Log", "3 Log", "2 Stone", "1 Stone" };

            float startX = 14f;
            float btnW = 68f;
            float spacing = 6f;

            _structureButtons.Clear();
            _buttonHighlights.Clear();

            for (int i = 0; i < types.Length; i++)
            {
                int index = i;
                StructureType type = types[i];
                BuildOptionButton(_barGo.transform, names[i], costs[i], new Vector2(startX + i * (btnW + spacing), 10f), new Vector2(btnW, 48f), font, () =>
                {
                    SelectBlueprint(type, false);
                });
            }

            // Demolish Button
            BuildOptionButton(_barGo.transform, "Demolish", "Refund", new Vector2(startX + types.Length * (btnW + spacing) + 4f, 10f), new Vector2(btnW + 4f, 48f), font, () =>
            {
                SelectBlueprint(StructureType.None, true);
            }, isDemolish: true);
        }

        private void BuildOptionButton(Transform parent, string label, string cost, Vector2 pos, Vector2 size, Font font, UnityEngine.Events.UnityAction onClick, bool isDemolish = false)
        {
            GameObject btnGo = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
            btnGo.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)btnGo.transform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = btnGo.GetComponent<Image>();
            img.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            img.type = Image.Type.Sliced;
            img.color = isDemolish ? new Color(0.85f, 0.40f, 0.35f, 0.95f) : new Color(0.90f, 0.82f, 0.72f, 0.95f);

            // Active Highlight Border
            GameObject hlGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            hlGo.transform.SetParent(btnGo.transform, false);
            RectTransform hlRt = (RectTransform)hlGo.transform;
            hlRt.anchorMin = Vector2.zero; hlRt.anchorMax = Vector2.one;
            hlRt.offsetMin = new Vector2(-2f, -2f); hlRt.offsetMax = new Vector2(2f, 2f);
            Image hlImg = hlGo.GetComponent<Image>();
            hlImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            hlImg.type = Image.Type.Sliced;
            hlImg.color = new Color(0.85f, 0.70f, 0.55f, 0.35f);
            hlImg.raycastTarget = false;
            _buttonHighlights.Add(hlImg);

            Button btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            _structureButtons.Add(btn);

            // Title
            GameObject titleGo = new GameObject("Label", typeof(RectTransform));
            titleGo.transform.SetParent(btnGo.transform, false);
            RectTransform tRt = (RectTransform)titleGo.transform;
            tRt.anchorMin = new Vector2(0f, 0.5f); tRt.anchorMax = new Vector2(1f, 1f);
            tRt.offsetMin = Vector2.zero; tRt.offsetMax = new Vector2(0f, -2f);

            Text tTxt = titleGo.AddComponent<Text>();
            tTxt.font = font;
            tTxt.text = label;
            tTxt.fontSize = 10;
            tTxt.fontStyle = FontStyle.Bold;
            tTxt.color = isDemolish ? Color.white : new Color(0.32f, 0.20f, 0.14f, 1f);
            tTxt.alignment = TextAnchor.MiddleCenter;
            tTxt.raycastTarget = false;

            // Cost Subtitle
            GameObject costGo = new GameObject("Cost", typeof(RectTransform));
            costGo.transform.SetParent(btnGo.transform, false);
            RectTransform cRt = (RectTransform)costGo.transform;
            cRt.anchorMin = new Vector2(0f, 0f); cRt.anchorMax = new Vector2(1f, 0.5f);
            cRt.offsetMin = new Vector2(0f, 2f); cRt.offsetMax = Vector2.zero;

            Text cTxt = costGo.AddComponent<Text>();
            cTxt.font = font;
            cTxt.text = cost;
            cTxt.fontSize = 9;
            cTxt.color = isDemolish ? new Color(1f, 0.9f, 0.9f, 0.85f) : new Color(0.52f, 0.40f, 0.30f, 1f);
            cTxt.alignment = TextAnchor.MiddleCenter;
            cTxt.raycastTarget = false;
        }
    }
}
