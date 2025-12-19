// ============================================================
// BossHealth.cs
// 文件位置: Assets/Scripts/Logic/Enemy/BossHealth.cs
// 用途：Boss 血量管理 - 支持护甲/核心差异化伤害 + 暴击叠加
// ============================================================

using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Logic.Boss;
using LightVsDecay.UI.FloatingText;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// Boss 血量管理器
    /// 支持护甲（30%伤害）和核心（200%伤害）差异化伤害
    /// 支持弱点 + 暴击叠加机制
    /// </summary>
    public class BossHealth : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("血量设置")]
        [Tooltip("Boss 最大血量")]
        [SerializeField] private float maxHealth = 5000f;
        
        [Header("伤害倍率")]
        [Tooltip("护甲伤害倍率（打外壳）")]
        [SerializeField] private float armorDamageMultiplier = 0.3f; // 30%
        
        [Tooltip("核心弱点伤害倍率（打核心）")]
        [SerializeField] private float coreDamageMultiplier = 2.0f;  // 200%
        
        [Header("组件引用")]
        [Tooltip("核心弱点碰撞器（Eyes）")]
        [SerializeField] private Collider2D coreCollider;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float currentHealth;
        private bool isDead = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前血量</summary>
        public float CurrentHealth => currentHealth;
        
        /// <summary>最大血量</summary>
        public float MaxHealth => maxHealth;
        
        /// <summary>血量百分比 (0-1)</summary>
        public float HealthPercent => maxHealth > 0 ? currentHealth / maxHealth : 0f;
        
        /// <summary>是否已死亡</summary>
        public bool IsDead => isDead;
        
        /// <summary>核心碰撞器（供外部检测）</summary>
        public Collider2D CoreCollider => coreCollider;
        
        /// <summary>护甲伤害倍率</summary>
        public float ArmorDamageMultiplier => armorDamageMultiplier;
        
        /// <summary>核心弱点伤害倍率</summary>
        public float CoreDamageMultiplier => coreDamageMultiplier;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 自动查找核心碰撞器（如果未设置）
            if (coreCollider == null)
            {
                Transform eyes = transform.Find("Eyes");
                if (eyes != null)
                {
                    coreCollider = eyes.GetComponent<Collider2D>();
                }
            }
        }
        
        private void Start()
        {
            currentHealth = maxHealth;
            
            // 通知 UI 更新
            GameEvents.TriggerBossHealthChanged(HealthPercent);
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossHealth] 初始化完成 - 血量: {currentHealth}/{maxHealth}");
                Debug.Log($"[BossHealth] 护甲倍率: {armorDamageMultiplier:P0}, 核心倍率: {coreDamageMultiplier:P0}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 受到护甲伤害（打外壳）
        /// 公式：基础伤害 × 护甲修正(30%) × 暴击倍率
        /// </summary>
        /// <param name="baseDamage">基础伤害值</param>
        /// <param name="hitPosition">受击位置（用于飘字）</param>
        /// <param name="isCrit">是否暴击</param>
        /// <param name="critMultiplier">暴击倍率（默认2.0）</param>
        public void TakeArmorDamage(float baseDamage, Vector3 hitPosition, bool isCrit = false, float critMultiplier = 2.0f)
        {
            if (isDead) return;
            
            // 计算最终伤害：基础伤害 × 护甲修正 × 暴击倍率
            float actualDamage = baseDamage * armorDamageMultiplier;
            if (isCrit)
            {
                actualDamage *= critMultiplier;
            }
            
            ApplyDamage(actualDamage);
            
            // 显示飘字（护甲伤害用银灰色盾牌样式）
            // 即使暴击，护甲伤害也用 BossShield 样式（只是数字更大）
            ShowArmorDamagePopup(actualDamage, hitPosition, isCrit);
            
            if (showDebugInfo)
            {
                string critStr = isCrit ? $" x{critMultiplier:P0}(暴击)" : "";
                Debug.Log($"[BossHealth] 护甲受击 - {baseDamage:F1} x {armorDamageMultiplier:P0}{critStr} = {actualDamage:F1}");
            }
        }
        
        /// <summary>
        /// 受到核心伤害（打弱点）
        /// 公式：基础伤害 × 护甲修正(100%) × 弱点修正(200%) × 暴击倍率
        /// </summary>
        /// <param name="baseDamage">基础伤害值</param>
        /// <param name="hitPosition">受击位置（用于飘字）</param>
        /// <param name="isCrit">是否暴击</param>
        /// <param name="critMultiplier">暴击倍率（默认2.0）</param>
        public void TakeCoreDamage(float baseDamage, Vector3 hitPosition, bool isCrit = false, float critMultiplier = 2.0f)
        {
            if (isDead) return;
            
            // 计算最终伤害：基础伤害 × 弱点修正 × 暴击倍率
            // 核心不受护甲修正影响（护甲修正=100%）
            float actualDamage = baseDamage * coreDamageMultiplier;
            if (isCrit)
            {
                actualDamage *= critMultiplier;
            }
            
            ApplyDamage(actualDamage);

            BossController controller = GetComponent<BossController>();
            if (controller != null)
            {
                controller.OnDamageReceived();
            }
            // 显示飘字
            ShowCoreDamagePopup(actualDamage, hitPosition, isCrit);
            
            if (showDebugInfo)
            {
                string critStr = isCrit ? $" x{critMultiplier:P0}(暴击)" : "";
                Debug.Log($"[BossHealth] 核心受击 - {baseDamage:F1} x {coreDamageMultiplier:P0}(弱点){critStr} = {actualDamage:F1}");
            }
        }
        
        /// <summary>
        /// 应用伤害（内部方法）
        /// </summary>
        private void ApplyDamage(float damage)
        {
            currentHealth -= damage;
            
            // 通知 UI 更新血条
            GameEvents.TriggerBossHealthChanged(HealthPercent);
            
            // 检查死亡
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                OnDeath();
            }
        }
        
        /// <summary>
        /// 显示护甲伤害飘字
        /// </summary>
        private void ShowArmorDamagePopup(float damage, Vector3 position, bool isCrit)
        {
            if (FloatingTextManager.Instance == null) return;
            
            // 护甲伤害始终用 BossShield 样式（银灰色 + 盾牌图标）
            // 暴击只影响数字大小，不改变样式
            FloatingTextManager.Instance.ShowBossShieldDamage(position, damage);
        }
        
        /// <summary>
        /// 显示核心伤害飘字
        /// </summary>
        private void ShowCoreDamagePopup(float damage, Vector3 position, bool isCrit)
        {
            if (FloatingTextManager.Instance == null) return;
            
            // 核心伤害：
            // - 普通弱点命中 → BossCore 样式（红色 1.3倍 + 眼睛图标）
            // - 弱点 + 暴击 → Crit 样式（红色 1.5倍 + 爆炸图标 + 弹跳动画）
            FloatingTextManager.Instance.ShowBossCoreDamage(position, damage, isCrit);
        }
        
        /// <summary>
        /// Boss 死亡处理
        /// </summary>
        private void OnDeath()
        {
            if (isDead) return;
            isDead = true;
            
            if (showDebugInfo)
            {
                Debug.Log("[BossHealth] Boss 已死亡！");
            }
            
            // 触发 Boss 死亡事件
            GameEvents.TriggerBossDeath();
            
            // 播放死亡特效（可扩展）
            // TODO: 死亡动画、掉落奖励等
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置最大血量（用于难度缩放）
        /// </summary>
        public void SetMaxHealth(float newMaxHealth)
        {
            maxHealth = newMaxHealth;
            currentHealth = maxHealth;
            GameEvents.TriggerBossHealthChanged(HealthPercent);
        }
        
        /// <summary>
        /// 重置血量
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;
            isDead = false;
            GameEvents.TriggerBossHealthChanged(HealthPercent);
        }
        
        /// <summary>
        /// 检查碰撞器是否为核心
        /// </summary>
        public bool IsCore(Collider2D collider)
        {
            return coreCollider != null && collider == coreCollider;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 300, 220, 120));
            GUILayout.Label("=== Boss Health ===");
            GUILayout.Label($"HP: {currentHealth:F0} / {maxHealth:F0}");
            GUILayout.Label($"Percent: {HealthPercent:P1}");
            GUILayout.Label($"Armor Mult: {armorDamageMultiplier:P0}");
            GUILayout.Label($"Core Mult: {coreDamageMultiplier:P0}");
            GUILayout.Label($"Dead: {isDead}");
            GUILayout.EndArea();
        }
#endif
    }
}