// ============================================================
// CrateRewardConfig.cs
// 文件位置: Assets/Scripts/Data/SO/CrateRewardConfig.cs
// 用途：战术空投宝箱奖励配置
// ============================================================

using UnityEngine;
using System;
using System.Collections.Generic;

namespace LightVsDecay.Data.SO
{
    /// <summary>
    /// 宝箱类型枚举
    /// </summary>
    public enum CrateType
    {
        /// <summary>蓝色补给箱 - 生存与稳健</summary>
        Supply,
        
        /// <summary>金色问号箱 - 运气与贪婪</summary>
        Gacha,
        
        /// <summary>红色契约箱 - 代价与交易</summary>
        Deal
    }
    
    /// <summary>
    /// 奖励类型枚举
    /// </summary>
    public enum RewardType
    {
        // ═══ 补给类 ═══
        HealthRestore,      // 生命恢复（固定值）
        ShieldRestore,      // 护盾恢复（固定值）
        ShieldFull,         // 护盾全满
        
        // ═══ 属性提升类 ═══
        BaseDamagePercent,  // 基础伤害百分比
        CritRatePercent,    // 暴击率百分比
        LaserWidthFlat,     // 激光宽度（固定值）
        LaserLengthFlat,    // 激光长度（固定值）
        
        // ═══ 负面类 ═══
        HealthLoss,         // 生命损失
        ShieldLoss,         // 护盾损失
        BaseDamageLossPercent, // 基础伤害降低
        CritRateLossPercent,   // 暴击率降低
        LaserWidthLossFlat,    // 激光宽度降低
        LaserLengthLossFlat,   // 激光长度降低
        MaxHealthLoss,         // 最大生命降低
        
        // ═══ 特殊类 ═══
        Nothing,            // 无奖励（谢谢惠顾）
    }
    
    /// <summary>
    /// 单个奖励条目
    /// </summary>
    [Serializable]
    public class RewardEntry
    {
        [Tooltip("奖励类型")]
        public RewardType type;
        
        [Tooltip("数值（百分比类型填0.05表示5%，固定值类型填实际数值）")]
        public float value;
        
        [Tooltip("显示文本（如：HP +100）")]
        public string displayText;
        
        [Tooltip("权重（用于随机抽取）")]
        [Range(1, 100)]
        public int weight = 10;
        
        [Tooltip("文字颜色")]
        public Color textColor = Color.white;
        
        [Tooltip("是否为大奖（特殊动画效果）")]
        public bool isJackpot = false;
    }
    
    /// <summary>
    /// 契约箱交易条目（代价 + 收益）
    /// </summary>
    [Serializable]
    public class DealEntry
    {
        [Tooltip("交易名称")]
        public string dealName = "轻度契约";
        
        [Tooltip("代价")]
        public RewardEntry cost;
        
        [Tooltip("收益")]
        public RewardEntry gain;
        
        [Tooltip("权重")]
        [Range(1, 100)]
        public int weight = 10;
    }
    
    /// <summary>
    /// 金箱结果类型
    /// </summary>
    public enum GachaResultType
    {
        Nothing,    // 谢谢惠顾 (20%)
        Negative,   // 系统故障 (15%)
        Normal,     // 普通奖励 (50%)
        Epic        // 史诗大奖 (15%)
    }
    
    /// <summary>
    /// 金箱结果配置
    /// </summary>
    [Serializable]
    public class GachaResultConfig
    {
        [Tooltip("结果类型")]
        public GachaResultType resultType;
        
        [Tooltip("概率（0-100）")]
        [Range(0, 100)]
        public int probability = 25;
        
        [Tooltip("可能的奖励池")]
        public List<RewardEntry> rewardPool = new List<RewardEntry>();
        
        [Tooltip("嘲讽文案（仅用于 Nothing 类型）")]
        public List<string> mockTexts = new List<string>();
    }
    
