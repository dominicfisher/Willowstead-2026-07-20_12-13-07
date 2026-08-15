using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Willowstead.World
{
    /// <summary>
    /// A lightweight weighted grass tile that lets you set custom percentage weights
    /// for randomly selected grass sprite variants (e.g. 70% basic grass, 20% flower grass, 10% tall grass).
    /// </summary>
    [CreateAssetMenu(fileName = "WeightedGrassTile", menuName = "2D/Tiles/Weighted Grass Tile")]
    public class WeightedGrassTile : Tile
    {
        [System.Serializable]
        public struct WeightedSprite
        {
            public Sprite sprite;
            [Min(0f)]
            [Tooltip("Relative weight/chance for this sprite to appear.")]
            public float weight;
        }

        [Header("Weighted Sprites")]
        [SerializeField] private List<WeightedSprite> _sprites = new List<WeightedSprite>();

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            base.GetTileData(position, tilemap, ref tileData);

            if (_sprites == null || _sprites.Count == 0) return;

            Sprite chosenSprite = PickWeightedSprite(position);
            if (chosenSprite != null)
            {
                tileData.sprite = chosenSprite;
            }
        }

        private Sprite PickWeightedSprite(Vector3Int position)
        {
            float totalWeight = 0f;
            for (int i = 0; i < _sprites.Count; i++)
            {
                if (_sprites[i].sprite != null)
                    totalWeight += Mathf.Max(0f, _sprites[i].weight);
            }

            if (totalWeight <= 0f)
            {
                for (int i = 0; i < _sprites.Count; i++)
                {
                    if (_sprites[i].sprite != null) return _sprites[i].sprite;
                }
                return null;
            }

            uint hash = (uint)(position.x * 1103515245 + position.y * 12345 + position.z * 1234567);
            hash ^= (hash >> 13);
            hash *= 1274126177u;
            hash ^= (hash >> 16);

            float roll = (hash % 100000u) / 100000f * totalWeight;

            float running = 0f;
            for (int i = 0; i < _sprites.Count; i++)
            {
                if (_sprites[i].sprite == null) continue;
                running += Mathf.Max(0f, _sprites[i].weight);
                if (roll <= running)
                {
                    return _sprites[i].sprite;
                }
            }

            return _sprites[0].sprite;
        }
    }
}
