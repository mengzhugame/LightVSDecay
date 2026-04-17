// ============================================================
// ItemInfoPanel.cs
// 文件位置: Assets/Scripts/UI/Equipment/ItemInfoPanel.cs
//
// UI 层级：
//   InfoPanel
//   ├─ Background              ← 点击空白区域关闭
//   ├─ Panel
//   │   ├─ ItemFrame
//   │   │   └─ Icon            ← iconImage
//   │   ├─ Text(TMP)           ← nameText（如"棱镜核心"）
//   │   └─ Info
//   │       ├─ Text(TMP)       ← leftHeaderText  "未装备"
//   │       ├─ Text(TMP)       ← leftStatsText   当前物品属性（Lv.1）
//   │       ├─ Text(TMP)       ← rightHeaderText "已装备"
//   │       └─ Text(TMP)       ← rightStatsText  已装备槽属性
//   ├─ Close_Button            ← closeButton
//   └─ Equipment_Button        ← equipButton
//
// 颜色规则：
//   左右对比同一属性，数值更高一侧 = 绿色，更低一侧 = 红色，相等 = 白色
//   用 ▲ 表示上涨，▼ 表示下降，= 表示持平
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightVsDecay.Data.Runtime;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Equipment;
using System;
using LightVsDecay.UI;

namespace LightVsDecay.UI.Equipment
{
    public class ItemInfoPanel : MonoBehaviour
    {
        public static event Action<InventoryStack> InfoShown;
        public static event Action<InventoryStack> InfoClosed;
        public static event Action<InventoryStack> Equipped;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置（按 UI 层级对应）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("Background — 点击关闭（挂 Button 组件）")]
        [SerializeField] private Button backgroundButton;

        [Header("ItemFrame/Icon — 物品图标")]
        [SerializeField] private Image itemIcon;

        [Header("物品名称文本")]
        [SerializeField] private TextMeshProUGUI nameText;

        [Header("Info — 左列（未装备/此物品）")]
        [Tooltip("左侧标题，固定显示 未装备")]
        [SerializeField] private TextMeshProUGUI leftHeaderText;

        [Tooltip("左侧属性文本（此物品 Lv.1 属性）")]
        [SerializeField] private TextMeshProUGUI leftStatsText;

        [Header("Info — 右列（已装备）")]
        [Tooltip("右侧标题，固定显示 已装备")]
        [SerializeField] private TextMeshProUGUI rightHeaderText;

        [Tooltip("右侧属性文本（已装备槽位当前属性）")]
        [SerializeField] private TextMeshProUGUI rightStatsText;

