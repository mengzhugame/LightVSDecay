using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Logic.Boss;
using LightVsDecay.Logic.Enemy;

namespace LightVsDecay.Core.Pool
{
    /// <summary>
    /// 敌人类型枚举
    /// 【修改】添加 Drifter 类型
    /// </summary>
    public enum EnemyType
    {
        // ── 第一章：黑油怪 ──
        Slime,      // A - 粘液 - 基础单位
        Tank,       // B - 硬壳 - 高血量
        Rusher,     // C - 速攻虫 - 快速小型
        Drifter,    // D - 漂流者 - 被击退时随机左后/右后漂移
        Treasure,   // T - 宝箱怪 - 横穿屏幕，高奖励
        EliteTank,      // 精英坦克 - 独立预制体，不走对象池
        EliteDrifter,   // 精英漂流者 - 独立预制体，不走对象池

        // ── 第二章：熔岩怪 ──
        LavaSplitter,       // 熔岩分裂者 - 死亡时分裂为多个小熔岩
        LavaTank,           // 熔岩坦克   - 高血量，缓慢推进
        LavaExploder,       // 熔岩爆炸者 - 死亡时留下熔岩水坑
        LavaGunner,         // 熔岩炮手   - 远程射击，停在屏幕上方
        EliteLavaSplitter,  // 精英熔岩分裂者
        EliteLavaExploder,  // 精英熔岩爆炸者
        LavaPuddle,         // 熔岩水坑   - 静止地形障碍，阻挡激光

        // ── 第三章：极寒怪 ──
        FrostSlime,             // 冰霜粘液怪 - 基础炮灰
        FrostTank,              // 极寒坦克   - 高血量，极高击退抗性
        FrostCatalyst,          // 极寒催化者 - 死亡时触发范围暴走
        Frostcaster,            // 霜冻施法者 - 停驻后定时召唤冰墙
        EliteIceShieldGuard,    // 精英冰甲卫士 - 前置冰盾，阻断激光
        EliteFrostcaster,       // 精英霜冻施法者 - 一次召唤3~5面冰墙
        IceWall,                // 冰墙 - 静止地形障碍，孵化后消失
        FrostGunner = 22,       // 极寒炮手 - 远程发射冰刺，命中时减缓炮塔旋转

        // ── 第二章补充 ──
        LavaSlime = 21,         // 熔岩粘液 - 分裂者死亡时的分裂产物，仅视觉与第一章不同
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

        [Tooltip("所属章节索引（0=Ch1, 1=Ch2, 2=Ch3，-1=全章节通用）")]
        public int chapterIndex = -1;

        [Tooltip("是否使用对象池。普通怪（数量多）填 true；精英怪/障碍物（1-2只）填 false，每次 Instantiate/Destroy，无池开销")]
        public bool usePool = true;
    }
    
    /// <summary>
    /// 敌人对象池管理器
    /// 单例模式，管理所有敌人类型的对象池
    /// </summary>
    public class EnemyPoolManager : PersistentSingleton<EnemyPoolManager>
    {
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
        private Dictionary<EnemyType, EnemyPoolConfig> configs;   // 全类型配置查询表（含非池化）
        private HashSet<EnemyBlob> nonPooledActive;               // 追踪非池化（精英怪）存活实例
        private Transform poolContainer;

        // 预热目标表：InitializeForChapter 不再同步预热，由 WarmupCoroutine 分帧完成
        private Dictionary<EnemyType, int> prewarmTargets = new Dictionary<EnemyType, int>();

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
        
        protected override void OnSingletonAwake()
        {
            // 创建容器
            CreatePoolContainer();

            // 对象池由 GameManager.ApplyChapterConfig() 按章节按需初始化
            pools         = new Dictionary<EnemyType, ObjectPool<EnemyBlob>>();
            configs       = new Dictionary<EnemyType, EnemyPoolConfig>();
            prewarmTargets = new Dictionary<EnemyType, int>();
            nonPooledActive = new HashSet<EnemyBlob>();
        }
        
