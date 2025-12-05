// ============================================================
// UIManager.cs
// 文件位置: Assets/Scripts/UI/UIManager.cs
// 用途：统一管理所有 UI 面板的显示/隐藏
// ============================================================

using LightVsDecay.Core;
using LightVsDecay.UI.Panels;
using UnityEngine;

namespace LightVsDecay.UI
{
    /// <summary>
    /// UI 管理器（单例）
    /// 挂载在 Canvas 上，统一控制所有弹窗面板的显示/隐藏
    /// 各面板控制器只负责业务逻辑，不负责显隐
    /// </summary>
    public class UIManager : Singleton<UIManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 面板引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("面板引用")]
        [Tooltip("结算面板")]
        [SerializeField] private GameObject settlementPanel;

        [Tooltip("复活面板")]
        [SerializeField] private GameObject revivePanel;
        
        [Tooltip("暂停面板")]
        [SerializeField] private GameObject pausePanel;
        [Tooltip("技能选择面板")]  // 【新增】
        [SerializeField] private GameObject skillChoosePanel;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 面板控制器引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("面板控制器")]
        [SerializeField] private SettlementPanel settlementController;
        [SerializeField] private SkillChooseOnePanel skillChooseController;  // 【新增】
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private GameObject currentActivePanel = null;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            // 初始化：隐藏所有面板
            HideAllPanels();
        }
        
        private void OnEnable()
        {
            // 订阅游戏事件
            Core.GameEvents.OnGameVictory += OnGameVictory;
            Core.GameEvents.OnGameDefeat += OnGameDefeat;
            Core.GameEvents.OnGamePaused += OnGamePaused;
            Core.GameEvents.OnGameResumed += OnGameResumed;
            
            if (showDebugInfo)
            {
                Debug.Log("[UIManager] 事件已订阅");
            }
        }
        
        private void OnDisable()
        {
            // 取消订阅
            Core.GameEvents.OnGameVictory -= OnGameVictory;
            Core.GameEvents.OnGameDefeat -= OnGameDefeat;
            Core.GameEvents.OnGamePaused -= OnGamePaused;
            Core.GameEvents.OnGameResumed -= OnGameResumed;
            
            if (showDebugInfo)
            {
                Debug.Log("[UIManager] 事件已取消订阅");
            }
        }
        
        private void OnSingletonDestroy()
        {
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnGameVictory()
        {
            if (showDebugInfo)
            {
                Debug.Log("[UIManager] 收到胜利事件，显示结算面板");
            }
            
            ShowSettlementPanel(true);
        }
        
        private void OnGameDefeat()
        {
            if (showDebugInfo)
            {
                Debug.Log("[UIManager] 收到失败事件，显示结算面板");
            }
            
            ShowSettlementPanel(false);
        }
        
        private void OnGamePaused()
        {
            if (showDebugInfo)
            {
                Debug.Log("[UIManager] 收到暂停事件");
            }
            
            ShowPausePanel();
        }
        
        private void OnGameResumed()
        {
            if (showDebugInfo)
            {
                Debug.Log("[UIManager] 收到恢复事件");
            }
            
            HidePausePanel();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 面板控制 - 结算面板
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示结算面板
        /// </summary>
        /// <param name="isVictory">是否胜利</param>
        public void ShowSettlementPanel(bool isVictory)
        {
            if (settlementPanel == null)
            {
                Debug.LogWarning("[UIManager] settlementPanel 未设置！");
                return;
            }
            
            // 隐藏其他面板
            HideAllPanels();
            
            // 显示结算面板
            settlementPanel.SetActive(true);
            currentActivePanel = settlementPanel;
            
            // 通知控制器显示内容
            if (settlementController != null)
            {
                settlementController.Show(isVictory);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[UIManager] 结算面板已显示 (胜利: {isVictory})");
            }
        }
        
        /// <summary>
        /// 隐藏结算面板
        /// </summary>
        public void HideSettlementPanel()
        {
            if (settlementPanel != null)
            {
                settlementPanel.SetActive(false);
                
                if (currentActivePanel == settlementPanel)
                {
                    currentActivePanel = null;
                }
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 面板控制 - 复活面板
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示复活面板
        /// </summary>
        public void ShowRevivePanel()
        {
            if (revivePanel == null)
            {
                Debug.LogWarning("[UIManager] revivePanel 未设置！");
                return;
            }
            
            revivePanel.SetActive(true);
            currentActivePanel = revivePanel;
            
            if (showDebugInfo)
            {
                Debug.Log("[UIManager] 复活面板已显示");
            }
        }
        
        /// <summary>
        /// 隐藏复活面板
        /// </summary>
        public void HideRevivePanel()
        {
            if (revivePanel != null)
            {
                revivePanel.SetActive(false);
                
                if (currentActivePanel == revivePanel)
                {
                    currentActivePanel = null;
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 面板控制 - 暂停面板
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示暂停面板
        /// </summary>
        public void ShowPausePanel()
        {
            if (pausePanel == null) return;
            
            pausePanel.SetActive(true);
            currentActivePanel = pausePanel;
            
            if (showDebugInfo)
            {
                Debug.Log("[UIManager] 暂停面板已显示");
            }
        }
        
        /// <summary>
        /// 隐藏暂停面板
        /// </summary>
        public void HidePausePanel()
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
                
                if (currentActivePanel == pausePanel)
                {
                    currentActivePanel = null;
                }
            }
        }
        
        /// <summary>
        /// 显示技能选择面板
        /// </summary>
        public void ShowSkillChoosePanel(int level)
        {
            if (skillChoosePanel == null)
            {
                Debug.LogWarning("[UIManager] skillChoosePanel 未设置！");
                return;
            }
    
            // 先隐藏其他面板
            if (settlementPanel != null) settlementPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
    
            // 显示技能选择面板
            skillChoosePanel.SetActive(true);
            currentActivePanel = skillChoosePanel;
    
            // 调用控制器初始化
            if (skillChooseController != null)
            {
                skillChooseController.Show(level);
            }
    
            if (showDebugInfo)
            {
                Debug.Log($"[UIManager] 技能选择面板已显示 Lv.{level}");
            }
        }

        /// <summary>
        /// 隐藏技能选择面板
        /// </summary>
        public void HideSkillChoosePanel()
        {
            if (skillChoosePanel != null)
            {
                skillChoosePanel.SetActive(false);
        
                if (currentActivePanel == skillChoosePanel)
                {
                    currentActivePanel = null;
                }
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 通用方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 隐藏所有面板
        /// </summary>
        public void HideAllPanels()
        {
            if (settlementPanel != null) settlementPanel.SetActive(false);
            if (revivePanel != null) revivePanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (skillChoosePanel != null) skillChoosePanel.SetActive(false); 
            currentActivePanel = null;
        }
        
        /// <summary>
        /// 是否有面板正在显示
        /// </summary>
        public bool IsAnyPanelActive()
        {
            return currentActivePanel != null && currentActivePanel.activeSelf;
        }
        
        /// <summary>
        /// 获取当前激活的面板
        /// </summary>
        public GameObject GetActivePanel()
        {
            return currentActivePanel;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 200, 150, 190, 150));
            GUILayout.Label("=== UIManager ===");
            GUILayout.Label($"Active Panel: {(currentActivePanel != null ? currentActivePanel.name : "None")}");
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("Show Victory"))
            {
                ShowSettlementPanel(true);
            }
            
            if (GUILayout.Button("Show Defeat"))
            {
                ShowSettlementPanel(false);
            }
            
            if (GUILayout.Button("Hide All"))
            {
                HideAllPanels();
            }
            
            GUILayout.EndArea();
        }
#endif
    }
}