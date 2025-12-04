using LightVsDecay.Logic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LightVsDecay.UI.Panels
{
    /// <summary>
    /// 主菜单控制器
    /// 负责 MainScene 的 UI 交互
    /// </summary>
    public class MainMenuPanel : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - TopArea
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("TopArea - 顶部区域")]
        [SerializeField] private Button settingButton;
        [SerializeField] private TextMeshProUGUI gemText;
        [SerializeField] private TextMeshProUGUI goldCoinText;
        [SerializeField] private TextMeshProUGUI energyText;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - MidArea
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("MidArea - 中间区域")]
        [SerializeField] private Image chapterImage;
        [SerializeField] private TextMeshProUGUI chapterText;
        
        [Tooltip("难度指示图标（5个）")]
        [SerializeField] private Image[] difficultyImages;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - BottomArea
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("BottomArea - 底部区域")]
        [SerializeField] private Button startButton;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 显示设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("显示设置")]
        [Tooltip("难度激活颜色")]
        [SerializeField] private Color difficultyActiveColor = Color.white;
        
        [Tooltip("难度未激活颜色")]
        [SerializeField] private Color difficultyInactiveColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        
        [Tooltip("能量不足时按钮颜色")]
        [SerializeField] private Color buttonDisabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int playerGems = 0;
        private int playerGoldCoins = 0;
        private int playerEnergy = 5;      // 当前能量
        private int maxEnergy = 5;         // 最大能量
        private int currentChapter = 1;
        private int currentDifficulty = 1; // 1-5
        
        private Image startButtonImage;
        private Color startButtonOriginalColor;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 缓存按钮图片组件
            if (startButton != null)
            {
                startButtonImage = startButton.GetComponent<Image>();
                if (startButtonImage != null)
                {
                    startButtonOriginalColor = startButtonImage.color;
                }
            }
        }
        
        private void Start()
        {
            SetupButtons();
            LoadPlayerData();
            UpdateAllUI();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void SetupButtons()
        {
            // 开始按钮
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartButtonClicked);
            }
            
            // 设置按钮
            if (settingButton != null)
            {
                settingButton.onClick.AddListener(OnSettingButtonClicked);
            }
        }
        
        private void LoadPlayerData()
        {
            // 从 PlayerProgressManager 或 PlayerPrefs 加载数据
            if (ProgressManager.Instance != null)
            {
                playerGems = ProgressManager.Instance.Gems;
                playerGoldCoins = ProgressManager.Instance.GoldCoins;
                playerEnergy = ProgressManager.Instance.Energy;
            }
            else
            {
                // 降级方案：从 PlayerPrefs 加载
                playerGems = PlayerPrefs.GetInt("PlayerGems", 0);
                playerGoldCoins = PlayerPrefs.GetInt("PlayerGoldCoins", 0);
                playerEnergy = PlayerPrefs.GetInt("PlayerEnergy", 5);
            }
            
            // 加载关卡进度
            currentChapter = PlayerPrefs.GetInt("CurrentChapter", 1);
            currentDifficulty = PlayerPrefs.GetInt("CurrentDifficulty", 1);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // UI 更新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateAllUI()
        {
            UpdateResourcesUI();
            UpdateChapterUI();
            UpdateDifficultyUI();
            UpdateStartButtonState();
        }
        
        /// <summary>
        /// 更新资源显示（宝石、金币、能量）
        /// </summary>
        private void UpdateResourcesUI()
        {
            if (gemText != null)
            {
                gemText.text = playerGems.ToString();
            }
            
            if (goldCoinText != null)
            {
                goldCoinText.text = playerGoldCoins.ToString();
            }
            
            if (energyText != null)
            {
                energyText.text = $"{playerEnergy}/{maxEnergy}";
            }
        }
        
        /// <summary>
        /// 更新章节显示
        /// </summary>
        private void UpdateChapterUI()
        {
            if (chapterText != null)
            {
                chapterText.text = $"Chapter {currentChapter}";
            }
            
            // TODO: 根据 currentChapter 切换 chapterImage
        }
        
        /// <summary>
        /// 更新难度指示器
        /// </summary>
        private void UpdateDifficultyUI()
        {
            if (difficultyImages == null || difficultyImages.Length == 0) return;
            
            for (int i = 0; i < difficultyImages.Length; i++)
            {
                if (difficultyImages[i] != null)
                {
                    // i+1 <= currentDifficulty 的图标点亮
                    bool isActive = (i + 1) <= currentDifficulty;
                    difficultyImages[i].color = isActive ? difficultyActiveColor : difficultyInactiveColor;
                }
            }
        }
        
        /// <summary>
        /// 更新开始按钮状态（能量不足时变灰）
        /// </summary>
        private void UpdateStartButtonState()
        {
            if (startButton == null) return;
            
            bool hasEnergy = playerEnergy > 0;
            startButton.interactable = hasEnergy;
            
            if (startButtonImage != null)
            {
                startButtonImage.color = hasEnergy ? startButtonOriginalColor : buttonDisabledColor;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnStartButtonClicked()
        {
            Debug.Log("[MainMenuController] 点击开始游戏");
            
            // 检查能量是否足够
            if (playerEnergy <= 0)
            {
                Debug.Log("[MainMenuController] 能量不足！");
                // TODO: 显示能量不足提示
                return;
            }
            
            // 扣除能量
            playerEnergy--;
            
            // 保存数据
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.ConsumeEnergy(1);
            }
            else
            {
                PlayerPrefs.SetInt("PlayerEnergy", playerEnergy);
                PlayerPrefs.Save();
            }
            
            // 加载游戏场景
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadGameScene();
            }
            else
            {
                // 降级方案：直接加载场景
                UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
            }
        }
        
        private void OnSettingButtonClicked()
        {
            Debug.Log("[MainMenuController] 点击设置按钮");
            // TODO: 打开设置面板
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 刷新UI（从外部调用）
        /// </summary>
        public void RefreshUI()
        {
            LoadPlayerData();
            UpdateAllUI();
        }
        
        /// <summary>
        /// 设置当前章节
        /// </summary>
        public void SetChapter(int chapter)
        {
            currentChapter = Mathf.Max(1, chapter);
            PlayerPrefs.SetInt("CurrentChapter", currentChapter);
            UpdateChapterUI();
        }
        
        /// <summary>
        /// 设置难度等级
        /// </summary>
        public void SetDifficulty(int difficulty)
        {
            currentDifficulty = Mathf.Clamp(difficulty, 1, 5);
            PlayerPrefs.SetInt("CurrentDifficulty", currentDifficulty);
            UpdateDifficultyUI();
        }
    }
}