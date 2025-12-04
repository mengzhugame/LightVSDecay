using UnityEngine;
using System.Collections;
using LightVsDecay.Core.Pool;
using LightVsDecay.Logic.Enemy;

namespace LightVsDecay.Logic.Player
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
    /// 【修复】冲击波改为子物体引用，增大力度，正确透明消失
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
        [Tooltip("冲击波子物体（不是预制体，是场景中的子物体）")]
        [SerializeField] private Transform shockwaveTransform;
        [SerializeField] private SpriteRenderer shockwaveRenderer;
        [SerializeField] private float shockwaveMaxRadius = 5f;
        [SerializeField] private float shockwaveDuration = 0.4f;
        [Tooltip("冲击波力度（增大到2000-5000）")]
        [SerializeField] private float shockwaveForce = 3000f;
        
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
        private Coroutine shockwaveCoroutine;
        
        // 缓存 Enemy Layer
        private int enemyLayerMask;
        
        // 冲击波初始缩放（用于重置）
        private Vector3 shockwaveOriginalScale;
        private Color shockwaveOriginalColor;
        
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
            
            // 初始化冲击波
            InitializeShockwave();
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
        // 冲击波初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void InitializeShockwave()
        {
            // 自动查找子物体
            if (shockwaveTransform == null)
            {
                shockwaveTransform = transform.Find("Shield_Shockwave");
            }
            
            if (shockwaveTransform != null)
            {
                if (shockwaveRenderer == null)
                {
                    shockwaveRenderer = shockwaveTransform.GetComponent<SpriteRenderer>();
                }
                
                // 记录初始状态
                shockwaveOriginalScale = shockwaveTransform.localScale;
                if (shockwaveRenderer != null)
                {
                    shockwaveOriginalColor = shockwaveRenderer.color;
                }
                
                // 初始隐藏
                shockwaveTransform.gameObject.SetActive(false);
                
                if (showDebugInfo)
                {
                    Debug.Log("[Shield] 冲击波子物体初始化完成");
                }
            }
            else
            {
                Debug.LogWarning("[Shield] 未找到冲击波子物体 Shield_Shockwave！");
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
                if (showDebugInfo)
                {
                    Debug.Log($"[Shield] 伤害无效 - State:{currentState}, Invincible:{isInvincible}");
                }
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
            PlayShockwave(ShockwaveType.OnHit);
            
            // 触发事件
            OnShieldHit?.Invoke(currentShieldHP);
            OnShieldHPChanged?.Invoke(currentShieldHP, maxShieldHP);
            // 触发全局事件，通知UI更新
            Core.GameEvents.TriggerShieldHPChanged(currentShieldHP, maxShieldHP);
            
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
            
            // 重置冲击波
            ResetShockwave();
            Core.GameEvents.TriggerShieldHPChanged(currentShieldHP, maxShieldHP);
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
            PlayShockwave(ShockwaveType.OnRecover);
            
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
        // 冲击波系统（修复版：使用子物体）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放冲击波（使用子物体）
        /// </summary>
        private void PlayShockwave(ShockwaveType type)
        {
            if (shockwaveTransform == null)
            {
                // 没有冲击波子物体，直接执行效果
                ApplyShockwaveEffect(type);
                return;
            }
            
            // 停止之前的冲击波动画
            if (shockwaveCoroutine != null)
            {
                StopCoroutine(shockwaveCoroutine);
                ResetShockwave();
            }
            
            shockwaveCoroutine = StartCoroutine(AnimateShockwaveCoroutine(type));
        }
        
        /// <summary>
        /// 冲击波动画协程
        /// </summary>
        private IEnumerator AnimateShockwaveCoroutine(ShockwaveType type)
        {
            // 显示冲击波
            shockwaveTransform.gameObject.SetActive(true);
            
            // 重置到初始状态
            shockwaveTransform.localScale = Vector3.zero;
            if (shockwaveRenderer != null)
            {
                Color c = shockwaveOriginalColor;
                c.a = 1f;
                shockwaveRenderer.color = c;
            }
            
            float elapsed = 0f;
            bool effectApplied = false;
            
            while (elapsed < shockwaveDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shockwaveDuration;
                
                // 从小到大扩展
                float radius = Mathf.Lerp(0f, shockwaveMaxRadius, t);
                shockwaveTransform.localScale = Vector3.one * radius * 2f;
                
                // 渐隐
                if (shockwaveRenderer != null)
                {
                    Color c = shockwaveOriginalColor;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    shockwaveRenderer.color = c;
                }
                
                // 在30%时执行物理效果（更早一点，让玩家感觉更及时）
                if (!effectApplied && t >= 0.3f)
                {
                    ApplyShockwaveEffect(type);
                    effectApplied = true;
                }
                
                yield return null;
            }
            
            // 动画结束，隐藏冲击波
            ResetShockwave();
            
            if (showDebugInfo)
            {
                Debug.Log($"[Shield] 冲击波动画完成 - Type:{type}");
            }
        }
        
        /// <summary>
        /// 重置冲击波到初始状态
        /// </summary>
        private void ResetShockwave()
        {
            if (shockwaveTransform != null)
            {
                shockwaveTransform.localScale = shockwaveOriginalScale;
                shockwaveTransform.gameObject.SetActive(false);
                
                if (shockwaveRenderer != null)
                {
                    shockwaveRenderer.color = shockwaveOriginalColor;
                }
            }
        }
        
        /// <summary>
        /// 应用冲击波物理效果
        /// </summary>
        private void ApplyShockwaveEffect(ShockwaveType type)
        {
            // 使用世界坐标位置检测
            Vector2 center = transform.position;
            
            Collider2D[] enemies = Physics2D.OverlapCircleAll(
                center, 
                shockwaveMaxRadius, 
                enemyLayerMask
            );
            
            if (showDebugInfo)
            {
                Debug.Log($"[Shield] 冲击波检测到 {enemies.Length} 个敌人");
            }
            
            foreach (var enemyCollider in enemies)
            {
                EnemyBlob enemy = enemyCollider.GetComponent<EnemyBlob>();
                Rigidbody2D rb = enemyCollider.GetComponent<Rigidbody2D>();
                
                if (enemy == null || rb == null) continue;
                
                // 计算方向（从护盾中心指向敌人）
                Vector2 direction = ((Vector2)enemyCollider.transform.position - center).normalized;
                
                // 距离衰减
                float distance = Vector2.Distance(center, enemyCollider.transform.position);
                float falloff = 1f - (distance / shockwaveMaxRadius);
                falloff = Mathf.Max(0.3f, falloff); // 最小保持30%力度
                
                bool isSmall = IsSmallEnemy(rb.mass);
                
                if (type == ShockwaveType.OnRecover)
                {
                    // 恢复冲击波：杀死小怪，弹开大怪
                    if (isSmall)
                    {
                        enemy.KillByShockwave();
                        
                        if (showDebugInfo)
                        {
                            Debug.Log($"[Shield] 恢复冲击波杀死小怪: {enemy.name}");
                        }
                    }
                    else
                    {
                        // 大怪用 Impulse 模式，瞬间施加力
                        float actualForce = shockwaveForce * falloff;
                        rb.AddForce(direction * actualForce, ForceMode2D.Impulse);
                        
                        if (showDebugInfo)
                        {
                            Debug.Log($"[Shield] 恢复冲击波弹开大怪: {enemy.name}, Force:{actualForce}");
                        }
                    }
                }
                else // ShockwaveType.OnHit
                {
                    // 受击冲击波：弹开所有怪
                    float actualForce = shockwaveForce * falloff;
                    
                    // 小怪用更大的力（因为质量小）
                    if (isSmall)
                    {
                        actualForce *= 0.5f; // 小怪力度减半（因为质量小，同样的力会飞更远）
                    }
                    
                    rb.AddForce(direction * actualForce, ForceMode2D.Impulse);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"[Shield] 受击冲击波弹开: {enemy.name}, Force:{actualForce}");
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
            
            if (GUILayout.Button("Test Shockwave"))
            {
                PlayShockwave(ShockwaveType.OnHit);
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