// ============================================================
// BattleRewardData.cs
// 文件位置: Assets/Scripts/Logic/BattleReward/BattleRewardData.cs
// 用途：战斗结算奖励数据结构
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Data.SO;

namespace LightVsDecay.Logic.BattleReward
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 单个掉落物
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 战斗结算中的一条掉落记录
    /// </summary>
    [System.Serializable]
    public class BattleDropItem
    {
        /// <summary>掉落类型</summary>
        public DropItemType type;

        /// <summary>装备ID（type == Equipment 时有效）</summary>
        public string equipmentId;

        /// <summary>品质（type == Equipment 时有效）</summary>
        public ItemRarity rarity;

        /// <summary>数量</summary>
        public int count;

        // ─── 快捷构造 ───────────────────────────────────────

        public static BattleDropItem Blueprints(int count)
            => new BattleDropItem { type = DropItemType.Blueprint, count = count };

        public static BattleDropItem Equipment(string id, ItemRarity r, int count = 1)
            => new BattleDropItem { type = DropItemType.Equipment, equipmentId = id, rarity = r, count = count };
    }

    /// <summary>
    /// 掉落类型
    /// </summary>
    public enum DropItemType
    {
        Blueprint  = 0,
        Equipment  = 1,
        Coin       = 2,   // ← 新增
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 结算奖励结果
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 一局战斗的完整奖励结果（由 BattleRewardCalculator 生成，由 ProgressManager 缓存）
    /// </summary>
    [System.Serializable]
    public class BattleRewardResult
    {
        /// <summary>掉落的装备和图纸列表</summary>
        public List<BattleDropItem> drops = new List<BattleDropItem>();

        /// <summary>是否为空（无任何掉落）</summary>
        public bool IsEmpty => drops == null || drops.Count == 0;

        /// <summary>获取图纸总数</summary>
        public int TotalBlueprints()
        {
            int total = 0;
            foreach (var d in drops)
                if (d.type == DropItemType.Blueprint) total += d.count;
            return total;
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 计算器上下文（输入参数）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 战斗上下文，传入计算器
    /// </summary>
    public class BattleContext
    {
        /// <summary>章节索引（0-based）</summary>
        public int chapterIndex;

        /// <summary>难度（1-5）</summary>
        public int difficulty;

        /// <summary>通关的波次数（0 = 全部失败）</summary>
        public int wavesCleared;

        /// <summary>总击杀数</summary>
        public int totalKills;

        /// <summary>是否击杀了 Boss（通关最终波）</summary>
        public bool bossKilled;

        /// <summary>是否满血通关</summary>
        public bool isPerfect;
    }
}