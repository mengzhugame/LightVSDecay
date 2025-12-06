// ============================================================
// SkillChooseOnePanel.cs
// 文件位置: Assets/Scripts/UI/Panels/SkillChooseOnePanel.cs
// 用途：升级时的技能三选一面板控制器
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightVsDecay.Core;
using LightVsDecay.Logic;
using LightVsDecay.Data.SO;

namespace LightVsDecay.UI.Panels
{
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
        
        [Header("卡片组件引用")]
        [SerializeField] private Button[] cardButtons = new Button[3];
        [SerializeField] private Image[] cardBackgrounds = new Image[3];
        [SerializeField] private Image[] skillIcons = new Image[3];
        [SerializeField] private TextMeshProUGUI[] skillNameTexts = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] skillDescTexts = new TextMeshProUGUI[3];
        
        [Header("重掷按钮")]
        [SerializeField] private Button retryButton;
        [SerializeField] private int maxRetryCount = 1;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int currentLevel;
        private int retryCountRemaining;
        private List<SkillData> currentChoices = new List<SkillData>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            SetupButtons();
        }
        
        private void OnEnable()
        {
            // 面板显示时确保 Time.timeScale = 0
            Time.timeScale = 0f;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void SetupButtons()
        {
            // 绑定卡片按钮点击事件
            for (int i = 0; i < cardButtons.Length; i++)
            {
                if (cardButtons[i] != null)
                {
                    int index = i; // 捕获局部变量
                    cardButtons[i].onClick.AddListener(() => OnCardSelected(index));
                }
            }
            
            // 绑定重掷按钮
            if (retryButton != null)
            {
                retryButton.onClick.AddListener(OnRetryClicked);
            }
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
            
            // 更新标题
            if (titleText != null)
            {
                titleText.text = "请选择一个技能";
            }
            
            // 更新重掷按钮状态
            UpdateRetryButton();
            
            // 生成并显示三选一
            GenerateAndDisplayChoices();
            
            gameObject.SetActive(true);
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillChooseOnePanel] 显示升级面板 Lv.{level}");
            }
        }
        
        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void Hide()
        {
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
                Debug.LogError("[SkillChooseOnePanel] ❌ SkillDatabase 未设置！请在 Inspector 中配置");
                return;
            }
    
            // 从 ProgressManager 获取当前技能等级
            Dictionary<SkillType, int> currentSkillLevels = GetCurrentSkillLevels();
    
            Debug.Log($"[SkillChooseOnePanel] 当前已有技能数量: {currentSkillLevels.Count}");
    
            // 从数据库生成三选一
            currentChoices = skillDatabase.GenerateChoices(currentSkillLevels);
    
            Debug.Log($"[SkillChooseOnePanel] 生成选项数量: {currentChoices.Count}");
    
            // 如果没有生成选项，显示警告
            if (currentChoices.Count == 0)
            {
                Debug.LogError("[SkillChooseOnePanel] ❌ 没有可选技能！检查 SkillDatabase 中是否已添加技能");
                return;
            }
    
            // 填充卡片UI
            for (int i = 0; i < 3; i++)
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
                Debug.LogWarning("[SkillChooseOnePanel] ⚠️ ProgressManager.Instance 为空，返回空字典");
                return new Dictionary<SkillType, int>();
            }
    
            // 检查方法是否存在
            try
            {
                return ProgressManager.Instance.GetSkillLevels();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SkillChooseOnePanel] ❌ 调用 GetSkillLevels 失败: {e.Message}");
                Debug.LogError("[SkillChooseOnePanel] 请确认 ProgressManager 中已添加 GetSkillLevels() 方法");
                return new Dictionary<SkillType, int>();
            }
        }
        
        /// <summary>
        /// 显示单张卡片
        /// </summary>
        private void DisplayCard(int index, SkillData skill, Dictionary<SkillType, int> currentLevels)
        {
            if (skill == null) return;
            
            // 获取当前等级和下一等级
            int currentLv = currentLevels.GetValueOrDefault(skill.type, 0);
            int nextLv = currentLv + 1;
            bool isMaxLevel = currentLv >= skill.maxLevel;
            
            // 设置卡片背景
            if (cardBackgrounds[index] != null)
            {
                cardBackgrounds[index].sprite = GetCardBackground(skill.cardType, isMaxLevel);
            }
            
            // 设置技能图标
            if (skillIcons[index] != null && skill.icon != null)
            {
                skillIcons[index].sprite = skill.icon;
                skillIcons[index].enabled = true;
            }
            else if (skillIcons[index] != null)
            {
                skillIcons[index].enabled = false;
            }
            
            // 设置技能名称
            if (skillNameTexts[index] != null)
            {
                skillNameTexts[index].text = skill.displayName;
            }
            
            // 设置技能描述（使用下一等级的描述）
            if (skillDescTexts[index] != null)
            {
                string desc = skill.GetLevelDescription(nextLv);
                skillDescTexts[index].text = desc;
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillChooseOnePanel] 卡片{index}: {skill.displayName} (Lv.{currentLv}→{nextLv})");
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
                case SkillCardType.Recovery:
                    return cardBgRecovery;
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
            if (cardButtons[index] != null)
            {
                cardButtons[index].gameObject.SetActive(active);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 重掷功能
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnRetryClicked()
        {
            if (retryCountRemaining <= 0) return;
            
            retryCountRemaining--;
            UpdateRetryButton();
            
            // 重新生成选项
            GenerateAndDisplayChoices();
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillChooseOnePanel] 重掷，剩余次数: {retryCountRemaining}");
            }
        }
        
        private void UpdateRetryButton()
        {
            if (retryButton != null)
            {
                retryButton.interactable = retryCountRemaining > 0;
                
                // 更新按钮文本（如果有）
                var buttonText = retryButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = $"重掷 ({retryCountRemaining})";
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 选择回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnCardSelected(int index)
        {
            if (index < 0 || index >= currentChoices.Count)
            {
                Debug.LogError($"[SkillChooseOnePanel] 无效的选项索引: {index}");
                return;
            }
            
            SkillData selectedSkill = currentChoices[index];
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillChooseOnePanel] 选择了: {selectedSkill.displayName}");
            }
            
            // 应用技能（更新 SessionData.skillLevels）
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.ApplySkill(selectedSkill.type);
            }
            
            // 隐藏面板
            Hide();
            
            // 触发选择完成事件
            GameEvents.TriggerLevelUpChoiceComplete();
            
            // 恢复游戏
            Time.timeScale = 1f;
            
            if (showDebugInfo)
            {
                Debug.Log("[SkillChooseOnePanel] 选择完成，游戏恢复");
            }
        }
    }
}