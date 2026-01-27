// ============================================================
// BossHealth.cs
// 文件位置: Assets/Scripts/Logic/Enemy/BossHealth.cs
// 用途：Boss 血量管理 - V3.1 统一配置 + 受击效果
// ============================================================

using UnityEngine;
using System.Collections;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Boss;
using LightVsDecay.UI.FloatingText;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// Boss 血量管理器 V3.1
    /// - 血量从 BossConfig 读取
    /// - bodyRenderers 从 BossController 获取
    /// - 支持受击闪白效果
    /// </summary>
    public class BossHealth : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("伤害倍率")]
        [Tooltip("核心弱点伤害倍率（打眼睛）")]
        [SerializeField] private float coreDamageMultiplier = 2.0f;
        
        [Header("碰撞器引用")]
        [Tooltip("身体碰撞器（常驻，受甲壳减伤）")]
        [SerializeField] private Collider2D bodyCollider;
        
        [Header("受击效果")]
        [SerializeField] private float hitFlashDuration = 0.15f;
        [SerializeField] private float hitFlowSpeedMultiplier = 10f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float maxHealth;
        private float currentHealth;
        private bool isDead = false;
        
        private BossController bossController;
        private BossConfig config;
        
        // 材质缓存（从 BossController.BodyRenderers 获取）
        private Material[] bodyMaterials;
        private float normalFlowSpeed = 0.1f;
        
        // 受击效果
        private Coroutine hitFlashCoroutine;
        private bool isBeingHit = false;
        private float lastHitTime;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthPercent => maxHealth > 0 ? currentHealth / maxHealth : 0f;
        public bool IsDead => isDead;
        public Collider2D BodyCollider => bodyCollider;
        public float CoreDamageMultiplier => coreDamageMultiplier;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            bossController = GetComponent<BossController>();
            
            // 查找身体碰撞器
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
            // 从 BossController 获取 config
            if (bossController != null)
            {
                config = bossController.Config;
            }
            
            // 从 config 读取血量
            if (config != null)
            {
                maxHealth = config.maxHealth;
            }
            else
            {
                maxHealth = 5000f; // 默认值
                Debug.LogWarning("[BossHealth] BossConfig 未设置，使用默认血量");
            }
            
            currentHealth = maxHealth;
            
            // 缓存材质
            CacheBodyMaterials();
            
            // 通知 UI
            GameEvents.TriggerBossHealthChanged(HealthPercent);
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossHealth] V3.1 初始化 - 血量: {currentHealth}/{maxHealth}, 材质数量: {(bodyMaterials != null ? bodyMaterials.Length : 0)}");
            }
        }
        
        private void Update()
        {
            UpdateHitEffect();
        }
        
        /// <summary>
        /// 缓存所有身体材质
        /// </summary>
        private void CacheBodyMaterials()
        {
            if (bossController == null) return;
            
            SpriteRenderer[] renderers = bossController.BodyRenderers;
            if (renderers == null || renderers.Length == 0) return;
            
            bodyMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    bodyMaterials[i] = renderers[i].material;
                    
                    // 缓存正常的 FlowSpeed
                    if (i == 0 && bodyMaterials[i] != null)
                    {
                        normalFlowSpeed = bodyMaterials[i].GetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed);
                    }
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 受击效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void TriggerHitEffect()
        {
            if (bodyMaterials == null || bodyMaterials.Length == 0) return;
            
            // 抖动加速：FlowSpeed x10
            foreach (var mat in bodyMaterials)
            {
                if (mat != null)
                {
                    mat.SetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed, normalFlowSpeed * hitFlowSpeedMultiplier);
                }
            }
            
            isBeingHit = true;
            lastHitTime = Time.time;
            
            // 闪烁效果
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
            }
            hitFlashCoroutine = StartCoroutine(HitFlashCoroutine());
        }
        
        private IEnumerator HitFlashCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < hitFlashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / hitFlashDuration;
                float intensity = Mathf.Lerp(1f, 0f, t);
                
                foreach (var mat in bodyMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetFloat(GameConstants.ShaderProperties.LiquidHitIntensity, intensity);
                    }
                }
                
                yield return null;
            }
            
            foreach (var mat in bodyMaterials)
            {
                if (mat != null)
                {
                    mat.SetFloat(GameConstants.ShaderProperties.LiquidHitIntensity, 0f);
                }
            }
            
            hitFlashCoroutine = null;
        }
        
        private void UpdateHitEffect()
        {
            if (bodyMaterials == null || bodyMaterials.Length == 0) return;
            
            // 0.15秒无新伤害，恢复正常 FlowSpeed
            if (isBeingHit && Time.time - lastHitTime > 0.15f)
            {
                foreach (var mat in bodyMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed, normalFlowSpeed);
                    }
                }
                isBeingHit = false;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void TakeBodyDamage(float baseDamage, Vector3 hitPosition, bool isCrit = false, float critMultiplier = 2.0f)
        {
            if (isDead) return;
            
            float armorReduction = config != null ? config.armorDamageReduction : 0.8f;
            float linkedBuffMultiplier = GetLinkedBuffDamageMultiplier();
            
            float actualDamage = baseDamage * (1f - armorReduction) * linkedBuffMultiplier;
            if (isCrit) actualDamage *= critMultiplier;
            
            ApplyDamage(actualDamage);
            ShowBodyDamagePopup(actualDamage, hitPosition, isCrit);
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossHealth] 身体受击 - {baseDamage:F1} x {(1f - armorReduction):P0} = {actualDamage:F1}");
            }
        }
        
        public void TakeCoreDamage(float baseDamage, Vector3 hitPosition, bool isCrit = false, float critMultiplier = 2.0f)
        {
            if (isDead) return;
            
            float linkedBuffMultiplier = GetLinkedBuffDamageMultiplier();
            
            float actualDamage = baseDamage * coreDamageMultiplier * linkedBuffMultiplier;
            if (isCrit) actualDamage *= critMultiplier;
            
            ApplyDamage(actualDamage);
            TriggerHitEffect();  // 【新增】触发受击效果
            ShowCoreDamagePopup(actualDamage, hitPosition, isCrit);
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossHealth] 眼睛受击 - {baseDamage:F1} x {coreDamageMultiplier:P0} = {actualDamage:F1}");
            }
        }
        /// <summary>
        /// 受到真实伤害（身体）- 无视护甲减伤 + 无视连体Buff
        /// Focus LV5 专用
        /// </summary>
        public void TakeTrueBodyDamage(float baseDamage, Vector3 hitPosition, bool isCrit = false, float critMultiplier = 2.0f)
        {
            if (isDead) return;
    
            // 真实伤害：无视护甲减伤，无视连体Buff
            float actualDamage = baseDamage;
            if (isCrit) actualDamage *= critMultiplier;
    
            ApplyDamage(actualDamage);
            ShowBodyDamagePopup(actualDamage, hitPosition, isCrit);
    
            if (showDebugInfo)
            {
                Debug.Log($"[BossHealth] 💥 身体真实伤害 - {baseDamage:F1} → {actualDamage:F1} (无视护甲+连体Buff)");
            }
        }

        /// <summary>
        /// 受到真实伤害（眼睛/弱点）- 保留弱点倍率 + 无视连体Buff
        /// Focus LV5 专用
        /// </summary>
        public void TakeTrueCoreDamage(float baseDamage, Vector3 hitPosition, bool isCrit = false, float critMultiplier = 2.0f)
        {
            if (isDead) return;
    
            // 真实伤害：保留弱点倍率，无视连体Buff
            float actualDamage = baseDamage * coreDamageMultiplier;
            if (isCrit) actualDamage *= critMultiplier;
    
            ApplyDamage(actualDamage);
            TriggerHitEffect();
            ShowCoreDamagePopup(actualDamage, hitPosition, isCrit);
    
            if (showDebugInfo)
            {
                Debug.Log($"[BossHealth] 💥 眼睛真实伤害 - {baseDamage:F1} x {coreDamageMultiplier:P0} = {actualDamage:F1} (无视连体Buff)");
            }
        }
        public void TakeDirectDamage(float damage, Vector3 hitPosition)
        {
            if (isDead) return;
            
            ApplyDamage(damage);
            TriggerHitEffect();  // 【新增】触发受击效果
            ShowCoreDamagePopup(damage, hitPosition, false);
        }
        
        private void ApplyDamage(float damage)
        {
            currentHealth -= damage;
            GameEvents.TriggerBossHealthChanged(HealthPercent);
            
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                OnDeath();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 连体Buff
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public int GetLinkedBuffStacks()
        {
            if (EnemyPoolManager.Instance == null) return 0;
            
            int rusherCount = EnemyPoolManager.Instance.GetActiveCount(EnemyType.Rusher);
            int maxStacks = config != null ? config.linkedBuffMaxStacks : 5;
            
            return Mathf.Min(rusherCount, maxStacks);
        }
        
        public float GetLinkedBuffDamageMultiplier()
        {
            int stacks = GetLinkedBuffStacks();
            float reductionPerStack = config != null ? config.linkedBuffDamageReductionPerStack : 0.1f;
            return 1f - (stacks * reductionPerStack);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 飘字
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ShowBodyDamagePopup(float damage, Vector3 position, bool isCrit)
        {
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.ShowBossShieldDamage(position, damage);
            }
        }
        
        private void ShowCoreDamagePopup(float damage, Vector3 position, bool isCrit)
        {
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.ShowBossCoreDamage(position, damage, isCrit);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 死亡
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDeath()
        {
            if (isDead) return;
            isDead = true;
            
            if (showDebugInfo)
            {
                Debug.Log("[BossHealth] Boss 已死亡！");
            }
            
            GameEvents.TriggerBossDeath();
        }
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 300, 250, 150));
            GUILayout.Label("=== Boss Health V3.1 ===");
            GUILayout.Label($"HP: {currentHealth:F0} / {maxHealth:F0} ({HealthPercent:P1})");
            GUILayout.Label($"连体Buff: {GetLinkedBuffStacks()}层 → {GetLinkedBuffDamageMultiplier():P0}");
            GUILayout.Label($"材质数量: {(bodyMaterials != null ? bodyMaterials.Length : 0)}");
            GUILayout.EndArea();
        }
#endif
    }
}