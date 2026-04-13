using System;
using System.Collections.Generic;
using LightVsDecay.Core;
using UnityEngine;

namespace LightVsDecay.Logic
{
    public class SystemUnlockManager : PersistentSingleton<SystemUnlockManager>
    {
        private const string KEY_EQUIPMENT_UNLOCKED = "SysUnlock_Equipment_v1";
        private const string KEY_TECH_TREE_UNLOCKED = "SysUnlock_TechTree_v1";
        private const string KEY_TOTAL_BATTLES_PLAYED = "SysUnlock_TotalBattles_v1";
        private const string KEY_PENDING_TECH_NOTIFY = "SysUnlock_PendingTechNotify";
        private const string KEY_PENDING_EQUIP_NOTIFY = "SysUnlock_PendingEquipNotify";

        [SerializeField] private bool showDebugInfo = false;

        public static event Action OnEquipmentSystemUnlocked;
        public static event Action OnTechTreeUnlocked;

        private bool _isEquipmentUnlocked;
        private bool _isTechTreeUnlocked;
        private int _totalBattlesPlayed;

        public bool IsEquipmentUnlocked => _isEquipmentUnlocked;
        public bool IsTechTreeUnlocked => _isTechTreeUnlocked;
        public int TotalBattlesPlayed => _totalBattlesPlayed;

        protected override void Awake()
        {
            base.Awake();
            Load();
        }

        public UnlockResult CheckAndProcessUnlocks(int chapterIndex, int difficulty, bool isVictory, int wavesCleared = 0)
        {
            var result = new UnlockResult();

            _totalBattlesPlayed++;

            if (!_isTechTreeUnlocked && _totalBattlesPlayed == 1)
            {
                _isTechTreeUnlocked = true;
                result.techTreeNewlyUnlocked = true;
                OnTechTreeUnlocked?.Invoke();
                PlayerPrefs.SetInt(KEY_PENDING_TECH_NOTIFY, 1);

                if (showDebugInfo)
                {
                    GameLogger.Log("[SystemUnlockManager] Tech tree unlocked after first settlement.");
                }
            }

            if (!_isEquipmentUnlocked && isVictory && chapterIndex == 1 && difficulty == 1)
            {
                _isEquipmentUnlocked = true;
                result.equipmentSystemNewlyUnlocked = true;
                OnEquipmentSystemUnlocked?.Invoke();
                PlayerPrefs.SetInt(KEY_PENDING_EQUIP_NOTIFY, 1);

                if (showDebugInfo)
                {
                    GameLogger.Log("[SystemUnlockManager] Equipment unlocked after Chapter 2 Difficulty 1 clear.");
                }
            }

            Save();

            if (showDebugInfo)
            {
                GameLogger.Log(
                    $"[SystemUnlockManager] Settlement processed: Chapter={chapterIndex + 1}, Diff={difficulty}, " +
                    $"Victory={isVictory}, WavesCleared={wavesCleared}, TotalBattles={_totalBattlesPlayed}");
            }

            return result;
        }

        public List<string> ConsumePendingNotifications()
        {
            var messages = new List<string>();

            bool pendingTech = PlayerPrefs.GetInt(KEY_PENDING_TECH_NOTIFY, 0) == 1;
            bool pendingEquip = PlayerPrefs.GetInt(KEY_PENDING_EQUIP_NOTIFY, 0) == 1;

            if (pendingTech)
            {
                messages.Add("科技树系统已解锁！永久强化你的战力！");
                PlayerPrefs.DeleteKey(KEY_PENDING_TECH_NOTIFY);
            }

            if (pendingEquip)
            {
                messages.Add("装备系统已解锁！强化你的光棱塔！");
                PlayerPrefs.DeleteKey(KEY_PENDING_EQUIP_NOTIFY);
            }

            if (messages.Count > 0)
            {
                PlayerPrefs.Save();
            }

            return messages;
        }

        public void ForceUnlockEquipment()
        {
            if (_isEquipmentUnlocked)
            {
                return;
            }

            _isEquipmentUnlocked = true;
            Save();
            OnEquipmentSystemUnlocked?.Invoke();
        }

        public void ForceUnlockTechTree()
        {
            if (_isTechTreeUnlocked)
            {
                return;
            }

            _isTechTreeUnlocked = true;
            Save();
            OnTechTreeUnlocked?.Invoke();
        }

        public void ResetAll()
        {
            _isEquipmentUnlocked = false;
            _isTechTreeUnlocked = false;
            _totalBattlesPlayed = 0;

            PlayerPrefs.DeleteKey(KEY_EQUIPMENT_UNLOCKED);
            PlayerPrefs.DeleteKey(KEY_TECH_TREE_UNLOCKED);
            PlayerPrefs.DeleteKey(KEY_TOTAL_BATTLES_PLAYED);
            PlayerPrefs.DeleteKey(KEY_PENDING_TECH_NOTIFY);
            PlayerPrefs.DeleteKey(KEY_PENDING_EQUIP_NOTIFY);
            PlayerPrefs.Save();
        }

        private void Save()
        {
            PlayerPrefs.SetInt(KEY_EQUIPMENT_UNLOCKED, _isEquipmentUnlocked ? 1 : 0);
            PlayerPrefs.SetInt(KEY_TECH_TREE_UNLOCKED, _isTechTreeUnlocked ? 1 : 0);
            PlayerPrefs.SetInt(KEY_TOTAL_BATTLES_PLAYED, _totalBattlesPlayed);
            PlayerPrefs.Save();
        }

        private void Load()
        {
            _isEquipmentUnlocked = PlayerPrefs.GetInt(KEY_EQUIPMENT_UNLOCKED, 0) == 1;
            _isTechTreeUnlocked = PlayerPrefs.GetInt(KEY_TECH_TREE_UNLOCKED, 0) == 1;
            _totalBattlesPlayed = PlayerPrefs.GetInt(KEY_TOTAL_BATTLES_PLAYED, 0);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Reset All")]
        private void DebugResetAll() => ResetAll();

        [ContextMenu("Debug: Unlock All")]
        private void DebugUnlockAll()
        {
            ForceUnlockEquipment();
            ForceUnlockTechTree();
        }
#endif
    }

    public class UnlockResult
    {
        public bool equipmentSystemNewlyUnlocked;
        public bool techTreeNewlyUnlocked;

        public bool HasAnyUnlock => equipmentSystemNewlyUnlocked || techTreeNewlyUnlocked;
    }
}
