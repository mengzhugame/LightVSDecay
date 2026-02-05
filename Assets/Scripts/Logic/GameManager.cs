// ============================================================
// GameManager.cs (章节系统版)
// 文件位置: Assets/Scripts/Logic/GameManager.cs
// 用途：游戏状态管理 - 支持章节选择和难度配置
// 更新：集成章节系统，从 GameSessionConfig 读取配置
// ============================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using LightVsDecay.Audio;
using LightVsDecay.Core;
using LightVsDecay.Data;
using LightVsDecay.Data.Runtime;
using LightVsDecay.Data.SO;

namespace LightVsDecay.Logic
{
    /// <summary>
    /// 游戏管理器
    /// 负责游戏状态管理、场景切换、章节配置应用
    /// </summary>
    public class GameManager : PersistentSingleton<GameManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 基础配置 ═══")]
        [Tooltip("游戏设置")]
        [SerializeField] private GameSettings settings;
        
        [Tooltip("默认波次配置（当章节未指定时使用）")]
        [SerializeField] private WaveConfig waveConfig;
        
        [Header("═══ 章节配置 ═══")]
        [Tooltip("章节数据库")]
        [SerializeField] private ChapterDatabase chapterDatabase;
        
        [Header("═══ 场景设置 ═══")]
        [SerializeField] private string mainMenuSceneName = "MainScene";
        [SerializeField] private string gameSceneName = "GameScene";

        [Header("═══ 调试 ═══")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时配置缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private SpriteRenderer battleBackground;
        private float gameDuration = 300f;
        private float bossBattleTimeLimit = 60f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 当前战斗的章节配置（新增）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private ChapterConfig currentChapterConfig;
        private DifficultySettings currentDifficultySettings;
        private int currentChapterIndex = 0;
        private int currentDifficulty = 1;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private GameState currentState = GameState.Menu;
        private float gameTimer = 0f;
        private bool isTimerRunning = false;
        private bool isBossFight = false;
        private float bossTimer = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性 - 基础
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public GameState CurrentState => currentState;
        public float GameTimer => gameTimer;
        public float GameDuration => gameDuration;
        public float GameProgress => gameDuration > 0 ? gameTimer / gameDuration : 0f;
        public bool IsPlaying => currentState == GameState.Playing;
        public bool IsPaused => currentState == GameState.Paused;
        public bool IsBossFight => isBossFight;
        
        public GameSettings Settings => settings;
        public WaveConfig WaveConfig => currentChapterConfig?.waveConfig ?? waveConfig;
        
        /// <summary>格式化的游戏时间 (MM:SS)</summary>
        public string GameTimeFormatted => $"{Mathf.FloorToInt(gameTimer / 60):D1}:{Mathf.FloorToInt(gameTimer % 60):D2}";
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性 - 章节系统（新增）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前章节配置</summary>
        public ChapterConfig CurrentChapterConfig => currentChapterConfig;
        
        /// <summary>当前难度配置</summary>
        public DifficultySettings CurrentDifficultySettings => currentDifficultySettings;
        
        /// <summary>当前章节索引 (0-based)</summary>
        public int CurrentChapterIndex => currentChapterIndex;
        
        /// <summary>当前难度等级 (1-5)</summary>
        public int CurrentDifficulty => currentDifficulty;
        
        /// <summary>章节数据库</summary>
        public ChapterDatabase ChapterDatabase => chapterDatabase;
        
        /// <summary>当前章节显示名称</summary>
        public string CurrentChapterName => currentChapterConfig?.DisplayTitle ?? $"章节 {currentChapterIndex + 1}";
        
        /// <summary>当前难度显示名称</summary>
        public string CurrentDifficultyName => currentDifficultySettings?.displayName ?? $"难度 {currentDifficulty}";
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            // ★★★ 调试 ★★★
            Debug.Log($"[GameManager] OnSingletonAwake - InstanceID: {this.GetInstanceID()}");
            Debug.Log($"[GameManager] OnSingletonAwake - chapterDatabase: {(chapterDatabase != null ? chapterDatabase.name : "NULL")}");
            // ★★★ 调试结束 ★★★
            LoadConfig();
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameEvents.OnBossDeath += OnBossDefeated;
        }
        
