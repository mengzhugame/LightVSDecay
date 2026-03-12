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

        [Tooltip("墙壁检测层")]
        [SerializeField] private LayerMask wallLayer;
        
        [Tooltip("敌人检测层")]
        [SerializeField] private LayerMask enemyLayer;
        
        [Header("VFX 缩放")]
        [Tooltip("基础宽度（对应VFX缩放为1）")]
        [SerializeField] private float baseWidth = 0.5f;
        
        [Tooltip("基础VFX缩放值")]
        [SerializeField] private float baseVFXScale = 1f;

        [Header("调试")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool showDebugInfo = false;
        
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
        
        private void LateUpdate()
        {
            UpdateLaserVisuals();
        }
        /// <summary>
        /// 主动强制更新路径（由 LaserController 在 Update 伤害检测前调用）
        /// </summary>
        public void ForceUpdatePath()
        {
            CalculateLaserPath();
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
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 激光路径计算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void CalculateLaserPath()
        {
            laserPoints.Clear();
            laserSegments.Clear();
            hitEnemy = false;
            
            if (laserPivot == null)
            {
                Debug.LogError("[LaserBeam] laserPivot 为空！无法计算激光路径");
                return;
            }
            
            // 起点（本地 0,0,0 转换为世界坐标）
            Vector3 startPoint = laserPivot.TransformPoint(Vector3.zero);
            laserPoints.Add(startPoint);
            
            // 计算激光方向（本地 Y 轴在世界空间中的方向）
            Vector3 localEndPoint = new Vector3(0, maxLength, 0);
            Vector3 worldEndPoint = laserPivot.TransformPoint(localEndPoint);
            Vector3 direction = (worldEndPoint - startPoint).normalized;
            
            // 只检测敌人（不检测墙壁）
            RaycastHit2D enemyHit = Physics2D.Raycast(startPoint, direction, maxLength, enemyLayer);
            
            Vector3 endPoint;
            
            if (enemyHit.collider != null)
            {
                // 击中敌人
                hitEnemy = true;
                hitPoint = enemyHit.point;
                currentHit = enemyHit;
                endPoint = enemyHit.point;
                
                if (showDebugInfo)
                {
                    Debug.Log($"[LaserBeam] 击中敌人 - 位置: {enemyHit.point}");
                }
            }
            else
            {
                // 没有击中任何东西，延伸到最大长度
                endPoint = startPoint + direction * maxLength;
                currentHit = default;
            }
            
            laserPoints.Add(endPoint);
            
            // 记录激光段
            laserSegments.Add(new LaserSegment
            {
                startPoint = startPoint,
                endPoint = endPoint,
                length = Vector3.Distance(startPoint, endPoint)
            });
            
            if (showDebugInfo)
            {
                Debug.Log($"[LaserBeam] 路径计算完成 - 起点: {startPoint}, 终点: {endPoint}");
            }
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
        /// 设置激光颜色（通过 MaterialPropertyBlock 设置 Shader 的 _BaseColor）
        /// </summary>
        public void SetColor(Color color)
        {
            if (lineRenderer == null) return;

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
        /// 是否击中敌人
        /// </summary>
        public bool HasHitEnemy() => hitEnemy;
        /// <summary>
        /// 获取击中点
        /// </summary>
        public Vector3 GetHitPoint() => hitPoint;
        /// <summary>
        /// 手动设置 LaserPivot（由 LaserController 调用）
        /// </summary>
        public void SetLaserPivot(Transform pivot)
        {
            laserPivot = pivot;
            
            if (pivot != null)
            {
                if(showDebugInfo)
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
    }
}