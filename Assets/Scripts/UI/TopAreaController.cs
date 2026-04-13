// ============================================================
// TopAreaController.cs
// 文件位置: Assets/Scripts/UI/TopAreaController.cs
//
// 职责：主场景顶部导航栏（TopArea）的完整控制器
//
//   ① 资源显示：宝石 / 金币 / 能量 / 图纸，实时响应 Manager 事件
//   ② 设置按钮：点击打开 SettingsPanel（主界面状态时显示）
//   ③ 返回按钮：点击调用 MainSceneUIManager.BackToMain()（子面板时显示）
//   ④ 按钮显隐：由 MainSceneUIManager 调用 SetNavState() 驱动
//
// 挂载位置：TopArea GameObject（独立于 MainPanel，不随面板切换隐藏）
//
// UI 层级建议（Canvas → TopArea）：
//   TopArea              ← 本脚本挂这里
//   ├── GemText          (TMP)
//   ├── GoldCoinText     (TMP)
//   ├── EnergyText       (TMP)
//   ├── BlueprintText    (TMP)  ← 新增，记得拖入
//   ├── SettingButton    (Button)
//   └── BackButton       (Button)
//
// MainMenuPanel 需删除：settingButton / gemText / goldCoinText / energyText 字段
// MainSceneUIManager 需删除：settingButton / backButton GameObject 字段，
//   ApplyXxxState() 里的 SetActive(settingButton/backButton) 改为调用
//   TopAreaController.Instance.SetNavState(state)
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using LightVsDecay.Audio;
using LightVsDecay.Logic;
using LightVsDecay.Logic.Equipment;
using LightVsDecay.UI.Panels;
using LightVsDecay.Core;

namespace LightVsDecay.UI
{
    public class TopAreaController : MonoBehaviour
    {
        public static event Action BackClicked;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public static TopAreaController Instance { get; private set; }
        public RectTransform BackButtonRect => backButton != null ? backButton.GetComponent<RectTransform>() : null;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 资源文字
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 资源文字 ═══")]
        [SerializeField] private TextMeshProUGUI gemText;
        [SerializeField] private TextMeshProUGUI goldCoinText;
        [SerializeField] private TextMeshProUGUI energyText;
        [Tooltip("图纸数量文本 ← 必须拖入！")]
        [SerializeField] private TextMeshProUGUI blueprintText;
        [Tooltip("体力区域点击按钮（套在体力文本外的 Button）")]
        [SerializeField] private Button energyButton;
        [Tooltip("金币区域点击按钮")]
        [SerializeField] private Button goldCoinButton;
        [Tooltip("图纸区域点击按钮")]
        [SerializeField] private Button blueprintButton;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 导航按钮
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 导航按钮 ═══")]
        [Tooltip("设置按钮（主界面时显示）")]
        [SerializeField] private Button settingButton;

        [Tooltip("返回按钮（科技树/装备面板时显示）")]
        [SerializeField] private Button backButton;
        [Tooltip("资源提示面板（体力/金币/图纸）")]
        [SerializeField] private TopBarTipsPanel topBarTipsPanel;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 面板引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 面板引用 ═══")]
        [Tooltip("点击设置按钮后打开的 SettingsPanel")]
        [SerializeField] private SettingsPanel settingsPanel;

        [Header("═══ 调试 ═══")]
        [SerializeField] private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            SetupButtons();
        }

        private void Start()
        {
            // 初始状态：主界面 → 设置显示，返回隐藏
            SetNavState(MainSceneState.Main);
            Refresh();
        }

        private void OnEnable()
        {
            EquipmentManager.OnBlueprintsChanged += OnBlueprintsChanged;
            ProgressManager.OnGoldCoinsChanged   += OnGoldCoinsChanged;
            ProgressManager.OnEnergyChanged += OnEnergyChanged;
        }

