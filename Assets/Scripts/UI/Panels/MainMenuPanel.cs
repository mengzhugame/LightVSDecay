// ============================================================
// MainMenuPanel.cs (章节选择版)
// 文件位置: Assets/Scripts/UI/Panels/MainMenuPanel.cs
// 用途：主菜单控制器 - 章节选择、难度显示、箭头切换
// 更新：完整重构支持章节系统
// ============================================================

using LightVsDecay.Audio;
using LightVsDecay.Ads;
using LightVsDecay.Data.Runtime;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightVsDecay.Core;
using LightVsDecay.UI;

namespace LightVsDecay.UI.Panels
{
    /// <summary>
    /// 主菜单控制器
    /// 负责 MainScene 的 UI 交互
    /// 包含章节选择、难度显示、箭头切换功能
    /// </summary>
    public class MainMenuPanel : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - MidArea 章节选择
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ MidArea - 章节选择 ═══")]
        [Tooltip("章节卡片背景图")]
        [SerializeField] private Image chapterCardImage;
        
        [Tooltip("章节标题文本（如：1.深暗虚空）")]
        [SerializeField] private TextMeshProUGUI chapterTitleText;
        
        [Tooltip("锁定图标（显示在未解锁章节上）")]
        [SerializeField] private GameObject lockIcon;
        
        [Header("章节切换箭头")]
        [Tooltip("左箭头按钮")]
        [SerializeField] private Button leftArrowButton;
        
        [Tooltip("右箭头按钮")]
        [SerializeField] private Button rightArrowButton;
        
        [Tooltip("左箭头图片（用于控制颜色）")]
        [SerializeField] private Image leftArrowImage;
        
        [Tooltip("右箭头图片（用于控制颜色）")]
        [SerializeField] private Image rightArrowImage;
        
        [Header("难度图标")]
        [Tooltip("难度指示图标数组（5个）")]
        [SerializeField] private Image[] difficultyIcons;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - BottomArea
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ BottomArea - 底部区域 ═══")]
        [SerializeField] private Button startButton;
        
        [Tooltip("开始按钮的父物体（用于隐藏整个按钮区域）")]
        [SerializeField] private GameObject startButtonContainer;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 设置面板
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 设置面板 ═══")]
        [SerializeField] private SettingsPanel settingsPanel;
        [Header("═══ 面板引用 ═══")]
        [Tooltip("体力不足时弹出的面板")]
        [SerializeField] private TopBarTipsPanel topBarTipsPanel;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 显示设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 显示设置 ═══")]
        [Tooltip("箭头正常颜色")]
        [SerializeField] private Color arrowNormalColor = Color.white;
        
        [Tooltip("箭头禁用颜色（灰色半透明）")]
        [SerializeField] private Color arrowDisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        
        [Tooltip("章节锁定时的灰色透明度")]
        [SerializeField] private Color chapterLockedColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        
        [Tooltip("章节解锁时的正常颜色")]
        [SerializeField] private Color chapterUnlockedColor = Color.white;
        
        [Tooltip("能量不足时按钮颜色")]
        [SerializeField] private Color buttonDisabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        
        [Header("═══ 调试 ═══")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        // 章节选择状态
        private int currentViewIndex = 0;      // 当前查看的章节索引 (0-based)
        private int totalChapters = 3;         // 总章节数
        
        // 缓存
        private ChapterDatabase chapterDatabase;
        private bool hasTriggeredFirstPlayAutoStart = false;
        private bool hasAutoShownEnergyPanel = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 缓存按钮图片组件
            if (startButton != null)
            {
                UIButtonCommonHelper.Ensure(startButton);
                UIButtonCommonHelper.Ensure(leftArrowButton);
                UIButtonCommonHelper.Ensure(rightArrowButton);
            }
        }
        
        private void Start()
        {
            InitializeChapterDatabase();
            SetupButtons();
            LoadPlayerData();
            UpdateAllUI();
            TryStartFirstPlayTutorialBattle();
            StartCoroutine(ShowEnergyPanelIfNeededNextFrame());
        }

        private void OnEnable()
        {
            hasAutoShownEnergyPanel = false;
            ProgressManager.OnEnergyChanged += OnEnergyChanged;
            AdManager.Instance.ShowBanner("MainMenu");
            UpdateStartButtonUI();
        }

