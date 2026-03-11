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
using LightVsDecay.Audio;
using LightVsDecay.Logic;
using LightVsDecay.Logic.Equipment;

namespace LightVsDecay.UI
{
    public class TopAreaController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public static TopAreaController Instance { get; private set; }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 资源文字
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 资源文字 ═══")]
        [SerializeField] private TextMeshProUGUI gemText;
        [SerializeField] private TextMeshProUGUI goldCoinText;
        [SerializeField] private TextMeshProUGUI energyText;
        [Tooltip("图纸数量文本 ← 必须拖入！")]
        [SerializeField] private TextMeshProUGUI blueprintText;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 导航按钮
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 导航按钮 ═══")]
        [Tooltip("设置按钮（主界面时显示）")]
        [SerializeField] private Button settingButton;

        [Tooltip("返回按钮（科技树/装备面板时显示）")]
        [SerializeField] private Button backButton;

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
        }

        private void OnDisable()
        {
            EquipmentManager.OnBlueprintsChanged -= OnBlueprintsChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
                Debug.Log($"[TopAreaController] NavState={state} " +
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
                Debug.LogWarning("[TopAreaController] settingsPanel 未绑定！");

            if (showDebugInfo)
                Debug.Log("[TopAreaController] 打开设置面板");
        }

        private void OnBackClicked()
        {
            PlayButtonSound();

            MainSceneUIManager.Instance?.BackToMain();

            if (showDebugInfo)
                Debug.Log("[TopAreaController] 返回主界面");
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
                Debug.Log("[TopAreaController] Refresh 完成");
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
                Debug.Log($"[TopAreaController] 图纸更新: {newCount}");
        }

        private void PlayButtonSound()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayButtonClick();
        }

        /// <summary>静态便捷刷新（无单例引用时调用）</summary>
        public static void RefreshIfExists() => Instance?.Refresh();
    }
}