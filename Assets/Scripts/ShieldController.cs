using UnityEngine;
using System.Collections;
using LightVsDecay.Core.Pool;

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
    /// 能量护盾控制器
    /// 负责：血量管理、视觉效果、动画控制、碰撞交互
    /// </summary>
    public class ShieldController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("护盾属性")]
        [Tooltip("护盾最大血量")]
        [SerializeField] private int maxShieldHP = 3;
        
        [Tooltip("护盾恢复时间（秒）")]
        [SerializeField] private float recoveryTime = 12f;
        
        [Tooltip("受击后无敌时间（秒）")]
        [SerializeField] private float invincibilityDuration = 1.0f;
        
        [Header("视觉组件")]
        [Tooltip("护盾渲染器")]
        [SerializeField] private SpriteRenderer shieldRenderer;
        
        [Tooltip("护盾材质（如果为空，自动获取）")]
        [SerializeField] private Material shieldMaterial;
        
        [Header("颜色配置")]
        [Tooltip("满血颜色 (青色)")]
        [ColorUsage(true,true)]
        [SerializeField] private Color fullHPColor = new Color(0f, 1f, 1f, 0.3f);
        
        [Tooltip("2血颜色 (黄绿色)")]
        [ColorUsage(true,true)]
        [SerializeField] private Color midHPColor = new Color(0.4f, 1f, 0.8f, 0.3f);
        
        [Tooltip("1血颜色 (橙色)")]
        [ColorUsage(true,true)]
        [SerializeField] private Color lowHPColor = new Color(1f, 0.7f, 0.4f, 0.3f);
        
        [Tooltip("边缘发光颜色（跟随血量变化）")]
        [ColorUsage(true,true)]
        [SerializeField] private Color fullHPEdgeColor = new Color(0f, 1f, 1f, 1f);
        [ColorUsage(true,true)]
        [SerializeField] private Color midHPEdgeColor = new Color(0.4f, 1f, 0.8f, 1f);
        [ColorUsage(true,true)]
        [SerializeField] private Color lowHPEdgeColor = new Color(1f, 0.7f, 0.4f, 1f);
        
        [Header("动画设置")]
        [Tooltip("撞击闪烁持续时间")]
        [SerializeField] private float hitFlashDuration = 0.15f;
        
        [Tooltip("破碎渐隐时间")]
        [SerializeField] private float breakFadeDuration = 0.5f;
        
        [Tooltip("恢复渐显时间")]
        [SerializeField] private float recoverFadeDuration = 0.8f;
        
        [Header("冲击波设置")]
        [Tooltip("冲击波预制体")]
        [SerializeField] private GameObject shockwavePrefab;
        
        [Tooltip("冲击波最大半径")]
        [SerializeField] private float shockwaveMaxRadius = 5f;
        
        [Tooltip("冲击波扩展时间")]
        [SerializeField] private float shockwaveDuration = 0.4f;
        
        [Tooltip("冲击波击退力")]
        [SerializeField] private float shockwaveForce = 500f;
        
        [Header("碰撞设置")]
        [Tooltip("护盾碰撞器")]
        [SerializeField] private CircleCollider2D shieldCollider;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>护盾受击时触发（参数：剩余血量）</summary>
        public event System.Action<int> OnShieldHit;
        
        /// <summary>护盾破碎时触发</summary>
        public event System.Action OnShieldBroken;
        
        /// <summary>护盾恢复时触发</summary>
        public event System.Action OnShieldRecovered;
        
        /// <summary>护盾血量变化时触发（参数：当前血量，最大血量）</summary>
        public event System.Action<int, int> OnShieldHPChanged;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int currentShieldHP;
        private ShieldState currentState = ShieldState.Active;
        private bool isInvincible = false;
        private float lastHitTime;
        private float recoveryTimer;
        
        // 材质属性ID缓存
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly int EdgeColorID = Shader.PropertyToID("_EdgeColor");
        private static readonly int HitFlashID = Shader.PropertyToID("_HitFlash");
        private static readonly int ShieldAlphaID = Shader.PropertyToID("_ShieldAlpha");
        
        // 协程引用
        private Coroutine hitFlashCoroutine;
        private Coroutine fadeCoroutine;
        private Coroutine recoveryCoroutine;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前护盾血量</summary>
        public int CurrentHP => currentShieldHP;
        
        /// <summary>护盾最大血量</summary>
        public int MaxHP => maxShieldHP;
        
        /// <summary>当前状态</summary>
        public ShieldState State => currentState;
        
        /// <summary>护盾是否激活</summary>
        public bool IsActive => currentState == ShieldState.Active;
        
        /// <summary>是否处于无敌状态</summary>
        public bool IsInvincible => isInvincible;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 自动获取组件
            if (shieldRenderer == null)
            {
                shieldRenderer = GetComponent<SpriteRenderer>();
            }
            
            if (shieldCollider == null)
            {
                shieldCollider = GetComponent<CircleCollider2D>();
            }
            
            // 创建材质实例（避免修改共享材质）
            if (shieldRenderer != null)
            {
                shieldMaterial = shieldRenderer.material;
            }
            
            // 初始化血量
            currentShieldHP = maxShieldHP;
            currentState = ShieldState.Active;
        }
        
        private void Start()
        {
            // 初始化视觉状态
            UpdateShieldVisuals();
            SetShieldAlpha(1f);
        }
        
        private void Update()
        {
            // 更新恢复计时器
            if (currentState == ShieldState.Broken)
            {
                recoveryTimer += Time.deltaTime;
                
                if (recoveryTimer >= recoveryTime)
                {
                    StartRecovery();
                }
            }
            
            // 更新无敌状态
            if (isInvincible && Time.time - lastHitTime >= invincibilityDuration)
            {
                isInvincible = false;
            }
        }
        
        private void OnDestroy()
        {
            // 销毁材质实例
            if (shieldMaterial != null)
            {
                Destroy(shieldMaterial);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 护盾受到伤害
        /// </summary>
        /// <param name="damage">伤害值（默认1）</param>
        /// <returns>是否造成了伤害</returns>
        public bool TakeDamage(int damage = 1)
        {
            // 检查是否可以受伤
            if (currentState != ShieldState.Active || isInvincible)
            {
                return false;
            }
            
            // 扣血
            currentShieldHP = Mathf.Max(0, currentShieldHP - damage);
            lastHitTime = Time.time;
            isInvincible = true;
            
            // 触发闪烁效果
            TriggerHitFlash();
            
            // 更新视觉
            UpdateShieldVisuals();
            
            // 触发事件
            OnShieldHit?.Invoke(currentShieldHP);
            OnShieldHPChanged?.Invoke(currentShieldHP, maxShieldHP);
            
            if (showDebugInfo)
            {
                Debug.Log($"[ShieldController] 受击! 剩余血量: {currentShieldHP}/{maxShieldHP}");
            }
            
            // 检查是否破碎
            if (currentShieldHP <= 0)
            {
                StartBreaking();
            }
            
            return true;
        }
        
        /// <summary>
        /// 触发撞击效果（不造成伤害，仅视觉反馈）
        /// </summary>
        public void TriggerHitEffect()
        {
            if (currentState == ShieldState.Active)
            {
                TriggerHitFlash();
            }
        }
        
        /// <summary>
        /// 强制破碎护盾
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
        /// 强制恢复护盾
        /// </summary>
        public void ForceRecover()
        {
            if (currentState == ShieldState.Broken)
            {
                StartRecovery();
            }
        }
        
        /// <summary>
        /// 重置护盾到满血状态
        /// </summary>
        public void Reset()
        {
            // 停止所有协程
            StopAllCoroutines();
            
            // 重置状态
            currentShieldHP = maxShieldHP;
            currentState = ShieldState.Active;
            isInvincible = false;
            recoveryTimer = 0f;
            
            // 重置视觉
            SetShieldAlpha(1f);
            SetHitFlash(0f);
            UpdateShieldVisuals();
            
            // 启用碰撞
            if (shieldCollider != null)
            {
                shieldCollider.enabled = true;
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[ShieldController] 护盾已重置");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态转换
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 开始破碎动画
        /// </summary>
        private void StartBreaking()
        {
            currentState = ShieldState.Breaking;
            
            // 停止之前的淡入淡出协程
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            
            fadeCoroutine = StartCoroutine(BreakingCoroutine());
        }
        
        /// <summary>
        /// 开始恢复动画
        /// </summary>
        private void StartRecovery()
        {
            currentState = ShieldState.Recovering;
            recoveryTimer = 0f;
            
            // 恢复血量
            currentShieldHP = maxShieldHP;
            
            // 停止之前的淡入淡出协程
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            
            fadeCoroutine = StartCoroutine(RecoveryCoroutine());
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 动画协程
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 破碎渐隐动画
        /// </summary>
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
            
            // 完全隐藏
            SetShieldAlpha(0f);
            
            // 禁用碰撞
            if (shieldCollider != null)
            {
                shieldCollider.enabled = false;
            }
            
            currentState = ShieldState.Broken;
            recoveryTimer = 0f;
            
            // 触发事件
            OnShieldBroken?.Invoke();
            
            if (showDebugInfo)
            {
                Debug.Log("[ShieldController] 护盾已破碎");
            }
        }
        
        /// <summary>
        /// 恢复渐显动画
        /// </summary>
        private IEnumerator RecoveryCoroutine()
        {
            // 更新颜色到满血状态
            UpdateShieldVisuals();
            
            // 启用碰撞
            if (shieldCollider != null)
            {
                shieldCollider.enabled = true;
            }
            
            // 播放冲击波
            SpawnShockwave();
            
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
            
            // 触发事件
            OnShieldRecovered?.Invoke();
            OnShieldHPChanged?.Invoke(currentShieldHP, maxShieldHP);
            
            if (showDebugInfo)
            {
                Debug.Log("[ShieldController] 护盾已恢复");
            }
        }
        
        /// <summary>
        /// 撞击闪烁动画
        /// </summary>
        private IEnumerator HitFlashCoroutine()
        {
            // 快速闪亮
            SetHitFlash(1f);
            
            // 渐变消退
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
        // 冲击波
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 生成冲击波效果
        /// </summary>
        private void SpawnShockwave()
        {
            // 使用VFX池或创建临时对象
            if (shockwavePrefab != null)
            {
                GameObject shockwave = Instantiate(shockwavePrefab, transform.position, Quaternion.identity);
                StartCoroutine(AnimateShockwave(shockwave));
            }
            else
            {
                // 没有预制体时，直接执行击退逻辑
                ApplyShockwaveForce();
            }
        }
        
        /// <summary>
        /// 冲击波动画
        /// </summary>
        private IEnumerator AnimateShockwave(GameObject shockwave)
        {
            SpriteRenderer sr = shockwave.GetComponent<SpriteRenderer>();
            float elapsed = 0f;
            
            while (elapsed < shockwaveDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shockwaveDuration;
                
                // 扩展半径
                float radius = Mathf.Lerp(0f, shockwaveMaxRadius, t);
                shockwave.transform.localScale = Vector3.one * radius * 2f;
                
                // 淡出
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    sr.color = c;
                }
                
                yield return null;
            }
            
            Destroy(shockwave);
            
            // 在冲击波扩展到一半时执行击退
            ApplyShockwaveForce();
        }
        
        /// <summary>
        /// 应用冲击波击退力
        /// </summary>
        private void ApplyShockwaveForce()
        {
            // 获取范围内的所有敌人
            Collider2D[] enemies = Physics2D.OverlapCircleAll(
                transform.position, 
                shockwaveMaxRadius, 
                LayerMask.GetMask("Enemy")
            );
            
            foreach (var enemyCollider in enemies)
            {
                Rigidbody2D rb = enemyCollider.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    // 计算击退方向
                    Vector2 direction = (enemyCollider.transform.position - transform.position).normalized;
                    
                    // 距离衰减
                    float distance = Vector2.Distance(transform.position, enemyCollider.transform.position);
                    float falloff = 1f - (distance / shockwaveMaxRadius);
                    falloff = Mathf.Max(0.3f, falloff); // 最小30%力量
                    
                    // 应用力
                    rb.AddForce(direction * shockwaveForce * falloff, ForceMode2D.Impulse);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"[ShieldController] 冲击波击退: {enemyCollider.name}, 力量: {shockwaveForce * falloff:F1}");
                    }
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 视觉效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 更新护盾视觉（根据血量）
        /// </summary>
        private void UpdateShieldVisuals()
        {
            if (shieldMaterial == null) return;
            
            Color baseColor, edgeColor;
            
            switch (currentShieldHP)
            {
                case 3:
                    baseColor = fullHPColor;
                    edgeColor = fullHPEdgeColor;
                    break;
                case 2:
                    baseColor = midHPColor;
                    edgeColor = midHPEdgeColor;
                    break;
                case 1:
                    baseColor = lowHPColor;
                    edgeColor = lowHPEdgeColor;
                    break;
                default:
                    baseColor = lowHPColor;
                    edgeColor = lowHPEdgeColor;
                    break;
            }
            
            shieldMaterial.SetColor(BaseColorID, baseColor);
            shieldMaterial.SetColor(EdgeColorID, edgeColor);
        }
        
        /// <summary>
        /// 设置护盾总透明度
        /// </summary>
        private void SetShieldAlpha(float alpha)
        {
            if (shieldMaterial != null)
            {
                shieldMaterial.SetFloat(ShieldAlphaID, alpha);
            }
        }
        
        /// <summary>
        /// 设置撞击闪烁强度
        /// </summary>
        private void SetHitFlash(float flash)
        {
            if (shieldMaterial != null)
            {
                shieldMaterial.SetFloat(HitFlashID, flash);
            }
        }
        
        /// <summary>
        /// 触发撞击闪烁
        /// </summary>
        private void TriggerHitFlash()
        {
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
            }
            hitFlashCoroutine = StartCoroutine(HitFlashCoroutine());
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 碰撞处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnCollisionEnter2D(Collision2D collision)
        {
            // 使用 Layer 检测而不是 Tag
            if (collision.gameObject.layer == LayerMask.NameToLayer("Enemy"))
            {
                TakeDamage(1);
            }
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 使用 Layer 检测而不是 Tag
            if (other.gameObject.layer == LayerMask.NameToLayer("Enemy"))
            {
                TakeDamage(1);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, Screen.height - 150, 200, 140));
            GUILayout.Label($"=== Shield Debug ===");
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
            
            if (currentState == ShieldState.Broken && GUILayout.Button("Force Recover"))
            {
                ForceRecover();
            }
            
            GUILayout.EndArea();
        }
        
        private void OnDrawGizmosSelected()
        {
            // 绘制冲击波范围
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, shockwaveMaxRadius);
        }
#endif
    }
}