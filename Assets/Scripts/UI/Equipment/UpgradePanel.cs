// ============================================================
// UpgradePanel.cs
// 文件位置: Assets/Scripts/UI/Equipment/UpgradePanel.cs
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
using LightVsDecay.Core;

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
        [SerializeField] private TextMeshProUGUI leftLevelText;
        [SerializeField] private TextMeshProUGUI leftStatsText;

        [Header("Info — 右列（升级后等级）")]
        [SerializeField] private TextMeshProUGUI rightLevelText;
        [SerializeField] private TextMeshProUGUI rightStatsText;

        [Header("升级按钮及消耗")]
        [SerializeField] private Button          upgradeButton;
        [SerializeField] private TextMeshProUGUI goldCostText;
        [SerializeField] private TextMeshProUGUI blueprintCostText;

        [Tooltip("按钮内的金币图标 Image（用于置灰）")]
        [SerializeField] private Image goldIconImage;

        [Tooltip("按钮内的图纸图标 Image（用于置灰）")]
        [SerializeField] private Image blueprintIconImage;
        [Tooltip("按钮内的'升级'静态标签文字")]
        [SerializeField] private TextMeshProUGUI upgradeLabelText;
        [Header("关闭按钮")]
        [SerializeField] private Button closeButton;

        [Header("按钮颜色（参考 TechTree 同款）")]
        [SerializeField] private Color buttonNormalColor     = new Color(0.2f, 0.7f, 1f,  1f);
        [SerializeField] private Color buttonDisabledColor   = new Color(0.35f,0.35f,0.35f,1f);
        [SerializeField] private Color costNormalColor       = Color.white;
        [SerializeField] private Color costInsufficientColor = new Color(1f, 0.3f, 0.3f, 1f);

        [Header("长按连升")]
        [SerializeField] private float longPressDelay      = 0.5f;
        [SerializeField] private float autoUpgradeInterval = 0.1f;

        // ── 颜色常量（属性对比文本用） ──────────────────────────
        private const string COLOR_UP    = "#00FF00";
        private const string COLOR_MAX   = "#888888";
        private const string COLOR_WHITE = "#FFFFFF";

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private EquipmentSlotType _slot;
        private EquipmentPanel    _parent;
        private Coroutine         _longPress;
        private Image             _buttonImage;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            if (closeButton      != null) closeButton.onClick.AddListener(Close);
            if (backgroundButton != null) backgroundButton.onClick.AddListener(Close);

            if (upgradeButton != null)
            {
                // 缓存按钮背景 Image
                _buttonImage = upgradeButton.GetComponent<Image>();

                // 长按支持
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
            var mgr = EquipmentManager.Instance;
            if (mgr == null) { Close(); return; }

            var slotData = mgr.GetSlot(_slot);
            var data     = mgr.GetSlotData(_slot);
            if (slotData.IsEmpty || data == null) { Close(); return; }

            int  curLv = slotData.upgradeLevel;
            int  maxLv = data.maxLevel;
            bool isMax = curLv >= maxLv;
            int  nxtLv = Mathf.Min(curLv + 1, maxLv);

            GameLogger.Log($"[UpgradePanel] GoldCoins={ProgressManager.Instance?.GoldCoins}, Blueprints={EquipmentManager.Instance?.Blueprints}, GoldCost={data.GetLevelUpGoldCost(curLv)}, BpCost={data.GetLevelUpBlueprintCost(curLv)}");
            
            // ── 图标 / 名称 ──
            if (itemIcon != null && data.icon != null) itemIcon.sprite = data.icon;
            if (nameText != null) nameText.text = data.displayName;

            // ── 左列：当前等级 ──
            if (leftLevelText != null)
                leftLevelText.text = $"<color={COLOR_WHITE}>Lv.{curLv}</color>";

            var curStats = data.GetStatsAtLevel(curLv);
            if (leftStatsText != null)
                leftStatsText.text = BuildStatsText(curStats, COLOR_WHITE, false);

            // ── 右列：升级后等级 ──
            if (rightLevelText != null)
                rightLevelText.text = isMax
                    ? $"<color={COLOR_MAX}>MAX</color>"
                    : $"<color={COLOR_UP}>Lv.{nxtLv}</color>";

            if (rightStatsText != null)
            {
                if (isMax)
                {
                    rightStatsText.text = $"<color={COLOR_MAX}>已满级</color>";
                }
                else
                {
                    var nxtStats = data.GetStatsAtLevel(nxtLv);
                    rightStatsText.text = BuildStatsDiffText(curStats, nxtStats);
                }
            }

            // ── 消耗显示 + 按钮状态 ──
            if (isMax)
            {
                if (goldCostText      != null) { goldCostText.text = "—";  goldCostText.color = Color.gray; }
                if (blueprintCostText != null) { blueprintCostText.text = "—"; blueprintCostText.color = Color.gray; }
                RefreshButtonVisuals(false, false, false);
            }
            else
            {
                int goldCost = data.GetLevelUpGoldCost(curLv);
                int bpCost   = data.GetLevelUpBlueprintCost(curLv);

                bool hasGold = HasEnoughGold(goldCost);
                bool hasBp   = HasEnoughBlueprints(bpCost);

                // 纯文本，颜色由 RefreshButtonVisuals 控制
                if (goldCostText      != null) goldCostText.text      = goldCost.ToString();
                if (blueprintCostText != null) blueprintCostText.text = bpCost.ToString();

                RefreshButtonVisuals(hasGold, hasBp, true);
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮状态刷新（参考 TechTree 同款逻辑）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void RefreshButtonVisuals(bool hasGold, bool hasBp, bool notMax)
        {
            bool canUpgrade = notMax && hasGold && hasBp;

            // 1. 按钮交互 + 背景色
            upgradeButton.interactable = canUpgrade;
            if (_buttonImage != null)
                _buttonImage.color = canUpgrade ? buttonNormalColor : buttonDisabledColor;

            // 2. "升级" 标签文字：可升级=白，否则=灰
            if (upgradeLabelText != null)
                upgradeLabelText.color = canUpgrade ? costNormalColor : Color.gray;

            // 3. 金币数字颜色规则：
            //    自身不足 → 红；对方不足（自身足够）→ 灰；都足够 → 白
            Color goldColor = !hasGold ? costInsufficientColor
                : (!hasBp  ? Color.gray
                    : costNormalColor);

            // 4. 图纸数字颜色规则（对称）
            Color bpColor   = !hasBp   ? costInsufficientColor
                : (!hasGold ? Color.gray
                    : costNormalColor);

            // 5. 图标颜色：可升级=白，否则统一灰
            Color iconColor = canUpgrade ? Color.white : Color.gray;

            if (goldCostText       != null) goldCostText.color       = goldColor;
            if (blueprintCostText  != null) blueprintCostText.color  = bpColor;
            if (goldIconImage      != null) goldIconImage.color      = iconColor;
            if (blueprintIconImage != null) blueprintIconImage.color = iconColor;
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 资源判断
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private bool HasEnoughGold(int cost)
        {
            if (ProgressManager.Instance == null)
            {
                GameLogger.LogWarning("[UpgradePanel] ProgressManager.Instance 为空，无法检查金币！");
                return false;
            }
            return ProgressManager.Instance.GoldCoins >= cost;
        }

        private bool HasEnoughBlueprints(int cost)
        {
            if (EquipmentManager.Instance == null)
            {
                GameLogger.LogWarning("[UpgradePanel] EquipmentManager.Instance 为空，无法检查图纸！");
                return false;
            }
            return EquipmentManager.Instance.Blueprints >= cost;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 升级按钮逻辑
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnUpgradeDown()
        {
            // ★ EventTrigger 不检查 interactable，必须手动拦截（与 TechTree 同款修复）
            if (upgradeButton != null && !upgradeButton.interactable) return;

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

            // 升级失败：刷新按钮状态（确保显示正确）
            Refresh();
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

        private string BuildStatsText(EquipmentStats s, string color, bool showSymbol)
        {
            var sb = new System.Text.StringBuilder();
            if (s.attackBonus  != 0) sb.AppendLine($"攻击力：<color={color}>+{s.attackBonus}</color>");
            if (s.hpBonus      != 0) sb.AppendLine($"生命值：<color={color}>+{s.hpBonus}</color>");
            if (s.shieldBonus  != 0) sb.AppendLine($"护盾值：<color={color}>+{s.shieldBonus}</color>");
            if (s.critBonus    > 0f) sb.AppendLine($"暴击率：<color={color}>+{s.critBonus * 100:F1}%</color>");
            if (s.chargeBonus  > 0f) sb.AppendLine($"充能率：<color={color}>+{s.chargeBonus * 100:F1}%</color>");
            return sb.Length > 0 ? sb.ToString().TrimEnd() : $"<color={color}>无属性</color>";
        }

        private string BuildStatsDiffText(EquipmentStats cur, EquipmentStats nxt)
        {
            var sb = new System.Text.StringBuilder();
            AppendUpgrade(sb,  "攻击力",   cur.attackBonus,        nxt.attackBonus,        "+{0}",    "+{0}");
            AppendUpgrade(sb,  "生命值",   cur.hpBonus,            nxt.hpBonus,            "+{0}",    "+{0}");
            AppendUpgrade(sb,  "护盾值",   cur.shieldBonus,        nxt.shieldBonus,        "+{0}",    "+{0}");
            AppendUpgradeF(sb, "暴击率",   cur.critBonus   * 100f, nxt.critBonus   * 100f, "+{0:F1}%","+{0:F1}%");
            AppendUpgradeF(sb, "充能率", cur.chargeBonus * 100f, nxt.chargeBonus * 100f, "+{0:F1}%","+{0:F1}%");
            return sb.Length > 0 ? sb.ToString().TrimEnd() : $"<color={COLOR_UP}>无变化</color>";
        }

        private void AppendUpgrade(System.Text.StringBuilder sb,
            string label, int curVal, int nxtVal, string curFmt, string nxtFmt)
        {
            if (curVal == 0 && nxtVal == 0) return;
            bool increased = nxtVal > curVal;
            string color  = increased ? COLOR_UP : COLOR_WHITE;
            string symbol = increased ? " ▲" : "";
            sb.AppendLine($"{label}：<color={color}>{string.Format(nxtFmt, nxtVal)}{symbol}</color>");
        }

        private void AppendUpgradeF(System.Text.StringBuilder sb,
            string label, float curVal, float nxtVal, string curFmt, string nxtFmt)
        {
            if (curVal < 0.001f && nxtVal < 0.001f) return;
            bool increased = nxtVal > curVal + 0.001f;
            string color  = increased ? COLOR_UP : COLOR_WHITE;
            string symbol = increased ? " ▲" : "";
            sb.AppendLine($"{label}：<color={color}>{string.Format(nxtFmt, nxtVal)}{symbol}</color>");
        }

        private static void AddTrigger(EventTrigger et, EventTriggerType type,
            UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(action);
            et.triggers.Add(e);
        }
    }
}