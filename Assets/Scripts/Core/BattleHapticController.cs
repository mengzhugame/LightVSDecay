// ============================================================
// BattleHapticController.cs
// 文件位置: Assets/Scripts/Core/BattleHapticController.cs
// 用途：战斗手感集中控制器
//   订阅 GameEvents 事件，在关键战斗节点触发手机震动 + 镜头抖动
//   挂载位置：GameManager 所在节点（与其他 Core 管理器同层级）
// ============================================================

using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Core
{
    /// <summary>
    /// 战斗手感控制器（手机震动 + 镜头抖动）
    /// 当前覆盖的触发点：
    ///   1. 护盾破碎 → Haptic Heavy(400ms) + CameraShake(0.4, 0.35)
    /// BOSS 入场和阶段切换的震动由各 BossController 内部直接调用
    /// </summary>
    public class BattleHapticController : MonoBehaviour
    {
        [Header("护盾破碎镜头抖动")]
        [SerializeField] private float shieldBreakShakeIntensity = 0.4f;
        [SerializeField] private float shieldBreakShakeDuration  = 0.35f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnEnable()
        {
            GameEvents.OnShieldBroken += OnShieldBroken;
        }

        private void OnDisable()
        {
            GameEvents.OnShieldBroken -= OnShieldBroken;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件响应
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 护盾破碎：重度震动 + 镜头抖动
        /// </summary>
        private void OnShieldBroken()
        {
            HapticFeedback.Instance?.Trigger(HapticType.Heavy);
            CameraShake.Instance?.Shake(shieldBreakShakeIntensity, shieldBreakShakeDuration);
        }
    }
}
