using System;
using System.Collections.Generic;
using UnityEngine;

namespace Willowstead.Inventory
{
    [Serializable]
    public class ItemIconDefinition
    {
        [Tooltip("Exact item name or alias (e.g. 'Hoe', 'Carrot Seeds', 'Carrot', 'Gold', 'Log').")]
        public string itemName;

        [Tooltip("The sprite icon to use across all UI (Hotbar, Inventory, Shop, Toast Notifications, etc.).")]
        public Sprite icon;
    }

    /// <summary>
    /// Central ScriptableObject asset for registering and overriding item sprites in one place.
    /// Configure this once in the Unity Inspector, and all UI systems will automatically fetch from it.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Willowstead/Inventory/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        private static ItemDatabase _instance;
        public static ItemDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<ItemDatabase>("ItemDatabase");
#if UNITY_EDITOR
                    if (_instance == null)
                    {
                        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemDatabase");
                        if (guids != null && guids.Length > 0)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                            _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
                        }
                    }
#endif
                    if (_instance == null)
                    {
                        var all = Resources.FindObjectsOfTypeAll<ItemDatabase>();
                        if (all != null && all.Length > 0) _instance = all[0];
                    }
                }
                return _instance;
            }
            set => _instance = value;
        }

        [Header("Item Definitions")]
        [Tooltip("Add any item name and drag & drop its Sprite here. UI systems will prioritize this list.")]
        [SerializeField] private List<ItemIconDefinition> _items = new List<ItemIconDefinition>();

        private readonly Dictionary<string, Sprite> _lookupCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private bool _isCacheBuilt = false;

        private void OnEnable()
        {
            BuildCache();
        }

        private void OnValidate()
        {
            BuildCache();
        }

        public void BuildCache()
        {
            _lookupCache.Clear();
            if (_items == null) return;

            foreach (var def in _items)
            {
                if (def != null && !string.IsNullOrWhiteSpace(def.itemName) && def.icon != null)
                {
                    string key = def.itemName.Trim();
                    _lookupCache[key] = def.icon;

                    // Automatically register standard aliases
                    if (key.Equals("Gold", StringComparison.OrdinalIgnoreCase))
                    {
                        _lookupCache["Gold Coin"] = def.icon;
                        _lookupCache["Coin"] = def.icon;
                    }
                    else if (key.Equals("Log", StringComparison.OrdinalIgnoreCase))
                    {
                        _lookupCache["Wood"] = def.icon;
                    }
                    else if (key.EndsWith(" Seeds", StringComparison.OrdinalIgnoreCase))
                    {
                        string singularSeed = key.Substring(0, key.Length - 1); // e.g. "Carrot Seed"
                        _lookupCache[singularSeed] = def.icon;
                    }
                }
            }
            _isCacheBuilt = true;
        }

        /// <summary>
        /// Attempts to get the configured Sprite for an item name. Returns true if found.
        /// </summary>
        public bool TryGetIcon(string itemName, out Sprite icon)
        {
            if (!_isCacheBuilt) BuildCache();
            return _lookupCache.TryGetValue(itemName, out icon);
        }

        /// <summary>
        /// Returns the configured sprite for the given item name, or null if not registered.
        /// </summary>
        public Sprite GetIcon(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;
            if (!_isCacheBuilt) BuildCache();
            _lookupCache.TryGetValue(itemName, out Sprite icon);
            return icon;
        }
    }
}
