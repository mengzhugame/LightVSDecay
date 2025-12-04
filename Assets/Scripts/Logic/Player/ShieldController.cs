// ============================================================
// ShieldController.cs (修复版)
// 文件位置: Assets/Scripts/Logic/Player/ShieldController.cs
// 用途：能量护盾控制 - 修复 VFXPoolManager 调用
// ============================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Enemy;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 能量护盾控制器
    /// 配置从 GameSettings ScriptableObject 读取
    /// </summary>
    public class ShieldController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("游戏设置")]
        [SerializeField] private GameSettings settings;
        
        [Header("组件引用")]
        [SerializeField] private SpriteRenderer shieldSprite;
        [SerializeField] private Collider2D shieldCollider;
        
        [Header("血量设置（如果没有 GameSettings）")]
        [SerializeField] private int defaultMaxShieldHP = 3;
        [SerializeField] private float defaultRecoveryTime = 12f;
        [SerializeField] private float defaultInvincibilityDuration = 1.0f;
        
        [Header("冲击波设置")]
        [Tooltip("冲击波范围")]
        [SerializeField] private float shockwaveRadius = 5f;
        
        [Tooltip("冲击波力度")]
        [SerializeField] private float shockwaveForce = 3000f;
        
        [Tooltip("冲击波对小怪的击杀质量阈值")]
        [SerializeField] private float shockwaveKillMassThreshold = 2f;
        
        [Header("视觉设置")]
        [SerializeField] private Color normalColor = new Color(0f, 1f, 1f, 0.5f);
        [SerializeField] private Color damagedColor = new Color(1f, 0.5f, 0f, 0.5f);
        [SerializeField] private Color invincibleColor = new Color(1f, 1f, 1f, 0.3f);
        
        [Header("闪烁设置")]
        [SerializeField] private float blinkInterval = 0.1f;
        
        [Header("冲击波子物体")]
        [SerializeField] private Transform shockwaveTransform;
        [SerializeField] private SpriteRenderer shockwaveRenderer;
        [SerializeField] private float shockwaveMaxRadius = 5f;
        [SerializeField] private float shockwaveDuration = 0.4f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时配置缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int maxShieldHP;
        private float shieldRecoveryTime;
        private float invincibilityDuration;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int currentShieldHP;
        private float lastDamageTime;
        private bool isInvincible = false;
        private bool isRecovering = false;
        
        private Coroutine invincibilityCoroutine;
        private Coroutine recoveryCoroutine;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public int CurrentShieldHP => currentShieldHP;
        public int MaxShieldHP => maxShieldHP;
        public bool IsInvincible => isInvincible;
        public bool IsShieldActive => currentShieldHP > 0;
        public float ShieldPercent => maxShieldHP > 0 ? (float)currentShieldHP / maxShieldHP : 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            LoadConfig();
            
            // 初始化冲击波子物体
            if (shockwaveTransform != null)
            {
                shockwaveTransform.localScale = Vector3.zero;
            }
            if (shockwaveRenderer != null)
            {
                Color c = shockwaveRenderer.color;
                c.a = 0f;
                shockwaveRenderer.color = c;
            }
        }
        
        private void Start()
        {
            currentShieldHP = maxShieldHP;
            UpdateVisuals();
            BroadcastShieldStatus();
        }
        
        private void Update()
        {
            // 检查恢复条件
            if (currentShieldHP < maxShieldHP && !isRecovering && !isInvincible)
            {
                if (Time.time - lastDamageTime >= shieldRecoveryTime)
                {
                    StartRecovery();
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置加载
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void LoadConfig()
        {
            if (settings != null)
            {
                maxShieldHP = settings.maxShieldHP;
                shieldRecoveryTime = settings.shieldRecoveryTime;
                invincibilityDuration = settings.invincibilityDuration;
            }
            else
            {
                // 使用默认值
                maxShieldHP = defaultMaxShieldHP;
                shieldRecoveryTime = defaultRecoveryTime;
                invincibilityDuration = defaultInvincibilityDuration;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 护盾受到伤害
        /// </summary>
        /// <returns>是否成功造成伤害</returns>
        public bool TakeDamage(int damage = 1)
        {
            // 无敌状态不受伤
            if (isInvincible)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[ShieldController] 无敌中，伤害无效");
                }
                return false;
            }
            
            // 护盾已破
            if (currentShieldHP <= 0)
            {
                return false;
            }
            
            // 扣血
            currentShieldHP = Mathf.Max(0, currentShieldHP - damage);
            lastDamageTime = Time.time;
            
            // 取消恢复中状态
            if (recoveryCoroutine != null)
            {
                StopCoroutine(recoveryCoroutine);
                recoveryCoroutine = null;
                isRecovering = false;
            }
            
            // 播放受伤特效 - 使用 VFXPoolManager.Play
            PlayDamageEffect();
            
            // 开始无敌
            StartInvincibility();
            
            // 广播状态
            BroadcastShieldStatus();
            
            if (showDebugInfo)
            {
                Debug.Log($"[ShieldController] 护盾受伤: {currentShieldHP}/{maxShieldHP}");
            }
            
            return true;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 无敌帧
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void StartInvincibility()
        {
            if (invincibilityCoroutine != null)
            {
                StopCoroutine(invincibilityCoroutine);
            }
            invincibilityCoroutine = StartCoroutine(InvincibilityCoroutine());
        }
        
        private IEnumerator InvincibilityCoroutine()
        {
            isInvincible = true;
            float elapsed = 0f;
            bool visible = true;
            
            while (elapsed < invincibilityDuration)
            {
                // 闪烁效果
                visible = !visible;
                if (shieldSprite != null)
                {
                    shieldSprite.color = visible ? invincibleColor : Color.clear;
                }
                
                yield return new WaitForSeconds(blinkInterval);
                elapsed += blinkInterval;
            }
            
            isInvincible = false;
            UpdateVisuals();
            
            invincibilityCoroutine = null;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 恢复系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void StartRecovery()
        {
            if (recoveryCoroutine != null)
            {
                StopCoroutine(recoveryCoroutine);
            }
            recoveryCoroutine = StartCoroutine(RecoveryCoroutine());
        }
        
        private IEnumerator RecoveryCoroutine()
        {
            isRecovering = true;
            
            if (showDebugInfo)
            {
                Debug.Log("[ShieldController] 开始恢复护盾");
            }
            
            // 恢复到满血
            currentShieldHP = maxShieldHP;
            
            // 触发冲击波
            TriggerShockwave();
            
            // 播放恢复特效
            PlayRecoveryEffect();
            
            // 广播状态
            BroadcastShieldStatus();
            
            UpdateVisuals();
            
            isRecovering = false;
            recoveryCoroutine = null;
            
            if (showDebugInfo)
            {
                Debug.Log($"[ShieldController] 护盾恢复完成: {currentShieldHP}/{maxShieldHP}");
            }
            
            yield break;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 冲击波
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void TriggerShockwave()
        {
            // 播放冲击波动画
            if (shockwaveTransform != null)
            {
                StartCoroutine(ShockwaveAnimationCoroutine());
            }
            
            // 检测范围内的敌人
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, shockwaveRadius);
            
            List<EnemyBlob> affectedEnemies = new List<EnemyBlob>();
            
            foreach (var hit in hits)
            {
                EnemyBlob enemy = hit.GetComponent<EnemyBlob>();
                if (enemy != null && !affectedEnemies.Contains(enemy))
                {
                    affectedEnemies.Add(enemy);
                }
            }
            
            foreach (var enemy in affectedEnemies)
            {
                // 小怪直接击杀
                if (enemy.GetMass() < shockwaveKillMassThreshold)
                {
                    enemy.KillByShockwave();
                }
                else
                {
                    // 大怪弹开
                    Vector2 direction = (enemy.transform.position - transform.position).normalized;
                    enemy.ApplyKnockback(direction * shockwaveForce);
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[ShieldController] 冲击波影响 {affectedEnemies.Count} 个敌人");
            }
        }
        
        private IEnumerator ShockwaveAnimationCoroutine()
        {
            float elapsed = 0f;
            
            while (elapsed < shockwaveDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shockwaveDuration;
                
                // 缩放扩大
                float scale = Mathf.Lerp(0f, shockwaveMaxRadius, t);
                shockwaveTransform.localScale = new Vector3(scale, scale, 1f);
                
                // 透明度衰减
                if (shockwaveRenderer != null)
                {
                    Color c = shockwaveRenderer.color;
                    c.a = Mathf.Lerp(0.8f, 0f, t);
                    shockwaveRenderer.color = c;
                }
                
                yield return null;
            }
            
            // 重置
            shockwaveTransform.localScale = Vector3.zero;
            if (shockwaveRenderer != null)
            {
                Color c = shockwaveRenderer.color;
                c.a = 0f;
                shockwaveRenderer.color = c;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 视觉效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateVisuals()
        {
            if (shieldSprite == null) return;
            
            if (currentShieldHP <= 0)
            {
                shieldSprite.color = Color.clear;
                if (shieldCollider != null) shieldCollider.enabled = false;
            }
            else if (currentShieldHP == 1)
            {
                shieldSprite.color = damagedColor;
                if (shieldCollider != null) shieldCollider.enabled = true;
            }
            else
            {
                shieldSprite.color = normalColor;
                if (shieldCollider != null) shieldCollider.enabled = true;
            }
        }
        
        private void PlayDamageEffect()
        {
            // 使用 VFXPoolManager.Play 方法
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.Play(VFXType.ShieldBreak, transform.position);
            }
        }
        
        private void PlayRecoveryEffect()
        {
            // 使用 VFXPoolManager.Play 方法
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.Play(VFXType.ShieldRecover, transform.position);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件广播
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void BroadcastShieldStatus()
        {
            GameEvents.TriggerShieldHPChanged(currentShieldHP, maxShieldHP);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 立即恢复护盾（消耗品使用）
        /// </summary>
        public void RestoreShield(int amount = 1)
        {
            currentShieldHP = Mathf.Min(currentShieldHP + amount, maxShieldHP);
            UpdateVisuals();
            BroadcastShieldStatus();
            
            if (showDebugInfo)
            {
                Debug.Log($"[ShieldController] 恢复护盾 +{amount}: {currentShieldHP}/{maxShieldHP}");
            }
        }
        
        /// <summary>
        /// 重置护盾（新游戏）
        /// </summary>
        public void ResetShield()
        {
            currentShieldHP = maxShieldHP;
            isInvincible = false;
            isRecovering = false;
            lastDamageTime = 0f;
            
            if (invincibilityCoroutine != null)
            {
                StopCoroutine(invincibilityCoroutine);
                invincibilityCoroutine = null;
            }
            
            if (recoveryCoroutine != null)
            {
                StopCoroutine(recoveryCoroutine);
                recoveryCoroutine = null;
            }
            
            UpdateVisuals();
            BroadcastShieldStatus();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, shockwaveRadius);
        }
    }
}