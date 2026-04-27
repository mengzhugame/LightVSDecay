// ============================================================
// TopBarTipsPanel.cs
// 文件位置: Assets/Scripts/UI/Panels/TopBarTipsPanel.cs
// 用途：顶部栏资源提示面板（体力 / 金币 / 图纸）
//
// 三种模式通过 Show(TopBarResourceType) 切换：
//   Energy    → 体力耗尽，显示恢复倒计时 + 广告换体力
//   Gold      → 获取金币，显示广告换金币说明
//   Blueprint → 获取图纸，显示广告换图纸说明
//
// UI 层级（Canvas 下，默认 SetActive false）：
//   TopBarTipsPanel
//   ├── Background (Button → 点击关闭)
//   └── PanelBody
//       ├── TitleText          (TMP - 标题)
//       ├── InfoBox            (整个信息框 GameObject)
//       │   ├── InfoMainText   (TMP - 主要说明文字)
//       │   ├── CountdownGroup (GameObject - 仅体力模式显示)
//       │   │   └── CountdownText (TMP - "29:45")
//       ├── AdButton           (Button)
//       │   ├── AdVideoIcon    (Image - 视频图标)
//       │   ├── AdResourceIcon (Image - 资源图标：⚡/🪙/📋)
//       │   └── AdButtonText   (TMP - "+2" / "+500" / "+3")
//       ├── AdCountText        (TMP - "今日已观看 2/5 次")
//       └── CloseHintText      (TMP - "点击空白处 关闭界面")
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightVsDecay.Ads;
using LightVsDecay.Logic;
using LightVsDecay.Logic.Equipment;
using LightVsDecay.Core;
using LightVsDecay.UI;

namespace LightVsDecay.UI.Panels
{
    /// <summary>面板资源类型枚举</summary>
    public enum TopBarResourceType
    {
        Energy,     // 体力
        Gold,       // 金币
        Blueprint   // 图纸
    }

    public class TopBarTipsPanel : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 核心组件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 关闭按钮 ═══")]
        [Tooltip("点击背景关闭（整个遮罩区）")]
        [SerializeField] private Button backgroundButton;
        [Tooltip("右上角 X 关闭按钮（可选）")]
        [SerializeField] private Button closeButton;

        [Header("═══ 标题 ═══")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("═══ 信息框 ═══")]
        [Tooltip("主要说明文字（体力模式=倒计时前缀；金币/图纸模式=广告说明）")]
        [SerializeField] private TextMeshProUGUI infoMainText;
        [Tooltip("倒计时数字，如 '29:45'")]
        [SerializeField] private TextMeshProUGUI countdownText;

        [Header("═══ 广告按钮 ═══")]
        [SerializeField] private Button          adButton;
        [Tooltip("按钮上的视频播放图标（置灰时一起变色）")]
        [SerializeField] private Image adVideoIcon;
        [Tooltip("按钮上的资源图标（随模式切换 Sprite）")]
        [SerializeField] private Image           adResourceIcon;
        [Tooltip("按钮上的数量文字，如 '+2' / '+500'")]
        [SerializeField] private TextMeshProUGUI adButtonText;
        [Tooltip("按钮禁用时的置灰颜色")]
        [SerializeField] private Color adDisabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        [Tooltip("按钮启用时图标和文字的正常颜色")]
        [SerializeField] private Color adNormalColor = Color.white;
        [Header("═══ 今日次数 & 提示 ═══")]
        [Tooltip("今日广告次数，如 '今日已观看 2/5 次'")]
        [SerializeField] private TextMeshProUGUI adCountText;
        [Tooltip("底部关闭提示，如 '点击空白处 关闭界面'")]
        [SerializeField] private TextMeshProUGUI closeHintText;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 资源图标 Sprite（各模式切换）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 资源图标 ═══")]
        [Tooltip("体力图标（闪电）")]
        [SerializeField] private Sprite energyIcon;
        [Tooltip("金币图标")]
        [SerializeField] private Sprite goldIcon;
        [Tooltip("图纸图标")]
        [SerializeField] private Sprite blueprintIcon;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 文案配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 体力模式文案 ═══")]
        [SerializeField] private string energyTitle         = "体力耗尽";
        [SerializeField] private string energyInfoFull      = "体力已满，无需等待！";
        [SerializeField] private string energyInfoRecover   = "距离下次体力恢复需要";

        [Header("═══ 金币模式文案 ═══")]
        [SerializeField] private string goldTitle           = "获取金币";
        [SerializeField] private string goldInfo            = "金币告急？\n观看短视频广告，立即领取金币补给！";

        [Header("═══ 图纸模式文案 ═══")]
        [SerializeField] private string blueprintTitle      = "获取图纸";
        [SerializeField] private string blueprintInfo       = "图纸是强化装备的关键！\n观看短视频广告，立即获取额外图纸！";

