// ============================================================
// EquipmentPanel.cs
// 文件位置: Assets/Scripts/UI/Equipment/EquipmentPanel.cs
// ============================================================

using System.Collections.Generic;
using System.Linq;
using System.Collections;
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
        private struct DisplayedStats
        {
            public int Attack;
            public int Hp;
            public int Shield;
            public float Crit;
            public float Charge;
        }

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
        [Header("属性数字演出")]
        [SerializeField] private float statRollDuration = 0.5f;
        [SerializeField] private float statPunchScale = 1.2f;
        [SerializeField] private float statPunchDuration = 0.2f;

        private List<EquipmentItemUI> _pool          = new List<EquipmentItemUI>();
        private InventoryStack        _selectedStack = null;
        private DisplayedStats _displayedStats;
        private bool _hasDisplayedStats;
        private readonly Dictionary<TextMeshProUGUI, Coroutine> _statCoroutines = new Dictionary<TextMeshProUGUI, Coroutine>();

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
            StopAllStatAnimations();
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

            DisplayedStats target = new DisplayedStats
            {
                Attack = baseAtk + bonus.attackBonus,
                Hp = baseHp + bonus.hpBonus,
                Shield = baseShield + bonus.shieldBonus,
                Crit = (baseCrit + bonus.critBonus) * 100f,
                Charge = (baseCharge + bonus.chargeBonus) * 100f
            };

            ApplyStats(target, _hasDisplayedStats);
            _displayedStats = target;
            _hasDisplayedStats = true;
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

        public EquipmentItemUI GetBestEquippableItemUI()
        {
            var mgr = EquipmentManager.Instance;
            if (mgr == null)
            {
                return null;
            }

            EquipmentItemUI best = null;

            foreach (var item in _pool)
            {
                if (item == null || !item.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var stack = item.Stack;
                var data = mgr.Database?.GetById(stack.equipmentId);
                if (data == null || stack.count <= 0)
                {
                    continue;
                }

                var equipped = mgr.GetSlot(data.slotType);
                bool canEquip = equipped.IsEmpty
                    || stack.equipmentId != equipped.equipmentId
                    || (int)stack.rarity > (int)equipped.rarity;

                if (!canEquip)
                {
                    continue;
                }

                if (best == null || (int)stack.rarity > (int)best.Stack.rarity)
                {
                    best = item;
                }
            }

            return best;
        }

        private void ApplyStats(DisplayedStats target, bool animate)
        {
            ApplyIntStat(attackText, "攻击力：", animate ? _displayedStats.Attack : target.Attack, target.Attack, animate);
            ApplyIntStat(hpText, "生命值：", animate ? _displayedStats.Hp : target.Hp, target.Hp, animate);
            ApplyIntStat(shieldText, "护盾值：", animate ? _displayedStats.Shield : target.Shield, target.Shield, animate);
            ApplyFloatStat(critText, "暴击率：", animate ? _displayedStats.Crit : target.Crit, target.Crit, animate);
            ApplyFloatStat(chargeText, "充能效率：", animate ? _displayedStats.Charge : target.Charge, target.Charge, animate);
        }

        private void ApplyIntStat(TextMeshProUGUI text, string label, int from, int to, bool animate)
        {
            if (text == null)
            {
                return;
            }

            if (!animate || from == to)
            {
                text.text = $"{label}{to}";
                return;
            }

            RestartStatAnimation(text, AnimateIntStat(text, label, from, to));
        }

        private void ApplyFloatStat(TextMeshProUGUI text, string label, float from, float to, bool animate)
        {
            if (text == null)
            {
                return;
            }

            if (!animate || Mathf.Abs(from - to) < 0.01f)
            {
                text.text = $"{label}{to:F1}%";
                return;
            }

            RestartStatAnimation(text, AnimateFloatStat(text, label, from, to));
        }

        private void RestartStatAnimation(TextMeshProUGUI text, IEnumerator routine)
        {
            if (_statCoroutines.TryGetValue(text, out var existing) && existing != null)
            {
                StopCoroutine(existing);
            }

            _statCoroutines[text] = StartCoroutine(routine);
        }

        private IEnumerator AnimateIntStat(TextMeshProUGUI text, string label, int from, int to)
        {
            yield return AnimatePunch(text.transform as RectTransform);

            float elapsed = 0f;
            while (elapsed < statRollDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / statRollDuration);
                int value = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                text.text = $"{label}{value}";
                yield return null;
            }

            text.text = $"{label}{to}";
            _statCoroutines[text] = null;
        }

        private IEnumerator AnimateFloatStat(TextMeshProUGUI text, string label, float from, float to)
        {
            yield return AnimatePunch(text.transform as RectTransform);

            float elapsed = 0f;
            while (elapsed < statRollDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / statRollDuration);
                float value = Mathf.Lerp(from, to, t);
                text.text = $"{label}{value:F1}%";
                yield return null;
            }

            text.text = $"{label}{to:F1}%";
            _statCoroutines[text] = null;
        }

        private IEnumerator AnimatePunch(RectTransform rect)
        {
            if (rect == null)
            {
                yield break;
            }

            Vector3 baseScale = rect.localScale;
            float half = Mathf.Max(0.05f, statPunchDuration * 0.5f);
            float elapsed = 0f;

            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                rect.localScale = Vector3.Lerp(baseScale, baseScale * statPunchScale, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                rect.localScale = Vector3.Lerp(baseScale * statPunchScale, baseScale, t);
                yield return null;
            }

            rect.localScale = baseScale;
        }

        private void StopAllStatAnimations()
        {
            foreach (var pair in _statCoroutines)
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }

            _statCoroutines.Clear();
        }
    }
}
