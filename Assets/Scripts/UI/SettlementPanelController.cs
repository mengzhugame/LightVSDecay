using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace LightVsDecay.UI
{
    /// <summary>
    /// 结算面板控制器
    /// 胜利和失败共用同一个面板，通过切换标题区分
    /// </summary>
    public class SettlementPanelController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 面板引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("面板组件")]
        [SerializeField] private GameObject settlementPanel;
        [SerializeField] private Image victoryTitle;
        [SerializeField] private Image defeatTitle;
        
        [Header("信息面板")]
        [SerializeField] private GameObject crownIcon; // 满血通关才显示
        [SerializeField] private TextMeshProUGUI coinText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI killCountText;
        [SerializeField] private TextMeshProUGUI maxHitCountText;
        
        [Header("底部按钮")]
        [SerializeField] private Button doubleReceivedButton;
        [SerializeField] private Button returnButton;
        
        [Header("动画设置")]
        [SerializeField] private float showDelay = 0.5f;
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float numberAnimDuration = 1.0f; // 数字滚动时长
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private bool isVictory = false;
        private Core.SettlementData settlementData;
        private CanvasGroup canvasGroup;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 获取或添加 CanvasGroup
            if (settlementPanel != null)
            {
                canvasGroup = settlementPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = settlementPanel.AddComponent<CanvasGroup>();
                }
            }
            
            // 初始隐藏
            HidePanel();
        }
        
        private void Start()
        {
            // 订阅游戏结束事件
            Core.GameEvents.OnGameVictory += OnVictory;
            Core.GameEvents.OnGameDefeat += OnDefeat;
            
            // 设置按钮回调
            SetupButtons();
        }
        
        private void OnDestroy()
        {
            Core.GameEvents.OnGameVictory -= OnVictory;
            Core.GameEvents.OnGameDefeat -= OnDefeat;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void SetupButtons()
        {
            if (doubleReceivedButton != null)
            {
                doubleReceivedButton.onClick.AddListener(OnDoubleReceivedClicked);
            }
            
            if (returnButton != null)
            {
                returnButton.onClick.AddListener(OnReturnClicked);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnVictory()
        {
            isVictory = true;
            
            // 获取结算数据
            if (Core.PlayerProgressManager.Instance != null)
            {
                settlementData = Core.PlayerProgressManager.Instance.GetSettlementData();
            }
            else
            {
                // 创建默认数据
                settlementData = new Core.SettlementData();
            }
            
            StartCoroutine(ShowPanelCoroutine());
        }
        
        private void OnDefeat()
        {
            isVictory = false;
            
            // 获取结算数据
            if (Core.PlayerProgressManager.Instance != null)
            {
                settlementData = Core.PlayerProgressManager.Instance.GetSettlementData();
            }
            else
            {
                settlementData = new Core.SettlementData();
            }
            
            StartCoroutine(ShowPanelCoroutine());
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 面板显示/隐藏
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示面板协程（带动画）
        /// </summary>
        private IEnumerator ShowPanelCoroutine()
        {
            // 等待延迟
            yield return new WaitForSecondsRealtime(showDelay);
            
            // 设置标题显示
            SetTitleDisplay(isVictory);
            
            // 设置皇冠显示（仅满血胜利时显示）
            bool showCrown = isVictory && settlementData.isPerfect;
            if (crownIcon != null)
            {
                crownIcon.SetActive(showCrown);
            }
            
            // 显示面板
            if (settlementPanel != null)
            {
                settlementPanel.SetActive(true);
            }
            
            // 淡入动画
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                
                float elapsed = 0f;
                while (elapsed < fadeInDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                    yield return null;
                }
                
                canvasGroup.alpha = 1f;
            }
            
            // 播放数字滚动动画
            StartCoroutine(AnimateNumbersCoroutine());
        }
        
        /// <summary>
        /// 设置标题显示
        /// </summary>
        private void SetTitleDisplay(bool victory)
        {
            if (victoryTitle != null)
            {
                victoryTitle.gameObject.SetActive(victory);
            }
            
            if (defeatTitle != null)
            {
                defeatTitle.gameObject.SetActive(!victory);
            }
        }
        
        /// <summary>
        /// 数字滚动动画
        /// </summary>
        private IEnumerator AnimateNumbersCoroutine()
        {
            float elapsed = 0f;
            
            int targetCoin = settlementData.coinsEarned;
            int targetKill = settlementData.killCount;
            int targetMaxHit = settlementData.maxHitCount;
            
            while (elapsed < numberAnimDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / numberAnimDuration;
                
                // 使用 EaseOut 曲线
                float easeT = 1f - Mathf.Pow(1f - t, 3f);
                
                // 更新金币
                if (coinText != null)
                {
                    int currentCoin = Mathf.RoundToInt(Mathf.Lerp(0, targetCoin, easeT));
                    coinText.text = $"x{currentCoin}";
                }
                
                // 更新击杀数
                if (killCountText != null)
                {
                    int currentKill = Mathf.RoundToInt(Mathf.Lerp(0, targetKill, easeT));
                    killCountText.text = currentKill.ToString();
                }
                
                // 更新最大连击
                if (maxHitCountText != null)
                {
                    int currentMaxHit = Mathf.RoundToInt(Mathf.Lerp(0, targetMaxHit, easeT));
                    maxHitCountText.text = currentMaxHit.ToString();
                }
                
                yield return null;
            }
            
            // 确保最终值准确
            if (coinText != null) coinText.text = $"x{targetCoin}";
            if (killCountText != null) killCountText.text = targetKill.ToString();
            if (maxHitCountText != null) maxHitCountText.text = targetMaxHit.ToString();
            
            // 更新时间（不做动画）
            UpdateTimeDisplay();
        }
        
        /// <summary>
        /// 更新时间显示
        /// </summary>
        private void UpdateTimeDisplay()
        {
            if (timeText != null)
            {
                float time = settlementData.survivalTime;
                int minutes = Mathf.FloorToInt(time / 60f);
                int seconds = Mathf.FloorToInt(time % 60f);
                timeText.text = $"{minutes}:{seconds:D2}";
            }
        }
        
        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void HidePanel()
        {
            if (settlementPanel != null)
            {
                settlementPanel.SetActive(false);
            }
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDoubleReceivedClicked()
        {
            Debug.Log("[SettlementPanel] 双倍领取按钮点击");
            
            // 双倍金币奖励
            int doubleCoins = settlementData.coinsEarned * 2;
            
            // 添加金币
            if (Core.PlayerProgressManager.Instance != null)
            {
                Core.PlayerProgressManager.Instance.AddGoldCoins(doubleCoins);
            }
            else
            {
                int currentCoins = PlayerPrefs.GetInt("PlayerGoldCoins", 0);
                PlayerPrefs.SetInt("PlayerGoldCoins", currentCoins + doubleCoins);
                PlayerPrefs.Save();
            }
            
            Debug.Log($"[SettlementPanel] 双倍领取金币: {doubleCoins}");
            
            // TODO: 播放广告或其他双倍领取条件
            
            // 返回主菜单
            ReturnToMainMenu();
        }
        
        private void OnReturnClicked()
        {
            Debug.Log("[SettlementPanel] 返回按钮点击");
            
            // 普通领取金币
            if (Core.PlayerProgressManager.Instance != null)
            {
                Core.PlayerProgressManager.Instance.AddGoldCoins(settlementData.coinsEarned);
            }
            else
            {
                int currentCoins = PlayerPrefs.GetInt("PlayerGoldCoins", 0);
                PlayerPrefs.SetInt("PlayerGoldCoins", currentCoins + settlementData.coinsEarned);
                PlayerPrefs.Save();
            }
            
            Debug.Log($"[SettlementPanel] 普通领取金币: {settlementData.coinsEarned}");
            
            // 返回主菜单
            ReturnToMainMenu();
        }
        
        private void ReturnToMainMenu()
        {
            // 恢复时间缩放
            Time.timeScale = 1f;
            
            // 加载主菜单场景
            if (Core.GameManager.Instance != null)
            {
                Core.GameManager.Instance.LoadMainMenu();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口（用于调试或外部调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 手动显示胜利面板（用于测试）
        /// </summary>
        [ContextMenu("Test: Show Victory")]
        public void TestShowVictory()
        {
            settlementData = new Core.SettlementData
            {
                isVictory = true,
                isPerfect = true,
                coinsEarned = 150,
                survivalTime = 300f,
                killCount = 128,
                maxHitCount = 568
            };
            
            isVictory = true;
            StartCoroutine(ShowPanelCoroutine());
        }
        
        /// <summary>
        /// 手动显示失败面板（用于测试）
        /// </summary>
        [ContextMenu("Test: Show Defeat")]
        public void TestShowDefeat()
        {
            settlementData = new Core.SettlementData
            {
                isVictory = false,
                isPerfect = false,
                coinsEarned = 50,
                survivalTime = 180f,
                killCount = 65,
                maxHitCount = 234
            };
            
            isVictory = false;
            StartCoroutine(ShowPanelCoroutine());
        }
    }
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// 结算数据结构（放在 Core 命名空间）
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

namespace LightVsDecay.Core
{
    /// <summary>
    /// 结算数据
    /// </summary>
    [System.Serializable]
    public struct SettlementData
    {
        public bool isVictory;       // 是否胜利
        public bool isPerfect;       // 是否满血通关
        public int coinsEarned;      // 获得的金币
        public float survivalTime;   // 存活时间（秒）
        public int killCount;        // 击杀数
        public int maxHitCount;      // 最大连击数
        public int totalCoins;
        public int totalKills;
        public int maxCombo;
        public int finalLevel;
        
        /// <summary>格式化的生存时间</summary>
        public string SurvivalTimeFormatted => $"{Mathf.FloorToInt(survivalTime / 60):D1}:{Mathf.FloorToInt(survivalTime % 60):D2}";
    }
}