// ============================================================
// LaserDamageCalculator.cs
// 文件位置: Assets/Scripts/Logic/Player/Laser/LaserDamageCalculator.cs
// 用途：激光伤害计算系统 - 从 LaserController 拆分
// ============================================================

using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 激光伤害计算器
    /// 负责：DPS计算、伤害倍率管理、Tick伤害计算
    /// </summary>
    [System.Serializable]
    public class LaserDamageCalculator
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 基础配置（从 GameSettings 读取）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float baseDPS = 100f;
        private float tickRate = 0.1f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能加成
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float skillDamageMultiplier = 1f;
        private float flatDamageBonus = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 大招加成（由 OverloadManager 控制）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private bool isOverloadActive = false;
        private float overloadDamageMultiplier = 1f;
        
        private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>每 Tick 伤害</summary>
        public float CurrentDamagePerTick => (baseDPS + flatDamageBonus) * tickRate * skillDamageMultiplier * overloadDamageMultiplier;
        
        /// <summary>当前面板 DPS（用于爆炸伤害等计算）</summary>
        public float CurrentPanelDPS => (baseDPS + flatDamageBonus) * skillDamageMultiplier * overloadDamageMultiplier;
        
        /// <summary>基础 DPS</summary>
        public float BaseDPS => baseDPS;
        
        /// <summary>Tick 间隔</summary>
        public float TickRate => tickRate;
        
        /// <summary>技能伤害倍率</summary>
        public float SkillDamageMultiplier => skillDamageMultiplier;
        
        /// <summary>固定伤害加成</summary>
        public float FlatDamageBonus => flatDamageBonus;
        
        /// <summary>大招是否激活</summary>
        public bool IsOverloadActive => isOverloadActive;
        
        /// <summary>大招伤害倍率</summary>
        public float OverloadDamageMultiplier => overloadDamageMultiplier;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 从 GameSettings 初始化
        /// </summary>
        public void Initialize(float dps, float tick, bool debug = false)
        {
            baseDPS = dps;
            tickRate = tick;
            showDebugInfo = debug;
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[LaserDamageCalculator] 初始化: DPS={dps}, TickRate={tick}s");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害计算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 计算副激光伤害
        /// </summary>
        public float CalculateSubLaserDamage(float damageMultiplier)
        {
            return CurrentDamagePerTick * damageMultiplier;
        }
        
        /// <summary>
        /// 计算穿透衰减伤害
        /// </summary>
        /// <param name="baseDamage">基础伤害</param>
        /// <param name="penetrationIndex">穿透索引（0=第一个目标）</param>
        /// <param name="decayRate">每次穿透的衰减率</param>
        public float CalculatePenetrationDamage(float baseDamage, int penetrationIndex, float decayRate)
        {
            if (penetrationIndex <= 0) return baseDamage;
            
            float decay = Mathf.Pow(1f - decayRate, penetrationIndex);
            return baseDamage * decay;
        }
        
        /// <summary>
        /// 计算 Boss 伤害（含易伤加成）
        /// </summary>
        public float CalculateBossDamage(float baseDamage, float bossBonus)
        {
            if (bossBonus > 0f)
            {
                return baseDamage * (1f + bossBonus);
            }
            return baseDamage;
        }
        
        /// <summary>
        /// 计算碎冰伤害
        /// </summary>
        public float CalculateShatterDamage(float baseDamage, float shatterMultiplier, bool isShatter)
        {
            if (isShatter && shatterMultiplier > 1f)
            {
                return baseDamage * shatterMultiplier;
            }
            return baseDamage;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 倍率管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置伤害倍率
        /// </summary>
        public void SetDamageMultiplier(float multiplier)
        {
            skillDamageMultiplier = Mathf.Max(0.1f, multiplier);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[LaserDamageCalculator] 伤害倍率设置: {multiplier:P0}");
            }
        }
        
        /// <summary>
        /// 增加伤害百分比
        /// </summary>
        public void AddDamagePercent(float percent)
        {
            skillDamageMultiplier += percent;
        }
        
        /// <summary>
        /// 添加固定值攻击力
        /// </summary>
        public void AddDamageFlat(float value)
        {
            flatDamageBonus += value;
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[LaserDamageCalculator] 固定攻击力 +{value}, 总加成: {flatDamageBonus}, 当前DPS: {CurrentPanelDPS:F0}");
            }
        }
        
        /// <summary>
        /// 设置大招状态
        /// </summary>
        public void SetOverloadActive(bool active, float damageMultiplier)
        {
            isOverloadActive = active;
            overloadDamageMultiplier = active ? damageMultiplier : 1f;
            
            if (showDebugInfo)
            {
                if (active)
                {
                    GameLogger.Log($"[LaserDamageCalculator] ⚡ 大招激活！伤害×{damageMultiplier}");
                }
                else
                {
                    GameLogger.Log("[LaserDamageCalculator] 大招结束");
                }
            }
        }
        
        /// <summary>
        /// 重置空投加成
        /// </summary>
        public void ResetDropBonuses()
        {
            flatDamageBonus = 0f;
            
            if (showDebugInfo)
            {
                GameLogger.Log("[LaserDamageCalculator] 空投加成已重置");
            }
        }
        
        /// <summary>
        /// 重置所有加成
        /// </summary>
        public void ResetAll()
        {
            skillDamageMultiplier = 1f;
            flatDamageBonus = 0f;
            isOverloadActive = false;
            overloadDamageMultiplier = 1f;
        }
        
        /// <summary>
        /// 设置调试模式
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            showDebugInfo = enabled;
        }
    }
}
