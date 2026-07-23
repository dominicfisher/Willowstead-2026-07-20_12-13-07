using System.Collections.Generic;
using UnityEngine;

namespace Willowstead.Player
{
    /// <summary>
    /// Manages the player's items (seeds, crops, etc.) using a basic dictionary.
    /// Supports starting items configuration in the Inspector and provides helper methods
    /// to add, remove, and check item quantities.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        [System.Serializable]
        public struct StartingItem
        {
            public string itemName;
            public int quantity;
        }

        [Header("Starting Items")]
        [Tooltip("Configure items the player starts with in their inventory.")]
        [SerializeField] private List<StartingItem> _startingItems = new List<StartingItem>
        {
            new StartingItem { itemName = "Carrot Seeds", quantity = 10 },
            new StartingItem { itemName = "Gold", quantity = 100 }
        };

        private Dictionary<string, int> _inventory = new Dictionary<string, int>();

        private void Awake()
        {
            // Populate inventory with starting items
            foreach (var item in _startingItems)
            {
                if (!string.IsNullOrEmpty(item.itemName))
                {
                    _inventory[item.itemName] = item.quantity;
                }
            }
            PrintInventory();
        }

        /// <summary>
        /// Adds a quantity of an item to the inventory.
        /// </summary>
        public void AddItem(string itemName, int amount)
        {
            if (amount <= 0) return;

            if (_inventory.ContainsKey(itemName))
            {
                _inventory[itemName] += amount;
            }
            else
            {
                _inventory[itemName] = amount;
            }

            if (ItemNotificationManager.Instance != null)
            {
                ItemNotificationManager.Instance.TriggerPickupNotification(itemName, amount);
            }

            Debug.Log($"[Inventory] Added {amount}x {itemName}. New total: {_inventory[itemName]}");
            PrintInventory();
        }

        /// <summary>
        /// Removes a quantity of an item from the inventory. Returns false if not enough items.
        /// </summary>
        public bool RemoveItem(string itemName, int amount)
        {
            if (amount <= 0) return true;

            if (!_inventory.ContainsKey(itemName) || _inventory[itemName] < amount)
            {
                Debug.LogWarning($"[Inventory] Cannot remove {amount}x {itemName}; not enough items!");
                return false;
            }

            _inventory[itemName] -= amount;

            if (ItemNotificationManager.Instance != null)
            {
                ItemNotificationManager.Instance.TriggerPickupNotification(itemName, -amount);
            }

            Debug.Log($"[Inventory] Removed {amount}x {itemName}. New total: {_inventory[itemName]}");
            PrintInventory();
            return true;
        }

        /// <summary>
        /// Checks if the inventory contains at least the specified amount of an item.
        /// </summary>
        public bool HasItem(string itemName, int amount)
        {
            if (amount <= 0) return true;
            return _inventory.ContainsKey(itemName) && _inventory[itemName] >= amount;
        }

        /// <summary>
        /// Returns the current quantity of an item.
        /// </summary>
        public int GetItemCount(string itemName)
        {
            if (_inventory.TryGetValue(itemName, out int count))
            {
                return count;
            }
            return 0;
        }

        /// <summary>
        /// Exposes a copy of the internal inventory dictionary safely.
        /// </summary>
        public Dictionary<string, int> GetInventoryData()
        {
            return new Dictionary<string, int>(_inventory);
        }

        /// <summary>
        /// Prints the current inventory state to the console logs.
        /// </summary>
        public void PrintInventory()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("--- Current Inventory ---");
            foreach (var kvp in _inventory)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine("-------------------------");
            Debug.Log(sb.ToString());
        }
    }
}
