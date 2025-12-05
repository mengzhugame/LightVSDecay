// ============================================================
// SkillChooseOnePanel.cs
// 文件位置: Assets/Scripts/UI/Panels/SkillChooseOnePanel.cs
// 用途：升级时的技能三选一面板控制器
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightVsDecay.Core;
using LightVsDecay.Logic;

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
        [SerializeField] private TextMeshProUGUI levelText;
        
        [Header("选项按钮（3个）")]
        [SerializeField] private Button[] choiceButtons = new Button[3];
        
        [Header("选项内容")]
        [SerializeField] private Image[] choiceIcons = new Image[3];
        [SerializeField] private TextMeshProUGUI[] choiceNameTexts = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] choiceDescTexts = new TextMeshProUGUI[3];
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int currentLevel;
        
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
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null)
                {
                    int index = i; // 捕获局部变量
                    choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(index));
                }
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
            
            // 更新标题
            if (titleText != null)
            {
                titleText.text = "LEVEL UP!";
            }
            
            if (levelText != null)
            {
                levelText.text = $"Lv.{level}";
            }
            
            // TODO: 从 SkillDatabase 获取三选一数据并填充
            // 目前先显示占位内容
            SetupPlaceholderChoices();
            
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
        // 占位数据（临时）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void SetupPlaceholderChoices()
        {
            string[] names = { "折射棱镜", "聚能透镜", "冲击模块" };
            string[] descs = 
            { 
                "分裂2条副激光，造成30%伤害", 
                "伤害提升至150%，宽度80%", 
                "击退力130%，造成0.1s硬直" 
            };
            
            for (int i = 0; i < 3; i++)
            {
                if (choiceNameTexts[i] != null)
                {
                    choiceNameTexts[i].text = names[i];
                }
                
                if (choiceDescTexts[i] != null)
                {
                    choiceDescTexts[i].text = descs[i];
                }
                
                // 图标暂时不设置
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 选择回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnChoiceSelected(int index)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[SkillChooseOnePanel] 选择了选项 {index + 1}");
            }
            
            // TODO: 应用技能效果
            // ProgressManager.Instance.ApplySkill(selectedSkill);
            
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