        private void OnDisable()
        {
            ProgressManager.OnEnergyChanged -= OnEnergyChanged;
            if (AdManager.HasInstance)
            {
                AdManager.Instance.HideBanner("MainMenu");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 初始化章节数据库引用
        /// </summary>
        private void InitializeChapterDatabase()
        {
            // 从 ProgressManager 获取章节数据库
            if (ProgressManager.Instance != null)
            {
                chapterDatabase = ProgressManager.Instance.ChapterDatabase;
                totalChapters = ProgressManager.Instance.TotalChapters;
                
                // 恢复上次查看的章节
                currentViewIndex = ProgressManager.Instance.CurrentViewChapterIndex;

                // 自动跳到最高已解锁章节（新解锁时直接呈现最新内容）
                int unlockedIndex = ProgressManager.Instance.UnlockedChapterIndex;
                if (unlockedIndex > currentViewIndex)
                {
                    currentViewIndex = unlockedIndex;
                    ProgressManager.Instance.CurrentViewChapterIndex = currentViewIndex;
                }
            }
            else
            {
                GameLogger.LogWarning("[MainMenuPanel] ProgressManager 未找到！使用默认值");
                totalChapters = 3;
                currentViewIndex = 0;
            }

            // 确保索引有效
            currentViewIndex = Mathf.Clamp(currentViewIndex, 0, totalChapters - 1);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[MainMenuPanel] 初始化: 总章节={totalChapters}, 当前查看={currentViewIndex}");
            }
        }
        
        /// <summary>
        /// 设置按钮事件
        /// </summary>
        private void SetupButtons()
        {
            // 开始按钮
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartButtonClicked);
            }

            // 左箭头
            if (leftArrowButton != null)
            {
                leftArrowButton.onClick.AddListener(OnLeftArrowClicked);
            }
            
