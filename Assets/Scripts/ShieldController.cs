using UnityEngine;
using System.Collections;
using LightVsDecay.Core.Pool;
using LightVsDecay.Enemy;

namespace LightVsDecay.Shield
{
    /// <summary>
    /// 护盾状态枚举
    /// </summary>
    public enum ShieldState
    {
        Active,         // 正常激活
        Breaking,       // 破碎中
        Broken,         // 已破碎
        Recovering      // 恢复中
    }
    
    /// <summary>
    /// 冲击波类型
    /// </summary>
    public enum ShockwaveType
    {
        OnHit,      // 受击时：只弹开
        OnRecover   // 恢复时：杀死小怪 + 弹开大怪
    }
    
    /// <summary>
    /// 能量护盾控制器
    /// 适配 EnergyShield2D / EnergyShield2D_URP Shader
    /// </summary>
    public class ShieldController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("护盾属性")]
        [SerializeField] private int maxShieldHP = 3;
        [SerializeField] private float recoveryTime = 12f;
        [SerializeField] private float invincibilityDuration = 1.0f;
        
        [Header("视觉组件")]
        [SerializeField] private SpriteRenderer shieldRenderer;
        
        [Header("颜色配置 - 满血 (3 HP)")]
        [SerializeField] private Color fullHP_BaseColor = new Color(0f, 1f, 1f, 0.3f);
        [SerializeField] private Color fullHP_EdgeColor = new Color(0f, 1f, 1f, 1f);
        
        [Header("颜色配置 - 中血 (2 HP)")]
        [SerializeField] private Color midHP_BaseColor = new Color(0.4f, 1f, 0.8f, 0.3f);
        [SerializeField] private Color midHP_EdgeColor = new Color(0.4f, 1f, 0.8f, 1f);
        
        [Header("颜色配置 - 低血 (1 HP)")]
        [SerializeField] private Color lowHP_BaseColor = new Color(1f, 0.7f, 0.4f, 0.3f);
        [SerializeField] private Color lowHP_EdgeColor = new Color(1f, 0.7f, 0.4f, 1f);
        
        [Header("动画设置")]
        [SerializeField] private float hitFlashDuration = 0.15f;
        [SerializeField] private float breakFadeDuration = 0.5f;
        [SerializeField] private float recoverFadeDuration = 0.8f;
        
        [Header("冲击波设置")]
        [SerializeField] private GameObject shockwavePrefab;
        [SerializeField] private float shockwaveMaxRadius = 5f;
        [SerializeField] private float shockwaveDuration = 0.4f;
        [SerializeField] private float shockwaveForce = 500f;
        
        [Header("大小怪判定")]
        [Tooltip("质量小于此值判定为小怪")]
        [SerializeField] private float smallEnemyMassThreshold = 2.0f;
        
        [Header("碰撞设置")]
        [SerializeField] private CircleCollider2D shieldCollider;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public event System.Action<int> OnShieldHit;
        public event System.Action OnShieldBroken;
        public event System.Action OnShieldRecovered;
        public event System.Action<int, int> OnShieldHPChanged;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Shader 属性 ID 缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly int EdgeColorID = Shader.PropertyToID("_EdgeColor");
        private static readonly int HitFlashID = Shader.PropertyToID("_HitFlash");
        private static readonly int ShieldAlphaID = Shader.PropertyToID("_ShieldAlpha");
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int currentShieldHP;
        private ShieldState currentState = ShieldState.Active;
        private bool isInvincible = false;
        private float lastHitTime;
        private float recoveryTimer;
        
        private Material shieldMaterial;
        private Coroutine hitFlashCoroutine;
        private Coroutine fadeCoroutine;
        private Coroutine invincibilityCoroutine;
        
        // 缓存 Enemy Layer
        private int enemyLayerMask;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public int CurrentHP => currentShieldHP;
        public int MaxHP => maxShieldHP;
        public ShieldState State => currentState;
        public bool IsActive => currentState == ShieldState.Active;
        public bool IsInvincible => isInvincible;
        public float SmallEnemyMassThreshold => smallEnemyMassThreshold;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            if (shieldRenderer == null)
            {
                shieldRenderer = GetComponent<SpriteRenderer>();
            }
            
            if (shieldCollider == null)
            {
                shieldCollider = GetComponent<CircleCollider2D>();
            }
            
            // 创建材质实例
            if (shieldRenderer != null)
            {
                shieldMaterial = shieldRenderer.material;
            }
            
            // 缓存 Layer
            enemyLayerMask = LayerMask.GetMask("Enemy");
            
            currentShieldHP = maxShieldHP;
            currentState = ShieldState.Active;
        }
        
        private void Start()
        {
            UpdateShieldVisuals();
            SetShieldAlpha(1f);
            SetHitFlash(0f);
        }
        
        private void Update()
        {
            // 恢复计时
            if (currentState == ShieldState.Broken)
            {
                recoveryTimer += Time.deltaTime;
                
                if (recoveryTimer >= recoveryTime)
                {
                    StartRecovery();
                }
            }
        }
        
        private void OnDestroy()
        {
            if (shieldMaterial != null)
            {
                Destroy(shieldMaterial);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 护盾受到伤害（由 EnemyBlob 调用）
        /// </summary>
        /// <returns>是否成功造成伤害</returns>
        public bool TakeDamage(int damage = 1)
        {
            if (currentState != ShieldState.Active || isInvincible)
            {
                return false;
            }
            
            currentShieldHP = Mathf.Max(0, currentShieldHP - damage);
            lastHitTime = Time.time;
            
            // 触发无敌帧
            StartInvincibility();
            
            // 触发闪烁效果
            TriggerHitFlash();
            
            // 更新颜色
            UpdateShieldVisuals();
            
            // 播放受击冲击波（只弹开）
            SpawnShockwave(ShockwaveType.OnHit);
            
            // 触发事件
            OnShieldHit?.Invoke(currentShieldHP);
            OnShieldHPChanged?.Invoke(currentShieldHP, maxShieldHP);
            
            if (showDebugInfo)
            {
                Debug.Log($"[Shield] 受击! HP: {currentShieldHP}/{maxShieldHP}");
            }
            
            // 检查是否破碎
            if (currentShieldHP <= 0)
            {
                StartBreaking();
            }
            
            return true;
        }
        
        /// <summary>
        /// 触发撞击效果（仅视觉，不扣血）
        /// </summary>
        public void TriggerHitEffect()
        {
            if (currentState == ShieldState.Active)
            {
                TriggerHitFlash();
            }
        }
        
        /// <summary>
        /// 强制破碎
        /// </summary>
        public void ForceBreak()
        {
            if (currentState == ShieldState.Active)
            {
                currentShieldHP = 0;
                StartBreaking();
            }
        }
        
        /// <summary>
        /// 强制恢复
        /// </summary>
        public void ForceRecover()
        {
            if (currentState == ShieldState.Broken)
            {
                StartRecovery();
            }
        }
        
        /// <summary>
        /// 重置护盾
        /// </summary>
        public void Reset()
        {
            StopAllCoroutines();
            
            currentShieldHP = maxShieldHP;
            currentState = ShieldState.Active;
            isInvincible = false;
            recoveryTimer = 0f;
            
            SetShieldAlpha(1f);
            SetHitFlash(0f);
            UpdateShieldVisuals();
            
            if (shieldCollider != null)
            {
                shieldCollider.enabled = true;
            }
        }
        
        /// <summary>
        /// 判断质量是否为小怪
        /// </summary>
        public bool IsSmallEnemy(float mass)
        {
            return mass < smallEnemyMassThreshold;
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
                Debug.Log("[Shield] 无敌帧开始");
            }
            
            yield return new WaitForSeconds(invincibilityDuration);
            
            isInvincible = false;
            
            if (showDebugInfo)
            {
                Debug.Log("[Shield] 无敌帧结束");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态转换
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void StartBreaking()
        {
            currentState = ShieldState.Breaking;
            
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            
            fadeCoroutine = StartCoroutine(BreakingCoroutine());
        }
        
        private void StartRecovery()
        {
            currentState = ShieldState.Recovering;
            recoveryTimer = 0f;
            currentShieldHP = maxShieldHP;
            
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            
            fadeCoroutine = StartCoroutine(RecoveryCoroutine());
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 动画协程
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator BreakingCoroutine()
        {
            float elapsed = 0f;
            
            while (elapsed < breakFadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / breakFadeDuration);
                SetShieldAlpha(alpha);
                yield return null;
            }
            
            SetShieldAlpha(0f);
            
            if (shieldCollider != null)
            {
                shieldCollider.enabled = false;
            }
            
            currentState = ShieldState.Broken;
            recoveryTimer = 0f;
            
            OnShieldBroken?.Invoke();
            
            if (showDebugInfo)
            {
                Debug.Log("[Shield] 破碎!");
            }
        }
        
        private IEnumerator RecoveryCoroutine()
        {
            // 先播放恢复冲击波（杀死小怪 + 弹开大怪）
            SpawnShockwave(ShockwaveType.OnRecover);
            
            // 更新颜色到满血状态
            UpdateShieldVisuals();
            
            // 启用碰撞
            if (shieldCollider != null)
            {
                shieldCollider.enabled = true;
            }
            
            // 渐显动画
            float elapsed = 0f;
            
            while (elapsed < recoverFadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(0f, 1f, elapsed / recoverFadeDuration);
                SetShieldAlpha(alpha);
                yield return null;
            }
            
            SetShieldAlpha(1f);
            currentState = ShieldState.Active;
            
            OnShieldRecovered?.Invoke();
            OnShieldHPChanged?.Invoke(currentShieldHP, maxShieldHP);
            
            if (showDebugInfo)
            {
                Debug.Log("[Shield] 恢复!");
            }
        }
        
        private IEnumerator HitFlashCoroutine()
        {
            SetHitFlash(1f);
            
            float elapsed = 0f;
            while (elapsed < hitFlashDuration)
            {
                elapsed += Time.deltaTime;
                float flash = Mathf.Lerp(1f, 0f, elapsed / hitFlashDuration);
                SetHitFlash(flash);
                yield return null;
            }
            
            SetHitFlash(0f);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 冲击波系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 生成冲击波
        /// </summary>
        /// <param name="type">冲击波类型</param>
        private void SpawnShockwave(ShockwaveType type)
        {
            if (shockwavePrefab != null)
            {
                GameObject shockwave = Instantiate(shockwavePrefab, transform.position, Quaternion.identity);
                StartCoroutine(AnimateShockwave(shockwave, type));
            }
            else
            {
                // 没有预制体，直接执行效果
                ApplyShockwaveEffect(type);
            }
        }
        
        private IEnumerator AnimateShockwave(GameObject shockwave, ShockwaveType type)
        {
            SpriteRenderer sr = shockwave.GetComponent<SpriteRenderer>();
            float elapsed = 0f;
            bool effectApplied = false;
            
            while (elapsed < shockwaveDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shockwaveDuration;
                
                // 从小到大扩展
                float radius = Mathf.Lerp(0f, shockwaveMaxRadius, t);
                shockwave.transform.localScale = Vector3.one * radius * 2f;
                
                // 渐隐
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    sr.color = c;
                }
                
                // 在50%时执行效果
                if (!effectApplied && t >= 0.5f)
                {
                    ApplyShockwaveEffect(type);
                    effectApplied = true;
                }
                
                yield return null;
            }
            
            Destroy(shockwave);
        }
        
        /// <summary>
        /// 应用冲击波效果
        /// </summary>
        /// <param name="type">冲击波类型</param>
        private void ApplyShockwaveEffect(ShockwaveType type)
        {
            Collider2D[] enemies = Physics2D.OverlapCircleAll(
                transform.position, 
                shockwaveMaxRadius, 
                enemyLayerMask
            );
            
            foreach (var enemyCollider in enemies)
            {
                EnemyBlob enemy = enemyCollider.GetComponent<EnemyBlob>();
                Rigidbody2D rb = enemyCollider.GetComponent<Rigidbody2D>();
                
                if (enemy == null || rb == null) continue;
                
                // 计算方向和距离衰减
                Vector2 direction = (enemyCollider.transform.position - transform.position).normalized;
                float distance = Vector2.Distance(transform.position, enemyCollider.transform.position);
                float falloff = 1f - (distance / shockwaveMaxRadius);
                falloff = Mathf.Max(0.3f, falloff);
                
                bool isSmall = IsSmallEnemy(rb.mass);
                
                if (type == ShockwaveType.OnRecover)
                {
                    // 恢复冲击波：杀死小怪，弹开大怪
                    if (isSmall)
                    {
                        // 小怪直接杀死
                        enemy.KillByShockwave();
                        
                        if (showDebugInfo)
                        {
                            Debug.Log($"[Shield] 恢复冲击波杀死小怪: {enemy.name}");
                        }
                    }
                    else
                    {
                        // 大怪弹开
                        rb.AddForce(direction * shockwaveForce * falloff, ForceMode2D.Impulse);
                        
                        if (showDebugInfo)
                        {
                            Debug.Log($"[Shield] 恢复冲击波弹开大怪: {enemy.name}");
                        }
                    }
                }
                else // ShockwaveType.OnHit
                {
                    // 受击冲击波：只弹开所有怪
                    rb.AddForce(direction * shockwaveForce * falloff, ForceMode2D.Impulse);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"[Shield] 受击冲击波弹开: {enemy.name}");
                    }
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 视觉效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateShieldVisuals()
        {
            if (shieldMaterial == null) return;
            
            Color baseColor, edgeColor;
            
            switch (currentShieldHP)
            {
                case 3:
                default:
                    baseColor = fullHP_BaseColor;
                    edgeColor = fullHP_EdgeColor;
                    break;
                case 2:
                    baseColor = midHP_BaseColor;
                    edgeColor = midHP_EdgeColor;
                    break;
                case 1:
                    baseColor = lowHP_BaseColor;
                    edgeColor = lowHP_EdgeColor;
                    break;
                case 0:
                    baseColor = lowHP_BaseColor;
                    edgeColor = lowHP_EdgeColor;
                    break;
            }
            
            shieldMaterial.SetColor(BaseColorID, baseColor);
            shieldMaterial.SetColor(EdgeColorID, edgeColor);
        }
        
        private void SetShieldAlpha(float alpha)
        {
            if (shieldMaterial != null)
            {
                shieldMaterial.SetFloat(ShieldAlphaID, alpha);
            }
        }
        
        private void SetHitFlash(float flash)
        {
            if (shieldMaterial != null)
            {
                shieldMaterial.SetFloat(HitFlashID, flash);
            }
        }
        
        private void TriggerHitFlash()
        {
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
            }
            hitFlashCoroutine = StartCoroutine(HitFlashCoroutine());
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, Screen.height - 180, 200, 170));
            GUILayout.Label("=== Shield Debug ===");
            GUILayout.Label($"HP: {currentShieldHP}/{maxShieldHP}");
            GUILayout.Label($"State: {currentState}");
            GUILayout.Label($"Invincible: {isInvincible}");
            
            if (currentState == ShieldState.Broken)
            {
                GUILayout.Label($"Recovery: {recoveryTimer:F1}/{recoveryTime}s");
            }
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("Take Damage"))
            {
                TakeDamage(1);
            }
            
            if (GUILayout.Button("Trigger Flash"))
            {
                TriggerHitEffect();
            }
            
            if (currentState == ShieldState.Broken && GUILayout.Button("Force Recover"))
            {
                ForceRecover();
            }
            
            if (GUILayout.Button("Reset"))
            {
                Reset();
            }
            
            GUILayout.EndArea();
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, shockwaveMaxRadius);
        }
#endif
    }
}