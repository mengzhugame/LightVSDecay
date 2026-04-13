// ============================================================
// TurretHealth.cs (重构版 v2.0)
// 文件位置: Assets/Scripts/Logic/Player/TurretHealth.cs
// 用途：塔本体生命值控制
// 改动：
//   - 删除无敌时间功能
//   - 支持大数值伤害（1000血量）
//   - 添加百分比恢复接口
//   - 添加低血量警告事件
// ============================================================

using UnityEngine;
using System.Collections;
using LightVsDecay.Audio;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Statistics;

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
        [SerializeField] private int defaultMaxHullHP = 1000;
        
        [Header("碰撞设置")]
        [Tooltip("小怪质量阈值（低于此值为小怪，撞击后自爆）")]
        [SerializeField] private float smallEnemyMassThreshold = 2.0f;
        
        [Tooltip("大怪反弹力度")]
        [SerializeField] private float bounceForce = 300f;
        
        [Header("视觉设置")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color dangerColor = new Color(1f, 0.5f, 0.5f, 1f);
        [SerializeField] private Color criticalColor = new Color(1f, 0.3f, 0.3f, 1f);
        
        [Header("低血量警告阈值")]
        [Tooltip("低血量警告阈值（百分比）")]
        [SerializeField] private float lowHealthThreshold = 0.2f;
        [Header("闪白效果")]
        [Tooltip("闪白持续时间")]
        [SerializeField] private float hitFlashDuration = 0.15f;
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时配置缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int maxHullHP;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int currentHullHP;
        private bool isLowHealth = false; // 追踪低血量状态
        private Coroutine hitFlashCoroutine;
        private Material turretMaterial;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public int CurrentHullHP => currentHullHP;
        public int MaxHullHP => maxHullHP;
        public bool IsDead => currentHullHP <= 0;
        public float HullPercent => maxHullHP > 0 ? (float)currentHullHP / maxHullHP : 0f;
        public bool IsLowHealth => HullPercent < lowHealthThreshold;
        
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
            if (turretSprite != null)
            {
                turretMaterial = turretSprite.material;
            }
        }
        
        private void Start()
        {
            currentHullHP = maxHullHP;
            isLowHealth = false;
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
            }
            else
            {
                maxHullHP = defaultMaxHullHP;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 受到伤害（小怪碰撞）
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <returns>是否成功造成伤害</returns>
        public bool TakeDamage(int damage)
        {
            if (damage <= 0) return false;
            // 【新增】大招无敌检查
            if (OverloadManager.Instance != null && OverloadManager.Instance.IsActive)
            {
                return false; // 无敌状态，不受伤害
            }
            // 先由护盾承担伤害
            if (shieldController != null && shieldController.CurrentShieldHP > 0)
            {
                int overflow = shieldController.TakeDamage(damage);

                if (overflow <= 0)
                {
                    // 护盾完全吸收
                    BattleStatistics.Instance?.RecordShieldDamageFromMobs(damage);
                    if (showDebugInfo)
                    {
                        GameLogger.Log($"[TurretHealth] 护盾完全吸收伤害: {damage}");
                    }
                    return true;
                }

                // 护盾部分吸收，记录已吸收量
                BattleStatistics.Instance?.RecordShieldDamageFromMobs(damage - overflow);
                // 溢出伤害扣本体
                damage = overflow;
            }
            
            // 扣本体血
            ApplyDamageToHull(damage);
            TriggerHitFlash();
            return true;
        }
        
        /// <summary>
        /// 受到BOSS伤害（大数值伤害）
        /// </summary>
        /// <param name="damage">伤害值</param>
        public void TakeBossDamage(int damage)
        {
            if (damage <= 0) return;
            // 【新增】大招无敌检查
            if (OverloadManager.Instance != null && OverloadManager.Instance.IsActive)
            {
                return; // 无敌状态，不受伤害
            }
            // 先由护盾承担伤害
            if (shieldController != null && shieldController.CurrentShieldHP > 0)
            {
                int overflow = shieldController.TakeBossDamage(damage);
                
                if (overflow <= 0)
                {
                    if (showDebugInfo)
                    {
                        GameLogger.Log($"[TurretHealth] 护盾完全吸收BOSS伤害: {damage}");
                    }
                    return;
                }
                
                damage = overflow;
            }
            
            // 溢出伤害扣本体
            ApplyDamageToHull(damage);
            TriggerHitFlash();
        }
        
        /// <summary>
        /// 对本体造成伤害（内部方法）
        /// </summary>
        private void ApplyDamageToHull(int damage)
        {
            int oldHP = currentHullHP;
            currentHullHP = Mathf.Max(0, currentHullHP - damage);
            int actualDamage = oldHP - currentHullHP;
            
            // 播放受伤特效
            PlayDamageEffect();
            // 【新增】播放光棱塔受击音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayTowerHit();
            }
            // 触发玩家受击飘字事件
            GameEvents.TriggerPlayerHealthDamaged(actualDamage, transform.position);
            
            // 广播状态
            BroadcastHullStatus();
            
            // 检查低血量状态
            CheckLowHealthStatus();
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[TurretHealth] 本体受伤: -{actualDamage}, 剩余: {currentHullHP}/{maxHullHP}");
            }
            
            // 更新视觉
            UpdateVisuals();
            
            // 检查死亡
            if (currentHullHP <= 0)
            {
                OnDeath();
            }
        }
        
        /// <summary>
        /// 检查并触发低血量状态
        /// </summary>
        private void CheckLowHealthStatus()
        {
            bool newLowHealthState = HullPercent < lowHealthThreshold;
            
            if (newLowHealthState && !isLowHealth)
            {
                // 刚进入低血量状态
                isLowHealth = true;
                GameEvents.TriggerLowHealthStart();
                
                if (showDebugInfo)
                {
                    GameLogger.Log("[TurretHealth] ⚠️ 进入低血量状态！");
                }
            }
            else if (!newLowHealthState && isLowHealth)
            {
                // 脱离低血量状态
                isLowHealth = false;
                GameEvents.TriggerLowHealthEnd();
                
                if (showDebugInfo)
                {
                    GameLogger.Log("[TurretHealth] ✓ 脱离低血量状态");
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 死亡处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDeath()
        {
            if (showDebugInfo)
            {
                GameLogger.Log("[TurretHealth] 💀 塔被摧毁！游戏结束！");
            }
            
            // 触发游戏失败事件
            GameEvents.TriggerPlayerDeathRequested();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 视觉效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateVisuals()
        {
            if (turretSprite == null) return;
            
            float percent = HullPercent;
            
            if (percent < 0.1f)
            {
                // 极度危险 (<10%)
                turretSprite.color = criticalColor;
            }
            else if (percent < 0.3f)
            {
                // 危险 (<30%)
                turretSprite.color = dangerColor;
            }
            else
            {
                // 正常
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
        }
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
            if (turretMaterial == null) yield break;
    
            // 设置闪白为1（全白）
            turretMaterial.SetFloat(GameConstants.ShaderProperties.HitFlash, 1f);
    
            float elapsed = 0f;
            while (elapsed < hitFlashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / hitFlashDuration;
        
                // 从1渐变到0
                float flashValue = Mathf.Lerp(1f, 0f, t);
                turretMaterial.SetFloat(GameConstants.ShaderProperties.HitFlash, flashValue);
        
                yield return null;
            }
    
            // 确保最终值为0
            turretMaterial.SetFloat(GameConstants.ShaderProperties.HitFlash, 0f);
            hitFlashCoroutine = null;
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
        /// 恢复生命值（固定数值）
        /// </summary>
        public void RestoreHealth(int amount)
        {
            if (amount <= 0) return;
            
            int oldHP = currentHullHP;
            currentHullHP = Mathf.Min(currentHullHP + amount, maxHullHP);
            int actualRestore = currentHullHP - oldHP;
            
            UpdateVisuals();
            BroadcastHullStatus();
            
            // 检查是否脱离低血量状态
            CheckLowHealthStatus();
            
            // 触发恢复飘字事件
            if (actualRestore > 0)
            {
                GameEvents.TriggerPlayerHealthRestored(actualRestore, transform.position);
            }
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[TurretHealth] 恢复生命 +{actualRestore}: {currentHullHP}/{maxHullHP}");
            }
        }
        /// <summary>
        /// 直接扣除本体血量（忽略护盾，用于契约代价等）
        /// </summary>
        /// <param name="amount">扣除的血量</param>
        /// <param name="allowDeath">是否允许扣死（false则保底1血）</param>
        public void DirectDamageToHull(int amount, bool allowDeath = false)
        {
            if (amount <= 0) return;
    
            // 保底检查
            if (!allowDeath && currentHullHP - amount < 1)
            {
                amount = currentHullHP - 1;
            }
    
            if (amount <= 0) return;
    
            int oldHP = currentHullHP;
            currentHullHP = Mathf.Max(0, currentHullHP - amount);
            int actualDamage = oldHP - currentHullHP;
    
            // 广播状态
            BroadcastHullStatus();
    
            // 检查低血量状态
            CheckLowHealthStatus();
    
            if (showDebugInfo)
            {
                GameLogger.Log($"[TurretHealth] 直接扣血: -{actualDamage}, 剩余: {currentHullHP}/{maxHullHP}");
            }
    
            // 更新视觉
            UpdateVisuals();
    
            // 检查死亡（如果允许）
            if (allowDeath && currentHullHP <= 0)
            {
                OnDeath();
            }
        }
        /// <summary>
        /// 恢复生命值（百分比）
        /// </summary>
        /// <param name="percent">恢复百分比（0-1），例如0.1表示10%</param>
        public void RestoreHealthPercent(float percent)
        {
            int amount = Mathf.RoundToInt(maxHullHP * percent);
            RestoreHealth(amount);
        }
        
        /// <summary>
        /// 重置（新游戏）
        /// </summary>
        public void ResetHealth()
        {
            currentHullHP = maxHullHP;
            isLowHealth = false;
            
            UpdateVisuals();
            BroadcastHullStatus();
        }
        
        /// <summary>
        /// 设置最大血量（用于养成系统）
        /// </summary>
        public void SetMaxHullHP(int newMax)
        {
            float percent = HullPercent;
            maxHullHP = newMax;
            currentHullHP = Mathf.RoundToInt(maxHullHP * percent);
            
            UpdateVisuals();
            BroadcastHullStatus();
            CheckLowHealthStatus();
        }
        /// <summary>
        /// 直接扣除血量（不经过护盾，用于契约代价等）
        /// </summary>
        /// <param name="damage">扣除数值</param>
        /// <param name="minRemaining">保底剩余血量（默认1）</param>
        /// <returns>实际扣除的血量</returns>
        public int TakeDirectDamage(int damage, int minRemaining = 1)
        {
            if (damage <= 0 || currentHullHP <= minRemaining) return 0;
    
            // 计算实际扣除量（保底剩余 minRemaining 点血）
            int maxLoss = currentHullHP - minRemaining;
            int actualLoss = Mathf.Min(damage, maxLoss);
    
            if (actualLoss <= 0) return 0;
    
            // 直接扣血，不经过护盾
            currentHullHP -= actualLoss;
    
            // 播放受伤特效
            PlayDamageEffect();
            TriggerHitFlash();
    
            // 触发玩家受击飘字事件
            GameEvents.TriggerPlayerHealthDamaged(actualLoss, transform.position);
    
            // 广播状态
            BroadcastHullStatus();
    
            // 检查低血量状态
            CheckLowHealthStatus();
    
            // 更新视觉
            UpdateVisuals();
    
            if (showDebugInfo)
            {
                GameLogger.Log($"[TurretHealth] 直接扣血: -{actualLoss}, 剩余: {currentHullHP}/{maxHullHP}");
            }
    
            // 检查死亡（理论上保底1血不会触发）
            if (currentHullHP <= 0)
            {
                OnDeath();
            }
    
            return actualLoss;
        }
    }
}
