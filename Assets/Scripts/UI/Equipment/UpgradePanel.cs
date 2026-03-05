// ============================================================
// UpgradePanel.cs
// 文件位置: Assets/Scripts/UI/Equipment/UpgradePanel.cs
//
// UI 层级：
//   UpdatePanel
//   ├─ Background              ← 点击空白区域关闭
//   ├─ Panel
//   │   ├─ ItemFrame
//   │   │   └─ Icon            ← itemIcon
//   │   ├─ Text(TMP)           ← nameText（如"棱镜核心"）
//   │   └─ Info
//   │       ├─ Text(TMP)       ← leftLevelText  "Lv.1"（当前等级）
//   │       ├─ Text(TMP)       ← leftStatsText  当前等级属性
//   │       ├─ Text(TMP)       ← rightLevelText "Lv.2"（升级后等级）
//   │       └─ Text(TMP)       ← rightStatsText 升级后等级属性（绿色）
//   ├─ Close_Button            ← closeButton
//   └─ Update_Button           ← upgradeButton
//       ├─ goldCostText        ← 金币图标旁边的数量文本（如 "50"）
//       └─ blueprintCostText   ← 图纸图标旁边的数量文本（如 "100"）
//
// 升级逻辑：
//   点击升级后，左侧 Lv.N → Lv.N+1，右侧 Lv.N+1 → Lv.N+2（始终右比左多1）
//   满级时右侧显示"MAX"，升级按钮置灰
//   金币或图纸不足时升级按钮置灰
//   支持长按连续升级
//
// 颜色规则：
//   右侧（升级后）数值 > 左侧（当前），右侧显绿 + ▲；满级时右侧显灰
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using LightVsDecay.Data.SO;
using LightVsDecay.Data.Runtime;
using LightVsDecay.Logic;
using LightVsDecay.Logic.Equipment;

