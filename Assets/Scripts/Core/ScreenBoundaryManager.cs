// ============================================================
// ScreenBoundaryManager.cs
// 文件位置: Assets/Scripts/Core/ScreenBoundaryManager.cs
// 用途：自适应屏幕空气墙系统 - 支持物理碰撞、激光折射、弹珠台玩法
// ============================================================

using UnityEngine;

namespace LightVsDecay.Core
{
    /// <summary>
    /// 空气墙边界类型
    /// </summary>
    public enum BoundaryEdge
    {
        Top,
        Bottom,
        Left,
        Right
    }
    
    /// <summary>
    /// 屏幕边界管理器
    /// 在游戏世界坐标中生成4个BoxCollider2D，自适应任何屏幕分辨率
    /// </summary>
    public class ScreenBoundaryManager : Singleton<ScreenBoundaryManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("引用")]
        [Tooltip("参考相机（用于计算屏幕边界），默认使用 Main Camera")]
        [SerializeField] private Camera gameCamera;
        
        [Header("墙体设置")]
        [Tooltip("墙体厚度（Unity单位）")]
        [SerializeField] private float wallThickness = 1f;
        
        [Tooltip("墙体向外延伸量（防止角落缝隙）")]
        [SerializeField] private float wallExtension = 0.5f;
        
        [Tooltip("是否启用底部墙（某些游戏可能不需要）")]
        [SerializeField] private bool enableBottomWall = true;
        
        [Header("物理材质")]
        [Tooltip("墙体物理材质（摩擦力=0，弹性=0）")]
        [SerializeField] private PhysicsMaterial2D wallPhysicsMaterial;
        
        [Header("视觉预留（Phase 2）")]
        [Tooltip("墙体视觉 Prefab（可选，Phase 1 不使用）")]
        [SerializeField] private GameObject wallVisualPrefab;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool showGizmos = true;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private GameObject wallContainer;
        private GameObject[] walls = new GameObject[4]; // Top, Bottom, Left, Right
        private BoxCollider2D[] wallColliders = new BoxCollider2D[4];
        
        // 屏幕边界缓存（世界坐标）
        private float screenLeft;
        private float screenRight;
        private float screenTop;
        private float screenBottom;
        private float screenWidth;
        private float screenHeight;
        
        // Layer 缓存
        private int wallLayerIndex;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>屏幕左边界（世界坐标）</summary>
        public float ScreenLeft => screenLeft;
        
        /// <summary>屏幕右边界（世界坐标）</summary>
        public float ScreenRight => screenRight;
        
        /// <summary>屏幕上边界（世界坐标）</summary>
        public float ScreenTop => screenTop;
        
        /// <summary>屏幕下边界（世界坐标）</summary>
        public float ScreenBottom => screenBottom;
        
        /// <summary>屏幕宽度（世界单位）</summary>
        public float ScreenWidth => screenWidth;
        
        /// <summary>屏幕高度（世界单位）</summary>
        public float ScreenHeight => screenHeight;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            // 获取相机
            if (gameCamera == null)
            {
                gameCamera = Camera.main;
            }
            
            // 缓存 Layer
            wallLayerIndex = LayerMask.NameToLayer(GameConstants.WALL_LAYER);
            if (wallLayerIndex == -1)
            {
                Debug.LogError($"[ScreenBoundaryManager] Layer '{GameConstants.WALL_LAYER}' 不存在！请在 Unity 中创建。");
                return;
            }
            
            // 创建墙体容器
            CreateWallContainer();
            
            // 计算边界并生成墙体
            CalculateScreenBounds();
            CreateAllWalls();
        }
        
        private void Start()
        {
            // 再次验证（防止 Awake 顺序问题）
            if (gameCamera == null)
            {
                gameCamera = Camera.main;
                if (gameCamera != null)
                {
                    CalculateScreenBounds();
                    UpdateAllWalls();
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 创建墙体容器
        /// </summary>
        private void CreateWallContainer()
        {
            wallContainer = new GameObject("ScreenBoundaries");
            wallContainer.transform.SetParent(transform);
            wallContainer.transform.localPosition = Vector3.zero;
        }
        
        /// <summary>
        /// 计算屏幕边界（世界坐标）
        /// </summary>
        private void CalculateScreenBounds()
        {
            if (gameCamera == null) return;
            
            // 正交相机：Height = orthographicSize * 2
            screenHeight = gameCamera.orthographicSize * 2f;
            screenWidth = screenHeight * gameCamera.aspect;
            
            // 相机位置为中心
            Vector3 camPos = gameCamera.transform.position;
            
            screenLeft = camPos.x - screenWidth * 0.5f;
            screenRight = camPos.x + screenWidth * 0.5f;
            screenTop = camPos.y + screenHeight * 0.5f;
            screenBottom = camPos.y - screenHeight * 0.5f;
            
            if (showDebugInfo)
            {
                Debug.Log($"[ScreenBoundaryManager] 屏幕边界计算完成:");
                Debug.Log($"  宽度: {screenWidth:F2}, 高度: {screenHeight:F2}");
                Debug.Log($"  Left: {screenLeft:F2}, Right: {screenRight:F2}");
                Debug.Log($"  Top: {screenTop:F2}, Bottom: {screenBottom:F2}");
                Debug.Log($"  相机 Aspect: {gameCamera.aspect:F2}, OrthoSize: {gameCamera.orthographicSize:F2}");
            }
        }
        
        /// <summary>
        /// 创建所有墙体
        /// </summary>
        private void CreateAllWalls()
        {
            CreateWall(BoundaryEdge.Top, 0);
            CreateWall(BoundaryEdge.Bottom, 1);
            CreateWall(BoundaryEdge.Left, 2);
            CreateWall(BoundaryEdge.Right, 3);
            
            // 是否禁用底部墙
            if (!enableBottomWall && walls[1] != null)
            {
                walls[1].SetActive(false);
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[ScreenBoundaryManager] 空气墙创建完成");
            }
        }
        
        /// <summary>
        /// 创建单个墙体
        /// </summary>
        private void CreateWall(BoundaryEdge edge, int index)
        {
            // 创建 GameObject
            string wallName = $"Wall_{edge}";
            GameObject wall = new GameObject(wallName);
            wall.transform.SetParent(wallContainer.transform);
            
            // 设置 Layer 和 Tag
            wall.layer = wallLayerIndex;
            wall.tag = GameConstants.WALL_TAG;
            
            // 添加 Collider
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            
            // 设置物理材质
            if (wallPhysicsMaterial != null)
            {
                collider.sharedMaterial = wallPhysicsMaterial;
            }
            
            // 添加 Rigidbody2D（Static，不受物理影响）
            Rigidbody2D rb = wall.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            
            // 存储引用
            walls[index] = wall;
            wallColliders[index] = collider;
            
            // 设置位置和大小
            UpdateWallTransform(edge, index);
            
            // 添加碰撞事件组件（用于视觉反馈）
            WallCollisionHandler handler = wall.AddComponent<WallCollisionHandler>();
            handler.Initialize(edge);
        }
        
        /// <summary>
        /// 更新墙体位置和大小
        /// </summary>
        private void UpdateWallTransform(BoundaryEdge edge, int index)
        {
            if (walls[index] == null || wallColliders[index] == null) return;
            
            Vector3 position = Vector3.zero;
            Vector2 size = Vector2.zero;
            
            float extendedWidth = screenWidth + wallExtension * 2f;
            float extendedHeight = screenHeight + wallExtension * 2f;
            
            switch (edge)
            {
                case BoundaryEdge.Top:
                    position = new Vector3(0f, screenTop + wallThickness * 0.5f, 0f);
                    size = new Vector2(extendedWidth, wallThickness);
                    break;
                    
                case BoundaryEdge.Bottom:
                    position = new Vector3(0f, screenBottom - wallThickness * 0.5f, 0f);
                    size = new Vector2(extendedWidth, wallThickness);
                    break;
                    
                case BoundaryEdge.Left:
                    position = new Vector3(screenLeft - wallThickness * 0.5f, 0f, 0f);
                    size = new Vector2(wallThickness, extendedHeight);
                    break;
                    
                case BoundaryEdge.Right:
                    position = new Vector3(screenRight + wallThickness * 0.5f, 0f, 0f);
                    size = new Vector2(wallThickness, extendedHeight);
                    break;
            }
            
            walls[index].transform.position = position;
            wallColliders[index].size = size;
        }
        
        /// <summary>
        /// 更新所有墙体（分辨率变化时调用）
        /// </summary>
        private void UpdateAllWalls()
        {
            UpdateWallTransform(BoundaryEdge.Top, 0);
            UpdateWallTransform(BoundaryEdge.Bottom, 1);
            UpdateWallTransform(BoundaryEdge.Left, 2);
            UpdateWallTransform(BoundaryEdge.Right, 3);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 重新计算边界（屏幕分辨率变化时调用）
        /// </summary>
        public void RecalculateBounds()
        {
            CalculateScreenBounds();
            UpdateAllWalls();
            
            if (showDebugInfo)
            {
                Debug.Log("[ScreenBoundaryManager] 边界已重新计算");
            }
        }
        
        /// <summary>
        /// 启用/禁用底部墙
        /// </summary>
        public void SetBottomWallEnabled(bool enabled)
        {
            enableBottomWall = enabled;
            if (walls[1] != null)
            {
                walls[1].SetActive(enabled);
            }
        }
        
        /// <summary>
        /// 获取指定边界的法线（用于激光反射）
        /// </summary>
        public Vector2 GetEdgeNormal(BoundaryEdge edge)
        {
            switch (edge)
            {
                case BoundaryEdge.Top: return Vector2.down;
                case BoundaryEdge.Bottom: return Vector2.up;
                case BoundaryEdge.Left: return Vector2.right;
                case BoundaryEdge.Right: return Vector2.left;
                default: return Vector2.zero;
            }
        }
        
        /// <summary>
        /// 检查点是否在屏幕内
        /// </summary>
        public bool IsPointInScreen(Vector2 point)
        {
            return point.x >= screenLeft && point.x <= screenRight &&
                   point.y >= screenBottom && point.y <= screenTop;
        }
        
        /// <summary>
        /// 将点限制在屏幕内
        /// </summary>
        public Vector2 ClampToScreen(Vector2 point)
        {
            return new Vector2(
                Mathf.Clamp(point.x, screenLeft, screenRight),
                Mathf.Clamp(point.y, screenBottom, screenTop)
            );
        }
        
        /// <summary>
        /// 触发墙体碰撞视觉效果（供外部调用）
        /// </summary>
        public void TriggerWallHitEffect(BoundaryEdge edge, Vector2 hitPoint)
        {
            int index = (int)edge;
            if (index >= 0 && index < walls.Length && walls[index] != null)
            {
                WallCollisionHandler handler = walls[index].GetComponent<WallCollisionHandler>();
                if (handler != null)
                {
                    handler.OnExternalHit(hitPoint);
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Gizmos
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            // 在编辑器中也能预览边界
            Camera cam = gameCamera != null ? gameCamera : Camera.main;
            if (cam == null) return;
            
            float height = cam.orthographicSize * 2f;
            float width = height * cam.aspect;
            Vector3 camPos = cam.transform.position;
            
            // 绘制屏幕边界
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // 青色
            
            Vector3 topLeft = new Vector3(camPos.x - width * 0.5f, camPos.y + height * 0.5f, 0f);
            Vector3 topRight = new Vector3(camPos.x + width * 0.5f, camPos.y + height * 0.5f, 0f);
            Vector3 bottomLeft = new Vector3(camPos.x - width * 0.5f, camPos.y - height * 0.5f, 0f);
            Vector3 bottomRight = new Vector3(camPos.x + width * 0.5f, camPos.y - height * 0.5f, 0f);
            
            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);
            
            // 绘制墙体区域
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // 橙色半透明
            
            float thick = wallThickness;
            
            // Top
            Gizmos.DrawCube(new Vector3(camPos.x, camPos.y + height * 0.5f + thick * 0.5f, 0f), 
                new Vector3(width + wallExtension * 2f, thick, 1f));
            
            // Bottom
            if (enableBottomWall)
            {
                Gizmos.DrawCube(new Vector3(camPos.x, camPos.y - height * 0.5f - thick * 0.5f, 0f), 
                    new Vector3(width + wallExtension * 2f, thick, 1f));
            }
            
            // Left
            Gizmos.DrawCube(new Vector3(camPos.x - width * 0.5f - thick * 0.5f, camPos.y, 0f), 
                new Vector3(thick, height + wallExtension * 2f, 1f));
            
            // Right
            Gizmos.DrawCube(new Vector3(camPos.x + width * 0.5f + thick * 0.5f, camPos.y, 0f), 
                new Vector3(thick, height + wallExtension * 2f, 1f));
        }
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 墙体碰撞处理器（用于视觉反馈）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 墙体碰撞事件处理器
    /// 负责：碰撞检测回调、视觉效果触发（Phase 2）
    /// </summary>
    public class WallCollisionHandler : MonoBehaviour
    {
        private BoundaryEdge edge;
        private SpriteRenderer visualRenderer;
        
        // 闪烁效果参数（Phase 2 使用）
        private float flashDuration = 0.1f;
        private Color flashColor = new Color(0f, 1f, 1f, 0.5f); // 青色
        private Color normalColor = Color.clear;
        private float flashTimer = 0f;
        private bool isFlashing = false;
        
        public void Initialize(BoundaryEdge boundaryEdge)
        {
            edge = boundaryEdge;
            
            // Phase 2: 如果有视觉组件，获取引用
            visualRenderer = GetComponent<SpriteRenderer>();
        }
        
        private void Update()
        {
            // 闪烁效果倒计时
            if (isFlashing && visualRenderer != null)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f)
                {
                    isFlashing = false;
                    visualRenderer.color = normalColor;
                }
            }
        }
        
        private void OnCollisionEnter2D(Collision2D collision)
        {
            // 触发闪烁效果（Phase 2）
            TriggerFlash(collision.GetContact(0).point);
            
            // 调试日志
            #if UNITY_EDITOR
            Debug.Log($"[WallCollisionHandler] {edge} 墙被 {collision.gameObject.name} 撞击");
            #endif
        }
        
        /// <summary>
        /// 外部调用的碰撞效果（如激光反射）
        /// </summary>
        public void OnExternalHit(Vector2 hitPoint)
        {
            TriggerFlash(hitPoint);
        }
        
        /// <summary>
        /// 触发闪烁效果
        /// </summary>
        private void TriggerFlash(Vector2 hitPoint)
        {
            // Phase 2: 实现视觉闪烁
            if (visualRenderer != null)
            {
                isFlashing = true;
                flashTimer = flashDuration;
                visualRenderer.color = flashColor;
            }
            
            // Phase 2: 可以在这里生成粒子效果
            // VFXPoolManager.Instance?.PlayWallHitEffect(hitPoint, ScreenBoundaryManager.Instance.GetEdgeNormal(edge));
        }
        
        /// <summary>
        /// 获取边界类型
        /// </summary>
        public BoundaryEdge GetEdge() => edge;
    }
}