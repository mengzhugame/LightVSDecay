// ============================================================
// SystemUnlockManager.cs
// 文件位置: Assets/Scripts/Logic/SystemUnlockManager.cs
//
// 解锁规则（v2 — 按第1波完成触发）：
//   科技树  ：第一章（chapterIndex=0）打完第1波（wavesCleared>=1）
//             不管胜负，结算面板出现时触发
//   装备系统：第二章（chapterIndex=1）打完第1波（wavesCleared>=1）
//             不管胜负，结算面板出现时触发
//
// Pending 通知机制：
//   战斗场景结算时写 "待通知" PlayerPrefs Key；
//   MainScene 启动时由 MainSceneUIManager.Start() 调用
//   ConsumePendingNotifications() 消费这些 Key，
//   得到需要展示的提示文字列表，驱动 TipsPanelController。
// ============================================================

using System;
using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Logic
{
    public class SystemUnlockManager : PersistentSingleton<SystemUnlockManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PlayerPrefs Keys
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private const string KEY_EQUIPMENT_UNLOCKED   = "SysUnlock_Equipment_v1";
        private const string KEY_TECH_TREE_UNLOCKED   = "SysUnlock_TechTree_v1";
        private const string KEY_TOTAL_BATTLES_PLAYED = "SysUnlock_TotalBattles_v1";

        // 待通知 Key（在战斗场景中写入，回到 MainScene 后消费并清除）
        private const string KEY_PENDING_TECH_NOTIFY  = "SysUnlock_PendingTechNotify";
        private const string KEY_PENDING_EQUIP_NOTIFY = "SysUnlock_PendingEquipNotify";

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [SerializeField] private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>装备系统首次解锁时触发（同帧）</summary>
        public static event Action OnEquipmentSystemUnlocked;

        /// <summary>科技树首次解锁时触发（同帧）</summary>
        public static event Action OnTechTreeUnlocked;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private bool _isEquipmentUnlocked;
        private bool _isTechTreeUnlocked;
        private int  _totalBattlesPlayed;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public bool IsEquipmentUnlocked  => _isEquipmentUnlocked;
        public bool IsTechTreeUnlocked   => _isTechTreeUnlocked;
        public int  TotalBattlesPlayed   => _totalBattlesPlayed;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void Awake()
        {
            base.Awake();
            Load();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 核心：战斗结算后调用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 每局战斗结算时由 SettlementPanel 调用。
        /// </summary>
        /// <param name="chapterIndex">本局章节索引（0-based）</param>
        /// <param name="difficulty">本局难度（1-5）</param>
        /// <param name="isVictory">是否通关</param>
        /// <param name="wavesCleared">本局实际完成的波次数（失败时=死前最后完成的波）</param>
        /// <returns>本次新解锁的系统列表</returns>
        public UnlockResult CheckAndProcessUnlocks(
            int chapterIndex, int difficulty, bool isVictory, int wavesCleared = 0)
        {
            var result = new UnlockResult();

            _totalBattlesPlayed++;

            // ── 科技树：第一章打完第1波即解锁（不要求胜利）────────
            if (!_isTechTreeUnlocked && chapterIndex == 0 && wavesCleared >= 1)
            {
                _isTechTreeUnlocked = true;
                result.techTreeNewlyUnlocked = true;
                OnTechTreeUnlocked?.Invoke();

                // 写入 Pending 通知 Key，供 MainScene 启动时弹窗
                PlayerPrefs.SetInt(KEY_PENDING_TECH_NOTIFY, 1);

                if (showDebugInfo)
                    Debug.Log("[SystemUnlockManager] ✅ 科技树已解锁！（第一章第1波完成）");
            }

            // ── 装备系统：第二章打完第1波即解锁（不要求胜利）──────
            if (!_isEquipmentUnlocked && chapterIndex == 1 && wavesCleared >= 1)
            {
                _isEquipmentUnlocked = true;
                result.equipmentSystemNewlyUnlocked = true;
                OnEquipmentSystemUnlocked?.Invoke();

                // 写入 Pending 通知 Key
                PlayerPrefs.SetInt(KEY_PENDING_EQUIP_NOTIFY, 1);

                if (showDebugInfo)
                    Debug.Log("[SystemUnlockManager] ✅ 装备系统已解锁！（第二章第1波完成）");
            }

            Save();

            if (showDebugInfo)
                Debug.Log($"[SystemUnlockManager] 结算处理: " +
                          $"Chapter={chapterIndex + 1}, Diff={difficulty}, " +
                          $"Victory={isVictory}, WavesCleared={wavesCleared}, " +
                          $"TotalBattles={_totalBattlesPlayed}");

            return result;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Pending 通知消费（由 MainSceneUIManager.Start() 调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 消费并清除所有"待通知"标记，返回需要在 MainScene 展示的提示文字列表。
        /// 每次 MainScene 加载时调用一次即可。
        /// </summary>
        /// <returns>待展示的提示字符串列表（顺序：科技树在前，装备在后）</returns>
        public System.Collections.Generic.List<string> ConsumePendingNotifications()
        {
            var messages = new System.Collections.Generic.List<string>();

            bool pendingTech  = PlayerPrefs.GetInt(KEY_PENDING_TECH_NOTIFY,  0) == 1;
            bool pendingEquip = PlayerPrefs.GetInt(KEY_PENDING_EQUIP_NOTIFY, 0) == 1;

            if (pendingTech)
            {
                messages.Add("🔓 科技树系统已解锁！快去升级分支能力吧！");
                PlayerPrefs.DeleteKey(KEY_PENDING_TECH_NOTIFY);
            }

            if (pendingEquip)
            {
                messages.Add("🔓 装备合成系统已解锁！打造属于你的专属装备！");
                PlayerPrefs.DeleteKey(KEY_PENDING_EQUIP_NOTIFY);
            }

            if (messages.Count > 0)
            {
                PlayerPrefs.Save();

                if (showDebugInfo)
                    Debug.Log($"[SystemUnlockManager] 消费 {messages.Count} 条待通知：" +
                              string.Join(", ", messages));
            }

            return messages;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 强制解锁（调试 / 外部购买 / 引导系统用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void ForceUnlockEquipment()
        {
            if (_isEquipmentUnlocked) return;
            _isEquipmentUnlocked = true;
            Save();
            OnEquipmentSystemUnlocked?.Invoke();

            if (showDebugInfo)
                Debug.Log("[SystemUnlockManager] 强制解锁：装备系统");
        }

        public void ForceUnlockTechTree()
        {
            if (_isTechTreeUnlocked) return;
            _isTechTreeUnlocked = true;
            Save();
            OnTechTreeUnlocked?.Invoke();

            if (showDebugInfo)
                Debug.Log("[SystemUnlockManager] 强制解锁：科技树");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 全量重置（供 PlayerDataResetTool 和 DebugMenu 调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 清除所有解锁状态及待通知标记（内存 + PlayerPrefs）
        /// </summary>
        public void ResetAll()
        {
            _isEquipmentUnlocked = false;
            _isTechTreeUnlocked  = false;
            _totalBattlesPlayed  = 0;

            PlayerPrefs.DeleteKey(KEY_EQUIPMENT_UNLOCKED);
            PlayerPrefs.DeleteKey(KEY_TECH_TREE_UNLOCKED);
            PlayerPrefs.DeleteKey(KEY_TOTAL_BATTLES_PLAYED);
            PlayerPrefs.DeleteKey(KEY_PENDING_TECH_NOTIFY);
            PlayerPrefs.DeleteKey(KEY_PENDING_EQUIP_NOTIFY);
            PlayerPrefs.Save();

            if (showDebugInfo)
                Debug.Log("[SystemUnlockManager] 所有解锁状态已重置");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 存档
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Save()
        {
            PlayerPrefs.SetInt(KEY_EQUIPMENT_UNLOCKED,   _isEquipmentUnlocked ? 1 : 0);
            PlayerPrefs.SetInt(KEY_TECH_TREE_UNLOCKED,   _isTechTreeUnlocked  ? 1 : 0);
            PlayerPrefs.SetInt(KEY_TOTAL_BATTLES_PLAYED, _totalBattlesPlayed);
            PlayerPrefs.Save();
        }

        private void Load()
        {
            _isEquipmentUnlocked = PlayerPrefs.GetInt(KEY_EQUIPMENT_UNLOCKED,   0) == 1;
            _isTechTreeUnlocked  = PlayerPrefs.GetInt(KEY_TECH_TREE_UNLOCKED,   0) == 1;
            _totalBattlesPlayed  = PlayerPrefs.GetInt(KEY_TOTAL_BATTLES_PLAYED, 0);

            if (showDebugInfo)
                Debug.Log($"[SystemUnlockManager] 读档: " +
                          $"Equipment={_isEquipmentUnlocked}, " +
                          $"TechTree={_isTechTreeUnlocked}, " +
                          $"Battles={_totalBattlesPlayed}");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Editor 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

#if UNITY_EDITOR
        [ContextMenu("Debug: Reset All")]
        private void DebugResetAll() => ResetAll();

        [ContextMenu("Debug: Unlock All")]
        private void DebugUnlockAll()
        {
            ForceUnlockEquipment();
            ForceUnlockTechTree();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrint()
        {
            Debug.Log($"[SystemUnlockManager] Equipment={_isEquipmentUnlocked}, " +
                      $"TechTree={_isTechTreeUnlocked}, Battles={_totalBattlesPlayed}\n" +
                      $"PendingTech={PlayerPrefs.GetInt(KEY_PENDING_TECH_NOTIFY, 0)}, " +
                      $"PendingEquip={PlayerPrefs.GetInt(KEY_PENDING_EQUIP_NOTIFY, 0)}");
        }
#endif
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 解锁结果（供 SettlementPanel 使用）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public class UnlockResult
    {
        public bool equipmentSystemNewlyUnlocked;
        public bool techTreeNewlyUnlocked;
        public bool HasAnyUnlock => equipmentSystemNewlyUnlocked || techTreeNewlyUnlocked;
    }
}