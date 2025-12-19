// ============================================================
// WaveManager.cs (重构版)
// 文件位置: Assets/Scripts/Logic/WaveManager.cs
// 用途：波次管理器 - 状态机驱动，基于波次序列
// ============================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Enemy;

namespace LightVsDecay.Logic
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 波次状态枚举
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 波次状态
    /// </summary>
    public enum WaveState
    {
        /// <summary>等待开始（商店/骰子阶段）</summary>
        Waiting,
        
        /// <summary>正在按时间轴生成敌人</summary>
        Spawning,
        
        /// <summary>敌人生成完毕，等待玩家清场</summary>
        Battle,
        
        /// <summary>波次胜利，结算奖励</summary>
        Complete,
        
        /// <summary>BOSS 战</summary>
        BossFight
    }

    /// <summary>
    /// 波次管理器
    /// 状态机驱动，控制 12 波敌人生成节奏
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
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool showSpawnArea = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态机
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private WaveState currentState = WaveState.Waiting;
        private int currentWaveNumber = 0;  // 当前波次（1-based）
        private WaveData currentWaveData;   // 当前波次配置
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 进度监控
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int totalEnemiesInWave = 0;     // 本波总敌人数（从配置读取）
        private int enemiesSpawned = 0;         // 已生成敌人数
        private int enemiesKilled = 0;          // 已击杀敌人数
        private float waveTimer = 0f;           // 波次计时器
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BOSS 相关
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private GameObject currentBossInstance;
        private bool bossSpawned = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 屏幕边界缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Vector2 screenMin;
        private Vector2 screenMax;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 协程引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Coroutine waveIntervalCoroutine;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前波次状态</summary>
        public WaveState CurrentState => currentState;
        
        /// <summary>当前波次编号（1-based）</summary>
        public int CurrentWaveNumber => currentWaveNumber;
        
        /// <summary>总波次数</summary>
        public int TotalWaves => waveConfig != null ? waveConfig.totalWaves : 12;
        
        /// <summary>本波总敌人数</summary>
        public int TotalEnemiesInWave => totalEnemiesInWave;
        
        /// <summary>已击杀敌人数</summary>
        public int EnemiesKilled => enemiesKilled;
        
        /// <summary>当前波次名称</summary>
        public string CurrentWaveName => currentWaveData?.displayName ?? "---";
        
        /// <summary>波次进度（0-1）用于进度条</summary>
        public float WaveProgress => TotalWaves > 0 ? (float)(currentWaveNumber - 1) / TotalWaves : 0f;
        
        /// <summary>是否在 BOSS 战</summary>
        public bool IsBossFight => currentState == WaveState.BossFight;
        
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
        }
        
        private void OnEnable()
        {
            // 订阅事件
            GameEvents.OnGameStart += OnGameStart;
            GameEvents.OnGameStateChanged += OnGameStateChanged;
            GameEvents.OnEnemyDied += OnEnemyDied;
            GameEvents.OnBossDeath += OnBossDefeated;
        }
        
        private void OnDisable()
        {
            // 取消订阅
            GameEvents.OnGameStart -= OnGameStart;
            GameEvents.OnGameStateChanged -= OnGameStateChanged;
            GameEvents.OnEnemyDied -= OnEnemyDied;
            GameEvents.OnBossDeath -= OnBossDefeated;
        }
        
        private void Update()
        {
            if (currentState == WaveState.Spawning)
            {
                ProcessSpawning();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态机控制
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 切换状态
        /// </summary>
        private void ChangeState(WaveState newState)
        {
            if (currentState == newState) return;
            
            WaveState oldState = currentState;
            currentState = newState;
            
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] 状态切换: {oldState} → {newState}");
            }
            
            // 触发状态变化事件
            GameEvents.TriggerWaveStateChanged(newState, currentWaveNumber);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 游戏流程
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 游戏开始回调
        /// </summary>
        private void OnGameStart()
        {
            if (showDebugInfo)
            {
                Debug.Log("[WaveManager] 游戏开始！");
            }
            
            // 重置状态
            currentWaveNumber = 0;
            bossSpawned = false;
            
            // 开始第一波
            StartNextWave();
        }
        
        /// <summary>
        /// 游戏状态变化回调
        /// </summary>
        private void OnGameStateChanged(GameState state)
        {
            if (state == GameState.Victory || state == GameState.Defeat)
            {
                // 游戏结束，停止所有波次逻辑
                StopAllCoroutines();
                ChangeState(WaveState.Waiting);
            }
        }
        
        /// <summary>
        /// 开始下一波
        /// </summary>
        public void StartNextWave()
        {
            currentWaveNumber++;
            
            if (currentWaveNumber > TotalWaves)
            {
                // 所有波次完成，触发胜利
                if (showDebugInfo)
                {
                    Debug.Log("[WaveManager] 所有波次完成！游戏胜利！");
                }
                GameManager.Instance?.TriggerVictory();
                return;
            }
            
            // 加载当前波次配置
            currentWaveData = waveConfig.GetWave(currentWaveNumber);
            
            if (currentWaveData == null)
            {
                Debug.LogError($"[WaveManager] 无法加载波次 {currentWaveNumber} 的配置！");
                return;
            }
            
            // 检查是否为 BOSS 波
            if (currentWaveData.isBossWave)
            {
                StartBossWave();
                return;
            }
            
            // 普通波次初始化
            InitializeWave();
            
            // 广播波次开始事件
            GameEvents.TriggerWaveStart(currentWaveNumber, TotalWaves);
            
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] ========== 波次 {currentWaveNumber}/{TotalWaves} 开始 ==========");
                Debug.Log($"[WaveManager] 名称: {currentWaveData.displayName}");
                Debug.Log($"[WaveManager] 总敌人数: {totalEnemiesInWave}");
                Debug.Log($"[WaveManager] 难度倍率: {currentWaveData.difficultyMultiplier}x");
            }
            
            // 进入生成状态
            ChangeState(WaveState.Spawning);
        }
        
        /// <summary>
        /// 初始化波次数据
        /// </summary>
        private void InitializeWave()
        {
            // 重置计数器
            waveTimer = 0f;
            enemiesSpawned = 0;
            enemiesKilled = 0;
            
            // 从配置读取总敌人数
            totalEnemiesInWave = currentWaveData.TotalEnemyCount;
            
            // 重置所有刷怪组的状态
            currentWaveData.ResetSpawnStates();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 刷怪执行器
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 处理刷怪逻辑（每帧调用）
        /// </summary>
        private void ProcessSpawning()
        {
            if (currentWaveData == null) return;
            if (EnemyPoolManager.Instance == null) return;
            
            // 更新波次计时器
            waveTimer += Time.deltaTime;
            
            // 遍历所有刷怪组
            bool allGroupsSpawned = true;
            
            foreach (var group in currentWaveData.spawnGroups)
            {
                // 跳过已执行的组
                if (group.hasSpawned) continue;
                
                allGroupsSpawned = false;
                
                // 检查是否到达生成时间
                if (waveTimer >= group.spawnTime)
                {
                    SpawnGroup(group);
                    group.hasSpawned = true;
                }
            }
            
            // 所有组都已生成，进入战斗状态
            if (allGroupsSpawned && currentState == WaveState.Spawning)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[WaveManager] 所有敌人已生成！共 {enemiesSpawned} 只");
                }
                
                ChangeState(WaveState.Battle);
            }
        }
        
        /// <summary>
        /// 执行单个刷怪组
        /// </summary>
        private void SpawnGroup(SpawnGroup group)
        {
            if (!EnemyPoolManager.Instance.HasPool(group.enemyType))
            {
                Debug.LogWarning($"[WaveManager] 敌人类型 {group.enemyType} 没有对象池！");
                return;
            }
            
            for (int i = 0; i < group.count; i++)
            {
                // 检查全局上限
                if (EnemyPoolManager.Instance.IsAtGlobalCapacity)
                {
                    Debug.LogWarning("[WaveManager] 达到全局敌人上限！");
                    break;
                }
                
                // 获取生成位置
                Vector3 position = GetSpawnPosition(group.spawnZone);
                
                // 生成敌人
                EnemyBlob enemy = EnemyPoolManager.Instance.Spawn(group.enemyType, position);
                
                if (enemy != null)
                {
                    // 应用难度倍率
                    ApplyDifficultyModifiers(enemy, group);
                    enemiesSpawned++;
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] 刷怪组: {group.enemyType} x{group.count} @ {group.spawnTime}s");
            }
        }
        
        /// <summary>
        /// 应用波次难度修正
        /// </summary>
        private void ApplyDifficultyModifiers(EnemyBlob enemy, SpawnGroup group)
        {
            if (enemy == null || currentWaveData == null) return;
            
            // 获取波次基础难度倍率
            float waveDifficulty = currentWaveData.difficultyMultiplier;
            
            // 获取刷怪组的额外倍率
            float groupHealthMult = group.healthMultiplier;
            float groupSpeedMult = group.speedMultiplier;
            float groupDamageMult = group.damageMultiplier;
            
            // 计算最终难度修正
            DifficultyModifiers modifiers = new DifficultyModifiers
            {
                hpMultiplier = waveDifficulty * groupHealthMult,
                speedMultiplier = Mathf.Min(waveDifficulty * groupSpeedMult, 2.0f), // 速度封顶2倍
                massMultiplier = 1f + (waveDifficulty - 1f) * 0.3f, // 质量增幅较小
                damageMultiplier = waveDifficulty * groupDamageMult
            };
            
            // 应用到敌人
            enemy.SetWaveModifiers(modifiers);
            
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] 应用难度: HP={modifiers.hpMultiplier:F2}x, " +
                          $"Speed={modifiers.speedMultiplier:F2}x, Dmg={modifiers.damageMultiplier:F2}x");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 进度监控
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 敌人死亡回调
        /// </summary>
        private void OnEnemyDied(EnemyType type, Vector3 pos, int xp, int coin)
        {
            // 只在战斗状态计数
            if (currentState != WaveState.Spawning && currentState != WaveState.Battle)
                return;
            
            enemiesKilled++;
            
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] 敌人击杀: {enemiesKilled}/{totalEnemiesInWave}");
            }
            
            // 检查是否清场完成
            CheckWaveComplete();
        }
        
        /// <summary>
        /// 检查波次是否完成
        /// </summary>
        private void CheckWaveComplete()
        {
            // 只在战斗状态检查
            if (currentState != WaveState.Battle) return;
            
            // 胜利条件：击杀数 >= 总数
            if (enemiesKilled >= totalEnemiesInWave)
            {
                OnWaveComplete();
            }
        }
        
        /// <summary>
        /// 波次完成
        /// </summary>
        private void OnWaveComplete()
        {
            ChangeState(WaveState.Complete);
            
            // 广播波次完成事件
            GameEvents.TriggerWaveComplete(currentWaveNumber, TotalWaves);
            
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] ========== 波次 {currentWaveNumber}/{TotalWaves} 完成！ ==========");
                Debug.Log($"[WaveManager] 击杀数: {enemiesKilled}");
            }
            
            // TODO: 播放 UI_Wave_Clear 音效
            // AudioManager.Instance?.PlaySFX("UI_Wave_Clear");
            
            // 开始波次间隔
            StartWaveInterval();
        }
        
        /// <summary>
        /// 开始波次间隔（等待期）
        /// </summary>
        private void StartWaveInterval()
        {
            if (waveIntervalCoroutine != null)
            {
                StopCoroutine(waveIntervalCoroutine);
            }
            
            waveIntervalCoroutine = StartCoroutine(WaveIntervalCoroutine());
        }
        
        /// <summary>
        /// 波次间隔协程
        /// </summary>
        private IEnumerator WaveIntervalCoroutine()
        {
            float interval = waveConfig != null ? waveConfig.waveInterval : 10f;
            
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] 波次间隔开始，{interval} 秒后进入下一波...");
            }
            
            // 等待指定时间
            // TODO: 这里后续替换为骰子动画和商店流程
            yield return new WaitForSeconds(interval);
            
            // 开始下一波
            StartNextWave();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BOSS 战
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 开始 BOSS 波
        /// </summary>
        private void StartBossWave()
        {
            if (showDebugInfo)
            {
                Debug.Log("[WaveManager] ========== BOSS 战开始！ ==========");
            }
            
            // 广播波次开始（BOSS）
            GameEvents.TriggerWaveStart(currentWaveNumber, TotalWaves);
            
            // 进入 BOSS 战状态
            ChangeState(WaveState.BossFight);
            
            // 通知 GameManager 进入 BOSS 战
            GameManager.Instance?.EnterBossFight();
            
            // 生成 BOSS
            SpawnBoss();
            
            // 广播 BOSS 战开始
            GameEvents.TriggerBossFightStart();
        }
        
        /// <summary>
        /// 生成 BOSS
        /// </summary>
        private void SpawnBoss()
        {
            if (bossPrefab == null)
            {
                Debug.LogError("[WaveManager] BOSS 预制体未设置！");
                return;
            }
            
            if (bossSpawned)
            {
                Debug.LogWarning("[WaveManager] BOSS 已经生成过了！");
                return;
            }
            
            // 计算 BOSS 生成位置（屏幕上方）
            Vector3 bossPosition = new Vector3(
                (screenMin.x + screenMax.x) / 2f,  // 屏幕中央
                screenMax.y + 2f,                   // 屏幕上方
                0f
            );
            
            // 生成 BOSS
            currentBossInstance = Instantiate(bossPrefab, bossPosition, Quaternion.identity);
            bossSpawned = true;
            
            // 设置 BOSS 血量
            var bossHealth = currentBossInstance.GetComponent<BossHealth>();
            if (bossHealth != null && waveConfig != null)
            {
                bossHealth.SetMaxHealth(waveConfig.bossHealth);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] BOSS 生成！位置: {bossPosition}");
            }
        }
        
        /// <summary>
        /// BOSS 被击败回调
        /// </summary>
        private void OnBossDefeated()
        {
            if (currentState != WaveState.BossFight) return;
            
            if (showDebugInfo)
            {
                Debug.Log("[WaveManager] ========== BOSS 被击败！ ==========");
            }
            
            // 清除所有残留敌人（作为通关庆祝）
            ClearAllEnemies();
            
            // 触发游戏胜利
            GameManager.Instance?.TriggerVictory();
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
        /// 强制开始下一波（供外部调用，如商店确认按钮）
        /// </summary>
        public void ForceStartNextWave()
        {
            if (waveIntervalCoroutine != null)
            {
                StopCoroutine(waveIntervalCoroutine);
                waveIntervalCoroutine = null;
            }
            
            StartNextWave();
        }
        
        /// <summary>
        /// 获取当前波次的格式化文本
        /// </summary>
        public string GetWaveText()
        {
            return $"波次: {currentWaveNumber}/{TotalWaves}";
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生成位置计算
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
        
        private Vector3 GetSpawnPosition(SpawnZone zone)
        {
            float x, y;
            float offset = spawnOffset;
            
            switch (zone)
            {
                case SpawnZone.TopOnly:
                    x = Random.Range(screenMin.x, screenMax.x);
                    y = screenMax.y + offset;
                    break;
                    
                case SpawnZone.TopRandom:
                    x = Random.Range(screenMin.x + 1f, screenMax.x - 1f);
                    y = screenMax.y + offset;
                    break;
                    
                case SpawnZone.SideRandom:
                    if (Random.value > 0.5f)
                    {
                        x = screenMin.x - offset;
                        y = Random.Range(screenMin.y + 1f, screenMax.y - 1f);
                    }
                    else
                    {
                        x = screenMax.x + offset;
                        y = Random.Range(screenMin.y + 1f, screenMax.y - 1f);
                    }
                    break;
                    
                case SpawnZone.LeftSide:
                    x = screenMin.x - offset;
                    y = Random.Range(screenMin.y + 1f, screenMax.y - 1f);
                    break;
                    
                case SpawnZone.RightSide:
                    x = screenMax.x + offset;
                    y = Random.Range(screenMin.y + 1f, screenMax.y - 1f);
                    break;
                    
                case SpawnZone.BottomCorners:
                    if (Random.value > 0.5f)
                    {
                        x = screenMin.x - offset;
                        y = screenMin.y - offset;
                    }
                    else
                    {
                        x = screenMax.x + offset;
                        y = screenMin.y - offset;
                    }
                    break;
                    
                case SpawnZone.AllEdges:
                default:
                    int edge = Random.Range(0, 4);
                    switch (edge)
                    {
                        case 0: // 上
                            x = Random.Range(screenMin.x, screenMax.x);
                            y = screenMax.y + offset;
                            break;
                        case 1: // 右
                            x = screenMax.x + offset;
                            y = Random.Range(screenMin.y, screenMax.y);
                            break;
                        case 2: // 下
                            x = Random.Range(screenMin.x, screenMax.x);
                            y = screenMin.y - offset;
                            break;
                        default: // 左
                            x = screenMin.x - offset;
                            y = Random.Range(screenMin.y, screenMax.y);
                            break;
                    }
                    break;
            }
            
            return new Vector3(x, y, 0f);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 测试方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 测试：立即生成 BOSS
        /// </summary>
        public void TestSpawnBoss()
        {
            if (bossSpawned)
            {
                Debug.LogWarning("[WaveManager] BOSS 已经生成过了！");
                return;
            }
            
            ChangeState(WaveState.BossFight);
            SpawnBoss();
        }
        
        /// <summary>
        /// 测试：销毁当前 BOSS
        /// </summary>
        public void TestDestroyBoss()
        {
            if (currentBossInstance != null)
            {
                Destroy(currentBossInstance);
                currentBossInstance = null;
                bossSpawned = false;
                Debug.Log("[WaveManager] BOSS 已销毁");
            }
        }
        
        /// <summary>
        /// 测试：跳到指定波次
        /// </summary>
        public void TestSkipToWave(int waveNumber)
        {
            StopAllCoroutines();
            ClearAllEnemies();
            
            currentWaveNumber = waveNumber - 1;
            StartNextWave();
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
            
            GUILayout.BeginArea(new Rect(10, 170, 250, 180));
            GUILayout.Label($"=== Wave Manager ===");
            GUILayout.Label($"状态: {currentState}");
            GUILayout.Label($"波次: {currentWaveNumber}/{TotalWaves}");
            GUILayout.Label($"名称: {CurrentWaveName}");
            GUILayout.Label($"敌人: {enemiesKilled}/{totalEnemiesInWave}");
            GUILayout.Label($"计时: {waveTimer:F1}s");
            GUILayout.Label($"BOSS: {(bossSpawned ? "已生成" : "未生成")}");
            
            if (EnemyPoolManager.Instance != null)
            {
                GUILayout.Label($"场上敌人: {EnemyPoolManager.Instance.TotalActiveEnemies}");
            }
            
            GUILayout.EndArea();
        }
#endif
    }
}