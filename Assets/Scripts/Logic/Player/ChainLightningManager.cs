// ============================================================
// ChainLightningManager.cs
// 文件位置: Assets/Scripts/Logic/Player/ChainLightningManager.cs
// 用途：连锁反应技能核心管理器
// 职责：管理所有传导链的创建、更新、伤害和销毁
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using LightVsDecay.Core;
using LightVsDecay.Logic.Enemy;
using LightVsDecay.Data.SO;
using LightVsDecay.Core.Pool;
using LightVsDecay.Audio;
using LightVsDecay.Logic.Statistics;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 单条传导链数据
    /// </summary>
    public class ChainLink
    {
        public EnemyBlob sourceEnemy;           // 传导源敌人
        public EnemyBlob targetEnemy;           // 传导目标敌人
        public ChainLightningRenderer renderer; // 渲染器
        public float currentDamage;             // 当前伤害值（DPS）
        public int bounceIndex;                 // 当前跳数（0-based）
        public bool isFromMainLaser;            // 是否来自主激光
        public float lastDamageTime;            // 上次造成伤害的时间
        
        /// <summary>是否有效（双方敌人都存活）</summary>
        public bool IsValid => sourceEnemy != null && !sourceEnemy.IsDead && 
                               targetEnemy != null && !targetEnemy.IsDead;
    }
    
    /// <summary>
    /// 传导链组（从一个激光命中点发出的整条链）
    /// </summary>
    public class ChainGroup
    {
        public EnemyBlob rootEnemy;             // 根敌人（被激光直接命中的）
        public bool isMainLaser;                // 是否来自主激光
        public float baseDamage;                // 基础伤害（DPS）
        public int maxBounces;                  // 最大跳数
        public float damageDecay;               // 伤害衰减率
        public float chainRange;                // 传导距离
        public List<ChainLink> links = new List<ChainLink>();  // 所有链接
        public HashSet<int> affectedEnemyIds = new HashSet<int>();  // 已影响的敌人ID
        public float lastFindTime;              // 上次查找目标的时间
        
        /// <summary>是否有效（根敌人存活）</summary>
        public bool IsValid => rootEnemy != null && !rootEnemy.IsDead;
    }
    
    /// <summary>
    /// 连锁传导管理器（单例）
    /// </summary>
    public class ChainLightningManager : Singleton<ChainLightningManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("预制体")]
        [Tooltip("传导线预制体（需要有 ChainLightningRenderer 组件）")]
        [SerializeField] private GameObject chainLightningPrefab;
        
        [Header("对象池设置")]
        [SerializeField] private int poolPrewarmCount = 10;
        [SerializeField] private int poolMaxCount = 20;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        // 技能参数
        private int chainLevel = 0;
        private int mainLaserMaxBounces = 1;
        private float chainRange = GameConstants.CHAIN_DEFAULT_RANGE;
        private float damageDecay = 0.2f;
        
        // 活跃的传导链组
        private List<ChainGroup> activeChainGroups = new List<ChainGroup>();
        
        // 对象池
        private Queue<ChainLightningRenderer> rendererPool = new Queue<ChainLightningRenderer>();
        private List<ChainLightningRenderer> activeRenderers = new List<ChainLightningRenderer>();
        private Transform poolContainer;
        
        // 缓存
        private Collider2D[] nearbyEnemyBuffer = new Collider2D[30];
        private RaycastHit2D[] chainBlockerBuffer = new RaycastHit2D[16];
        private int enemyLayerMask;
        
        // 本帧被激光直接命中的敌人（用于触发传导）
        private Dictionary<int, LaserHitData> frameHitEnemies = new Dictionary<int, LaserHitData>();
        
        private Color chainColor = Color.white;
        /// <summary>激光命中数据</summary>
        private struct LaserHitData
        {
            public EnemyBlob enemy;
            public float damage;
            public bool isMainLaser;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>技能等级</summary>
        public int ChainLevel => chainLevel;
        
        /// <summary>是否启用</summary>
        public bool IsEnabled => chainLevel > 0;
        
        /// <summary>当前活跃传导线数量</summary>
        public int ActiveLineCount => activeRenderers.Count;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            InitializePool();
            CacheLayerMask();
        }
        
        protected override void OnSingletonDestroy()
        {
            ClearAllChains();
            ClearPool();
        }
        
        private void Update()
        {
            if (!IsEnabled) return;
            
            float currentTime = Time.time;
            
            // 处理本帧被命中的敌人，启动/更新传导链
            ProcessFrameHits();
            
            // 更新所有活跃的传导链组
            for (int i = activeChainGroups.Count - 1; i >= 0; i--)
            {
                var group = activeChainGroups[i];
                
                // 检查组是否有效
                if (!group.IsValid)
                {
                    RemoveChainGroup(group);
                    activeChainGroups.RemoveAt(i);
                    continue;
                }
                
                // 定时查找新目标（扩展链条）
                if (currentTime - group.lastFindTime >= GameConstants.CHAIN_FIND_INTERVAL)
                {
                    ExtendChainTargets(group);
                    group.lastFindTime = currentTime;
                }
                
                // 更新链接位置和伤害
                UpdateChainLinks(group, currentTime);
            }
            
            // 清空本帧命中记录
            frameHitEnemies.Clear();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置技能参数（由 SkillEffectManager 调用）
        /// </summary>
        public void SetChainParameters(int level, int maxBounces, float range, float decay)
        {
            chainLevel = level;
            mainLaserMaxBounces = maxBounces;
            chainRange = range;
            damageDecay = decay;
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[ChainLightningManager] 设置参数: Lv.{level}, 跳数={maxBounces}, 距离={range}m, 衰减={decay:P0}");
            }
            
            // 如果等级为0，清除所有传导
            if (level <= 0)
            {
                ClearAllChains();
            }
        }
        
        /// <summary>
        /// 注册激光命中敌人（由 LaserController 每tick调用）
        /// </summary>
        /// <param name="enemy">被命中的敌人</param>
        /// <param name="damage">造成的伤害（DPS）</param>
        /// <param name="isMainLaser">是否为主激光</param>
        public void RegisterLaserHit(EnemyBlob enemy, float damage, bool isMainLaser)
        {
            if (!IsEnabled || enemy == null || enemy.IsDead) return;
            if (enemy.Data != null && enemy.Data.IsStationary) return;
            
            int enemyId = enemy.GetInstanceID();
            
            // 记录或更新本帧命中数据
            if (!frameHitEnemies.ContainsKey(enemyId))
            {
                frameHitEnemies[enemyId] = new LaserHitData
                {
                    enemy = enemy,
                    damage = damage,
                    isMainLaser = isMainLaser
                };
            }
            else if (isMainLaser && !frameHitEnemies[enemyId].isMainLaser)
            {
                // 主激光优先级更高
                var data = frameHitEnemies[enemyId];
                data.isMainLaser = true;
                data.damage = damage;
                frameHitEnemies[enemyId] = data;
            }
        }
        
        /// <summary>
        /// 通知激光离开敌人（由 LaserController 调用）
        /// </summary>
        public void NotifyLaserLeft(EnemyBlob enemy)
        {
            if (enemy == null) return;
            
            int enemyId = enemy.GetInstanceID();
            
            // 移除以该敌人为根的传导链组
            for (int i = activeChainGroups.Count - 1; i >= 0; i--)
            {
                var group = activeChainGroups[i];
                if (group.rootEnemy != null && group.rootEnemy.GetInstanceID() == enemyId)
                {
                    RemoveChainGroup(group);
                    activeChainGroups.RemoveAt(i);
                    
                    if (showDebugInfo)
                    {
                        GameLogger.Log($"[ChainLightningManager] 激光离开，移除传导链: {enemy.name}");
                    }
                }
            }
        }
        
        /// <summary>
        /// 清除所有传导链
        /// </summary>
        public void ClearAllChains()
        {
            foreach (var group in activeChainGroups)
            {
                RemoveChainGroup(group);
            }
            activeChainGroups.Clear();
            frameHitEnemies.Clear();
        }
        /// <summary>设置所有传导线颜色（跟随主激光）</summary>
        public void SetChainColor(Color color)
        {
            chainColor = color;
            // 更新所有当前活跃的传导线
            foreach (var r in activeRenderers)
                if (r != null && r.IsActive) r.SetColor(color);
        }

        /// <summary>重置传导线颜色为默认</summary>
        public void ResetChainColor()
        {
            foreach (var r in activeRenderers)
                if (r != null && r.IsActive) r.SetColor(chainColor);
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法 - 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void InitializePool()
        {
            // 创建池容器
            GameObject container = new GameObject("[ChainLightningPool]");
            container.transform.SetParent(transform);
            poolContainer = container.transform;
            
            // 如果没有预制体，创建一个简单的默认预制体
            if (chainLightningPrefab == null)
            {
                CreateDefaultPrefab();
            }
            
            // 预热对象池
            for (int i = 0; i < poolPrewarmCount; i++)
            {
                var renderer = CreateRenderer();
                if (renderer != null)
                {
                    renderer.Deactivate();
                    rendererPool.Enqueue(renderer);
                }
            }
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[ChainLightningManager] 对象池初始化: 预热{poolPrewarmCount}, 上限{poolMaxCount}");
            }
        }
        
        private void CreateDefaultPrefab()
        {
            // 创建一个简单的默认预制体（运行时用）
            GameObject defaultPrefab = new GameObject("ChainLightning_Default");
            defaultPrefab.SetActive(false);
            
            // 添加 LineRenderer
            LineRenderer lr = defaultPrefab.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = 0.15f;
            lr.endWidth = 0.15f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(0.5f, 0.9f, 1f, 1f);
            lr.endColor = new Color(0.3f, 0.7f, 1f, 0.8f);
            lr.sortingLayerName = "Effects";
            lr.sortingOrder = 10;
            
            // 添加 ChainLightningRenderer 组件
            defaultPrefab.AddComponent<ChainLightningRenderer>();
            
            // 保存引用
            chainLightningPrefab = defaultPrefab;
            defaultPrefab.transform.SetParent(poolContainer);
            
            if (showDebugInfo)
            {
                GameLogger.Log("[ChainLightningManager] 已创建默认传导线预制体");
            }
        }
        
        private void CacheLayerMask()
        {
            enemyLayerMask = LayerMask.GetMask(GameConstants.ENEMY_LAYER, GameConstants.BOUNCING_ENEMY_LAYER);
        }
        
        private void ClearPool()
        {
            foreach (var renderer in rendererPool)
            {
                if (renderer != null)
                {
                    Destroy(renderer.gameObject);
                }
            }
            rendererPool.Clear();
            
            foreach (var renderer in activeRenderers)
            {
                if (renderer != null)
                {
                    Destroy(renderer.gameObject);
                }
            }
            activeRenderers.Clear();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法 - 传导链处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 处理本帧被命中的敌人
        /// </summary>
        private void ProcessFrameHits()
        {
            foreach (var kvp in frameHitEnemies)
            {
                var hitData = kvp.Value;
                
                // 检查是否已有以该敌人为根的传导链
                ChainGroup existingGroup = FindChainGroupByRoot(hitData.enemy);
                
                if (existingGroup != null)
                {
                    // 更新已存在的链组的伤害值
                    existingGroup.baseDamage = hitData.damage;
                }
                else
                {
                    // 创建新的链组
                    CreateChainGroup(hitData.enemy, hitData.damage, hitData.isMainLaser);
                }
            }
        }
        
        /// <summary>
        /// 创建新的传导链组
        /// </summary>
        private void CreateChainGroup(EnemyBlob rootEnemy, float baseDamage, bool isMainLaser)
        {
            // 检查是否超过最大数量
            if (activeChainGroups.Count >= GameConstants.CHAIN_MAX_ACTIVE_LINES)
            {
                if (showDebugInfo)
                {
                    GameLogger.LogWarning("[ChainLightningManager] 达到传导链组上限，跳过创建");
                }
                return;
            }
            
            var group = new ChainGroup
            {
                rootEnemy = rootEnemy,
                isMainLaser = isMainLaser,
                baseDamage = baseDamage,
                maxBounces = isMainLaser ? mainLaserMaxBounces : GameConstants.CHAIN_SUB_LASER_MAX_BOUNCES,
                damageDecay = damageDecay,
                chainRange = chainRange,
                lastFindTime = Time.time
            };
            
            // 将根敌人加入已影响列表
            group.affectedEnemyIds.Add(rootEnemy.GetInstanceID());
            
            activeChainGroups.Add(group);
            
            // 立即查找第一个目标
            ExtendChainTargets(group);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[ChainLightningManager] 创建传导链: {rootEnemy.name}, 主激光={isMainLaser}, 最大跳数={group.maxBounces}");
            }
        }
        
        /// <summary>
        /// 扩展传导链目标（每隔 CHAIN_FIND_INTERVAL 调用）
        /// </summary>
        private void ExtendChainTargets(ChainGroup group)
        {
            if (!group.IsValid) return;
            
            // 获取当前链的末端敌人
            EnemyBlob currentEnd = group.links.Count > 0 ? group.links[group.links.Count - 1].targetEnemy : group.rootEnemy;
            int currentBounce = group.links.Count;
            
            // 检查是否已达到最大跳数
            if (currentBounce >= group.maxBounces)
            {
                return;
            }
            
            // 检查末端敌人是否有效
            if (currentEnd == null || currentEnd.IsDead)
            {
                // 尝试缩短链
                TrimDeadLinks(group);
                return;
            }
            
            // 查找最近的可传导目标
            EnemyBlob nearestTarget = FindNearestTarget(currentEnd.transform.position, group.affectedEnemyIds, group.chainRange);
            
            if (nearestTarget != null)
            {
                // 创建新链接
                CreateChainLink(group, currentEnd, nearestTarget, currentBounce);
                
                // 将目标加入已影响列表
                group.affectedEnemyIds.Add(nearestTarget.GetInstanceID());
            }
        }
        
        /// <summary>
        /// 创建单个链接
        /// </summary>
        private void CreateChainLink(ChainGroup group, EnemyBlob source, EnemyBlob target, int bounceIndex)
        {
            // 计算该链接的伤害（每跳衰减）
            float linkDamage = group.baseDamage * Mathf.Pow(1f - group.damageDecay, bounceIndex);
            
            // 获取渲染器
            var renderer = GetRenderer();
            if (renderer != null)
            {
                renderer.SetColor(chainColor);

                renderer.Initialize(
                    source.transform.position,
                    target.transform.position,
                    bounceIndex,
                    group.isMainLaser
                );
            }
            
            var link = new ChainLink
            {
                sourceEnemy = source,
                targetEnemy = target,
                renderer = renderer,
                currentDamage = linkDamage,
                bounceIndex = bounceIndex,
                isFromMainLaser = group.isMainLaser,
                lastDamageTime = Time.time
            };
            
            group.links.Add(link);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[ChainLightningManager] 创建链接: {source.name} -> {target.name}, 跳数={bounceIndex}, DPS={linkDamage:F1}");
            }
        }
        
        /// <summary>
        /// 更新链接位置和伤害
        /// </summary>
        private void UpdateChainLinks(ChainGroup group, float currentTime)
        {
            for (int i = group.links.Count - 1; i >= 0; i--)
            {
                var link = group.links[i];
                
                // 检查链接是否有效
                if (!link.IsValid)
                {
                    ReturnRenderer(link.renderer);
                    group.links.RemoveAt(i);
                    
                    // 移除后续所有链接（链断了）
                    TrimLinksAfter(group, i);
                    continue;
                }
                
                // 更新渲染器位置
                if (link.renderer != null)
                {
                    link.renderer.UpdatePositions(
                        link.sourceEnemy.transform.position,
                        link.targetEnemy.transform.position
                    );
                }
                
                // 造成伤害（每 CHAIN_DAMAGE_INTERVAL 秒）
                if (currentTime - link.lastDamageTime >= GameConstants.CHAIN_DAMAGE_INTERVAL)
                {
                    ApplyChainDamage(link, group);
                    link.lastDamageTime = currentTime;
                }
            }
        }
        
        /// <summary>
        /// 对链接目标造成伤害
        /// </summary>
        private void ApplyChainDamage(ChainLink link, ChainGroup group)
        {
            if (link.targetEnemy == null || link.targetEnemy.IsDead) return;
            
            // 计算实际伤害（每tick伤害 = DPS × tick间隔）
            float tickDamage = link.currentDamage * GameConstants.CHAIN_DAMAGE_INTERVAL;
            
            // 数据破碎加成（对受损敌人的额外伤害）
            if (SkillEffectManager.Instance != null)
            {
                float shatterBonus = SkillEffectManager.Instance.GetShatterDamageBonus();
                if (shatterBonus > 0f && link.targetEnemy.IsImpaired)
                {
                    tickDamage *= (1f + shatterBonus);
                }
            }
            
            // 造成伤害（不带击退，不带暴击）
            link.targetEnemy.TakeDamage(tickDamage, Vector2.zero, false, false);
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 【新增】数据埋点：上报 Chain 伤害
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            if (BattleStatistics.Instance != null)
            {
                // 注意：这里假设 EnemyBlob 有 Type 属性获取 EnemyType
                // 如果你的属性名是 enemyType，请自行调整
                BattleStatistics.Instance.RecordDamage(
                    tickDamage,                     // 有效伤害
                    0f,                             // 溢出伤害（连锁通常不计算溢出）
                    link.targetEnemy.Type,          // 敌人类型
                    DamageSource.Chain,             // 【关键】标记为连锁伤害
                    false                           // 连锁伤害通常不暴击
                );
            }
            // 应用 Frost 效果
            ApplyFrostEffect(link.targetEnemy);
        }
        
        /// <summary>
        /// 应用 Frost 效果到传导目标
        /// </summary>
        private void ApplyFrostEffect(EnemyBlob enemy)
        {
            if (SkillEffectManager.Instance == null) return;
            
            int frostLevel = SkillEffectManager.Instance.GetFrostLevel();
            if (frostLevel <= 0) return;
            
            float slowPercent, duration;
            SkillEffectManager.Instance.GetFrostParams(out slowPercent, out duration);
            
            if (slowPercent > 0f)
            {
                enemy.ApplyFrostSlow(slowPercent, duration);
                
                // 播放 Frost VFX（低频率）
                if (VFXPoolManager.Instance != null && Random.value < 0.1f)
                {
                    VFXPoolManager.Instance.PlayFrostHit(enemy.transform.position);
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法 - 目标查找
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 查找最近的可传导目标
        /// </summary>
        private EnemyBlob FindNearestTarget(Vector3 position, HashSet<int> excludeIds, float range)
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(position, range, nearbyEnemyBuffer, enemyLayerMask);
            
            EnemyBlob nearest = null;
            float nearestDist = float.MaxValue;
            
            for (int i = 0; i < hitCount; i++)
            {
                var collider = nearbyEnemyBuffer[i];
                if (collider == null) continue;
                
                EnemyBlob enemy = collider.GetComponentInParent<EnemyBlob>();
                if (enemy == null || enemy.IsDead) continue;
                if (enemy.Data != null && enemy.Data.IsStationary) continue;

                int enemyId = enemy.GetInstanceID();

                // 排除已在链中的敌人
                if (excludeIds.Contains(enemyId)) continue;
                if (IsChainPathBlockedByStationaryObstacle(position, enemy)) continue;
                
                float dist = Vector3.Distance(position, enemy.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = enemy;
                }
            }
            
            return nearest;
        }

        /// <summary>
        /// 熔浆液、冰墙等 Stationary 障碍会阻断连锁反应的传导视线。
        /// </summary>
        private bool IsChainPathBlockedByStationaryObstacle(Vector3 startPosition, EnemyBlob target)
        {
            if (target == null) return true;

            Vector2 start = startPosition;
            Vector2 end = target.transform.position;
            float targetDistance = Vector2.Distance(start, end);
            if (targetDistance <= 0.01f) return false;

            int hitCount = Physics2D.LinecastNonAlloc(start, end, chainBlockerBuffer, enemyLayerMask);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = chainBlockerBuffer[i];
                if (hit.collider == null) continue;

                EnemyBlob hitEnemy = hit.collider.GetComponentInParent<EnemyBlob>();
                if (hitEnemy == null || hitEnemy == target || hitEnemy.IsDead) continue;
                if (!hitEnemy.IsStationary) continue;

                // 只阻挡源点和目标之间的地形障碍，避免目标背后的冰墙误拦截。
                if (hit.distance < targetDistance - 0.05f)
                    return true;
            }

            return false;
        }
        
        /// <summary>
        /// 根据根敌人查找链组
        /// </summary>
        private ChainGroup FindChainGroupByRoot(EnemyBlob rootEnemy)
        {
            int rootId = rootEnemy.GetInstanceID();
            foreach (var group in activeChainGroups)
            {
                if (group.rootEnemy != null && group.rootEnemy.GetInstanceID() == rootId)
                {
                    return group;
                }
            }
            return null;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法 - 链维护
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 移除整个链组
        /// </summary>
        private void RemoveChainGroup(ChainGroup group)
        {
            foreach (var link in group.links)
            {
                ReturnRenderer(link.renderer);
            }
            group.links.Clear();
            group.affectedEnemyIds.Clear();
        }
        
        /// <summary>
        /// 裁剪已死亡的链接
        /// </summary>
        private void TrimDeadLinks(ChainGroup group)
        {
            for (int i = group.links.Count - 1; i >= 0; i--)
            {
                var link = group.links[i];
                if (!link.IsValid)
                {
                    // 从已影响列表中移除目标
                    if (link.targetEnemy != null)
                    {
                        group.affectedEnemyIds.Remove(link.targetEnemy.GetInstanceID());
                    }
                    
                    ReturnRenderer(link.renderer);
                    group.links.RemoveAt(i);
                }
                else
                {
                    // 找到有效链接，停止裁剪
                    break;
                }
            }
        }
        
        /// <summary>
        /// 移除指定索引之后的所有链接
        /// </summary>
        private void TrimLinksAfter(ChainGroup group, int startIndex)
        {
            for (int i = group.links.Count - 1; i > startIndex; i--)
            {
                var link = group.links[i];
                ReturnRenderer(link.renderer);
                
                // 从已影响列表移除
                if (link.targetEnemy != null)
                {
                    group.affectedEnemyIds.Remove(link.targetEnemy.GetInstanceID());
                }
                
                group.links.RemoveAt(i);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法 - 对象池
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取渲染器
        /// </summary>
        private ChainLightningRenderer GetRenderer()
        {
            ChainLightningRenderer renderer;
            
            if (rendererPool.Count > 0)
            {
                renderer = rendererPool.Dequeue();
            }
            else if (activeRenderers.Count < poolMaxCount)
            {
                renderer = CreateRenderer();
            }
            else
            {
                if (showDebugInfo)
                {
                    GameLogger.LogWarning("[ChainLightningManager] 渲染器对象池已满");
                }
                return null;
            }
            
            if (renderer != null)
            {
                activeRenderers.Add(renderer);
            }
            
            return renderer;
        }
        
        /// <summary>
        /// 归还渲染器
        /// </summary>
        private void ReturnRenderer(ChainLightningRenderer renderer)
        {
            if (renderer == null) return;
            
            renderer.Deactivate();
            activeRenderers.Remove(renderer);
            rendererPool.Enqueue(renderer);
        }
        
        /// <summary>
        /// 创建新渲染器
        /// </summary>
        private ChainLightningRenderer CreateRenderer()
        {
            if (chainLightningPrefab == null)
            {
                GameLogger.LogError("[ChainLightningManager] 传导线预制体未设置！");
                return null;
            }
            
            GameObject go = Instantiate(chainLightningPrefab, poolContainer);
            go.name = $"ChainLightning_{activeRenderers.Count + rendererPool.Count:D2}";
            go.SetActive(true);
            
            var renderer = go.GetComponent<ChainLightningRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<ChainLightningRenderer>();
            }
            
            return renderer;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !showDebugInfo) return;
            
            // 绘制所有活跃的传导链
            Gizmos.color = Color.cyan;
            foreach (var group in activeChainGroups)
            {
                foreach (var link in group.links)
                {
                    if (link.sourceEnemy != null && link.targetEnemy != null)
                    {
                        Gizmos.DrawLine(link.sourceEnemy.transform.position, link.targetEnemy.transform.position);
                    }
                }
            }
        }
#endif
    }
}