    /// <summary>
    /// 宝箱奖励配置 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "CrateRewardConfig", menuName = "LightVsDecay/Crate Reward Config", order = 10)]
    public class CrateRewardConfig : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 通用设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("通用设置")]
        [Tooltip("宝箱基础血量")]
        public int crateHP = 500;
        
        [Tooltip("宝箱下落时间（秒）")]
        public float dropDuration = 0.8f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 蓝色补给箱配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("蓝色补给箱 (Supply)")]
        [Tooltip("奖励池")]
        public List<RewardEntry> supplyRewards = new List<RewardEntry>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 金色问号箱配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("金色问号箱 (Gacha)")]
        [Tooltip("各结果配置")]
        public List<GachaResultConfig> gachaResults = new List<GachaResultConfig>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 红色契约箱配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("红色契约箱 (Deal)")]
        [Tooltip("交易池")]
        public List<DealEntry> dealEntries = new List<DealEntry>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 保护机制
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("保护机制")]
        [Tooltip("金箱连续负收益次数触发保底")]
        public int gachaBadLuckProtectionCount = 2;
        
        [Tooltip("激光宽度下限（初始值的百分比）")]
        [Range(0.5f, 1f)]
        public float laserWidthMinPercent = 0.8f;
        
        [Tooltip("激光长度下限（初始值的百分比）")]
        [Range(0.5f, 1f)]
        public float laserLengthMinPercent = 0.8f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 颜色配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("颜色配置")]
        public Color positiveColor = new Color(0.3f, 1f, 0.3f);   // 绿色（获得）
        public Color negativeColor = new Color(1f, 0.3f, 0.3f);   // 红色（失去）
        public Color neutralColor = new Color(0.7f, 0.7f, 0.7f);  // 灰色（无奖励）
        public Color jackpotColor = new Color(1f, 0.84f, 0f);     // 金色（大奖）
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 便捷方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 从补给箱奖励池随机抽取
        /// </summary>
        public RewardEntry GetRandomSupplyReward()
        {
            return GetWeightedRandom(supplyRewards);
        }
        
        /// <summary>
        /// 获取金箱结果
        /// </summary>
        /// <param name="forceEpic">是否强制史诗（保底触发）</param>
        public (GachaResultType resultType, RewardEntry reward, string mockText) GetGachaResult(bool forceEpic = false)
        {
            GachaResultConfig resultConfig = null;
            
            if (forceEpic)
            {
                // 强制史诗大奖
                resultConfig = gachaResults.Find(r => r.resultType == GachaResultType.Epic);
            }
            else
            {
                // 按概率抽取结果类型
                int roll = UnityEngine.Random.Range(0, 100);
                int cumulative = 0;
                
                foreach (var config in gachaResults)
                {
                    cumulative += config.probability;
                    if (roll < cumulative)
                    {
                        resultConfig = config;
                        break;
                    }
                }
            }
            
            if (resultConfig == null)
            {
                // 保底返回普通
                resultConfig = gachaResults.Find(r => r.resultType == GachaResultType.Normal);
            }
            
            // 根据结果类型处理
            if (resultConfig.resultType == GachaResultType.Nothing)
            {
                string mockText = resultConfig.mockTexts.Count > 0 
                    ? resultConfig.mockTexts[UnityEngine.Random.Range(0, resultConfig.mockTexts.Count)]
                    : "下次一定";
                return (GachaResultType.Nothing, null, mockText);
            }
            
            RewardEntry reward = GetWeightedRandom(resultConfig.rewardPool);
            return (resultConfig.resultType, reward, null);
        }
        
        /// <summary>
        /// 获取契约箱交易
        /// </summary>
        public DealEntry GetRandomDeal()
        {
            if (dealEntries.Count == 0) return null;
            
            int totalWeight = 0;
            foreach (var entry in dealEntries)
            {
                totalWeight += entry.weight;
            }
            
            int roll = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            
            foreach (var entry in dealEntries)
            {
                cumulative += entry.weight;
                if (roll < cumulative)
                {
                    return entry;
                }
            }
            
            return dealEntries[0];
        }
        
        /// <summary>
        /// 权重随机抽取
        /// </summary>
        private RewardEntry GetWeightedRandom(List<RewardEntry> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            
            int totalWeight = 0;
            foreach (var entry in pool)
            {
                totalWeight += entry.weight;
            }
            
            int roll = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            
            foreach (var entry in pool)
            {
                cumulative += entry.weight;
                if (roll < cumulative)
                {
                    return entry;
                }
            }
            
            return pool[0];
        }
    }
}
