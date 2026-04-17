// ============================================================
// SkillChooseOnePanel.cs
// 文件位置: Assets/Scripts/UI/Panels/SkillChooseOnePanel.cs
// 用途：升级时的技能三选一面板控制器
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightVsDecay.Ads;
using LightVsDecay.Core;
using LightVsDecay.Logic;
using LightVsDecay.Data.SO;
using LightVsDecay.Audio;
using LightVsDecay.UI;

namespace LightVsDecay.UI.Panels
{
    /// <summary>
    /// 单张卡片的 UI 组件引用
    /// </summary>
    [System.Serializable]
    public class SkillCardUI
    {
        [Header("卡片按钮和背景")]
        public Button cardButton;
        public Image cardBackground;
        public Image glowFrame;
        
        [Header("技能信息")]
        public Image skillIcon;
        public TextMeshProUGUI skillNameText;
        public TextMeshProUGUI skillDescText;
        public Image laneBadge;
        public TextMeshProUGUI laneText;
        
        [Header("NewTag 标签")]
        public GameObject newTagObj;
        public TextMeshProUGUI tagText;
        
        [Header("Upgrade 菱形")]
        public GameObject upgradeObj;
        public Image[] diamondImages = new Image[3];
        [System.NonSerialized] public Vector3 defaultScale = Vector3.one;
    }
    
    /// <summary>
    /// 技能选择面板控制器
    /// 升级时显示，玩家选择后关闭并恢复游戏
    /// </summary>
    public class SkillChooseOnePanel : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("标题")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI tipsBannerText;
        
        [Header("技能数据库")]
        [SerializeField] private SkillDatabase skillDatabase;
        
        [Header("卡片背景图（按类型）")]
        [Tooltip("红色/橙色 - 主动输出技能")]
        [SerializeField] private Sprite cardBgAttack;
        [Tooltip("青色/蓝色 - 被动/控制技能")]
        [SerializeField] private Sprite cardBgPassive;
        [Tooltip("绿色 - 消耗品")]
        [SerializeField] private Sprite cardBgRecovery;
        [Tooltip("金色 - 满级技能")]
        [SerializeField] private Sprite cardBgMaxLevel;
        
        [Header("菱形图标")]
        [Tooltip("亮起的菱形图标")]
        [SerializeField] private Sprite diamondLit;
        [Tooltip("灰色的菱形图标")]
        [SerializeField] private Sprite diamondDim;
        
        [Header("卡片组件引用（3张卡）")]
        [SerializeField] private SkillCardUI[] cards = new SkillCardUI[3];
        
        [Header("重掷按钮")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Image retryIconImage;
        [SerializeField] private TextMeshProUGUI retryButtonText;
        [SerializeField] private Sprite freeRetryIconSprite;
        [SerializeField] private Sprite adRetryIconSprite;
        [SerializeField] private Color retryIconNormalColor = Color.white;
        [SerializeField] private Color retryIconDisabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        [SerializeField] private int maxRetryCount = 1;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private static readonly Color FrostColor = new Color(0.35f, 0.78f, 1f, 1f);
        private static readonly Color FocusColor = new Color(1f, 0.72f, 0.24f, 1f);
        private static readonly Color PrismColor = new Color(0.96f, 0.44f, 0.78f, 1f);
        private static readonly Color ImpactColor = new Color(0.98f, 0.88f, 0.3f, 1f);
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int currentLevel;
        private int retryCountRemaining;
        private List<SkillData> currentChoices = new List<SkillData>();
        private Color retryTextNormalColor = Color.white;
        private bool isSelectionInProgress;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            CacheRetryIcon();
            CacheCardDefaultScales();
            SetupButtons();
            UIButtonCommonHelper.Ensure(retryButton);
        }
        