        [Header("═══ 通用文案 ═══")]
        [Tooltip("次数耗尽时 InfoMainText 显示的说明文字")]
        [SerializeField] private string adExhaustedInfo = "今日广告次数已达上限\n明天再来领取吧！";
        [Tooltip("底部关闭提示文字")]
        [SerializeField] private string closeHint           = "点击空白处 关闭界面";

        [Header("═══ 调试 ═══")]
        [SerializeField] private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private bool                _isVisible    = false;
        private TopBarResourceType  _currentMode  = TopBarResourceType.Energy;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            if (closeButton      != null) closeButton.onClick.AddListener(Hide);
            if (backgroundButton != null) backgroundButton.onClick.AddListener(Hide);
            if (adButton         != null) adButton.onClick.AddListener(OnAdButtonClicked);
            UIButtonCommonHelper.Ensure(adButton);

            if (adCountText == adButtonText)
                adCountText = null;

            if (closeHintText    != null) closeHintText.text = closeHint;
        }

        private void OnEnable()
        {
            ProgressManager.OnEnergyChanged += OnResourceChanged;
            ProgressManager.OnGoldCoinsChanged += OnGoldChanged;
            EquipmentManager.OnBlueprintsChanged += OnBlueprintsChanged;
        }

        private void OnDisable()
        {
            ProgressManager.OnEnergyChanged -= OnResourceChanged;
            ProgressManager.OnGoldCoinsChanged -= OnGoldChanged;
            EquipmentManager.OnBlueprintsChanged -= OnBlueprintsChanged;
        }

        private void Update()
        {
            // 体力模式下每帧刷新倒计时
            if (_isVisible && _currentMode == TopBarResourceType.Energy)
                RefreshCountdown();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 打开面板并切换到对应资源模式
        /// </summary>
        public void Show(TopBarResourceType mode = TopBarResourceType.Energy)
        {
            _currentMode = mode;
            _isVisible   = true;
            gameObject.SetActive(true);
            // ★ 清除 EventSystem 当前选中，防止首次点击被吞
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            RefreshAll();

            if (showDebugInfo)
                GameLogger.Log($"[TopBarTipsPanel] 打开，模式: {mode}");
        }

        public void Hide()
        {
            _isVisible = false;
            gameObject.SetActive(false);

            if (showDebugInfo)
                GameLogger.Log("[TopBarTipsPanel] 关闭");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 刷新逻辑
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void RefreshAll()
        {
            switch (_currentMode)
            {
                case TopBarResourceType.Energy:    RefreshEnergyMode();    break;
                case TopBarResourceType.Gold:      RefreshGoldMode();      break;
                case TopBarResourceType.Blueprint: RefreshBlueprintMode(); break;
            }
        }

        // ── 体力模式 ──────────────────────────────────────────

        private void RefreshEnergyMode()
        {
            var pm = ProgressManager.Instance;
            var adManager = AdManager.Instance;

            SetTitle(energyTitle);
            SetAdIcon(energyIcon);

            bool isFull   = pm == null || pm.IsEnergyFull;
            bool canWatch = pm != null && adManager != null && adManager.CanWatchAd(AdType.EnergyTopUp);

            // 倒计时：满载或次数耗尽时隐藏
            SetCountdownGroupVisible(!isFull);

            // InfoMainText：满载 / 可看 / 已耗尽 三种状态
            if (isFull)
                SetInfoText(energyInfoFull);
            else if (canWatch)
                SetInfoText(energyInfoRecover);
            else
                SetInfoText(adExhaustedInfo);

            RefreshCountdown();

            SetAdButton(canWatch, $"+{pm?.AdEnergyReward ?? 2}", adExhaustedInfo);
            SetAdCount(adManager?.GetDailyCount(AdType.EnergyTopUp) ?? 0, adManager?.GetDailyLimit(AdType.EnergyTopUp) ?? 0);
        }

        private void RefreshCountdown()
        {
            if (countdownText == null) return;
            var pm = ProgressManager.Instance;
            if (pm == null || pm.IsEnergyFull)
            {
                countdownText.gameObject.SetActive(false);  // ← 改这里
                return;
            }

            float secs    = pm.EnergyRecoverySecondsRemaining;
            int   minutes = Mathf.FloorToInt(secs / 60f);
            int   seconds = Mathf.FloorToInt(secs % 60f);
            countdownText.text = $"<color=#16DCDD>{minutes:D2}:{seconds:D2}</color>";
        }

        // ── 金币模式 ──────────────────────────────────────────

        private void RefreshGoldMode()
        {
            var pm = ProgressManager.Instance;
            var adManager = AdManager.Instance;

            SetTitle(goldTitle);
            SetAdIcon(goldIcon);
            SetCountdownGroupVisible(false);

            bool canWatch = pm != null && adManager != null && adManager.CanWatchAd(AdType.GoldTopUp);
            SetInfoText(canWatch ? goldInfo : adExhaustedInfo);
            SetAdButton(canWatch, $"+{pm?.AdGoldReward ?? 500}", adExhaustedInfo);
            SetAdCount(adManager?.GetDailyCount(AdType.GoldTopUp) ?? 0, adManager?.GetDailyLimit(AdType.GoldTopUp) ?? 0);
        }

        // ── 图纸模式 ──────────────────────────────────────────

        private void RefreshBlueprintMode()
        {
            var pm = ProgressManager.Instance;
            var adManager = AdManager.Instance;

            SetTitle(blueprintTitle);
            SetAdIcon(blueprintIcon);
            SetCountdownGroupVisible(false);

            bool canWatch = pm != null && adManager != null && adManager.CanWatchAd(AdType.BlueprintTopUp);
            SetInfoText(canWatch ? blueprintInfo : adExhaustedInfo);
            SetAdButton(canWatch, $"+{pm?.AdBlueprintReward ?? 3}", adExhaustedInfo);
            SetAdCount(adManager?.GetDailyCount(AdType.BlueprintTopUp) ?? 0, adManager?.GetDailyLimit(AdType.BlueprintTopUp) ?? 0);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 辅助 Set 方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void SetTitle(string text)
        {
            if (titleText != null) titleText.text = text;
        }

        private void SetAdIcon(Sprite sprite)
        {
            if (adResourceIcon != null && sprite != null)
                adResourceIcon.sprite = sprite;
        }

        private void SetCountdownGroupVisible(bool visible)
        {
            if (countdownText != null)
                countdownText.gameObject.SetActive(visible);
        }

        private void SetInfoText(string text)
        {
            if (infoMainText != null)
                infoMainText.text = string.IsNullOrEmpty(text) ? string.Empty : text;
        }

        private void SetAdButton(bool interactable, string buttonAmountText, string exhaustedInfoText = "")
        {
            // 按钮可交互状态
            UIButtonCommonHelper.SetInteractable(adButton, interactable);

            // 按钮内文字（仅显示数量，如 "+2"）
            if (adButtonText != null)
                adButtonText.text = buttonAmountText;

            // 按钮内图标和文字颜色置灰
            UIButtonCommonHelper.Sync(adButton);

            // 次数耗尽时，InfoMainText 切换为提示文案
            if (!interactable && infoMainText != null && !string.IsNullOrEmpty(exhaustedInfoText))
                infoMainText.text = exhaustedInfoText;
        }

        private void SetAdCount(int current, int total)
        {
            if (adCountText != null)
                adCountText.text = $"今日已观看 {current}/{total} 次";
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 广告按钮点击
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnAdButtonClicked()
        {
            var pm = ProgressManager.Instance;
            var adManager = AdManager.Instance;
            if (pm == null || adManager == null) return;

            // 播放期间禁用按钮，防止重复点击
            UIButtonCommonHelper.SetInteractable(adButton, false);

            switch (_currentMode)
            {
                case TopBarResourceType.Energy:
                    AnalyticsManager.LogScene(AnalyticsSceneIds.AdClickEnergy);
                    adManager.ShowRewardedAd(AdType.EnergyTopUp,
                        onSuccess: () => { pm.GrantAdEnergyReward(); Hide(); },
                        onFail: RefreshAll);
                    break;
                case TopBarResourceType.Gold:
                    AnalyticsManager.LogScene(AnalyticsSceneIds.AdClickGold);
                    adManager.ShowRewardedAd(AdType.GoldTopUp,
                        onSuccess: () => { pm.GrantAdGoldReward(); RefreshAll(); },
                        onFail: RefreshAll);
                    break;
                case TopBarResourceType.Blueprint:
                    AnalyticsManager.LogScene(AnalyticsSceneIds.AdClickBlueprint);
                    adManager.ShowRewardedAd(AdType.BlueprintTopUp,
                        onSuccess: () => { pm.GrantAdBlueprintReward(); RefreshAll(); },
                        onFail: RefreshAll);
                    break;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件响应
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnResourceChanged(int current, int max)
        {
            if (!_isVisible || _currentMode != TopBarResourceType.Energy) return;
            RefreshEnergyMode();
        }

        private void OnGoldChanged(int amount)
        {
            if (!_isVisible || _currentMode != TopBarResourceType.Gold) return;
            RefreshGoldMode();
        }

        private void OnBlueprintsChanged(int amount)
        {
            if (!_isVisible || _currentMode != TopBarResourceType.Blueprint) return;
            RefreshBlueprintMode();
        }
    }
}
