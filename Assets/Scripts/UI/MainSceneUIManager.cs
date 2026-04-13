// ============================================================
// MainSceneUIManager.cs
// 文件位置: Assets/Scripts/UI/MainSceneUIManager.cs
//
// 新增功能（v2）：
//   ① Start() 时消费 SystemUnlockManager 的 Pending 通知，
//      通过 TipsPanelController 依次播放解锁提示
//   ② 按解锁状态控制 keJiButton / zhuangBeiButton 的
//      interactable 和图标颜色（未解锁时置灰）
//   ③ 订阅 OnEquipmentSystemUnlocked / OnTechTreeUnlocked 事件，
//      实时刷新按钮状态
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using LightVsDecay.Audio;
using LightVsDecay.Logic;
using LightVsDecay.Core;

namespace LightVsDecay.UI
{
    public enum MainSceneState
    {
        Main,
        KeJi,
        ZhuangBei
    }

    public class MainSceneUIManager : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public static event Action<MainSceneState> OnStateChanged;
        public static MainSceneUIManager Instance { get; private set; }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 面板引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 面板引用 ═══")]
        [SerializeField] private GameObject globalBackground;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject keJiPanel;
        [SerializeField] private GameObject zhuangBeiPanel;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — BottomArea 按钮
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ BottomArea 按钮 ═══")]
        [Tooltip("科技树按钮 Button 组件")]
        [SerializeField] private Button keJiButton;

        [Tooltip("装备按钮 Button 组件")]
        [SerializeField] private Button zhuangBeiButton;

        [Tooltip("科技树按钮上的 TextMeshPro 文字（可选，用于置灰颜色）")]
        [SerializeField] private TextMeshProUGUI keJiButtonText;

        [Tooltip("装备按钮上的 TextMeshPro 文字（可选，用于置灰颜色）")]
        [SerializeField] private TextMeshProUGUI zhuangBeiButtonText;

        [Tooltip("科技树按钮图标 Image（可选）")]
        [SerializeField] private Image keJiButtonIcon;

        [Tooltip("装备按钮图标 Image（可选）")]
        [SerializeField] private Image zhuangBeiButtonIcon;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 返回按钮
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 返回按钮 ═══")]
        [SerializeField] private Button backButtonComponent;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 提示面板
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 提示面板 (TipsPanal) ═══")]
        [Tooltip("挂有 TipsPanelController 脚本的 TipsPanal GameObject")]
        [SerializeField] private TipsPanelController tipsPanel;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 按钮锁定样式
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 按钮锁定样式 ═══")]
        [Tooltip("按钮已解锁时的文字颜色")]
        [SerializeField] private Color unlockedTextColor  = Color.white;

        [Tooltip("按钮未解锁时的文字颜色（置灰）")]
        [SerializeField] private Color lockedTextColor    = new Color(0.45f, 0.45f, 0.45f, 1f);

        [Tooltip("按钮图标已解锁时的颜色")]
        [SerializeField] private Color unlockedIconColor  = Color.white;

        [Tooltip("按钮图标未解锁时的颜色（置灰）")]
        [SerializeField] private Color lockedIconColor    = new Color(0.35f, 0.35f, 0.35f, 0.8f);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 调试 ═══")]
        [SerializeField] private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private MainSceneState currentState = MainSceneState.Main;
        public  MainSceneState CurrentState => currentState;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            SetupButtons();
            SwitchToState(MainSceneState.Main);

            // 初始化按钮解锁状态（根据已有存档置灰/亮起）
            UpdateButtonUnlockState();

