// ============================================================
// MainSceneUIManager.cs
// 文件位置: Assets/Scripts/UI/MainSceneUIManager.cs
// 用途：主场景 UI 管理器 - 负责面板切换（主界面、科技树、装备）
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using LightVsDecay.Audio;

namespace LightVsDecay.UI
{
    /// <summary>
    /// 主场景 UI 状态枚举
    /// </summary>
    public enum MainSceneState
    {
        Main,       // 主界面（默认）
        KeJi,       // 科技树界面
        ZhuangBei   // 装备界面
    }
    
    /// <summary>
    /// 主场景 UI 管理器
    /// 负责 MainScene 中各面板的切换和按钮状态管理
    /// </summary>
    public class MainSceneUIManager : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public static MainSceneUIManager Instance { get; private set; }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 面板引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 面板引用 ═══")]
        [Tooltip("全局背景（KeJiPanel 显示时隐藏）")]
        [SerializeField] private GameObject globalBackground;
        
        [Tooltip("主界面面板")]
        [SerializeField] private GameObject mainPanel;
        
        [Tooltip("科技树面板")]
        [SerializeField] private GameObject keJiPanel;
        
        [Tooltip("装备面板")]
        [SerializeField] private GameObject zhuangBeiPanel;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - TopArea 按钮引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ TopArea 按钮 ═══")]
        [Tooltip("设置按钮")]
        [SerializeField] private GameObject settingButton;
        
        [Tooltip("返回按钮")]
        [SerializeField] private GameObject backButton;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - BottomArea 按钮引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ BottomArea 按钮 ═══")]
        [Tooltip("科技树按钮")]
        [SerializeField] private Button keJiButton;
        
        [Tooltip("装备按钮")]
        [SerializeField] private Button zhuangBeiButton;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 返回按钮
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 返回按钮引用 ═══")]
        [Tooltip("返回按钮组件")]
        [SerializeField] private Button backButtonComponent;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 调试 ═══")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private MainSceneState currentState = MainSceneState.Main;
        
        /// <summary>当前 UI 状态</summary>
        public MainSceneState CurrentState => currentState;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 单例设置
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void Start()
        {
            SetupButtons();
            
            // 初始化为主界面状态
            SwitchToState(MainSceneState.Main);
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置按钮事件
        /// </summary>
        private void SetupButtons()
        {
            // 科技树按钮
            if (keJiButton != null)
            {
                keJiButton.onClick.AddListener(OnKeJiButtonClicked);
            }
            
            // 装备按钮
            if (zhuangBeiButton != null)
            {
                zhuangBeiButton.onClick.AddListener(OnZhuangBeiButtonClicked);
            }
            
            // 返回按钮
            if (backButtonComponent != null)
            {
                backButtonComponent.onClick.AddListener(OnBackButtonClicked);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 科技树按钮点击
        /// </summary>
        private void OnKeJiButtonClicked()
        {
            PlayButtonSound();
            SwitchToState(MainSceneState.KeJi);
            
            if (showDebugInfo)
            {
                Debug.Log("[MainSceneUIManager] 切换到科技树界面");
            }
        }
        
        /// <summary>
        /// 装备按钮点击
        /// </summary>
        private void OnZhuangBeiButtonClicked()
        {
            PlayButtonSound();
            SwitchToState(MainSceneState.ZhuangBei);
            
            if (showDebugInfo)
            {
                Debug.Log("[MainSceneUIManager] 切换到装备界面");
            }
        }
        
        /// <summary>
        /// 返回按钮点击
        /// </summary>
        private void OnBackButtonClicked()
        {
            PlayButtonSound();
            SwitchToState(MainSceneState.Main);
            
            if (showDebugInfo)
            {
                Debug.Log("[MainSceneUIManager] 返回主界面");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态切换
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 切换到指定状态
        /// </summary>
        /// <param name="newState">目标状态</param>
        public void SwitchToState(MainSceneState newState)
        {
            currentState = newState;
            
            switch (newState)
            {
                case MainSceneState.Main:
                    ApplyMainState();
                    break;
                    
                case MainSceneState.KeJi:
                    ApplyKeJiState();
                    break;
                    
                case MainSceneState.ZhuangBei:
                    ApplyZhuangBeiState();
                    break;
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[MainSceneUIManager] 状态切换: {newState}");
            }
        }
        
        /// <summary>
        /// 应用主界面状态
        /// </summary>
        private void ApplyMainState()
        {
            // 面板显示
            SetActive(globalBackground, true);
            SetActive(mainPanel, true);
            SetActive(keJiPanel, false);
            SetActive(zhuangBeiPanel, false);
            
            // 按钮显示
            SetActive(settingButton, true);
            SetActive(backButton, false);
        }
        
        /// <summary>
        /// 应用科技树状态
        /// </summary>
        private void ApplyKeJiState()
        {
            // 面板显示
            SetActive(globalBackground, false);  // KeJiPanel 有自己的背景
            SetActive(mainPanel, false);
            SetActive(keJiPanel, true);
            SetActive(zhuangBeiPanel, false);
            
            // 按钮显示
            SetActive(settingButton, false);
            SetActive(backButton, true);
        }
        
        /// <summary>
        /// 应用装备状态
        /// </summary>
        private void ApplyZhuangBeiState()
        {
            // 面板显示
            SetActive(globalBackground, true);   // ZhuangBeiPanel 透出全局背景
            SetActive(mainPanel, false);
            SetActive(keJiPanel, false);
            SetActive(zhuangBeiPanel, true);
            
            // 按钮显示
            SetActive(settingButton, false);
            SetActive(backButton, true);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示科技树界面（供外部调用）
        /// </summary>
        public void ShowKeJiPanel()
        {
            SwitchToState(MainSceneState.KeJi);
        }
        
        /// <summary>
        /// 显示装备界面（供外部调用）
        /// </summary>
        public void ShowZhuangBeiPanel()
        {
            SwitchToState(MainSceneState.ZhuangBei);
        }
        
        /// <summary>
        /// 返回主界面（供外部调用）
        /// </summary>
        public void BackToMain()
        {
            SwitchToState(MainSceneState.Main);
        }
        
        /// <summary>
        /// 检查是否在主界面
        /// </summary>
        public bool IsInMainState()
        {
            return currentState == MainSceneState.Main;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 辅助方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 安全设置 GameObject 显示状态
        /// </summary>
        private void SetActive(GameObject obj, bool active)
        {
            if (obj != null)
            {
                obj.SetActive(active);
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
    }
}