        protected override void OnSingletonDestroy()
        {
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
        
        /// <summary>
        /// 按章节初始化对象池结构（由 GameManager.ApplyChapterConfig 调用）。
        /// chapterIndex=-1 的配置属于全章节通用类型，始终加载。
        ///
        /// ★ 性能优化：此方法只建池容器和注册配置，不做任何 Instantiate。
        ///   实际预热由 GameManager 调用 <see cref="WarmupCoroutine"/> 分帧完成，
        ///   避免单帧 50~80 次 Instantiate 导致进入战斗时 15fps 卡顿。
        /// </summary>
        public void InitializeForChapter(int chapterIndex)
        {
            // 清除上一章节的池（销毁所有实例）
            ClearAllPools();

            // 销毁旧的子容器节点，保持层级整洁
            for (int i = poolContainer.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(poolContainer.GetChild(i).gameObject);
            }

            pools          = new Dictionary<EnemyType, ObjectPool<EnemyBlob>>();
            configs        = new Dictionary<EnemyType, EnemyPoolConfig>();
            prewarmTargets = new Dictionary<EnemyType, int>();
            nonPooledActive = new HashSet<EnemyBlob>();

            int poolCount = 0;
            int directCount = 0;

            foreach (var config in enemyConfigs)
            {
                // 跳过不属于当前章节的配置（-1 表示全章节通用）
                if (config.chapterIndex != -1 && config.chapterIndex != chapterIndex)
                    continue;

                if (config.prefab == null)
                {
                    GameLogger.LogError($"[EnemyPoolManager] {config.type} 的预制体未设置！");
                    continue;
                }

                // 登记到全类型查询表
                configs[config.type] = config;

                if (config.usePool)
                {
                    // ── 普通怪：只建空池（initialSize = 0），避免构造函数内同步 Prewarm ──
                    GameObject typeContainer = new GameObject($"Pool_{config.type}");
                    typeContainer.transform.SetParent(poolContainer);

                    int maxForType = config.maxCount > 0 ? config.maxCount : globalMaxEnemies;
                    var pool = new ObjectPool<EnemyBlob>(
                        config.prefab,
                        typeContainer.transform,
                        0,              // ← 不在构造函数内预热，由 WarmupCoroutine 分帧完成
                        maxForType,
                        allowDynamicExpand
                    );

                    pools[config.type]          = pool;
                    prewarmTargets[config.type] = config.prewarmCount; // 记录目标数量
                    poolCount++;

                    if (showDebugInfo)
                        GameLogger.Log($"[EnemyPoolManager] [池化] {config.type}: 目标预热{config.prewarmCount}, 上限{maxForType}");
                }
                else
                {
                    // ── 精英怪/障碍物：仅注册配置，不建池，Spawn 时 Instantiate ──
                    directCount++;

                    if (showDebugInfo)
                        GameLogger.Log($"[EnemyPoolManager] [直接] {config.type}: 每次 Instantiate/Destroy");
                }
            }

            GameLogger.Log($"[EnemyPoolManager] 章节 {chapterIndex + 1} 结构初始化完成 — 池化:{poolCount} 种，直接生成:{directCount} 种（预热将由协程分帧执行）");
        }

        /// <summary>
        /// 分帧预热所有对象池，避免进入战斗时单帧大量 Instantiate 造成卡顿。
        /// 每帧最多创建 <paramref name="instancesPerFrame"/> 个实例，直到达到各池的目标预热数量。
        /// 由 GameManager.DelayedStartGame 通过 yield return StartCoroutine 调用。
        /// </summary>
        public System.Collections.IEnumerator WarmupCoroutine(int instancesPerFrame = 2)
        {
            foreach (var kv in prewarmTargets)
            {
                EnemyType type  = kv.Key;
                int       total = kv.Value;
                if (total <= 0) continue;
                if (!pools.TryGetValue(type, out var pool)) continue;

                int remaining = total;
                while (remaining > 0)
                {
                    int batch = Mathf.Min(remaining, instancesPerFrame);
                    pool.Prewarm(batch);
                    remaining -= batch;
                    yield return null; // 让出帧，避免单帧卡顿
                }

                if (showDebugInfo)
                    GameLogger.Log($"[EnemyPoolManager] [Warmup] {type} 预热完成: {total} 个");
            }

            GameLogger.Log("[EnemyPoolManager] 所有对象池预热完毕");
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
            if (rotation == default)
                rotation = Quaternion.identity;

            // 查询配置
            if (!configs.TryGetValue(type, out var config))
            {
                GameLogger.LogError($"[EnemyPoolManager] 未注册的敌人类型 {type}！请在 Inspector 中添加配置。");
                return null;
            }

            EnemyBlob enemy;

            if (config.usePool)
            {
                // ── 普通怪：全局上限检查 + 从池中取 ──
                if (totalActiveEnemies >= globalMaxEnemies)
                {
                    GameLogger.LogWarning($"[EnemyPoolManager] 已达全局上限 {globalMaxEnemies}！");
                    return null;
                }

                if (!pools.TryGetValue(type, out var pool))
                {
                    GameLogger.LogError($"[EnemyPoolManager] {type} 的对象池未初始化！");
                    return null;
                }

                enemy = pool.Get(position, rotation);
            }
            else
            {
                // ── 精英怪：直接 Instantiate，不受全局上限限制 ──
                enemy = Object.Instantiate(config.prefab, position, rotation);
                if (enemy != null)
                {
                    nonPooledActive.Add(enemy);
                    enemy.OnSpawn(); // 池化路径由 ObjectPool.Get() 调用；非池化需手动补调
                }
            }

            if (enemy != null)
            {
                GlacialBossController.ActiveInstance?.ConfigureFriendlyEnemyPassThrough(enemy);

                // Stationary 障碍物不计入战斗敌人数
                if (enemy.Data == null || !enemy.Data.IsStationary)
                    totalActiveEnemies++;

                if (showDebugInfo)
                    GameLogger.Log($"[EnemyPoolManager] 生成 {type} (pooled={config.usePool}) @ {position}, 总数: {totalActiveEnemies}");
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
        /// 回收敌人（池化→归还；非池化→销毁）
        /// </summary>
        public void Despawn(EnemyBlob enemy)
        {
            if (enemy == null) return;

            bool isStationary = enemy.Data != null && enemy.Data.IsStationary;

            if (!System.Enum.TryParse<EnemyType>(enemy.PoolKey, out var type))
            {
                GameLogger.LogWarning($"[EnemyPoolManager] 无法解析敌人类型: {enemy.PoolKey}，降级直接销毁");
                Destroy(enemy.gameObject);
                if (!isStationary) totalActiveEnemies = Mathf.Max(0, totalActiveEnemies - 1);
                return;
            }

            // 根据注册配置决定归还方式
            bool usePool = !configs.TryGetValue(type, out var config) || config.usePool;

            if (usePool && pools.TryGetValue(type, out var pool))
            {
                pool.Return(enemy);
            }
            else
            {
                // 精英怪/非池化：直接销毁
                nonPooledActive.Remove(enemy);
                Destroy(enemy.gameObject);
            }

            if (!isStationary)
                totalActiveEnemies = Mathf.Max(0, totalActiveEnemies - 1);

            if (showDebugInfo)
                GameLogger.Log($"[EnemyPoolManager] 回收 {type} (pooled={usePool}), 剩余总数: {totalActiveEnemies}");
        }
        
        /// <summary>
        /// 回收所有敌人（池化→归还；非池化→销毁）
        /// </summary>
        public void DespawnAll()
        {
            // 归还池化敌人
            foreach (var pool in pools.Values)
                pool.ReturnAll();

            // 销毁非池化精英怪
            foreach (var enemy in nonPooledActive)
            {
                if (enemy != null)
                    Destroy(enemy.gameObject);
            }
            nonPooledActive.Clear();

            totalActiveEnemies = 0;
            if (showDebugInfo)
                GameLogger.Log("[EnemyPoolManager] 所有敌人已回收/销毁");
        }

        /// <summary>
        /// 清空所有对象池（章节切换时调用）
        /// </summary>
        public void ClearAllPools()
        {
            if (pools == null) return;

            foreach (var pool in pools.Values)
                pool.Clear();
            pools.Clear();

            // 销毁残余非池化实例
            if (nonPooledActive != null)
            {
                foreach (var enemy in nonPooledActive)
                {
                    if (enemy != null)
                        Destroy(enemy.gameObject);
                }
                nonPooledActive.Clear();
            }

            configs?.Clear();
            totalActiveEnemies = 0;
            if (showDebugInfo)
                GameLogger.Log("[EnemyPoolManager] 所有对象池已清空");
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

        /// <summary>
        /// 获取当前场景中实际仍然激活的 EnemyBlob 数量。
        /// 用于波次结算这类需要把 Stationary（如冰墙）也算进去的判定。
        /// </summary>
        public int GetSceneEnemyCount()
        {
            EnemyBlob[] activeEnemies = Object.FindObjectsByType<EnemyBlob>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            return activeEnemies != null ? activeEnemies.Length : 0;
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
            if (showDebugInfo)
                GameLogger.Log($"[EnemyPoolManager] 测试生成 {testSpawnCount} 个 {testSpawnType}");
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
