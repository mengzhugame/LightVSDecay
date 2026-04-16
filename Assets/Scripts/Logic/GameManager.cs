// ============================================================
// GameManager.cs (章节系统版)
// 文件位置: Assets/Scripts/Logic/GameManager.cs
// 用途：游戏状态管理 - 支持章节选择和难度配置
// 更新：集成章节系统，从 GameSessionConfig 读取配置
// ============================================================

using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using LightVsDecay.Audio;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;
using LightVsDecay.Data.Runtime;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Boss;
using LightVsDecay.Logic.Enemy;

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

        [Header("═══ Shader 预热 ═══")]
        [Tooltip("拖入 Assets/Shaders/URPShaderVariants.shadervariants，游戏启动时一次性预热所有 Shader 变体，消除首次渲染卡顿")]
        [SerializeField] private ShaderVariantCollection shaderVariantCollection;

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
        public WaveConfig WaveConfig =>
            currentDifficultySettings?.overrideWaveConfig != null
                ? currentDifficultySettings.overrideWaveConfig
                : currentChapterConfig?.waveConfig ?? waveConfig;
        
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
            // ── 帧率设置：Android 默认 30fps，必须显式设置为 60 ──
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount  = 0; // 部分设备的 vsync 会覆盖 targetFrameRate

            // ── Shader 变体预热：在启动阶段一次性编译所有已录制的 Shader 变体，
            //    避免首次渲染某种材质时 GPU 实时编译造成的卡顿（毛刺帧）
            if (shaderVariantCollection != null)
            {
                shaderVariantCollection.WarmUp();
                GameLogger.Log("[GameManager] Shader 变体预热完成");
            }

            // ★★★ 调试 ★★★
            GameLogger.Log($"[GameManager] OnSingletonAwake - InstanceID: {this.GetInstanceID()}");
            GameLogger.Log($"[GameManager] OnSingletonAwake - chapterDatabase: {(chapterDatabase != null ? chapterDatabase.name : "NULL")}");
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
                GameLogger.Log($"[GameManager] 场景加载: {scene.name}");
            }
            
            if (scene.name == gameSceneName)
            {
                // ClearAllEvents() 在 LoadGameScene/LoadMainMenu 中会将 OnBossDeath 置 null，
                // PersistentSingleton 的 OnSingletonAwake 只执行一次，无法自动恢复订阅，
                // 因此每次进入游戏场景时必须在此重新订阅，防止死亡演出序列永远不触发。
                GameEvents.OnBossDeath -= OnBossDefeated; // 防止重复订阅
                GameEvents.OnBossDeath += OnBossDefeated;

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
            // 注意：此方法内部调用 EnemyPoolManager.InitializeForChapter，
            //       但该调用不再做同步预热（prewarmCount=0），防止单帧大量 Instantiate 卡顿
            ApplyChapterConfig();

            // 将对象池预热分散到多帧执行，避免首帧 50~80 次 Instantiate 导致 15fps 卡顿
            if (EnemyPoolManager.Instance != null)
                yield return StartCoroutine(EnemyPoolManager.Instance.WarmupCoroutine());

            // 开始游戏
            StartGame();
        }
        
        /// <summary>
        /// 加载主菜单场景（安全异步，适配微信小游戏WebGL）
        /// </summary>
        public void LoadMainMenu()
        {
            // 清除事件订阅，防止内存泄漏
            GameEvents.ClearAllEvents();

            // 恢复时间缩放
            Time.timeScale = 1f;

            ChangeState(GameState.Menu);
            StartCoroutine(SafeSceneLoader.LoadSceneSafe(mainMenuSceneName));
        }

        /// <summary>
        /// 加载游戏场景（安全异步，适配微信小游戏WebGL）
        /// </summary>
        public void LoadGameScene()
        {
            // 清除事件订阅
            GameEvents.ClearAllEvents();

            StartCoroutine(SafeSceneLoader.LoadSceneSafe(gameSceneName));
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
            GameLogger.Log($"[GameManager] ========== InitializeChapterConfig 开始 ==========");
            GameLogger.Log($"[GameManager] this.GetInstanceID(): {this.GetInstanceID()}");
            GameLogger.Log($"[GameManager] chapterDatabase 是否为 null: {chapterDatabase == null}");
            GameLogger.Log($"[GameManager] chapterDatabase: {(chapterDatabase != null ? chapterDatabase.name : "NULL")}");
            GameLogger.Log($"[GameManager] GameSessionConfig.IsConfigured: {GameSessionConfig.IsConfigured}");
            GameLogger.Log($"[GameManager] GameSessionConfig.SelectedChapterIndex: {GameSessionConfig.SelectedChapterIndex}");
            GameLogger.Log($"[GameManager] GameSessionConfig.SelectedDifficulty: {GameSessionConfig.SelectedDifficulty}");
            if (chapterDatabase != null)
            {
                GameLogger.Log($"[GameManager] chapterDatabase.ChapterCount: {chapterDatabase.ChapterCount}");
                var chapter = chapterDatabase.GetChapter(0);
                GameLogger.Log($"[GameManager] chapterDatabase.GetChapter(0): {(chapter != null ? chapter.chapterName : "NULL")}");
                if (chapter != null)
                {
                    GameLogger.Log($"[GameManager] chapter.waveConfig: {(chapter.waveConfig != null ? chapter.waveConfig.name : "NULL")}");
                }
            }
            GameLogger.Log($"[GameManager] ================================================");
            // 如果没有配置，使用默认值（直接启动 GameScene 调试时）
            if (!GameSessionConfig.IsConfigured)
            {
                GameSessionConfig.UseDefaultIfNotConfigured();
                
                if (showDebugInfo)
                {
                    GameLogger.Log("[GameManager] 使用默认章节配置（调试模式）");
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
                GameLogger.Log($"[GameManager] 章节配置初始化完成:");
                GameLogger.Log($"  - 章节: {currentChapterIndex + 1} ({currentChapterConfig?.chapterName ?? "默认"})");
                GameLogger.Log($"  - 难度: {currentDifficulty} ({currentDifficultySettings?.displayName ?? "默认"})");
                GameLogger.Log($"  - 敌人血量倍率: x{currentDifficultySettings?.enemyHealthMultiplier ?? 1f}");
                GameLogger.Log($"  - 敌人数量倍率: x{currentDifficultySettings?.enemyCountMultiplier ?? 1f}");
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
                    GameLogger.Log("[GameManager] 无章节配置，使用默认设置");
                }
                return;
            }

            // 按章节初始化敌人对象池（只加载当前章节的怪物预制体）
            if (EnemyPoolManager.Instance != null)
            {
                EnemyPoolManager.Instance.InitializeForChapter(currentChapterIndex);
            }

            // 应用战斗背景
            ApplyBattleBackground();

            // 更新 HUD 关卡名称
            ApplyStageName();

            // 应用战斗 BGM
            ApplyBattleBGM();
            // 【新增】应用流体怪物底色
            ApplyEnemyBlobColor();
            if (showDebugInfo)
            {
                GameLogger.Log($"[GameManager] 章节配置已应用: {currentChapterConfig.chapterName}");
            }
        }
        
        /// <summary>
        /// 应用战斗背景
        /// </summary>
        private void ApplyBattleBackground()
        {
            GameLogger.Log($"[BG_DIAG] ApplyBattleBackground 开始 | chapterIndex={currentChapterIndex} | battleBackground={(battleBackground == null ? "NULL" : battleBackground.gameObject.name)}");

            // 如果没有在 Inspector 中设置，尝试查找
            if (battleBackground == null)
            {
                // 场景中背景节点名为 "Background"
                GameObject bgObj = GameObject.Find("BattleLevelBackground");
                GameLogger.Log($"[BG_DIAG] GameObject.Find(\"Background\") = {(bgObj == null ? "NULL" : bgObj.name)}");

                if (bgObj != null)
                {
                    battleBackground = bgObj.GetComponent<SpriteRenderer>();
                    GameLogger.Log($"[BG_DIAG] 通过Find获取SpriteRenderer = {(battleBackground == null ? "NULL" : "OK")}");
                }
            }

            // 应用背景图
            if (battleBackground == null)
            {
                GameLogger.LogWarning("[GameManager] 战斗背景 SpriteRenderer 未找到！请确认场景中有挂载 BattleBackgroundRegister 的物体。");
                return;
            }
            if (currentChapterConfig?.battleBackgroundImage == null)
            {
                GameLogger.LogWarning($"[GameManager] 章节 {currentChapterIndex + 1} 的 battleBackgroundImage 未配置！请在 Chapter0{currentChapterIndex + 1} 的 ChapterConfig 中设置背景图。");
                return;
            }
            GameLogger.Log($"[BG_DIAG] 即将设置背景: sprite={currentChapterConfig.battleBackgroundImage.name}, 当前sprite={(battleBackground.sprite == null ? "NULL" : battleBackground.sprite.name)}");
            battleBackground.sprite = currentChapterConfig.battleBackgroundImage;
            GameLogger.Log($"[BG_DIAG] ✅ 背景设置完成: {battleBackground.sprite?.name}, 物体激活={battleBackground.gameObject.activeInHierarchy}");
        }
        
        /// <summary>
        /// 更新 HUD 关卡名称
        /// </summary>
        private void ApplyStageName()
        {
            var hud = FindObjectOfType<LightVsDecay.UI.Panels.HUDPanel>();
            if (hud == null) return;
            int chNum = currentChapterConfig.chapterNumber;
            string stageName = $"LEVEL {chNum}-{currentDifficulty}";
            hud.SetStageName(stageName);
            GameLogger.Log($"[GameManager] 关卡名称已更新: {stageName}");
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
                    GameLogger.Log($"[GameManager] 战斗BGM已设置: {currentChapterConfig.battleBGM.name}");
                }
            }
            // 否则使用 AudioManager 的默认战斗 BGM（在 OnSceneLoaded 中已处理）
        }
        /// <summary>
        /// 应用流体怪物底色（章节特色颜色）
        /// </summary>
        private void ApplyEnemyBlobColor()
        {
            // 查找场景中的 MetaballsManager
            var metaballsManager = FindObjectOfType<MetaballsManager>();
            
            if (metaballsManager != null)
            {
                metaballsManager.SetBlobColor(currentChapterConfig.enemyBlobColor);
                
                if (showDebugInfo)
                {
                    GameLogger.Log($"[GameManager] 流体底色已设置: {currentChapterConfig.enemyBlobColor}");
                }
            }
            else
            {
                GameLogger.LogWarning("[GameManager] 未找到 MetaballsManager，无法设置流体颜色！");
            }
        }
        /// <summary>
        /// 设置战斗背景引用（供场景中的脚本调用）
        /// </summary>
        public void SetBattleBackground(SpriteRenderer bg)
        {
            battleBackground = bg;
            // 若章节配置已就绪（InitializeChapterConfig 先于 BattleBackgroundRegister.Start 执行时），立即应用
            if (currentChapterConfig != null)
            {
                ApplyBattleBackground();
            }
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
                GameLogger.Log($"[GameManager] 游戏开始 - 章节{currentChapterIndex + 1} 难度{currentDifficulty}");
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
                GameLogger.Log("[GameManager] 游戏暂停");
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
                GameLogger.Log("[GameManager] 游戏恢复");
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
                GameLogger.Log("[GameManager] 游戏胜利！");
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
                    GameLogger.Log($"[GameManager] 解锁新章节: {currentChapterIndex + 2}");
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
                GameLogger.Log("[GameManager] 游戏失败！");
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
                GameLogger.Log("[GameManager] 进入BOSS战！");
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
                GameLogger.Log($"[GameManager] 状态变化: {newState}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BOSS击杀回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void OnBossDefeated()
        {
            // isBossFight 标志依赖 EnterBossFight() 被调用，但实际未接入调用链；
            // 改为直接检查游戏状态，只要处于 Playing 就触发演出序列
            if (currentState == GameState.Playing)
            {
                isTimerRunning = false;
                StartCoroutine(BossDeathSequenceCoroutine());
            }
        }

        /// <summary>
        /// Boss 死亡演出协程：子弹时间 + 爆炸特效 + 相机震动，约 1.8s 后调用 Victory()
        /// 全程使用 WaitForSecondsRealtime / unscaledDeltaTime，不受 timeScale 影响
        /// </summary>
        private System.Collections.IEnumerator BossDeathSequenceCoroutine()
        {
            BaseBossController boss = BaseBossController.Current;
            Vector3 bossPos = boss != null ? boss.transform.position : Vector3.zero;

            // ── T+0.00s：立即停止 Boss AI（防止死亡演出期间继续攻击）────────
            boss?.PrepareForDeath();

            // ── T+0.00s：轻震 + Boss 抖动缩放（正常速度感受冲击）─────────
            CameraShake.Instance?.Shake(0.4f, 0.4f);

#if DOTWEEN
            if (boss != null)
            {
                UnityEngine.Transform punchTarget = boss.BodyTransform != null
                    ? boss.BodyTransform
                    : boss.transform;
                punchTarget.DOPunchScale(Vector3.one * 0.35f, 0.4f, 8, 0.5f);
            }
#endif

            yield return new WaitForSecondsRealtime(0.40f);

            // ── T+0.40s：Boss Sprite 闪白 × 3次（0.3s）─────────────────
            if (boss != null)
                yield return StartCoroutine(FlashBossWhite(boss));

            // ── T+0.70s：Boss 隐藏 + 播放专属爆炸特效 + 子弹时间开始 ────
            if (boss != null)
                boss.gameObject.SetActive(false);

            GameObject vfxPrefab = boss != null ? boss.DeathVFXPrefab : null;
            if (vfxPrefab != null)
                Object.Instantiate(vfxPrefab, bossPos, Quaternion.identity);

            // 子弹时间
            Time.timeScale        = 0.1f;
            Time.fixedDeltaTime   = 0.02f * 0.1f;

            CameraShake.Instance?.Shake(1.5f, 0.4f);
            HapticFeedback.Instance?.TriggerRaw(600);
            AudioManager.Instance?.SetBGMPitch(0.5f);

            yield return new WaitForSecondsRealtime(0.50f);

            // ── T+1.20s：timeScale 线性恢复 + 色差脉冲 + BGM pitch 恢复 ─
            float lerpDuration = 0.20f;
            float elapsed = 0f;
            while (elapsed < lerpDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / lerpDuration);
                Time.timeScale      = Mathf.Lerp(0.1f, 1.0f, t);
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                yield return null;
            }
            Time.timeScale      = 1.0f;
            Time.fixedDeltaTime = 0.02f;

            AudioManager.Instance?.ResetBGMPitch();
            LightVsDecay.VFX.PostProcess.ScreenEffectController.Instance?.PlayChromaticPulse(0.3f);

            yield return new WaitForSecondsRealtime(0.20f);

            // ── T+1.40s：暗角收拢（视觉过渡到结算界面）─────────────────
            LightVsDecay.VFX.PostProcess.ScreenEffectController.Instance?.PlayVignetteClose(0.55f, 0.4f);

            yield return new WaitForSecondsRealtime(0.40f);

            // ── T+1.80s：调用正常胜利流程 ─────────────────────────────
            Victory();
        }

        /// <summary>
        /// Boss Sprite 闪白 × flashCount 次（每次 flashInterval 秒），使用真实时间
        /// </summary>
        private System.Collections.IEnumerator FlashBossWhite(BaseBossController boss,
            int flashCount = 3, float flashInterval = 0.10f)
        {
            SpriteRenderer[] renderers   = boss.BodyRenderers;
            Color[]           origColors = boss.OriginalColors;
            if (renderers == null || renderers.Length == 0) yield break;

            float onTime  = flashInterval * 0.4f;
            float offTime = flashInterval * 0.6f;

            for (int i = 0; i < flashCount; i++)
            {
                for (int j = 0; j < renderers.Length; j++)
                    if (renderers[j] != null) renderers[j].color = Color.white;

                yield return new WaitForSecondsRealtime(onTime);

                for (int j = 0; j < renderers.Length; j++)
                    if (renderers[j] != null && origColors != null && j < origColors.Length)
                        renderers[j].color = origColors[j];

                yield return new WaitForSecondsRealtime(offTime);
            }
        }
        
//         // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//         // 调试 === Meta
//         // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//         
// #if UNITY_EDITOR
//         private void OnGUI()
//         {
//             if (!showDebugInfo) return;
//             
//             GUILayout.BeginArea(new Rect(Screen.width - 220, 10, 210, 200));
//             GUILayout.Label($"=== GameManager ===");
//             GUILayout.Label($"State: {currentState}");
//             GUILayout.Label($"Time: {GameTimeFormatted}");
//             GUILayout.Label($"Chapter: {currentChapterIndex + 1} ({currentChapterConfig?.chapterName ?? "N/A"})");
//             GUILayout.Label($"Difficulty: {currentDifficulty}");
//             GUILayout.Label($"HP Mult: x{currentDifficultySettings?.enemyHealthMultiplier ?? 1f:F2}");
//             GUILayout.Label($"Boss Fight: {isBossFight}");
//             
//             if (currentState == GameState.Playing)
//             {
//                 if (GUILayout.Button("Force Victory"))
//                     Victory();
//                 if (GUILayout.Button("Force Defeat"))
//                     Defeat();
//             }
//             
//             GUILayout.EndArea();
//         }
// #endif
    }
}