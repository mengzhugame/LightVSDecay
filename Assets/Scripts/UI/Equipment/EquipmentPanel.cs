// ============================================================
// EquipmentPanel.cs
// 文件位置: Assets/Scripts/UI/Equipment/EquipmentPanel.cs
// ============================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightVsDecay.Data.Runtime;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Equipment;

namespace LightVsDecay.UI.Equipment
{
    public class EquipmentPanel : MonoBehaviour
    {
        [Header("装备槽 UI（0=PrismCore / 1=TowerBase / 2=Processor）")]
        [SerializeField] private EquipmentSlotUI[] slotUIs = new EquipmentSlotUI[3];

        [Header("背包")]
        [SerializeField] private Transform       inventoryGridRoot;
        [SerializeField] private EquipmentItemUI itemUIPrefab;
        [Header("基础配置（读取初始属性）")]
        [SerializeField] private GameSettings gameSettings;
        [Header("总属性显示")]
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI shieldText;
        [SerializeField] private TextMeshProUGUI critText;
        [SerializeField] private TextMeshProUGUI chargeText;

        // 资源显示（金币/图纸/体力）统一在 TopArea，此处不持有

        [Header("功能按钮")]
        [SerializeField] private Button     autoMergeButton;
        [SerializeField] private Button     autoEquipButton;

        [Tooltip("合成红点：背包中存在可合成组（≥3个同种同品质）时显示")]
        [SerializeField] private GameObject mergeRedDot;

        [Tooltip("装备红点：背包中存在比已装备更高品质的同槽物品时显示")]
        [SerializeField] private GameObject equipRedDot;

        [Header("二级面板")]
        [SerializeField] private UpgradePanel  upgradePanel;
        [SerializeField] private ItemInfoPanel itemInfoPanel;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private List<EquipmentItemUI> _pool          = new List<EquipmentItemUI>();
        private InventoryStack        _selectedStack = null;

        // ── 生命周期 ──────────────────────────────────────────

        private void OnEnable()
        {
            EquipmentManager.OnInventoryChanged     += OnInventoryChanged;
            EquipmentManager.OnEquipmentSlotChanged += OnSlotChanged;
            if (autoMergeButton != null) autoMergeButton.onClick.AddListener(OnAutoMerge);
            if (autoEquipButton != null) autoEquipButton.onClick.AddListener(OnAutoEquip);
            RefreshAll();
        }

        private void OnDisable()
        {
            EquipmentManager.OnInventoryChanged     -= OnInventoryChanged;
            EquipmentManager.OnEquipmentSlotChanged -= OnSlotChanged;
            if (autoMergeButton != null) autoMergeButton.onClick.RemoveListener(OnAutoMerge);
            if (autoEquipButton != null) autoEquipButton.onClick.RemoveListener(OnAutoEquip);
        }

        // ── 刷新 ──────────────────────────────────────────────

        public void RefreshAll()
        {
            RefreshSlots();
            RefreshInventory();
            RefreshStats();
            RefreshRedDots();
        }

        private void RefreshSlots()
        {
            var mgr = EquipmentManager.Instance;
            if (mgr == null) return;
            for (int i = 0; i < slotUIs.Length; i++)
            {
                if (slotUIs[i] == null) continue;
                var slot = (EquipmentSlotType)i;
                slotUIs[i].Setup(slot, mgr.GetSlot(slot), mgr.GetSlotData(slot), OnSlotButtonClicked);
            }
        }

