// ============================================================
// BattleRewardConfig.cs
// 文件位置: Assets/Scripts/Data/SO/BattleRewardConfig.cs
// 用途：战斗结算掉落表配置（ScriptableObject）
//
// 创建路径：Assets → Create → LightVsDecay → Battle → Reward Config
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Data.SO;

namespace LightVsDecay.Data.SO
{
    [CreateAssetMenu(
        fileName = "BattleRewardConfig",
        menuName = "LightVsDecay/Battle/Reward Config",
        order = 20)]
    public class BattleRewardConfig : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 图纸设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("图纸掉落")]
        [Tooltip("每通过1波的基础图纸数量")]
        public int blueprintsPerWave = 3;

        [Tooltip("难度乘数（难度1=1.0x, 难度5=2.0x）。共5档，索引0对应难度1")]
        public float[] difficultyMultipliers = { 1.0f, 1.2f, 1.5f, 1.75f, 2.0f };

        [Tooltip("Boss击杀额外奖励图纸")]
        public int bossKillBlueprintBonus = 15;

        [Tooltip("满血通关额外奖励图纸")]
        public int perfectClearBlueprintBonus = 10;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 装备掉落表（按章节）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("装备掉落表（每个元素对应一个章节）")]
        public List<ChapterDropTable> chapterDropTables = new List<ChapterDropTable>();

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 全局掉落控制
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("全局掉落控制")]
        [Tooltip("每局最多掉落几件装备")]
        public int maxEquipmentDropsPerBattle = 3;

        [Tooltip("通过波次≥此值才开始掉落装备（避免速败也能拿装备）")]
        public int minWavesForEquipmentDrop = 3;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 查询方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>获取难度乘数（difficulty 1-5）</summary>
        public float GetDifficultyMultiplier(int difficulty)
        {
            int idx = Mathf.Clamp(difficulty - 1, 0, difficultyMultipliers.Length - 1);
            return difficultyMultipliers[idx];
        }

        /// <summary>获取指定章节的掉落表（没有则返回 null）</summary>
        public ChapterDropTable GetChapterDropTable(int chapterIndex)
        {
            foreach (var t in chapterDropTables)
                if (t.chapterIndex == chapterIndex) return t;
            return null;
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 章节掉落表
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 指定章节的装备掉落配置
    /// </summary>
    [System.Serializable]
    public class ChapterDropTable
    {
        [Tooltip("章节索引（0-based）")]
        public int chapterIndex = 0;

        [Tooltip("本章节可掉落的装备条目列表")]
        public List<EquipmentDropEntry> entries = new List<EquipmentDropEntry>();

        /// <summary>
        /// 根据权重随机抽取 count 件装备ID（有放回抽样，可重复）
        /// </summary>
        public List<EquipmentDropEntry> RollDrops(int count)
        {
            var result = new List<EquipmentDropEntry>();
            if (entries == null || entries.Count == 0) return result;

            // 计算总权重
            float totalWeight = 0f;
            foreach (var e in entries)
                if (e.weight > 0) totalWeight += e.weight;

            if (totalWeight <= 0f) return result;

            for (int i = 0; i < count; i++)
            {
                float roll = Random.Range(0f, totalWeight);
                float accumulated = 0f;
                foreach (var e in entries)
                {
                    accumulated += e.weight;
                    if (roll <= accumulated)
                    {
                        result.Add(e);
                        break;
                    }
                }
            }
            return result;
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 装备掉落条目
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 掉落表中的单个装备条目
    /// </summary>
    [System.Serializable]
    public class EquipmentDropEntry
    {
        [Tooltip("装备ID（需与 EquipmentDatabase 中的 equipmentId 一致）")]
        public string equipmentId;

        [Tooltip("掉落品质")]
        public ItemRarity rarity = ItemRarity.Uncommon;

        [Tooltip("掉落权重（值越大掉落概率越高）")]
        public float weight = 10f;

        [Tooltip("Boss击杀时才能掉落（勾选则普通通关不掉）")]
        public bool bossOnlyDrop = false;
    }
}