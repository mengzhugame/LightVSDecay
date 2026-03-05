// ============================================================
// EquipmentData.cs
// 文件位置: Assets/Scripts/Data/SO/EquipmentData.cs
// 用途：装备 ScriptableObject 定义 - 单件装备的数据模板
// ============================================================

using UnityEngine;

namespace LightVsDecay.Data.SO
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 枚举定义
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 装备槽位类型
    /// </summary>
    public enum EquipmentSlotType
    {
        PrismCore   = 0,    // 棱镜核心 - 攻击位，改变塔顶水晶
        TowerBase   = 1,    // 塔身底座 - 生存位，改变底座外形
        Processor   = 2,    // 运算芯片 - 策略位，无视觉变化
    }

    /// <summary>
    /// 装备品质等级（按策划案：绿=优秀, 蓝=精良, 紫=史诗, 橙=传说）
    /// 整数值用于品质比较（越大越高）
    /// </summary>
    public enum ItemRarity
    {
        Common      = 0,    // 白 - 普通
        Uncommon    = 1,    // 绿 - 优秀
        Rare        = 2,    // 蓝 - 精良（解锁副词条1）
        Epic        = 3,    // 紫 - 史诗（解锁副词条2）
        Legendary   = 4,    // 橙 - 传说（激活特殊被动）
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 装备属性结构
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 装备基础属性（基础值，等级倍率在 EquipmentData 中定义）
    /// </summary>
    [System.Serializable]
    public class EquipmentStats
    {
        [Header("攻击属性（棱镜核心主属性）")]
        [Tooltip("基础攻击力加成")]
        public int attackBonus = 0;

        [Header("生存属性（塔身底座主属性）")]
        [Tooltip("最大生命值加成")]
        public int hpBonus = 0;

        [Tooltip("护盾上限加成")]
        public int shieldBonus = 0;

        [Header("策略属性（运算芯片主属性）")]
        [Tooltip("暴击率加成（0.05 = 5%）")]
        public float critBonus = 0f;

        [Tooltip("充能效率加成（0.1 = 10%，影响技能充能速度）")]
        public float chargeBonus = 0f;

        /// <summary>
        /// 按等级计算实际属性（每级+10%）
        /// </summary>
        public EquipmentStats GetScaledStats(int level)
        {
            float multiplier = 1f + (level - 1) * 0.10f;
            return new EquipmentStats
            {
                attackBonus  = Mathf.RoundToInt(attackBonus  * multiplier),
                hpBonus      = Mathf.RoundToInt(hpBonus      * multiplier),
                shieldBonus  = Mathf.RoundToInt(shieldBonus  * multiplier),
                critBonus    = critBonus  * multiplier,
                chargeBonus  = chargeBonus * multiplier,
            };
        }

        /// <summary>两个属性相加（用于合计全身装备）</summary>
        public static EquipmentStats operator +(EquipmentStats a, EquipmentStats b)
        {
            return new EquipmentStats
            {
                attackBonus  = a.attackBonus  + b.attackBonus,
                hpBonus      = a.hpBonus      + b.hpBonus,
                shieldBonus  = a.shieldBonus  + b.shieldBonus,
                critBonus    = a.critBonus    + b.critBonus,
                chargeBonus  = a.chargeBonus  + b.chargeBonus,
            };
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // ScriptableObject 定义
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 装备数据模板（ScriptableObject）
    /// 每件不同的装备对应一个 SO 资产
    /// 创建路径：Assets → Create → LightVsDecay → Equipment → Equipment Data
    /// </summary>
    [CreateAssetMenu(
        fileName = "EquipmentData_New",
        menuName  = "LightVsDecay/Equipment/Equipment Data",
        order     = 10)]
    public class EquipmentData : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 基本信息
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("基本信息")]
        [Tooltip("装备唯一ID（代码中用于存档，请勿修改已有装备的ID）")]
        public string equipmentId = "equip_core_001";

        [Tooltip("装备显示名称")]
        public string displayName = "棱镜核心 I";

        [Tooltip("装备描述")]
        [TextArea(2, 4)]
        public string description = "标准棱镜核心，提供基础攻击力。";

        [Tooltip("装备槽位类型")]
        public EquipmentSlotType slotType = EquipmentSlotType.PrismCore;

        [Tooltip("装备品质")]
        public ItemRarity rarity = ItemRarity.Common;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("基础属性（Lv.1 数值，每级+10%）")]
        public EquipmentStats baseStats = new EquipmentStats();

        [Header("副词条（Rare+ 解锁）")]
        [Tooltip("副词条描述文本（精良+才显示）")]
        [TextArea(1, 3)]
        public string subEffectDescription = "";

        [Tooltip("传说专属被动技能描述")]
        [TextArea(1, 3)]
        public string legendaryPassiveDescription = "";

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 升级配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("升级配置")]
        [Tooltip("最大等级（由品质决定：白10/绿15/蓝20/紫25/橙30）")]
        public int maxLevel = 10;

        [Tooltip("每级升级所需金币（基础值，实际 = base * level）")]
        public int levelUpGoldBase = 100;

        [Tooltip("每级升级所需图纸（基础值，实际 = base * level）")]
        public int levelUpBlueprintBase = 5;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 视觉资产
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("视觉资产")]
        [Tooltip("背包中显示的图标")]
        public Sprite icon;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 便捷方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>获取指定等级的实际属性</summary>
        public EquipmentStats GetStatsAtLevel(int level)
        {
            int clampedLevel = Mathf.Clamp(level, 1, maxLevel);
            return baseStats.GetScaledStats(clampedLevel);
        }

        /// <summary>计算升级到指定等级的金币消耗</summary>
        public int GetLevelUpGoldCost(int currentLevel)
        {
            return levelUpGoldBase * currentLevel;
        }

        /// <summary>计算升级到指定等级的图纸消耗</summary>
        public int GetLevelUpBlueprintCost(int currentLevel)
        {
            return levelUpBlueprintBase * currentLevel;
        }

        /// <summary>是否达到满级</summary>
        public bool IsMaxLevel(int level) => level >= maxLevel;

        /// <summary>获取品质对应的最大等级（静态工具方法）</summary>
        public static int GetMaxLevelForRarity(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common    => 0,   // 仅用于"无装备"默认状态，不创建实际SO
                ItemRarity.Uncommon  => 10,  // 绿
                ItemRarity.Rare      => 20,  // 蓝
                ItemRarity.Epic      => 25,  // 紫
                ItemRarity.Legendary => 30,  // 橙
                _                    => 10,
            };
        }

        private void OnValidate()
        {
            maxLevel = GetMaxLevelForRarity(rarity);

#if UNITY_EDITOR
            if (rarity == ItemRarity.Common)
                Debug.LogWarning($"[EquipmentData] {name}: 品质不应设为 Common，" +
                                 "Common 仅用于无装备时的默认塔外观，请改为 Uncommon 或以上。");
#endif
        }
    }
}