        private void OnEnable()
        {
            // 面板显示时确保 Time.timeScale = 0
            Time.timeScale = 0f;
            // 弹出三选一时暂停战斗音效（含火球等场景 AudioSource）
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PauseBattleAudioForOverlay();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void SetupButtons()
        {
            // 绑定卡片按钮点击事件
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i]?.cardButton != null)
                {
                    int index = i; // 捕获局部变量
                    UIButtonCommonHelper.Ensure(cards[i].cardButton);
                    cards[i].cardButton.onClick.AddListener(() => OnCardSelected(index));
                }
            }
            
            // 绑定重掷按钮
            if (retryButton != null)
            {
                retryButton.onClick.AddListener(OnRetryClicked);
            }
        }

        private void CacheCardDefaultScales()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                RectTransform rect = cards[i]?.cardButton != null
                    ? cards[i].cardButton.transform as RectTransform
                    : null;
                cards[i].defaultScale = rect != null ? rect.localScale : Vector3.one;
            }
        }

        private void ResetCardVisualState()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i]?.cardButton == null)
                {
                    continue;
                }

                RectTransform rect = cards[i].cardButton.transform as RectTransform;
                if (rect != null)
                {
                    rect.localScale = cards[i].defaultScale;
                }

                UIButtonCommonHelper.SetInteractable(cards[i].cardButton, true);
            }

            UpdateRetryButton();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示面板并设置等级
        /// </summary>
        public void Show(int level)
        {
            currentLevel = level;
            retryCountRemaining = maxRetryCount;
            isSelectionInProgress = false;
            ResetCardVisualState();
            
            // 更新标题
            if (titleText != null)
            {
                titleText.text = "请选择一个技能";
            }
            if (tipsBannerText != null)
            {
                tipsBannerText.text = "首次推荐从极寒 / 聚能 / 分裂中选择一个流派，后续同流派技能会更容易形成联动。";
            }
            
            // 更新重掷按钮状态
            UpdateRetryButton();
            
            // 生成并显示三选一
            GenerateAndDisplayChoices();
            
            gameObject.SetActive(true);
            // ★ 清除 EventSystem 当前选中，防止首次点击被吞
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillChooseOnePanel] 显示升级面板 Lv.{level}");
            }
        }
        
        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void Hide()
        {
            ResetCardVisualState();
            gameObject.SetActive(false);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 三选一生成
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void GenerateAndDisplayChoices()
        {
            // 检查数据库
            if (skillDatabase == null)
            {
                GameLogger.LogError("[SkillChooseOnePanel] SkillDatabase is not assigned.");
                return;
            }
            
            // 从 ProgressManager 获取当前技能等级
            Dictionary<SkillType, int> currentSkillLevels = GetCurrentSkillLevels();
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillChooseOnePanel] 当前已有技能数量: {currentSkillLevels.Count}");
            }
            
            // 从数据库生成三选一
            currentChoices = skillDatabase.GenerateChoices(currentSkillLevels);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillChooseOnePanel] 生成选项数量: {currentChoices.Count}");
            }
            
            // 如果没有生成选项，显示警告
            if (currentChoices.Count == 0)
            {
                GameLogger.LogError("[SkillChooseOnePanel] No available skill choices were generated.");
                return;
            }
            
            // 填充卡片UI
            for (int i = 0; i < cards.Length; i++)
            {
                if (i < currentChoices.Count)
                {
                    DisplayCard(i, currentChoices[i], currentSkillLevels);
                    SetCardActive(i, true);
                }
                else
                {
                    SetCardActive(i, false);
                }
            }
        }
        
        /// <summary>
        /// 获取当前技能等级（从 ProgressManager）
        /// </summary>
        private Dictionary<SkillType, int> GetCurrentSkillLevels()
        {
            if (ProgressManager.Instance == null)
            {
                GameLogger.LogWarning("[SkillChooseOnePanel] ⚠️ ProgressManager.Instance 为空，返回空字典");
                return new Dictionary<SkillType, int>();
            }
            
            try
            {
                return ProgressManager.Instance.GetSkillLevels();
            }
            catch (System.Exception e)
            {
                GameLogger.LogError($"[SkillChooseOnePanel] ❌ 调用 GetSkillLevels 失败: {e.Message}");
                return new Dictionary<SkillType, int>();
            }
        }
        
        /// <summary>
        /// 显示单张卡片
        /// </summary>
        private void DisplayCard(int index, SkillData skill, Dictionary<SkillType, int> currentLevels)
        {
            if (skill == null || index >= cards.Length) return;
            
            SkillCardUI card = cards[index];
            if (card == null) return;
            
            // 获取当前等级和下一等级
            int currentLv = currentLevels.GetValueOrDefault(skill.type, 0);
            int nextLv = currentLv + 1;
            bool isMaxLevel = nextLv >= skill.maxLevel;

            // ========== 1. 设置卡片背景 ==========
            if (card.cardBackground != null)
            {
                card.cardBackground.sprite = GetCardBackground(skill.cardType, isMaxLevel);
            }
            
            // ========== 2. 设置技能图标 ==========
            if (card.skillIcon != null)
            {
                if (skill.icon != null)
                {
                    card.skillIcon.sprite = skill.icon;
                    card.skillIcon.enabled = true;
                }
                else
                {
                    card.skillIcon.enabled = false;
                }
            }
            
            // ========== 3. 设置技能名称 ==========
            if (card.skillNameText != null)
            {
                card.skillNameText.text = skill.displayName;
            }
            
            // ========== 4. 设置技能描述（支持富文本颜色） ==========
            if (card.skillDescText != null)
            {
                string desc = skill.GetLevelDescription(nextLv);
                card.skillDescText.text = desc;
            }
            
            // ========== 5. 设置 NewTag ==========
            SetupNewTag(card, nextLv, isMaxLevel);
            
            // ========== 6. 设置 Upgrade 菱形 ==========
            SetupUpgradeDiamonds(card, nextLv);
            SetupLaneDecor(card, skill, currentLevels);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillChooseOnePanel] Card {index}: {skill.displayName} (Lv.{currentLv}->{nextLv}, Max={isMaxLevel})");
            }
        }
        
        /// <summary>
        /// 设置 NewTag 显示
        /// </summary>
        private void SetupNewTag(SkillCardUI card, int nextLv, bool isMaxLevel)
        {
            if (card.newTagObj == null) return;

            // 等级1显示 "NEW"，等级5显示 "MAX"，其他隐藏
            if (nextLv == 1)
            {
                card.newTagObj.SetActive(true);
                if (card.tagText != null)
                {
                    card.tagText.text = "NEW";
                }
            }
            else if (isMaxLevel) // 等级5
            {
                card.newTagObj.SetActive(true);
                if (card.tagText != null)
                {
                    card.tagText.text = "MAX";
                }
            }
            else // 等级2-4
            {
                card.newTagObj.SetActive(false);
            }
        }
        
        /// <summary>
        /// 设置 Upgrade 菱形显示
        /// </summary>
        private void SetupUpgradeDiamonds(SkillCardUI card, int nextLv)
        {
            if (card.upgradeObj == null) return;

            // 显示 Upgrade
            card.upgradeObj.SetActive(true);
            
            // 根据等级设置菱形亮灭
            // 等级1: 0亮, 等级2: 1亮, 等级3: 2亮, 等级4: 3亮, 等级5: 3亮
            int litCount = Mathf.Clamp(nextLv - 1, 0, 3);
            
            for (int i = 0; i < card.diamondImages.Length; i++)
            {
                if (card.diamondImages[i] != null)
                {
                    // i < litCount 的菱形亮起
                    card.diamondImages[i].sprite = (i < litCount) ? diamondLit : diamondDim;
                }
            }
        }
        
        /// <summary>
        /// 根据卡片类型获取背景图
        /// </summary>
        private Sprite GetCardBackground(SkillCardType cardType, bool isMaxLevel)
        {
            // 满级使用金色背景
            if (isMaxLevel && cardBgMaxLevel != null)
            {
                return cardBgMaxLevel;
            }
            
            switch (cardType)
            {
                case SkillCardType.Attack:
                    return cardBgAttack;
                case SkillCardType.Passive:
                    return cardBgPassive;
                case SkillCardType.MaxLevel:
                    return cardBgMaxLevel;
                default:
                    return cardBgAttack;
            }
        }
        
        /// <summary>
        /// 设置卡片显示/隐藏
        /// </summary>
        private void SetCardActive(int index, bool active)
        {
            if (index < cards.Length && cards[index]?.cardButton != null)
            {
                cards[index].cardButton.gameObject.SetActive(active);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 重掷功能
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnRetryClicked()
        {
            if (retryCountRemaining > 0)
            {
                retryCountRemaining--;
                UpdateRetryButton();
                GenerateAndDisplayChoices();

                if (showDebugInfo)
                {
                    GameLogger.Log($"[SkillChooseOnePanel] 重掷，剩余次数: {retryCountRemaining}");
                }

                return;
            }

            AnalyticsManager.LogScene(AnalyticsSceneIds.AdClickReroll);
            AdManager.Instance.ShowRewardedAd(AdType.SkillReroll,
                onSuccess: () =>
                {
                    GenerateAndDisplayChoices();
                    UpdateRetryButton();
                },
                onFail: UpdateRetryButton);
        }

        private void SetupLaneDecor(SkillCardUI card, SkillData skill, Dictionary<SkillType, int> currentLevels)
        {
            string lane = GetLaneName(skill.type);
            Color laneColor = GetLaneColor(skill.type);
            bool hasSynergy = HasOwnedLane(currentLevels, lane);

            if (card.laneBadge != null)
            {
                card.laneBadge.gameObject.SetActive(!string.IsNullOrEmpty(lane));
                card.laneBadge.color = laneColor;
            }

            if (card.laneText != null)
            {
                card.laneText.gameObject.SetActive(!string.IsNullOrEmpty(lane));
                card.laneText.text = string.IsNullOrEmpty(lane) ? string.Empty : $"{lane}流派";
                card.laneText.color = hasSynergy ? laneColor : Color.white;
            }

            if (card.glowFrame != null)
            {
                card.glowFrame.gameObject.SetActive(hasSynergy);
                card.glowFrame.color = laneColor;
            }
        }

        private static bool HasOwnedLane(Dictionary<SkillType, int> currentLevels, string lane)
        {
            if (string.IsNullOrEmpty(lane))
            {
                return false;
            }

            foreach (var pair in currentLevels)
            {
                if (pair.Value > 0 && GetLaneName(pair.Key) == lane)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetLaneName(SkillType type)
        {
            switch (type)
            {
                case SkillType.Frost:
                case SkillType.FrostSpread:
                    return "极寒";
                case SkillType.Focus:
                case SkillType.Power:
                case SkillType.Crit:
                case SkillType.Shatter:
                    return "聚能";
                case SkillType.Prism:
                case SkillType.Wide:
                case SkillType.Chain:
                case SkillType.Reflex:
                    return "分裂";
                case SkillType.Impact:
                    return "冲击";
                default:
                    return string.Empty;
            }
        }

        private static Color GetLaneColor(SkillType type)
        {
            switch (type)
            {
                case SkillType.Frost:
                case SkillType.FrostSpread:
                    return FrostColor;
                case SkillType.Focus:
                case SkillType.Power:
                case SkillType.Crit:
                case SkillType.Shatter:
                    return FocusColor;
                case SkillType.Prism:
                case SkillType.Wide:
                case SkillType.Chain:
                case SkillType.Reflex:
                    return PrismColor;
                case SkillType.Impact:
                    return ImpactColor;
                default:
                    return Color.white;
            }
        }
        
        private void UpdateRetryButton()
        {
            if (retryButton != null)
            {
                bool hasFreeRetry = retryCountRemaining > 0;
                bool canWatchAd = AdManager.Instance.CanWatchAd(AdType.SkillReroll);
                UIButtonCommonHelper.SetInteractable(retryButton, hasFreeRetry || canWatchAd);

                UpdateRetryIcon(hasFreeRetry, canWatchAd);
                UIButtonCommonHelper.Sync(retryButton);
            }
        }

        private void CacheRetryIcon()
        {
            if (retryIconImage == null && retryButton != null)
            {
                Image[] images = retryButton.GetComponentsInChildren<Image>(true);
                foreach (Image image in images)
                {
                    if (image != null && image != retryButton.targetGraphic)
                    {
                        retryIconImage = image;
                        break;
                    }
                }
            }

            if (freeRetryIconSprite == null && retryIconImage != null)
            {
                freeRetryIconSprite = retryIconImage.sprite;
            }

            if (retryButtonText == null && retryButton != null)
            {
                retryButtonText = retryButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (retryButtonText != null)
            {
                retryTextNormalColor = retryButtonText.color;
            }
        }

        private void UpdateRetryIcon(bool hasFreeRetry, bool canWatchAd)
        {
            if (retryIconImage == null)
            {
                return;
            }

            Sprite targetSprite = hasFreeRetry ? freeRetryIconSprite : adRetryIconSprite;
            if (targetSprite != null)
            {
                retryIconImage.sprite = targetSprite;
            }

            retryIconImage.color = retryIconNormalColor;

            if (retryButtonText != null)
            {
                retryButtonText.color = retryTextNormalColor;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 选择回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private void OnCardSelected(int index)
        {
            if (isSelectionInProgress)
            {
                return;
            }
            if (index < 0 || index >= currentChoices.Count)
            {
                GameLogger.LogError($"[SkillChooseOnePanel] 无效的选项索引: {index}");
                return;
            }
            // 【新增】播放技能选择音效
            SkillData selectedSkill = currentChoices[index];
            isSelectionInProgress = true;
            SetButtonsInteractable(false);
            StartCoroutine(FinishCardSelection(index, selectedSkill));
            return;
        }


        private IEnumerator FinishCardSelection(int index, SkillData selectedSkill)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySkillCardSelect();
            }

            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillChooseOnePanel] Selected skill: {selectedSkill.displayName}");
            }

            RectTransform rect = cards[index]?.cardButton != null
                ? cards[index].cardButton.transform as RectTransform
                : null;

            if (rect != null)
            {
                yield return UIAnimationHelper.PlayScalePunch(rect, 1.08f, 0.18f, true);
                rect.localScale = cards[index].defaultScale;
            }

            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.ApplySkill(selectedSkill.type);
                if (!ProgressManager.Instance.Meta.hasSeenSkillTutorial)
                {
                    ProgressManager.Instance.Meta.hasSeenSkillTutorial = true;
                    ProgressManager.Instance.Meta.Save();
                }
            }

            Hide();
            Time.timeScale = 1f;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ResumeBattleAudioForOverlay();
            }

            GameEvents.TriggerLevelUpChoiceComplete();

            if (showDebugInfo)
            {
                GameLogger.Log("[SkillChooseOnePanel] Skill selection completed");
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i]?.cardButton != null)
                {
                    UIButtonCommonHelper.SetInteractable(cards[i].cardButton, interactable);
                }
            }

            if (retryButton != null)
            {
                bool canRetry = interactable && (retryCountRemaining > 0 || AdManager.Instance.CanWatchAd(AdType.SkillReroll));
                UIButtonCommonHelper.SetInteractable(retryButton, canRetry);
            }
        }
    }
}
