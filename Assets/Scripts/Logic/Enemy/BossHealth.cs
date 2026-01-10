// ============================================================
// BossHealth.cs
// 文件位置: Assets/Scripts/Logic/Enemy/BossHealth.cs
// 用途：Boss 血量管理 - V3.0 双碰撞器 + 连体Buff
// 【重构】支持身体/眼睛差异化伤害 + Rusher连体Buff减伤
// ============================================================

using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Boss;
using LightVsDecay.UI.FloatingText;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// Boss 血量管理器 V3.0
    /// 支持：
    /// - 身体碰撞器（80%减伤 + 连体Buff）
    /// - 眼睛碰撞器（全额伤害 + 连体Buff）
    /// - Rusher连体Buff（每只+10%减伤，上限50%）
    /// </summary>
    public class BossHealth : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("血量设置")]
        [Tooltip("Boss 最大血量")]
        [SerializeField] private float maxHealth = 5000f;
        
        [Header("V3.0 配置引用")]
        [Tooltip("Boss行为配置（用于读取V3.0参数）")]
        [SerializeField] private BossConfig config;
        
        [Header("伤害倍率（仅眼睛）")]
        [Tooltip("核心弱点伤害倍率（打眼睛）")]
        [SerializeField] private float coreDamageMultiplier = 2.0f;  // 200%
        
        [Header("碰撞器引用")]
        [Tooltip("身体碰撞器（常驻，受甲壳减伤）")]
        [SerializeField] private Collider2D bodyCollider;
        
        [Tooltip("眼睛碰撞器（切换，弱点）")]
        [SerializeField] private Collider2D eyeCollider;
        
        [Header("音效预留")]
        [Tooltip("打身体音效ID（预留）")]
        [SerializeField] private string bodyHitSfxId = "boss_armor_hit";
        
        [Tooltip("打眼睛音效ID（预留）")]
        [SerializeField] private string eyeHitSfxId = "boss_core_hit";
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float currentHealth;
        private bool isDead = false;
        private BossController bossController;
        
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
        
        /// <summary>身体碰撞器（供外部检测）</summary>
        public Collider2D BodyCollider => bodyCollider;
        
        /// <summary>眼睛碰撞器（供外部检测）</summary>
        public Collider2D EyeCollider => eyeCollider;
        
        /// <summary>核心弱点伤害倍率</summary>
        public float CoreDamageMultiplier => coreDamageMultiplier;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            bossController = GetComponent<BossController>();
            
            // 自动查找碰撞器（如果未设置）
            if (eyeCollider == null)
            {
                Transform eyes = transform.Find("Eyes");
                if (eyes != null)
                {
                    eyeCollider = eyes.GetComponent<Collider2D>();
                }
            }
            
            // 身体碰撞器通常在Boss根节点或Body子节点
            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
                if (bodyCollider == null)
                {
                    Transform body = transform.Find("Body");
                    if (body != null)
                    {
                        bodyCollider = body.GetComponent<Collider2D>();
                    }
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
                Debug.Log($"[BossHealth] V3.0 初始化完成 - 血量: {currentHealth}/{maxHealth}");
                Debug.Log($"[BossHealth] 身体碰撞器: {(bodyCollider != null ? bodyCollider.name : "未设置")}");
                Debug.Log($"[BossHealth] 眼睛碰撞器: {(eyeCollider != null ? eyeCollider.name : "未设置")}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // V3.0 连体Buff计算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取当前连体Buff层数（场上Rusher数量，上限5）
        /// </summary>
        public int GetLinkedBuffStacks()
        {
            if (EnemyPoolManager.Instance == null) return 0;
            
            int rusherCount = EnemyPoolManager.Instance.GetActiveCount(EnemyType.Rusher);
            int maxStacks = config != null ? config.linkedBuffMaxStacks : 5;
            
            return Mathf.Min(rusherCount, maxStacks);
        }
        
        /// <summary>
        /// 获取连体Buff减伤倍率
        /// 例：3只Rusher = 3层 × 10% = 30%减伤 → 返回0.7
        /// </summary>
        public float GetLinkedBuffDamageMultiplier()
        {
            int stacks = GetLinkedBuffStacks();
            float reductionPerStack = config != null ? config.linkedBuffDamageReductionPerStack : 0.1f;
            float totalReduction = stacks * reductionPerStack;
            
            return 1f - totalReduction;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 受到身体伤害（打外壳）
        /// 公式：基础伤害 × 甲壳减伤(20%) × 连体Buff减伤 × 暴击倍率
        /// </summary>
        /// <param name="baseDamage">基础伤害值</param>
        /// <param name="hitPosition">受击位置（用于飘字）</param>
        /// <param name="isCrit">是否暴击</param>
        /// <param name="critMultiplier">暴击倍率（默认2.0）</param>
        public void TakeBodyDamage(float baseDamage, Vector3 hitPosition, bool isCrit = false, float critMultiplier = 2.0f)
        {
            if (isDead) return;
            
            // V3.0 甲壳减伤（闭眼时80%减伤 = 20%伤害）
            float armorMultiplier = config != null ? (1f - config.armorDamageReduction) : 0.2f;
            
            // V3.0 连体Buff减伤
            float linkedBuffMultiplier = GetLinkedBuffDamageMultiplier();
            
            // 计算最终伤害
            float actualDamage = baseDamage * armorMultiplier * linkedBuffMultiplier;
            if (isCrit)
            {
                actualDamage *= critMultiplier;
            }
            
            ApplyDamage(actualDamage);
            
            // 通知Controller记录受击（用于频率打断）
            if (bossController != null)
            {
                bossController.OnHitReceived();
            }
            
            // 显示飘字（护甲伤害用银灰色盾牌样式）
            ShowBodyDamagePopup(actualDamage, hitPosition, isCrit);
            
            // 预留音效接口
            PlayHitSound(false);
            
            if (showDebugInfo)
            {
                int stacks = GetLinkedBuffStacks();
                string critStr = isCrit ? $" x{critMultiplier:P0}(暴击)" : "";
                Debug.Log($"[BossHealth] 身体受击 - {baseDamage:F1} x {armorMultiplier:P0}(甲壳) x {linkedBuffMultiplier:P0}(Buff:{stacks}层){critStr} = {actualDamage:F1}");
            }
        }
        
        /// <summary>
        /// 受到眼睛伤害（打弱点）
        /// 公式：基础伤害 × 弱点倍率(200%) × 连体Buff减伤 × 暴击倍率
        /// 注意：眼睛不受甲壳减伤影响
        /// </summary>
        /// <param name="baseDamage">基础伤害值</param>
        /// <param name="hitPosition">受击位置（用于飘字）</param>
        /// <param name="isCrit">是否暴击</param>
        /// <param name="critMultiplier">暴击倍率（默认2.0）</param>
        public void TakeCoreDamage(float baseDamage, Vector3 hitPosition, bool isCrit = false, float critMultiplier = 2.0f)
        {
            if (isDead) return;
            
            // V3.0 连体Buff减伤（眼睛也受Buff影响）
            float linkedBuffMultiplier = GetLinkedBuffDamageMultiplier();
            
            // 计算最终伤害：基础伤害 × 弱点修正 × 连体Buff × 暴击
            float actualDamage = baseDamage * coreDamageMultiplier * linkedBuffMultiplier;
            if (isCrit)
            {
                actualDamage *= critMultiplier;
            }
            
            ApplyDamage(actualDamage);
            
            // 通知Controller记录受击（用于频率打断）
            if (bossController != null)
            {
                bossController.OnHitReceived();
                bossController.OnDamageReceived(actualDamage); // 用于Press过载检测
            }
            
            // 显示飘字
            ShowCoreDamagePopup(actualDamage, hitPosition, isCrit);
            
            // 预留音效接口
            PlayHitSound(true);
            
            if (showDebugInfo)
            {
                int stacks = GetLinkedBuffStacks();
                string critStr = isCrit ? $" x{critMultiplier:P0}(暴击)" : "";
                Debug.Log($"[BossHealth] 眼睛受击 - {baseDamage:F1} x {coreDamageMultiplier:P0}(弱点) x {linkedBuffMultiplier:P0}(Buff:{stacks}层){critStr} = {actualDamage:F1}");
            }
        }
        
        /// <summary>
        /// 受到直接伤害（无修正，用于毒球反弹等特殊情况）
        /// </summary>
        public void TakeDirectDamage(float damage, Vector3 hitPosition)
        {
            if (isDead) return;
            
            ApplyDamage(damage);
            ShowCoreDamagePopup(damage, hitPosition, false);
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossHealth] 直接伤害: {damage:F1}");
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
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 飘字显示
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示身体伤害飘字
        /// </summary>
        private void ShowBodyDamagePopup(float damage, Vector3 position, bool isCrit)
        {
            if (FloatingTextManager.Instance == null) return;
            
            // 身体伤害用 BossShield 样式（银灰色 + 盾牌图标）
            FloatingTextManager.Instance.ShowBossShieldDamage(position, damage);
        }
        
        /// <summary>
        /// 显示眼睛伤害飘字
        /// </summary>
        private void ShowCoreDamagePopup(float damage, Vector3 position, bool isCrit)
        {
            if (FloatingTextManager.Instance == null) return;
            
            // 核心伤害用 BossCore 样式（红色）
            FloatingTextManager.Instance.ShowBossCoreDamage(position, damage, isCrit);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 音效预留接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放受击音效（预留接口）
        /// </summary>
        /// <param name="isEyeHit">true=打眼睛, false=打身体</param>
        private void PlayHitSound(bool isEyeHit)
        {
            // TODO: 接入音效管理器
            // string sfxId = isEyeHit ? eyeHitSfxId : bodyHitSfxId;
            // AudioManager.Instance?.PlaySfx(sfxId);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 死亡处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
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
        /// 检查碰撞器是否为眼睛
        /// </summary>
        public bool IsEyeCollider(Collider2D collider)
        {
            return eyeCollider != null && collider == eyeCollider;
        }
        
        /// <summary>
        /// 检查碰撞器是否为身体
        /// </summary>
        public bool IsBodyCollider(Collider2D collider)
        {
            return bodyCollider != null && collider == bodyCollider;
        }
        
        // 兼容旧代码
        [System.Obsolete("Use IsEyeCollider instead")]
        public bool IsCore(Collider2D collider) => IsEyeCollider(collider);
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 300, 250, 150));
            GUILayout.Label("=== Boss Health V3.0 ===");
            GUILayout.Label($"HP: {currentHealth:F0} / {maxHealth:F0} ({HealthPercent:P1})");
            
            int stacks = GetLinkedBuffStacks();
            float buffMult = GetLinkedBuffDamageMultiplier();
            GUILayout.Label($"连体Buff: {stacks}层 → {buffMult:P0} 伤害");
            
            float armorMult = config != null ? (1f - config.armorDamageReduction) : 0.2f;
            GUILayout.Label($"甲壳倍率: {armorMult:P0}");
            GUILayout.Label($"弱点倍率: {coreDamageMultiplier:P0}");
            GUILayout.Label($"Dead: {isDead}");
            GUILayout.EndArea();
        }
#endif
    }
}