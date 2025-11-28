using UnityEngine;
using System.Collections;
using LightVsDecay.Shield;

namespace LightVsDecay
{
    /// <summary>
    /// 光棱塔本体血量管理
    /// 与 ShieldController 配合，实现 3+3 双层血条系统
    /// </summary>
    public class TurretHealth : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("本体属性")]
        [SerializeField] private int maxHullHP = 3;
        [SerializeField] private float invincibilityDuration = 1.0f;
        
        [Header("大小怪判定")]
        [Tooltip("质量小于此值判定为小怪")]
        [SerializeField] private float smallEnemyMassThreshold = 2.0f;
        
        [Header("大怪弹开设置")]
        [SerializeField] private float bounceForce = 300f;
        
        [Header("视觉反馈")]
        [SerializeField] private SpriteRenderer turretRenderer;
        [SerializeField] private float flashDuration = 0.1f;
        [SerializeField] private int flashCount = 5;
        
        [Header("关联组件")]
        [Tooltip("护盾控制器（如果为空则自动查找子物体）")]
        [SerializeField] private ShieldController shieldController;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>本体受击时触发（参数：剩余血量）</summary>
        public event System.Action<int> OnHullHit;
        
        /// <summary>本体血量变化时触发（参数：当前血量，最大血量）</summary>
        public event System.Action<int, int> OnHullHPChanged;
        
        /// <summary>游戏结束时触发（本体血量归零）</summary>
        public event System.Action OnGameOver;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int currentHullHP;
        private bool isInvincible = false;
        private bool isDead = false;
        
        private Coroutine invincibilityCoroutine;
        private Coroutine flashCoroutine;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public int CurrentHP => currentHullHP;
        public int MaxHP => maxHullHP;
        public bool IsInvincible => isInvincible;
        public bool IsDead => isDead;
        
        /// <summary>护盾血量（便捷访问）</summary>
        public int ShieldHP => shieldController != null ? shieldController.CurrentHP : 0;
        
        /// <summary>护盾是否激活</summary>
        public bool IsShieldActive => shieldController != null && shieldController.IsActive;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            currentHullHP = maxHullHP;
            
            // 自动查找护盾
            if (shieldController == null)
            {
                shieldController = GetComponentInChildren<ShieldController>();
            }
            
            // 自动查找渲染器
            if (turretRenderer == null)
            {
                turretRenderer = GetComponent<SpriteRenderer>();
            }
        }
        
        private void Start()
        {
            // 订阅护盾事件
            if (shieldController != null)
            {
                shieldController.OnShieldBroken += OnShieldBroken;
                shieldController.OnShieldRecovered += OnShieldRecovered;
                
                if (showDebugInfo)
                {
                    Debug.Log("[TurretHealth] 已连接护盾控制器");
                }
            }
            else
            {
                Debug.LogWarning("[TurretHealth] 未找到护盾控制器！");
            }
        }
        
        private void OnDestroy()
        {
            // 取消订阅
            if (shieldController != null)
            {
                shieldController.OnShieldBroken -= OnShieldBroken;
                shieldController.OnShieldRecovered -= OnShieldRecovered;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 本体受到伤害（由 EnemyBlob 调用）
        /// </summary>
        /// <returns>是否成功造成伤害</returns>
        public bool TakeDamage(int damage = 1)
        {
            if (isDead || isInvincible)
            {
                return false;
            }
            
            currentHullHP = Mathf.Max(0, currentHullHP - damage);
            
            // 触发无敌帧
            StartInvincibility();
            
            // 触发闪烁效果
            TriggerFlash();
            
            // 触发事件
            OnHullHit?.Invoke(currentHullHP);
            OnHullHPChanged?.Invoke(currentHullHP, maxHullHP);
            
            if (showDebugInfo)
            {
                Debug.Log($"[TurretHealth] 本体受击! HP: {currentHullHP}/{maxHullHP}");
            }
            
            // 检查死亡
            if (currentHullHP <= 0)
            {
                Die();
            }
            
            return true;
        }
        
        /// <summary>
        /// 判断质量是否为小怪
        /// </summary>
        public bool IsSmallEnemy(float mass)
        {
            return mass < smallEnemyMassThreshold;
        }
        
        /// <summary>
        /// 获取弹开力度
        /// </summary>
        public float GetBounceForce()
        {
            return bounceForce;
        }
        
        /// <summary>
        /// 重置塔（用于重新开始游戏）
        /// </summary>
        public void Reset()
        {
            StopAllCoroutines();
            
            currentHullHP = maxHullHP;
            isInvincible = false;
            isDead = false;
            
            // 重置护盾
            if (shieldController != null)
            {
                shieldController.Reset();
            }
            
            // 重置视觉
            if (turretRenderer != null)
            {
                turretRenderer.enabled = true;
                Color c = turretRenderer.color;
                c.a = 1f;
                turretRenderer.color = c;
            }
            
            OnHullHPChanged?.Invoke(currentHullHP, maxHullHP);
            
            if (showDebugInfo)
            {
                Debug.Log("[TurretHealth] 已重置");
            }
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
            
            if (showDebugInfo)
            {
                Debug.Log("[TurretHealth] 无敌帧开始");
            }
            
            yield return new WaitForSeconds(invincibilityDuration);
            
            isInvincible = false;
            
            if (showDebugInfo)
            {
                Debug.Log("[TurretHealth] 无敌帧结束");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 视觉效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void TriggerFlash()
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            flashCoroutine = StartCoroutine(FlashCoroutine());
        }
        
        private IEnumerator FlashCoroutine()
        {
            if (turretRenderer == null) yield break;
            
            for (int i = 0; i < flashCount; i++)
            {
                // 隐藏
                turretRenderer.enabled = false;
                yield return new WaitForSeconds(flashDuration);
                
                // 显示
                turretRenderer.enabled = true;
                yield return new WaitForSeconds(flashDuration);
            }
            
            // 确保最终显示
            turretRenderer.enabled = true;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 死亡逻辑
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Die()
        {
            if (isDead) return;
            isDead = true;
            
            if (showDebugInfo)
            {
                Debug.Log("[TurretHealth] 游戏结束！");
            }
            
            OnGameOver?.Invoke();
            
            // TODO: 播放死亡动画、暂停游戏等
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 护盾事件处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnShieldBroken()
        {
            if (showDebugInfo)
            {
                Debug.Log("[TurretHealth] 护盾已破碎，本体暴露！");
            }
        }
        
        private void OnShieldRecovered()
        {
            if (showDebugInfo)
            {
                Debug.Log("[TurretHealth] 护盾已恢复！");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(220, Screen.height - 180, 200, 170));
            GUILayout.Label("=== Turret Health ===");
            GUILayout.Label($"Hull HP: {currentHullHP}/{maxHullHP}");
            GUILayout.Label($"Shield HP: {ShieldHP}/{(shieldController != null ? shieldController.MaxHP : 0)}");
            GUILayout.Label($"Invincible: {isInvincible}");
            GUILayout.Label($"Shield Active: {IsShieldActive}");
            GUILayout.Label($"Dead: {isDead}");
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("Take Hull Damage"))
            {
                TakeDamage(1);
            }
            
            if (GUILayout.Button("Reset All"))
            {
                Reset();
            }
            
            GUILayout.EndArea();
        }
#endif
    }
}