        private void OnDisable()
        {
            EquipmentManager.OnBlueprintsChanged -= OnBlueprintsChanged;
            ProgressManager.OnGoldCoinsChanged   -= OnGoldCoinsChanged;
            ProgressManager.OnEnergyChanged -= OnEnergyChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ProgressManager.OnGoldCoinsChanged   -= OnGoldCoinsChanged;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void SetupButtons()
        {
            if (settingButton != null)
                settingButton.onClick.AddListener(OnSettingClicked);

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
            if (energyButton    != null) energyButton.onClick.AddListener(OnEnergyClicked);
            if (goldCoinButton  != null) goldCoinButton.onClick.AddListener(OnGoldClicked);
            if (blueprintButton != null) blueprintButton.onClick.AddListener(OnBlueprintClicked);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 导航状态切换（由 MainSceneUIManager 每次 SwitchToState 后调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 同步顶部按钮显隐到当前场景状态。
        /// 主界面：设置按钮可见，返回按钮隐藏。
        /// 科技树/装备：设置按钮隐藏，返回按钮可见。
        /// </summary>
        public void SetNavState(MainSceneState state)
        {
            bool isMain = state == MainSceneState.Main;

            if (settingButton != null) settingButton.gameObject.SetActive(isMain);
            if (backButton    != null) backButton.gameObject.SetActive(!isMain);

            if (showDebugInfo)
                GameLogger.Log($"[TopAreaController] NavState={state} " +
                          $"Setting={isMain} Back={!isMain}");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮点击回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnSettingClicked()
        {
            PlayButtonSound();

            if (settingsPanel != null)
                settingsPanel.Show(false);   // false = 主界面，不显示重启按钮
            else
                GameLogger.LogWarning("[TopAreaController] settingsPanel 未绑定！");

            if (showDebugInfo)
                GameLogger.Log("[TopAreaController] 打开设置面板");
        }

        private void OnBackClicked()
        {
            PlayButtonSound();

            MainSceneUIManager.Instance?.BackToMain();
            BackClicked?.Invoke();

            if (showDebugInfo)
                GameLogger.Log("[TopAreaController] 返回主界面");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 资源刷新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>全量刷新所有资源 UI（从 Manager 读取最新值）</summary>
        public void Refresh()
        {
            RefreshCurrency();
            RefreshBlueprints();

            if (showDebugInfo)
                GameLogger.Log("[TopAreaController] Refresh 完成");
        }

        private void RefreshCurrency()
        {
            int gems = 0, gold = 0, energy = 0, maxEnergy = 5;

            if (ProgressManager.Instance != null)
            {
                gems      = ProgressManager.Instance.Gems;
                gold      = ProgressManager.Instance.GoldCoins;
                energy    = ProgressManager.Instance.Energy;
                maxEnergy = ProgressManager.Instance.MaxEnergy;
            }
            else
            {
                gems      = PlayerPrefs.GetInt("PlayerGems",      0);
                gold      = PlayerPrefs.GetInt("PlayerGoldCoins", 0);
                energy    = PlayerPrefs.GetInt("PlayerEnergy",    5);
            }

            if (gemText      != null) gemText.text     = gems.ToString();
            if (goldCoinText != null) goldCoinText.text = gold.ToString();
            if (energyText   != null) energyText.text   = $"{energy}/{maxEnergy}";
            if (energyText != null) energyText.text = $"{energy}/{maxEnergy}";
        }

        private void RefreshBlueprints()
        {
            int bp = EquipmentManager.Instance != null
                ? EquipmentManager.Instance.Blueprints
                : PlayerPrefs.GetInt("Equip_BP", 0);

            if (blueprintText != null)
                blueprintText.text = bp.ToString();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>图纸数量变化时实时刷新（EquipmentManager 事件驱动）</summary>
        private void OnBlueprintsChanged(int newCount)
        {
            if (blueprintText != null)
                blueprintText.text = newCount.ToString();

            if (showDebugInfo)
                GameLogger.Log($"[TopAreaController] 图纸更新: {newCount}");
        }
        /// <summary>局外金币变化时实时刷新（ProgressManager 事件驱动）</summary>
        private void OnGoldCoinsChanged(int newAmount)
        {
            if (goldCoinText != null)
                goldCoinText.text = newAmount.ToString();

            if (showDebugInfo)
                GameLogger.Log($"[TopAreaController] 金币更新: {newAmount}");
        }
        private void PlayButtonSound()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayButtonClick();
        }

        /// <summary>静态便捷刷新（无单例引用时调用）</summary>
        public static void RefreshIfExists() => Instance?.Refresh();
        
        private void OnEnergyClicked()
        {
            PlayButtonSound();
            topBarTipsPanel?.Show();

            if (showDebugInfo)
                GameLogger.Log("[TopAreaController] 打开提示面板");
        }
        private void OnGoldClicked()
        {
            PlayButtonSound();
            topBarTipsPanel?.Show(TopBarResourceType.Gold);
        }
        private void OnBlueprintClicked()
        {
            PlayButtonSound();
            topBarTipsPanel?.Show(TopBarResourceType.Blueprint);
        }
        private void OnEnergyChanged(int current, int max)
        {
            // 体力变化时实时刷新显示
            if (energyText != null)
                energyText.text = $"{current}/{max}";
        }
    }
}
