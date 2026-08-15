using System.Collections.Generic;
using UnityEngine;

namespace Willowstead.Player
{
    /// <summary>
    /// Represents a single slot in the inventory.
    /// </summary>
    [System.Serializable]
    public class InventorySlot
    {
        public string itemName;
        public int quantity;

        public bool IsEmpty => string.IsNullOrEmpty(itemName) || quantity <= 0;

        public void Clear()
        {
            itemName = "";
            quantity = 0;
        }
    }

    /// <summary>
    /// Manages the player's items using a unified slot-based system (24 slots total).
    /// Index 0-7 represents the Hotbar slots, and index 8-23 represents the main inventory slots.
    /// Gold is tracked separately as a currency and does not occupy a slot.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        [System.Serializable]
        public struct StartingItem
        {
            public string itemName;
            public int quantity;
        }

        [Header("Item Database (Global Icon Registry)")]
        [Tooltip("Optional: Assign your ItemDatabase asset here, or leave empty to automatically load from Resources/ItemDatabase.")]
        [SerializeField] private Willowstead.Inventory.ItemDatabase _itemDatabase;

        [Header("Starting Items")]
        [Tooltip("Configure additional starting items. Hoe, Watering Can, and Axe are force-equipped in hotbar slots 0, 1, and 2 automatically.")]
        [SerializeField] private List<StartingItem> _startingItems = new List<StartingItem>
        {
            // "prefer main inventory" path can't accidentally bury it.
            new StartingItem { itemName = "Carrot Seeds", quantity = 10 },
            new StartingItem { itemName = "Gold", quantity = 100 }
        };

        // 24 Slots: 0-7 for Hotbar, 8-23 for main Inventory panel.
        public InventorySlot[] slots = new InventorySlot[24];

        private int _gold = 0;

        public static InventoryManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            if (_itemDatabase != null)
            {
                Willowstead.Inventory.ItemDatabase.Instance = _itemDatabase;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new InventorySlot();
            }

            slots[0].itemName = "Hoe";
            slots[0].quantity = 1;

            slots[1].itemName = "Watering Can";
            slots[1].quantity = 1;

            // the woodcutting tool out of the player's 1-8 quick-swap.
            slots[2].itemName = "Axe";
            slots[2].quantity = 1;

            foreach (var item in _startingItems)
            {
                if (string.IsNullOrEmpty(item.itemName)) continue;

                if (item.itemName == "Gold")
                {
                    _gold = item.quantity;
                }
                else
                {
                    if (item.itemName == "Hoe" || item.itemName == "Watering Can") continue;

                    AddItem(item.itemName, item.quantity);
                }
            }

#if UNITY_EDITOR
            PrintInventory();
#endif
        }

        /// <summary>
        /// Swaps the items in two inventory/hotbar slots.
        /// </summary>
        public void SwapSlots(int indexA, int indexB)
        {
            if (indexA < 0 || indexA >= slots.Length || indexB < 0 || indexB >= slots.Length) return;

            InventorySlot temp = new InventorySlot
            {
                itemName = slots[indexA].itemName,
                quantity = slots[indexA].quantity
            };

            slots[indexA].itemName = slots[indexB].itemName;
            slots[indexA].quantity = slots[indexB].quantity;

            slots[indexB].itemName = temp.itemName;
            slots[indexB].quantity = temp.quantity;

#if UNITY_EDITOR
            Debug.Log($"[InventoryManager] Swapped Slot {indexA} with Slot {indexB}");
            PrintInventory();
#endif
        }

        /// <summary>
        /// Returns the item inside a specific slot.
        /// </summary>
        public InventorySlot GetSlotItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return null;
            return slots[slotIndex];
        }

        /// <summary>
        /// Deducts quantity directly from a specified slot.
        /// </summary>
        public void RemoveItemFromSlot(int slotIndex, int amount)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return;
            InventorySlot slot = slots[slotIndex];

            if (slot.IsEmpty || amount <= 0) return;

            int toRemove = Mathf.Min(slot.quantity, amount);
            slot.quantity -= toRemove;

            if (ItemNotificationManager.Instance != null)
            {
                ItemNotificationManager.Instance.TriggerPickupNotification(slot.itemName, -toRemove);
            }

            if (slot.quantity <= 0)
            {
                slot.Clear();
            }

#if UNITY_EDITOR
            PrintInventory();
#endif
        }

        /// <summary>
        /// Adds a quantity of an item to the slots (or gold account).
        /// </summary>
        public void AddItem(string itemName, int amount)
        {
            if (amount <= 0) return;

            if (itemName == "Gold")
            {
                _gold += amount;
                if (ItemNotificationManager.Instance != null)
                {
                    ItemNotificationManager.Instance.TriggerPickupNotification(itemName, amount);
                }
                return;
            }

            int remaining = amount;

            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty && slots[i].itemName == itemName)
                {
                    slots[i].quantity += remaining;
                    remaining = 0;
                    break;
                }
            }

            if (remaining > 0)
            {
                // First try to place in empty Hotbar slots (0..7)
                for (int i = 0; i < 8; i++)
                {
                    if (slots[i].IsEmpty)
                    {
                        slots[i].itemName = itemName;
                        slots[i].quantity = remaining;
                        remaining = 0;
                        break;
                    }
                }

                // If hotbar is full, place in main Inventory slots (8..23)
                if (remaining > 0)
                {
                    for (int i = 8; i < slots.Length; i++)
                    {
                        if (slots[i].IsEmpty)
                        {
                            slots[i].itemName = itemName;
                            slots[i].quantity = remaining;
                            remaining = 0;
                            break;
                        }
                    }
                }
            }

            if (remaining > 0)
            {
                Debug.LogWarning($"[InventoryManager] Inventory is full! Could not add {remaining}x {itemName}");
            }

            if (ItemNotificationManager.Instance != null)
            {
                ItemNotificationManager.Instance.TriggerPickupNotification(itemName, amount - remaining);
            }

