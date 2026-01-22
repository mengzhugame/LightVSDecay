// ============================================================
// WaveManager.cs (重构版 v2.0)
// 文件位置: Assets/Scripts/Logic/WaveManager.cs
// 用途：波次管理器 - 控制整局游戏的波次流程
// 更新：支持间隔刷怪、精英怪、Wave12 BOSS 特殊处理
// ============================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Enemy;
using LightVsDecay.Logic.TacticalDrop;

namespace LightVsDecay.Logic
{
    /// <summary>
    /// 波次状态枚举
    /// </summary>
    public enum WaveState
    {
        Idle,       // 空闲/等待开始
        Countdown,  // 倒计时
        Spawning,   // 刷怪中
        Battle,     // 战斗中（所有怪已生成，等待清场）
        Complete,   // 波次完成
        BossFight,  // BOSS 战
        Victory,    // 胜利
        Defeat      // 失败
    }
    
    /// <summary>
    /// 波次管理器
    /// 负责控制整局游戏的波次流程、敌人生成和胜负判定
    /// </summary>
    public class WaveManager : Singleton<WaveManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("波次配置（ScriptableObject）")]
        [SerializeField] private WaveConfig waveConfig;
        
        [Header("BOSS 相关")]
        [Tooltip("BOSS 预制体")]
        [SerializeField] private GameObject bossPrefab;
        
        [Tooltip("BOSS 生成位置")]
        [SerializeField] private Transform bossSpawnPoint;
        
        [Header("生成设置")]
        [Tooltip("生成区域边界偏移（相对于屏幕边缘）")]
        [SerializeField] private float spawnOffset = 1.5f;
        [Header("精英怪预制体（不走对象池）")]
        [Tooltip("精英坦克预制体")]
        [SerializeField] private GameObject eliteTankPrefab;

        [Tooltip("精英漂流者预制体")]
        [SerializeField] private GameObject eliteDrifterPrefab;
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool showSpawnArea = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private WaveState currentState = WaveState.Idle;
        private int currentWaveNumber = 0;
        private WaveData currentWaveData;
        
        // 刷怪统计
        private float waveTimer;
        private int enemiesSpawned;
        private int enemiesKilled;
        private int totalEnemiesInWave;
        
