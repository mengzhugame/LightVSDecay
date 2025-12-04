using UnityEngine;

/// <summary>
/// 简化版激光测试脚本
/// 用于测试击退效果，独立于主游戏系统
/// 
/// 使用方法：
/// 1. 将此脚本挂载到激光 Pivot 或 Turret 上
/// 2. 将 laserOrigin 设置为激光发射点
/// 3. 运行游戏，激光会自动检测并击退 Enemy Layer 上的物体
/// </summary>
public class LaserKnockbackTester : MonoBehaviour
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Inspector 配置
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    [Header("激光设置")]
    [Tooltip("激光发射点（如果为空则使用自身 Transform）")]
    [SerializeField] private Transform laserOrigin;
    
    [Tooltip("激光最大长度")]
    [SerializeField] private float maxLength = 15f;
    
    [Tooltip("激光宽度（仅用于 Gizmo 显示）")]
    [SerializeField] private float laserWidth = 0.5f;
    
    [Header("伤害设置")]
    [Tooltip("每秒伤害 (DPS)")]
    [SerializeField] private float dps = 100f;
    
    [Tooltip("伤害判定间隔（秒）")]
    [SerializeField] private float tickRate = 0.1f;
    
    [Header("击退设置")]
    [Tooltip("基础击退力")]
    [SerializeField] private float baseKnockbackForce = 50f;
    
    [Tooltip("击退力模式")]
    [SerializeField] private ForceMode2D forceMode = ForceMode2D.Force;
    
    [Header("Layer 设置")]
    [Tooltip("检测的 Layer")]
    [SerializeField] private LayerMask targetLayer;
    
    [Header("调试")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showDebugLog = true;
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 运行时数据
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    private float lastTickTime;
    private RaycastHit2D currentHit;
    private float currentHitDistance;
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Unity 生命周期
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    private void Awake()
    {
        if (laserOrigin == null)
        {
            laserOrigin = transform;
        }
        
        // 自动设置 Enemy Layer
        if (targetLayer == 0)
        {
            targetLayer = LayerMask.GetMask("Enemy");
            Debug.Log("[LaserKnockbackTester] 自动设置 targetLayer 为 'Enemy'");
        }
    }
    
    private void Update()
    {
        // 执行 Raycast 检测
        PerformRaycast();
        
        // 处理伤害和击退
        ProcessDamageAndKnockback();
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 核心逻辑
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    private void PerformRaycast()
    {
        Vector2 origin = laserOrigin.position;
        Vector2 direction = laserOrigin.up; // 假设激光朝上
        
        currentHit = Physics2D.Raycast(origin, direction, maxLength, targetLayer);
        
        if (currentHit.collider != null)
        {
            currentHitDistance = currentHit.distance;
        }
        else
        {
            currentHitDistance = maxLength;
        }
    }
    
    private void ProcessDamageAndKnockback()
    {
        // 检查 Tick 间隔
        if (Time.time - lastTickTime < tickRate)
            return;
        
        lastTickTime = Time.time;
        
        // 如果没有击中任何东西
        if (currentHit.collider == null)
            return;
        
        // 计算伤害
        float damage = dps * tickRate;
        
        // 计算击退力方向（从激光原点指向目标）
        Vector2 knockbackDirection = (currentHit.point - (Vector2)laserOrigin.position).normalized;
        Vector2 knockbackForce = knockbackDirection * baseKnockbackForce;
        
        // 尝试获取 KnockbackTestCube 组件
        var testCube = currentHit.collider.GetComponent<KnockbackTestCube>();
        if (testCube != null)
        {
            testCube.TakeDamage(damage, knockbackForce);
            
            if (showDebugLog)
            {
                Debug.Log($"[LaserKnockbackTester] 击中 KnockbackTestCube" +
                          $"\n  伤害: {damage:F1}" +
                          $"\n  击退方向: {knockbackDirection}" +
                          $"\n  击退力: {knockbackForce.magnitude:F2}");
            }
            return;
        }
        
        // 尝试获取 EnemyBlob 组件（兼容现有系统）
        var enemyBlob = currentHit.collider.GetComponent<LightVsDecay.Logic.Enemy.EnemyBlob>();
        if (enemyBlob != null)
        {
            enemyBlob.TakeDamage(damage, knockbackForce);
            
            if (showDebugLog)
            {
                Debug.Log($"[LaserKnockbackTester] 击中 EnemyBlob: {enemyBlob.name}");
            }
            return;
        }
        
        // 如果都没有，尝试直接操作 Rigidbody2D
        var rb = currentHit.collider.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.AddForce(knockbackForce, forceMode);
            
            if (showDebugLog)
            {
                Debug.Log($"[LaserKnockbackTester] 直接击退 Rigidbody2D: {rb.name}");
            }
        }
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 调试可视化
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        Transform origin = laserOrigin != null ? laserOrigin : transform;
        Vector3 start = origin.position;
        Vector3 direction = origin.up;
        
        // 绘制激光线
        if (Application.isPlaying)
        {
            // 运行时：显示实际检测结果
            Gizmos.color = currentHit.collider != null ? Color.red : Color.green;
            Gizmos.DrawLine(start, start + direction * currentHitDistance);
            
            // 绘制击中点
            if (currentHit.collider != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentHit.point, 0.3f);
                
                // 绘制击退方向
                Gizmos.color = Color.magenta;
                Vector3 knockbackDir = (currentHit.point - (Vector2)start).normalized;
                //Gizmos.DrawLine(currentHit.point, currentHit.point + knockbackDir * 2f);
            }
        }
        else
        {
            // 编辑器模式：显示最大长度
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(start, start + direction * maxLength);
        }
        
        // 绘制激光宽度（矩形边界）
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Vector3 right = origin.right * laserWidth / 2f;
        Vector3 end = start + direction * (Application.isPlaying ? currentHitDistance : maxLength);
        
        Gizmos.DrawLine(start + right, end + right);
        Gizmos.DrawLine(start - right, end - right);
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 编辑器调试
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    private void OnGUI()
    {
        if (!showDebugLog || !Application.isPlaying) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        
        GUI.color = Color.white;
        GUILayout.Label("=== Laser Knockback Tester ===");
        GUILayout.Label($"击中目标: {(currentHit.collider != null ? currentHit.collider.name : "无")}");
        GUILayout.Label($"击中距离: {currentHitDistance:F2}");
        GUILayout.Label($"DPS: {dps} | 单次伤害: {dps * tickRate:F1}");
        GUILayout.Label($"击退力: {baseKnockbackForce}");
        GUILayout.Label($"力模式: {forceMode}");
        
        GUILayout.EndArea();
    }
}