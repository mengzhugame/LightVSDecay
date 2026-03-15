// ============================================================
// LaserKnockbackHandler.cs
// 文件位置: Assets/Scripts/Logic/Player/Laser/LaserKnockbackHandler.cs
// 用途：激光击退系统 - 从 LaserController 拆分
// ============================================================

using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 激光击退处理器
    /// 负责：击退力度计算、击退方向、击退效果应用
    /// </summary>
    [System.Serializable]
    public class LaserKnockbackHandler
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float baseKnockbackForce = 10f;
        private float skillKnockbackMultiplier = 1f;
        
        private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前击退力</summary>
        public float CurrentKnockbackForce => baseKnockbackForce * skillKnockbackMultiplier;
        
        /// <summary>基础击退力</summary>
        public float BaseKnockbackForce => baseKnockbackForce;
        
        /// <summary>击退倍率</summary>
        public float KnockbackMultiplier => skillKnockbackMultiplier;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 从 GameSettings 初始化
        /// </summary>
        public void Initialize(float baseForce, bool debug = false)
        {
            baseKnockbackForce = baseForce;
            showDebugInfo = debug;
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[LaserKnockbackHandler] 初始化: 基础击退力={baseForce}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 击退计算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 计算击退向量
        /// </summary>
        /// <param name="direction">击退方向（通常是激光方向）</param>
        /// <param name="multiplier">额外倍率（如副激光倍率）</param>
        /// <returns>最终击退向量</returns>
        public Vector2 CalculateKnockback(Vector2 direction, float multiplier = 1f)
        {
            float magnitude = CurrentKnockbackForce * multiplier;
            return direction.normalized * magnitude;
        }
        
        /// <summary>
        /// 计算穿透目标的击退（衰减版）
        /// </summary>
        /// <param name="direction">击退方向</param>
        /// <param name="penetrationIndex">穿透索引（0=第一个目标）</param>
        /// <param name="decayRate">每次穿透的衰减率</param>
        public Vector2 CalculatePenetrationKnockback(Vector2 direction, int penetrationIndex, float decayRate = 0.5f)
        {
            float magnitude = CurrentKnockbackForce;
            
            // 穿透目标击退减半
            if (penetrationIndex > 0)
            {
                magnitude *= decayRate;
            }
            
            return direction.normalized * magnitude;
        }
        
        /// <summary>
        /// 计算污秽球的击退（加强版）
        /// </summary>
        public Vector2 CalculatePollutionBallKnockback(Vector2 direction)
        {
            // 污秽球使用2倍击退力
            return direction.normalized * (CurrentKnockbackForce * 2f);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 倍率管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置击退倍率
        /// </summary>
        public void SetKnockbackMultiplier(float multiplier)
        {
            skillKnockbackMultiplier = Mathf.Max(0f, multiplier);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[LaserKnockbackHandler] 击退倍率设置: {multiplier:P0}");
            }
        }
        
        /// <summary>
        /// 重置击退倍率
        /// </summary>
        public void ResetKnockbackMultiplier()
        {
            skillKnockbackMultiplier = 1f;
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
