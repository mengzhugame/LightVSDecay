using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Logic.Enemy;

namespace LightVsDecay.Core.Pool
{
    /// <summary>
    /// 敌人类型枚举
    /// 【修改】添加 Drifter 类型
    /// </summary>
    public enum EnemyType
    {
        Slime,      // A - 粘液 - 基础单位
        Tank,       // B - 硬壳 - 高血量
        Rusher,     // C - 速攻虫 - 快速小型
        Drifter     // D - 漂流者 - 被击退时随机左后/右后漂移
    }
    
    /// <summary>
    /// 敌人预制体配置
    /// </summary>
    [System.Serializable]
    public class EnemyPoolConfig
    {
        public EnemyType type;
        public EnemyBlob prefab;
        public int prewarmCount;
        
        [Tooltip("该类型的最大数量（0=使用全局上限）")]
        public int maxCount;
    }
    
    /// <summary>
    /// 敌人对象池管理器
    /// 单例模式，管理所有敌人类型的对象池
    /// </summary>
    public class EnemyPoolManager : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public static EnemyPoolManager Instance { get; private set; }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("敌人预制体配置")]
        [SerializeField] private List<EnemyPoolConfig> enemyConfigs = new List<EnemyPoolConfig>();
        
        [Header("全局设置")]
        [Tooltip("所有敌人的最大总数")]
        [SerializeField] private int globalMaxEnemies = 200;
        
        [Tooltip("池空时是否动态创建")]
        [SerializeField] private bool allowDynamicExpand = true;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Dictionary<EnemyType, ObjectPool<EnemyBlob>> pools;
        private Transform poolContainer;
        
        // 统计
        private int totalActiveEnemies = 0;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前活跃敌人总数</summary>
        public int TotalActiveEnemies => totalActiveEnemies;
        
        /// <summary>是否已达全局上限</summary>
        public bool IsAtGlobalCapacity => totalActiveEnemies >= globalMaxEnemies;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 单例设置
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // 创建容器
            CreatePoolContainer();
            
            // 初始化对象池
            InitializePools();
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            
            // 清理所有池
            ClearAllPools();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void CreatePoolContainer()
        {
            // 创建统一容器，便于层级管理
            GameObject containerGO = new GameObject("[EnemyPool]");
            containerGO.transform.SetParent(transform);
            poolContainer = containerGO.transform;
        }
        
        private void InitializePools()
        {
            pools = new Dictionary<EnemyType, ObjectPool<EnemyBlob>>();
            
            foreach (var config in enemyConfigs)
            {
                if (config.prefab == null)
                {
                    Debug.LogError($"[EnemyPoolManager] {config.type} 的预制体未设置！");
                    continue;
                }
                
                // 为每种类型创建子容器
                GameObject typeContainer = new GameObject($"Pool_{config.type}");
                typeContainer.transform.SetParent(poolContainer);
                
                // 确定该类型的最大数量
                int maxForType = config.maxCount > 0 ? config.maxCount : globalMaxEnemies;
                
                // 创建对象池
                var pool = new ObjectPool<EnemyBlob>(
                    config.prefab,
                    typeContainer.transform,
                    config.prewarmCount,
                    maxForType,
                    allowDynamicExpand
                );
                
                pools[config.type] = pool;
                
                Debug.Log($"[EnemyPoolManager] {config.type} 池初始化完成: 预热{config.prewarmCount}, 上限{maxForType}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 生成敌人
        /// </summary>
        /// <param name="type">敌人类型</param>
        /// <param name="position">生成位置</param>
        /// <param name="rotation">生成旋转（可选）</param>
        /// <returns>敌人实例，如果失败返回null</returns>
        public EnemyBlob Spawn(EnemyType type, Vector3 position, Quaternion rotation = default)
        {
            // 全局上限检查
            if (totalActiveEnemies >= globalMaxEnemies)
            {
                if (showDebugInfo)
                    Debug.LogWarning($"[EnemyPoolManager] 已达全局上限 {globalMaxEnemies}！");
                return null;
            }
            
            // 检查池是否存在
            if (!pools.TryGetValue(type, out var pool))
            {
                Debug.LogError($"[EnemyPoolManager] 未找到 {type} 类型的对象池！");
                return null;
            }
            
            // 从池中获取
            if (rotation == default)
                rotation = Quaternion.identity;
            
            EnemyBlob enemy = pool.Get(position, rotation);
            
            if (enemy != null)
            {
                totalActiveEnemies++;
                
                if (showDebugInfo)
                    Debug.Log($"[EnemyPoolManager] 生成 {type} @ {position}, 当前总数: {totalActiveEnemies}");
            }
            
            return enemy;
        }
        
        /// <summary>
        /// 生成敌人（简化版，只需位置）
        /// </summary>
        public EnemyBlob Spawn(EnemyType type, Vector3 position)
        {
            return Spawn(type, position, Quaternion.identity);
        }
        
        /// <summary>
        /// 回收敌人
        /// </summary>
        public void Despawn(EnemyBlob enemy)
        {
            if (enemy == null) return;
            
            // 通过 PoolKey 找到对应的池
            if (System.Enum.TryParse<EnemyType>(enemy.PoolKey, out var type))
            {
                if (pools.TryGetValue(type, out var pool))
                {
                    pool.Return(enemy);
                    totalActiveEnemies = Mathf.Max(0, totalActiveEnemies - 1);
                    
                    if (showDebugInfo)
                        Debug.Log($"[EnemyPoolManager] 回收 {type}, 剩余总数: {totalActiveEnemies}");
                }
            }
            else
            {
                Debug.LogWarning($"[EnemyPoolManager] 无法解析敌人类型: {enemy.PoolKey}");
                // 降级处理：直接销毁
                Destroy(enemy.gameObject);
                totalActiveEnemies = Mathf.Max(0, totalActiveEnemies - 1);
            }
        }
        
        /// <summary>
        /// 回收所有敌人
        /// </summary>
        public void DespawnAll()
        {
            foreach (var pool in pools.Values)
            {
                pool.ReturnAll();
            }
            totalActiveEnemies = 0;
            
            Debug.Log("[EnemyPoolManager] 所有敌人已回收");
        }
        
        /// <summary>
        /// 清空所有对象池
        /// </summary>
        public void ClearAllPools()
        {
            if (pools == null) return;
            
            foreach (var pool in pools.Values)
            {
                pool.Clear();
            }
            pools.Clear();
            totalActiveEnemies = 0;
            
            Debug.Log("[EnemyPoolManager] 所有对象池已清空");
        }
        
        /// <summary>
        /// 获取指定类型的活跃数量
        /// </summary>
        public int GetActiveCount(EnemyType type)
        {
            if (pools.TryGetValue(type, out var pool))
            {
                return pool.ActiveCount;
            }
            return 0;
        }
        
        /// <summary>
        /// 获取指定类型的可用数量
        /// </summary>
        public int GetAvailableCount(EnemyType type)
        {
            if (pools.TryGetValue(type, out var pool))
            {
                return pool.AvailableCount;
            }
            return 0;
        }
        
        /// <summary>
        /// 检查指定类型的池是否存在
        /// </summary>
        public bool HasPool(EnemyType type)
        {
            return pools != null && pools.ContainsKey(type);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [Header("测试按钮（仅编辑器）")]
        [SerializeField] private EnemyType testSpawnType = EnemyType.Slime;
        [SerializeField] private int testSpawnCount = 50;
        [SerializeField] private float testSpawnRadius = 8f;
        
        /// <summary>
        /// 编辑器测试：批量生成敌人
        /// </summary>
        [ContextMenu("Test: Spawn Enemies")]
        public void TestSpawnEnemies()
        {
            for (int i = 0; i < testSpawnCount; i++)
            {
                // 在圆形区域随机生成
                Vector2 randomPos = Random.insideUnitCircle.normalized * testSpawnRadius;
                Vector3 spawnPos = new Vector3(randomPos.x, randomPos.y, 0f);
                
                Spawn(testSpawnType, spawnPos);
            }
            
            Debug.Log($"[EnemyPoolManager] 测试生成 {testSpawnCount} 个 {testSpawnType}");
        }
        
        /// <summary>
        /// 编辑器测试：回收所有敌人
        /// </summary>
        [ContextMenu("Test: Despawn All")]
        public void TestDespawnAll()
        {
            DespawnAll();
        }
        
        private void OnGUI()
        {
            if (!showDebugInfo || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"=== Enemy Pool Debug ===");
            GUILayout.Label($"Total Active: {totalActiveEnemies} / {globalMaxEnemies}");
            
            if (pools != null)
            {
                foreach (var kvp in pools)
                {
                    GUILayout.Label($"{kvp.Key}: Active={kvp.Value.ActiveCount}, Available={kvp.Value.AvailableCount}");
                }
            }
            GUILayout.EndArea();
        }
#endif
    }
}