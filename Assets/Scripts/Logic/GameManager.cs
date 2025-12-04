// ============================================================
// GameManager.cs (修复版)
// 文件位置: Assets/Scripts/Logic/GameManager.cs
// 用途：游戏状态管理 - 使用 Core.GameState，修复命名空间冲突
// ============================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using LightVsDecay.Core;
using LightVsDecay.Data;
using LightVsDecay.Data.SO;

namespace LightVsDecay.Logic
{
    // 注意：GameState 枚举定义在 LightVsDecay.Core.GameEvents.cs 中
    // 不要在这里重复定义！
    
    /// <summary>
    /// 游戏管理器
    /// 配置从 GameSettings/WaveConfig ScriptableObject 读取
    /// </summary>
    public class GameManager : PersistentSingleton<GameManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("游戏设置")]
        [SerializeField] private GameSettings settings;
        
        [Tooltip("波次配置")]
        [SerializeField] private WaveConfig waveConfig;
        
        [Header("场景设置")]
        [SerializeField] private string mainMenuSceneName = "MainScene";
        [SerializeField] private string gameSceneName = "GameScene";
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时配置缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float gameDuration = 300f;
        private float bossBattleTimeLimit = 60f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private GameState currentState = GameState.Menu;
        private float gameTimer = 0f;
        private bool isTimerRunning = false;
        private bool isBossFight = false;
        private float bossTimer = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public GameState CurrentState => currentState;
        public float GameTimer => gameTimer;
        public float GameDuration => gameDuration;
        public float GameProgress => gameDuration > 0 ? gameTimer / gameDuration : 0f;
        public bool IsPlaying => currentState == GameState.Playing;
        public bool IsPaused => currentState == GameState.Paused;
        public bool IsBossFight => isBossFight;
        
        public GameSettings Settings => settings;
        public WaveConfig WaveConfig => waveConfig;
        
        /// <summary>格式化的游戏时间 (MM:SS)</summary>
        public string GameTimeFormatted => $"{Mathf.FloorToInt(gameTimer / 60):D1}:{Mathf.FloorToInt(gameTimer % 60):D2}";
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            LoadConfig();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        protected override void OnSingletonDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        
        private void Update()
        {
            if (isTimerRunning && currentState == GameState.Playing)
            {
                UpdateGameTimer();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置加载
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void LoadConfig()
        {
            // 优先从 WaveConfig 读取游戏时长
            if (waveConfig != null)
            {
                gameDuration = waveConfig.gameDuration;
                bossBattleTimeLimit = waveConfig.bossBattleTimeLimit;
            }
            else if (settings != null)
            {
                gameDuration = settings.gameDuration;
                bossBattleTimeLimit = 60f;
            }
            // 否则使用默认值
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
                // 进入游戏场景，开始游戏
                StartCoroutine(DelayedStartGame());
            }
            else if (scene.name == mainMenuSceneName)
            {
                // 回到主菜单
                ChangeState(GameState.Menu);
            }
        }
        /// <summary>
        /// 延迟启动游戏，确保所有Manager都完成初始化
        /// </summary>
        private System.Collections.IEnumerator DelayedStartGame()
        {
            // 等待一帧，让所有 Start() 执行完毕
            yield return null;
    
            StartGame();
        }
        /// <summary>
        /// 加载主菜单场景（保持原有方法名兼容性）
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
                Debug.Log("[GameManager] 游戏开始");
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
        /// 游戏胜利（保持原有方法名兼容性）
        /// </summary>
        public void Victory()
        {
            if (currentState != GameState.Playing) return;
            
            isTimerRunning = false;
            Time.timeScale = 0f;
            ChangeState(GameState.Victory);
            GameEvents.TriggerGameVictory();
            
            if (showDebugInfo)
            {
                Debug.Log("[GameManager] 游戏胜利！");
            }
        }
        
        /// <summary>
        /// 游戏失败（保持原有方法名兼容性）
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
            LoadGameScene();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 计时器
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateGameTimer()
        {
            gameTimer += Time.deltaTime;
            
            // 广播时间更新
            GameEvents.TriggerGameTimeUpdated(gameTimer, gameDuration);
            
            // 检查是否进入BOSS阶段
            if (!isBossFight && gameTimer >= gameDuration)
            {
                // 暂时直接胜利，等BOSS系统完成后修改为进入BOSS战
                Victory();
            }
            
            // BOSS战计时
            if (isBossFight)
            {
                bossTimer += Time.deltaTime;
                
                // BOSS战超时 = 失败
                if (bossTimer >= bossBattleTimeLimit)
                {
                    Defeat();
                }
            }
        }
        
        /// <summary>
        /// 进入BOSS战（供WaveManager调用）
        /// </summary>
        public void EnterBossFight()
        {
            isBossFight = true;
            bossTimer = 0f;
            
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
        
        /// <summary>
        /// BOSS被击杀（由BossController调用）
        /// </summary>
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
            
            GUILayout.BeginArea(new Rect(Screen.width - 200, 10, 190, 150));
            GUILayout.Label($"=== GameManager ===");
            GUILayout.Label($"State: {currentState}");
            GUILayout.Label($"Time: {GameTimeFormatted}");
            GUILayout.Label($"Progress: {GameProgress * 100:F1}%");
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