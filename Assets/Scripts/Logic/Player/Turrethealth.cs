// ============================================================
// TurretHealth.cs (修复版)
// 文件位置: Assets/Scripts/Logic/Player/TurretHealth.cs
// 用途：塔本体生命值 - 修复 VFXPoolManager 调用
// ============================================================

using UnityEngine;
using System.Collections;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;
using LightVsDecay.Data.SO;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 光棱塔本体生命值控制
    /// 配置从 GameSettings ScriptableObject 读取
    /// </summary>
    public class TurretHealth : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("游戏设置")]
        [SerializeField] private GameSettings settings;
        
        [Header("组件引用")]
        [SerializeField] private ShieldController shieldController;
        [SerializeField] private SpriteRenderer turretSprite;
        
        [Header("血量设置（如果没有 GameSettings）")]
        [SerializeField] private int defaultMaxHullHP = 3;
        [SerializeField] private float defaultInvincibilityDuration = 1.0f;
        
        [Header("碰撞设置")]
        [Tooltip("小怪质量阈值（低于此值为小怪，撞击后自爆）")]
        [SerializeField] private float smallEnemyMassThreshold = 2.0f;
        
        [Tooltip("大怪反弹力度")]
        [SerializeField] private float bounceForce = 300f;
        
        [Header("视觉设置")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color damagedColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private float blinkInterval = 0.1f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时配置缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int maxHullHP;
        private float invincibilityDuration;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int currentHullHP;
        private bool isInvincible = false;
        private Coroutine invincibilityCoroutine;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public int CurrentHullHP => currentHullHP;
        public int MaxHullHP => maxHullHP;
        public bool IsInvincible => isInvincible || (shieldController != null && shieldController.IsInvincible);
        public bool IsDead => currentHullHP <= 0;
        public float HullPercent => maxHullHP > 0 ? (float)currentHullHP / maxHullHP : 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            LoadConfig();
            
            if (shieldController == null)
            {
                shieldController = GetComponentInChildren<ShieldController>();
            }
        }
        
        private void Start()
        {
            currentHullHP = maxHullHP;
            UpdateVisuals();
            BroadcastHullStatus();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置加载
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void LoadConfig()
        {
            if (settings != null)
            {
                maxHullHP = settings.maxHullHP;
                invincibilityDuration = settings.invincibilityDuration;
            }
            else
            {
                // 使用默认值
                maxHullHP = defaultMaxHullHP;
                invincibilityDuration = defaultInvincibilityDuration;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 受到伤害
        /// </summary>
        /// <returns>是否成功造成伤害</returns>
        public bool TakeDamage(int damage = 1)
        {
            // 检查无敌状态
            if (IsInvincible)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[TurretHealth] 无敌中，伤害无效");
                }
                return false;
            }
            
            // 先由护盾承担伤害
            if (shieldController != null && shieldController.CurrentShieldHP > 0)
            {
                bool shieldDamaged = shieldController.TakeDamage(damage);
                if (shieldDamaged)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"[TurretHealth] 护盾承担伤害");
                    }
                    return true;
                }
            }
            
            // 护盾破了，扣本体血
            currentHullHP = Mathf.Max(0, currentHullHP - damage);
            
            // 播放受伤特效
            PlayDamageEffect();
            
            // 开始无敌
            StartInvincibility();
            
            // 广播状态
            BroadcastHullStatus();
            
            if (showDebugInfo)
            {
                Debug.Log($"[TurretHealth] 本体受伤: {currentHullHP}/{maxHullHP}");
            }
            
            // 检查死亡
            if (currentHullHP <= 0)
            {
                OnDeath();
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
                if (turretSprite != null)
                {
                    turretSprite.color = visible ? damagedColor : new Color(1f, 1f, 1f, 0.3f);
                }
                
                yield return new WaitForSeconds(blinkInterval);
                elapsed += blinkInterval;
            }
            
            isInvincible = false;
            UpdateVisuals();
            
            invincibilityCoroutine = null;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 死亡处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDeath()
        {
            if (showDebugInfo)
            {
                Debug.Log("[TurretHealth] 光棱塔被摧毁！");
            }
            
            // 触发游戏失败
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Defeat();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 视觉效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateVisuals()
        {
            if (turretSprite == null) return;
            
            if (currentHullHP <= 1)
            {
                turretSprite.color = damagedColor;
            }
            else
            {
                turretSprite.color = normalColor;
            }
        }
        
        private void PlayDamageEffect()
        {
            // 使用 VFXPoolManager.Play 方法
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.Play(VFXType.TowerDamage, transform.position);
            }
            
            // TODO: 屏幕震动
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件广播
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void BroadcastHullStatus()
        {
            GameEvents.TriggerHullHPChanged(currentHullHP, maxHullHP);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 判断是否为小型敌人（根据质量）
        /// </summary>
        public bool IsSmallEnemy(float mass)
        {
            return mass < smallEnemyMassThreshold;
        }
        
        /// <summary>
        /// 获取反弹力度
        /// </summary>
        public float GetBounceForce()
        {
            return bounceForce;
        }
        
        /// <summary>
        /// 恢复生命值（消耗品使用）
        /// </summary>
        public void RestoreHealth(int amount = 1)
        {
            currentHullHP = Mathf.Min(currentHullHP + amount, maxHullHP);
            UpdateVisuals();
            BroadcastHullStatus();
            
            if (showDebugInfo)
            {
                Debug.Log($"[TurretHealth] 恢复生命 +{amount}: {currentHullHP}/{maxHullHP}");
            }
        }
        
        /// <summary>
        /// 重置（新游戏）
        /// </summary>
        public void ResetHealth()
        {
            currentHullHP = maxHullHP;
            isInvincible = false;
            
            if (invincibilityCoroutine != null)
            {
                StopCoroutine(invincibilityCoroutine);
                invincibilityCoroutine = null;
            }
            
            UpdateVisuals();
            BroadcastHullStatus();
        }
    }
}