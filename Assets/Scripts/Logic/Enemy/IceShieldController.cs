// ============================================================
// IceShieldController.cs
// 文件位置: Assets/Scripts/Logic/Enemy/IceShieldController.cs
// 用途：精英冰甲卫士 · 前置冰盾系统
//
//   - 冰盾存在时，EnemyBlob.TakeDamage() 将激光伤害完全重定向到此组件
//   - 冰盾 HP 归零后自动 Deactivate（隐藏视觉，主体恢复正常受击）
//   - 提供 Initialize / TakeDamage / IsActive / HPPercent 接口
//
// 挂载位置：EliteIceShieldGuard 预制体的子 GameObject（含冰盾 Sprite）
// ============================================================

using UnityEngine;
using LightVsDecay.Audio;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Logic.Statistics;
using LightVsDecay.UI.FloatingText;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// 精英冰甲卫士的前置冰盾。
    /// 由 EnemyBlob 在 TakeDamage() 中检测并重定向伤害。
    /// </summary>
    public class IceShieldController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("视觉")]
        [Tooltip("冰盾的 SpriteRenderer（存活时显示，破碎后隐藏）")]
        [SerializeField] private SpriteRenderer shieldSprite;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private float maxHP;
        private float currentHP;
        private bool isActive = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>冰盾是否仍然存活</summary>
        public bool IsActive => isActive;

        /// <summary>当前 HP 百分比（0~1）</summary>
        public float HPPercent => maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 由 EnemyBlob.OnSpawn() 调用，初始化冰盾并激活。
        /// </summary>
        public void Initialize(float hp)
        {
            maxHP     = hp;
            currentHP = hp;
            isActive  = true;
            SetVisual(true);

            if (showDebugInfo)
                GameLogger.Log($"[IceShield] 冰盾初始化，HP = {maxHP}");
        }

        /// <summary>
        /// 由 EnemyBlob.OnSpawn() 调用（无冰盾配置时强制隐藏）。
        /// </summary>
        public void Deactivate()
        {
            isActive = false;
            SetVisual(false);
        }

        /// <summary>
        /// 冰盾受到激光伤害（由 EnemyBlob.TakeDamage 重定向而来）。
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (!isActive) return;

            currentHP -= amount;
            BattleStatistics.Instance?.RecordIceShieldDamageAbsorbed(amount);

            // 显示伤害飘字（复用 Boss 护盾样式，蓝色调）
            if (FloatingTextManager.Instance != null)
                FloatingTextManager.Instance.ShowBossShieldDamage(transform.position, amount);

            if (showDebugInfo)
                GameLogger.Log($"[IceShield] 受到伤害 {amount}，剩余 HP = {currentHP:F0}/{maxHP}");

            if (currentHP <= 0f)
            {
                OnShieldBroken();
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内部
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnShieldBroken()
        {
            isActive = false;
            SetVisual(false);
            BattleStatistics.Instance?.RecordIceShieldBroken();

            // 冰盾破碎特效 + 音效
            VFXPoolManager.Instance?.Play(VFXType.IceShieldBreak, transform.position);
            AudioManager.Instance?.PlayIceShieldBreak();

            if (showDebugInfo)
                GameLogger.Log("[IceShield] 冰盾已破碎！本体恢复正常受击。");
        }

        private void SetVisual(bool visible)
        {
            if (shieldSprite != null)
                shieldSprite.enabled = visible;

            // 同步子级所有 Renderer
            foreach (var rend in GetComponentsInChildren<Renderer>(true))
            {
                if (rend != null) rend.enabled = visible;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器辅助
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!isActive) return;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.8f);
        }
#endif
    }
}
