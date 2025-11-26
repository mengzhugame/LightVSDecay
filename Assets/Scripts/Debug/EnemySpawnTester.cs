using UnityEngine;
using LightVsDecay.Core.Pool;

namespace LightVsDecay
{
    /// <summary>
    /// 生成区域类型
    /// </summary>
    public enum SpawnAreaType
    {
        ScreenEdge,     // 屏幕边缘（四周）
        TopOnly,        // 仅上方（竖屏推荐）
        TopThird,       // 上方1/3区域（你要求的）
        Custom          // 自定义矩形区域
    }
    
    /// <summary>
    /// 敌人生成测试器
    /// 【修改】支持多种生成区域配置，适配竖屏游戏
    /// </summary>
    public class EnemySpawnTester : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生成区域设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("生成区域设置")]
        [Tooltip("生成区域类型")]
        [SerializeField] private SpawnAreaType spawnAreaType = SpawnAreaType.TopThird;
        
        [Tooltip("参考相机（用于计算屏幕边界）")]
        [SerializeField] private Camera gameCamera;
        
        [Tooltip("屏幕外偏移量（生成点距离屏幕边缘的距离）")]
        [SerializeField] private float outsideOffset = 1.5f;
        
        [Header("自定义区域（仅当SpawnAreaType为Custom时生效）")]
        [Tooltip("自定义生成区域最小点")]
        [SerializeField] private Vector2 customAreaMin = new Vector2(-4f, 8f);
        
        [Tooltip("自定义生成区域最大点")]
        [SerializeField] private Vector2 customAreaMax = new Vector2(4f, 12f);
        
        [Header("测试配置")]
        [SerializeField] private int slimeCount = 30;
        [SerializeField] private int tankCount = 10;
        [SerializeField] private int rusherCount = 10;
        
        [Header("运行时信息")]
        [SerializeField] private bool showRuntimeInfo = true;
        [SerializeField] private bool showSpawnArea = true;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Rect screenBounds;      // 屏幕边界（世界坐标）
        private Rect spawnArea;         // 实际生成区域
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            if (gameCamera == null)
            {
                gameCamera = Camera.main;
            }
        }
        
        private void Start()
        {
            CalculateSpawnArea();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生成区域计算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 计算生成区域
        /// </summary>
        private void CalculateSpawnArea()
        {
            if (gameCamera == null)
            {
                Debug.LogError("[EnemySpawnTester] 未找到相机！");
                return;
            }
            
            // 计算相机可视范围（世界坐标）
            float camHeight = gameCamera.orthographicSize * 2f;
            float camWidth = camHeight * gameCamera.aspect;
            Vector3 camPos = gameCamera.transform.position;
            
            screenBounds = new Rect(
                camPos.x - camWidth / 2f,
                camPos.y - camHeight / 2f,
                camWidth,
                camHeight
            );
            
            // 根据类型计算生成区域
            switch (spawnAreaType)
            {
                case SpawnAreaType.TopOnly:
                    // 屏幕上方外，宽度与屏幕相同
                    spawnArea = new Rect(
                        screenBounds.xMin,
                        screenBounds.yMax + outsideOffset,
                        screenBounds.width,
                        outsideOffset * 2f
                    );
                    break;
                    
                case SpawnAreaType.TopThird:
                    // 屏幕外上方1/3区域
                    // 左右范围：屏幕宽度的中间1/3（避免角落）
                    // 上下范围：屏幕上边缘外
                    float thirdWidth = screenBounds.width / 3f;
                    spawnArea = new Rect(
                        screenBounds.xMin + thirdWidth,           // 从左边1/3开始
                        screenBounds.yMax + outsideOffset,        // 屏幕上方外
                        thirdWidth,                               // 宽度为1/3
                        outsideOffset * 3f                        // 高度
                    );
                    break;
                    
                case SpawnAreaType.Custom:
                    // 使用自定义区域
                    spawnArea = new Rect(
                        customAreaMin.x,
                        customAreaMin.y,
                        customAreaMax.x - customAreaMin.x,
                        customAreaMax.y - customAreaMin.y
                    );
                    break;
                    
                case SpawnAreaType.ScreenEdge:
                default:
                    // 三边（左、上、右）
                    // 这种情况特殊处理，在GetRandomSpawnPosition中处理
                    spawnArea = screenBounds;
                    break;
            }
            
            Debug.Log($"[EnemySpawnTester] 屏幕范围: {screenBounds}, 生成区域: {spawnArea}");
        }
        
        /// <summary>
        /// 获取随机生成位置
        /// </summary>
        private Vector3 GetRandomSpawnPosition()
        {
            float x, y;
            
            switch (spawnAreaType)
            {
                case SpawnAreaType.ScreenEdge:
                    // 三边随机（左、上、右）
                    return GetScreenEdgePosition();
                    
                case SpawnAreaType.TopOnly:
                case SpawnAreaType.TopThird:
                case SpawnAreaType.Custom:
                default:
                    // 在生成区域内随机
                    x = Random.Range(spawnArea.xMin, spawnArea.xMax);
                    y = Random.Range(spawnArea.yMin, spawnArea.yMax);
                    return new Vector3(x, y, 0f);
            }
        }
        
        /// <summary>
        /// 获取屏幕边缘生成位置（三边）
        /// </summary>
        private Vector3 GetScreenEdgePosition()
        {
            int side = Random.Range(0, 3); // 0=左, 1=右, 2=上
            float x, y;
            
            switch (side)
            {
                case 0: // 左边
                    x = screenBounds.xMin - outsideOffset;
                    y = Random.Range(screenBounds.yMin, screenBounds.yMax);
                    break;
                case 1: // 右边
                    x = screenBounds.xMax + outsideOffset;
                    y = Random.Range(screenBounds.yMin, screenBounds.yMax);
                    break;
                default: // 上边
                    x = Random.Range(screenBounds.xMin, screenBounds.xMax);
                    y = screenBounds.yMax + outsideOffset;
                    break;
            }
            
            return new Vector3(x, y, 0f);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 测试方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [ContextMenu("Test: Spawn 50 Mixed Enemies")]
        public void TestSpawn50Mixed()
        {
            if (!ValidatePoolManager()) return;
            
            SpawnEnemies(EnemyType.Slime, 30);
            SpawnEnemies(EnemyType.Tank, 10);
            SpawnEnemies(EnemyType.Rusher, 10);
            
            Debug.Log($"[EnemySpawnTester] 已生成50个混合敌人");
        }
        
        [ContextMenu("Test: Spawn Slimes")]
        public void TestSpawnSlimes()
        {
            if (!ValidatePoolManager()) return;
            
            SpawnEnemies(EnemyType.Slime, slimeCount);
            Debug.Log($"[EnemySpawnTester] 已生成 {slimeCount} 个 Slime");
        }
        
        [ContextMenu("Test: Spawn Tanks")]
        public void TestSpawnTanks()
        {
            if (!ValidatePoolManager()) return;
            
            SpawnEnemies(EnemyType.Tank, tankCount);
            Debug.Log($"[EnemySpawnTester] 已生成 {tankCount} 个 Tank");
        }
        
        [ContextMenu("Test: Spawn Rushers")]
        public void TestSpawnRushers()
        {
            if (!ValidatePoolManager()) return;
            
            SpawnEnemies(EnemyType.Rusher, rusherCount);
            Debug.Log($"[EnemySpawnTester] 已生成 {rusherCount} 个 Rusher");
        }
        
        [ContextMenu("Test: Despawn All")]
        public void TestDespawnAll()
        {
            if (!ValidatePoolManager()) return;
            
            EnemyPoolManager.Instance.DespawnAll();
            Debug.Log("[EnemySpawnTester] 已回收所有敌人");
        }
        
        [ContextMenu("Test: Stress Test (200 enemies)")]
        public void TestStress200()
        {
            if (!ValidatePoolManager()) return;
            
            SpawnEnemies(EnemyType.Slime, 200);
            Debug.Log("[EnemySpawnTester] 压力测试：尝试生成200个Slime");
        }
        
        [ContextMenu("Recalculate Spawn Area")]
        public void RecalculateSpawnArea()
        {
            CalculateSpawnArea();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生成逻辑
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void SpawnEnemies(EnemyType type, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = GetRandomSpawnPosition();
                EnemyPoolManager.Instance.Spawn(type, spawnPos);
            }
        }
        
        private bool ValidatePoolManager()
        {
            if (EnemyPoolManager.Instance == null)
            {
                Debug.LogError("[EnemySpawnTester] EnemyPoolManager 未初始化！");
                return false;
            }
            return true;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时UI
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnGUI()
        {
            if (!showRuntimeInfo || !Application.isPlaying) return;
            if (EnemyPoolManager.Instance == null) return;
            
            GUILayout.BeginArea(new Rect(10, 200, 220, 350));
            
            GUILayout.Label("=== 敌人生成测试 ===");
            GUILayout.Label($"生成模式: {spawnAreaType}");
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("生成50个混合敌人"))
            {
                TestSpawn50Mixed();
            }
            
            if (GUILayout.Button($"生成 {slimeCount} Slime"))
            {
                TestSpawnSlimes();
            }
            
            if (GUILayout.Button($"生成 {tankCount} Tank"))
            {
                TestSpawnTanks();
            }
            
            if (GUILayout.Button($"生成 {rusherCount} Rusher"))
            {
                TestSpawnRushers();
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("回收所有敌人"))
            {
                TestDespawnAll();
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("压力测试 (200个)"))
            {
                TestStress200();
            }
            
            GUILayout.Space(10);
            GUILayout.Label($"当前敌人数: {EnemyPoolManager.Instance.TotalActiveEnemies}");
            
            GUILayout.EndArea();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器可视化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDrawGizmos()
        {
            if (!showSpawnArea) return;
            
            // 运行时使用计算好的区域
            if (Application.isPlaying && gameCamera != null)
            {
                // 绘制屏幕边界（白色）
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                DrawRectGizmo(screenBounds);
                
                // 绘制生成区域（绿色）
                Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
                DrawRectGizmo(spawnArea);
            }
            else
            {
                // 编辑器模式：绘制自定义区域预览
                Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
                Rect previewArea = new Rect(
                    customAreaMin.x,
                    customAreaMin.y,
                    customAreaMax.x - customAreaMin.x,
                    customAreaMax.y - customAreaMin.y
                );
                DrawRectGizmo(previewArea);
            }
        }
        
        private void DrawRectGizmo(Rect rect)
        {
            Vector3 center = new Vector3(rect.center.x, rect.center.y, 0f);
            Vector3 size = new Vector3(rect.width, rect.height, 0.1f);
            Gizmos.DrawCube(center, size);
            
            // 绘制边框
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 1f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}