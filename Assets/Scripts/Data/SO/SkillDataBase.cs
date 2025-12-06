// ============================================================
// SkillDatabase.cs
// 文件位置: Assets/Scripts/Data/SO/SkillDatabase.cs
// 用途：管理所有技能配置的数据库（ScriptableObject）
// 核心功能：加权随机抽取、满级剔除、消耗品填充
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
        
        [Tooltip("消耗品出现的基础概率（已废弃，改用权重系统）")]
        [Range(0f, 1f)]
        public float consumableChance = 0.3f;
        
        [Tooltip("被动技能出现的基础概率（已废弃，改用权重系统）")]
        [Range(0f, 1f)]
        public float passiveChance = 0.25f;
        
        [Header("调试")]
        [Tooltip("显示调试日志")]
        public bool showDebugInfo = false;
        
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
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 核心抽取算法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 生成三选一选项（核心算法）
        /// 逻辑：
        /// 1. 从主技能池（未满级的主动+被动）中加权随机抽取
        /// 2. 抽取时不重复
        /// 3. 如果主技能不足3个，用消耗品填充
        /// </summary>
        /// <param name="currentSkillLevels">当前已解锁技能及其等级</param>
        /// <returns>可选择的技能列表（最多3个）</returns>
        public List<SkillData> GenerateChoices(Dictionary<SkillType, int> currentSkillLevels)
        {
            List<SkillData> choices = new List<SkillData>();
            
            if (showDebugInfo)
            {
                Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Debug.Log("[SkillDatabase] 开始生成三选一...");
            }
            
            // ========== Step 1: 构建主技能池（未满级的主动+被动） ==========
            List<SkillData> mainPool = BuildMainPool(currentSkillLevels);
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillDatabase] 主技能池数量: {mainPool.Count}");
                foreach (var skill in mainPool)
                {
                    int lv = currentSkillLevels.GetValueOrDefault(skill.type, 0);
                    int weight = CalculateWeight(skill, lv);
                    Debug.Log($"  - {skill.displayName} (Lv.{lv}/{skill.maxLevel}) 权重:{weight}");
                }
            }
            
            // ========== Step 2: 从主技能池加权随机抽取（最多3个，不重复） ==========
            List<SkillData> tempMainPool = new List<SkillData>(mainPool); // 复制一份用于抽取
            
            while (choices.Count < choiceCount && tempMainPool.Count > 0)
            {
                SkillData selected = WeightedRandomSelect(tempMainPool, currentSkillLevels);
                if (selected != null)
                {
                    choices.Add(selected);
                    tempMainPool.Remove(selected); // 移除已选中的，确保不重复
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"[SkillDatabase] ✓ 抽中主技能: {selected.displayName}");
                    }
                }
                else
                {
                    break;
                }
            }
            
            // ========== Step 3: 如果主技能不足3个，用消耗品填充 ==========
            if (choices.Count < choiceCount)
            {
                int needFill = choiceCount - choices.Count;
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillDatabase] 主技能不足，需要填充 {needFill} 个消耗品");
                }
                
                List<SkillData> consumablePool = new List<SkillData>(consumables);
                
                // 从消耗品池中随机抽取填充（也不重复）
                while (choices.Count < choiceCount && consumablePool.Count > 0)
                {
                    // 消耗品使用简单随机（或也可以用加权）
                    SkillData selected = WeightedRandomSelect(consumablePool, currentSkillLevels);
                    if (selected != null)
                    {
                        choices.Add(selected);
                        consumablePool.Remove(selected); // 确保消耗品也不重复
                        
                        if (showDebugInfo)
                        {
                            Debug.Log($"[SkillDatabase] ✓ 填充消耗品: {selected.displayName}");
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillDatabase] 最终选项数量: {choices.Count}");
                Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            }
            
            return choices;
        }
        
        /// <summary>
        /// 构建主技能池（未满级的主动+被动技能）
        /// </summary>
        private List<SkillData> BuildMainPool(Dictionary<SkillType, int> currentSkillLevels)
        {
            List<SkillData> pool = new List<SkillData>();
            
            // 添加未满级的主动技能
            foreach (var skill in activeSkills)
            {
                if (skill == null) continue;
                
                int currentLevel = currentSkillLevels.GetValueOrDefault(skill.type, 0);
                
                // 满级剔除
                if (currentLevel >= skill.maxLevel)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"[SkillDatabase] 剔除满级技能: {skill.displayName} (Lv.{currentLevel}/{skill.maxLevel})");
                    }
                    continue;
                }
                
                pool.Add(skill);
            }
            
            // 添加未满级的被动技能
            foreach (var skill in passiveSkills)
            {
                if (skill == null) continue;
                
                int currentLevel = currentSkillLevels.GetValueOrDefault(skill.type, 0);
                
                // 满级剔除
                if (currentLevel >= skill.maxLevel)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"[SkillDatabase] 剔除满级技能: {skill.displayName} (Lv.{currentLevel}/{skill.maxLevel})");
                    }
                    continue;
                }
                
                pool.Add(skill);
            }
            
            return pool;
        }
        
        /// <summary>
        /// 加权随机选择（累加权重法）
        /// </summary>
        /// <param name="pool">候选池</param>
        /// <param name="currentSkillLevels">当前技能等级</param>
        /// <returns>选中的技能，如果池为空返回null</returns>
        private SkillData WeightedRandomSelect(List<SkillData> pool, Dictionary<SkillType, int> currentSkillLevels)
        {
            if (pool == null || pool.Count == 0) return null;
            
            // 计算所有候选的权重
            int totalWeight = 0;
            List<int> weights = new List<int>(pool.Count);
            
            foreach (var skill in pool)
            {
                int currentLevel = currentSkillLevels.GetValueOrDefault(skill.type, 0);
                int weight = CalculateWeight(skill, currentLevel);
                
                weights.Add(weight);
                totalWeight += weight;
            }
            
            // 如果总权重为0，返回null
            if (totalWeight <= 0)
            {
                Debug.LogWarning("[SkillDatabase] 总权重为0，无法选择！");
                return null;
            }
            
            // 随机选择
            int randomValue = Random.Range(0, totalWeight);
            int cumulative = 0;
            
            for (int i = 0; i < pool.Count; i++)
            {
                cumulative += weights[i];
                if (randomValue < cumulative)
                {
                    return pool[i];
                }
            }
            
            // 兜底：返回最后一个
            return pool[pool.Count - 1];
        }
        
        /// <summary>
        /// 计算单个技能的权重
        /// 公式: weight = baseWeight + (currentLevel * weightPerLevel)
        /// </summary>
        /// <param name="skill">技能数据</param>
        /// <param name="currentLevel">当前等级</param>
        /// <returns>计算后的权重（最小为1）</returns>
        private int CalculateWeight(SkillData skill, int currentLevel)
        {
            if (skill == null) return 0;
            
            // 基础权重 + 等级加成
            int weight = skill.baseWeight + (currentLevel * skill.weightPerLevel);
            
            // 确保权重至少为1（否则永远选不到）
            return Mathf.Max(1, weight);
        }
        
        /// <summary>
        /// 简化版三选一（纯随机，用于测试）
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
                        Debug.Log($"  - {skill.type}: {skill.displayName} (权重:{skill.baseWeight}, 等级加成:{skill.weightPerLevel})");
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
        
        [ContextMenu("测试抽取（空状态）")]
        public void TestGenerateEmpty()
        {
            showDebugInfo = true;
            var choices = GenerateChoices(new Dictionary<SkillType, int>());
            Debug.Log($"[测试] 抽取结果: {choices.Count} 个技能");
            foreach (var skill in choices)
            {
                Debug.Log($"  - {skill.displayName}");
            }
            showDebugInfo = false;
        }
        
        [ContextMenu("测试抽取（模拟满级场景）")]
        public void TestGenerateWithMaxLevel()
        {
            showDebugInfo = true;
            
            // 模拟大部分技能已满级
            var mockLevels = new Dictionary<SkillType, int>
            {
                { SkillType.Prism, 5 },
                { SkillType.Focus, 5 },
                { SkillType.Impact, 5 },
                { SkillType.Frost, 5 },
                { SkillType.Power, 5 },
                { SkillType.Wide, 4 } // 只有 Wide 未满级
            };
            
            var choices = GenerateChoices(mockLevels);
            Debug.Log($"[测试-满级场景] 抽取结果: {choices.Count} 个技能");
            foreach (var skill in choices)
            {
                Debug.Log($"  - {skill.displayName}");
            }
            showDebugInfo = false;
        }
#endif
    }
}