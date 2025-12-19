// ============================================================
// WaveConfig.cs (重构版 v2.0)
// 文件位置: Assets/Scripts/Data/SO/WaveConfig.cs
// 用途：波次配置数据（ScriptableObject）
// 更新：支持间隔刷怪、精英怪标记、心跳感节奏设计
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Core.Pool;

namespace LightVsDecay.Data.SO
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 枚举定义
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 生成区域枚举
    /// </summary>
    public enum SpawnZone
    {
        AllEdges,       // 所有边缘
        TopOnly,        // 仅上方
        TopRandom,      // 上方随机
        SideRandom,     // 两侧随机
        BottomCorners,  // 底部角落
        LeftSide,       // 左侧
        RightSide       // 右侧
    }
    
    /// <summary>
    /// 刷怪模式枚举（预设节奏）
    /// </summary>
    public enum SpawnPattern
    {
        Trickle,    // 涓流: interval=2.0s - 教学用，给玩家反应时间
        Normal,     // 普通: interval=1.0s - 正常节奏
        Burst,      // 连发: interval=0.2s - 瞬间出来一队，考验瞬间输出
        Swarm,      // 蜂群: interval=0.5s - 高密度压制
        Flood,      // 洪水: interval=0.1s - 极限割草爽感
        Instant     // 瞬间: interval=0   - 一次性全部生成
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 刷怪组配置（单次刷怪事件）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 单个刷怪组
    /// 定义在波次中某个时间点生成一批敌人
    /// </summary>
    [System.Serializable]
    public class SpawnGroup
    {
        [Header("时间轴")]
        [Tooltip("相对于波次开始的生成时间（秒）")]
        public float spawnTime = 0f;
        
        [Header("敌人配置")]
        [Tooltip("敌人类型")]
        public EnemyType enemyType = EnemyType.Slime;
        
        [Tooltip("生成数量")]
        [Range(1, 50)]
        public int count = 3;
        
        [Header("刷怪节奏（新增）")]
        [Tooltip("刷怪模式预设")]
        public SpawnPattern pattern = SpawnPattern.Normal;
        
        [Tooltip("自定义间隔（秒）- 设为 -1 时使用 pattern 预设值")]
        [Range(-1f, 5f)]
        public float customInterval = -1f;
        
        [Header("位置与属性")]
        [Tooltip("生成区域")]
        public SpawnZone spawnZone = SpawnZone.AllEdges;
        
        [Tooltip("血量倍率（用于难度调整）")]
        [Range(0.5f, 10f)]
        public float healthMultiplier = 1f;
        
        [Tooltip("速度倍率")]
        [Range(0.5f, 3f)]
        public float speedMultiplier = 1f;
        
        [Tooltip("伤害倍率")]
        [Range(0.5f, 3f)]
        public float damageMultiplier = 1f;
        
        [Header("精英标记（新增）")]
        [Tooltip("是否为精英怪（高血量+高伤害+特殊视觉）")]
        public bool isElite = false;
        
        [Tooltip("精英怪血量倍率（叠加在 healthMultiplier 之上）")]
        [Range(1f, 10f)]
        public float eliteHealthMultiplier = 3f;
        
        [Tooltip("精英怪伤害倍率")]
        [Range(1f, 5f)]
        public float eliteDamageMultiplier = 2f;
        
        // 运行时标记（不序列化）
        [System.NonSerialized]
        public bool hasSpawned = false;
        
        /// <summary>
        /// 获取实际刷怪间隔（秒）
        /// </summary>
        public float GetInterval()
        {
            // 优先使用自定义间隔
            if (customInterval >= 0f)
            {
                return customInterval;
            }
            
            // 使用预设模式
            return pattern switch
            {
                SpawnPattern.Trickle => 2.0f,
                SpawnPattern.Normal => 1.0f,
                SpawnPattern.Burst => 0.2f,
                SpawnPattern.Swarm => 0.5f,
                SpawnPattern.Flood => 0.1f,
                SpawnPattern.Instant => 0f,
                _ => 1.0f
            };
        }
        
        /// <summary>
        /// 获取最终血量倍率（考虑精英加成）
        /// </summary>
        public float GetFinalHealthMultiplier()
        {
            return isElite ? healthMultiplier * eliteHealthMultiplier : healthMultiplier;
        }
        
        /// <summary>
        /// 获取最终伤害倍率（考虑精英加成）
        /// </summary>
        public float GetFinalDamageMultiplier()
        {
            return isElite ? damageMultiplier * eliteDamageMultiplier : damageMultiplier;
        }
        
        /// <summary>
        /// 获取本组刷怪总耗时（从第一只到最后一只）
        /// </summary>
        public float GetTotalDuration()
        {
            if (count <= 1) return 0f;
            return (count - 1) * GetInterval();
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 单波配置
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 单波配置数据
    /// 定义一波的所有刷怪组和属性
    /// </summary>
    [System.Serializable]
    public class WaveData
    {
        [Header("波次信息")]
        [Tooltip("波次编号（1-12）")]
        public int waveNumber = 1;
        
        [Tooltip("波次名称（显示用）")]
        public string displayName = "Wave 1";
        
        [Tooltip("波次描述")]
        [TextArea(1, 2)]
        public string description = "";
        
        [Header("刷怪配置")]
        [Tooltip("本波所有刷怪组")]
        public List<SpawnGroup> spawnGroups = new List<SpawnGroup>();
        
        [Header("难度设置")]
        [Tooltip("全局难度倍率（影响所有怪物）")]
        [Range(0.5f, 3f)]
        public float difficultyMultiplier = 1f;
        
        [Header("特殊标记")]
        [Tooltip("是否为BOSS波")]
        public bool isBossWave = false;
        
        [Tooltip("波次开始提示文本")]
        public string hintText = "";
        
        /// <summary>
        /// 计算本波总敌人数
        /// </summary>
        public int TotalEnemyCount
        {
            get
            {
                int total = 0;
                foreach (var group in spawnGroups)
                {
                    total += group.count;
                }
                return total;
            }
        }
        
        /// <summary>
        /// 获取本波最后一组的生成结束时间（考虑间隔生成）
        /// </summary>
        public float LastSpawnEndTime
        {
            get
            {
                float maxEndTime = 0f;
                foreach (var group in spawnGroups)
                {
                    float endTime = group.spawnTime + group.GetTotalDuration();
                    if (endTime > maxEndTime)
                        maxEndTime = endTime;
                }
                return maxEndTime;
            }
        }
        
        /// <summary>
        /// 重置所有刷怪组的状态
        /// </summary>
        public void ResetSpawnStates()
        {
            foreach (var group in spawnGroups)
            {
                group.hasSpawned = false;
            }
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 波次配置 ScriptableObject
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 波次配置 (ScriptableObject)
    /// 定义整局游戏的 12 波敌人配置
    /// </summary>
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "LightVsDecay/Wave Config", order = 3)]
    public class WaveConfig : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 全局设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("全局设置")]
        [Tooltip("总波次数")]
        public int totalWaves = 12;
        
        [Tooltip("波次间隔时间（秒）")]
        public float waveInterval = 10f;
        
        [Tooltip("全局敌人上限")]
        public int globalEnemyLimit = 200;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 波次配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("波次配置")]
        [Tooltip("所有波次的配置")]
        public List<WaveData> waves = new List<WaveData>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BOSS 设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("BOSS设置")]
        [Tooltip("BOSS生命值")]
        public float bossHealth = 30000f;
        
        [Tooltip("BOSS移动速度")]
        public float bossMoveSpeed = 0.2f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 便捷方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取指定波次的配置
        /// </summary>
        /// <param name="waveNumber">波次编号（1-based）</param>
        public WaveData GetWave(int waveNumber)
        {
            int index = waveNumber - 1;
            if (index >= 0 && index < waves.Count)
            {
                return waves[index];
            }
            
            Debug.LogWarning($"[WaveConfig] 波次 {waveNumber} 不存在！");
            return null;
        }
        
        /// <summary>
        /// 检查是否为BOSS波
        /// </summary>
        public bool IsBossWave(int waveNumber)
        {
            var wave = GetWave(waveNumber);
            return wave != null && wave.isBossWave;
        }
        
        /// <summary>
        /// 获取波次总数
        /// </summary>
        public int WaveCount => waves.Count;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器支持
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [ContextMenu("生成默认12波配置（心跳节奏版）")]
        public void GenerateDefaultConfig()
        {
            waves.Clear();
            
            // ══════════════════════════════════════════════════════════════
            // 第一阶段：教学期 (Wave 1-3)
            // 节奏特征：线性涓流 (Linear)，让玩家适应瞄准
            // ══════════════════════════════════════════════════════════════
            
            // Wave 1: 纯 Slime 入门
            waves.Add(CreateWave(1, "初见", "极慢，让玩家试射激光", 1.0f, false,
                // 0s: Trickle 涓流，5只，2.0s间隔
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 5, 
                    pattern = SpawnPattern.Trickle, spawnZone = SpawnZone.TopOnly },
                // 15s: Trickle，5只，1.5s间隔
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Slime, count = 5, 
                    pattern = SpawnPattern.Trickle, customInterval = 1.5f, spawnZone = SpawnZone.TopRandom },
                // 30s: Normal，10只，1.0s间隔
                new SpawnGroup { spawnTime = 30f, enemyType = EnemyType.Slime, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.AllEdges }
            ));
            
            // Wave 2: 引入 Drifter (Fast/狗)
            waves.Add(CreateWave(2, "热身", "引入速度变化，需要快速划动", 1.0f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Drifter, count = 5, 
                    pattern = SpawnPattern.Burst, customInterval = 0.8f, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Slime, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.AllEdges }
            ));
            
            // Wave 3: 教学收尾
            waves.Add(CreateWave(3, "试炼", "混合练习", 1.0f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 8, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 10f, enemyType = EnemyType.Drifter, count = 8, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 20f, enemyType = EnemyType.Slime, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.AllEdges }
            ));
            
            // ══════════════════════════════════════════════════════════════
            // 第二阶段：成长期 (Wave 4-6)
            // 节奏特征：脉冲式 (Pulse)，平稳期中间穿插"危机点"
            // ══════════════════════════════════════════════════════════════
            
            // Wave 4: 首次 Rusher
            waves.Add(CreateWave(4, "冲锋", "⚡ 第一次偷袭！Rusher来袭！", 1.1f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 10f, enemyType = EnemyType.Rusher, count = 2, 
                    pattern = SpawnPattern.Burst, customInterval = 0.2f, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Slime, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 30f, enemyType = EnemyType.Rusher, count = 3, 
                    pattern = SpawnPattern.Burst, customInterval = 0.5f, spawnZone = SpawnZone.SideRandom }
            ));
            
            // Wave 5: Rusher 压力测试
            waves.Add(CreateWave(5, "突袭", "Rusher密度增加", 1.2f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 8f, enemyType = EnemyType.Rusher, count = 3, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.LeftSide },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Drifter, count = 6, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.RightSide },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Rusher, count = 4, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.SideRandom }
            ));
            
            // Wave 6: 精英登场 - Elite Tank
            waves.Add(CreateWave(6, "精英", "🛡 Elite Tank 出现！", 1.3f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 2, 
                    pattern = SpawnPattern.Normal, customInterval = 1.5f, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 35f, enemyType = EnemyType.Tank, count = 1, 
                    pattern = SpawnPattern.Instant, spawnZone = SpawnZone.TopOnly,
                    isElite = true, eliteHealthMultiplier = 5f, eliteDamageMultiplier = 2f },
                new SpawnGroup { spawnTime = 40f, enemyType = EnemyType.Rusher, count = 4, 
                    pattern = SpawnPattern.Burst, customInterval = 0.5f, spawnZone = SpawnZone.SideRandom }
            ));
            
            // ══════════════════════════════════════════════════════════════
            // 第三阶段：高压期 (Wave 7-9)
            // 节奏特征：波浪式 (Waves)
            // ══════════════════════════════════════════════════════════════
            
            // Wave 7: 三军混战
            waves.Add(CreateWave(7, "重甲", "🛡 Tank 成群", 1.4f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 3, 
                    pattern = SpawnPattern.Normal, customInterval = 2f, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 5f, enemyType = EnemyType.Slime, count = 15, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Rusher, count = 4, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Tank, count = 2, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopRandom }
            ));
            
            // Wave 8: 精英 Phantom
            waves.Add(CreateWave(8, "乱战", "满屏乱飞", 1.5f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Drifter, count = 5, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 5f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Burst, customInterval = 0.3f, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 20f, enemyType = EnemyType.Drifter, count = 1, 
                    pattern = SpawnPattern.Instant, spawnZone = SpawnZone.TopOnly,
                    isElite = true, eliteHealthMultiplier = 4f, speedMultiplier = 1.5f },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom }
            ));
            
            // Wave 9: 高压测试
            waves.Add(CreateWave(9, "炼狱", "极限压力测试", 1.6f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 4, 
                    pattern = SpawnPattern.Normal, customInterval = 1.5f, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 5f, enemyType = EnemyType.Rusher, count = 4, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.LeftSide },
                new SpawnGroup { spawnTime = 5f, enemyType = EnemyType.Rusher, count = 4, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.RightSide },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Rusher, count = 6, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.SideRandom }
            ));
            
            // ══════════════════════════════════════════════════════════════
            // 第四阶段：疯狂期 (Wave 10-11)
            // 节奏特征：洪水 (Flood)
            // ══════════════════════════════════════════════════════════════
            
            // Wave 10: 大规模混战
            waves.Add(CreateWave(10, "狂潮", "🔥 全面进攻！", 1.8f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 5, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 3f, enemyType = EnemyType.Rusher, count = 8, 
                    pattern = SpawnPattern.Burst, customInterval = 0.3f, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 10f, enemyType = EnemyType.Slime, count = 30, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 20f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 30f, enemyType = EnemyType.Rusher, count = 6, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.AllEdges }
            ));
            
            // Wave 11: 最终试炼
            waves.Add(CreateWave(11, "死守", "💀 激光割草！", 2.0f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 5, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 5f, enemyType = EnemyType.Rusher, count = 10, 
                    pattern = SpawnPattern.Burst, customInterval = 0.3f, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Slime, count = 50, 
                    pattern = SpawnPattern.Flood, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 30f, enemyType = EnemyType.Tank, count = 5, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 35f, enemyType = EnemyType.Rusher, count = 10, 
                    pattern = SpawnPattern.Burst, customInterval = 0.3f, spawnZone = SpawnZone.SideRandom }
            ));
            
            // ══════════════════════════════════════════════════════════════
            // Wave 12: BOSS 战
            // 特殊：WaveManager 不刷怪，只生成 BOSS
            // ══════════════════════════════════════════════════════════════
            
            waves.Add(CreateWave(12, "决战", "👹 污染之核降临！", 2.5f, true
                // 无 SpawnGroup！BOSS 战不使用时间轴刷怪
            ));
            
            totalWaves = waves.Count;
            
            Debug.Log($"[WaveConfig] 默认配置已生成！共 {waves.Count} 波");
        }
        
        private WaveData CreateWave(int number, string name, string hint, float difficulty, bool isBoss, params SpawnGroup[] groups)
        {
            var wave = new WaveData
            {
                waveNumber = number,
                displayName = name,
                description = hint,
                hintText = hint,
                difficultyMultiplier = difficulty,
                isBossWave = isBoss,
                spawnGroups = new List<SpawnGroup>(groups)
            };
            return wave;
        }
        
        [ContextMenu("验证配置")]
        public void ValidateConfig()
        {
            Debug.Log("=== 波次配置验证 ===");
            
            int errorCount = 0;
            int totalEnemies = 0;
            
            for (int i = 0; i < waves.Count; i++)
            {
                var wave = waves[i];
                
                if (wave.waveNumber != i + 1)
                {
                    Debug.LogWarning($"[{i}] 波次编号不匹配: 预期 {i + 1}, 实际 {wave.waveNumber}");
                    errorCount++;
                }
                
                int waveEnemies = wave.TotalEnemyCount;
                totalEnemies += waveEnemies;
                
                int eliteCount = 0;
                foreach (var group in wave.spawnGroups)
                {
                    if (group.isElite) eliteCount += group.count;
                }
                
                string eliteStr = eliteCount > 0 ? $", 精英x{eliteCount}" : "";
                
                Debug.Log($"[Wave {wave.waveNumber}] {wave.displayName}: " +
                          $"{wave.spawnGroups.Count} 组, {waveEnemies} 敌人{eliteStr}, " +
                          $"难度x{wave.difficultyMultiplier:F1}, BOSS={wave.isBossWave}");
            }
            
            Debug.Log($"=== 验证完成: {waves.Count} 波, {totalEnemies} 总敌人, {errorCount} 个错误 ===");
        }
#endif
    }
}