        protected override void OnSingletonDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameEvents.OnBossDeath -= OnBossDefeated;
        }
        
        private void Update()
        {
            if (currentState == GameState.Playing && isTimerRunning)
            {
                UpdateGameTimer();
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置加载
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void LoadConfig()
        {
            if (settings != null)
            {
                // 读取其他游戏设置...
            }
            
            // 初始化默认难度配置（难度1）
            currentDifficultySettings = DifficultySettings.CreateDefault(1);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 场景管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[GameManager] 场景加载: {scene.name}");
            }
            
            if (scene.name == gameSceneName)
            {
                // 进入游戏场景
                StartCoroutine(DelayedStartGame());
            }
            else if (scene.name == mainMenuSceneName)
            {
                // 回到主菜单
                ChangeState(GameState.Menu);
                
                // 重置会话配置
                GameSessionConfig.Reset();
            }
        }
        
        /// <summary>
        /// 延迟启动游戏，确保所有Manager都完成初始化
        /// </summary>
        private System.Collections.IEnumerator DelayedStartGame()
        {
            // 等待一帧，让所有 Start() 执行完毕
            yield return null;
            
            // 初始化章节配置
            InitializeChapterConfig();
            
            // 应用章节配置（背景、BGM等）
            ApplyChapterConfig();
            
            // 开始游戏
            StartGame();
        }
        
        /// <summary>
        /// 加载主菜单场景
        /// </summary>
        public void LoadMainMenu()
        {
            // 清除事件订阅，防止内存泄漏
            GameEvents.ClearAllEvents();
            
            // 恢复时间缩放
            Time.timeScale = 1f;
            
            ChangeState(GameState.Menu);
            SceneManager.LoadScene(mainMenuSceneName);
        }
        
        /// <summary>
        /// 加载游戏场景
        /// </summary>
        public void LoadGameScene()
        {
            // 清除事件订阅
            GameEvents.ClearAllEvents();
            
            SceneManager.LoadScene(gameSceneName);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 章节配置初始化（新增）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 初始化章节配置（从 GameSessionConfig 读取）
        /// </summary>
        private void InitializeChapterConfig()
        {
            // ★★★ 调试：检查时序和引用 ★★★
            Debug.Log($"[GameManager] ========== InitializeChapterConfig 开始 ==========");
            Debug.Log($"[GameManager] this.GetInstanceID(): {this.GetInstanceID()}");
            Debug.Log($"[GameManager] chapterDatabase 是否为 null: {chapterDatabase == null}");
            Debug.Log($"[GameManager] chapterDatabase: {(chapterDatabase != null ? chapterDatabase.name : "NULL")}");
            Debug.Log($"[GameManager] GameSessionConfig.IsConfigured: {GameSessionConfig.IsConfigured}");
            Debug.Log($"[GameManager] GameSessionConfig.SelectedChapterIndex: {GameSessionConfig.SelectedChapterIndex}");
            Debug.Log($"[GameManager] GameSessionConfig.SelectedDifficulty: {GameSessionConfig.SelectedDifficulty}");
            if (chapterDatabase != null)
            {
                Debug.Log($"[GameManager] chapterDatabase.ChapterCount: {chapterDatabase.ChapterCount}");
                var chapter = chapterDatabase.GetChapter(0);
                Debug.Log($"[GameManager] chapterDatabase.GetChapter(0): {(chapter != null ? chapter.chapterName : "NULL")}");
                if (chapter != null)
                {
                    Debug.Log($"[GameManager] chapter.waveConfig: {(chapter.waveConfig != null ? chapter.waveConfig.name : "NULL")}");
                }
            }
            Debug.Log($"[GameManager] ================================================");
            // 如果没有配置，使用默认值（直接启动 GameScene 调试时）
            if (!GameSessionConfig.IsConfigured)
            {
                GameSessionConfig.UseDefaultIfNotConfigured();
                
                if (showDebugInfo)
                {
                    Debug.Log("[GameManager] 使用默认章节配置（调试模式）");
                }
            }
            
            // 读取选择的章节和难度
            currentChapterIndex = GameSessionConfig.SelectedChapterIndex;
            currentDifficulty = GameSessionConfig.SelectedDifficulty;
            
            // 获取章节配置
            if (chapterDatabase != null)
            {
                currentChapterConfig = chapterDatabase.GetChapter(currentChapterIndex);
            }
            
            // 获取难度配置
            if (currentChapterConfig != null && currentChapterConfig.difficulties != null && currentChapterConfig.difficulties.Length > 0)
            {
                int diffIndex = Mathf.Clamp(currentDifficulty - 1, 0, currentChapterConfig.difficulties.Length - 1);
                currentDifficultySettings = currentChapterConfig.difficulties[diffIndex];
            }
            else
            {
                // 使用默认难度配置（根据当前难度等级创建）
                currentDifficultySettings = DifficultySettings.CreateDefault(currentDifficulty);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[GameManager] 章节配置初始化完成:");
                Debug.Log($"  - 章节: {currentChapterIndex + 1} ({currentChapterConfig?.chapterName ?? "默认"})");
                Debug.Log($"  - 难度: {currentDifficulty} ({currentDifficultySettings?.displayName ?? "默认"})");
                Debug.Log($"  - 敌人血量倍率: x{currentDifficultySettings?.enemyHealthMultiplier ?? 1f}");
                Debug.Log($"  - 敌人数量倍率: x{currentDifficultySettings?.enemyCountMultiplier ?? 1f}");
            }
        }
        
        /// <summary>
        /// 应用章节配置（背景、BGM等）
        /// </summary>
        private void ApplyChapterConfig()
        {
            if (currentChapterConfig == null)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[GameManager] 无章节配置，使用默认设置");
                }
                return;
            }
            
            // 应用战斗背景
            ApplyBattleBackground();
            
            // 应用战斗 BGM
            ApplyBattleBGM();
            
            if (showDebugInfo)
            {
                Debug.Log($"[GameManager] 章节配置已应用: {currentChapterConfig.chapterName}");
            }
        }
        
        /// <summary>
        /// 应用战斗背景
        /// </summary>
        private void ApplyBattleBackground()
        {
            // 如果没有在 Inspector 中设置，尝试查找
            if (battleBackground == null)
            {
                // 修改：先尝试 Find，不用 FindWithTag（避免 Tag 未定义报错）
                GameObject bgObj = GameObject.Find("BattleBackground");
        
                if (bgObj != null)
                {
                    battleBackground = bgObj.GetComponent<SpriteRenderer>();
                }
            }
    
            // 应用背景图
            if (battleBackground != null && currentChapterConfig?.battleBackgroundImage != null)
            {
                battleBackground.sprite = currentChapterConfig.battleBackgroundImage;
        
                if (showDebugInfo)
                {
                    Debug.Log($"[GameManager] 战斗背景已设置: {currentChapterConfig.battleBackgroundImage.name}");
                }
            }
        }
        
        /// <summary>
        /// 应用战斗 BGM
        /// </summary>
        private void ApplyBattleBGM()
        {
            if (AudioManager.Instance == null) return;
            
            // 如果章节配置了专属 BGM
            if (currentChapterConfig.battleBGM != null)
            {
                AudioManager.Instance.PlayBGM(currentChapterConfig.battleBGM);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[GameManager] 战斗BGM已设置: {currentChapterConfig.battleBGM.name}");
                }
            }
            // 否则使用 AudioManager 的默认战斗 BGM（在 OnSceneLoaded 中已处理）
        }
        
        /// <summary>
        /// 设置战斗背景引用（供场景中的脚本调用）
        /// </summary>
        public void SetBattleBackground(SpriteRenderer bg)
        {
            battleBackground = bg;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 游戏流程
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 开始游戏
        /// </summary>
        public void StartGame()
        {
            gameTimer = 0f;
            isTimerRunning = true;
            isBossFight = false;
            bossTimer = 0f;
            Time.timeScale = 1f;
            
            ChangeState(GameState.Playing);
            GameEvents.TriggerGameStart();
            
            if (showDebugInfo)
            {
                Debug.Log($"[GameManager] 游戏开始 - 章节{currentChapterIndex + 1} 难度{currentDifficulty}");
            }
        }
        
        /// <summary>
        /// 暂停游戏
        /// </summary>
        public void PauseGame()
        {
            if (currentState != GameState.Playing) return;
            
            Time.timeScale = 0f;
            ChangeState(GameState.Paused);
            GameEvents.TriggerGamePaused();
            
            if (showDebugInfo)
            {
                Debug.Log("[GameManager] 游戏暂停");
            }
        }
        
        /// <summary>
        /// 恢复游戏
        /// </summary>
        public void ResumeGame()
        {
            if (currentState != GameState.Paused) return;
            
            Time.timeScale = 1f;
            ChangeState(GameState.Playing);
            GameEvents.TriggerGameResumed();
            
            if (showDebugInfo)
            {
                Debug.Log("[GameManager] 游戏恢复");
            }
        }
        
        /// <summary>
        /// 游戏胜利
        /// </summary>
        public void Victory()
        {
            if (currentState != GameState.Playing) return;
            
            isTimerRunning = false;
            Time.timeScale = 0f;
            ChangeState(GameState.Victory);
            GameEvents.TriggerGameVictory();
            
            // 更新章节进度
            UpdateChapterProgress();
            
            if (showDebugInfo)
            {
                Debug.Log("[GameManager] 游戏胜利！");
            }
        }
        
        /// <summary>
        /// 更新章节进度（胜利时调用）
        /// </summary>
        private void UpdateChapterProgress()
        {
            if (ProgressManager.Instance != null)
            {
                bool unlockedNew = ProgressManager.Instance.CompleteChapterDifficulty(
                    currentChapterIndex, 
                    currentDifficulty
                );
                
                if (unlockedNew && showDebugInfo)
                {
                    Debug.Log($"[GameManager] 解锁新章节: {currentChapterIndex + 2}");
                }
            }
        }
        
        /// <summary>
        /// 游戏失败
        /// </summary>
        public void Defeat()
        {
            if (currentState != GameState.Playing) return;
            
            isTimerRunning = false;
            Time.timeScale = 0f;
            ChangeState(GameState.Defeat);
            GameEvents.TriggerGameDefeat();
            
            if (showDebugInfo)
            {
                Debug.Log("[GameManager] 游戏失败！");
            }
        }
        
        /// <summary>
        /// 触发胜利（别名方法）
        /// </summary>
        public void TriggerVictory() => Victory();
        
        /// <summary>
        /// 触发失败（别名方法）
        /// </summary>
        public void TriggerDefeat() => Defeat();
        
        /// <summary>
        /// 重新开始游戏
        /// </summary>
        public void RestartGame()
        {
            // 重新开始时保持原有的章节/难度配置
            LoadGameScene();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 计时器
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateGameTimer()
        {
            gameTimer += Time.deltaTime;
            
            if (isBossFight)
            {
                bossTimer += Time.deltaTime;
            }
        }
        
        /// <summary>
        /// 进入BOSS战（供WaveManager调用）
        /// </summary>
        public void EnterBossFight()
        {
            isBossFight = true;
            bossTimer = 0f;
            
            // 播放 Boss BGM（如果章节配置了）
            if (currentChapterConfig?.bossBGM != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM(currentChapterConfig.bossBGM);
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[GameManager] 进入BOSS战！");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ChangeState(GameState newState)
        {
            if (currentState == newState) return;
            
            currentState = newState;
            GameEvents.TriggerGameStateChanged(newState);
            
            if (showDebugInfo)
            {
                Debug.Log($"[GameManager] 状态变化: {newState}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BOSS击杀回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void OnBossDefeated()
        {
            if (isBossFight)
            {
                Victory();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 220, 10, 210, 200));
            GUILayout.Label($"=== GameManager ===");
            GUILayout.Label($"State: {currentState}");
            GUILayout.Label($"Time: {GameTimeFormatted}");
            GUILayout.Label($"Chapter: {currentChapterIndex + 1} ({currentChapterConfig?.chapterName ?? "N/A"})");
            GUILayout.Label($"Difficulty: {currentDifficulty}");
            GUILayout.Label($"HP Mult: x{currentDifficultySettings?.enemyHealthMultiplier ?? 1f:F2}");
            GUILayout.Label($"Boss Fight: {isBossFight}");
            
            if (currentState == GameState.Playing)
            {
                if (GUILayout.Button("Force Victory"))
                    Victory();
                if (GUILayout.Button("Force Defeat"))
                    Defeat();
            }
            
            GUILayout.EndArea();
        }
#endif
    }
}