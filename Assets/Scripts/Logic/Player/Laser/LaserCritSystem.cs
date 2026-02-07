// ============================================================
// LaserCritSystem.cs
// 文件位置: Assets/Scripts/Logic/Player/Laser/LaserCritSystem.cs
// 用途：激光暴击系统 - 从 LaserController 拆分
// ============================================================

using UnityEngine;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 激光暴击系统
    /// 负责：暴击率计算、暴击判定、暴击加成管理
    /// </summary>
    [System.Serializable]
    public class LaserCritSystem
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float baseCritRate = 0.1f;
        private float critDamageMultiplier = 2.0f;
        private float critRateBonus = 0f;
        private int critLevel = 0;
        
        private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前暴击率</summary>
        public float CurrentCritRate => Mathf.Clamp01(baseCritRate + critRateBonus);
        
        /// <summary>暴击倍率</summary>
        public float CritMultiplier => critDamageMultiplier;
        
        /// <summary>暴击等级</summary>
        public int CritLevel => critLevel;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 从 GameSettings 初始化
        /// </summary>
        public void Initialize(float baseRate, float damageMultiplier, bool debug = false)
        {
            baseCritRate = baseRate;
            critDamageMultiplier = damageMultiplier;
            showDebugInfo = debug;
            
            if (showDebugInfo)
            {
                Debug.Log($"[LaserCritSystem] 初始化: 基础暴击率={baseRate:P0}, 暴击倍率={damageMultiplier:P0}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 暴击判定
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 判定是否暴击
        /// </summary>
        public bool RollCrit()
        {
            return Random.value < CurrentCritRate;
        }
        
        /// <summary>
        /// 计算暴击后的伤害
        /// </summary>
        public float CalculateCritDamage(float baseDamage, bool isCrit)
        {
            return isCrit ? baseDamage * critDamageMultiplier : baseDamage;
        }
        
        /// <summary>
        /// 判定并计算暴击伤害（一步完成）
        /// </summary>
        public float RollAndCalculateDamage(float baseDamage, out bool isCrit)
        {
            isCrit = RollCrit();
            return CalculateCritDamage(baseDamage, isCrit);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 加成管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 增加暴击率加成
        /// </summary>
        public void AddCritRateBonus(float bonus)
        {
            critRateBonus += bonus;
            if (showDebugInfo)
            {
                Debug.Log($"[LaserCritSystem] 暴击率加成 +{bonus:P0}, 当前暴击率: {CurrentCritRate:P0}");
            }
        }
        
        /// <summary>
        /// 重置暴击率加成
        /// </summary>
        public void ResetCritRateBonus()
        {
            critRateBonus = 0f;
        }
        
        /// <summary>
        /// 从配置设置 Crit 等级
        /// </summary>
        public void SetCritLevelFromConfig(int level, float critBonus)
        {
            critLevel = level;
            critRateBonus = critBonus;
            
            if (showDebugInfo)
            {
                Debug.Log($"[LaserCritSystem] Crit Lv.{level}: 暴击率加成={critBonus:P0}, 总暴击率={CurrentCritRate:P0}");
            }
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
