// ============================================================
// GameSettings.cs
// 文件位置: Assets/Scripts/Data/SO/GameSettings.cs
// 用途：游戏全局配置（ScriptableObject）
// 从 ProgressManager 拆分出的配置数据
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
        // 游戏时间设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("游戏时间")]
        [Tooltip("单局总时长（秒）")]
        public float gameDuration = 300f; // 5分钟
        
        [Tooltip("BOSS战限时（秒）")]
        public float bossBattleTimeLimit = 60f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 经验系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("经验系统")]
        [Tooltip("升级公式基础值: XP = Base + (Level * Growth)")]
        public int expBase = 5;
        
        [Tooltip("升级公式增量系数")]
        public int expGrowth = 5;
        
        [Tooltip("最大等级")]
        public int maxLevel = 20;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 大招系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("大招系统")]
        [Tooltip("大招最大能量")]
        public int ultMaxEnergy = 100;
        
        [Tooltip("每次击杀获得的大招能量（小怪）")]
        public int ultEnergyPerKill = 2;
        
        [Tooltip("精英怪击杀获得的大招能量（Tank）")]
        public int ultEnergyPerEliteKill = 5;
        
        [Tooltip("大招持续时间（秒）")]
        public float ultDuration = 5f;
        
        [Tooltip("大招激光宽度倍率")]
        public float ultLaserWidthMultiplier = 2f;
        
        [Tooltip("大招旋转速度（度/秒）")]
        public float ultRotationSpeed = 360f;
        
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
        [Tooltip("基础DPS")]
        public float baseDPS = 100f;
        
        [Tooltip("判定频率（秒）")]
        public float tickRate = 0.1f;
        
        [Tooltip("基础击退力")]
        public float baseKnockbackForce = 10f;
        
        [Tooltip("激光最大长度")]
        public float maxLaserLength = 15f;
        
        [Tooltip("激光基础宽度")]
        public float baseLaserWidth = 0.5f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 护盾系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("护盾系统")]
        [Tooltip("护盾最大值")]
        public int maxShieldHP = 3;
        
        [Tooltip("护盾恢复时间（秒）")]
        public float shieldRecoveryTime = 12f;
        
        [Tooltip("本体最大生命值")]
        public int maxHullHP = 3;
        
        [Tooltip("无敌帧时长（秒）")]
        public float invincibilityDuration = 1.0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 便捷方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 计算指定等级升级所需经验
        /// </summary>
        public int CalculateExpToNextLevel(int level)
        {
            return expBase + (level * expGrowth);
        }
        
        /// <summary>
        /// 计算单次伤害（基于DPS和判定频率）
        /// </summary>
        public float DamagePerTick => baseDPS * tickRate;
    }
}