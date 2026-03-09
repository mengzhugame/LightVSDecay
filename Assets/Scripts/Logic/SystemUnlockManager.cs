// ============================================================
// SystemUnlockManager.cs
// 文件位置: Assets/Scripts/Logic/SystemUnlockManager.cs
// 用途：管理外围系统（装备系统、科技树）的解锁状态
//
// 解锁规则：
//   装备系统  → 第一局战斗结束（无论输赢）
//   科技树    → 第一章难度1通关（第一次击败Boss）
//   第二章    → 第一章难度1通关
//   第三章    → 第二章难度1通关
//   （以此类推）
// ============================================================

using System;
using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Logic
{
    /// <summary>
    /// 系统解锁管理器（PersistentSingleton）
    /// 挂载到 PersistentManagers 预制体下
    /// </summary>
    public class SystemUnlockManager : PersistentSingleton<SystemUnlockManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PlayerPrefs 存储键
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private const string KEY_EQUIPMENT_UNLOCKED  = "SysUnlock_Equipment_v1";
        private const string KEY_TECH_TREE_UNLOCKED  = "SysUnlock_TechTree_v1";
        private const string KEY_TOTAL_BATTLES_PLAYED = "SysUnlock_TotalBattles_v1";

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>装备系统解锁时触发（仅触发一次）</summary>
        public static event Action OnEquipmentSystemUnlocked;

        /// <summary>科技树解锁时触发（仅触发一次）</summary>
        public static event Action OnTechTreeUnlocked;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private bool _isEquipmentUnlocked;
        private bool _isTechTreeUnlocked;
        private int  _totalBattlesPlayed;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 只读属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>装备合成系统是否已解锁</summary>
        public bool IsEquipmentUnlocked => _isEquipmentUnlocked;

        /// <summary>科技树系统是否已解锁</summary>
        public bool IsTechTreeUnlocked => _isTechTreeUnlocked;

        /// <summary>总战斗局数</summary>
        public int TotalBattlesPlayed => _totalBattlesPlayed;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void Awake()
        {
            base.Awake();
            Load();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 核心：战斗结束后检查并处理解锁
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 每局战斗结束后调用（由 SettlementPanel 或 ProgressManager 调用）
        /// </summary>
        /// <param name="chapterIndex">完成的章节（0-based）</param>
        /// <param name="difficulty">完成的难度（1-5）</param>
        /// <param name="isVictory">是否通关</param>
        /// <returns>本次解锁了哪些系统的文字提示列表（供结算UI展示"新功能已解锁"）</returns>
        public UnlockResult CheckAndProcessUnlocks(int chapterIndex, int difficulty, bool isVictory)
        {
            var unlockResult = new UnlockResult();

            _totalBattlesPlayed++;
            Save();

            // ── 1. 装备系统：第一局战斗结束即解锁 ──────────────
            if (!_isEquipmentUnlocked)
            {
                _isEquipmentUnlocked = true;
                unlockResult.equipmentSystemNewlyUnlocked = true;
                OnEquipmentSystemUnlocked?.Invoke();

                if (showDebugInfo)
                    Debug.Log("[SystemUnlockManager] ✅ 装备合成系统已解锁！");
            }

            // ── 2. 科技树：第一章难度1通关 ──────────────────────
            if (!_isTechTreeUnlocked && isVictory && chapterIndex == 0 && difficulty == 1)
            {
                _isTechTreeUnlocked = true;
                unlockResult.techTreeNewlyUnlocked = true;
                OnTechTreeUnlocked?.Invoke();

                if (showDebugInfo)
                    Debug.Log("[SystemUnlockManager] ✅ 科技树系统已解锁！");
            }

            Save();

            if (showDebugInfo)
                Debug.Log($"[SystemUnlockManager] 战斗结束处理: Chapter={chapterIndex+1}, " +
                          $"Difficulty={difficulty}, Victory={isVictory}, TotalBattles={_totalBattlesPlayed}");

            return unlockResult;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 强制解锁（调试/购买用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void ForceUnlockEquipment()
        {
            if (_isEquipmentUnlocked) return;
            _isEquipmentUnlocked = true;
            Save();
            OnEquipmentSystemUnlocked?.Invoke();
        }

        public void ForceUnlockTechTree()
        {
            if (_isTechTreeUnlocked) return;
            _isTechTreeUnlocked = true;
            Save();
            OnTechTreeUnlocked?.Invoke();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 存档
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Save()
        {
            PlayerPrefs.SetInt(KEY_EQUIPMENT_UNLOCKED,   _isEquipmentUnlocked  ? 1 : 0);
            PlayerPrefs.SetInt(KEY_TECH_TREE_UNLOCKED,   _isTechTreeUnlocked   ? 1 : 0);
            PlayerPrefs.SetInt(KEY_TOTAL_BATTLES_PLAYED, _totalBattlesPlayed);
            PlayerPrefs.Save();
        }

        private void Load()
        {
            _isEquipmentUnlocked  = PlayerPrefs.GetInt(KEY_EQUIPMENT_UNLOCKED,   0) == 1;
            _isTechTreeUnlocked   = PlayerPrefs.GetInt(KEY_TECH_TREE_UNLOCKED,   0) == 1;
            _totalBattlesPlayed   = PlayerPrefs.GetInt(KEY_TOTAL_BATTLES_PLAYED, 0);

            if (showDebugInfo)
                Debug.Log($"[SystemUnlockManager] 读档: Equipment={_isEquipmentUnlocked}, " +
                          $"TechTree={_isTechTreeUnlocked}, Battles={_totalBattlesPlayed}");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

#if UNITY_EDITOR
        [ContextMenu("Debug: Reset All Unlock Flags")]
        private void DebugResetAll()
        {
            _isEquipmentUnlocked = false;
            _isTechTreeUnlocked  = false;
            _totalBattlesPlayed  = 0;
            Save();
            Debug.Log("[SystemUnlockManager] 调试：所有解锁标记已重置");
        }

        [ContextMenu("Debug: Unlock All")]
        private void DebugUnlockAll()
        {
            ForceUnlockEquipment();
            ForceUnlockTechTree();
            Debug.Log("[SystemUnlockManager] 调试：所有系统已解锁");
        }
#endif
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 解锁结果数据
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 本次战斗结束触发的解锁结果（供 SettlementPanel 显示"新系统解锁"提示）
    /// </summary>
    public class UnlockResult
    {
        /// <summary>本次是否首次解锁装备系统</summary>
        public bool equipmentSystemNewlyUnlocked;

        /// <summary>本次是否首次解锁科技树</summary>
        public bool techTreeNewlyUnlocked;

        /// <summary>是否有任何新解锁</summary>
        public bool HasAnyUnlock => equipmentSystemNewlyUnlocked || techTreeNewlyUnlocked;
    }
}