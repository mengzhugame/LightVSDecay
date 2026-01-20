// ============================================================
// LaserBeam.cs (重构版 - LineRenderer + 反射)
// 文件位置: Assets/Scripts/Logic/Player/LaserBeam.cs
// 用途：单个激光光束组件 - 支持墙壁反射
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using LightVsDecay.Core;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 激光光束段数据
    /// </summary>
    public struct LaserSegment
    {
        public Vector3 startPoint;
        public Vector3 endPoint;
        public float length;
        public bool isReflected;  // 是否是反射段
        
        public Vector3 Direction => (endPoint - startPoint).normalized;
    }
    
    /// <summary>
    /// 单个激光光束组件（重构版）
    /// 使用 LineRenderer 实现，支持墙壁反射
    /// </summary>
    public class LaserBeam : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 可配置参数
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("组件引用")]
        [Tooltip("LineRenderer 组件")]
        [SerializeField] private LineRenderer lineRenderer;
        
        [Tooltip("起点粒子特效")]
        [SerializeField] private Transform startVFX;
        
        [Tooltip("终点粒子特效")]
        [SerializeField] private Transform endVFX;
        
        [Header("VFX 子节点引用（用于缩放）")]
        [Tooltip("StartVFX 下需要缩放的子节点")]
        [SerializeField] private Transform[] startVFXChildren;
        
        [Tooltip("EndVFX 下需要缩放的子节点")]
        [SerializeField] private Transform[] endVFXChildren;
        
        [Header("激光属性")]
        [Tooltip("激光最大长度")]
        [SerializeField] private float maxLength = GameConstants.LASER_MAX_LENGTH;
        
        [Tooltip("激光宽度")]
        [SerializeField] private float laserWidth = GameConstants.LASER_DEFAULT_WIDTH;
        
        [Header("反射设置")]
        [Tooltip("是否启用反射")]
        [SerializeField] private bool reflectionEnabled = false;
        
        [Tooltip("墙壁检测层")]
        [SerializeField] private LayerMask wallLayer;
        
        [Tooltip("敌人检测层")]
        [SerializeField] private LayerMask enemyLayer;
        
        [Header("VFX 缩放")]
        [Tooltip("基础宽度（对应VFX缩放为1）")]
        [SerializeField] private float baseWidth = 0.5f;
        
        [Tooltip("基础VFX缩放值")]
        [SerializeField] private float baseVFXScale = 1f;
        
        [Header("性能优化")]
        [Tooltip("Raycast检测间隔（秒）")]
        [SerializeField] private float raycastInterval = GameConstants.REFLEX_RAYCAST_INTERVAL;
        
        [Header("调试")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool showDebugLogs = false;
        
        [Header("旋转控制")]
        [Tooltip("旋转控制节点（LaserPivot）- 如果为空则自动查找")]
        [SerializeField] private Transform laserPivot;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有变量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Transform cachedTransform;
        
        // Raycast 优化
        private float lastRaycastTime;
        
        // 激光路径点（世界坐标）
        private List<Vector3> laserPoints = new List<Vector3>();
        
        // 激光段数据（供伤害检测使用）
        private List<LaserSegment> laserSegments = new List<LaserSegment>();
        
        // 击中状态
        private bool hitEnemy = false;
        private Vector3 hitPoint;
        private RaycastHit2D currentHit;
        
        // 反射状态
        private bool hasReflection = false;
        private Vector3 reflectionPoint;
        // ========== 材质颜色控制 ==========
        private MaterialPropertyBlock laserPropertyBlock;
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private Color originalBaseColor;  // 缓存原始材质颜色
        private bool hasOriginalColor = false;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            cachedTransform = transform;
            
            // 自动查找 LineRenderer
            if (lineRenderer == null)
            {
                lineRenderer = GetComponentInChildren<LineRenderer>();
            }
            
            // 注意：laserPivot 将由 LaserController 通过 SetLaserPivot() 传递
            // 这里不再自动查找，避免找错节点
            
            // 初始化 Layer
            if (wallLayer == 0)
            {
                wallLayer = LayerMask.GetMask(GameConstants.WALL_LAYER);
            }
            if (enemyLayer == 0)
            {
                enemyLayer = LayerMask.GetMask(GameConstants.ENEMY_LAYER, GameConstants.BOUNCING_ENEMY_LAYER);
            }
            // 初始化材质属性块
            laserPropertyBlock = new MaterialPropertyBlock();
            // 缓存原始材质颜色
            CacheOriginalColor();
        }
        
        private void Start()
        {
            InitializeLaser();
        }
        
        private void Update()
        {
            // 每帧都计算激光路径（确保实时响应旋转）
            CalculateLaserPath();
            UpdateLaserVisuals();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void InitializeLaser()
        {
            if (lineRenderer == null)
            {
                Debug.LogError("[LaserBeam] LineRenderer 未找到！");
                return;
            }
            
            // 确保使用世界空间
            lineRenderer.useWorldSpace = true;
            
            // 初始化路径点列表
            laserPoints.Clear();
            laserSegments.Clear();
            
            // 立即计算一次路径
            CalculateLaserPath();
            UpdateLaserVisuals();
            
            if (showDebugLogs)
            {
                Debug.Log($"[LaserBeam] 初始化完成 - 反射: {(reflectionEnabled ? "启用" : "禁用")}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 激光路径计算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 计算激光路径（包含反射）
        /// 使用 TransformPoint 将本地坐标转换为世界坐标
        /// </summary>
        private void CalculateLaserPath()
        {
            laserPoints.Clear();
            laserSegments.Clear();
            hitEnemy = false;
            hasReflection = false;
            
            if (laserPivot == null)
            {
                Debug.LogError("[LaserBeam] laserPivot 为空！无法计算激光路径");
                return;
            }
            
            // ═══════════════════════════════════════════════════
            // 使用 TransformPoint 计算起点和终点（关键！）
            // 本地坐标 (0, 0, 0) -> 世界坐标起点
            // 本地坐标 (0, maxLength, 0) -> 世界坐标终点（无遮挡时）
            // ═══════════════════════════════════════════════════
            
            // 起点（本地 0,0,0 转换为世界坐标）
            Vector3 startPoint = laserPivot.TransformPoint(Vector3.zero);
            laserPoints.Add(startPoint);
            
            // 计算激光方向（本地 Y 轴在世界空间中的方向）
            Vector3 localEndPoint = new Vector3(0, maxLength, 0);
            Vector3 worldEndPoint = laserPivot.TransformPoint(localEndPoint);
            Vector3 currentDirection = (worldEndPoint - startPoint).normalized;
            
            // 调试：打印旋转信息
            if (showDebugLogs)
            {
                Debug.Log($"[LaserBeam] 起点: {startPoint}, 终点: {worldEndPoint}, 方向: {currentDirection}, Pivot旋转Z: {laserPivot.eulerAngles.z:F1}°");
            }
            
            Vector3 currentPoint = startPoint;
            float remainingLength = maxLength;
            bool isFirstSegment = true;
            
            // 最多处理2段（主激光 + 1次反射）
            int maxIterations = reflectionEnabled ? 2 : 1;
            
            for (int i = 0; i < maxIterations && remainingLength > 0.1f; i++)
            {
                // 执行射线检测
                RaycastResult result = PerformRaycast(currentPoint, currentDirection, remainingLength);
                
                if (result.hitSomething)
                {
                    // 添加击中点
                    laserPoints.Add(result.hitPoint);
                    
                    // 记录激光段
                    laserSegments.Add(new LaserSegment
                    {
                        startPoint = currentPoint,
                        endPoint = result.hitPoint,
                        length = result.hitDistance,
                        isReflected = !isFirstSegment
                    });
                    
                    remainingLength -= result.hitDistance;
                    
                    if (result.hitEnemy)
                    {
                        // 击中敌人，停止
                        hitEnemy = true;
                        hitPoint = result.hitPoint;
                        currentHit = result.raycastHit;
                        
                        if (showDebugLogs)
                        {
                            Debug.Log($"[LaserBeam] 击中敌人 - 位置: {result.hitPoint}");
                        }
                        break;
                    }
                    else if (result.hitWall && reflectionEnabled && isFirstSegment)
                    {
                        // 击中墙壁且允许反射
                        hasReflection = true;
                        reflectionPoint = result.hitPoint;
                        
                        // 计算反射方向
                        currentDirection = Vector3.Reflect(currentDirection, result.hitNormal);
                        // 偏移一点避免重复检测
                        currentPoint = result.hitPoint + currentDirection * GameConstants.REFLEX_POINT_OFFSET;
                        isFirstSegment = false;
                        
                        if (showDebugLogs)
                        {
                            Debug.Log($"[LaserBeam] 墙壁反射 - 位置: {result.hitPoint}, 新方向: {currentDirection}");
                        }
                    }
                    else
                    {
                        // 击中墙壁但不反射，停止
                        break;
                    }
                }
                else
                {
                    // 没有击中任何东西，延伸到最大长度
                    Vector3 endPoint = currentPoint + currentDirection * remainingLength;
                    laserPoints.Add(endPoint);
                    
                    // 记录激光段
                    laserSegments.Add(new LaserSegment
                    {
                        startPoint = currentPoint,
                        endPoint = endPoint,
                        length = remainingLength,
                        isReflected = !isFirstSegment
                    });
                    
                    break;
                }
            }
            
            // 确保至少有2个点
            if (laserPoints.Count < 2)
            {
                Vector3 endPoint = startPoint + laserPivot.up * maxLength;
                laserPoints.Add(endPoint);
                
                laserSegments.Add(new LaserSegment
                {
                    startPoint = startPoint,
                    endPoint = endPoint,
                    length = maxLength,
                    isReflected = false
                });
            }
        }
        
        /// <summary>
        /// 执行射线检测
        /// </summary>
        private RaycastResult PerformRaycast(Vector3 origin, Vector3 direction, float maxDistance)
        {
            RaycastResult result = new RaycastResult();
            
            // 分别检测敌人和墙壁
            RaycastHit2D enemyHit = Physics2D.Raycast(origin, direction, maxDistance, enemyLayer);
            RaycastHit2D wallHit = Physics2D.Raycast(origin, direction, maxDistance, wallLayer);
            
            bool hasEnemyHit = enemyHit.collider != null;
            bool hasWallHit = wallHit.collider != null;
            
            // 优先敌人（无论距离）
            if (hasEnemyHit && hasWallHit)
            {
                // 两者都击中，优先敌人
                result.hitSomething = true;
                result.hitEnemy = true;
                result.hitPoint = enemyHit.point;
                result.hitNormal = enemyHit.normal;
                result.hitDistance = enemyHit.distance;
                result.hitCollider = enemyHit.collider;
                result.raycastHit = enemyHit;
            }
            else if (hasEnemyHit)
            {
                result.hitSomething = true;
                result.hitEnemy = true;
                result.hitPoint = enemyHit.point;
                result.hitNormal = enemyHit.normal;
                result.hitDistance = enemyHit.distance;
                result.hitCollider = enemyHit.collider;
                result.raycastHit = enemyHit;
            }
            else if (hasWallHit)
            {
                result.hitSomething = true;
                result.hitWall = true;
                result.hitPoint = wallHit.point;
                result.hitNormal = wallHit.normal;
                result.hitDistance = wallHit.distance;
                result.hitCollider = wallHit.collider;
                result.raycastHit = wallHit;
            }
            
            return result;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 视觉更新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 更新激光视觉效果
        /// </summary>
        private void UpdateLaserVisuals()
        {
            if (lineRenderer == null || laserPoints.Count < 2) return;
            
            // 更新 LineRenderer 点数
            lineRenderer.positionCount = laserPoints.Count;
            
            // 设置所有点的位置
            for (int i = 0; i < laserPoints.Count; i++)
            {
                lineRenderer.SetPosition(i, laserPoints[i]);
            }
            
            // 更新宽度
            lineRenderer.startWidth = laserWidth;
            lineRenderer.endWidth = laserWidth;
            
            // 更新 VFX 位置
            UpdateVFXPositions();
            
            // 更新 VFX 缩放
            UpdateVFXScale();
        }
        
        /// <summary>
        /// 更新 VFX 位置
        /// </summary>
        private void UpdateVFXPositions()
        {
            if (laserPoints.Count < 2) return;
            
            // StartVFX 在激光起点
            if (startVFX != null)
            {
                startVFX.position = laserPoints[0];
            }
            
            // EndVFX 在激光终点
            if (endVFX != null)
            {
                endVFX.position = laserPoints[laserPoints.Count - 1];
            }
        }
        
        /// <summary>
        /// 更新 VFX 子节点缩放
        /// </summary>
        private void UpdateVFXScale()
        {
            float widthRatio = laserWidth / baseWidth;
            float targetScale = baseVFXScale * widthRatio;
            
            if (startVFXChildren != null)
            {
                foreach (var child in startVFXChildren)
                {
                    if (child != null)
                    {
                        child.localScale = Vector3.one * targetScale;
                    }
                }
            }
            
            if (endVFXChildren != null)
            {
                foreach (var child in endVFXChildren)
                {
                    if (child != null)
                    {
                        child.localScale = Vector3.one * targetScale;
                    }
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置激光宽度
        /// </summary>
        public void SetLaserWidth(float width)
        {
            laserWidth = Mathf.Max(0.01f, width);
        }
        
        /// <summary>
        /// 设置最大长度
        /// </summary>
        public void SetMaxLength(float length)
        {
            maxLength = Mathf.Max(0.1f, length);
        }
        
        /// <summary>
        /// 启用/禁用反射
        /// </summary>
        public void SetReflectionEnabled(bool enabled)
        {
            reflectionEnabled = enabled;
            if (showDebugLogs)
            {
                Debug.Log($"[LaserBeam] 反射 {(enabled ? "启用" : "禁用")}");
            }
        }
        
        /// <summary>
        /// 设置激光颜色（通过 MaterialPropertyBlock 设置 Shader 的 _BaseColor）
        /// </summary>
        public void SetColor(Color color)
        {
            if (lineRenderer == null) return;
    
            // 1. 设置顶点颜色（兼容某些材质）
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
    
            // 2. 通过 MaterialPropertyBlock 设置 Shader 的 _BaseColor
            if (laserPropertyBlock == null)
            {
                laserPropertyBlock = new MaterialPropertyBlock();
            }
    
            lineRenderer.GetPropertyBlock(laserPropertyBlock);
            laserPropertyBlock.SetColor(BaseColorID, color);
            lineRenderer.SetPropertyBlock(laserPropertyBlock);
        }
        
        /// <summary>
        /// 设置光晕颜色（兼容旧接口）
        /// </summary>
        public void SetGlowColor(Color color)
        {
            SetColor(color);
        }
        
        /// <summary>
        /// 获取当前击中的目标
        /// </summary>
        public RaycastHit2D GetCurrentHit() => currentHit;
        
        /// <summary>
        /// 获取当前激光宽度
        /// </summary>
        public float GetLaserWidth() => laserWidth;
        
        /// <summary>
        /// 获取当前最大长度
        /// </summary>
        public float GetMaxLength() => maxLength;
        
        /// <summary>
        /// 获取所有激光段数据（供伤害检测使用）
        /// </summary>
        public List<LaserSegment> GetLaserSegments() => laserSegments;
        
        /// <summary>
        /// 获取激光路径点
        /// </summary>
        public List<Vector3> GetLaserPoints() => laserPoints;
        
        /// <summary>
        /// 是否有反射
        /// </summary>
        public bool HasReflection() => hasReflection;
        
        /// <summary>
        /// 获取反射点
        /// </summary>
        public Vector3 GetReflectionPoint() => reflectionPoint;
        
        /// <summary>
        /// 是否击中敌人
        /// </summary>
        public bool HasHitEnemy() => hitEnemy;
        
        /// <summary>
        /// 手动设置 LaserPivot（由 LaserController 调用）
        /// </summary>
        public void SetLaserPivot(Transform pivot)
        {
            laserPivot = pivot;
            
            if (pivot != null)
            {
                Debug.Log($"[LaserBeam] LaserPivot 已设置: {pivot.name} (位置: {pivot.position})");
            }
            else
            {
                Debug.LogError("[LaserBeam] SetLaserPivot 收到空引用！");
            }
        }
        /// <summary>
        /// 缓存原始材质颜色
        /// </summary>
        private void CacheOriginalColor()
        {
            if (lineRenderer != null && lineRenderer.sharedMaterial != null)
            {
                if (lineRenderer.sharedMaterial.HasProperty(BaseColorID))
                {
                    originalBaseColor = lineRenderer.sharedMaterial.GetColor(BaseColorID);
                    hasOriginalColor = true;
                }
            }
        }
        /// <summary>
        /// 重置激光颜色为原始材质颜色
        /// </summary>
        public void ResetColor()
        {
            if (lineRenderer == null) return;
    
            if (hasOriginalColor)
            {
                // 使用缓存的原始颜色
                lineRenderer.startColor = originalBaseColor;
                lineRenderer.endColor = originalBaseColor;
        
                if (laserPropertyBlock == null)
                {
                    laserPropertyBlock = new MaterialPropertyBlock();
                }
        
                lineRenderer.GetPropertyBlock(laserPropertyBlock);
                laserPropertyBlock.SetColor(BaseColorID, originalBaseColor);
                lineRenderer.SetPropertyBlock(laserPropertyBlock);
            }
            else
            {
                // 清除 PropertyBlock，恢复材质默认值
                if (laserPropertyBlock != null)
                {
                    laserPropertyBlock.Clear();
                    lineRenderer.SetPropertyBlock(laserPropertyBlock);
                }
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying) return;
            if (laserPoints == null || laserPoints.Count < 2) return;
            
            for (int i = 0; i < laserPoints.Count; i++)
            {
                // 绘制点
                if (i == 0)
                {
                    Gizmos.color = Color.green;
                }
                else if (i == laserPoints.Count - 1)
                {
                    Gizmos.color = hitEnemy ? Color.red : new Color(1f, 0.5f, 0f);
                }
                else
                {
                    Gizmos.color = Color.blue;
                }
                Gizmos.DrawWireSphere(laserPoints[i], 0.2f);
                
                // 绘制线段
                if (i < laserPoints.Count - 1)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(laserPoints[i], laserPoints[i + 1]);
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内部结构
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private struct RaycastResult
        {
            public bool hitSomething;
            public bool hitEnemy;
            public bool hitWall;
            public Vector3 hitPoint;
            public Vector3 hitNormal;
            public float hitDistance;
            public Collider2D hitCollider;
            public RaycastHit2D raycastHit;
        }
    }
}