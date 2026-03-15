// ============================================================
// EquipmentDatabase.cs
// 文件位置: Assets/Scripts/Data/SO/EquipmentDatabase.cs
// 用途：装备数据库 + 运行时装备实例定义
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Data.SO
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 运行时装备实例（背包中每一件装备的存档+状态）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 运行时装备实例
    /// 一个"实例"= 玩家背包中的某一件具体装备
    /// 多件同名装备 = 多个实例（但 equipmentId 相同）
    /// </summary>
    [System.Serializable]
    public class EquipmentInstance
    {
        /// <summary>实例唯一ID（用于存档区分同名装备）</summary>
        public string instanceId;

        /// <summary>装备模板ID（对应 EquipmentData.equipmentId）</summary>
        public string equipmentId;

        /// <summary>当前等级（1起）</summary>
        public int level = 1;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时缓存（不存档，从 Database 查询）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [System.NonSerialized]
        private EquipmentData _cachedData;

        /// <summary>装备模板数据（运行时懒加载）</summary>
        public EquipmentData Data
        {
            get
            {
                if (_cachedData == null && EquipmentDatabase.Instance != null)
                {
                    _cachedData = EquipmentDatabase.Instance.GetById(equipmentId);
                }
                return _cachedData;
            }
            set => _cachedData = value;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 便捷属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public string DisplayName    => Data?.displayName ?? "未知装备";
        public EquipmentSlotType Slot => Data?.slotType ?? EquipmentSlotType.PrismCore;
        public ItemRarity Rarity     => Data?.rarity ?? ItemRarity.Common;
        public Sprite Icon           => Data?.icon;
        public int MaxLevel          => Data?.maxLevel ?? 10;
        public bool IsMaxLevel       => level >= MaxLevel;

        /// <summary>当前等级的实际属性</summary>
        public EquipmentStats CurrentStats => Data?.GetStatsAtLevel(level) ?? new EquipmentStats();

        /// <summary>创建新实例</summary>
        public static EquipmentInstance Create(EquipmentData data)
        {
            return new EquipmentInstance
            {
                instanceId  = System.Guid.NewGuid().ToString(),
                equipmentId = data.equipmentId,
                level       = 1,
                _cachedData = data,
            };
        }

        /// <summary>创建测试实例（直接指定ID，调试用）</summary>
        public static EquipmentInstance CreateDebug(string equipmentId, int level = 1)
        {
            return new EquipmentInstance
            {
                instanceId  = System.Guid.NewGuid().ToString(),
                equipmentId = equipmentId,
                level       = level,
            };
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 装备数据库 ScriptableObject
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 装备数据库（ScriptableObject）
    /// 注册所有装备模板，提供快速查找
    /// 创建路径：Assets → Create → LightVsDecay → Equipment → Equipment Database
    /// </summary>
    [CreateAssetMenu(
        fileName = "EquipmentDatabase",
        menuName  = "LightVsDecay/Equipment/Equipment Database",
        order     = 11)]
    public class EquipmentDatabase : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 全局单例引用（由 EquipmentManager 初始化时注入）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public static EquipmentDatabase Instance { get; private set; }

        public static void SetInstance(EquipmentDatabase db) => Instance = db;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("所有装备模板（拖入所有 EquipmentData SO）")]
        [SerializeField] private List<EquipmentData> allEquipments = new List<EquipmentData>();

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时查找表
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private Dictionary<string, EquipmentData> _lookupById;

        /// <summary>初始化查找表</summary>
        public void Initialize()
        {
            _lookupById = new Dictionary<string, EquipmentData>(allEquipments.Count);
            foreach (var eq in allEquipments)
            {
                if (eq == null) continue;
                if (_lookupById.ContainsKey(eq.equipmentId))
                {
                    GameLogger.LogWarning($"[EquipmentDatabase] 重复的 equipmentId: {eq.equipmentId}，请检查 SO 配置");
                    continue;
                }
                _lookupById[eq.equipmentId] = eq;
            }
            GameLogger.Log($"[EquipmentDatabase] 初始化完成，共 {_lookupById.Count} 件装备模板");
        }

        /// <summary>按 ID 查找装备模板</summary>
        public EquipmentData GetById(string id)
        {
            if (_lookupById == null) Initialize();
            return _lookupById.TryGetValue(id, out var data) ? data : null;
        }

        /// <summary>获取指定槽位的所有装备模板</summary>
        public List<EquipmentData> GetBySlot(EquipmentSlotType slot)
        {
            var result = new List<EquipmentData>();
            foreach (var eq in allEquipments)
            {
                if (eq != null && eq.slotType == slot)
                    result.Add(eq);
            }
            return result;
        }

        /// <summary>全部装备模板（只读）</summary>
        public IReadOnlyList<EquipmentData> AllEquipments => allEquipments;

        /// <summary>合成目标（下一个品质的同槽同名装备）</summary>
        public EquipmentData GetMergeResult(EquipmentData source)
        {
            if (source == null) return null;
            ItemRarity next = source.rarity + 1;
            if (next > ItemRarity.Legendary) return null;
            // 同槽位、下一品质，只有1种 → 直接按槽位+品质找
            foreach (var eq in allEquipments)
                if (eq != null && eq.slotType == source.slotType && eq.rarity == next)
                    return eq;
            return null;
        }

#if UNITY_EDITOR
        [ContextMenu("验证所有装备ID唯一性")]
        private void ValidateIds()
        {
            var ids = new HashSet<string>();
            int dupCount = 0;
            foreach (var eq in allEquipments)
            {
                if (eq == null) continue;
                if (!ids.Add(eq.equipmentId))
                {
                    GameLogger.LogError($"重复ID: {eq.equipmentId} ({eq.name})");
                    dupCount++;
                }
            }
            if (dupCount == 0)
                GameLogger.Log($"验证通过：{allEquipments.Count} 件装备，ID全部唯一");
        }
#endif
    }
}