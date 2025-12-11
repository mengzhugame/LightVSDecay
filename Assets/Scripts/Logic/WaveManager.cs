// ============================================================
// WaveManager.cs (修复版)
// 文件位置: Assets/Scripts/Logic/WaveManager.cs
// 用途：敌人波次管理 - 修复 GameState 命名空间
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Enemy;

namespace LightVsDecay.Logic
{
    /// <summary>
    /// 波次管理器
    /// 根据 WaveConfig 配置控制敌人生成节奏
    /// </summary>
    public class WaveManager : Singleton<WaveManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("波次配置")]
        [SerializeField] private WaveConfig waveConfig;
        
        [Header("生成范围")]
        [Tooltip("参考相机")]
        [SerializeField] private Camera gameCamera;
        
        [Tooltip("屏幕外偏移")]
        [SerializeField] private float spawnOffset = 1.5f;
        [Header("BOSS配置")]
        [Tooltip("BOSS预制体")]
        [SerializeField] private GameObject bossPrefab;
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool showSpawnArea = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private bool isSpawning = false;
        private PhaseConfig currentPhase;
        private GamePhase currentPhaseType = GamePhase.Warmup;
        
        // 生成计时器（每种敌人类型独立计时）
        private Dictionary<EnemyType, float> spawnTimers = new Dictionary<EnemyType, float>();
        
        // 屏幕边界缓存
        private Vector2 screenMin;
        private Vector2 screenMax;
        // 当前BOSS实例引用
        private GameObject currentBossInstance;
        // BOSS相关
        private bool bossSpawned = false;
        private float bossMinionTimer = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public bool IsSpawning => isSpawning;
        public GamePhase CurrentPhase => currentPhaseType;
        public string CurrentPhaseName => currentPhase?.displayName ?? "未知";
        /// <summary>当前BOSS实例</summary>
        public GameObject CurrentBoss => currentBossInstance;

        /// <summary>BOSS是否存活</summary>
        public bool IsBossAlive => currentBossInstance != null;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            if (gameCamera == null)
            {
                gameCamera = Camera.main;
            }
            
            CalculateScreenBounds();
            InitializeTimers();
            GameEvents.OnGameStart += OnGameStart;
            GameEvents.OnGameStateChanged += OnGameStateChanged;
        }
        
        private void Start()
        {
        }
        
        protected override void OnSingletonDestroy()
        {
            GameEvents.OnGameStart -= OnGameStart;
            GameEvents.OnGameStateChanged -= OnGameStateChanged;
        }
        
        private void Update()
        {
            // 【修复】GameManager 检查移到最前面，这是唯一应该完全阻断的条件
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) 
            {
                if (showDebugInfo) Debug.Log($"[WaveManager] Update跳过: GameManager空或不在Playing状态");
                return;
            }
    
            float gameTime = GameManager.Instance.GameTimer;
    
            // 【修复】阶段更新必须始终执行，不受 isSpawning 影响
            UpdateCurrentPhase(gameTime);
    
            // 【修复】只有 isSpawning = true 时才生成敌人
            if (isSpawning)
            {
                // 根据阶段生成敌人
                if (currentPhase != null && currentPhase.enableSpawning)
                {
                    ProcessSpawning();
                }
        
                // BOSS阶段特殊处理
                if (currentPhaseType == GamePhase.BossFight && bossSpawned)
                {
                    ProcessBossMinionSpawning();
                }
            }
    
            // 【新增】每10秒打印一次当前时间和阶段信息（调试用）
            if (showDebugInfo && Mathf.FloorToInt(gameTime) % 10 == 0 && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[WaveManager] 当前时间: {gameTime:F1}s, 当前阶段: {currentPhase?.phase}, isSpawning: {isSpawning}, TimeScale: {Time.timeScale}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void CalculateScreenBounds()
        {
            if (gameCamera == null) return;
            
            float height = gameCamera.orthographicSize * 2f;
            float width = height * gameCamera.aspect;
            
            Vector3 camPos = gameCamera.transform.position;
            
            screenMin = new Vector2(camPos.x - width / 2f, camPos.y - height / 2f);
            screenMax = new Vector2(camPos.x + width / 2f, camPos.y + height / 2f);
        }
        
        private void InitializeTimers()
        {
            spawnTimers.Clear();
            foreach (EnemyType type in System.Enum.GetValues(typeof(EnemyType)))
            {
                spawnTimers[type] = 0f;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnGameStart()
        {
            isSpawning = true;
            bossSpawned = false;
            bossMinionTimer = 0f;
            InitializeTimers();
            
            // 设置初始阶段
            if (waveConfig != null && waveConfig.phases.Count > 0)
            {
                currentPhase = waveConfig.phases[0];
                currentPhaseType = currentPhase.phase;
        
                // 【新增】打印初始阶段信息
                if (showDebugInfo)
                {
                    Debug.Log($"[WaveManager] 开始生成敌人，初始阶段: {currentPhase.displayName} ({currentPhase.phase})");
                    Debug.Log($"[WaveManager] WaveConfig 共 {waveConfig.phases.Count} 个阶段:");
                    foreach (var p in waveConfig.phases)
                    {
                        Debug.Log($"  - {p.phase}: {p.startTime}s - {p.endTime}s");
                    }
                }
            }
            else
            {
                Debug.LogError("[WaveManager] waveConfig 为空或没有配置阶段！");
            }
        }
        
        /// <summary>
        /// 游戏状态变化回调 - 使用 Core.GameState
        /// </summary>
        private void OnGameStateChanged(GameState state)
        {
            if (state == GameState.Victory || state == GameState.Defeat)
            {
                isSpawning = false;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 阶段管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateCurrentPhase(float gameTime)
        {
            if (waveConfig == null) 
            {
                if (showDebugInfo) Debug.LogWarning("[WaveManager] waveConfig 为空！");
                return;
            }
    
            PhaseConfig newPhase = waveConfig.GetPhaseAtTime(gameTime);
    
            // 【新增】调试：无法找到阶段时打印警告
            if (newPhase == null)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"[WaveManager] 找不到 gameTime={gameTime:F1}s 对应的阶段！检查 WaveConfig 配置");
                }
                return;
            }
    
            if (newPhase != currentPhase)
            {
                // 【新增】详细日志
                if (showDebugInfo)
                {
                    Debug.Log($"[WaveManager] 阶段切换: {currentPhase?.phase} → {newPhase.phase}, 时间: {gameTime:F1}s");
                }
        
                // 阶段切换
                OnPhaseEnd(currentPhase);
                currentPhase = newPhase;
                currentPhaseType = newPhase.phase;
                OnPhaseStart(newPhase);
            }
        }
        
        private void OnPhaseStart(PhaseConfig phase)
        {
            if (phase == null) return;
    
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] 进入阶段: {phase.displayName} ({phase.phase})");
            }
    
            // 显示阶段提示
            if (phase.showPhaseHint && !string.IsNullOrEmpty(phase.hintText))
            {
                // TODO: 显示UI提示
                Debug.Log($"[WaveManager] 提示: {phase.hintText}");
            }
    
            // 【修复】根据阶段配置设置 isSpawning
            // 这样每个阶段开始时会自动恢复/暂停生成
            isSpawning = phase.enableSpawning;
    
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] isSpawning 设置为: {isSpawning} (由 enableSpawning 决定)");
            }
    
            // 处理阶段开始事件（PhaseEvent 可以覆盖上面的设置）
            HandlePhaseEvent(phase.onPhaseStart);
    
            // 重置计时器
            InitializeTimers();
        }
        
        private void OnPhaseEnd(PhaseConfig phase)
        {
            if (phase == null) return;
            
            // 处理阶段结束事件
            HandlePhaseEvent(phase.onPhaseEnd);
        }
        
        private void HandlePhaseEvent(PhaseEvent evt)
        {
            switch (evt)
            {
                case PhaseEvent.ClearAllEnemies:
                    ClearAllEnemies();
                    break;
                    
                case PhaseEvent.PlayWarningSound:
                    // TODO: 播放警告音效
                    Debug.Log("[WaveManager] 警告音效！");
                    break;
                    
                case PhaseEvent.SpawnBoss:
                    SpawnBoss();
                    break;
                    
                case PhaseEvent.PauseSpawning:
                    isSpawning = false;
                    break;
                    
                case PhaseEvent.ResumeSpawning:
                    isSpawning = true;
                    break;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 敌人生成
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ProcessSpawning()
        {
            if (currentPhase == null) return;
            if (EnemyPoolManager.Instance == null) return;
            if (EnemyPoolManager.Instance.IsAtGlobalCapacity) return;
            
            float rateMultiplier = currentPhase.spawnRateMultiplier > 0 
                ? 1f / currentPhase.spawnRateMultiplier 
                : 1f;
            
            foreach (var entry in currentPhase.spawnEntries)
            {
                // 更新计时器
                spawnTimers[entry.enemyType] += Time.deltaTime;
                
                // 检查生成间隔
                float interval = entry.spawnInterval * rateMultiplier;
                
                if (spawnTimers[entry.enemyType] >= interval)
                {
                    spawnTimers[entry.enemyType] = 0f;
                    
                    // 生成敌人
                    SpawnEnemies(entry);
                }
            }
        }
        
        private void SpawnEnemies(EnemySpawnEntry entry)
        {
            if (!EnemyPoolManager.Instance.HasPool(entry.enemyType))
            {
                return;
            }
            
            for (int i = 0; i < entry.spawnCount; i++)
            {
                if (EnemyPoolManager.Instance.IsAtGlobalCapacity) break;
                
                Vector3 position = GetSpawnPosition(entry.spawnZone);
                EnemyBlob enemy = EnemyPoolManager.Instance.Spawn(entry.enemyType, position);
                
                if (enemy != null)
                {
                    if (entry.speedMultiplier != 1f)
                    {
                        enemy.SetSpeedMultiplier(entry.speedMultiplier);
                    }
            
                    // 【新增】为横穿屏幕类型设置目标点
                    if (entry.enemyType == EnemyType.Treasure)
                    {
                        Vector3 targetPos = GetCrossScreenTarget(entry.spawnZone, position);
                        enemy.SetCrossScreenTarget(targetPos);
                    }
                }
            }
        }
        /// <summary>
        /// 获取横穿屏幕的目标位置
        /// </summary>
        private Vector3 GetCrossScreenTarget(SpawnZone spawnZone, Vector3 startPos)
        {
            // 从左侧生成 → 目标在右侧
            // 从右侧生成 → 目标在左侧
            float targetX;
            if (spawnZone == SpawnZone.LeftSideUpper)
            {
                targetX = screenMax.x + spawnOffset * 2f;
            }
            else
            {
                targetX = screenMin.x - spawnOffset * 2f;
            }
    
            // Y 坐标保持大致相同（略有随机偏移）
            float targetY = startPos.y + Random.Range(-1f, 1f);
    
            return new Vector3(targetX, targetY, 0f);
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生成位置计算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Vector3 GetSpawnPosition(SpawnZone zone)
        {
            switch (zone)
            {
                case SpawnZone.AllEdges:
                    return GetRandomEdgePosition();
                    
                case SpawnZone.TopOnly:
                    return GetTopPosition();
                    
                case SpawnZone.TopRandom:
                    return GetTopRandomPosition();
                    
                case SpawnZone.SideRandom:
                    return GetSideRandomPosition();
                    
                case SpawnZone.BottomCorners:
                    return GetBottomCornerPosition();
                case SpawnZone.LeftSideUpper:
                    return GetLeftSideUpperPosition();
    
                case SpawnZone.RightSideUpper:
                    return GetRightSideUpperPosition();
                default:
                    return GetRandomEdgePosition();
            }
        }
        private Vector3 GetRandomEdgePosition()
        {
            int edge = Random.Range(0, 3); // 0=上, 1=左, 2=右
            float screenMidY = (screenMin.y + screenMax.y) * 0.5f;
            switch (edge)
            {
                case 0: // 上
                    return new Vector3(
                        Random.Range(screenMin.x, screenMax.x),
                        screenMax.y + spawnOffset,
                        0f
                    );
                case 1: // 左
                    return new Vector3(
                        screenMin.x - spawnOffset,
                        Random.Range(screenMidY, screenMax.y),
                        0f
                    );
                case 2: // 右
                    return new Vector3(
                        screenMax.x + spawnOffset,
                        Random.Range(screenMidY, screenMax.y),
                        0f
                    );
                default:
                    return GetTopPosition();
            }
        }
        
        private Vector3 GetTopPosition()
        {
            return new Vector3(
                Random.Range(screenMin.x, screenMax.x),
                screenMax.y + spawnOffset,
                0f
            );
        }
        
        private Vector3 GetTopRandomPosition()
        {
            float x = Random.Range(screenMin.x * 0.8f, screenMax.x * 0.8f);
            return new Vector3(x, screenMax.y + spawnOffset, 0f);
        }
        
        private Vector3 GetSideRandomPosition()
        {
            bool isLeft = Random.value > 0.5f;
            float x = isLeft ? screenMin.x - spawnOffset : screenMax.x + spawnOffset;
            // 【修改】Y 轴限制在屏幕上半部分（>= 50%）
            float screenMidY = (screenMin.y + screenMax.y) * 0.5f;
            float y = Random.Range(screenMidY, screenMax.y);
            return new Vector3(x, y, 0f);
        }
        
        private Vector3 GetBottomCornerPosition()
        {
            bool isLeft = Random.value > 0.5f;
            float x = isLeft ? screenMin.x - spawnOffset : screenMax.x + spawnOffset;
            float y = screenMin.y + spawnOffset;
            return new Vector3(x, y, 0f);
        }
        private Vector3 GetLeftSideUpperPosition()
        {
            // 屏幕左侧，Y 轴在上半部分（避开塔）
            float x = screenMin.x - spawnOffset;
            float yMin = (screenMin.y + screenMax.y) * 0.5f; // 屏幕中点
            float yMax = screenMax.y - 1f; // 离顶部留点距离
            float y = Random.Range(yMin, yMax);
            return new Vector3(x, y, 0f);
        }

        private Vector3 GetRightSideUpperPosition()
        {
            // 屏幕右侧，Y 轴在上半部分
            float x = screenMax.x + spawnOffset;
            float yMin = (screenMin.y + screenMax.y) * 0.5f;
            float yMax = screenMax.y - 1f;
            float y = Random.Range(yMin, yMax);
            return new Vector3(x, y, 0f);
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BOSS相关
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void SpawnBoss()
        {
            if (bossSpawned) return;
    
            if (bossPrefab == null)
            {
                Debug.LogError("[WaveManager] bossPrefab 未设置！无法生成BOSS");
                return;
            }
    
            bossSpawned = true;
    
            // BOSS生成位置（屏幕上方中央）
            Vector3 bossPosition = new Vector3(0f, screenMax.y + 2f, 0f);
    
            // 实际生成BOSS
            currentBossInstance = Instantiate(bossPrefab, bossPosition, Quaternion.identity);
    
            // 通知 GameManager 进入 BOSS 战
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EnterBossFight();
            }
    
            // 从 WaveConfig 读取 BOSS 血量并设置
            BossHealth bossHealth = currentBossInstance.GetComponent<BossHealth>();
            if (bossHealth != null && waveConfig != null)
            {
                bossHealth.SetMaxHealth(waveConfig.bossHealth);
            }
    
            Debug.Log($"[WaveManager] BOSS 生成！位置: {bossPosition}");
        }
        
        private void ProcessBossMinionSpawning()
        {
            if (waveConfig == null) return;
            
            bossMinionTimer += Time.deltaTime;
            
            if (bossMinionTimer >= waveConfig.bossMinionSpawnInterval)
            {
                bossMinionTimer = 0f;
                
                // 生成小弟
                for (int i = 0; i < waveConfig.bossMinionCount; i++)
                {
                    if (EnemyPoolManager.Instance.IsAtGlobalCapacity) break;
                    
                    Vector3 position = GetRandomEdgePosition();
                    EnemyPoolManager.Instance.Spawn(EnemyType.Slime, position);
                }
                
                if (showDebugInfo)
                {
                    Debug.Log($"[WaveManager] BOSS召唤 {waveConfig.bossMinionCount} 个小弟");
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 清除所有敌人
        /// </summary>
        public void ClearAllEnemies()
        {
            if (EnemyPoolManager.Instance != null)
            {
                EnemyPoolManager.Instance.DespawnAll();
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[WaveManager] 清除所有敌人");
            }
        }
        
        /// <summary>
        /// 停止生成
        /// </summary>
        public void StopSpawning()
        {
            isSpawning = false;
        }
        
        /// <summary>
        /// 恢复生成
        /// </summary>
        public void ResumeSpawning()
        {
            isSpawning = true;
        }
        /// <summary>
        /// 测试用：立即生成BOSS
        /// </summary>
        public void TestSpawnBoss()
        {
            if (bossSpawned)
            {
                Debug.LogWarning("[WaveManager] BOSS已经生成过了！");
                return;
            }
    
            // 强制进入BOSS阶段
            currentPhaseType = GamePhase.BossFight;
    
            SpawnBoss();
        }

        /// <summary>
        /// 测试用：销毁当前BOSS
        /// </summary>
        public void TestDestroyBoss()
        {
            if (currentBossInstance != null)
            {
                Destroy(currentBossInstance);
                currentBossInstance = null;
                bossSpawned = false;
                Debug.Log("[WaveManager] BOSS已销毁");
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDrawGizmos()
        {
            if (!showSpawnArea) return;
            
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameCamera == null) return;
            
            CalculateScreenBounds();
            
            // 绘制屏幕边界
            Gizmos.color = Color.yellow;
            Vector3 min = new Vector3(screenMin.x, screenMin.y, 0f);
            Vector3 max = new Vector3(screenMax.x, screenMax.y, 0f);
            
            Gizmos.DrawLine(new Vector3(min.x, min.y, 0), new Vector3(max.x, min.y, 0));
            Gizmos.DrawLine(new Vector3(max.x, min.y, 0), new Vector3(max.x, max.y, 0));
            Gizmos.DrawLine(new Vector3(max.x, max.y, 0), new Vector3(min.x, max.y, 0));
            Gizmos.DrawLine(new Vector3(min.x, max.y, 0), new Vector3(min.x, min.y, 0));
            
            // 绘制生成区域
            Gizmos.color = Color.red;
            float offset = spawnOffset;
            Gizmos.DrawLine(new Vector3(min.x - offset, max.y + offset, 0), new Vector3(max.x + offset, max.y + offset, 0));
        }
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 170, 220, 120));
            GUILayout.Label($"=== Wave Manager ===");
            GUILayout.Label($"Phase: {currentPhaseType}");
            GUILayout.Label($"Name: {CurrentPhaseName}");
            GUILayout.Label($"Spawning: {isSpawning}");
            GUILayout.Label($"Boss Spawned: {bossSpawned}");
            if (EnemyPoolManager.Instance != null)
            {
                GUILayout.Label($"Active Enemies: {EnemyPoolManager.Instance.TotalActiveEnemies}");
            }
            GUILayout.EndArea();
        }
#endif
    }
}