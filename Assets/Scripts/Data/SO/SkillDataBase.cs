// ============================================================
// SkillDatabase.cs
// 文件位置: Assets/Scripts/Data/SO/SkillDatabase.cs
// 用途：管理所有技能配置的数据库（ScriptableObject）
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace LightVsDecay.Data.SO
{
    /// <summary>
    /// 技能数据库 (ScriptableObject)
    /// 统一管理所有技能配置，支持三选一随机选取
    /// </summary>
    [CreateAssetMenu(fileName = "SkillDatabase", menuName = "LightVsDecay/Skill Database", order = 0)]
    public class SkillDatabase : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("主动技能")]
        [SerializeField] private List<SkillData> activeSkills = new List<SkillData>();
        
        [Header("被动技能")]
        [SerializeField] private List<SkillData> passiveSkills = new List<SkillData>();
        
        [Header("消耗品")]
        [SerializeField] private List<SkillData> consumables = new List<SkillData>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 三选一配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("三选一配置")]
        [Tooltip("每次升级提供的选择数量")]
        public int choiceCount = 3;
        
        [Tooltip("消耗品出现的基础概率")]
        [Range(0f, 1f)]
        public float consumableChance = 0.3f;
        
        [Tooltip("被动技能出现的基础概率")]
        [Range(0f, 1f)]
        public float passiveChance = 0.25f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Dictionary<SkillType, SkillData> _cache;
        
        private Dictionary<SkillType, SkillData> Cache
        {
            get
            {
                if (_cache == null) BuildCache();
                return _cache;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取指定类型的技能配置
        /// </summary>
        public SkillData GetData(SkillType type)
        {
            if (Cache.TryGetValue(type, out var data))
            {
                return data;
            }
            Debug.LogWarning($"[SkillDatabase] 未找到 {type} 类型的技能配置！");
            return null;
        }
        
        /// <summary>
        /// 获取所有主动技能
        /// </summary>
        public IReadOnlyList<SkillData> GetActiveSkills() => activeSkills;
        
        /// <summary>
        /// 获取所有被动技能
        /// </summary>
        public IReadOnlyList<SkillData> GetPassiveSkills() => passiveSkills;
        
        /// <summary>
        /// 获取所有消耗品
        /// </summary>
        public IReadOnlyList<SkillData> GetConsumables() => consumables;
        
        /// <summary>
        /// 生成三选一选项
        /// </summary>
        /// <param name="currentSkillLevels">当前已解锁技能及其等级</param>
        /// <returns>可选择的技能列表</returns>
        public List<SkillData> GenerateChoices(Dictionary<SkillType, int> currentSkillLevels)
        {
            List<SkillData> choices = new List<SkillData>();
            List<SkillData> pool = new List<SkillData>();
            
            // 构建可选池
            // 1. 添加未满级的主动技能
            foreach (var skill in activeSkills)
            {
                int currentLevel = currentSkillLevels.GetValueOrDefault(skill.type, 0);
                if (currentLevel < skill.maxLevel)
                {
                    pool.Add(skill);
                }
            }
            
            // 2. 添加未满级的被动技能
            foreach (var skill in passiveSkills)
            {
                int currentLevel = currentSkillLevels.GetValueOrDefault(skill.type, 0);
                if (currentLevel < skill.maxLevel)
                {
                    pool.Add(skill);
                }
            }
            
            // 3. 添加消耗品（始终可选）
            pool.AddRange(consumables);
            
            // 如果池太小，直接返回
            if (pool.Count <= choiceCount)
            {
                return new List<SkillData>(pool);
            }
            
            // 加权随机选择
            while (choices.Count < choiceCount && pool.Count > 0)
            {
                int totalWeight = 0;
                foreach (var skill in pool)
                {
                    int level = currentSkillLevels.GetValueOrDefault(skill.type, 0);
                    int weight = skill.baseWeight + (level * skill.weightPerLevel);
                    totalWeight += Mathf.Max(1, weight);
                }
                
                int randomValue = Random.Range(0, totalWeight);
                int cumulative = 0;
                
                for (int i = 0; i < pool.Count; i++)
                {
                    var skill = pool[i];
                    int level = currentSkillLevels.GetValueOrDefault(skill.type, 0);
                    int weight = Mathf.Max(1, skill.baseWeight + (level * skill.weightPerLevel));
                    cumulative += weight;
                    
                    if (randomValue < cumulative)
                    {
                        choices.Add(skill);
                        pool.RemoveAt(i);
                        break;
                    }
                }
            }
            
            return choices;
        }
        
        /// <summary>
        /// 简化版三选一（纯随机）
        /// </summary>
        public List<SkillData> GenerateRandomChoices()
        {
            return GenerateChoices(new Dictionary<SkillType, int>());
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内部方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void BuildCache()
        {
            _cache = new Dictionary<SkillType, SkillData>();
            
            void AddToCache(IEnumerable<SkillData> skills)
            {
                foreach (var skill in skills)
                {
                    if (skill == null) continue;
                    if (_cache.ContainsKey(skill.type))
                    {
                        Debug.LogWarning($"[SkillDatabase] 重复的技能类型: {skill.type}");
                        continue;
                    }
                    _cache[skill.type] = skill;
                }
            }
            
            AddToCache(activeSkills);
            AddToCache(passiveSkills);
            AddToCache(consumables);
        }
        
        private void OnValidate()
        {
            _cache = null;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器支持
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [ContextMenu("验证数据")]
        public void ValidateData()
        {
            Debug.Log("=== 技能数据库验证 ===");
            
            int totalCount = activeSkills.Count + passiveSkills.Count + consumables.Count;
            Debug.Log($"主动技能: {activeSkills.Count}");
            Debug.Log($"被动技能: {passiveSkills.Count}");
            Debug.Log($"消耗品: {consumables.Count}");
            Debug.Log($"总计: {totalCount}");
            
            // 检查重复
            HashSet<SkillType> types = new HashSet<SkillType>();
            int duplicates = 0;
            
            void CheckDuplicates(IEnumerable<SkillData> skills, string category)
            {
                foreach (var skill in skills)
                {
                    if (skill == null)
                    {
                        Debug.LogError($"[{category}] 存在空引用！");
                        continue;
                    }
                    
                    if (types.Contains(skill.type))
                    {
                        Debug.LogWarning($"[{category}] 重复类型: {skill.type}");
                        duplicates++;
                    }
                    else
                    {
                        types.Add(skill.type);
                        Debug.Log($"  - {skill.type}: {skill.displayName}");
                    }
                }
            }
            
            Debug.Log("\n--- 主动技能 ---");
            CheckDuplicates(activeSkills, "主动技能");
            
            Debug.Log("\n--- 被动技能 ---");
            CheckDuplicates(passiveSkills, "被动技能");
            
            Debug.Log("\n--- 消耗品 ---");
            CheckDuplicates(consumables, "消耗品");
            
            Debug.Log($"\n=== 验证完成: {duplicates} 个重复 ===");
        }
#endif
    }
}