namespace LightVsDecay.UI.Equipment
{
    public class UpgradePanel : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("Background — 点击关闭（挂 Button 组件）")]
        [SerializeField] private Button backgroundButton;

        [Header("ItemFrame/Icon")]
        [SerializeField] private Image itemIcon;

        [Header("物品名称")]
        [SerializeField] private TextMeshProUGUI nameText;

        [Header("Info — 左列（当前等级）")]
        [SerializeField] private TextMeshProUGUI leftLevelText;   // "Lv.1"
        [SerializeField] private TextMeshProUGUI leftStatsText;   // 当前等级属性

        [Header("Info — 右列（升级后等级）")]
        [SerializeField] private TextMeshProUGUI rightLevelText;  // "Lv.2" / "MAX"
        [SerializeField] private TextMeshProUGUI rightStatsText;  // 升级后属性（绿色）

        [Header("升级按钮及消耗文本")]
        [SerializeField] private Button          upgradeButton;

        [Tooltip("升级按钮内：金币消耗数量文本（图标另做在 UI 里）")]
        [SerializeField] private TextMeshProUGUI goldCostText;

        [Tooltip("升级按钮内：图纸消耗数量文本")]
        [SerializeField] private TextMeshProUGUI blueprintCostText;

        [Header("关闭按钮")]
        [SerializeField] private Button closeButton;

        [Header("长按连升")]
        [SerializeField] private float longPressDelay     = 0.5f;
        [SerializeField] private float autoUpgradeInterval = 0.1f;

        // ── 颜色 ──────────────────────────────────────────────
        private const string COLOR_UP    = "#00FF00";  // 升级后数值更高 — 绿
        private const string COLOR_MAX   = "#888888";  // 满级 — 灰
        private const string COLOR_WHITE = "#FFFFFF";  // 当前等级 — 白

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private EquipmentSlotType _slot;
        private EquipmentPanel    _parent;
        private Coroutine         _longPress;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            if (closeButton      != null) closeButton.onClick.AddListener(Close);
            if (backgroundButton != null) backgroundButton.onClick.AddListener(Close);

            // 升级按钮：点击 + 长按
            if (upgradeButton != null)
            {
                var et = upgradeButton.gameObject.GetComponent<EventTrigger>()
                         ?? upgradeButton.gameObject.AddComponent<EventTrigger>();
                AddTrigger(et, EventTriggerType.PointerDown, _ => OnUpgradeDown());
                AddTrigger(et, EventTriggerType.PointerUp,   _ => OnUpgradeUp());
                AddTrigger(et, EventTriggerType.PointerExit, _ => OnUpgradeUp());
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公开接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void Setup(EquipmentSlotType slot, EquipmentPanel parent)
        {
            _slot   = slot;
            _parent = parent;
            Refresh();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 刷新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Refresh()
        {
            var mgr      = EquipmentManager.Instance;
            if (mgr == null) { Close(); return; }

            var slotData = mgr.GetSlot(_slot);
            var data     = mgr.GetSlotData(_slot);
            if (slotData.IsEmpty || data == null) { Close(); return; }

            int  curLv = slotData.upgradeLevel;
            int  maxLv = data.maxLevel;
            bool isMax = curLv >= maxLv;
            int  nxtLv = Mathf.Min(curLv + 1, maxLv);

            // ── 图标 / 名称 ──
            if (itemIcon != null && data.icon != null) itemIcon.sprite = data.icon;
            if (nameText != null) nameText.text = data.displayName;

            // ── 左列：当前等级 ──
            if (leftLevelText != null)
                leftLevelText.text = $"<color={COLOR_WHITE}>Lv.{curLv}</color>";

            var curStats = data.GetStatsAtLevel(curLv);
            if (leftStatsText != null)
                leftStatsText.text = BuildStatsText(curStats, COLOR_WHITE, showSymbol: false);

            // ── 右列：升级后等级 ──
            if (isMax)
            {
                if (rightLevelText != null) rightLevelText.text = $"<color={COLOR_MAX}>MAX</color>";
                if (rightStatsText != null) rightStatsText.text = $"<color={COLOR_MAX}>已满级</color>";
            }
            else
            {
                if (rightLevelText != null)
                    rightLevelText.text = $"<color={COLOR_UP}>Lv.{nxtLv}</color>";

                var nxtStats = data.GetStatsAtLevel(nxtLv);
                if (rightStatsText != null)
                    rightStatsText.text = BuildStatsDiffText(curStats, nxtStats);
            }

            // ── 消耗显示 ──
            if (!isMax)
            {
                int goldCost = data.GetLevelUpGoldCost(curLv);
                int bpCost   = data.GetLevelUpBlueprintCost(curLv);
                if (goldCostText      != null) goldCostText.text      = goldCost.ToString();
                if (blueprintCostText != null) blueprintCostText.text = bpCost.ToString();
            }
            else
            {
                if (goldCostText      != null) goldCostText.text      = "—";
                if (blueprintCostText != null) blueprintCostText.text = "—";
            }

            // ── 升级按钮可交互状态 ──
            RefreshButtonState(data, curLv, isMax);
        }

        private void RefreshButtonState(EquipmentData data, int curLv, bool isMax)
        {
            if (upgradeButton == null) return;

            if (isMax)
            {
                upgradeButton.interactable = false;
                return;
            }

            bool canAfford = CanAffordUpgrade(data, curLv);
            upgradeButton.interactable = canAfford;
        }

        private bool CanAffordUpgrade(EquipmentData data, int curLv)
        {
            if (ProgressManager.Instance == null) return false;
            int goldCost = data.GetLevelUpGoldCost(curLv);
            int bpCost   = data.GetLevelUpBlueprintCost(curLv);
            return ProgressManager.Instance.GoldCoins >= goldCost
                   && EquipmentManager.Instance.Blueprints >= bpCost;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 升级按钮
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnUpgradeDown()
        {
            TryUpgrade();
            if (_longPress != null) StopCoroutine(_longPress);
            _longPress = StartCoroutine(LongPressRoutine());
        }

        private void OnUpgradeUp()
        {
            if (_longPress != null) { StopCoroutine(_longPress); _longPress = null; }
        }

        private IEnumerator LongPressRoutine()
        {
            yield return new WaitForSecondsRealtime(longPressDelay);
            while (true)
            {
                if (!TryUpgrade()) yield break;
                yield return new WaitForSecondsRealtime(autoUpgradeInterval);
            }
        }

        private bool TryUpgrade()
        {
            var result = EquipmentManager.Instance?.LevelUpSlot(_slot) ?? LevelUpResult.NotFound;
            if (result == LevelUpResult.Success)
            {
                Refresh();
                return true;
            }
            // 不足时刷新按钮状态（确保置灰）
            if (result == LevelUpResult.InsufficientGold
                || result == LevelUpResult.InsufficientBlueprints)
            {
                if (upgradeButton != null) upgradeButton.interactable = false;
            }
            return false;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 关闭
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Close()
        {
            OnUpgradeUp();
            _parent?.OnSubPanelClosed();
            gameObject.SetActive(false);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性文本构建
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>纯色文本（左列用）</summary>
        private string BuildStatsText(EquipmentStats s, string color, bool showSymbol)
        {
            var sb = new System.Text.StringBuilder();
            if (s.attackBonus  != 0) sb.AppendLine($"攻击力：<color={color}>+{s.attackBonus}</color>");
            if (s.hpBonus      != 0) sb.AppendLine($"生命值：<color={color}>+{s.hpBonus}</color>");
            if (s.shieldBonus  != 0) sb.AppendLine($"护盾值：<color={color}>+{s.shieldBonus}</color>");
            if (s.critBonus    > 0f) sb.AppendLine($"暴击率：<color={color}>+{s.critBonus * 100:F1}%</color>");
            if (s.chargeBonus  > 0f) sb.AppendLine($"充能效率：<color={color}>+{s.chargeBonus * 100:F1}%</color>");
            return sb.Length > 0 ? sb.ToString().TrimEnd() : $"<color={color}>无属性</color>";
        }

        /// <summary>右列：升级后属性，与当前对比，上涨值用绿色 + ▲</summary>
        private string BuildStatsDiffText(EquipmentStats cur, EquipmentStats nxt)
        {
            var sb = new System.Text.StringBuilder();
            AppendUpgrade(sb, "攻击力",  cur.attackBonus,        nxt.attackBonus,        "+{0}",    "+{0}");
            AppendUpgrade(sb, "生命值",  cur.hpBonus,            nxt.hpBonus,            "+{0}",    "+{0}");
            AppendUpgrade(sb, "护盾值",  cur.shieldBonus,        nxt.shieldBonus,        "+{0}",    "+{0}");
            AppendUpgradeF(sb,"暴击率",  cur.critBonus   * 100f, nxt.critBonus   * 100f, "+{0:F1}%","+{0:F1}%");
            AppendUpgradeF(sb,"充能效率",cur.chargeBonus * 100f, nxt.chargeBonus * 100f, "+{0:F1}%","+{0:F1}%");
            return sb.Length > 0 ? sb.ToString().TrimEnd() : $"<color={COLOR_UP}>无变化</color>";
        }

        private void AppendUpgrade(
            System.Text.StringBuilder sb,
            string label, int curVal, int nxtVal,
            string curFmt, string nxtFmt)
        {
            if (curVal == 0 && nxtVal == 0) return;
            bool increased = nxtVal > curVal;
            string color   = increased ? COLOR_UP : COLOR_WHITE;
            string symbol  = increased ? " ▲" : "";
            string val     = string.Format(nxtFmt, nxtVal);
            sb.AppendLine($"{label}：<color={color}>{val}{symbol}</color>");
        }

        private void AppendUpgradeF(
            System.Text.StringBuilder sb,
            string label, float curVal, float nxtVal,
            string curFmt, string nxtFmt)
        {
            if (curVal < 0.001f && nxtVal < 0.001f) return;
            bool increased = nxtVal > curVal + 0.001f;
            string color   = increased ? COLOR_UP : COLOR_WHITE;
            string symbol  = increased ? " ▲" : "";
            string val     = string.Format(nxtFmt, nxtVal);
            sb.AppendLine($"{label}：<color={color}>{val}{symbol}</color>");
        }

        // ── 工具 ──────────────────────────────────────────────

        private static void AddTrigger(
            EventTrigger et, EventTriggerType type,
            UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(action);
            et.triggers.Add(e);
        }
    }
}