        [Header("按钮")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button equipButton;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 颜色常量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private const string COLOR_BETTER = "#00FF00";   // 绿 — 数值更高
        private const string COLOR_WORSE  = "#FF4444";   // 红 — 数值更低
        private const string COLOR_EQUAL  = "#FFFFFF";   // 白 — 相等

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private InventoryStack _stack;
        private EquipmentPanel _parent;
        private EquipmentData  _data;
        public RectTransform EquipButtonRect => equipButton != null ? equipButton.GetComponent<RectTransform>() : null;
        public RectTransform CloseButtonRect => closeButton != null ? closeButton.GetComponent<RectTransform>() : null;
        public InventoryStack CurrentStack => _stack;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            if (closeButton      != null) closeButton.onClick.AddListener(Close);
            if (backgroundButton != null) backgroundButton.onClick.AddListener(Close);
            if (equipButton      != null) equipButton.onClick.AddListener(OnEquipClicked);
            UIButtonCommonHelper.Ensure(closeButton);
            UIButtonCommonHelper.Ensure(equipButton);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公开接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void Setup(InventoryStack stack, EquipmentPanel parent)
        {
            _stack  = stack;
            _parent = parent;
            _data   = EquipmentManager.Instance?.Database?.GetById(stack.equipmentId);
            if (_data == null) { Close(); return; }
            Refresh();
            InfoShown?.Invoke(_stack);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 刷新显示
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Refresh()
        {
            var mgr = EquipmentManager.Instance;

            // ── 图标 ──
            if (itemIcon != null && _data.icon != null)
                itemIcon.sprite = _data.icon;

            // ── 名称 ──
            if (nameText != null) nameText.text = _data.displayName;

            // ── 标题 ──
            if (leftHeaderText  != null) leftHeaderText.text  = "未装备";
            if (rightHeaderText != null) rightHeaderText.text = "已装备";

            // ── 获取两侧属性 ──
            // 左：此物品 Lv.1 属性（背包物品无等级）
            EquipmentStats thisStats = _data.GetStatsAtLevel(1);

            // 右：当前槽位已装备属性
            var slotData    = mgr?.GetSlot(_data.slotType);
            var slotEquip   = mgr?.GetSlotData(_data.slotType);
            bool hasEquipped = slotData != null && !slotData.IsEmpty && slotEquip != null;
            EquipmentStats equippedStats = hasEquipped
                ? slotEquip.GetStatsAtLevel(slotData.upgradeLevel)
                : new EquipmentStats();

            // ── 生成左右文本（带颜色对比） ──
            if (leftStatsText  != null) leftStatsText.text  = BuildStatsText(thisStats,    equippedStats, isSelf: true);
            if (rightStatsText != null) rightStatsText.text = hasEquipped
                ? BuildStatsText(equippedStats, thisStats, isSelf: false)
                : "<color=#888888>（空槽）</color>";

            // ── 装备按钮 ──
            // 已装备同品质或更高品质的同物品 → 置灰
            bool alreadyBest = hasEquipped
                               && slotData.equipmentId == _stack.equipmentId
                               && slotData.rarity      >= _stack.rarity;
            UIButtonCommonHelper.SetInteractable(equipButton, !alreadyBest);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnEquipClicked()
        {
            EquipmentManager.Instance?.Equip(_stack.equipmentId, _stack.rarity);
            Equipped?.Invoke(_stack);
            Close();
        }

        private void Close()
        {
            InfoClosed?.Invoke(_stack);
            _parent?.OnSubPanelClosed();
            gameObject.SetActive(false);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性文本构建（带颜色 + ▲▼ 符号）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 构建带颜色的属性文本
        /// isSelf=true：这是被评价方（自身），与 other 对比，高于 other 显绿，低于显红
        /// </summary>
        private string BuildStatsText(EquipmentStats self, EquipmentStats other, bool isSelf)
        {
            var sb = new System.Text.StringBuilder();

            AppendStatLine(sb, "攻击力",   self.attackBonus,          other.attackBonus,         "+{0}",    isSelf);
            AppendStatLine(sb, "生命值",   self.hpBonus,              other.hpBonus,             "+{0}",    isSelf);
            AppendStatLine(sb, "护盾值",   self.shieldBonus,          other.shieldBonus,         "+{0}",    isSelf);
            AppendStatLinef(sb,"暴击率",   self.critBonus   * 100f,   other.critBonus   * 100f,  "+{0:F1}%", isSelf);
            AppendStatLinef(sb,"充能率", self.chargeBonus * 100f,   other.chargeBonus * 100f,  "+{0:F1}%", isSelf);

            return sb.Length > 0 ? sb.ToString().TrimEnd() : "<color=#888888>无属性</color>";
        }

        private void AppendStatLine(
            System.Text.StringBuilder sb,
            string label, int selfVal, int otherVal,
            string fmt, bool isSelf)
        {
            if (selfVal == 0 && otherVal == 0) return;

            string color  = GetColor(selfVal, otherVal);
            string symbol = GetSymbol(selfVal, otherVal);
            string valStr = string.Format(fmt, selfVal);

            sb.AppendLine($"{label}：<color={color}>{valStr}{symbol}</color>");
        }

        private void AppendStatLinef(
            System.Text.StringBuilder sb,
            string label, float selfVal, float otherVal,
            string fmt, bool isSelf)
        {
            if (selfVal < 0.001f && otherVal < 0.001f) return;

            string color  = GetColorf(selfVal, otherVal);
            string symbol = GetSymbolf(selfVal, otherVal);
            string valStr = string.Format(fmt, selfVal);

            sb.AppendLine($"{label}：<color={color}>{valStr}{symbol}</color>");
        }

        // ── 颜色/符号辅助 ────────────────────────────────────

        private static string GetColor(int self, int other)
        {
            if (self > other) return COLOR_BETTER;
            if (self < other) return COLOR_WORSE;
            return COLOR_EQUAL;
        }

        private static string GetColorf(float self, float other)
        {
            if (self > other + 0.001f) return COLOR_BETTER;
            if (self < other - 0.001f) return COLOR_WORSE;
            return COLOR_EQUAL;
        }

        private static string GetSymbol(int self, int other)
        {
            if (self > other) return " ▲";
            if (self < other) return " ▼";
            return "";
        }

        private static string GetSymbolf(float self, float other)
        {
            if (self > other + 0.001f) return " ▲";
            if (self < other - 0.001f) return " ▼";
            return "";
        }
    }
}
