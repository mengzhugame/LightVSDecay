// ============================================================
// DroneRewardConfig.cs
// 文件位置: Assets/Scripts/Data/SO/DroneRewardConfig.cs
// 用途：无人机奖励系统完整配置（奖励池 + 图标 + 飘字）
// 合并自：CrateRewardConfig.cs + DroneRewardConfig.cs
// ============================================================

using UnityEngine;
using System;
using System.Collections.Generic;

namespace LightVsDecay.Data.SO
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 枚举定义
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 无人机类型枚举
    /// </summary>
    public enum CrateType
    {
        /// <summary>蓝色补给无人机 - 生存与稳健</summary>
        Supply,
        
        /// <summary>金色问号无人机 - 运气与贪婪</summary>
        Gacha,
        
        /// <summary>红色契约无人机 - 代价与交易</summary>
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
    /// 奖励图标类型
    /// </summary>
    public enum RewardIconType
    {
        HealthUp,       // 血量增加
        HealthDown,     // 血量减少
        ShieldUp,       // 护盾增加
        ShieldDown,     // 护盾减少
        AttackUp,       // 攻击力增加
        AttackDown,     // 攻击力减少
        CritUp,         // 暴击率增加
        CritDown,       // 暴击率减少
        LaserUp,        // 激光属性增加（宽度/长度）
        LaserDown,      // 激光属性减少
        Nothing         // 空气（无事发生）
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 数据类定义
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
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
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 主配置类
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 无人机奖励系统完整配置
    /// </summary>
    [CreateAssetMenu(fileName = "DroneRewardConfig", menuName = "LightVsDecay/Drone Reward Config", order = 10)]
    public class DroneRewardConfig : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 通用设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 通用设置 ═══")]
        [Tooltip("无人机基础血量")]
        public int crateHP = 500;
        
        [Tooltip("无人机入场动画时间（秒）")]
        public float enterDuration = 0.8f;
        
        [Tooltip("无人机离场动画时间（秒）")]
        public float exitDuration = 0.5f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 蓝色补给无人机配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 蓝色补给无人机 (Supply) ═══")]
        [Tooltip("奖励池")]
        public List<RewardEntry> supplyRewards = new List<RewardEntry>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 金色问号无人机配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 金色问号无人机 (Gacha) ═══")]
        [Tooltip("各结果配置")]
        public List<GachaResultConfig> gachaResults = new List<GachaResultConfig>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 红色契约无人机配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 红色契约无人机 (Deal) ═══")]
        [Tooltip("交易池")]
        public List<DealEntry> dealEntries = new List<DealEntry>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 保护机制
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 保护机制 ═══")]
        [Tooltip("金箱连续负收益次数触发保底")]
        public int gachaBadLuckProtectionCount = 2;
        
        [Tooltip("激光宽度下限（初始值的百分比）")]
        [Range(0.5f, 1f)]
        public float laserWidthMinPercent = 0.8f;
        
        [Tooltip("激光长度下限（初始值的百分比）")]
        [Range(0.5f, 1f)]
        public float laserLengthMinPercent = 0.8f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 图标配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 图标配置 ═══")]
        [Tooltip("血量增加图标")]
        public Sprite healthUpIcon;
        
        [Tooltip("血量减少图标")]
        public Sprite healthDownIcon;
        
        [Tooltip("护盾增加图标")]
        public Sprite shieldUpIcon;
        
        [Tooltip("护盾减少图标")]
        public Sprite shieldDownIcon;
        
        [Tooltip("攻击力增加图标")]
        public Sprite attackUpIcon;
        
        [Tooltip("攻击力减少图标")]
        public Sprite attackDownIcon;
        
        [Tooltip("暴击率增加图标")]
        public Sprite critUpIcon;
        
        [Tooltip("暴击率减少图标")]
        public Sprite critDownIcon;
        
        [Tooltip("激光属性增加图标")]
        public Sprite laserUpIcon;
        
        [Tooltip("激光属性减少图标")]
        public Sprite laserDownIcon;
        
        [Tooltip("空气图标（无事发生）")]
        public Sprite nothingIcon;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 颜色配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 颜色配置 ═══")]
        [Tooltip("正面效果颜色（增加）")]
        public Color positiveColor = new Color(0.3f, 1f, 0.4f);  // 绿色
        
        [Tooltip("负面效果颜色（减少）")]
        public Color negativeColor = new Color(1f, 0.3f, 0.3f);  // 红色
        
        [Tooltip("中性效果颜色（空气）")]
        public Color neutralColor = new Color(0.6f, 0.6f, 0.6f); // 灰色
        
        [Tooltip("史诗效果颜色（大奖）")]
        public Color epicColor = new Color(1f, 0.84f, 0f);       // 金色
        
        [Tooltip("补给无人机文字颜色")]
        public Color supplyTextColor = new Color(0.4f, 0.9f, 1f); // 蓝绿色
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 飘字 Prefab 引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 飘字 Prefab ═══")]
        [Tooltip("补给无人机飘字预制体（单行）")]
        public GameObject supplyTextPrefab;
        
        [Tooltip("问号无人机飘字预制体（单行）")]
        public GameObject gachaTextPrefab;
        
        [Tooltip("契约无人机飘字预制体（双行）")]
        public GameObject dealTextPrefab;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 飘字对象池设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 飘字对象池 ═══")]
        [Tooltip("预热数量")]
        public int textPrewarmCount = 3;
        
        [Tooltip("最大数量")]
        public int textMaxPoolSize = 10;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 奖励池方法
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
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 图标方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 根据图标类型获取 Sprite
        /// </summary>
        public Sprite GetIcon(RewardIconType iconType)
        {
            switch (iconType)
            {
                case RewardIconType.HealthUp:
                    return healthUpIcon;
                case RewardIconType.HealthDown:
                    return healthDownIcon;
                case RewardIconType.ShieldUp:
                    return shieldUpIcon;
                case RewardIconType.ShieldDown:
                    return shieldDownIcon;
                case RewardIconType.AttackUp:
                    return attackUpIcon;
                case RewardIconType.AttackDown:
                    return attackDownIcon;
                case RewardIconType.CritUp:
                    return critUpIcon;
                case RewardIconType.CritDown:
                    return critDownIcon;
                case RewardIconType.LaserUp:
                    return laserUpIcon;
                case RewardIconType.LaserDown:
                    return laserDownIcon;
                case RewardIconType.Nothing:
                default:
                    return nothingIcon;
            }
        }
        
        /// <summary>
        /// 根据 RewardType 获取对应的图标类型
        /// </summary>
        public RewardIconType GetIconTypeFromRewardType(RewardType rewardType)
        {
            switch (rewardType)
            {
                // 正面效果
                case RewardType.HealthRestore:
                    return RewardIconType.HealthUp;
                case RewardType.ShieldRestore:
                case RewardType.ShieldFull:
                    return RewardIconType.ShieldUp;
                case RewardType.BaseDamagePercent:
                    return RewardIconType.AttackUp;
                case RewardType.CritRatePercent:
                    return RewardIconType.CritUp;
                case RewardType.LaserWidthFlat:
                case RewardType.LaserLengthFlat:
                    return RewardIconType.LaserUp;
                    
                // 负面效果
                case RewardType.HealthLoss:
                case RewardType.MaxHealthLoss:
                    return RewardIconType.HealthDown;
                case RewardType.ShieldLoss:
                    return RewardIconType.ShieldDown;
                case RewardType.BaseDamageLossPercent:
                    return RewardIconType.AttackDown;
                case RewardType.CritRateLossPercent:
                    return RewardIconType.CritDown;
                case RewardType.LaserWidthLossFlat:
                case RewardType.LaserLengthLossFlat:
                    return RewardIconType.LaserDown;
                    
                // 空
                case RewardType.Nothing:
                default:
                    return RewardIconType.Nothing;
            }
        }
        
        /// <summary>
        /// 判断是否为正面效果
        /// </summary>
        public bool IsPositiveReward(RewardType rewardType)
        {
            switch (rewardType)
            {
                case RewardType.HealthRestore:
                case RewardType.ShieldRestore:
                case RewardType.ShieldFull:
                case RewardType.BaseDamagePercent:
                case RewardType.CritRatePercent:
                case RewardType.LaserWidthFlat:
                case RewardType.LaserLengthFlat:
                    return true;
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// 获取奖励对应的文字颜色
        /// </summary>
        public Color GetRewardColor(RewardType rewardType, bool isEpic = false)
        {
            if (isEpic)
            {
                return epicColor;
            }
            
            if (rewardType == RewardType.Nothing)
            {
                return neutralColor;
            }
            
            return IsPositiveReward(rewardType) ? positiveColor : negativeColor;
        }
    }
}