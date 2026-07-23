using UnityEngine;

namespace Willowstead.World
{
    /// <summary>
    /// Generates a simple grid of alternating colored squares at startup.
    /// Provides a visual reference for movement and camera follow in the prototype.
    /// </summary>
    public class ProceduralGridGenerator : MonoBehaviour
    {
        [Header("Grid Settings")]
        [Tooltip("Number of columns in the grid.")]
        [SerializeField] private int _width = 30;

        [Tooltip("Number of rows in the grid.")]
        [SerializeField] private int _height = 30;

        [Tooltip("First color of the checkerboard pattern.")]
        [SerializeField] private Color _colorA = new Color(0.18f, 0.18f, 0.18f, 1f);

        [Tooltip("Second color of the checkerboard pattern.")]
        [SerializeField] private Color _colorB = new Color(0.22f, 0.22f, 0.22f, 1f);

        [Header("Meadow Sprites")]
        [Tooltip("The main plain base grass sprite (e.g. meadow_3).")]
        [SerializeField] private Sprite _baseGrassSprite;

        [Tooltip("The floral/weeds/clover sprites painted in patches.")]
        [SerializeField] private Sprite[] _detailSprites;

        [Header("Meadow Noise Settings")]
        [Tooltip("Controls the size of flower/detail patches. Smaller values = larger patches.")]
        [SerializeField] private float _noiseScale = 0.12f;

        [Tooltip("Percentage of the world covered by detail patches (0 = all grass, 1 = all details).")]
        [Range(0f, 1f)]
        [SerializeField] private float _detailFrequency = 0.30f;

        [Tooltip("Randomly flips tiles horizontally/vertically to prevent grid pattern repeating.")]
        [SerializeField] private bool _allowRandomFlips = true;

        private void Start()
        {
            GenerateGrid();
        }

        private void GenerateGrid()
        {
            bool useSprites = _baseGrassSprite != null;
            Sprite whiteSprite = null;

            if (!useSprites)
            {
                // Create a simple 1x1 white texture programmatically so we don't need any sprite assets
                Texture2D texture = new Texture2D(1, 1);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();

                // Create a sprite from the texture (1 unit per pixel to make it exactly 1x1 unit in Unity)
                whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }

            // Create a parent container to keep the Hierarchy panel clean
            GameObject gridContainer = new GameObject("Procedural Visual Grid");
            gridContainer.transform.parent = transform;

            // Offset the generation so that (0, 0) is roughly the center of the grid
            int halfWidth = _width / 2;
            int halfHeight = _height / 2;

            for (int x = -halfWidth; x < halfWidth; x++)
            {
                for (int y = -halfHeight; y < halfHeight; y++)
                {
                    GameObject tile = new GameObject($"Tile_{x}_{y}");
                    tile.transform.parent = gridContainer.transform;
                    tile.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f);

                    SpriteRenderer sr = tile.AddComponent<SpriteRenderer>();
                    
                    if (useSprites)
                    {
                        // Calculate Perlin Noise coordinate with large offset to avoid symmetry at 0, 0
                        float noiseVal = Mathf.PerlinNoise((x + 1000) * _noiseScale, (y + 1000) * _noiseScale);

                        // If noise value is below the threshold, paint main grass
                        float grassThreshold = 1f - _detailFrequency;
                        if (noiseVal < grassThreshold || _detailSprites == null || _detailSprites.Length == 0)
                        {
                            sr.sprite = _baseGrassSprite;
                        }
                        else
                        {
                            // Map the remaining detail noise range (from grassThreshold to 1.0) to the detail sprite indices
                            float normalizedVal = (noiseVal - grassThreshold) / _detailFrequency;
                            int detailIndex = Mathf.FloorToInt(normalizedVal * _detailSprites.Length);
                            detailIndex = Mathf.Clamp(detailIndex, 0, _detailSprites.Length - 1);
                            
                            sr.sprite = _detailSprites[detailIndex];
                        }

                        // Apply random flips to break up repeating patterns
                        if (_allowRandomFlips)
                        {
                            sr.flipX = Random.value > 0.5f;
                            sr.flipY = Random.value > 0.5f;
                        }
                        
                        sr.color = Color.white;
                    }
                    else
                    {
                        sr.sprite = whiteSprite;
                        // Assign checkerboard color based on coordinates
                        sr.color = ((x + y) % 2 == 0) ? _colorA : _colorB;
                    }
                    
                    // Render in background (sorting order below player and items)
                    sr.sortingOrder = -5000;
                }
            }
        }
    }
}
