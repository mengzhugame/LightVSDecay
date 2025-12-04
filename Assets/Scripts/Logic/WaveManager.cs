using UnityEngine;
using System.Collections.Generic;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Logic.Enemy;

namespace LightVsDecay.Logic
{
    /// <summary>
    /// 生成位置区域
    /// </summary>
    public enum SpawnZone
    {
        TopCenter,      // 屏幕正上方
        TopLeft,        // 左上
        TopRight,       // 右上
        Left,           // 左边
        Right,          // 右边
        TopRandom,      // 上方随机（左上/中上/右上）
        SideRandom,     // 两侧随机（左/右）
        AllEdges        // 三边随机（上/左/右）
    }
    
    /// <summary>
    /// 游戏阶段
    /// </summary>
    public enum GamePhase
    {
        WarmUp1,        // 0:00-0:30 适应期前半
        WarmUp2,        // 0:30-1:00 适应期后半
        TheWall1,       // 1:00-1:30 Tank登场
        TheWall2,       // 1:30-2:00 Drifter展示
        TheSwarm1,      // 2:00-2:30 Rusher登场
        TheSwarm2,      // 2:30-3:00 夹击战术
        Chaos,          // 3:00-4:00 全家桶混乱
        Frenzy,         // 4:00-4:45 狂暴潮
        Silence,        // 4:45-5:00 静默期
        Boss            // 5:00+ Boss战
    }
    
