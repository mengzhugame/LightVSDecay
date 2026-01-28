// ============================================================
// SkillData.cs
// 文件位置: Assets/Scripts/Data/SO/SkillData.cs
// 用途：技能配置数据（ScriptableObject）
// 修改：Step 1 - 新增 Focus 爆炸、颜色配置、冰冻阈值等字段
// ============================================================

using UnityEngine;

namespace LightVsDecay.Data.SO
{
    /// <summary>
    /// 技能类型枚举
    /// </summary>
    public enum SkillType
    {
        // 主动技能（最多5级）- 红色/橙色卡
        Prism,      // 折射棱镜 - AOE清怪
        Focus,      // 聚能透镜 - 单体攻坚
        Impact,     // 冲击模块 - 控制/防近身
        Chain,     // 连锁反应 - 激光传导（替代原 Reflex）
        Frost,      // 极寒光束 - 减速辅助
        // 被动技能（最多5级）- 青色/蓝色卡
        Power,      // 功率超频 - +DPS
        Wide,       // 广域透镜 - +激光宽度
        Crit,       // 致命暴击 - +暴击率
        Shatter,    // 数据破碎 - 对受损敌人额外伤害
    }
    
    /// <summary>
    /// 技能类别枚举
    /// </summary>
    public enum SkillCategory
    {
        Active,     // 主动技能
        Passive,    // 被动技能
    }
    
    /// <summary>
    /// 卡片颜色类型（基于技能功能分类）
    /// </summary>
    public enum SkillCardType
    {
        Attack,     // 红色/橙色 - 主动输出技能 (Prism, Focus, Impact)
        Passive,    // 青色/蓝色 - 被动/控制技能 (Frost, Power, Wide)
        // 绿色 - 消耗品 (Repair, Charge, Adrenaline)
        MaxLevel    // 金色 - 满级技能（运行时判断）
    }
    
    /// <summary>
    /// 技能等级数据
    /// </summary>
    [System.Serializable]
    public class SkillLevelData
    {
        [Tooltip("等级描述")]
        [TextArea(1, 2)]
        public string description;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害相关
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("伤害相关")]
        [Tooltip("伤害倍率 (1.0 = 100%)")]
        public float damageMultiplier = 1.0f;
        
        [Tooltip("对BOSS额外伤害倍率 (0.2 = +20%)【新增】")]
        public float bossDamageBonus = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 击退相关（冲击模块）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("击退相关（冲击模块）")]
        [Tooltip("击退力倍率")]
        public float knockbackMultiplier = 1.0f;
