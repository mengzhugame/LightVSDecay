using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace LightVsDecay.Core
{
    /// <summary>
    /// 游戏管理器 (单例)
    /// 负责：游戏状态管理、场景切换、游戏计时
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public static GameManager Instance { get; private set; }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("场景名称")]
        [SerializeField] private string mainMenuSceneName = "MainScene";
        [SerializeField] private string gameSceneName = "GameScene";
        
        [Header("游戏时间设置")]
        [Tooltip("游戏总时长（秒）")]
        [SerializeField] private float gameDuration = 300f; // 5分钟
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private GameState currentState = GameState.Menu;
        private float gameTimer = 0f;
        private bool isTimerRunning = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public GameState CurrentState => currentState;
        public float GameTimer => gameTimer;
        public float GameDuration => gameDuration;
        public bool IsPlaying => currentState == GameState.Playing;
        
        /// <summary>格式化的游戏时间 (MM:SS)</summary>
        public string GameTimeFormatted => $"{Mathf.FloorToInt(gameTimer / 60):D1}:{Mathf.FloorToInt(gameTimer % 60):D2}";
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 单例设置 (跨场景保持)
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 监听场景加载
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        private void Update()
        {
            if (isTimerRunning && currentState == GameState.Playing)
            {
                UpdateGameTimer();
            }
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 场景管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
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
        
        /// <summary>
        /// 场景加载完成回调
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[GameManager] 场景加载完成: {scene.name}");
            }
            
            if (scene.name == gameSceneName)
            {
                // 游戏场景加载完成，开始游戏
                StartGame();
            }
            else if (scene.name == mainMenuSceneName)
            {
                ChangeState(GameState.Menu);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 游戏流程控制
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 开始游戏
        /// </summary>
        public void StartGame()
        {
            gameTimer = 0f;
            isTimerRunning = true;
            Time.timeScale = 1f;
            
            ChangeState(GameState.Playing);
            GameEvents.TriggerGameStart();
            
            if (showDebugInfo)
            {
                Debug.Log("[GameManager] 游戏开始！");
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
            Time.timeScale = 0f; // 暂停游戏
            ChangeState(GameState.Victory);
            GameEvents.TriggerGameVictory();
            
            if (showDebugInfo)
            {
                Debug.Log("[GameManager] 游戏胜利！");
            }
        }
        
        /// <summary>
        /// 游戏失败
        /// </summary>
        public void Defeat()
        {
            if (currentState != GameState.Playing) return;
            
            isTimerRunning = false;
            Time.timeScale = 0f; // 暂停游戏
            ChangeState(GameState.Defeat);
            GameEvents.TriggerGameDefeat();
            
            if (showDebugInfo)
            {
                Debug.Log("[GameManager] 游戏失败！");
            }
        }
        
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
            
            // 检查是否到达5分钟（暂时直接胜利，等BOSS系统完成后修改）
            if (gameTimer >= gameDuration)
            {
                Victory();
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
                Debug.Log($"[GameManager] 状态切换: {newState}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 200, 10, 190, 120));
            GUILayout.Label($"=== GameManager ===");
            GUILayout.Label($"State: {currentState}");
            GUILayout.Label($"Time: {GameTimeFormatted}");
            GUILayout.Label($"TimeScale: {Time.timeScale}");
            
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