    /// <summary>
    /// 刷怪管理器
    /// 实现完整5分钟波次系统，支持暂停/恢复、Boss预留
    /// </summary>
    public class WaveManager : PersistentSingleton<WaveManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("相机引用")]
        [SerializeField] private Camera gameCamera;
        
        [Header("生成区域设置")]
        [Tooltip("屏幕外偏移量")]
        [SerializeField] private float outsideOffset = 1.5f;
        
        [Tooltip("生成点随机散布范围")]
        [SerializeField] private float spawnSpread = 0.5f;
        
        [Header("狂暴模式设置")]
        [Tooltip("狂暴模式刷新间隔倍率（0.5 = 快50%）")]
        [SerializeField] private float frenzySpawnRateMultiplier = 0.5f;
        
        [Tooltip("狂暴模式敌人速度倍率")]
        [SerializeField] private float frenzySpeedMultiplier = 1.2f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool showSpawnGizmos = true;
        
        [Header("测试选项")]
        [Tooltip("快速测试：时间流速倍率（1=正常，2=两倍速）")]
        [SerializeField] private float timeScale = 1f;
        
        [Tooltip("从指定时间开始（秒，0=从头开始）")]
        [SerializeField] private float startFromTime = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>阶段变化时触发</summary>
        public event System.Action<GamePhase> OnPhaseChanged;
        
        /// <summary>Boss降临时触发</summary>
        public event System.Action OnBossSpawn;
        
        /// <summary>静默期开始时触发（可用于UI警告）</summary>
        public event System.Action OnSilenceBegin;
        
        /// <summary>狂暴模式开始时触发</summary>
        public event System.Action OnFrenzyBegin;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float gameTime;                     // 游戏经过时间
        private GamePhase currentPhase;             // 当前阶段
        private bool isSpawning;                    // 是否正在刷怪
        private bool isPaused;                      // 是否暂停（升级选择时）
        private bool isFrenzyMode;                  // 是否狂暴模式
        private bool bossSpawned;                   // Boss是否已生成
        
        // 生成区域缓存
        private Rect screenBounds;
        private bool boundsCalculated;
        
        // 各类型刷怪计时器
        private float slimeTimer;
        private float tankTimer;
        private float rusherTimer;
        private float drifterTimer;
        private float waveTimer;                    // 用于特殊波次（如Rusher成群）
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前游戏时间（秒）</summary>
        public float GameTime => gameTime;
        
        /// <summary>当前游戏时间（格式化 MM:SS）</summary>
        public string GameTimeFormatted => $"{Mathf.FloorToInt(gameTime / 60):D2}:{Mathf.FloorToInt(gameTime % 60):D2}";
        
        /// <summary>当前阶段</summary>
        public GamePhase CurrentPhase => currentPhase;
        
        /// <summary>是否暂停中</summary>
        public bool IsPaused => isPaused;
        
        /// <summary>是否狂暴模式</summary>
        public bool IsFrenzyMode => isFrenzyMode;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            if (gameCamera == null)
            {
                gameCamera = Camera.main;
            }
        }
        
        private void Start()
        {
            CalculateScreenBounds();
            
            // 测试模式：从指定时间开始
            if (startFromTime > 0f)
            {
                gameTime = startFromTime;
                Debug.Log($"[WaveManager] 测试模式：从 {GameTimeFormatted} 开始");
            }
            
            StartSpawning();
        }
        
        private void Update()
        {
            if (!isSpawning || isPaused) return;
            
            // 更新游戏时间
            gameTime += Time.deltaTime * timeScale;
            
            // 更新阶段
            UpdatePhase();
            
            // 根据当前阶段执行刷怪逻辑
            ExecuteSpawnLogic();
        }
        
        protected override void OnSingletonDestroy()
        {

        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 开始刷怪
        /// </summary>
        public void StartSpawning()
        {
            isSpawning = true;
            isPaused = false;
            currentPhase = DeterminePhase(gameTime);
            
            Debug.Log($"[WaveManager] 开始刷怪，当前阶段: {currentPhase}");
        }
        
        /// <summary>
        /// 停止刷怪
        /// </summary>
        public void StopSpawning()
        {
            isSpawning = false;
            Debug.Log("[WaveManager] 停止刷怪");
        }
        
        /// <summary>
        /// 暂停刷怪（升级选择时调用）
        /// </summary>
        public void PauseSpawning()
        {
            isPaused = true;
            Debug.Log("[WaveManager] 刷怪暂停");
        }
        
        /// <summary>
        /// 恢复刷怪
        /// </summary>
        public void ResumeSpawning()
        {
            isPaused = false;
            Debug.Log("[WaveManager] 刷怪恢复");
        }
        
        /// <summary>
        /// 生成Boss（预留接口）
        /// </summary>
        public void SpawnBoss()
        {
            if (bossSpawned)
            {
                Debug.LogWarning("[WaveManager] Boss已经生成过了！");
                return;
            }
            
            bossSpawned = true;
            
            // TODO: 实际的Boss生成逻辑
            // 暂时只触发事件
            OnBossSpawn?.Invoke();
            
            Debug.Log("[WaveManager] Boss降临！");
        }
        
        /// <summary>
        /// 重置管理器
        /// </summary>
        public void Reset()
        {
            gameTime = 0f;
            currentPhase = GamePhase.WarmUp1;
            isSpawning = false;
            isPaused = false;
            isFrenzyMode = false;
            bossSpawned = false;
            
            slimeTimer = 0f;
            tankTimer = 0f;
            rusherTimer = 0f;
            drifterTimer = 0f;
            waveTimer = 0f;
            
            Debug.Log("[WaveManager] 已重置");
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 阶段管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 根据时间确定当前阶段
        /// </summary>
        private GamePhase DeterminePhase(float time)
        {
            if (time < 30f)       return GamePhase.WarmUp1;      // 0:00 - 0:30
            if (time < 60f)       return GamePhase.WarmUp2;      // 0:30 - 1:00
            if (time < 90f)       return GamePhase.TheWall1;     // 1:00 - 1:30
            if (time < 120f)      return GamePhase.TheWall2;     // 1:30 - 2:00
            if (time < 150f)      return GamePhase.TheSwarm1;    // 2:00 - 2:30
            if (time < 180f)      return GamePhase.TheSwarm2;    // 2:30 - 3:00
            if (time < 240f)      return GamePhase.Chaos;        // 3:00 - 4:00
            if (time < 285f)      return GamePhase.Frenzy;       // 4:00 - 4:45
            if (time < 300f)      return GamePhase.Silence;      // 4:45 - 5:00
            return GamePhase.Boss;                               // 5:00+
        }
        
        /// <summary>
        /// 更新阶段状态
        /// </summary>
        private void UpdatePhase()
        {
            GamePhase newPhase = DeterminePhase(gameTime);
            
            if (newPhase != currentPhase)
            {
                GamePhase oldPhase = currentPhase;
                currentPhase = newPhase;
                
                OnPhaseTransition(oldPhase, newPhase);
                OnPhaseChanged?.Invoke(newPhase);
                
                Debug.Log($"[WaveManager] 阶段切换: {oldPhase} -> {newPhase} @ {GameTimeFormatted}");
            }
        }
        
        /// <summary>
        /// 阶段切换时的特殊处理
        /// </summary>
        private void OnPhaseTransition(GamePhase from, GamePhase to)
        {
            switch (to)
            {
                case GamePhase.Frenzy:
                    isFrenzyMode = true;
                    OnFrenzyBegin?.Invoke();
                    Debug.Log("[WaveManager] 狂暴模式开启！刷新加速，敌人提速！");
                    break;
                    
                case GamePhase.Silence:
                    isFrenzyMode = false;
                    OnSilenceBegin?.Invoke();
                    Debug.Log("[WaveManager] 静默期开始...Boss即将降临...");
                    break;
                    
                case GamePhase.Boss:
                    SpawnBoss();
                    break;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 刷怪逻辑
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 根据当前阶段执行刷怪
        /// </summary>
        private void ExecuteSpawnLogic()
        {
            // 更新所有计时器
            float deltaTime = Time.deltaTime * timeScale;
            slimeTimer += deltaTime;
            tankTimer += deltaTime;
            rusherTimer += deltaTime;
            drifterTimer += deltaTime;
            waveTimer += deltaTime;
            
            // 根据阶段执行不同逻辑
            switch (currentPhase)
            {
                case GamePhase.WarmUp1:
                    SpawnPhase_WarmUp1();
                    break;
                    
                case GamePhase.WarmUp2:
                    SpawnPhase_WarmUp2();
                    break;
                    
                case GamePhase.TheWall1:
                    SpawnPhase_TheWall1();
                    break;
                    
                case GamePhase.TheWall2:
                    SpawnPhase_TheWall2();
                    break;
                    
                case GamePhase.TheSwarm1:
                    SpawnPhase_TheSwarm1();
                    break;
                    
                case GamePhase.TheSwarm2:
                    SpawnPhase_TheSwarm2();
                    break;
                    
                case GamePhase.Chaos:
                    SpawnPhase_Chaos();
                    break;
                    
                case GamePhase.Frenzy:
                    SpawnPhase_Frenzy();
                    break;
                    
                case GamePhase.Silence:
                    // 静默期不刷怪
                    break;
                    
                case GamePhase.Boss:
                    // Boss阶段由Boss自己召唤小怪
                    break;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 各阶段刷怪实现
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// Phase 1: 0:00-0:30 适应期前半
        /// 只有Slime，每2秒刷2只，屏幕正上方
        /// </summary>
        private void SpawnPhase_WarmUp1()
        {
            float interval = 2f;
            
            if (slimeTimer >= interval)
            {
                slimeTimer = 0f;
                SpawnEnemies(EnemyType.Slime, 2, SpawnZone.TopCenter);
            }
        }
        
        /// <summary>
        /// Phase 2: 0:30-1:00 适应期后半
        /// Slime加倍，每1.5秒刷3只，左上/右上
        /// </summary>
        private void SpawnPhase_WarmUp2()
        {
            float interval = 1.5f;
            
            if (slimeTimer >= interval)
            {
                slimeTimer = 0f;
                SpawnEnemies(EnemyType.Slime, 3, SpawnZone.TopRandom);
            }
        }
        
        /// <summary>
        /// Phase 3: 1:00-1:30 Tank登场
        /// 每5秒：1个Tank + 3个Slime护航
        /// 持续刷Slime：每2秒刷2只
        /// </summary>
        private void SpawnPhase_TheWall1()
        {
            // 持续刷Slime
            if (slimeTimer >= 2f)
            {
                slimeTimer = 0f;
                SpawnEnemies(EnemyType.Slime, 2, SpawnZone.TopRandom);
            }
            
            // Tank + 护航
            if (tankTimer >= 5f)
            {
                tankTimer = 0f;
                SpawnEnemy(EnemyType.Tank, SpawnZone.TopCenter);
                SpawnEnemies(EnemyType.Slime, 3, SpawnZone.TopRandom);
            }
        }
        
        /// <summary>
        /// Phase 4: 1:30-2:00 Drifter展示
        /// 持续刷Slime，每10秒刷一波Drifter（5只）
        /// </summary>
        private void SpawnPhase_TheWall2()
        {
            // 持续刷Slime
            if (slimeTimer >= 1.5f)
            {
                slimeTimer = 0f;
                SpawnEnemies(EnemyType.Slime, 2, SpawnZone.TopRandom);
            }
            
            // Drifter波次
            if (drifterTimer >= 10f)
            {
                drifterTimer = 0f;
                SpawnEnemies(EnemyType.Drifter, 5, SpawnZone.AllEdges);
            }
        }
        
        /// <summary>
        /// Phase 5: 2:00-2:30 Rusher登场
        /// 5只一组快速冲下来，每4秒一波
        /// 背景持续刷Slime
        /// </summary>
        private void SpawnPhase_TheSwarm1()
        {
            // 背景Slime
            if (slimeTimer >= 2f)
            {
                slimeTimer = 0f;
                SpawnEnemies(EnemyType.Slime, 2, SpawnZone.TopRandom);
            }
            
            // Rusher成群
            if (rusherTimer >= 4f)
            {
                rusherTimer = 0f;
                SpawnEnemies(EnemyType.Rusher, 5, SpawnZone.TopCenter);
            }
        }
        
        /// <summary>
        /// Phase 6: 2:30-3:00 夹击战术
        /// Tank从中间，Rusher从左右两侧
        /// </summary>
        private void SpawnPhase_TheSwarm2()
        {
            // 背景Slime
            if (slimeTimer >= 2f)
            {
                slimeTimer = 0f;
                SpawnEnemies(EnemyType.Slime, 2, SpawnZone.TopRandom);
            }
            
            // Tank从中间
            if (tankTimer >= 6f)
            {
                tankTimer = 0f;
                SpawnEnemy(EnemyType.Tank, SpawnZone.TopCenter);
            }
            
            // Rusher从两侧
            if (rusherTimer >= 3f)
            {
                rusherTimer = 0f;
                SpawnEnemies(EnemyType.Rusher, 3, SpawnZone.Left);
                SpawnEnemies(EnemyType.Rusher, 3, SpawnZone.Right);
            }
        }
        
        /// <summary>
        /// Phase 7: 3:00-4:00 全家桶混乱
        /// 大量Drifter混在Slime和Tank中间
        /// </summary>
        private void SpawnPhase_Chaos()
        {
            // Slime
            if (slimeTimer >= 1.2f)
            {
                slimeTimer = 0f;
                SpawnEnemies(EnemyType.Slime, 3, SpawnZone.AllEdges);
            }
            
            // Tank
            if (tankTimer >= 5f)
            {
                tankTimer = 0f;
                SpawnEnemies(EnemyType.Tank, 2, SpawnZone.TopRandom);
            }
            
            // Drifter
            if (drifterTimer >= 3f)
            {
                drifterTimer = 0f;
                SpawnEnemies(EnemyType.Drifter, 4, SpawnZone.AllEdges);
            }
            
            // Rusher
            if (rusherTimer >= 6f)
            {
                rusherTimer = 0f;
                SpawnEnemies(EnemyType.Rusher, 4, SpawnZone.SideRandom);
            }
        }
        
        /// <summary>
        /// Phase 8: 4:00-4:45 狂暴潮
        /// 所有怪物刷新速度+50%，移动速度+20%
        /// </summary>
        private void SpawnPhase_Frenzy()
        {
            float rateMultiplier = frenzySpawnRateMultiplier; // 0.5 = 间隔减半
            
            // Slime (疯狂)
            if (slimeTimer >= 0.8f * rateMultiplier)
            {
                slimeTimer = 0f;
                SpawnEnemiesWithSpeedBoost(EnemyType.Slime, 4, SpawnZone.AllEdges);
            }
            
            // Tank
            if (tankTimer >= 3f * rateMultiplier)
            {
                tankTimer = 0f;
                SpawnEnemiesWithSpeedBoost(EnemyType.Tank, 2, SpawnZone.TopRandom);
            }
            
            // Drifter
            if (drifterTimer >= 2f * rateMultiplier)
            {
                drifterTimer = 0f;
                SpawnEnemiesWithSpeedBoost(EnemyType.Drifter, 5, SpawnZone.AllEdges);
            }
            
            // Rusher
            if (rusherTimer >= 2.5f * rateMultiplier)
            {
                rusherTimer = 0f;
                SpawnEnemiesWithSpeedBoost(EnemyType.Rusher, 6, SpawnZone.SideRandom);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生成方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 生成单个敌人
        /// </summary>
        private EnemyBlob SpawnEnemy(EnemyType type, SpawnZone zone)
        {
            if (EnemyPoolManager.Instance == null) return null;
            
            // 检查该类型是否配置了池
            if (!EnemyPoolManager.Instance.HasPool(type))
            {
                Debug.LogWarning($"[WaveManager] {type} 类型未配置对象池，跳过生成");
                return null;
            }
            
            Vector3 position = GetSpawnPosition(zone);
            return EnemyPoolManager.Instance.Spawn(type, position);
        }
        
        /// <summary>
        /// 生成多个敌人
        /// </summary>
        private void SpawnEnemies(EnemyType type, int count, SpawnZone zone)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnEnemy(type, zone);
            }
        }
        
        /// <summary>
        /// 生成带速度加成的敌人（狂暴模式）
        /// </summary>
        private void SpawnEnemiesWithSpeedBoost(EnemyType type, int count, SpawnZone zone)
        {
            for (int i = 0; i < count; i++)
            {
                var enemy = SpawnEnemy(type, zone);
                if (enemy != null)
                {
                    enemy.SetSpeedMultiplier(frenzySpeedMultiplier);
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生成位置计算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 计算屏幕边界
        /// </summary>
        private void CalculateScreenBounds()
        {
            if (gameCamera == null)
            {
                Debug.LogError("[WaveManager] 未找到相机！");
                return;
            }
            
            float camHeight = gameCamera.orthographicSize * 2f;
            float camWidth = camHeight * gameCamera.aspect;
            Vector3 camPos = gameCamera.transform.position;
            
            screenBounds = new Rect(
                camPos.x - camWidth / 2f,
                camPos.y - camHeight / 2f,
                camWidth,
                camHeight
            );
            
            boundsCalculated = true;
            
            Debug.Log($"[WaveManager] 屏幕边界计算完成: {screenBounds}");
        }
        
        /// <summary>
        /// 获取生成位置
        /// </summary>
        private Vector3 GetSpawnPosition(SpawnZone zone)
        {
            if (!boundsCalculated)
            {
                CalculateScreenBounds();
            }
            
            float x, y;
            float spread = Random.Range(-spawnSpread, spawnSpread);
            
            switch (zone)
            {
                case SpawnZone.TopCenter:
                    x = screenBounds.center.x + spread;
                    y = screenBounds.yMax + outsideOffset;
                    break;
                    
                case SpawnZone.TopLeft:
                    x = screenBounds.xMin + screenBounds.width * 0.25f + spread;
                    y = screenBounds.yMax + outsideOffset;
                    break;
                    
                case SpawnZone.TopRight:
                    x = screenBounds.xMax - screenBounds.width * 0.25f + spread;
                    y = screenBounds.yMax + outsideOffset;
                    break;
                    
                case SpawnZone.Left:
                    x = screenBounds.xMin - outsideOffset;
                    y = Random.Range(screenBounds.yMin + screenBounds.height * 0.3f, screenBounds.yMax);
                    break;
                    
                case SpawnZone.Right:
                    x = screenBounds.xMax + outsideOffset;
                    y = Random.Range(screenBounds.yMin + screenBounds.height * 0.3f, screenBounds.yMax);
                    break;
                    
                case SpawnZone.TopRandom:
                    int topChoice = Random.Range(0, 3);
                    return GetSpawnPosition(topChoice == 0 ? SpawnZone.TopLeft : 
                                           topChoice == 1 ? SpawnZone.TopCenter : SpawnZone.TopRight);
                    
                case SpawnZone.SideRandom:
                    return GetSpawnPosition(Random.value > 0.5f ? SpawnZone.Left : SpawnZone.Right);
                    
                case SpawnZone.AllEdges:
                default:
                    int edgeChoice = Random.Range(0, 3);
                    if (edgeChoice == 0) return GetSpawnPosition(SpawnZone.TopRandom);
                    if (edgeChoice == 1) return GetSpawnPosition(SpawnZone.Left);
                    return GetSpawnPosition(SpawnZone.Right);
            }
            
            return new Vector3(x, y, 0f);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试UI
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!showDebugInfo || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 280, 10, 270, 200));
            
            GUI.color = isFrenzyMode ? Color.red : Color.white;
            GUILayout.Label($"=== Wave Manager ===");
            GUI.color = Color.white;
            
            GUILayout.Label($"时间: {GameTimeFormatted}");
            GUILayout.Label($"阶段: {currentPhase}");
            GUILayout.Label($"状态: {(isPaused ? "暂停" : isSpawning ? "运行中" : "停止")}");
            
            if (isFrenzyMode)
            {
                GUI.color = Color.red;
                GUILayout.Label("★ 狂暴模式 ★");
                GUI.color = Color.white;
            }
            
            if (EnemyPoolManager.Instance != null)
            {
                GUILayout.Label($"敌人总数: {EnemyPoolManager.Instance.TotalActiveEnemies}");
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button(isPaused ? "恢复刷怪" : "暂停刷怪"))
            {
                if (isPaused) ResumeSpawning();
                else PauseSpawning();
            }
            
            GUILayout.EndArea();
        }
#endif
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器可视化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDrawGizmos()
        {
            if (!showSpawnGizmos) return;
            
            if (Application.isPlaying && boundsCalculated)
            {
                // 绘制屏幕边界
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                Vector3 center = new Vector3(screenBounds.center.x, screenBounds.center.y, 0f);
                Vector3 size = new Vector3(screenBounds.width, screenBounds.height, 0.1f);
                Gizmos.DrawWireCube(center, size);
                
                // 绘制生成点
                Gizmos.color = Color.green;
                
                // TopCenter
                Gizmos.DrawWireSphere(new Vector3(screenBounds.center.x, screenBounds.yMax + outsideOffset, 0f), 0.5f);
                
                // TopLeft
                Gizmos.DrawWireSphere(new Vector3(screenBounds.xMin + screenBounds.width * 0.25f, screenBounds.yMax + outsideOffset, 0f), 0.3f);
                
                // TopRight
                Gizmos.DrawWireSphere(new Vector3(screenBounds.xMax - screenBounds.width * 0.25f, screenBounds.yMax + outsideOffset, 0f), 0.3f);
                
                // Left
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(new Vector3(screenBounds.xMin - outsideOffset, screenBounds.center.y, 0f), 0.3f);
                
                // Right
                Gizmos.DrawWireSphere(new Vector3(screenBounds.xMax + outsideOffset, screenBounds.center.y, 0f), 0.3f);
            }
        }
    }
}