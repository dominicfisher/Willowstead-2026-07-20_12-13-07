using System;
using System.Collections.Generic;
using UnityEngine;

namespace Willowstead.Player
{
    public enum SkillType
    {
        Farming,
        Fishing,
        Mining,
        Woodcutting,
        Cooking,
        Ranching,
        Building,
        Exploring
    }

    [System.Serializable]
    public class SkillData
    {
        public SkillType skillType;
        public int level = 1;
        public int currentXP = 0;

        public int XPForNextLevel => GetRequiredXPForLevel(level);

        public static int GetRequiredXPForLevel(int lvl)
        {
            // Cozy progressive curve: Lvl 1->2: 100 XP, Lvl 2->3: 220 XP, Lvl 3->4: 360 XP, etc.
            return Mathf.RoundToInt(80f * Mathf.Pow(lvl, 1.25f) + 20f * lvl);
        }
    }

    /// <summary>
    /// Manages player skill levels, XP progression, and level-up events for all 8 skills:
    /// Farming, Fishing, Mining, Woodcutting, Cooking, Ranching, Building, and Exploring.
    /// </summary>
    public class SkillsManager : MonoBehaviour
    {
        public static SkillsManager Instance { get; private set; }

        public event Action<SkillType, int, int> OnSkillXPAdded; // (skill, newXP, requiredXP)
        public event Action<SkillType, int> OnSkillLevelUp;      // (skill, newLevel)

        private readonly Dictionary<SkillType, SkillData> _skills = new Dictionary<SkillType, SkillData>();

        private Vector3 _lastPlayerPosition;
        private float _exploreDistanceAccumulator = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            InitSkills();
        }

        private void Start()
        {
            _lastPlayerPosition = transform.position;
        }

        private void Update()
        {
            TrackExploringXP();
        }

        private void InitSkills()
        {
            foreach (SkillType type in Enum.GetValues(typeof(SkillType)))
            {
                if (!_skills.ContainsKey(type))
                {
                    _skills[type] = new SkillData
                    {
                        skillType = type,
                        level = 1,
                        currentXP = 0
                    };
                }
            }
        }

        public SkillData GetSkill(SkillType type)
        {
            if (!_skills.ContainsKey(type)) InitSkills();
            return _skills[type];
        }

        public IReadOnlyDictionary<SkillType, SkillData> GetAllSkills()
        {
            if (_skills.Count == 0) InitSkills();
            return _skills;
        }

        public void AddXP(SkillType type, int amount)
        {
            if (amount <= 0) return;
            if (!_skills.TryGetValue(type, out var data))
            {
                InitSkills();
                data = _skills[type];
            }

            data.currentXP += amount;
            int req = data.XPForNextLevel;

            while (data.currentXP >= req)
            {
                data.currentXP -= req;
                data.level++;
                req = data.XPForNextLevel;

                OnSkillLevelUp?.Invoke(type, data.level);
                TriggerLevelUpNotification(type, data.level);
            }

            OnSkillXPAdded?.Invoke(type, data.currentXP, req);
        }

        private void TriggerLevelUpNotification(SkillType type, int newLevel)
        {
            string skillName = type.ToString();
            Debug.Log($"<color=#FFD700>★ LEVEL UP!</color> Your <b>{skillName}</b> skill reached Level {newLevel}!");

            if (Player.ItemNotificationManager.Instance != null)
            {
                Player.ItemNotificationManager.Instance.TriggerNotification($"★ {skillName} Level {newLevel}!", UIResourceHelper.GetSparkleStarSprite(), new Color(1f, 0.88f, 0.35f, 1f));
            }
        }

        private void TrackExploringXP()
        {
            float dist = Vector3.Distance(transform.position, _lastPlayerPosition);
            if (dist > 0.01f && dist < 10f) // Sanity check against teleports
            {
                _exploreDistanceAccumulator += dist;
                if (_exploreDistanceAccumulator >= 18f)
                {
                    _exploreDistanceAccumulator = 0f;
                    AddXP(SkillType.Exploring, 6);
                }
            }
            _lastPlayerPosition = transform.position;
        }
    }
}
