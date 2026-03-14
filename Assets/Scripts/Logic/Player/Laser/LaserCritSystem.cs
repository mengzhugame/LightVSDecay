// ============================================================
// LaserCritSystem.cs
// 文件位置: Assets/Scripts/Logic/Player/Laser/LaserCritSystem.cs
// 用途：激光暴击系统
// 修改：新增暴击伤害加成、暴击率上限60%、Lv5暴击击退
// ============================================================

using UnityEngine;

namespace LightVsDecay.Logic.Player
{
    [System.Serializable]
    public class LaserCritSystem
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 常量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>全局暴击率上限（防止高频激光全暴击失去惊喜感）</summary>
        private const float MAX_CRIT_RATE = 0.6f;

        /// <summary>Lv5 暴击附加击退倍率（在基础击退上额外乘以此值）</summary>
        private const float CRIT_KNOCKBACK_MULTIPLIER = 1.5f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置字段
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>基础暴击率（来自 GameSettings）</summary>
        private float baseCritRate = 0.1f;

        /// <summary>基础暴击倍率（来自 GameSettings，如 2.0 = 200%）</summary>
        private float baseCritDamageMultiplier = 2.0f;

        /// <summary>技能提供的暴击率加成（累计值）</summary>
        private float critRateBonus = 0f;

        /// <summary>技能提供的暴击伤害加成（累加值，如 0.3 = +30%）</summary>
        private float critDamageBonus = 0f;

        /// <summary>当前 Crit 技能等级</summary>
        private int critLevel = 0;

        /// <summary>Lv5 是否启用暴击附带击退</summary>
        private bool critKnockbackEnabled = false;

        private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 当前总暴击率（硬上限 60%，防止激光高频全暴击）
        /// </summary>
        public float CurrentCritRate => Mathf.Min(baseCritRate + critRateBonus, MAX_CRIT_RATE);

        /// <summary>
        /// 当前总暴击伤害倍率 = 基础倍率 + 技能暴击伤害加成
        /// 例：基础 2.0，技能 +0.3 → 总计 2.3x
        /// </summary>
        public float TotalCritMultiplier => baseCritDamageMultiplier + critDamageBonus;

        /// <summary>当前 Crit 技能等级</summary>
        public int CritLevel => critLevel;

        /// <summary>暴击率上限（供 UI 面板展示）</summary>
        public float MaxCritRate => MAX_CRIT_RATE;

        /// <summary>Lv5：暴击时是否附带微弱击退</summary>
        public bool IsCritKnockbackEnabled => critKnockbackEnabled;

        /// <summary>暴击附加击退倍率</summary>
        public float CritKnockbackMultiplier => critKnockbackEnabled ? CRIT_KNOCKBACK_MULTIPLIER : 1f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 从 GameSettings 初始化基础参数
        /// </summary>
        public void Initialize(float baseRate, float damageMultiplier, bool debug = false)
        {
            baseCritRate = baseRate;
            baseCritDamageMultiplier = damageMultiplier;
            showDebugInfo = debug;

            if (showDebugInfo)
            {
                Debug.Log($"[LaserCritSystem] 初始化: 基础暴击率={baseRate:P0}, " +
                          $"基础暴击倍率={damageMultiplier:F2}x, 上限={MAX_CRIT_RATE:P0}");
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
        /// 计算暴击伤害（使用总暴击倍率）
        /// </summary>
        public float CalculateCritDamage(float baseDamage, bool isCrit)
        {
            return isCrit ? baseDamage * TotalCritMultiplier : baseDamage;
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
        /// 增加暴击率加成（外部调用，如科技树）
        /// </summary>
        public void AddCritRateBonus(float bonus)
        {
            critRateBonus += bonus;
            if (showDebugInfo)
            {
                Debug.Log($"[LaserCritSystem] 暴击率加成 +{bonus:P0}, " +
                          $"当前={CurrentCritRate:P0}（上限{MAX_CRIT_RATE:P0}）");
            }
        }

        /// <summary>
        /// 重置暴击率加成（战斗结束时清理）
        /// </summary>
        public void ResetCritRateBonus()
        {
            critRateBonus = 0f;
        }

        /// <summary>
        /// 从技能配置设置 Crit 等级（由 SkillEffectManager 调用）
        /// </summary>
        /// <param name="level">技能等级</param>
        /// <param name="rateBonus">暴击率加成（如 0.03 = +3%）</param>
        /// <param name="damageBonus">暴击伤害加成（如 0.15 = +15%）</param>
        /// <param name="enableKnockback">Lv5 暴击击退开关</param>
        public void SetCritLevelFromConfig(int level, float rateBonus, float damageBonus, bool enableKnockback)
        {
            critLevel = level;
            critRateBonus = rateBonus;
            critDamageBonus = damageBonus;
            critKnockbackEnabled = enableKnockback;

            if (showDebugInfo)
            {
                Debug.Log($"[LaserCritSystem] Crit Lv.{level}: " +
                          $"暴击率加成={rateBonus:P0} → 总={CurrentCritRate:P0}（上限{MAX_CRIT_RATE:P0}）, " +
                          $"暴击倍率={TotalCritMultiplier:F2}x, 击退={enableKnockback}");
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