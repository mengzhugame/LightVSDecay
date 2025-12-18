// ============================================================
// ShieldController.cs (重构版 v2.0)
// 文件位置: Assets/Scripts/Logic/Player/ShieldController.cs
// 用途：能量护盾控制
// 改动：
//   - 删除无敌时间功能
//   - 删除自动恢复功能
//   - 冲击波改为护盾破碎时触发
//   - 支持大数值伤害（500护盾值）
//   - 添加百分比恢复接口
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
        [Tooltip("护盾危险颜色（<30%）")]
        [SerializeField] private Color dangerColor = new Color(1f, 0.5f, 0f, 0.5f);
        [Tooltip("护盾极度危险颜色（<10%）")]
        [SerializeField] private Color criticalColor = new Color(1f, 0.2f, 0.2f, 0.5f);
        
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
        private bool wasShieldActive = true; // 用于检测护盾破碎
        
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
            }
            else
            {
                maxShieldHP = defaultMaxShieldHP;
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
            // 与普通伤害逻辑相同，但可以添加额外效果
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
            
            // 触发冲击波（击退/击杀小怪）
            TriggerShockwave();
            
            // 播放护盾破碎特效
            PlayShieldBreakVFX();
            
            // 触发护盾破碎事件（用于后处理效果）
            GameEvents.TriggerShieldBroken();
            
            wasShieldActive = false;
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
            // 查找范围内的敌人
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, shockwaveRadius);
            
            foreach (var hit in hits)
            {
                EnemyBlob enemy = hit.GetComponent<EnemyBlob>();
                if (enemy == null) continue;
                
                Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
                if (enemyRb == null) continue;
                
                // 检查是否为小怪（可被击杀）
                if (enemyRb.mass < shockwaveKillMassThreshold)
                {
                    // 小怪直接击杀
                    enemy.TakeDamage(9999f, Vector2.zero, false);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"[ShieldController] 冲击波击杀小怪: {enemy.name}");
                    }
                }
                else
                {
                    // 大怪击退
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
        
        private void UpdateVisuals()
        {
            if (shieldSprite == null) return;
            
            float percent = ShieldPercent;
            
            if (currentShieldHP <= 0)
            {
                // 护盾破碎 - 隐藏
                shieldSprite.color = Color.clear;
                if (shieldCollider != null) shieldCollider.enabled = false;
            }
            else if (percent < 0.1f)
            {
                // 极度危险 (<10%)
                shieldSprite.color = criticalColor;
                if (shieldCollider != null) shieldCollider.enabled = true;
            }
            else if (percent < 0.3f)
            {
                // 危险 (<30%)
                shieldSprite.color = dangerColor;
                if (shieldCollider != null) shieldCollider.enabled = true;
            }
            else
            {
                // 正常
                shieldSprite.color = normalColor;
                if (shieldCollider != null) shieldCollider.enabled = true;
            }
        }
        
        private void PlayDamageEffect()
        {
            // 使用 VFXPoolManager.Play 方法（高频特效用对象池）
            // 注：ShieldBreak 用于护盾受击特效
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.Play(VFXType.ShieldBreak, transform.position);
            }
        }
        
        /// <summary>
        /// 播放护盾破碎特效（低频，不用对象池）
        /// </summary>
        private void PlayShieldBreakVFX()
        {
            if (shieldBreakVFXPrefab != null)
            {
                // 直接实例化（低频特效）
                GameObject vfx = Instantiate(shieldBreakVFXPrefab, transform.position, Quaternion.identity);
                
                // 自动销毁
                ParticleSystem ps = vfx.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    float lifetime = ps.main.duration + ps.main.startLifetime.constantMax;
                    Destroy(vfx, lifetime);
                }
                else
                {
                    Destroy(vfx, 3f);
                }
                
                if (showDebugInfo)
                {
                    Debug.Log("[ShieldController] 播放护盾破碎特效");
                }
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
        /// 恢复护盾（固定数值）
        /// </summary>
        public void RestoreShield(int amount)
        {
            if (amount <= 0) return;
            
            int oldHP = currentShieldHP;
            currentShieldHP = Mathf.Min(currentShieldHP + amount, maxShieldHP);
            int actualRestore = currentShieldHP - oldHP;
            
            // 如果护盾从0恢复，更新状态
            if (oldHP <= 0 && currentShieldHP > 0)
            {
                wasShieldActive = true;
            }
            
            UpdateVisuals();
            BroadcastShieldStatus();
            PlayRecoveryEffect();
            
            // 触发恢复飘字事件
            if (actualRestore > 0)
            {
                GameEvents.TriggerPlayerShieldRestored(actualRestore, transform.position);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[ShieldController] 恢复护盾 +{actualRestore}: {currentShieldHP}/{maxShieldHP}");
            }
        }
        
        /// <summary>
        /// 恢复护盾（百分比）
        /// </summary>
        /// <param name="percent">恢复百分比（0-1），例如0.1表示10%</param>
        public void RestoreShieldPercent(float percent)
        {
            int amount = Mathf.RoundToInt(maxShieldHP * percent);
            RestoreShield(amount);
        }
        
        /// <summary>
        /// 重置护盾（新游戏）
        /// </summary>
        public void ResetShield()
        {
            currentShieldHP = maxShieldHP;
            wasShieldActive = true;
            
            UpdateVisuals();
            BroadcastShieldStatus();
        }
        
        /// <summary>
        /// 设置最大护盾值（用于养成系统）
        /// </summary>
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