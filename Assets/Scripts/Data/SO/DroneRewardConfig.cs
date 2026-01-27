// ============================================================
// DroneRewardConfig.cs
// 文件位置: Assets/Scripts/Data/SO/DroneRewardConfig.cs
// 用途：无人机奖励系统完整配置（奖励池 + 图标 + 飘字）
// 更新：v2.1 - 新增金币奖励、下一波怪物增强效果
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
        BaseDamageFlat,     // 基础伤害固定值【新增】
        CritRatePercent,    // 暴击率百分比
        LaserWidthPercent,     // 激光宽度（固定值）
        LaserLengthPercent,    // 激光长度（固定值）
        
        // ═══ 金币类 ═══【新增】
        CoinReward,         // 金币奖励
        
        // ═══ 负面类（削弱玩家，永久生效）═══
        HealthLoss,             // 生命损失
        ShieldLoss,             // 护盾损失
        BaseDamageLossPercent,  // 基础伤害降低（百分比）
        CritRateLossPercent,    // 暴击率降低
        LaserWidthLossPercent,  // 激光宽度降低（百分比）【修改】
        LaserLengthLossPercent,    // 激光长度降低（固定值）
        MaxHealthLoss,          // 最大生命降低
        
        // ═══ 负面类（增强怪物，下一波生效）═══【新增】
        NextWaveSpeedBuff,      // 下一波怪物移速增强
        NextWaveHealthBuff,     // 下一波怪物血量增强
        NextWaveDamageBuff,     // 下一波怪物伤害增强
        
        // ═══ 特殊类 ═══
        Nothing,            // 无奖励（已废弃，改为金币）
    }
    
    /// <summary>
    /// 金箱结果类型
    /// </summary>
    public enum GachaResultType
    {
        Nothing,    // 小奖（金币）【修改：原"谢谢惠顾"改为金币奖励】
        Negative,   // 系统故障（负面效果）
        Normal,     // 普通奖励
        Epic        // 史诗大奖
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
        Coin,           // 金币【新增】
        MonsterBuff,    // 怪物增强【新增】
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
        
        [Tooltip("嘲讽文案（仅用于 Nothing 类型显示，现在会同时给金币）")]
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
        [Range(0.3f, 0.6f)]
        public float enterDuration = 0.45f;
        
        [Tooltip("无人机入场错帧间隔（秒）")]
        [Range(0.05f, 0.2f)]
        public float staggerDelay = 0.1f;
        
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
        
        [Tooltip("金币图标")]
        public Sprite coinIcon;
        
        [Tooltip("怪物增强图标")]
        public Sprite monsterBuffIcon;
        
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
        
        [Tooltip("金币文字颜色")]
        public Color coinTextColor = new Color(1f, 0.84f, 0f);   // 金色
        
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
        
        [Tooltip("金币飘字预制体")]
        public GameObject coinTextPrefab;
        
        [Tooltip("怪物增强飘字预制体")]
        public GameObject monsterBuffTextPrefab;
        
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
        /// <param name="isShieldBroken">护盾是否已破碎</param>
        public RewardEntry GetRandomSupplyReward(bool isShieldBroken = false)
        {
            // 构建过滤后的奖励池
            List<RewardEntry> filteredPool = new List<RewardEntry>();
    
            foreach (var entry in supplyRewards)
            {
                if (isShieldBroken)
                {
                    // 护盾破碎时：剔除 ShieldRestore（加护盾值），保留 ShieldFull（护盾重启）
                    if (entry.type == RewardType.ShieldRestore)
                    {
                        continue;
                    }
                }
                else
                {
                    // 护盾未破碎时：剔除 ShieldFull（护盾重启），保留 ShieldRestore
                    if (entry.type == RewardType.ShieldFull)
                    {
                        continue;
                    }
                }
        
                filteredPool.Add(entry);
            }
    
            if (filteredPool.Count == 0)
            {
                Debug.LogWarning("[DroneRewardConfig] 补给箱过滤后奖励池为空！");
                return null;
            }
    
            return GetWeightedRandom(filteredPool);
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
                // 【v2.1】Nothing 现在会从奖励池抽取金币奖励
                string mockText = resultConfig.mockTexts.Count > 0 
                    ? resultConfig.mockTexts[UnityEngine.Random.Range(0, resultConfig.mockTexts.Count)]
                    : "小赚一笔";
                
                // 如果 Nothing 池有配置，抽取金币奖励
                RewardEntry coinReward = null;
                if (resultConfig.rewardPool.Count > 0)
                {
                    coinReward = GetWeightedRandom(resultConfig.rewardPool);
                }
                
                return (GachaResultType.Nothing, coinReward, mockText);
            }
            
            RewardEntry reward = GetWeightedRandom(resultConfig.rewardPool);
            return (resultConfig.resultType, reward, null);
        }
        
        /// <summary>
        /// 获取契约箱交易
        /// </summary>
        /// <param name="isShieldBroken">护盾是否已破碎</param>
        public DealEntry GetRandomDeal(bool isShieldBroken = false)
        {
            if (dealEntries.Count == 0) return null;
    
            // 构建过滤后的交易池
            List<DealEntry> filteredPool = new List<DealEntry>();
    
            foreach (var entry in dealEntries)
            {
                // 护盾破碎时，剔除代价为扣护盾的交易
                if (isShieldBroken && entry.cost != null && entry.cost.type == RewardType.ShieldLoss)
                {
                    continue;
                }
        
                filteredPool.Add(entry);
            }
    
            if (filteredPool.Count == 0)
            {
                Debug.LogWarning("[DroneRewardConfig] 契约箱过滤后交易池为空！");
                return null;
            }
    
            int totalWeight = 0;
            foreach (var entry in filteredPool)
            {
                totalWeight += entry.weight;
            }
    
            int roll = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
    
            // 调试日志
            Debug.Log($"[DroneRewardConfig] 契约抽取: 池大小={filteredPool.Count}, 总权重={totalWeight}, 随机值={roll}, 护盾破碎={isShieldBroken}");
    
            int index = 0;
            foreach (var entry in filteredPool)
            {
                cumulative += entry.weight;
                if (roll < cumulative)
                {
                    Debug.Log($"[DroneRewardConfig] 抽中第 {index} 个: {entry.dealName}, 代价={entry.cost?.displayText}, 收益={entry.gain?.displayText}");
                    return entry;
                }
                index++;
            }
    
            return filteredPool[0];
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
                case RewardIconType.Coin:
                    return coinIcon;
                case RewardIconType.MonsterBuff:
                    return monsterBuffIcon;
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
                case RewardType.BaseDamageFlat:
                    return RewardIconType.AttackUp;
                case RewardType.CritRatePercent:
                    return RewardIconType.CritUp;
                case RewardType.LaserWidthPercent:
                case RewardType.LaserLengthPercent:
                    return RewardIconType.LaserUp;
                    
                // 金币
                case RewardType.CoinReward:
                    return RewardIconType.Coin;
                    
                // 负面效果（削弱玩家）
                case RewardType.HealthLoss:
                case RewardType.MaxHealthLoss:
                    return RewardIconType.HealthDown;
                case RewardType.ShieldLoss:
                    return RewardIconType.ShieldDown;
                case RewardType.BaseDamageLossPercent:
                    return RewardIconType.AttackDown;
                case RewardType.CritRateLossPercent:
                    return RewardIconType.CritDown;
                case RewardType.LaserWidthLossPercent:
                case RewardType.LaserLengthLossPercent:
                    return RewardIconType.LaserDown;
                    
                // 负面效果（增强怪物）
                case RewardType.NextWaveSpeedBuff:
                case RewardType.NextWaveHealthBuff:
                case RewardType.NextWaveDamageBuff:
                    return RewardIconType.MonsterBuff;
                    
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
                case RewardType.BaseDamageFlat:
                case RewardType.CritRatePercent:
                case RewardType.LaserWidthPercent:
                case RewardType.LaserLengthPercent:
                case RewardType.CoinReward:
                    return true;
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// 判断是否为怪物增强效果
        /// </summary>
        public bool IsMonsterBuffReward(RewardType rewardType)
        {
            switch (rewardType)
            {
                case RewardType.NextWaveSpeedBuff:
                case RewardType.NextWaveHealthBuff:
                case RewardType.NextWaveDamageBuff:
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
            
            if (rewardType == RewardType.CoinReward)
            {
                return coinTextColor;
            }
            
            return IsPositiveReward(rewardType) ? positiveColor : negativeColor;
        }
    }
}