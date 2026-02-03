// ============================================================
// ShieldController.cs (修复版 v2.1)
// 文件位置: Assets/Scripts/Logic/Player/ShieldController.cs
// 用途：能量护盾控制
// 改动 v2.1:
//   - 【新增】受伤闪白效果 (HitFlash shader)
//   - 【修改】护盾 <20% 时降低透明度到0.5，去掉颜色变化
// ============================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using LightVsDecay.Audio;
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
        
        [Header("护盾设置（如果没有 GameSettings）")]
        [SerializeField] private int defaultMaxShieldHP = 500;
        
        [Header("冲击波设置")]
        [Tooltip("冲击波范围")]
        [SerializeField] private float shockwaveRadius = 5f;
        
        [Tooltip("冲击波力度")]
        [SerializeField] private float shockwaveForce = 3000f;
        
        [Tooltip("冲击波对小怪的击杀质量阈值")]
        [SerializeField] private float shockwaveKillMassThreshold = 2f;
        
        [Header("视觉设置")]
        [Tooltip("护盾正常颜色")]
        [SerializeField] private Color normalColor = new Color(0f, 1f, 1f, 0.5f);
        
        [Header("闪白效果")]
        [Tooltip("闪白持续时间")]
        [SerializeField] private float hitFlashDuration = 0.15f;
        
        [Header("透明度设置")]
        [Tooltip("护盾低血量阈值（低于此百分比降低透明度）")]
        [SerializeField] private float lowShieldThreshold = 0.2f;
        
        [Tooltip("低血量时的透明度")]
        [SerializeField] private float lowShieldAlpha = 0.5f;
        
        [Header("冲击波子物体")]
        [SerializeField] private Transform shockwaveTransform;
        [SerializeField] private SpriteRenderer shockwaveRenderer;
        [SerializeField] private float shockwaveMaxRadius = 5f;
        [SerializeField] private float shockwaveDuration = 0.4f;
        
        [Header("护盾破碎特效")]
        [Tooltip("护盾破碎粒子特效 Prefab")]
        [SerializeField] private GameObject shieldBreakVFXPrefab;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时配置缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int maxShieldHP;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int currentShieldHP;
        private bool wasShieldActive = true;
        
        // 闪白效果
        private Coroutine hitFlashCoroutine;
        private Material shieldMaterial;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public int CurrentShieldHP => currentShieldHP;
        public int MaxShieldHP => maxShieldHP;
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
            
            // 【新增】缓存 Material（用于闪白效果）
            if (shieldSprite != null)
            {
                shieldMaterial = shieldSprite.material;
            }
        }
        
        private void Start()
        {
            currentShieldHP = maxShieldHP;
            wasShieldActive = true;
            UpdateVisuals();
            BroadcastShieldStatus();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置加载
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void LoadConfig()
        {
            if (settings != null)
            {
                maxShieldHP = settings.maxShieldHP;
                
                if (showDebugInfo)
                {
                    Debug.Log($"[ShieldController] 从 GameSettings 加载: maxShieldHP = {maxShieldHP}");
                }
            }
            else
            {
                maxShieldHP = defaultMaxShieldHP;
                
                if (showDebugInfo)
                {
                    Debug.Log($"[ShieldController] GameSettings 未设置，使用默认值: maxShieldHP = {maxShieldHP}");
                }
            }
            
            // 安全检查：防止异常值
            if (maxShieldHP <= 0 || maxShieldHP > 100000)
            {
                Debug.LogWarning($"[ShieldController] ⚠️ maxShieldHP 异常值: {maxShieldHP}，重置为 500");
                maxShieldHP = 500;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 护盾受到伤害
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <returns>溢出伤害（护盾无法吸收的部分）</returns>
        public int TakeDamage(int damage)
        {
            if (damage <= 0) return 0;
            // 【新增】大招无敌检查
            if (OverloadManager.Instance != null && OverloadManager.Instance.IsActive)
            {
                return 0; // 无敌状态，不受伤害
            }
            // 护盾已破，直接返回全部伤害
            if (currentShieldHP <= 0)
            {
                return damage;
            }
            
            // 记录之前的状态
            bool wasActive = currentShieldHP > 0;
            
            // 计算溢出伤害
            int overflow = 0;
            if (damage > currentShieldHP)
            {
                overflow = damage - currentShieldHP;
                currentShieldHP = 0;
            }
            else
            {
                currentShieldHP -= damage;
            }
            
            // 播放受伤特效
            PlayDamageEffect();
            // 【新增】播放护盾受击音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayShieldHit();
            }
            // 【新增】触发闪白效果
            TriggerHitFlash();
            
            // 广播状态
            BroadcastShieldStatus();
            
            // 触发玩家受击飘字事件
            GameEvents.TriggerPlayerShieldDamaged(damage - overflow, transform.position);
            
            if (showDebugInfo)
            {
                Debug.Log($"[ShieldController] 护盾受伤: -{damage}, 剩余: {currentShieldHP}/{maxShieldHP}, 溢出: {overflow}");
            }
            
            // 检查护盾是否刚刚破碎
            if (wasActive && currentShieldHP <= 0)
            {
                OnShieldBroken();
            }
            
            // 更新视觉
            UpdateVisuals();
            
            return overflow;
        }
        
        /// <summary>
        /// 受到BOSS伤害（大数值伤害）
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <returns>溢出伤害</returns>
        public int TakeBossDamage(int damage)
        {
            return TakeDamage(damage);
        }
        
        /// <summary>
        /// 护盾破碎处理
        /// </summary>
        private void OnShieldBroken()
        {
            if (showDebugInfo)
            {
                Debug.Log("[ShieldController] 💔 护盾破碎！触发冲击波！");
            }
            
            // 触发冲击波
            TriggerShockwave();
            
            // 播放护盾破碎特效
            PlayBreakEffect();

            // 触发护盾破碎事件
            GameEvents.TriggerShieldBroken();
            
            wasShieldActive = false;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 闪白效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 触发闪白效果
        /// </summary>
        private void TriggerHitFlash()
        {
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
            }
            hitFlashCoroutine = StartCoroutine(HitFlashCoroutine());
        }
        
        /// <summary>
        /// 闪白效果协程
        /// </summary>
        private IEnumerator HitFlashCoroutine()
        {
            if (shieldMaterial == null) yield break;
            
            // 设置闪白为1（全白）
            shieldMaterial.SetFloat(GameConstants.ShaderProperties.HitFlash, 1f);
            
            float elapsed = 0f;
            while (elapsed < hitFlashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / hitFlashDuration;
                
                // 从1渐变到0
                float flashValue = Mathf.Lerp(1f, 0f, t);
                shieldMaterial.SetFloat(GameConstants.ShaderProperties.HitFlash, flashValue);
                
                yield return null;
            }
            
            // 确保最终值为0
            shieldMaterial.SetFloat(GameConstants.ShaderProperties.HitFlash, 0f);
            hitFlashCoroutine = null;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 冲击波
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void TriggerShockwave()
        {
            // 播放冲击波动画
            if (shockwaveTransform != null)
            {
                StartCoroutine(ShockwaveAnimation());
            }
            
            // 对范围内敌人造成效果
            ApplyShockwaveEffect();
        }
        
        private IEnumerator ShockwaveAnimation()
        {
            float elapsed = 0f;
            
            while (elapsed < shockwaveDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shockwaveDuration;
                
                // 缩放动画
                float scale = Mathf.Lerp(0f, shockwaveMaxRadius, t);
                shockwaveTransform.localScale = Vector3.one * scale;
                
                // 淡出
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
        
        private void ApplyShockwaveEffect()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, shockwaveRadius);
            
            foreach (var hit in hits)
            {
                EnemyBlob enemy = hit.GetComponent<EnemyBlob>();
                if (enemy == null) continue;
                
                Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
                if (enemyRb == null) continue;
                
                if (enemyRb.mass < shockwaveKillMassThreshold)
                {
                    enemy.TakeDamage(9999f, Vector2.zero, false);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"[ShieldController] 冲击波击杀小怪: {enemy.name}");
                    }
                }
                else
                {
                    Vector2 direction = (enemy.transform.position - transform.position).normalized;
                    enemyRb.AddForce(direction * shockwaveForce, ForceMode2D.Impulse);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"[ShieldController] 冲击波击退大怪: {enemy.name}");
                    }
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 视觉效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 【修改】更新视觉 - 护盾<20%时降低透明度到0.5，去掉颜色变化
        /// </summary>
        private void UpdateVisuals()
        {
            if (shieldSprite == null) return;
            
            if (currentShieldHP <= 0)
            {
                // 护盾破碎 - 完全透明
                shieldSprite.color = Color.clear;
                if (shieldCollider != null) shieldCollider.enabled = false;
            }
            else
            {
                // 护盾存在
                if (shieldCollider != null) shieldCollider.enabled = true;
                
                // 计算透明度：护盾 < 20% 时降低透明度到0.5
                float percent = ShieldPercent;
                float alpha;
                
                if (percent < lowShieldThreshold)
                {
                    // 护盾低于阈值，降低透明度
                    alpha = lowShieldAlpha;
                }
                else
                {
                    // 正常透明度
                    alpha = normalColor.a;
                }
                
                // 始终使用 normalColor 的颜色，只调整透明度
                Color displayColor = normalColor;
                displayColor.a = alpha;
                shieldSprite.color = displayColor;
            }
        }
        
        private void PlayDamageEffect()
        {
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance?.PlayShieldHit(transform.position);
            }
        }
        private void PlayBreakEffect()
        {
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance?.PlayShieldBreak(transform.position);
            }
        }

        private void PlayRecoveryEffect()
        {
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance?.PlayShieldRecover(transform.position);
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
        
        public void RestoreShield(int amount)
        {
            if (amount <= 0) return;
            
            int oldHP = currentShieldHP;
            currentShieldHP = Mathf.Min(currentShieldHP + amount, maxShieldHP);
            int actualRestore = currentShieldHP - oldHP;
            
            if (oldHP <= 0 && currentShieldHP > 0)
            {
                wasShieldActive = true;
            }
            
            UpdateVisuals();
            BroadcastShieldStatus();
            PlayRecoveryEffect();
            
            if (actualRestore > 0)
            {
                GameEvents.TriggerPlayerShieldRestored(actualRestore, transform.position);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[ShieldController] 恢复护盾 +{actualRestore}: {currentShieldHP}/{maxShieldHP}");
            }
        }
        
        public void RestoreShieldPercent(float percent)
        {
            int amount = Mathf.RoundToInt(maxShieldHP * percent);
            RestoreShield(amount);
        }
        
        public void ResetShield()
        {
            currentShieldHP = maxShieldHP;
            wasShieldActive = true;
            
            UpdateVisuals();
            BroadcastShieldStatus();
        }
        
        public void SetMaxShieldHP(int newMax)
        {
            float percent = ShieldPercent;
            maxShieldHP = newMax;
            currentShieldHP = Mathf.RoundToInt(maxShieldHP * percent);
            
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