        private void RefreshInventory()
        {
            var mgr = EquipmentManager.Instance;
            if (mgr == null || inventoryGridRoot == null) return;

            var sorted = mgr.Inventory
                .Where(s => s.count > 0)
                .OrderByDescending(s => (int)s.rarity)
                .ThenBy(s => (int)(mgr.Database?.GetById(s.equipmentId)?.slotType ?? 0))
                .ToList();

            while (_pool.Count < sorted.Count)
            {
                var cell = Instantiate(itemUIPrefab, inventoryGridRoot);
                cell.gameObject.SetActive(false);
                _pool.Add(cell);
            }

            for (int i = 0; i < _pool.Count; i++)
            {
                if (i < sorted.Count)
                {
                    var stack = sorted[i];
                    bool sel  = _selectedStack != null
                                && _selectedStack.equipmentId == stack.equipmentId
                                && _selectedStack.rarity      == stack.rarity;
                    _pool[i].Setup(stack, mgr.Database?.GetById(stack.equipmentId), sel, OnItemClicked);
                    _pool[i].gameObject.SetActive(true);
                }
                else _pool[i].gameObject.SetActive(false);
            }
        }

        private void RefreshStats()
        {
            var mgr = EquipmentManager.Instance;
            if (mgr == null) return;

            // 装备加成
            var bonus = mgr.GetTotalStats();

            // 基础值（来自 GameSettings，未来可替换为服务器下发数据）
            int   baseAtk    = gameSettings != null ? Mathf.RoundToInt(gameSettings.baseDPS)    : 0;
            int   baseHp     = gameSettings != null ? gameSettings.maxHullHP                    : 0;
            int   baseShield = gameSettings != null ? gameSettings.maxShieldHP                  : 0;
            float baseCrit   = gameSettings != null ? gameSettings.baseCritRate                 : 0f;
            float baseCharge = 0f; // 暂无基础值，服务器接入后补充

            // 显示：基础值 + 装备加成
            if (attackText != null) attackText.text = $"攻击力：{baseAtk  + bonus.attackBonus}";
            if (hpText     != null) hpText.text     = $"生命值：{baseHp   + bonus.hpBonus}";
            if (shieldText != null) shieldText.text = $"护盾值：{baseShield + bonus.shieldBonus}";
            if (critText   != null) critText.text   = $"暴击率：{(baseCrit   + bonus.critBonus)   * 100:F1}%";
            if (chargeText != null) chargeText.text = $"充能效率：{(baseCharge + bonus.chargeBonus) * 100:F1}%";
        }

        private void RefreshRedDots()
        {
            var mgr = EquipmentManager.Instance;
            if (mgr == null) return;

            // 合成红点
            if (mergeRedDot != null)
                mergeRedDot.SetActive(mgr.HasAnyMergeable());

            // 装备红点：背包中存在比已装备品质更高的同槽物品
            if (equipRedDot != null)
                equipRedDot.SetActive(HasBetterItemInInventory(mgr));
        }

        private bool HasBetterItemInInventory(EquipmentManager mgr)
        {
            foreach (var stack in mgr.Inventory)
            {
                if (stack.count <= 0) continue;
                var data = mgr.Database?.GetById(stack.equipmentId);
                if (data == null) continue;
                var current = mgr.GetSlot(data.slotType);
                if (current.IsEmpty) return true;
                if ((int)stack.rarity > (int)current.rarity) return true;
            }
            return false;
        }

        // ── 事件回调 ──────────────────────────────────────────

        private void OnInventoryChanged()           { RefreshInventory(); RefreshStats(); RefreshRedDots(); }
        private void OnSlotChanged(EquipmentSlotType _) { RefreshSlots();    RefreshStats(); RefreshRedDots(); }

        private void OnSlotButtonClicked(EquipmentSlotType slot)
        {
            if (upgradePanel == null) return;
            upgradePanel.gameObject.SetActive(true);
            upgradePanel.Setup(slot, this);
        }

        private void OnItemClicked(InventoryStack stack)
        {
            _selectedStack = stack;
            RefreshInventory();
            if (itemInfoPanel == null) return;
            itemInfoPanel.gameObject.SetActive(true);
            itemInfoPanel.Setup(stack, this);
        }

        private void OnAutoMerge() => EquipmentManager.Instance?.AutoMergeAll();
        private void OnAutoEquip() => EquipmentManager.Instance?.AutoEquipBest();

        public void OnSubPanelClosed()
        {
            _selectedStack = null;
            RefreshAll();
        }
    }
}