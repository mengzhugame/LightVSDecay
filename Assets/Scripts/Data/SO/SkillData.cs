// ============================================================
// SkillData.cs
// 文件位置: Assets/Scripts/Data/SO/SkillData.cs
// 用途：技能配置数据（ScriptableObject）
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
        
        // 被动技能（最多5级）- 青色/蓝色卡
        Frost,      // 极寒光束 - 减速辅助
        Power,      // 功率超频 - +DPS
        Wide,       // 广域透镜 - +激光宽度
        
        // 消耗品（无限重复）- 绿色卡
        Repair,     // 紧急抢修 - 回复护盾+1血
        Charge,     // 能量过载 - 大招+50%
        Adrenaline  // 肾上腺素 - 恢复护盾+20秒增益
    }
    
    /// <summary>
    /// 技能类别枚举
    /// </summary>
    public enum SkillCategory
    {
        Active,     // 主动技能
        Passive,    // 被动技能
        Consumable  // 消耗品
    }
    
    /// <summary>
    /// 卡片颜色类型（基于技能功能分类）
    /// </summary>
    public enum SkillCardType
    {
        Attack,     // 红色/橙色 - 主动输出技能 (Prism, Focus, Impact)
        Passive,    // 青色/蓝色 - 被动/控制技能 (Frost, Power, Wide)
        Recovery,   // 绿色 - 消耗品 (Repair, Charge, Adrenaline)
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
        
        [Header("伤害相关")]
        [Tooltip("伤害倍率 (1.0 = 100%)")]
        public float damageMultiplier = 1.0f;
        
        [Header("击退相关")]
        [Tooltip("击退力倍率")]
        public float knockbackMultiplier = 1.0f;
        
        [Tooltip("硬直时间（秒）")]
        public float stunDuration = 0f;
        
        [Header("减速相关")]
        [Tooltip("减速百分比 (0.2 = 20%)")]
        [Range(0f, 1f)]
        public float slowPercent = 0f;
        
        [Tooltip("减速持续时间")]
        public float slowDuration = 0f;
        
        [Tooltip("冰冻概率")]
        [Range(0f, 1f)]
        public float freezeChance = 0f;
        
        [Tooltip("冰冻持续时间")]
        public float freezeDuration = 0f;
        
        [Header("分裂相关（折射棱镜）")]
        [Tooltip("分裂数量")]
        public int splitCount = 0;
        
        [Tooltip("分裂伤害倍率")]
        public float splitDamageMultiplier = 0.3f;
        
        [Tooltip("分裂长度")]
        public float splitLength = 3f;
        
        [Header("宽度相关")]
        [Tooltip("宽度倍率")]
        public float widthMultiplier = 1.0f;
        
        [Header("消耗品效果")]
        [Tooltip("恢复护盾数量")]
        public int shieldRestore = 0;
        
        [Tooltip("恢复生命数量")]
        public int hullRestore = 0;
        
        [Tooltip("大招能量恢复百分比")]
        [Range(0f, 1f)]
        public float ultEnergyPercent = 0f;
        
        [Header("肾上腺素效果")]
        [Tooltip("增益持续时间")]
        public float buffDuration = 0f;
        
        [Tooltip("激光转速加成")]
        public float rotationSpeedBonus = 0f;
        
        [Tooltip("击退力加成")]
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
        
        [Tooltip("技能颜色（用于激光变色等）")]
        [ColorUsage(true, true)]
        public Color skillColor = Color.white;
        
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
        
        /// <summary>
        /// 是否为消耗品
        /// </summary>
        public bool IsConsumable => category == SkillCategory.Consumable;
        
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
            
            // 消耗品设置
            if (category == SkillCategory.Consumable)
            {
                isRepeatable = true;
                maxLevel = 1;
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
                case SkillType.Prism:
                case SkillType.Focus:
                case SkillType.Impact:
                    cardType = SkillCardType.Attack;
                    break;
                    
                // 被动/控制技能 - 蓝色
                case SkillType.Frost:
                case SkillType.Power:
                case SkillType.Wide:
                    cardType = SkillCardType.Passive;
                    break;
                    
                // 消耗品 - 绿色
                case SkillType.Repair:
                case SkillType.Charge:
                case SkillType.Adrenaline:
                    cardType = SkillCardType.Recovery;
                    break;
            }
        }
#endif
    }
}