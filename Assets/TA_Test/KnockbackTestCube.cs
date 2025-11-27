using UnityEngine;

/// <summary>
/// 击退测试方块
/// 用于验证激光击退效果是否正常工作
/// 
/// 使用方法：
/// 1. 创建一个 2D Sprite（方块或圆形都可以）
/// 2. 添加 Rigidbody2D 组件
/// 3. 添加 BoxCollider2D 或 CircleCollider2D 组件
/// 4. 将该物体的 Layer 设置为 "Enemy"
/// 5. 挂载此脚本
/// 6. 运行游戏，用激光照射方块
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class KnockbackTestCube : MonoBehaviour
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Inspector 配置
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    [Header("物理设置")]
    [Tooltip("方块质量（越大越难推动）")]
    [SerializeField] private float mass = 1.0f;
    
    [Tooltip("线性阻力（越大停下越快）")]
    [SerializeField] private float linearDrag = 2.0f;
    
    [Header("击退设置")]
    [Tooltip("击退力倍率（调试用）")]
    [SerializeField] private float knockbackMultiplier = 1.0f;
    
    [Tooltip("使用 Impulse 模式（瞬间冲击）还是 Force 模式（持续推力）")]
    [SerializeField] private bool useImpulseMode = true;
    
    [Header("视觉反馈")]
    [Tooltip("受击时的颜色")]
    [SerializeField] private Color hitColor = Color.red;
    
    [Tooltip("颜色恢复时间")]
    [SerializeField] private float colorRecoveryTime = 0.2f;
    
    [Header("调试信息")]
    [SerializeField] private bool showDebugLog = true;
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 运行时数据
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private float lastHitTime;
    
    // 统计数据
    private int hitCount = 0;
    private float totalDamageReceived = 0f;
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Unity 生命周期
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // 配置 Rigidbody2D
        ConfigureRigidbody();
        
        // 保存原始颜色
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        
        // 检查 Layer
        if (gameObject.layer != LayerMask.NameToLayer("Enemy"))
        {
            Debug.LogWarning($"[KnockbackTestCube] 警告：当前 Layer 不是 'Enemy'！激光可能无法检测到。当前 Layer: {LayerMask.LayerToName(gameObject.layer)}");
        }
    }
    
    private void Update()
    {
        // 颜色恢复
        UpdateColorRecovery();
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 配置
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    private void ConfigureRigidbody()
    {
        rb.gravityScale = 0f;           // 无重力（2D俯视角）
        rb.mass = mass;
        rb.drag = linearDrag;
        rb.angularDrag = 1f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // 防止旋转
        
        if (showDebugLog)
        {
            Debug.Log($"[KnockbackTestCube] Rigidbody 配置完成 - Mass: {mass}, Drag: {linearDrag}");
        }
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 核心接口：被激光调用
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 接收伤害和击退力（与 EnemyBlob.TakeDamage 接口相同）
    /// </summary>
    /// <param name="damage">伤害值</param>
    /// <param name="knockbackForce">击退力向量</param>
    public void TakeDamage(float damage, Vector2 knockbackForce)
    {
        hitCount++;
        totalDamageReceived += damage;
        lastHitTime = Time.time;
        
        // 应用击退力
        ApplyKnockback(knockbackForce);
        
        // 视觉反馈
        TriggerHitFlash();
        
        if (showDebugLog)
        {
            Debug.Log($"[KnockbackTestCube] 受击 #{hitCount}" +
                      $"\n  伤害: {damage:F1}" +
                      $"\n  击退力: {knockbackForce} (magnitude: {knockbackForce.magnitude:F2})" +
                      $"\n  当前速度: {rb.velocity} (magnitude: {rb.velocity.magnitude:F2})" +
                      $"\n  累计伤害: {totalDamageReceived:F1}");
        }
    }
    
    /// <summary>
    /// 应用击退力
    /// </summary>
    private void ApplyKnockback(Vector2 force)
    {
        Vector2 finalForce = force * knockbackMultiplier;
        
        if (useImpulseMode)
        {
            // Impulse 模式：瞬间冲击，适合单次击退
            rb.AddForce(finalForce, ForceMode2D.Impulse);
        }
        else
        {
            // Force 模式：持续推力，适合持续激光
            rb.AddForce(finalForce, ForceMode2D.Force);
        }
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 视觉反馈
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    private void TriggerHitFlash()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = hitColor;
        }
    }
    
    private void UpdateColorRecovery()
    {
        if (spriteRenderer == null) return;
        
        float timeSinceHit = Time.time - lastHitTime;
        if (timeSinceHit < colorRecoveryTime)
        {
            float t = timeSinceHit / colorRecoveryTime;
            spriteRenderer.color = Color.Lerp(hitColor, originalColor, t);
        }
        else if (spriteRenderer.color != originalColor)
        {
            spriteRenderer.color = originalColor;
        }
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 编辑器调试
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    private void OnGUI()
    {
        if (!showDebugLog || !Application.isPlaying) return;
        
        // 在物体位置上方显示信息
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
        
        // 转换为 GUI 坐标（Y轴翻转）
        screenPos.y = Screen.height - screenPos.y;
        
        // 只在屏幕内显示
        if (screenPos.z > 0 && screenPos.x > 0 && screenPos.x < Screen.width && screenPos.y > 0 && screenPos.y < Screen.height)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(screenPos.x - 60, screenPos.y - 60, 200, 100),
                $"击中次数: {hitCount}\n" +
                $"累计伤害: {totalDamageReceived:F0}\n" +
                $"当前速度: {rb.velocity.magnitude:F2}");
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // 绘制当前速度向量
        if (Application.isPlaying && rb != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)rb.velocity);
            
            // 绘制速度箭头
            Gizmos.DrawWireSphere(transform.position + (Vector3)rb.velocity, 0.1f);
        }
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 重置方法（编辑器测试用）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    [ContextMenu("重置位置和状态")]
    public void ResetState()
    {
        transform.position = Vector3.zero;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        hitCount = 0;
        totalDamageReceived = 0f;
        
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        
        Debug.Log("[KnockbackTestCube] 状态已重置");
    }
    
    [ContextMenu("测试击退（向上）")]
    public void TestKnockbackUp()
    {
        TakeDamage(10f, Vector2.up * 50f);
    }
    
    [ContextMenu("测试击退（向右）")]
    public void TestKnockbackRight()
    {
        TakeDamage(10f, Vector2.right * 50f);
    }
}