// 协程管理
        private Coroutine waveIntervalCoroutine;
        private List<Coroutine> activeSpawnCoroutines = new List<Coroutine>();
        private int activeSpawnCoroutineCount = 0;  // ★ 新增：追踪活跃协程数量
        
        // BOSS 相关
        private bool bossSpawned = false;
        private GameObject currentBossInstance;
        
        // 屏幕边界
        private Camera gameCamera;
        private Vector2 screenMin;
        private Vector2 screenMax;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public WaveState CurrentState => currentState;
        public int CurrentWaveNumber => currentWaveNumber;
        public int TotalWaves => waveConfig != null ? waveConfig.WaveCount : 12;
        public string CurrentWaveName => currentWaveData?.displayName ?? "";
        public string CurrentWaveHint => currentWaveData?.hintText ?? "";
        public bool IsBossWave => currentWaveData?.isBossWave ?? false;
        public float WaveProgress => totalEnemiesInWave > 0 ? (float)enemiesKilled / totalEnemiesInWave : 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            gameCamera = Camera.main;
            CalculateScreenBounds();
        }
        
        private void OnEnable()
        {
            // 订阅事件
            GameEvents.OnGameStart += OnGameStart;
            GameEvents.OnEnemyDied += OnEnemyDied;
            GameEvents.OnBossDeath += OnBossDied;
        }
        
        private void OnDisable()
        {
            GameEvents.OnGameStart -= OnGameStart;
            GameEvents.OnEnemyDied -= OnEnemyDied;
            GameEvents.OnBossDeath -= OnBossDied;
        }
        
        private void Update()
        {
            if (currentState == WaveState.Spawning)
            {
                ProcessSpawning();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnGameStart()
        {
            if (showDebugInfo)
            {
                Debug.Log("[WaveManager] 游戏开始！准备第一波");
            }
            
            currentWaveNumber = 0;
            bossSpawned = false;
            StartNextWave();
        }
        
        private void OnEnemyDied(EnemyType type, Vector3 pos, int xp, int coin)
        {
            // 只在刷怪或战斗状态计数
            if (currentState != WaveState.Spawning && currentState != WaveState.Battle)
                return;
            
            enemiesKilled++;
            
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] 敌人击杀: {enemiesKilled}/{totalEnemiesInWave}");
            }
            
            CheckWaveComplete();
        }
        
        private void OnBossDied()
        {
            if (currentState != WaveState.BossFight) return;
    
            if (showDebugInfo)
            {
                Debug.Log("[WaveManager] ★★★ BOSS 已击败！★★★");
            }
    
            // BOSS 死亡 = 清除所有残余小怪
            ClearAllEnemies();
    
            // ★★★ 【V3.0 修复】先触发波次完成，让 BattleStatistics 记录 W12 数据 ★★★
            GameEvents.TriggerWaveComplete(currentWaveNumber, TotalWaves);
    
            // 然后触发胜利
            ChangeState(WaveState.Victory);
            GameEvents.TriggerGameVictory();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态控制
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ChangeState(WaveState newState)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] 状态切换: {currentState} -> {newState}");
            }
            
            currentState = newState;
            GameEvents.TriggerWaveStateChanged(newState, currentWaveNumber);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 波次流程
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 开始下一波
        /// </summary>
        public void StartNextWave()
        {
            currentWaveNumber++;
            
            if (currentWaveNumber > TotalWaves)
            {
                // 所有波次完成（理论上不应该走到这里，因为 W12 是 BOSS）
                ChangeState(WaveState.Victory);
                GameEvents.TriggerGameVictory();
                return;
            }
            
            // 获取波次配置
            currentWaveData = waveConfig?.GetWave(currentWaveNumber);
            // ★★★ 调试：打印所有 SpawnGroup 的详细信息 ★★★
            if (showDebugInfo && currentWaveData != null)
            {
                Debug.Log($"[WaveManager] ====== 波次 {currentWaveNumber} SpawnGroups 详情 ======");
                Debug.Log($"[WaveManager] WaveConfig 资源: {waveConfig.name}");
                Debug.Log($"[WaveManager] SpawnGroups 数量: {currentWaveData.spawnGroups.Count}");
    
                for (int i = 0; i < currentWaveData.spawnGroups.Count; i++)
                {
                    var g = currentWaveData.spawnGroups[i];
                    Debug.Log($"[WaveManager]   [{i}] {g.enemyType} x{g.count} @ {g.spawnTime}s | Pattern={g.pattern}");
                }
                Debug.Log($"[WaveManager] ================================================");
            }
            if (currentWaveData == null)
            {
                Debug.LogError($"[WaveManager] 找不到波次 {currentWaveNumber} 的配置！");
                return;
            }
            
            // 检查是否为 BOSS 波
            if (currentWaveData.isBossWave)
            {
                StartBossWave();
                return;
            }
            
            // 正常波次
            InitializeWave();
            
            // 广播波次开始事件
            GameEvents.TriggerWaveStart(currentWaveNumber, TotalWaves);
            
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] ========== 波次 {currentWaveNumber}/{TotalWaves} 开始！ ==========");
                Debug.Log($"[WaveManager] 名称: {currentWaveData.displayName}");
                Debug.Log($"[WaveManager] 提示: {currentWaveData.hintText}");
                Debug.Log($"[WaveManager] 敌人总数: {totalEnemiesInWave}");
                Debug.Log($"[WaveManager] 难度倍率: x{currentWaveData.difficultyMultiplier}");
            }
            
            ChangeState(WaveState.Spawning);
        }
        
        /// <summary>
        /// 初始化波次数据
        /// </summary>
        private void InitializeWave()
        {
            waveTimer = 0f;
            enemiesSpawned = 0;
            enemiesKilled = 0;
            totalEnemiesInWave = currentWaveData.TotalEnemyCount;
            
            // 重置所有刷怪组状态
            currentWaveData.ResetSpawnStates();
            
            // 停止所有进行中的刷怪协程
            StopAllSpawnCoroutines();
            // ★ 重置协程计数器
            activeSpawnCoroutineCount = 0;
        }
        
        /// <summary>
        /// 开始 BOSS 波（特殊处理）
        /// </summary>
        private void StartBossWave()
        {
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] ========== BOSS 波开始！ ==========");
                Debug.Log($"[WaveManager] {currentWaveData.hintText}");
            }
            
            // 清空场上所有小怪
            ClearAllEnemies();
            
            // 广播波次开始
            GameEvents.TriggerWaveStart(currentWaveNumber, TotalWaves);
            
            // 进入 BOSS 战状态
            ChangeState(WaveState.BossFight);
            
            // 生成 BOSS（延迟一帧确保清场完成）
            StartCoroutine(SpawnBossDelayed());
        }
        
        private IEnumerator SpawnBossDelayed()
        {
            yield return null; // 等待一帧
            SpawnBoss();
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
            
            Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.position : new Vector3(0, 10f, 0);
            currentBossInstance = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
            bossSpawned = true;
            
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] BOSS 已生成 @ {spawnPos}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 刷怪执行器（支持间隔生成）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 处理刷怪逻辑（每帧调用）
        /// </summary>
        private void ProcessSpawning()
        {
            if (currentWaveData == null) return;
            if (EnemyPoolManager.Instance == null) return;
            
            waveTimer += Time.deltaTime;
            
            bool allGroupsStarted = true;
            
            foreach (var group in currentWaveData.spawnGroups)
            {
                if (group.hasSpawned) continue;
                
                allGroupsStarted = false;
                
                // 检查是否到达生成时间
                if (waveTimer >= group.spawnTime)
                {
                    StartSpawnGroup(group);
                    group.hasSpawned = true;
                }
            }
            
            if (allGroupsStarted && activeSpawnCoroutineCount == 0)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[WaveManager] 所有敌人已生成！共 {enemiesSpawned} 只");
                }
    
                ChangeState(WaveState.Battle);
    
                // ★ BUG FIX: 切换到 Battle 后立即检查波次是否已完成
                // 避免敌人在 Spawning 阶段就被击杀完毕导致无法进入下一波
                CheckWaveComplete();
            }
        }
        
        /// <summary>
        /// 开始执行单个刷怪组（支持间隔生成）
        /// </summary>
        private void StartSpawnGroup(SpawnGroup group)
        {
            float interval = group.GetInterval();
            
            if (interval <= 0f || group.count == 1)
            {
                // 瞬间生成所有
                SpawnAllAtOnce(group);
            }
            else
            {
                // 间隔生成
                activeSpawnCoroutineCount++;  // ★ 计数器+1
                Coroutine co = StartCoroutine(SpawnWithInterval(group));
                activeSpawnCoroutines.Add(co);
            }
            
            if (showDebugInfo)
            {
                string patternStr = interval <= 0 ? "Instant" : $"{group.pattern}({interval}s)";
                Debug.Log($"[WaveManager] 刷怪组: {group.enemyType} x{group.count} @ {group.spawnTime}s | {patternStr}");
            }
        }
        
        /// <summary>
        /// 一次性生成所有敌人
        /// </summary>
        private void SpawnAllAtOnce(SpawnGroup group)
        {
            for (int i = 0; i < group.count; i++)
            {
                if (EnemyPoolManager.Instance.IsAtGlobalCapacity)
                {
                    Debug.LogWarning("[WaveManager] 达到全局敌人上限！");
                    break;
                }
                
                SpawnSingleEnemy(group);
            }
        }
        
        /// <summary>
        /// 间隔生成敌人协程
        /// </summary>
        private IEnumerator SpawnWithInterval(SpawnGroup group)
        {
            float interval = group.GetInterval();
    
            for (int i = 0; i < group.count; i++)
            {
                if (currentState != WaveState.Spawning && currentState != WaveState.Battle)
                {
                    // 状态改变，停止生成
                    break;  // ★ 改为 break，确保执行 finally 逻辑
                }
        
                if (EnemyPoolManager.Instance.IsAtGlobalCapacity)
                {
                    Debug.LogWarning("[WaveManager] 达到全局敌人上限！");
                    break;  // ★ 改为 break，确保执行 finally 逻辑
                }
        
                SpawnSingleEnemy(group);
        
                // 最后一只不需要等待
                if (i < group.count - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }
    
            // ★ 协程结束时，计数器-1
            activeSpawnCoroutineCount--;
    
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] 刷怪组完成: {group.enemyType} x{group.count}, 剩余活跃协程: {activeSpawnCoroutineCount}");
            }
        }
        
        /// <summary>
        /// 生成单个敌人
        /// </summary>
        private void SpawnSingleEnemy(SpawnGroup group)
        {
            // ★ 精英怪：直接 Instantiate，不走对象池（必须放在最前面！）
            if (group.enemyType == EnemyType.EliteTank || group.enemyType == EnemyType.EliteDrifter)
            {
                SpawnEliteEnemy(group);
                return;
            }

            // ★ 检查对象池（仅对普通敌人）
            if (!EnemyPoolManager.Instance.HasPool(group.enemyType))
            {
                Debug.LogWarning($"[WaveManager] 敌人类型 {group.enemyType} 没有对象池！");
                return;
            }

            // ★ Drifter：使用特殊生成逻辑（屏幕内 + 传送门）
            if (group.enemyType == EnemyType.Drifter)
            {
                SpawnDrifterSpecial(group);
                return;
            }

            // ★ 普通敌人：走对象池
            Vector3 position = GetSpawnPosition(group.spawnZone);
            EnemyBlob enemy = EnemyPoolManager.Instance.Spawn(group.enemyType, position);

            if (enemy != null)
            {
                ApplyDifficultyModifiers(enemy, group);
                // ═══════════════════════════════════════════════════════════
                // 【新增】上报敌人血量到 BattleStatistics
                // ═══════════════════════════════════════════════════════════
                if (BattleStatistics.Instance != null)
                {
                    BattleStatistics.Instance.RecordEnemyHP(enemy.MaxHealth);
                }
                enemiesSpawned++;
            }
        }
        /// <summary>
        /// Drifter 特殊生成（屏幕内 + 传送门特效）
        /// </summary>
        private void SpawnDrifterSpecial(SpawnGroup group)
        {
            if (DrifterSpawnHelper.Instance == null)
            {
                Debug.LogError("[WaveManager] DrifterSpawnHelper 不存在！使用普通生成");
                // 降级：使用普通生成
                Vector3 position = GetSpawnPosition(group.spawnZone);
                EnemyBlob enemy = EnemyPoolManager.Instance.Spawn(group.enemyType, position);
                if (enemy != null)
                {
                    ApplyDifficultyModifiers(enemy, group);
                    // ═══════════════════════════════════════════════════════════
                    // 【新增】上报敌人血量到 BattleStatistics
                    // ═══════════════════════════════════════════════════════════
                    if (BattleStatistics.Instance != null)
                    {
                        BattleStatistics.Instance.RecordEnemyHP(enemy.MaxHealth);
                    }
                    enemiesSpawned++;
                }
                return;
            }
    
            // 使用 DrifterSpawnHelper 生成
            DrifterSpawnHelper.Instance.SpawnDrifter((drifter) =>
            {
                if (drifter != null)
                {
                    ApplyDifficultyModifiers(drifter, group);
                    // ═══════════════════════════════════════════════════════════
                    // 【新增】上报敌人血量到 BattleStatistics
                    // ═══════════════════════════════════════════════════════════
                    if (BattleStatistics.Instance != null)
                    {
                        BattleStatistics.Instance.RecordEnemyHP(drifter.MaxHealth);
                    }
                    enemiesSpawned++;
            
                    if (showDebugInfo)
                    {
                        Debug.Log($"[WaveManager] Drifter 特殊生成完成 @ {drifter.transform.position}");
                    }
                }
            });
        }
        /// <summary>
        /// 生成精英怪（直接 Instantiate，不走对象池）
        /// </summary>
        private void SpawnEliteEnemy(SpawnGroup group)
        {
            GameObject prefab = null;
    
            switch (group.enemyType)
            {
                case EnemyType.EliteTank:
                    prefab = eliteTankPrefab;
                    break;
                case EnemyType.EliteDrifter:
                    prefab = eliteDrifterPrefab;
                    break;
            }
    
            if (prefab == null)
            {
                Debug.LogError($"[WaveManager] 精英怪预制体未设置: {group.enemyType}，请在 Inspector 中配置！");
                return;
            }
    
            Vector3 position = GetSpawnPosition(group.spawnZone);
            GameObject eliteObj = Instantiate(prefab, position, Quaternion.identity);
    
            EnemyBlob enemy = eliteObj.GetComponent<EnemyBlob>();
            if (enemy != null)
            {
                // 应用难度倍率
                ApplyDifficultyModifiers(enemy, group);
                // ═══════════════════════════════════════════════════════════
                // 【新增】上报敌人血量到 BattleStatistics
                // ═══════════════════════════════════════════════════════════
                if (BattleStatistics.Instance != null)
                {
                    BattleStatistics.Instance.RecordEnemyHP(enemy.MaxHealth);
                }
                enemiesSpawned++;
        
                if (showDebugInfo)
                {
                    Debug.Log($"[WaveManager] ⭐ 精英怪已生成: {group.enemyType} @ {position}");
                }
            }
            else
            {
                Debug.LogError($"[WaveManager] 精英怪预制体缺少 EnemyBlob 组件: {group.enemyType}");
            }
        }
        /// <summary>
        /// 应用波次难度修正
        /// </summary>
        private void ApplyDifficultyModifiers(EnemyBlob enemy, SpawnGroup group)
        {
            if (enemy == null || currentWaveData == null) return;
            
            float waveDifficulty = currentWaveData.difficultyMultiplier;
            
            // 计算最终难度修正（考虑精英加成）
            DifficultyModifiers modifiers = new DifficultyModifiers
            {
                hpMultiplier = waveDifficulty,
                speedMultiplier = Mathf.Min(waveDifficulty * group.speedMultiplier, 2.5f),
                massMultiplier = 1f + (waveDifficulty - 1f) * 0.3f,
                damageMultiplier = waveDifficulty
            };
            
            enemy.SetWaveModifiers(modifiers);
        }
        
        /// <summary>
        /// 停止所有刷怪协程
        /// </summary>
        private void StopAllSpawnCoroutines()
        {
            foreach (var co in activeSpawnCoroutines)
            {
                if (co != null)
                {
                    StopCoroutine(co);
                }
            }
            activeSpawnCoroutines.Clear();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 进度监控
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 检查波次是否完成
        /// </summary>
        private void CheckWaveComplete()
        {
            if (currentState != WaveState.Battle) return;
            
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
            
            GameEvents.TriggerWaveComplete(currentWaveNumber, TotalWaves);
            
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] ========== 波次 {currentWaveNumber}/{TotalWaves} 完成！ ==========");
                Debug.Log($"[WaveManager] 击杀数: {enemiesKilled}");
            }
            
            StartWaveInterval();
        }
        
        /// <summary>
        /// 开始波次间隔
        /// </summary>
        private void StartWaveInterval()
        {
// 【新增】如果有宝箱系统，交给宝箱系统处理
            if (TacticalDropManager.Instance != null)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[WaveManager] 宝箱系统已激活，等待玩家选择...");
                }
        
                // 修复：启动超时/故障检测协程。如果宝箱没出来，自动跳过。
                StartCoroutine(ProtectDropPhaseRoutine());
        
                return; 
            }
      
            // 原有逻辑（没有宝箱系统时使用）
            if (waveIntervalCoroutine != null)
            {
                StopCoroutine(waveIntervalCoroutine);
            }
      
            waveIntervalCoroutine = StartCoroutine(WaveIntervalCoroutine());
        }
        // 2. 新增 ProtectDropPhaseRoutine 方法
        private IEnumerator ProtectDropPhaseRoutine()
        {
            // 等待一小段时间，让 TacticalDropManager 有机会生成宝箱
            yield return new WaitForSeconds(2.0f);

            // 检查场上是否有宝箱
            // 注意：需要引用 LightVsDecay.Logic.TacticalDrop 命名空间
            var crates = FindObjectsOfType<TacticalCrate>();
    
            // 如果处于 Complete 状态（说明还没进下一波），且场上没有宝箱
            if (currentState == WaveState.Complete && (crates == null || crates.Length == 0))
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("[WaveManager] ⚠️ 检测到 Drop 阶段异常（无宝箱），强制进入下一波！");
                }
        
                // 强制开始下一波，防止卡死
                StartNextWave();
            }
        }
        private IEnumerator WaveIntervalCoroutine()
        {
            float interval = waveConfig != null ? waveConfig.waveInterval : 10f;
            
            if (showDebugInfo)
            {
                Debug.Log($"[WaveManager] 波次间隔: {interval}s");
            }
            
            yield return new WaitForSeconds(interval);
            
            StartNextWave();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 辅助方法
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
                Debug.Log("[WaveManager] 已清除所有敌人");
            }
        }
        
        /// <summary>
        /// 计算屏幕边界
        /// </summary>
        private void CalculateScreenBounds()
        {
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameCamera == null) return;
            
            float height = gameCamera.orthographicSize;
            float width = height * gameCamera.aspect;
            
            screenMin = new Vector2(-width, -height);
            screenMax = new Vector2(width, height);
        }
        
        /// <summary>
        /// 获取生成位置
        /// </summary>
        private Vector3 GetSpawnPosition(SpawnZone zone)
        {
            float x = 0f, y = 0f;
            float offset = spawnOffset;
            
            // ★ 关键修复：定义安全Y坐标下限（塔的位置通常在屏幕下方）
            // 假设塔在 Y=screenMin.y 附近，敌人应该从塔上方生成
            // 左右两侧的最低点不应低于屏幕中间偏上的位置
            float towerY = screenMin.y; // 塔的大致Y位置
            float minSideY = towerY + (screenMax.y - towerY) * 0.3f; // 左右侧最低生成点（塔上方30%处）
            
            switch (zone)
            {
                case SpawnZone.TopOnly:
                    x = Random.Range(screenMin.x * 0.9f, screenMax.x * 0.9f);
                    y = screenMax.y + offset;
                    break;
                    
                case SpawnZone.TopRandom:
                    x = Random.Range(screenMin.x * 0.7f, screenMax.x * 0.7f);
                    y = screenMax.y + offset + Random.Range(0f, offset);
                    break;
                    
                case SpawnZone.LeftSide:
                    x = screenMin.x - offset;
                    // ★ 修复：Y下限提高到塔上方，确保在激光攻击范围内
                    y = Random.Range(minSideY, screenMax.y + offset * 0.5f);
                    break;
                    
                case SpawnZone.RightSide:
                    x = screenMax.x + offset;
                    // ★ 修复：Y下限提高到塔上方
                    y = Random.Range(minSideY, screenMax.y + offset * 0.5f);
                    break;
                    
                case SpawnZone.SideRandom:
                    if (Random.value < 0.5f)
                    {
                        x = screenMin.x - offset;
                    }
                    else
                    {
                        x = screenMax.x + offset;
                    }
                    // ★ 修复：Y下限提高到塔上方
                    y = Random.Range(minSideY, screenMax.y + offset * 0.5f);
                    break;
                    
                case SpawnZone.BottomCorners:
                    // ★ 修复：底部角落改为侧面中上方（不再从底部生成）
                    if (Random.value < 0.5f)
                    {
                        x = screenMin.x - offset;
                    }
                    else
                    {
                        x = screenMax.x + offset;
                    }
                    // 在侧面中间偏上的位置
                    y = Random.Range(minSideY, screenMax.y * 0.5f);
                    break;
                    
                case SpawnZone.AllEdges:
                default:
                    // ★ 修复：只有3边（上、左、右），移除底部
                    int edge = Random.Range(0, 3); // 改为0-2，移除底部
                    switch (edge)
                    {
                        case 0: // 上
                            x = Random.Range(screenMin.x, screenMax.x);
                            y = screenMax.y + offset;
                            break;
                        case 1: // 右（上半部分）
                            x = screenMax.x + offset;
                            y = Random.Range(minSideY, screenMax.y + offset * 0.5f);
                            break;
                        default: // 左（上半部分）
                            x = screenMin.x - offset;
                            y = Random.Range(minSideY, screenMax.y + offset * 0.5f);
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
        /// 跳过当前宝箱阶段，直接开始下一波（调试用）
        /// </summary>
        public void SkipDropPhase()
        {
            if (TacticalDropManager.Instance != null && TacticalDropManager.Instance.IsDropPhase)
            {
                // 清理宝箱
                foreach (var crate in FindObjectsOfType<TacticalCrate>())
                {
                    Destroy(crate.gameObject);
                }
         
                // 直接开始下一波
                StartNextWave();
            }
        }  
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
            StopAllSpawnCoroutines();
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
            
            Gizmos.color = Color.yellow;
            Vector3 min = new Vector3(screenMin.x, screenMin.y, 0f);
            Vector3 max = new Vector3(screenMax.x, screenMax.y, 0f);
            
            Gizmos.DrawLine(new Vector3(min.x, min.y, 0), new Vector3(max.x, min.y, 0));
            Gizmos.DrawLine(new Vector3(max.x, min.y, 0), new Vector3(max.x, max.y, 0));
            Gizmos.DrawLine(new Vector3(max.x, max.y, 0), new Vector3(min.x, max.y, 0));
            Gizmos.DrawLine(new Vector3(min.x, max.y, 0), new Vector3(min.x, min.y, 0));
            
            Gizmos.color = Color.red;
            float offset = spawnOffset;
            Gizmos.DrawLine(new Vector3(min.x - offset, max.y + offset, 0), new Vector3(max.x + offset, max.y + offset, 0));
        }
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 170, 280, 200));
            GUILayout.Label("=== Wave Manager (v2.0) ===");
            GUILayout.Label($"状态: {currentState}");
            GUILayout.Label($"波次: {currentWaveNumber}/{TotalWaves}");
            GUILayout.Label($"名称: {CurrentWaveName}");
            GUILayout.Label($"敌人: {enemiesKilled}/{totalEnemiesInWave} (已生成: {enemiesSpawned})");
            GUILayout.Label($"计时: {waveTimer:F1}s");
            GUILayout.Label($"BOSS: {(bossSpawned ? "已生成" : "未生成")}");
            GUILayout.Label($"活动协程: {activeSpawnCoroutines.Count}");
            
            if (EnemyPoolManager.Instance != null)
            {
                GUILayout.Label($"场上敌人: {EnemyPoolManager.Instance.TotalActiveEnemies}");
            }
            
            GUILayout.EndArea();
        }
#endif
    }
}