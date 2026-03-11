// ============================================================
// EquipmentManager.cs
// 文件位置: Assets/Scripts/Logic/Equipment/EquipmentManager.cs
// 用途：装备系统核心管理器（堆叠背包版）
//
// 背包：同种同品质堆叠 → 合成消耗堆叠数
// 装备槽：独立记录 equipmentId + rarity + upgradeLevel
// 物品本身无等级，等级仅存在于装备槽
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Data.Runtime;
using LightVsDecay.Data.SO;

namespace LightVsDecay.Logic.Equipment
{
    public class EquipmentManager : PersistentSingleton<EquipmentManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("配置")]
        [SerializeField] private EquipmentDatabase database;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

#if UNITY_EDITOR
        [Header("Debug：初始背包（仅编辑器，格式 id:rarity:count）")]
        [SerializeField] private List<string> debugInitItems = new List<string>();
#endif

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>背包内容变化（堆叠数增减/合成）</summary>
        public static event Action OnInventoryChanged;

        /// <summary>某槽位装备变化（装备/卸下/升级）</summary>
        public static event Action<EquipmentSlotType> OnEquipmentSlotChanged;

        /// <summary>图纸数量变化</summary>
        public static event Action<int> OnBlueprintsChanged;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private List<InventoryStack>  _inventory = new List<InventoryStack>();
        private EquippedSlotData[]    _slots     = new EquippedSlotData[3]
        {
            EquippedSlotData.Empty(),
            EquippedSlotData.Empty(),
            EquippedSlotData.Empty(),
        };
        private int _blueprints = 0;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共只读属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public IReadOnlyList<InventoryStack> Inventory   => _inventory;
        public int                           Blueprints  => _blueprints;
        public EquipmentDatabase             Database    => database;

        /// <summary>获取指定槽位数据（空槽时 IsEmpty=true）</summary>
        public EquippedSlotData GetSlot(EquipmentSlotType slot) => _slots[(int)slot];

        /// <summary>获取指定槽位的 EquipmentData（空槽返回 null）</summary>
        public EquipmentData GetSlotData(EquipmentSlotType slot)
        {
            var s = _slots[(int)slot];
            return s.IsEmpty ? null : database?.GetById(s.equipmentId);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void OnSingletonAwake()
        {
            if (database == null)
            {
                Debug.LogError("[EquipmentManager] 缺少 EquipmentDatabase！");
                return;
            }
            EquipmentDatabase.SetInstance(database);
            database.Initialize();
            LoadFromSave();

#if UNITY_EDITOR
            if (_inventory.Count == 0 && debugInitItems.Count > 0)
                ParseDebugItems();
#endif
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 背包操作
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>向背包添加物品（自动堆叠）</summary>
        public void AddToInventory(string equipmentId, ItemRarity rarity, int count = 1)
        {
            if (database?.GetById(equipmentId) == null)
            {
                Debug.LogWarning($"[EquipmentManager] 未知装备ID: {equipmentId}");
                return;
            }

            var stack = FindStack(equipmentId, rarity);
            if (stack != null)
                stack.count += count;
            else
                _inventory.Add(new InventoryStack(equipmentId, rarity, count));

            Save();
            OnInventoryChanged?.Invoke();

            if (showDebugInfo)
                Debug.Log($"[EquipManager] 获得 {equipmentId}({rarity}) ×{count}");
        }

        /// <summary>获取指定种类的堆叠数量</summary>
        public int GetStackCount(string equipmentId, ItemRarity rarity)
            => FindStack(equipmentId, rarity)?.count ?? 0;

        /// <summary>是否存在任何可合成的组（≥3个同种同品质）</summary>
        public bool HasAnyMergeable()
            => _inventory.Any(s => s.count >= 3 && s.rarity < ItemRarity.Legendary
                                   && database.GetMergeResult(database.GetById(s.equipmentId)) != null);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 装备/卸下
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 装备背包中某个堆叠格到对应槽位
        /// 若槽位已有装备，先卸下（返回背包，等级清零）
        /// </summary>
        public EquipResult Equip(string equipmentId, ItemRarity rarity)
        {
            var data = database?.GetById(equipmentId);
            if (data == null) return EquipResult.DataNotFound;

            var stack = FindStack(equipmentId, rarity);
            if (stack == null || stack.count <= 0) return EquipResult.NotEnoughItems;

            int slotIdx = (int)data.slotType;

            // 卸下已有装备（返回背包）
            if (!_slots[slotIdx].IsEmpty)
                UnequipInternal(data.slotType, returnToInventory: true);

            // 消耗1个
            stack.count--;
            if (stack.count <= 0) _inventory.Remove(stack);

            // 装备（初始Lv.1）
            _slots[slotIdx] = EquippedSlotData.Create(equipmentId, rarity);

            Save();
            OnInventoryChanged?.Invoke();
            OnEquipmentSlotChanged?.Invoke(data.slotType);

            if (showDebugInfo)
                Debug.Log($"[EquipManager] 装备: {equipmentId}({rarity}) → 槽位{data.slotType}");

            return EquipResult.Success;
        }

        /// <summary>卸下槽位装备，返回背包</summary>
        public void Unequip(EquipmentSlotType slot)
            => UnequipInternal(slot, returnToInventory: true);

        private void UnequipInternal(EquipmentSlotType slot, bool returnToInventory)
        {
            int idx = (int)slot;
            if (_slots[idx].IsEmpty) return;

            if (returnToInventory)
            {
                // 卸下后以基础物品（等级丢失）返回背包
                AddToInventoryNoSave(_slots[idx].equipmentId, _slots[idx].rarity, 1);
            }

            _slots[idx] = EquippedSlotData.Empty();
            Save();
            OnEquipmentSlotChanged?.Invoke(slot);

            if (showDebugInfo)
                Debug.Log($"[EquipManager] 卸下槽位: {slot}");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 升级槽位
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>升级指定槽位装备等级（消耗金币+图纸）</summary>
        public LevelUpResult LevelUpSlot(EquipmentSlotType slot)
        {
            var slotData = _slots[(int)slot];
            if (slotData.IsEmpty)         return LevelUpResult.NotFound;

            var data = database?.GetById(slotData.equipmentId);
            if (data == null)             return LevelUpResult.NotFound;
            if (slotData.upgradeLevel >= data.maxLevel) return LevelUpResult.AlreadyMaxLevel;

            int goldCost = data.GetLevelUpGoldCost(slotData.upgradeLevel);
            int bpCost   = data.GetLevelUpBlueprintCost(slotData.upgradeLevel);

            if (ProgressManager.Instance == null) return LevelUpResult.InsufficientResources;
            if (ProgressManager.Instance.GoldCoins < goldCost) return LevelUpResult.InsufficientGold;
            if (_blueprints < bpCost)              return LevelUpResult.InsufficientBlueprints;

            ProgressManager.Instance.ConsumeGoldCoins(goldCost);
            _blueprints -= bpCost;
            slotData.upgradeLevel++;

            Save();
            OnEquipmentSlotChanged?.Invoke(slot);

            if (showDebugInfo)
                Debug.Log($"[EquipManager] {slot} 升级 → Lv.{slotData.upgradeLevel}（消耗 {goldCost}金 {bpCost}图纸）");

            return LevelUpResult.Success;
        }

        /// <summary>无损重置：退还所有升级消耗，等级归1</summary>
        public void ResetSlotLevel(EquipmentSlotType slot)
        {
            var slotData = _slots[(int)slot];
            if (slotData.IsEmpty || slotData.upgradeLevel <= 1) return;

            var data = database?.GetById(slotData.equipmentId);
            if (data == null) return;

            // 退还从 Lv.1 到当前等级的全部消耗
            int refundGold = 0, refundBp = 0;
            for (int lv = 1; lv < slotData.upgradeLevel; lv++)
            {
                refundGold += data.GetLevelUpGoldCost(lv);
                refundBp   += data.GetLevelUpBlueprintCost(lv);
            }

            ProgressManager.Instance?.AddGoldCoins(refundGold);
            _blueprints += refundBp;
            slotData.upgradeLevel = 1;

            Save();
            OnEquipmentSlotChanged?.Invoke(slot);
            OnBlueprintsChanged?.Invoke(_blueprints);

            if (showDebugInfo)
                Debug.Log($"[EquipManager] {slot} 无损重置，退还 {refundGold}金 {refundBp}图纸");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 合成
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>对指定堆叠格合成一次（消耗3个 → 获得1个更高品质）</summary>
        public bool Merge(string equipmentId, ItemRarity rarity)
        {
            var stack = FindStack(equipmentId, rarity);
            if (stack == null || stack.count < 3) return false;

            var sourceData = database?.GetById(equipmentId);
            if (sourceData == null) return false;

            var resultData = database.GetMergeResult(sourceData);
            if (resultData == null) return false;

            stack.count -= 3;
            if (stack.count <= 0) _inventory.Remove(stack);

            AddToInventoryNoSave(resultData.equipmentId, resultData.rarity, 1);

            Save();
            OnInventoryChanged?.Invoke();

            if (showDebugInfo)
                Debug.Log($"[EquipManager] 合成: 3×{equipmentId}({rarity}) → {resultData.equipmentId}({resultData.rarity})");

            return true;
        }

        /// <summary>一键合成：自动合成所有可合成的组，返回合成次数</summary>
        public int AutoMergeAll()
        {
            int total = 0;
            bool merged = true;
            while (merged)
            {
                merged = false;
                foreach (var s in _inventory.ToList())
                {
                    if (s.count >= 3 && s.rarity < ItemRarity.Legendary)
                    {
                        if (Merge(s.equipmentId, s.rarity))
                        { total++; merged = true; break; }
                    }
                }
            }
            if (showDebugInfo && total > 0)
                Debug.Log($"[EquipManager] 一键合成完成，共 {total} 次");
            return total;
        }

        /// <summary>一键装备：每个槽位自动装备背包中最高品质的物品</summary>
        public void AutoEquipBest()
        {
            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                // 找对应槽位中最高品质的背包物品
                var slotId = GetSlotEquipmentId(slot);
                var best   = _inventory
                    .Where(s => s.count > 0 && database?.GetById(s.equipmentId)?.slotType == slot)
                    .OrderByDescending(s => (int)s.rarity)
                    .FirstOrDefault();

                if (best == null) continue;

                // 如果背包最好的和已装备的一样，跳过
                var current = _slots[(int)slot];
                if (!current.IsEmpty && current.equipmentId == best.equipmentId
                    && current.rarity >= best.rarity) continue;

                Equip(best.equipmentId, best.rarity);
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 分解
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>分解背包中指定堆叠格的1个物品，获得图纸</summary>
        public int Decompose(string equipmentId, ItemRarity rarity)
        {
            var stack = FindStack(equipmentId, rarity);
            if (stack == null || stack.count <= 0) return 0;

            int gain = (int)rarity + 1;   // 绿+2, 蓝+3, 紫+4, 橙+5
            stack.count--;
            if (stack.count <= 0) _inventory.Remove(stack);

            _blueprints += gain;
            Save();
            OnInventoryChanged?.Invoke();
            OnBlueprintsChanged?.Invoke(_blueprints);

            if (showDebugInfo)
                Debug.Log($"[EquipManager] 分解 {equipmentId}({rarity})，获得 {gain} 图纸");

            return gain;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 合计属性 & 塔外观档次
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>计算三件装备的合计属性（供 TurretHealth 等读取）</summary>
        public EquipmentStats GetTotalStats()
        {
            var total = new EquipmentStats();
            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                var s    = _slots[(int)slot];
                var data = s.IsEmpty ? null : database?.GetById(s.equipmentId);
                if (data == null) continue;
                total = total + data.GetStatsAtLevel(s.upgradeLevel);
            }
            return total;
        }

        /// <summary>
        /// 塔外观档次（0=灰/无装备，1=绿，2=蓝，3=紫，4=橙）
        /// 规则：任一槽位为空→0；否则取三槽最低品质对应档次
        /// </summary>
        public int GetOverallTowerTier()
        {
            foreach (var s in _slots)
                if (s.IsEmpty) return 0;

            int lowest = (int)ItemRarity.Legendary;
            foreach (var s in _slots)
                lowest = Mathf.Min(lowest, (int)s.rarity);

            // ItemRarity: Common=0(不存在实际装备) Uncommon=1 Rare=2 Epic=3 Legendary=4
            return Mathf.Clamp(lowest, 0, 4);
        }
        /// <summary>直接增加图纸数量（结算奖励用）</summary>
        public void AddBlueprints(int count)
        {
            if (count <= 0) return;
            _blueprints += count;
            Save();
            OnBlueprintsChanged?.Invoke(_blueprints);

            if (showDebugInfo)
                Debug.Log($"[EquipManager] 获得图纸 ×{count}，当前共 {_blueprints}");
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 辅助
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private InventoryStack FindStack(string id, ItemRarity r)
            => _inventory.FirstOrDefault(s => s.equipmentId == id && s.rarity == r);

        private string GetSlotEquipmentId(EquipmentSlotType slot)
            => _slots[(int)slot].equipmentId;

        private void AddToInventoryNoSave(string id, ItemRarity r, int count)
        {
            var s = FindStack(id, r);
            if (s != null) s.count += count;
            else _inventory.Add(new InventoryStack(id, r, count));
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 存档
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Save()
        {
            var d = new EquipmentSaveData
            {
                inventory  = _inventory,
                slots      = _slots,
                blueprints = _blueprints,
            };
            d.Save();
        }

        private void LoadFromSave()
        {
            var d = EquipmentSaveData.Load();
            _inventory  = d.inventory ?? new List<InventoryStack>();
            _slots      = d.slots     ?? new EquippedSlotData[3]
                { EquippedSlotData.Empty(), EquippedSlotData.Empty(), EquippedSlotData.Empty() };
            _blueprints = d.blueprints;

            if (showDebugInfo)
                Debug.Log($"[EquipManager] 读档完成：背包 {_inventory.Count} 格，图纸 {_blueprints}");
        }
        /// <summary>
        /// 重置所有装备数据（内存 + PlayerPrefs），供 PlayerDataResetTool 调用
        /// </summary>
        public void ResetAll()
        {
            _inventory  = new List<InventoryStack>();
            _slots      = new EquippedSlotData[3]
            {
                EquippedSlotData.Empty(),
                EquippedSlotData.Empty(),
                EquippedSlotData.Empty(),
            };
            _blueprints = 0;

            EquipmentSaveData.Reset();   // 同时清 PlayerPrefs

            OnInventoryChanged?.Invoke();
            OnBlueprintsChanged?.Invoke(0);

            if (showDebugInfo)
                Debug.Log("[EquipmentManager] ResetAll: 内存+存档全部清除");
        }
#if UNITY_EDITOR
        private void ParseDebugItems()
        {
            foreach (var entry in debugInitItems)
            {
                // 格式: equipmentId:rarityInt:count  例如 core_uncommon:1:5
                var parts = entry.Split(':');
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[1], out int r)) continue;
                int cnt = parts.Length >= 3 && int.TryParse(parts[2], out int c) ? c : 1;
                AddToInventory(parts[0], (ItemRarity)r, cnt);
            }
        }
#endif
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 结果枚举
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public enum EquipResult
    {
        Success, DataNotFound, NotEnoughItems
    }

    public enum LevelUpResult
    {
        Success, NotFound, AlreadyMaxLevel,
        InsufficientResources, InsufficientGold, InsufficientBlueprints
    }
}