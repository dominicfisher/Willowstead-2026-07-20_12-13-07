using System;
using System.Collections.Generic;
using UnityEngine;
using Willowstead.Player;

namespace Willowstead.World
{
    public enum ObjectiveId
    {
        TillSoil,        // Till farmland with Hoe
        WaterSoil,       // Water dry soil with Watering Can
        PlantCrops,      // Plant seeds
        HarvestCrop,     // Harvest a mature crop
        ChopTree,        // Chop a tree with Axe
        VisitShop,       // Open Shop & trade
        CheckSkills      // Open Skills Journal (K)
    }

    [Serializable]
    public class ObjectiveData
    {
        public ObjectiveId id;
        public string title;
        public string instruction;
        public int currentCount;
        public int targetCount;
        public bool isCompleted;

        public ObjectiveData(ObjectiveId id, string title, string instruction, int targetCount)
        {
            this.id = id;
            this.title = title;
            this.instruction = instruction;
            this.targetCount = targetCount;
            this.currentCount = 0;
            this.isCompleted = false;
        }
    }

    /// <summary>
    /// Manages the player's early-game guidance objectives and tracks progress automatically.
    /// </summary>
    public class ObjectiveManager : MonoBehaviour
    {
        public static ObjectiveManager Instance { get; private set; }

        public event Action OnObjectivesUpdated;
        public event Action<ObjectiveData> OnObjectiveCompleted;

        private readonly List<ObjectiveData> _objectives = new List<ObjectiveData>();
        public IReadOnlyList<ObjectiveData> Objectives => _objectives;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[ObjectiveManager]");
            DontDestroyOnLoad(go);
            go.AddComponent<ObjectiveManager>();
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

            InitDefaultObjectives();
        }

        private void InitDefaultObjectives()
        {
            _objectives.Clear();
            _objectives.Add(new ObjectiveData(ObjectiveId.TillSoil, "Till Farmland", "Equip Hoe (1) & Left Click", 3));
            _objectives.Add(new ObjectiveData(ObjectiveId.WaterSoil, "Water the Soil", "Equip Watering Can (2) & click dirt", 3));
            _objectives.Add(new ObjectiveData(ObjectiveId.PlantCrops, "Plant Seeds", "Equip Seeds (4) & click tilled soil", 2));
            _objectives.Add(new ObjectiveData(ObjectiveId.ChopTree, "Gather Timber", "Equip Axe (3) & chop a tree", 1));
            _objectives.Add(new ObjectiveData(ObjectiveId.CheckSkills, "Field Skills", "Press 'K' to open your Skills Journal", 1));
            _objectives.Add(new ObjectiveData(ObjectiveId.VisitShop, "Town Merchant", "Press 'B' to trade produce / buy seeds", 1));
            _objectives.Add(new ObjectiveData(ObjectiveId.HarvestCrop, "First Harvest", "Harvest a ripe crop when fully grown", 1));
        }

        public void ReportProgress(ObjectiveId id, int amount = 1)
        {
            for (int i = 0; i < _objectives.Count; i++)
            {
                var obj = _objectives[i];
                if (obj.id == id && !obj.isCompleted)
                {
                    obj.currentCount = Mathf.Min(obj.targetCount, obj.currentCount + amount);
                    if (obj.currentCount >= obj.targetCount)
                    {
                        obj.isCompleted = true;
                        OnObjectiveCompleted?.Invoke(obj);
                    }
                    OnObjectivesUpdated?.Invoke();
                    break;
                }
            }
        }

        public bool IsAllCompleted()
        {
            for (int i = 0; i < _objectives.Count; i++)
            {
                if (!_objectives[i].isCompleted) return false;
            }
            return true;
        }
    }
}