            // 右箭头
            if (rightArrowButton != null)
            {
                rightArrowButton.onClick.AddListener(OnRightArrowClicked);
            }
        }
        
        /// <summary>
        /// 加载玩家数据
        /// </summary>
        private void LoadPlayerData()
        {
            // 资源数据（宝石/金币/能量/图纸）由 TopAreaController 统一管理
            TopAreaController.RefreshIfExists();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // UI 更新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 更新所有 UI
        /// </summary>
        private void UpdateAllUI()
        {
            UpdateChapterUI();
            UpdateArrowsUI();
            UpdateDifficultyUI();
            UpdateStartButtonUI();
        }

        /// <summary>
        /// 更新章节显示（卡片图、标题、锁定状态）
        /// </summary>
        private void UpdateChapterUI()
        {
            // 获取当前章节配置
            ChapterConfig chapterConfig = null;
            if (chapterDatabase != null)
            {
                chapterConfig = chapterDatabase.GetChapter(currentViewIndex);
            }
            
            // 检查章节是否解锁
            bool isUnlocked = IsChapterUnlocked(currentViewIndex);
            
            // 更新章节标题
            if (chapterTitleText != null)
            {
                if (chapterConfig != null)
                {
                    chapterTitleText.text = chapterConfig.DisplayTitle;
                }
                else
                {
                    chapterTitleText.text = $"{currentViewIndex + 1}.章节{currentViewIndex + 1}";
                }
            }
            
            // 更新章节卡片图
            if (chapterCardImage != null)
            {
                // 设置章节图片
                if (chapterConfig != null && chapterConfig.chapterCardImage != null)
                {
                    chapterCardImage.sprite = chapterConfig.chapterCardImage;
                }
                
                // 根据锁定状态设置颜色
                chapterCardImage.color = isUnlocked ? chapterUnlockedColor : chapterLockedColor;
            }
            
            // 更新锁图标
            if (lockIcon != null)
            {
                lockIcon.SetActive(!isUnlocked);
            }
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[MainMenuPanel] 章节UI更新: index={currentViewIndex}, unlocked={isUnlocked}");
            }
        }
        
        /// <summary>
        /// 更新箭头按钮状态
        /// </summary>
        private void UpdateArrowsUI()
        {
            // 左箭头：第一章时禁用
            bool canGoLeft = currentViewIndex > 0;
            UpdateArrowState(leftArrowButton, leftArrowImage, canGoLeft);
            
            // 右箭头：最后一章时禁用
            bool canGoRight = currentViewIndex < totalChapters - 1;
            UpdateArrowState(rightArrowButton, rightArrowImage, canGoRight);
        }
        
        /// <summary>
        /// 更新单个箭头的状态
        /// </summary>
        private void UpdateArrowState(Button button, Image image, bool isEnabled)
        {
            UIButtonCommonHelper.SetInteractable(button, isEnabled);
        }
        
        /// <summary>
        /// 更新难度图标显示
        /// </summary>
        private void UpdateDifficultyUI()
        {
            if (difficultyIcons == null || difficultyIcons.Length == 0) return;
            
            // 获取当前章节已通关的难度
            int completedDifficulty = GetChapterCompletedDifficulty(currentViewIndex);
            
            // 获取难度图标资源
            Sprite activeIcon = null;
            Sprite inactiveIcon = null;
            
            if (chapterDatabase != null)
            {
                activeIcon = chapterDatabase.difficultyIconActive;
                inactiveIcon = chapterDatabase.difficultyIconInactive;
            }
            
            // 更新每个难度图标
            for (int i = 0; i < difficultyIcons.Length; i++)
            {
                if (difficultyIcons[i] == null) continue;
                
                // i+1 对应难度等级 (1-5)
                // 已通关的难度显示亮图标
                bool isCompleted = (i + 1) <= completedDifficulty;
                
                // 设置图标
                if (isCompleted && activeIcon != null)
                {
                    difficultyIcons[i].sprite = activeIcon;
                    difficultyIcons[i].color = Color.white;
                }
                else if (!isCompleted && inactiveIcon != null)
                {
                    difficultyIcons[i].sprite = inactiveIcon;
                    difficultyIcons[i].color = Color.white;
                }
                else
                {
                    // 备用方案：没有图片时用颜色区分
                    difficultyIcons[i].color = isCompleted ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.5f);
                }
            }
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[MainMenuPanel] 难度UI更新: chapter={currentViewIndex}, completed={completedDifficulty}");
            }
        }
        
        /// <summary>
        /// 更新开始按钮状态
        /// </summary>
        private void UpdateStartButtonUI()
        {
            bool isUnlocked = IsChapterUnlocked(currentViewIndex);
            int  currentEnergy = ProgressManager.Instance != null
                ? ProgressManager.Instance.Energy
                : PlayerPrefs.GetInt("PlayerEnergy", 5);
            bool hasEnergy = currentEnergy > 0;
            
            // 如果章节未解锁，隐藏开始按钮
            if (startButtonContainer != null)
            {
                startButtonContainer.SetActive(isUnlocked);
            }
            else if (startButton != null)
            {
                // 没有容器时直接控制按钮
                startButton.gameObject.SetActive(isUnlocked);
            }
            
            // 能量不足时按钮变灰（但仍可见）
            if (startButton != null && isUnlocked)
            {
                UIButtonCommonHelper.SetInteractable(startButton, hasEnergy);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 数据查询辅助方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 检查章节是否已解锁
        /// </summary>
        private bool IsChapterUnlocked(int chapterIndex)
        {
            if (ProgressManager.Instance != null)
            {
                return ProgressManager.Instance.IsChapterUnlocked(chapterIndex);
            }
            
            // 降级：第一章默认解锁
            return chapterIndex == 0;
        }
        
        /// <summary>
        /// 获取章节已通关的最高难度
        /// </summary>
        private int GetChapterCompletedDifficulty(int chapterIndex)
        {
            if (ProgressManager.Instance != null)
            {
                return ProgressManager.Instance.GetChapterCompletedDifficulty(chapterIndex);
            }
            return 0;
        }
        
        /// <summary>
        /// 获取章节下一个要挑战的难度
        /// </summary>
        private int GetChapterNextDifficulty(int chapterIndex)
        {
            if (ProgressManager.Instance != null)
            {
                return ProgressManager.Instance.GetChapterNextDifficulty(chapterIndex);
            }
            return 1;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 左箭头点击 - 切换到上一章节
        /// </summary>
        private void OnLeftArrowClicked()
        {
            if (currentViewIndex <= 0) return;
            
            PlayButtonSound();
            
            currentViewIndex--;
            SaveCurrentViewIndex();
            
            UpdateChapterUI();
            UpdateArrowsUI();
            UpdateDifficultyUI();
            UpdateStartButtonUI();
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[MainMenuPanel] 切换到章节 {currentViewIndex + 1}");
            }
        }
        
        /// <summary>
        /// 右箭头点击 - 切换到下一章节
        /// </summary>
        private void OnRightArrowClicked()
        {
            if (currentViewIndex >= totalChapters - 1) return;
            
            PlayButtonSound();
            
            currentViewIndex++;
            SaveCurrentViewIndex();
            
            UpdateChapterUI();
            UpdateArrowsUI();
            UpdateDifficultyUI();
            UpdateStartButtonUI();
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[MainMenuPanel] 切换到章节 {currentViewIndex + 1}");
            }
        }
        
        /// <summary>
        /// 保存当前查看的章节索引
        /// </summary>
        private void SaveCurrentViewIndex()
        {
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.CurrentViewChapterIndex = currentViewIndex;
            }
        }
        
        /// <summary>
        /// 开始按钮点击
        /// </summary>
        private void OnStartButtonClicked()
        {
            // 检查章节是否解锁
            if (!IsChapterUnlocked(currentViewIndex))
            {
                GameLogger.Log("[MainMenuPanel] 章节未解锁！");
                return;
            }
            
            // 检查能量是否足够
            int currentEnergy = ProgressManager.Instance != null
                ? ProgressManager.Instance.Energy
                : PlayerPrefs.GetInt("PlayerEnergy", 5);
            if (currentEnergy <= 0)
            {
                // 原: GameLogger.Log("[MainMenuPanel] 能量不足！");
                // 新: 弹出体力面板引导玩家
                if (topBarTipsPanel != null)
                    topBarTipsPanel.Show();
                else
                    GameLogger.Log("[MainMenuPanel] 能量不足！");  // 降级兜底
                return;
            }
            
            PlayButtonSound();
            
            // 获取要挑战的难度
            int difficulty = GetChapterNextDifficulty(currentViewIndex);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[MainMenuPanel] 开始游戏: 章节={currentViewIndex + 1}, 难度={difficulty}");
            }
            
            // 设置游戏会话配置
            GameSessionConfig.Set(currentViewIndex, difficulty);
            
            // 扣除能量
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.ConsumeEnergy(1);
            }
            else
            {
                int e = PlayerPrefs.GetInt("PlayerEnergy", 5);
                PlayerPrefs.SetInt("PlayerEnergy", Mathf.Max(0, e - 1));
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

        /// <summary>
        /// 播放按钮音效
        /// </summary>
        private void PlayButtonSound()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButtonClick();
            }
        }

        private void TryStartFirstPlayTutorialBattle()
        {
            if (hasTriggeredFirstPlayAutoStart)
            {
                return;
            }

            MetaData meta = ProgressManager.Instance?.Meta;
            if (meta == null || !meta.isFirstPlay)
            {
                return;
            }

            hasTriggeredFirstPlayAutoStart = true;
            StartCoroutine(AutoStartFirstPlayBattle());
        }

        private IEnumerator AutoStartFirstPlayBattle()
        {
            yield return null;

            MetaData meta = ProgressManager.Instance?.Meta;
            if (meta == null || !meta.isFirstPlay)
            {
                yield break;
            }

            currentViewIndex = 0;
            SaveCurrentViewIndex();
            UpdateAllUI();

            meta.isFirstPlay = false;
            meta.Save();

            GameSessionConfig.Set(0, 1);

            if (showDebugInfo)
            {
                GameLogger.Log("[MainMenuPanel] 首次启动自动进入第1章难度1战斗，不消耗体力。");
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadGameScene();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 刷新UI（从外部调用，如返回主菜单时）
        /// </summary>
        public void RefreshUI()
        {
            LoadPlayerData();
            
            // 重新获取章节数据（可能有新解锁）
            if (ProgressManager.Instance != null)
            {
                totalChapters = ProgressManager.Instance.TotalChapters;
                currentViewIndex = Mathf.Clamp(currentViewIndex, 0, totalChapters - 1);
            }
            
            UpdateAllUI();
            StartCoroutine(ShowEnergyPanelIfNeededNextFrame());
            
            if (showDebugInfo)
            {
                GameLogger.Log("[MainMenuPanel] UI 已刷新");
            }
        }
        
        /// <summary>
        /// 跳转到指定章节（外部调用）
        /// </summary>
        public void NavigateToChapter(int chapterIndex)
        {
            if (chapterIndex >= 0 && chapterIndex < totalChapters)
            {
                currentViewIndex = chapterIndex;
                SaveCurrentViewIndex();
                UpdateAllUI();
            }
        }

        private IEnumerator ShowEnergyPanelIfNeededNextFrame()
        {
            yield return null;

            if (hasAutoShownEnergyPanel || hasTriggeredFirstPlayAutoStart)
            {
                yield break;
            }

            if (ProgressManager.Instance?.Meta?.isFirstPlay == true)
            {
                yield break;
            }

            int currentEnergy = ProgressManager.Instance != null
                ? ProgressManager.Instance.Energy
                : PlayerPrefs.GetInt("PlayerEnergy", 5);

            if (currentEnergy > 0 || topBarTipsPanel == null || topBarTipsPanel.gameObject.activeSelf)
            {
                yield break;
            }

            hasAutoShownEnergyPanel = true;
            topBarTipsPanel.Show(TopBarResourceType.Energy);
        }

        private void OnEnergyChanged(int current, int max)
        {
            UpdateStartButtonUI();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [ContextMenu("Debug - Unlock All Chapters")]
        private void DebugUnlockAllChapters()
        {
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.Meta.DebugUnlockAllChapters();
                RefreshUI();
            }
        }
        
        [ContextMenu("Debug - Complete All Difficulties")]
        private void DebugCompleteAllDifficulties()
        {
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.Meta.DebugCompleteAllDifficulties();
                RefreshUI();
            }
        }
        
        [ContextMenu("Debug - Reset Chapter Progress")]
        private void DebugResetChapterProgress()
        {
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.ResetChapterProgress();
                currentViewIndex = 0;
                RefreshUI();
            }
        }
#endif
    }
}
