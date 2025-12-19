// ============================================================
// GameSettings.cs
// 文件位置: Assets/Scripts/Data/SO/GameSettings.cs
// 用途：游戏全局配置（ScriptableObject）
// 更新：根据数值框架文档 v1.0 重构
// ============================================================

using UnityEngine;

namespace LightVsDecay.Data.SO
{
    /// <summary>
    /// 游戏全局设置 (ScriptableObject)
    /// 统一管理所有游戏配置参数
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "LightVsDecay/Game Settings", order = 0)]
    public class GameSettings : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 经验系统
        // 公式: UpgradeCost = expBase + (CurrentLevel - 1) * expGrowth
        // 例: Lv1→2 = 100, Lv2→3 = 150, ..., Lv19→20 = 1000
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("经验系统")]
        [Tooltip("升级公式基础值: XP = Base + (Level-1) * Growth")]
        public int expBase = 100;
        
        [Tooltip("升级公式增量系数")]
        public int expGrowth = 50;
        
        [Tooltip("最大等级")]
        public int maxLevel = 20;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 连击系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("连击系统")]
        [Tooltip("连击超时时间（秒）")]
        public float comboTimeout = 2.0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 能量系统（局外）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("能量系统（局外）")]
        [Tooltip("最大能量值")]
        public int maxEnergy = 5;
        
        [Tooltip("能量恢复间隔（秒）")]
        public float energyRecoveryInterval = 600f; // 10分钟
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 激光系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("激光系统")]
        [Tooltip("基础DPS（每秒伤害）")]
        public float baseDPS = 100f;
        
        [Tooltip("判定频率（秒）- 每0.1秒造成10点伤害")]
        public float tickRate = 0.1f;
        
        [Tooltip("基础击退力")]
        public float baseKnockbackForce = 10f;
        
        [Tooltip("激光最大长度（3/4屏）")]
        public float maxLaserLength = 15f;
        
        [Tooltip("激光基础宽度")]
        public float baseLaserWidth = 0.5f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 暴击系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("暴击系统")]
        [Tooltip("基础暴击率（0-1）")]
        [Range(0f, 1f)]
        public float baseCritRate = 0.1f;  // 10%
        
        [Tooltip("暴击伤害倍率")]
        public float critDamageMultiplier = 2.0f;  // 200%

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 护盾系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("护盾系统")]
        [Tooltip("护盾最大值")]
        public int maxShieldHP = 500;
        
        [Tooltip("本体最大生命值")]
        public int maxHullHP = 1000;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 便捷方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 计算指定等级升级所需经验
        /// 公式: UpgradeCost = expBase + (level - 1) * expGrowth
        /// </summary>
        public int CalculateExpToNextLevel(int level)
        {
            // Lv1→2: 100 + 0*50 = 100
            // Lv2→3: 100 + 1*50 = 150
            // Lv19→20: 100 + 18*50 = 1000
            return expBase + (level - 1) * expGrowth;
        }
        
        /// <summary>
        /// 计算单次伤害（基于DPS和判定频率）
        /// </summary>
        public float DamagePerTick => baseDPS * tickRate;
        
        /// <summary>
        /// 计算从1级升到指定等级所需的累计经验
        /// </summary>
        public int CalculateTotalExpForLevel(int targetLevel)
        {
            int total = 0;
            for (int lv = 1; lv < targetLevel; lv++)
            {
                total += CalculateExpToNextLevel(lv);
            }
            return total;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器验证
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [ContextMenu("打印经验表")]
        public void PrintExpTable()
        {
            Debug.Log("=== 经验表 ===");
            int cumulative = 0;
            for (int lv = 1; lv < maxLevel; lv++)
            {
                int need = CalculateExpToNextLevel(lv);
                cumulative += need;
                Debug.Log($"Lv.{lv} → {lv + 1}: {need} XP (累计: {cumulative})");
            }
        }
        
        [ContextMenu("验证数值")]
        public void ValidateSettings()
        {
            Debug.Log("=== GameSettings 验证 ===");
            Debug.Log($"DPS: {baseDPS}, Tick: {tickRate}s, 单次伤害: {DamagePerTick}");
            Debug.Log($"激光: 长度={maxLaserLength}, 宽度={baseLaserWidth}");
            Debug.Log($"暴击: {baseCritRate:P0} / {critDamageMultiplier:P0}");
            Debug.Log($"护盾: {maxShieldHP}, 本体: {maxHullHP}");
            Debug.Log($"Lv1→20 累计经验: {CalculateTotalExpForLevel(maxLevel)}");
        }
#endif
    }
}