// 【新增】碎冰相关（冲击模块）
        [Header("碎冰相关（冲击模块）")]
        [Tooltip("碎冰伤害倍率 (1.5 = +50% 额外伤害)")]
        public float shatterDamageMultiplier = 1.0f;

        [Tooltip("是否启用终极粉碎（LV5秒杀冰冻普通怪）")]
        public bool enableExecution = false;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 减速/冰冻相关（极寒光束）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("减速相关（极寒光束）")]
        [Tooltip("减速百分比 (0.2 = 20%)")]
        [Range(0f, 1f)]
        public float slowPercent = 0f;
        
        [Tooltip("减速持续时间")]
        public float slowDuration = 0f;
        
        [Tooltip("冰冻触发时间阈值（持续照射X秒后触发）【新增】")]
        public float freezeThreshold = 0f;
        
        [Tooltip("冰冻持续时间")]
        public float freezeDuration = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 分裂相关（折射棱镜）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("分裂相关（折射棱镜）")]
        [Tooltip("分裂数量")]
        public int splitCount = 0;
        
        [Tooltip("分裂伤害倍率")]
        public float splitDamageMultiplier = 0.3f;
        
        [Tooltip("分裂长度")]
        public float splitLength = 8f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 宽度相关（广域透镜 / 聚能透镜）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("宽度相关")]
        [Tooltip("宽度倍率 (1.4 = +40%)")]
        public float widthMultiplier = 1.0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 连锁反应相关（Chain）- 替代原 Reflex
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("连锁反应相关（Chain）")]
        [Tooltip("主激光传导跳数")]
        public int chainBounces = 1;
        
        [Tooltip("传导距离（米）")]
        public float chainRange = 3f;
        
        [Tooltip("伤害衰减率（每跳衰减，0.2 = 20%）")]
        [Range(0f, 0.5f)]
        public float chainDamageDecay = 0.2f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 暴击相关（致命暴击）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("暴击相关（致命暴击）")]
        [Tooltip("暴击率加成 (0.05 = +5%)")]
        [Range(0f, 0.5f)]
        public float critRateBonus = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 穿透相关（聚能透镜 Lv5）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        [Header("Focus穿透相关")]
        [Tooltip("穿透敌人数量（0=不穿透，-1=无限穿透）")]
        public int penetrationCount = 0;

        [Tooltip("穿透伤害衰减率（每穿透一个敌人伤害衰减的比例，0.1=10%）")]
        [Range(0f, 0.5f)]
        public float penetrationDecay = 0.1f;

        [Tooltip("对Boss造成真实伤害（无视护甲+无视连体Buff）")]
        public bool dealsTrueDamageToBoss = false;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 数据破碎相关（Shatter）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("数据破碎相关（Shatter）")]
        [Tooltip("对受损敌人的额外伤害加成 (0.3 = +30%)")]
        public float shatterDamageBonus = 0f;
        
        [Tooltip("击杀受损敌人时是否触发漏洞扩散爆炸（LV5）")]
        public bool shatterExplosionOnKill = false;
        
        [Tooltip("漏洞扩散爆炸半径")]
        public float shatterExplosionRadius = 2f;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 其他
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("其他")]
        [Tooltip("激光转速加成")]
        public float rotationSpeedBonus = 0f;
        
        [Tooltip("击退力加成（旧字段，保留兼容）")]
        public float knockbackBonus = 0f;
    }
    
    /// <summary>
    /// 技能配置 (ScriptableObject)
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_New", menuName = "LightVsDecay/Skill Data", order = 1)]
    public class SkillData : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 基础信息
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("基础信息")]
        [Tooltip("技能类型（唯一标识）")]
        public SkillType type;
        
        [Tooltip("技能类别")]
        public SkillCategory category;
        
        [Tooltip("卡片颜色类型")]
        public SkillCardType cardType = SkillCardType.Attack;
        
        [Tooltip("显示名称")]
        public string displayName = "新技能";
        
        [Tooltip("技能图标")]
        public Sprite icon;
        
        [Tooltip("技能描述")]
        [TextArea(2, 4)]
        public string description;
        
        [Header("颜色设置")]
        [Tooltip("是否修改激光/VFX颜色")]
        public bool changeColor = false;

        [Tooltip("技能颜色（HDR，用于激光和VFX变色）")]
        [ColorUsage(true, true)]
        public Color skillColor = new Color(0f, 3f, 3f, 1f); // 默认青
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 等级设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("等级设置")]
        [Tooltip("最大等级（主动/被动=5，消耗品=无限）")]
        public int maxLevel = 5;
        
        [Tooltip("是否可无限重复获取（消耗品）")]
        public bool isRepeatable = false;
        
        [Tooltip("各等级数据")]
        public SkillLevelData[] levelData = new SkillLevelData[5];
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 选择权重
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("选择权重")]
        [Tooltip("在三选一中出现的基础权重")]
        [Min(0)]
        public int baseWeight = 100;
        
        [Tooltip("每升一级后权重变化")]
        public int weightPerLevel = 0;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 便捷方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取指定等级的数据（1-based）
        /// </summary>
        public SkillLevelData GetLevelData(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, levelData.Length - 1);
            return levelData[index];
        }
        
        /// <summary>
        /// 获取指定等级的描述文本
        /// </summary>
        public string GetLevelDescription(int level)
        {
            var data = GetLevelData(level);
            return data?.description ?? description;
        }
        
        /// <summary>
        /// 是否为主动技能
        /// </summary>
        public bool IsActive => category == SkillCategory.Active;
        
        /// <summary>
        /// 是否为被动技能
        /// </summary>
        public bool IsPassive => category == SkillCategory.Passive;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器支持
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // 确保等级数据数组长度正确
            if (levelData == null || levelData.Length != maxLevel)
            {
                var newData = new SkillLevelData[maxLevel];
                for (int i = 0; i < maxLevel; i++)
                {
                    if (levelData != null && i < levelData.Length)
                    {
                        newData[i] = levelData[i];
                    }
                    else
                    {
                        newData[i] = new SkillLevelData();
                    }
                }
                levelData = newData;
            }

            // 自动设置卡片类型
            AutoSetCardType();
        }
        
        /// <summary>
        /// 根据技能类型自动设置卡片颜色
        /// </summary>
        private void AutoSetCardType()
        {
            switch (type)
            {
                // 主动输出技能 - 红色
                case SkillType.Prism:   // 分裂棱镜
                case SkillType.Focus:   // 聚能透镜
                case SkillType.Impact:  // 冲击模块
                case SkillType.Chain:  // 反射透镜
                case SkillType.Frost:   // 极寒光束
                    cardType = SkillCardType.Attack;
                    break;
                    
                // 被动/控制技能 - 蓝色
                case SkillType.Power:
                case SkillType.Wide:
                case SkillType.Crit: 
                case SkillType.Shatter:  // 【新增】数据破碎 - 被动技能
                    cardType = SkillCardType.Passive;
                    break;
            }
        }
#endif
    }
}