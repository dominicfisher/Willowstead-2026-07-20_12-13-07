using UnityEngine;

namespace Willowstead.Farming
{
    public enum CropLayoutMode
    {
        Scattered,
        RowAligned
    }

    /// <summary>
    /// Configuration asset for a specific type of crop.
    /// Defines growth time, stage visuals, and yield properties.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCropData", menuName = "Willowstead/Farming/Crop Data")]
    public class CropData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _cropName = "Turnip";
        
        [Header("Growth Settings")]
        [Tooltip("Number of days/waterings required to advance to the next growth stage.")]
        [SerializeField] private int _daysPerStage = 1;

        [Tooltip("The sequential sprites representing the crop growth from seed to harvestable.")]
        [SerializeField] private Sprite[] _growthStageSprites;

        [Header("Yield")]
        [Tooltip("Icon representing the seeds of this crop.")]
        [SerializeField] private Sprite _seedIcon;

        [Tooltip("Item identifier of the crop produced when harvested.")]
        [SerializeField] private string _yieldItemName = "Turnip Item";

        [Tooltip("Quantity of items produced when harvested.")]
        [SerializeField] private int _yieldCount = 1;

        [Header("Visual Layout (Clustering)")]
        [Tooltip("Layout algorithm for positioning multiple crop instances.")]
        [SerializeField] private CropLayoutMode _layoutMode = CropLayoutMode.RowAligned;

        [Tooltip("Minimum number of visual crops to render on a single tile.")]
        [SerializeField] private int _minVisualsPerTile = 1;

        [Tooltip("Maximum number of visual crops to render on a single tile.")]
        [SerializeField] private int _maxVisualsPerTile = 4;

        [Tooltip("Maximum distance from the tile center to position instances.")]
        [SerializeField] private float _scatterRadius = 0.22f;

        [Tooltip("Jitter applied to the positioning to make it look organic.")]
        [SerializeField] private float _randomJitter = 0.05f;

        [Tooltip("Minimum scale multiplier for each crop sprite.")]
        [SerializeField] private float _minScale = 0.8f;

        [Tooltip("Maximum scale multiplier for each crop sprite.")]
        [SerializeField] private float _maxScale = 1.2f;

        [Tooltip("Whether to randomly flip crop sprites horizontally.")]
        [SerializeField] private bool _allowHorizontalFlip = true;

        // Public getters
        public string CropName => _cropName;
        public int DaysPerStage => _daysPerStage;
        public Sprite[] GrowthStageSprites => _growthStageSprites;
        public Sprite SeedIcon => _seedIcon;
        public string YieldItemName => _yieldItemName;
        public int YieldCount => _yieldCount;

        public CropLayoutMode LayoutMode => _layoutMode;
        public int MinVisualsPerTile => _minVisualsPerTile;
        public int MaxVisualsPerTile => _maxVisualsPerTile;
        public float ScatterRadius => _scatterRadius;
        public float RandomJitter => _randomJitter;
        public float MinScale => _minScale;
        public float MaxScale => _maxScale;
        public bool AllowHorizontalFlip => _allowHorizontalFlip;

        /// <summary>
        /// Total number of growth stages.
        /// (e.g. if we have 4 sprites, stages are 0, 1, 2, 3)
        /// </summary>
        public int TotalStages => _growthStageSprites != null ? _growthStageSprites.Length : 0;
    }
}