            // 消费战斗场景遗留的 Pending 通知，延迟0.5秒再弹（等场景加载完）
            CheckAndShowPendingUnlockTips();
            if (TipsPanelController.Instance != null)
                TipsPanelController.Instance.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            SystemUnlockManager.OnEquipmentSystemUnlocked += OnEquipmentUnlocked;
            SystemUnlockManager.OnTechTreeUnlocked        += OnTechTreeUnlocked;
        }

        private void OnDisable()
        {
            SystemUnlockManager.OnEquipmentSystemUnlocked -= OnEquipmentUnlocked;
            SystemUnlockManager.OnTechTreeUnlocked        -= OnTechTreeUnlocked;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void SetupButtons()
        {
            if (keJiButton       != null) keJiButton.onClick.AddListener(OnKeJiButtonClicked);
            if (zhuangBeiButton  != null) zhuangBeiButton.onClick.AddListener(OnZhuangBeiButtonClicked);
            if (backButtonComponent != null) backButtonComponent.onClick.AddListener(OnBackButtonClicked);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮解锁状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 根据 SystemUnlockManager 的当前状态，
        /// 刷新科技树和装备按钮的交互性与颜色。
        /// </summary>
        private void UpdateButtonUnlockState()
        {
            var mgr = SystemUnlockManager.Instance;
            if (mgr == null) return;

            bool techUnlocked  = mgr.IsTechTreeUnlocked;
            bool equipUnlocked = mgr.IsEquipmentUnlocked;

            // 科技树按钮
            SetButtonLockVisual(keJiButton, keJiButtonText, keJiButtonIcon, techUnlocked);

            // 装备按钮
            SetButtonLockVisual(zhuangBeiButton, zhuangBeiButtonText, zhuangBeiButtonIcon, equipUnlocked);

            if (showDebugInfo)
                GameLogger.Log($"[MainSceneUIManager] 按钮状态刷新: " +
                          $"科技树={techUnlocked}, 装备={equipUnlocked}");
        }

        private void SetButtonLockVisual(Button btn, TextMeshProUGUI label, Image icon, bool unlocked)
        {
            if (btn   != null) btn.interactable  = unlocked;
            if (label != null) label.color       = unlocked ? unlockedTextColor : lockedTextColor;
            if (icon  != null) icon.color        = unlocked ? unlockedIconColor : lockedIconColor;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 待通知消费 + 提示弹窗
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void CheckAndShowPendingUnlockTips()
        {
            var mgr = SystemUnlockManager.Instance;
            if (mgr == null) return;

            var messages = mgr.ConsumePendingNotifications();
            if (messages.Count == 0) return;

            // 用协程延迟 0.6s 再弹，确保场景 UI 全部初始化完毕
            StartCoroutine(DelayedShowTips(messages, 0.6f));
        }

        private System.Collections.IEnumerator DelayedShowTips(
            System.Collections.Generic.List<string> messages, float delay)
        {
            yield return new WaitForSeconds(delay);

            // 优先用 Inspector 绑定的引用，否则找单例
            var panel = tipsPanel != null ? tipsPanel : TipsPanelController.Instance;
            if (panel == null)
            {
                GameLogger.LogWarning("[MainSceneUIManager] TipsPanelController 未找到，无法显示解锁提示！");
                yield break;
            }

            panel.ShowTips(messages);

            if (showDebugInfo)
                GameLogger.Log($"[MainSceneUIManager] 已触发 {messages.Count} 条解锁提示");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调（系统实时解锁时刷新按钮）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnEquipmentUnlocked() => UpdateButtonUnlockState();
        private void OnTechTreeUnlocked()  => UpdateButtonUnlockState();

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnKeJiButtonClicked()
        {
            PlayButtonSound();
            SwitchToState(MainSceneState.KeJi);
            if (showDebugInfo) GameLogger.Log("[MainSceneUIManager] → 科技树界面");
        }

        private void OnZhuangBeiButtonClicked()
        {
            PlayButtonSound();
            SwitchToState(MainSceneState.ZhuangBei);
            if (showDebugInfo) GameLogger.Log("[MainSceneUIManager] → 装备界面");
        }

        private void OnBackButtonClicked()
        {
            PlayButtonSound();
            SwitchToState(MainSceneState.Main);
            if (showDebugInfo) GameLogger.Log("[MainSceneUIManager] → 主界面");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态切换
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void SwitchToState(MainSceneState newState)
        {
            currentState = newState;
            switch (newState)
            {
                case MainSceneState.Main:       ApplyMainState();      break;
                case MainSceneState.KeJi:       ApplyKeJiState();      break;
                case MainSceneState.ZhuangBei:  ApplyZhuangBeiState(); break;
            }
            OnStateChanged?.Invoke(newState);
            if (showDebugInfo) GameLogger.Log($"[MainSceneUIManager] 切换状态: {newState}");
        }

        private void ApplyMainState()
        {
            SetActive(globalBackground, true);
            SetActive(mainPanel,        true);
            SetActive(keJiPanel,        false);
            SetActive(zhuangBeiPanel,   false);
            TopAreaController.Instance?.SetNavState(MainSceneState.Main);
            TopAreaController.RefreshIfExists();   // 返回主界面时顺带刷新资源数据
        }

        private void ApplyKeJiState()
        {
            SetActive(globalBackground, false);
            SetActive(mainPanel,        false);
            SetActive(keJiPanel,        true);
            SetActive(zhuangBeiPanel,   false);
            TopAreaController.Instance?.SetNavState(MainSceneState.KeJi);
        }

        private void ApplyZhuangBeiState()
        {
            SetActive(globalBackground, true);
            SetActive(mainPanel,        false);
            SetActive(keJiPanel,        false);
            SetActive(zhuangBeiPanel,   true);
            TopAreaController.Instance?.SetNavState(MainSceneState.ZhuangBei);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void ShowKeJiPanel()    => SwitchToState(MainSceneState.KeJi);
        public void ShowZhuangBeiPanel() => SwitchToState(MainSceneState.ZhuangBei);
        public void BackToMain()       => SwitchToState(MainSceneState.Main);
        public bool IsInMainState()    => currentState == MainSceneState.Main;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 辅助
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void SetActive(GameObject obj, bool active)
        {
            if (obj != null) obj.SetActive(active);
        }

        private void PlayButtonSound()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayButtonClick();
        }
    }
}
