using UnityEngine;
using System.Collections.Generic;

namespace NeonGamblingTower.Laser
{
    /// <summary>
    /// 激光 LineRenderer 控制器 - Step 2 反射逻辑
    /// 功能：旋转控制、长度控制、宽度控制、VFX同步、墙壁反射
    /// 使用世界空间坐标，支持最多3次反射
    /// </summary>
    public class LaserLineRendererController : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("=== 组件引用 ===")]
        [SerializeField] private Transform laserPivot;          // 旋转控制节点
        [SerializeField] private LineRenderer lineRenderer;     // 激光LineRenderer
        [SerializeField] private Transform startVFX;            // 起点粒子特效
        [SerializeField] private Transform endVFX;              // 终点粒子特效
        
        [Header("=== VFX 子节点引用 ===")]
        [SerializeField] private Transform[] startVFXChildren;  // StartVFX 下需要缩放的子节点
        [SerializeField] private Transform[] endVFXChildren;    // EndVFX 下需要缩放的子节点
        
        [Header("=== 激光参数 ===")]
        [SerializeField] private float laserLength = 19f;       // 激光总长度
        [SerializeField] private float laserWidth = 0.5f;       // 激光宽度
        [SerializeField] private float startPointOffset = 0f;   // 起点Y轴偏移（本地坐标）
        
        [Header("=== 旋转控制 ===")]
        [SerializeField] private float rotationSpeed = 100f;    // 旋转速度
        [SerializeField] private float minAngle = -90f;         // 最小角度（右侧）
        [SerializeField] private float maxAngle = 90f;          // 最大角度（左侧）
        
        [Header("=== VFX 缩放 ===")]
        [SerializeField] private float baseWidth = 0.5f;        // 基础宽度（对应VFX缩放为1）
        [SerializeField] private float baseVFXScale = 1f;       // 基础VFX缩放值
        
        [Header("=== 反射设置 ===")]
        [SerializeField] private int maxReflections = 0;        // 最大反射次数（0-3，由技能等级决定）
        [SerializeField] private LayerMask wallLayer;           // 墙壁Layer
        [SerializeField] private LayerMask enemyLayer;          // 敌人Layer（包含Enemy和BouncingEnemy）
        
        [Header("=== 性能优化 ===")]
        [SerializeField] private float raycastInterval = 0.02f; // Raycast检测间隔（秒）
        
        [Header("=== 调试 ===")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool showDebugLogs = false;
        
        #endregion
        
        #region Private Variables
        
        // 旋转控制
        private float currentAngle = 0f;
        private Vector2 lastTouchPosition;
        private bool isDragging = false;
        
        // Raycast 优化
        private float lastRaycastTime;
        
        // 激光路径点（世界坐标）
        private List<Vector3> laserPoints = new List<Vector3>();
        
        // 缓存
        private Transform cachedTransform;
        
        // 当前击中的目标信息
        private bool hitEnemy = false;
        private Vector3 hitPoint;
        private int currentReflectionCount = 0;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            cachedTransform = transform;
        }
        
        private void Start()
        {
            InitializeLaser();
        }
        
        private void Update()
        {
            HandleRotationInput();
            
            // 定期执行 Raycast 检测
            if (Time.time - lastRaycastTime >= raycastInterval)
            {
                lastRaycastTime = Time.time;
                CalculateLaserPath();
            }
            
            UpdateLaserVisuals();
        }
        
        private void OnValidate()
        {
            // 限制反射次数在 0-3 之间
            maxReflections = Mathf.Clamp(maxReflections, 0, 3);
            
            if (lineRenderer != null && Application.isPlaying)
            {
                CalculateLaserPath();
                UpdateLaserVisuals();
            }
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// 初始化激光设置
        /// </summary>
        private void InitializeLaser()
        {
            if (lineRenderer == null)
            {
                Debug.LogError("[LaserController] LineRenderer 引用为空！");
                return;
            }
            
            // 确保使用世界空间
            lineRenderer.useWorldSpace = true;
            
            // 初始化路径点列表
            laserPoints.Clear();
            
            // 初始化 Layer（如果未设置）
            if (wallLayer == 0)
            {
                wallLayer = LayerMask.GetMask("Wall");
            }
            if (enemyLayer == 0)
            {
                enemyLayer = LayerMask.GetMask("Enemy", "BouncingEnemy");
            }
            
            // 立即计算一次路径
            CalculateLaserPath();
            UpdateLaserVisuals();
            
            Debug.Log($"[LaserController] 激光初始化完成 - 世界空间模式, 最大反射次数: {maxReflections}");
        }
        
        #endregion
        
        #region Input Handling
        
        /// <summary>
        /// 处理旋转输入（支持触摸和鼠标）
        /// </summary>
        private void HandleRotationInput()
        {
            // 触摸输入
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                HandleTouchInput(touch);
            }
            // 鼠标输入（编辑器测试用）
            else
            {
                HandleMouseInput();
            }
        }
        
        /// <summary>
        /// 处理触摸输入
        /// </summary>
        private void HandleTouchInput(Touch touch)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    lastTouchPosition = touch.position;
                    isDragging = true;
                    break;
                    
                case TouchPhase.Moved:
                    if (isDragging)
                    {
                        float deltaX = touch.position.x - lastTouchPosition.x;
                        ApplyRotation(deltaX);
                        lastTouchPosition = touch.position;
                    }
                    break;
                    
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isDragging = false;
                    break;
            }
        }
        
        /// <summary>
        /// 处理鼠标输入
        /// </summary>
        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                lastTouchPosition = Input.mousePosition;
                isDragging = true;
            }
            else if (Input.GetMouseButton(0) && isDragging)
            {
                float deltaX = Input.mousePosition.x - lastTouchPosition.x;
                ApplyRotation(deltaX);
                lastTouchPosition = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }
        }
        
        /// <summary>
        /// 应用旋转
        /// </summary>
        private void ApplyRotation(float deltaX)
        {
            float rotationDelta = -deltaX * rotationSpeed * Time.deltaTime;
            currentAngle = Mathf.Clamp(currentAngle + rotationDelta, minAngle, maxAngle);
            
            if (laserPivot != null)
            {
                laserPivot.localRotation = Quaternion.Euler(0, 0, currentAngle);
            }
        }
        
        #endregion
        
        #region Laser Path Calculation
        
        /// <summary>
        /// 计算激光路径（包含反射）
        /// </summary>
        private void CalculateLaserPath()
        {
            laserPoints.Clear();
            hitEnemy = false;
            currentReflectionCount = 0;
            
            if (laserPivot == null) return;
            
            // 起点（世界坐标）
            Vector3 startPoint = CalculateStartPointWorld();
            laserPoints.Add(startPoint);
            
            // 初始方向（LaserPivot 的本地 Y 轴方向）
            Vector3 currentDirection = laserPivot.up;
            Vector3 currentPoint = startPoint;
            float remainingLength = laserLength;
            
            // 循环计算反射路径
            while (remainingLength > 0 && currentReflectionCount <= maxReflections)
            {
                // 执行射线检测
                RaycastResult result = PerformRaycast(currentPoint, currentDirection, remainingLength);
                
                if (result.hitSomething)
                {
                    // 添加击中点
                    laserPoints.Add(result.hitPoint);
                    remainingLength -= result.hitDistance;
                    
                    if (result.hitEnemy)
                    {
                        // 击中敌人，停止
                        hitEnemy = true;
                        hitPoint = result.hitPoint;
                        
                        if (showDebugLogs)
                        {
                            Debug.Log($"[LaserController] 击中敌人，激光停止 - 位置: {result.hitPoint}");
                        }
                        break;
                    }
                    else if (result.hitWall)
                    {
                        // 击中墙壁，检查是否可以反射
                        if (currentReflectionCount < maxReflections && remainingLength > 0.1f)
                        {
                            // 计算反射方向
                            currentDirection = Vector3.Reflect(currentDirection, result.hitNormal);
        
                            // 【修复】将起点沿反射方向偏移一小段距离，避免立即再次检测到同一面墙
                            currentPoint = result.hitPoint + currentDirection * 0.01f;
        
                            currentReflectionCount++;
        
                            if (showDebugLogs)
                            {
                                Debug.Log($"[LaserController] 反射 #{currentReflectionCount} - 位置: {result.hitPoint}, 新方向: {currentDirection}");
                            }
                        }
                        else
                        {
                            // 达到最大反射次数或剩余长度不足，停止
                            if (showDebugLogs)
                            {
                                Debug.Log($"[LaserController] 达到最大反射次数或剩余长度不足，停止");
                            }
                            break;
                        }
                    }
                }
                else
                {
                    // 没有击中任何东西，延伸到最大长度
                    Vector3 endPoint = currentPoint + currentDirection * remainingLength;
                    laserPoints.Add(endPoint);
                    
                    if (showDebugLogs)
                    {
                        Debug.Log($"[LaserController] 激光延伸到最大长度 - 终点: {endPoint}");
                    }
                    break;
                }
            }
            
            // 确保至少有2个点
            if (laserPoints.Count < 2)
            {
                Vector3 endPoint = startPoint + laserPivot.up * laserLength;
                laserPoints.Add(endPoint);
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
            
            // 根据优先级处理（优先敌人）
            if (hasEnemyHit && hasWallHit)
            {
                // 两者都击中，优先敌人（无论距离）
                result.hitSomething = true;
                result.hitEnemy = true;
                result.hitPoint = enemyHit.point;
                result.hitNormal = enemyHit.normal;
                result.hitDistance = enemyHit.distance;
                result.hitCollider = enemyHit.collider;
            }
            else if (hasEnemyHit)
            {
                // 只击中敌人
                result.hitSomething = true;
                result.hitEnemy = true;
                result.hitPoint = enemyHit.point;
                result.hitNormal = enemyHit.normal;
                result.hitDistance = enemyHit.distance;
                result.hitCollider = enemyHit.collider;
            }
            else if (hasWallHit)
            {
                // 只击中墙壁
                result.hitSomething = true;
                result.hitWall = true;
                result.hitPoint = wallHit.point;
                result.hitNormal = wallHit.normal;
                result.hitDistance = wallHit.distance;
                result.hitCollider = wallHit.collider;
            }
            
            return result;
        }
        
        /// <summary>
        /// 计算起点的世界坐标
        /// </summary>
        private Vector3 CalculateStartPointWorld()
        {
            if (laserPivot == null) return Vector3.zero;
            
            Vector3 localStartPoint = new Vector3(0, startPointOffset, 0);
            return laserPivot.TransformPoint(localStartPoint);
        }
        
        #endregion
        
        #region Laser Visuals Update
        
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
            UpdateLaserWidth();
            
            // 更新 VFX 位置和缩放
            UpdateVFXPositions();
            UpdateVFXScale();
        }
        
        /// <summary>
        /// 更新激光宽度
        /// </summary>
        private void UpdateLaserWidth()
        {
            if (lineRenderer == null) return;
            
            lineRenderer.startWidth = laserWidth;
            lineRenderer.endWidth = laserWidth;
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
            
            // EndVFX 在激光终点（最后一个点）
            if (endVFX != null)
            {
                endVFX.position = laserPoints[laserPoints.Count - 1];
            }
        }
        
        /// <summary>
        /// 更新 VFX 子节点缩放（根据激光宽度）
        /// </summary>
        private void UpdateVFXScale()
        {
            float widthRatio = laserWidth / baseWidth;
            float targetScale = baseVFXScale * widthRatio;
            
            // 缩放 StartVFX 的子节点
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
            
            // 缩放 EndVFX 的子节点
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
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// 设置激光长度
        /// </summary>
        public void SetLength(float length)
        {
            laserLength = Mathf.Max(0, length);
            CalculateLaserPath();
            UpdateLaserVisuals();
        }
        
        /// <summary>
        /// 设置激光宽度
        /// </summary>
        public void SetWidth(float width)
        {
            laserWidth = Mathf.Max(0.01f, width);
            UpdateLaserVisuals();
        }
        
        /// <summary>
        /// 设置旋转角度
        /// </summary>
        public void SetRotation(float angle)
        {
            currentAngle = Mathf.Clamp(angle, minAngle, maxAngle);
            if (laserPivot != null)
            {
                laserPivot.localRotation = Quaternion.Euler(0, 0, currentAngle);
            }
            CalculateLaserPath();
            UpdateLaserVisuals();
        }
        
        /// <summary>
        /// 设置最大反射次数（由技能等级决定）
        /// </summary>
        /// <param name="level">技能等级：1=1次反射, 3=2次反射, 5=3次反射</param>
        public void SetReflectionLevel(int level)
        {
            if (level <= 0)
            {
                maxReflections = 0;
            }
            else if (level <= 2)
            {
                maxReflections = 1;
            }
            else if (level <= 4)
            {
                maxReflections = 2;
            }
            else
            {
                maxReflections = 3;
            }
            
            if (showDebugLogs)
            {
                Debug.Log($"[LaserController] 反射等级设置 - 技能等级: {level}, 最大反射次数: {maxReflections}");
            }
            
            CalculateLaserPath();
            UpdateLaserVisuals();
        }
        
        /// <summary>
        /// 直接设置最大反射次数
        /// </summary>
        public void SetMaxReflections(int count)
        {
            maxReflections = Mathf.Clamp(count, 0, 3);
            CalculateLaserPath();
            UpdateLaserVisuals();
        }
        
        /// <summary>
        /// 获取当前旋转角度
        /// </summary>
        public float GetCurrentAngle()
        {
            return currentAngle;
        }
        
        /// <summary>
        /// 获取激光起点世界坐标
        /// </summary>
        public Vector3 GetStartPoint()
        {
            return laserPoints.Count > 0 ? laserPoints[0] : Vector3.zero;
        }
        
        /// <summary>
        /// 获取激光终点世界坐标
        /// </summary>
        public Vector3 GetEndPoint()
        {
            return laserPoints.Count > 0 ? laserPoints[laserPoints.Count - 1] : Vector3.zero;
        }
        
        /// <summary>
        /// 获取所有激光路径点
        /// </summary>
        public List<Vector3> GetLaserPoints()
        {
            return new List<Vector3>(laserPoints);
        }
        
        /// <summary>
        /// 获取当前反射次数
        /// </summary>
        public int GetCurrentReflectionCount()
        {
            return currentReflectionCount;
        }
        
        /// <summary>
        /// 激光是否击中敌人
        /// </summary>
        public bool HasHitEnemy()
        {
            return hitEnemy;
        }
        
        /// <summary>
        /// 获取激光方向（第一段）
        /// </summary>
        public Vector3 GetLaserDirection()
        {
            if (laserPoints.Count < 2) return Vector3.up;
            return (laserPoints[1] - laserPoints[0]).normalized;
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;
            if (laserPoints == null || laserPoints.Count < 2) return;
            
            // 绘制激光路径
            for (int i = 0; i < laserPoints.Count; i++)
            {
                // 绘制点
                if (i == 0)
                {
                    // 起点（绿色）
                    Gizmos.color = Color.green;
                }
                else if (i == laserPoints.Count - 1)
                {
                    // 终点（红色或橙色）
                    Gizmos.color = hitEnemy ? Color.red : new Color(1f, 0.5f, 0f);
                }
                else
                {
                    // 反射点（蓝色）
                    Gizmos.color = Color.blue;
                }
                Gizmos.DrawWireSphere(laserPoints[i], 0.3f);
                
                // 绘制线段
                if (i < laserPoints.Count - 1)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(laserPoints[i], laserPoints[i + 1]);
                }
            }
            
            // 绘制反射次数标签位置
            if (currentReflectionCount > 0)
            {
                Gizmos.color = Color.cyan;
                for (int i = 1; i < laserPoints.Count - 1; i++)
                {
                    Gizmos.DrawWireCube(laserPoints[i], Vector3.one * 0.2f);
                }
            }
        }
        
        #endregion
        
        #region Helper Structs
        
        /// <summary>
        /// Raycast 结果数据结构
        /// </summary>
        private struct RaycastResult
        {
            public bool hitSomething;
            public bool hitEnemy;
            public bool hitWall;
            public Vector3 hitPoint;
            public Vector3 hitNormal;
            public float hitDistance;
            public Collider2D hitCollider;
        }
        
        #endregion
    }
}