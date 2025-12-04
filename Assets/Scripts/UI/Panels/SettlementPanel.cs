using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using LightVsDecay.Logic;

namespace LightVsDecay.UI.Panels
{
    /// <summary>
    /// 结算面板控制器
    /// 胜利和失败共用同一个面板，通过切换标题区分
    /// 
    /// 【职责分离】
    /// - UIManager：负责面板的显示/隐藏
    /// - SettlementPanelController：负责面板内容和交互逻辑
    /// </summary>
    public class SettlementPanel : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - UI 元素
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("标题")] [SerializeField] private GameObject victoryTitle;
        [SerializeField] private GameObject defeatTitle;

        [Header("信息面板")] [SerializeField] private GameObject crownIcon;
        [SerializeField] private TextMeshProUGUI coinText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI killCountText;
        [SerializeField] private TextMeshProUGUI maxHitCountText;

        [Header("底部按钮")] [SerializeField] private Button doubleReceivedButton;
        [SerializeField] private Button returnButton;

        [Header("动画设置")] [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float numberAnimDuration = 1.0f;

        [Header("调试")] [SerializeField] private bool showDebugInfo = false;

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
            // 获取或添加 CanvasGroup（用于淡入动画）
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 设置按钮回调
            SetupButtons();
        }

        private void SetupButtons()
        {
            if (doubleReceivedButton != null)
            {
                doubleReceivedButton.onClick.RemoveAllListeners();
                doubleReceivedButton.onClick.AddListener(OnDoubleReceivedClicked);
            }

            if (returnButton != null)
            {
                returnButton.onClick.RemoveAllListeners();
                returnButton.onClick.AddListener(OnReturnClicked);
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口（由 UIManager 调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 显示结算内容（由 UIManager 调用）
        /// </summary>
        /// <param name="victory">是否胜利</param>
        public void Show(bool victory)
        {
            isVictory = victory;

            // 获取结算数据
            if (ProgressManager.Instance != null)
            {
                settlementData = ProgressManager.Instance.GetSettlementData();
            }
            else
            {
                settlementData = new Core.SettlementData();
            }

            if (showDebugInfo)
            {
                Debug.Log($"[SettlementPanel] 显示结算 (胜利: {victory})");
            }

            // 开始显示动画
            StartCoroutine(ShowContentCoroutine());
        }

        /// <summary>
        /// 重置面板状态（由 UIManager 在隐藏前调用，可选）
        /// </summary>
        public void ResetPanel()
        {
            StopAllCoroutines();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内容显示
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator ShowContentCoroutine()
        {
            // 设置标题显示
            SetTitleDisplay(isVictory);

            // 设置皇冠显示（仅满血胜利）
            bool showCrown = isVictory && settlementData.isPerfect;
            if (crownIcon != null)
            {
                crownIcon.SetActive(showCrown);
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
            yield return StartCoroutine(AnimateNumbersCoroutine());

            if (showDebugInfo)
            {
                Debug.Log("[SettlementPanel] 内容显示完成");
            }
        }

        private void SetTitleDisplay(bool victory)
        {
            if (victoryTitle != null)
            {
                victoryTitle.SetActive(victory);
            }

            if (defeatTitle != null)
            {
                defeatTitle.SetActive(!victory);
            }
        }

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
                float easeT = 1f - Mathf.Pow(1f - t, 3f); // EaseOut

                if (coinText != null)
                {
                    int currentCoin = Mathf.RoundToInt(Mathf.Lerp(0, targetCoin, easeT));
                    coinText.text = $"x{currentCoin}";
                }

                if (killCountText != null)
                {
                    int currentKill = Mathf.RoundToInt(Mathf.Lerp(0, targetKill, easeT));
                    killCountText.text = currentKill.ToString();
                }

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

            // 更新时间显示
            UpdateTimeDisplay();
        }

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

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnDoubleReceivedClicked()
        {
            Debug.Log("[SettlementPanel] 双倍领取按钮点击");

            int doubleCoins = settlementData.coinsEarned * 2;

            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.AddGoldCoins(doubleCoins);
            }
            else
            {
                int currentCoins = PlayerPrefs.GetInt("PlayerGoldCoins", 0);
                PlayerPrefs.SetInt("PlayerGoldCoins", currentCoins + doubleCoins);
                PlayerPrefs.Save();
            }

            Debug.Log($"[SettlementPanel] 双倍领取金币: {doubleCoins}");

            // TODO: 播放广告

            ReturnToMainMenu();
        }

        private void OnReturnClicked()
        {
            Debug.Log("[SettlementPanel] 返回按钮点击");

            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.AddGoldCoins(settlementData.coinsEarned);
            }
            else
            {
                int currentCoins = PlayerPrefs.GetInt("PlayerGoldCoins", 0);
                PlayerPrefs.SetInt("PlayerGoldCoins", currentCoins + settlementData.coinsEarned);
                PlayerPrefs.Save();
            }

            Debug.Log($"[SettlementPanel] 普通领取金币: {settlementData.coinsEarned}");

            ReturnToMainMenu();
        }

        private void ReturnToMainMenu()
        {
            // 恢复时间缩放
            Time.timeScale = 1f;

            // 加载主菜单
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadMainMenu();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
            }
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