#if UNITY_EDITOR
            PrintInventory();
#endif
        }

        /// <summary>
        /// Removes a quantity of an item from the inventory. Returns false if not enough items.
        /// </summary>
        public bool RemoveItem(string itemName, int amount)
        {
            if (amount <= 0) return true;

            if (itemName == "Gold")
            {
                if (_gold >= amount)
                {
                    _gold -= amount;
                    if (ItemNotificationManager.Instance != null)
                    {
                        ItemNotificationManager.Instance.TriggerPickupNotification(itemName, -amount);
                    }
                    return true;
                }
                return false;
            }

            if (GetItemCount(itemName) < amount)
            {
                Debug.LogWarning($"[InventoryManager] Cannot remove {amount}x {itemName}; not enough items!");
                return false;
            }

            int remaining = amount;

            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty && slots[i].itemName == itemName)
                {
                    int toSubtract = Mathf.Min(slots[i].quantity, remaining);
                    slots[i].quantity -= toSubtract;
                    remaining -= toSubtract;

                    if (slots[i].quantity <= 0)
                    {
                        slots[i].Clear();
                    }

                    if (remaining <= 0) break;
                }
            }

            if (ItemNotificationManager.Instance != null)
            {
                ItemNotificationManager.Instance.TriggerPickupNotification(itemName, -amount);
            }

#if UNITY_EDITOR
            PrintInventory();
#endif
            return true;
        }

        /// <summary>
        /// Checks if the inventory contains at least the specified amount of an item.
        /// </summary>
        public bool HasItem(string itemName, int amount)
        {
            if (amount <= 0) return true;
            return GetItemCount(itemName) >= amount;
        }

        /// <summary>
        /// Returns the total quantity of an item across all slots (or gold balance).
        /// </summary>
        /// <summary>Read every slot out for serialization.</summary>
        public List<Willowstead.Persistence.SavedInventorySlot> CaptureInventory()
        {
            var list = new List<Willowstead.Persistence.SavedInventorySlot>(slots != null ? slots.Length : 0);
            if (slots == null) return list;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                list.Add(new Willowstead.Persistence.SavedInventorySlot
                {
                    itemName = (slot != null && !slot.IsEmpty) ? slot.itemName : string.Empty,
                    quantity = slot != null ? slot.quantity : 0,
                });
            }
            return list;
        }

        /// <summary>
        /// Restore every slot + gold from a save. Overwrites any
        /// starting-items logic so a reloaded save exactly matches what
        /// the player put down before quitting.
        /// </summary>
        public void RestoreInventory(List<Willowstead.Persistence.SavedInventorySlot> data, int gold)
        {
            if (slots == null) return;
            int n = Mathf.Min(data != null ? data.Count : 0, slots.Length);
            for (int i = 0; i < n; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;
                if (data[i] == null || string.IsNullOrEmpty(data[i].itemName))
                {
                    slot.itemName = string.Empty;
                    slot.quantity = 0;
                }
                else
                {
                    slot.itemName = data[i].itemName;
                    slot.quantity = Mathf.Max(0, data[i].quantity);
                }
            }
            // Pad any new slots the save didn't fill (shouldn't normally happen).
            for (int i = n; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].itemName = string.Empty;
                slots[i].quantity = 0;
            }

            // because gold lives as a regular stack like any other item.
            int currentGold = GetItemCount("Gold");
            int delta = gold - currentGold;
            if (delta != 0) AddItem("Gold", delta);

            if (HotbarUI.Instance != null) HotbarUI.Instance.RefreshUI();
            if (InventoryUI.Instance != null) InventoryUI.Instance.RefreshUI();
        }

        public int GetItemCount(string itemName)
        {
            if (itemName == "Gold") return _gold;

            int count = 0;
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty && slot.itemName == itemName)
                {
                    count += slot.quantity;
                }
            }
            return count;
        }

        /// <summary>
        /// Compiles a Dictionary containing total quantities of all items (backward-compatible).
        /// </summary>
        public Dictionary<string, int> GetInventoryData()
        {
            Dictionary<string, int> data = new Dictionary<string, int>();
            data["Gold"] = _gold;

            foreach (var slot in slots)
            {
                if (slot.IsEmpty) continue;

                if (data.ContainsKey(slot.itemName))
                {
                    data[slot.itemName] += slot.quantity;
                }
                else
                {
                    data[slot.itemName] = slot.quantity;
                }
            }
            return data;
        }

        /// <summary>
        /// Prints the current inventory state to the console logs.
        /// </summary>
        public void PrintInventory()
        {
#if UNITY_EDITOR
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("--- Current Inventory Slots ---");
            sb.AppendLine($"- Gold: {_gold}");
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    sb.AppendLine($"- Slot {i}: {slots[i].itemName} x{slots[i].quantity}");
                }
            }
            sb.AppendLine("-------------------------------");
            Debug.Log(sb.ToString());
#endif
        }
    }
}
