// ============================================================
// BattleRewardCalculator.cs
// 文件位置: Assets/Scripts/Logic/BattleReward/BattleRewardCalculator.cs
// 用途：战斗结算奖励计算器（纯静态逻辑，无 MonoBehaviour）
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Data.SO;

namespace LightVsDecay.Logic.BattleReward
{
    /// <summary>
    /// 战斗奖励计算器
    /// 输入：BattleContext + BattleRewardConfig
    /// 输出：BattleRewardResult（含掉落列表）
    /// </summary>
    public static class BattleRewardCalculator
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 核心计算入口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 根据战斗上下文计算奖励列表
        /// </summary>
        public static BattleRewardResult Calculate(BattleRewardConfig config, BattleContext ctx)
        {
            var result = new BattleRewardResult();

            if (config == null || ctx == null)
            {
                Debug.LogWarning("[BattleRewardCalculator] config 或 ctx 为 null，返回空奖励");
                return result;
            }

            // ── 1. 计算图纸 ────────────────────────────────────
            int blueprints = CalculateBlueprints(config, ctx);
            if (blueprints > 0)
                result.drops.Add(BattleDropItem.Blueprints(blueprints));

            // ── 2. 计算装备掉落 ────────────────────────────────
            if (ctx.wavesCleared >= config.minWavesForEquipmentDrop)
            {
                var equipDrops = CalculateEquipmentDrops(config, ctx);
                result.drops.AddRange(equipDrops);
            }

            return result;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 图纸计算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static int CalculateBlueprints(BattleRewardConfig config, BattleContext ctx)
        {
            if (ctx.wavesCleared <= 0) return 0;

            float multiplier = config.GetDifficultyMultiplier(ctx.difficulty);
            int base_ = Mathf.RoundToInt(ctx.wavesCleared * config.blueprintsPerWave * multiplier);

            if (ctx.bossKilled)
                base_ += config.bossKillBlueprintBonus;

            if (ctx.isPerfect)
                base_ += config.perfectClearBlueprintBonus;

            // 加一点随机性 ±20%
            float variance = Random.Range(0.8f, 1.2f);
            return Mathf.Max(1, Mathf.RoundToInt(base_ * variance));
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 装备掉落计算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static List<BattleDropItem> CalculateEquipmentDrops(BattleRewardConfig config, BattleContext ctx)
        {
            var result = new List<BattleDropItem>();
            var table = config.GetChapterDropTable(ctx.chapterIndex);

            if (table == null || table.entries == null || table.entries.Count == 0)
                return result;

            // 决定掉落件数：通关波次越多、难度越高，掉落越多
            int dropCount = CalculateDropCount(config, ctx);
            if (dropCount <= 0) return result;

            // 过滤掉需要Boss才能掉落但本局没打Boss的条目
            // 先暂时只在有Boss击杀时才保留 bossOnlyDrop 条目（通过修改权重为0）
            if (!ctx.bossKilled)
            {
                foreach (var entry in table.entries)
                    if (entry.bossOnlyDrop) entry.weight = 0f;
            }

            var rolled = table.RollDrops(dropCount);

            // 恢复被临时置0的权重（避免污染配置SO）
            // 注意：SO在运行时被修改会影响其他引用，
            // 这里直接重载一个"临时列表"而不是修改SO更安全
            // 见下方 RollDropsFiltered 方法

            // ─── 合并相同ID+品质的掉落 ───
            var merged = new Dictionary<string, BattleDropItem>();
            foreach (var entry in rolled)
            {
                string key = $"{entry.equipmentId}_{(int)entry.rarity}";
                if (merged.ContainsKey(key))
                    merged[key].count++;
                else
                    merged[key] = BattleDropItem.Equipment(entry.equipmentId, entry.rarity, 1);
            }

            result.AddRange(merged.Values);
            return result;
        }

        /// <summary>
        /// 计算本局掉落件数（0~maxEquipmentDropsPerBattle）
        /// </summary>
        private static int CalculateDropCount(BattleRewardConfig config, BattleContext ctx)
        {
            // 基础：每4波掉1件
            int count = ctx.wavesCleared / 4;

            // 难度加成
            if (ctx.difficulty >= 3) count++;
            if (ctx.difficulty >= 5) count++;

            // Boss奖励
            if (ctx.bossKilled) count++;

            return Mathf.Clamp(count, 0, config.maxEquipmentDropsPerBattle);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 安全版本：不修改SO，而是构建过滤列表
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 对 ChapterDropTable 进行 Boss 过滤后随机抽取（不污染 ScriptableObject）
        /// </summary>
        public static List<EquipmentDropEntry> RollDropsFiltered(
            ChapterDropTable table,
            int count,
            bool bossKilled)
        {
            var result = new List<EquipmentDropEntry>();
            if (table == null || table.entries == null || count <= 0) return result;

            // 构建过滤后的临时列表
            var filtered = new List<EquipmentDropEntry>();
            foreach (var e in table.entries)
            {
                if (e.bossOnlyDrop && !bossKilled) continue;
                if (e.weight > 0f) filtered.Add(e);
            }

            if (filtered.Count == 0) return result;

            float totalWeight = 0f;
            foreach (var e in filtered) totalWeight += e.weight;

            for (int i = 0; i < count; i++)
            {
                float roll = Random.Range(0f, totalWeight);
                float acc = 0f;
                foreach (var e in filtered)
                {
                    acc += e.weight;
                    if (roll <= acc)
                    {
                        result.Add(e);
                        break;
                    }
                }
            }
            return result;
        }
    }
}