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
        
        // BOSS相关
        private bool bossSpawned = false;
        private float bossMinionTimer = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public bool IsSpawning => isSpawning;
        public GamePhase CurrentPhase => currentPhaseType;
        public string CurrentPhaseName => currentPhase?.displayName ?? "未知";
        
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
            if (!isSpawning) return;
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            
            float gameTime = GameManager.Instance.GameTimer;
            
            // 更新当前阶段
            UpdateCurrentPhase(gameTime);
            
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
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[WaveManager] 开始生成敌人");
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
            if (waveConfig == null) return;
            
            PhaseConfig newPhase = waveConfig.GetPhaseAtTime(gameTime);
            
            if (newPhase != null && newPhase != currentPhase)
            {
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
            
            // 处理阶段开始事件
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
                
                if (enemy != null && entry.speedMultiplier != 1f)
                {
                    enemy.SetSpeedMultiplier(entry.speedMultiplier);
                }
            }
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
                    
                default:
                    return GetRandomEdgePosition();
            }
        }
        
        private Vector3 GetRandomEdgePosition()
        {
            int edge = Random.Range(0, 3); // 0=上, 1=左, 2=右
            
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
                        Random.Range(screenMin.y, screenMax.y),
                        0f
                    );
                case 2: // 右
                    return new Vector3(
                        screenMax.x + spawnOffset,
                        Random.Range(screenMin.y, screenMax.y),
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
            float y = Random.Range(screenMin.y + 1f, screenMax.y);
            return new Vector3(x, y, 0f);
        }
        
        private Vector3 GetBottomCornerPosition()
        {
            bool isLeft = Random.value > 0.5f;
            float x = isLeft ? screenMin.x - spawnOffset : screenMax.x + spawnOffset;
            float y = screenMin.y + spawnOffset;
            return new Vector3(x, y, 0f);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BOSS相关
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void SpawnBoss()
        {
            if (bossSpawned) return;
            
            bossSpawned = true;
            
            // TODO: 生成BOSS
            Debug.Log("[WaveManager] BOSS 生成！");
            
            // BOSS生成位置（屏幕上方中央）
            Vector3 bossPosition = new Vector3(0f, screenMax.y + 2f, 0f);
            
            // TODO: 实际BOSS生成逻辑
            // BossController.Instance.SpawnBoss(bossPosition);
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