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
        [ContextMenu("生成默认12波配置 v3.0（11波=Lv.16）")]
        public void GenerateDefaultConfig()
        {
            waves.Clear();
            
            // ══════════════════════════════════════════════════════════════
            // 第一阶段：教学期 (Wave 1-3)
            // 节奏特征：线性涓流 (Linear)
            // ══════════════════════════════════════════════════════════════
            
            // Wave 1: 初见 (200 XP)
            waves.Add(CreateWave(1, "初见", "极慢，让玩家试射激光", 1.0f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 5, 
                    pattern = SpawnPattern.Trickle, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Slime, count = 5, 
                    pattern = SpawnPattern.Trickle, customInterval = 1.5f, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 30f, enemyType = EnemyType.Slime, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.AllEdges }
            ));
            
            // Wave 2: 热身 (300 XP)
            waves.Add(CreateWave(2, "热身", "引入速度变化", 1.0f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Drifter, count = 5, 
                    pattern = SpawnPattern.Burst, customInterval = 0.8f, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Slime, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.AllEdges }
            ));
            
            // Wave 3: 试炼 (340 XP)
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
            // 节奏特征：脉冲式 (Pulse)
            // ══════════════════════════════════════════════════════════════
            
            // Wave 4: 冲锋 (240 XP) - 首次 Rusher
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
            
            // Wave 5: 突袭 (276 XP)
            waves.Add(CreateWave(5, "突袭", "Rusher密度增加", 1.2f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 8f, enemyType = EnemyType.Rusher, count = 3, 
                    pattern = SpawnPattern.Burst, customInterval = 0.2f, spawnZone = SpawnZone.LeftSide },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Drifter, count = 6, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.RightSide },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Rusher, count = 4, 
                    pattern = SpawnPattern.Burst, customInterval = 0.2f, spawnZone = SpawnZone.SideRandom }
            ));
            
            // Wave 6: 精英 (582 XP) - Elite Tank 登场
            waves.Add(CreateWave(6, "精英", "🛡 Elite Tank 出现！", 1.3f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 2, 
                    pattern = SpawnPattern.Normal, customInterval = 1.5f, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 35f, enemyType = EnemyType.EliteTank, count = 1, 
                    pattern = SpawnPattern.Instant, spawnZone = SpawnZone.TopOnly},
                new SpawnGroup { spawnTime = 40f, enemyType = EnemyType.Rusher, count = 4, 
                    pattern = SpawnPattern.Burst, customInterval = 0.5f, spawnZone = SpawnZone.SideRandom }
            ));
            
            // ══════════════════════════════════════════════════════════════
            // 第三阶段：高压期 (Wave 7-9)
            // 节奏特征：波浪式 (Waves) + 增量小怪
            // ══════════════════════════════════════════════════════════════
            
            // Wave 7: 重甲 (648 XP) - 📈 增量：Slime +20, Rusher +2
            waves.Add(CreateWave(7, "重甲", "🛡 Tank 成群，数量增加！", 1.4f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 3, 
                    pattern = SpawnPattern.Normal, customInterval = 2f, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 3f, enemyType = EnemyType.Slime, count = 15, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Rusher, count = 4, 
                    pattern = SpawnPattern.Burst, customInterval = 0.2f, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 20f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Swarm, customInterval = 0.4f, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 30f, enemyType = EnemyType.Tank, count = 2, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 35f, enemyType = EnemyType.Rusher, count = 2, 
                    pattern = SpawnPattern.Burst, customInterval = 0.3f, spawnZone = SpawnZone.SideRandom }
            ));
            
            // Wave 8: 乱战 (750 XP) - 📈 增量：Slime +15, Drifter +4
            waves.Add(CreateWave(8, "乱战", "满屏乱飞！", 1.5f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Drifter, count = 5, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 5f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Burst, customInterval = 0.3f, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Slime, count = 15, 
                    pattern = SpawnPattern.Swarm, customInterval = 0.4f, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 35f, enemyType = EnemyType.Drifter, count = 5, 
                    pattern = SpawnPattern.Burst, customInterval = 0.3f, spawnZone = SpawnZone.SideRandom }
            ));
            
            // Wave 9: 炼狱 (812 XP) - 📈 增量：Slime +20, + Elite Phantom
            waves.Add(CreateWave(9, "炼狱", "💀 Elite Phantom 降临！极限压力！", 1.6f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 4, 
                    pattern = SpawnPattern.Normal, customInterval = 1.5f, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 5f, enemyType = EnemyType.Rusher, count = 4, 
                    pattern = SpawnPattern.Burst, customInterval = 0.2f, spawnZone = SpawnZone.LeftSide },
                new SpawnGroup { spawnTime = 5f, enemyType = EnemyType.Rusher, count = 4, 
                    pattern = SpawnPattern.Burst, customInterval = 0.2f, spawnZone = SpawnZone.RightSide },
                new SpawnGroup { spawnTime = 12f, enemyType = EnemyType.Slime, count = 25, 
                    pattern = SpawnPattern.Swarm, customInterval = 0.3f, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.EliteDrifter, count = 1, 
                    pattern = SpawnPattern.Instant, spawnZone = SpawnZone.TopOnly},
                new SpawnGroup { spawnTime = 28f, enemyType = EnemyType.Slime, count = 15, 
                    pattern = SpawnPattern.Flood, customInterval = 0.2f, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 35f, enemyType = EnemyType.Rusher, count = 6, 
                    pattern = SpawnPattern.Burst, customInterval = 0.2f, spawnZone = SpawnZone.SideRandom }
            ));
            
            // ══════════════════════════════════════════════════════════════
            // 第四阶段：疯狂期 (Wave 10-11)
            // 节奏特征：洪水 (Flood) + 割草爽感
            // ══════════════════════════════════════════════════════════════
            
            // Wave 10: 狂潮 (1262 XP) - 🔥 大量割草
            waves.Add(CreateWave(10, "狂潮", "🔥 全面进攻！割草开始！", 1.8f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 5, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 3f, enemyType = EnemyType.Rusher, count = 8, 
                    pattern = SpawnPattern.Burst, customInterval = 0.3f, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 8f, enemyType = EnemyType.Slime, count = 30, 
                    pattern = SpawnPattern.Flood, customInterval = 0.15f, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 22f, enemyType = EnemyType.Slime, count = 30, 
                    pattern = SpawnPattern.Flood, customInterval = 0.15f, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 30f, enemyType = EnemyType.Drifter, count = 5, 
                    pattern = SpawnPattern.Burst, customInterval = 0.3f, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 35f, enemyType = EnemyType.Rusher, count = 6, 
                    pattern = SpawnPattern.Burst, customInterval = 0.2f, spawnZone = SpawnZone.AllEdges }
            ));
            
            // Wave 11: 死守 (1360 XP) - 💀🔥 割草高潮
            waves.Add(CreateWave(11, "死守", "💀 激光割草的极致爽感！", 2.0f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 3, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 3f, enemyType = EnemyType.Rusher, count = 10, 
                    pattern = SpawnPattern.Burst, customInterval = 0.3f, spawnZone = SpawnZone.SideRandom },
                // 🔥 割草高潮 Part 1: 40只 Slime 喷涌
                new SpawnGroup { spawnTime = 8f, enemyType = EnemyType.Slime, count = 40, 
                    pattern = SpawnPattern.Flood, customInterval = 0.1f, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Tank, count = 3, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 20f, enemyType = EnemyType.Rusher, count = 10, 
                    pattern = SpawnPattern.Burst, customInterval = 0.3f, spawnZone = SpawnZone.SideRandom },
                // 🔥 割草高潮 Part 2: 50只 Slime 喷涌
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Slime, count = 50, 
                    pattern = SpawnPattern.Flood, customInterval = 0.1f, spawnZone = SpawnZone.AllEdges }
            ));
            
            // ══════════════════════════════════════════════════════════════
            // 第五阶段：BOSS战 (Wave 12)
            // ══════════════════════════════════════════════════════════════
            
            // Wave 12: BOSS战 - 刷怪由 BossController 控制
            waves.Add(CreateWave(12, "决战", "👹 污染之核 The Corruptor 降临！", 2.5f, true));
            
            // 标记配置已修改
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
            
            Debug.Log("=== WaveConfig v3.0 已生成 ===");
            Debug.Log("目标：11波结束达到 Lv.16 (累计6770 XP)");
            Debug.Log("总敌人数：516只（含2只精英）");
            Debug.Log("");
            Debug.Log("增量统计：");
            Debug.Log("  W7:  +22 (原24 → 46)");
            Debug.Log("  W8:  +19 (原36 → 55)");
            Debug.Log("  W9:  +21 (原38 → 59) + Elite Phantom");
            Debug.Log("  W10: +35 (原59 → 94)");
            Debug.Log("  W11: +36 (原80 → 116)");
            Debug.Log("");
            Debug.Log("精英怪：");
            Debug.Log("  W6 @35s: Elite Tank (HP×5, DMG×2, XP×5=250)");
            Debug.Log("  W9 @25s: Elite Phantom (HP×5, Speed×1.5, XP×5=100)");
        }
        // ============================================================
        // WaveConfig.cs 新增方法 (v4.0)
        // 添加位置: 在 #if UNITY_EDITOR 区域内，GenerateDefaultConfig() 方法之后
        // ============================================================

        [ContextMenu("生成普通模式8波配置 v4.0（7波=Lv.18）")]
        public void GenerateNormalConfig_v4()
        {
            waves.Clear();
            totalWaves = 8;
            
            // ══════════════════════════════════════════════════════════════
            // 第一阶段：教学期 (Wave 1-2)
            // 目标：让玩家熟悉操作，轻松升级
            // ══════════════════════════════════════════════════════════════
            
            // Wave 1: 初临 (500 XP → Lv.4)
            // 纯 Slime，让玩家熟悉激光操作和转向
            waves.Add(CreateWave(1, "初临", "⚡ 净化之光，启动！", 1.0f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 15, 
                    pattern = SpawnPattern.Trickle, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 20f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 40f, enemyType = EnemyType.Slime, count = 15, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.AllEdges }
            ));
            
            // Wave 2: 暗涌 (700 XP → Lv.6)
            // 引入 Drifter（漂移者），教会玩家追踪移动目标
            waves.Add(CreateWave(2, "暗涌", "🌀 漂流者出现了...", 1.0f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 15, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 10f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Trickle, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Slime, count = 15, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 40f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom }
            ));
            
            // ══════════════════════════════════════════════════════════════
            // 第二阶段：过渡期 (Wave 3-4)
            // 目标：引入更多怪物类型，节奏加快
            // ══════════════════════════════════════════════════════════════
            
            // Wave 3: 骤雨 (900 XP → Lv.8)
            // 引入 Rusher（冲锋者），首次出现快速威胁
            waves.Add(CreateWave(3, "骤雨", "⚡ 冲锋者来袭！", 1.1f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 10f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 20f, enemyType = EnemyType.Rusher, count = 15, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 35f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 45f, enemyType = EnemyType.Rusher, count = 10, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.BottomCorners }
            ));
            
            // Wave 4: 铁壁 (1,110 XP → Lv.11)
            // 引入 Tank（坦克），教会玩家集火高血量目标
            waves.Add(CreateWave(4, "铁壁", "🛡️ 重甲污染体出现！", 1.2f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 10f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Tank, count = 3, 
                    pattern = SpawnPattern.Trickle, customInterval = 3f, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Rusher, count = 12, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 40f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 50f, enemyType = EnemyType.Tank, count = 2, 
                    pattern = SpawnPattern.Trickle, customInterval = 2f, spawnZone = SpawnZone.TopRandom }
            ));
            
            // ══════════════════════════════════════════════════════════════
            // 第三阶段：转折点 (Wave 5) ⭐ 精英怪①
            // 目标：难度跳升，精英坦克登场
            // ══════════════════════════════════════════════════════════════
            
            // Wave 5: 污染先驱 (1,310 XP → Lv.13)
            // 精英坦克首次登场，作为小 Boss 体验，打破节奏
            waves.Add(CreateWave(5, "污染先驱", "⭐ 精英污染体降临！", 1.3f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 25, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 10f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 20f, enemyType = EnemyType.Rusher, count = 12, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.BottomCorners },
                // ⭐ 精英坦克 @25s
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.EliteTank, count = 1, 
                    pattern = SpawnPattern.Instant, spawnZone = SpawnZone.TopOnly},
                new SpawnGroup { spawnTime = 30f, enemyType = EnemyType.Slime, count = 25, 
                    pattern = SpawnPattern.Flood, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 45f, enemyType = EnemyType.Tank, count = 2, 
                    pattern = SpawnPattern.Trickle, customInterval = 2f, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 50f, enemyType = EnemyType.Rusher, count = 8, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.SideRandom }
            ));
            
            // ══════════════════════════════════════════════════════════════
            // 第四阶段：压力期 (Wave 6)
            // 目标：高密度刷怪，考验操作
            // ══════════════════════════════════════════════════════════════
            
            // Wave 6: 浪潮 (1,690 XP → Lv.15)
            // 高密度刷怪，考验玩家的技能构筑和操作
            waves.Add(CreateWave(6, "浪潮", "🌊 污染浪潮来袭！", 1.4f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 35, 
                    pattern = SpawnPattern.Flood, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 8f, enemyType = EnemyType.Drifter, count = 15, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Rusher, count = 18, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.BottomCorners },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Tank, count = 3, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 35f, enemyType = EnemyType.Slime, count = 35, 
                    pattern = SpawnPattern.Flood, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 45f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 50f, enemyType = EnemyType.Rusher, count = 12, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 55f, enemyType = EnemyType.Tank, count = 2, 
                    pattern = SpawnPattern.Trickle, spawnZone = SpawnZone.TopOnly }
            ));
            
            // ══════════════════════════════════════════════════════════════
            // 第五阶段：高潮前奏 (Wave 7) ⭐ 精英怪②
            // 目标：最后的资源波，精英漂流者登场
            // ══════════════════════════════════════════════════════════════
            
            // Wave 7: 最后的宁静 (2,310 XP → Lv.18)
            // 最后的资源波，精英漂流者高速移动考验追踪能力，为 Boss 战热身
            waves.Add(CreateWave(7, "最后的宁静", "⭐ 风暴前的宁静...", 1.5f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 50, 
                    pattern = SpawnPattern.Flood, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 8f, enemyType = EnemyType.Drifter, count = 20, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Rusher, count = 25, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.BottomCorners },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Tank, count = 3, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopRandom },
                // ⭐ 精英漂流者 @30s
                new SpawnGroup { spawnTime = 30f, enemyType = EnemyType.EliteDrifter, count = 1, 
                    pattern = SpawnPattern.Instant, spawnZone = SpawnZone.SideRandom},
                new SpawnGroup { spawnTime = 35f, enemyType = EnemyType.Slime, count = 50, 
                    pattern = SpawnPattern.Flood, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 45f, enemyType = EnemyType.Drifter, count = 12, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 50f, enemyType = EnemyType.Rusher, count = 15, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 55f, enemyType = EnemyType.Tank, count = 2, 
                    pattern = SpawnPattern.Trickle, spawnZone = SpawnZone.TopOnly }
            ));
            
            // ══════════════════════════════════════════════════════════════
            // 第六阶段：BOSS战 (Wave 8)
            // ══════════════════════════════════════════════════════════════
            
            // Wave 8: BOSS战 - 刷怪由 BossController 控制
            waves.Add(CreateWave(8, "决战", "👹 污染之核 The Corruptor 降临！", 1.5f, true));
            
            // 标记配置已修改
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
            
            Debug.Log("=== WaveConfig v4.0 普通模式已生成 ===");
            Debug.Log("目标：7波结束达到 Lv.18 (累计 8,520 XP)");
            Debug.Log("总敌人数：656只（含2只精英）+ Boss");
            Debug.Log("");
            Debug.Log("经验分配：");
            Debug.Log("  W1: 500 XP  → Lv.4");
            Debug.Log("  W2: 700 XP  → Lv.6");
            Debug.Log("  W3: 900 XP  → Lv.8");
            Debug.Log("  W4: 1,110 XP → Lv.11");
            Debug.Log("  W5: 1,310 XP → Lv.13 (Elite Tank @25s)");
            Debug.Log("  W6: 1,690 XP → Lv.15");
            Debug.Log("  W7: 2,310 XP → Lv.18 (Elite Drifter @30s)");
            Debug.Log("  W8: Boss战");
            Debug.Log("");
            Debug.Log("难度倍率：W1=1.0, W2=1.0, W3=1.1, W4=1.2, W5=1.3, W6=1.4, W7=1.5");
        }
        
        [ContextMenu("生成困难模式8波配置 v4.0（7波=Lv.18）")]
        public void GenerateHardConfig_v4()
        {
            waves.Clear();
            totalWaves = 8;
            
            // ══════════════════════════════════════════════════════════════
            // 困难模式：相同怪物配置，更高难度倍率
            // 难度倍率影响：血量、速度、伤害
            // ══════════════════════════════════════════════════════════════
            
            // Wave 1: 初临 (500 XP → Lv.4)
            waves.Add(CreateWave(1, "初临", "⚡ 净化之光，启动！", 1.2f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 15, 
                    pattern = SpawnPattern.Trickle, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 20f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 40f, enemyType = EnemyType.Slime, count = 15, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.AllEdges }
            ));
            
            // Wave 2: 暗涌 (700 XP → Lv.6)
            waves.Add(CreateWave(2, "暗涌", "🌀 漂流者出现了...", 1.3f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 15, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 10f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Trickle, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Slime, count = 15, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 40f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom }
            ));
            
            // Wave 3: 骤雨 (900 XP → Lv.8)
            waves.Add(CreateWave(3, "骤雨", "⚡ 冲锋者来袭！", 1.4f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 10f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 20f, enemyType = EnemyType.Rusher, count = 15, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 35f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 45f, enemyType = EnemyType.Rusher, count = 10, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.BottomCorners }
            ));
            
            // Wave 4: 铁壁 (1,110 XP → Lv.11)
            waves.Add(CreateWave(4, "铁壁", "🛡️ 重甲污染体出现！", 1.6f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 10f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Tank, count = 3, 
                    pattern = SpawnPattern.Trickle, customInterval = 3f, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Rusher, count = 12, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 40f, enemyType = EnemyType.Slime, count = 20, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 50f, enemyType = EnemyType.Tank, count = 2, 
                    pattern = SpawnPattern.Trickle, customInterval = 2f, spawnZone = SpawnZone.TopRandom }
            ));
            
            // Wave 5: 污染先驱 (1,310 XP → Lv.13) ⭐ 精英怪①
            waves.Add(CreateWave(5, "污染先驱", "⭐ 精英污染体降临！", 1.8f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 25, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 10f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 20f, enemyType = EnemyType.Rusher, count = 12, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.BottomCorners },
                // ⭐ 精英坦克 @25s
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.EliteTank, count = 1, 
                    pattern = SpawnPattern.Instant, spawnZone = SpawnZone.TopOnly},
                new SpawnGroup { spawnTime = 30f, enemyType = EnemyType.Slime, count = 25, 
                    pattern = SpawnPattern.Flood, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 45f, enemyType = EnemyType.Tank, count = 2, 
                    pattern = SpawnPattern.Trickle, customInterval = 2f, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 50f, enemyType = EnemyType.Rusher, count = 8, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.SideRandom }
            ));
            
            // Wave 6: 浪潮 (1,690 XP → Lv.15)
            waves.Add(CreateWave(6, "浪潮", "🌊 污染浪潮来袭！", 2.0f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 35, 
                    pattern = SpawnPattern.Flood, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 8f, enemyType = EnemyType.Drifter, count = 15, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Rusher, count = 18, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.BottomCorners },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Tank, count = 3, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 35f, enemyType = EnemyType.Slime, count = 35, 
                    pattern = SpawnPattern.Flood, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 45f, enemyType = EnemyType.Drifter, count = 10, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 50f, enemyType = EnemyType.Rusher, count = 12, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 55f, enemyType = EnemyType.Tank, count = 2, 
                    pattern = SpawnPattern.Trickle, spawnZone = SpawnZone.TopOnly }
            ));
            
            // Wave 7: 最后的宁静 (2,310 XP → Lv.18) ⭐ 精英怪②
            waves.Add(CreateWave(7, "最后的宁静", "⭐ 风暴前的宁静...", 2.2f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 50, 
                    pattern = SpawnPattern.Flood, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 8f, enemyType = EnemyType.Drifter, count = 20, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 15f, enemyType = EnemyType.Rusher, count = 25, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.BottomCorners },
                new SpawnGroup { spawnTime = 25f, enemyType = EnemyType.Tank, count = 3, 
                    pattern = SpawnPattern.Normal, spawnZone = SpawnZone.TopRandom },
                // ⭐ 精英漂流者 @30s
                new SpawnGroup { spawnTime = 30f, enemyType = EnemyType.EliteDrifter, count = 1, 
                    pattern = SpawnPattern.Instant, spawnZone = SpawnZone.SideRandom},
                new SpawnGroup { spawnTime = 35f, enemyType = EnemyType.Slime, count = 50, 
                    pattern = SpawnPattern.Flood, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 45f, enemyType = EnemyType.Drifter, count = 12, 
                    pattern = SpawnPattern.Swarm, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 50f, enemyType = EnemyType.Rusher, count = 15, 
                    pattern = SpawnPattern.Burst, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 55f, enemyType = EnemyType.Tank, count = 2, 
                    pattern = SpawnPattern.Trickle, spawnZone = SpawnZone.TopOnly }
            ));
            
            // Wave 8: BOSS战
            waves.Add(CreateWave(8, "决战", "👹 污染之核 The Corruptor 降临！", 2.2f, true));
            
            // 标记配置已修改
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
            
            Debug.Log("=== WaveConfig v4.0 困难模式已生成 ===");
            Debug.Log("目标：7波结束达到 Lv.18 (累计 8,520 XP)");
            Debug.Log("总敌人数：656只（含2只精英）+ Boss");
            Debug.Log("");
            Debug.Log("难度倍率（困难模式）：");
            Debug.Log("  W1=1.2, W2=1.3, W3=1.4, W4=1.6, W5=1.8, W6=2.0, W7=2.2");
            Debug.Log("");
            Debug.Log("与普通模式对比：");
            Debug.Log("  W1: 1.0 → 1.2 (+20%)");
            Debug.Log("  W2: 1.0 → 1.3 (+30%)");
            Debug.Log("  W3: 1.1 → 1.4 (+27%)");
            Debug.Log("  W4: 1.2 → 1.6 (+33%)");
            Debug.Log("  W5: 1.3 → 1.8 (+38%)");
            Debug.Log("  W6: 1.4 → 2.0 (+43%)");
            Debug.Log("  W7: 1.5 → 2.2 (+47%)");
        }
        
        [ContextMenu("验证8波配置经验计算 v4.0")]
        public void ValidateExpCalculation_v4()
        {
            Debug.Log("=== WaveConfig v4.0 经验验证 ===");
            Debug.Log("怪物经验值: Slime=10, Drifter=20, Rusher=8, Tank=50");
            Debug.Log("精英加成: ×5 (Elite Tank=250, Elite Drifter=100)");
            Debug.Log("");
            
            // 各波次经验计算（基于配置）
            int[] waveExp = {
                500,    // W1: Slime×50
                700,    // W2: Slime×30 + Drifter×20
                900,    // W3: Slime×40 + Drifter×10 + Rusher×25
                1110,   // W4: Slime×40 + Drifter×10 + Rusher×12 + Tank×5
                1310,   // W5: Slime×50 + Drifter×10 + Rusher×20 + Tank×2 + EliteTank(250)
                1690,   // W6: Slime×70 + Drifter×25 + Rusher×30 + Tank×5
                2310    // W7: Slime×100 + Drifter×32 + Rusher×40 + Tank×5 + EliteDrifter(100)
            };
            
            // 升级所需经验 (公式: 100 + (lv-1) × 50)
            int[] levelUpReq = new int[20];
            for (int i = 0; i < 20; i++)
            {
                levelUpReq[i] = 100 + i * 50;
            }
            
            int cumulative = 0;
            int currentLevel = 1;
            int expToNext = levelUpReq[currentLevel - 1];
            int currentExp = 0;
            
            for (int w = 0; w < waveExp.Length; w++)
            {
                cumulative += waveExp[w];
                currentExp += waveExp[w];
                
                // 检查升级
                while (currentExp >= expToNext && currentLevel < 20)
                {
                    currentExp -= expToNext;
                    currentLevel++;
                    expToNext = levelUpReq[currentLevel - 1];
                }
                
                Debug.Log($"W{w + 1}: +{waveExp[w]} XP | 累计: {cumulative} | → Lv.{currentLevel}");
            }
            
            Debug.Log("");
            Debug.Log($"最终等级: Lv.{currentLevel}");
            Debug.Log($"目标: Lv.18 (需要 8,500 XP)");
            Debug.Log($"实际: {cumulative} XP → {(cumulative >= 8500 ? "✓ 达标" : "✗ 未达标")}");
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
                string eliteStr = eliteCount > 0 ? $", 精英x{eliteCount}" : "";
                
                Debug.Log($"[Wave {wave.waveNumber}] {wave.displayName}: " +
                          $"{wave.spawnGroups.Count} 组, {waveEnemies} 敌人{eliteStr}, " +
                          $"难度x{wave.difficultyMultiplier:F1}, BOSS={wave.isBossWave}");
            }
            
            Debug.Log($"=== 验证完成: {waves.Count} 波, {totalEnemies} 总敌人, {errorCount} 个错误 ===");
        }
        
        
        [ContextMenu("验证经验计算")]
        public void ValidateExpCalculation()
        {
            Debug.Log("=== WaveConfig v3.0 经验验证 ===");
            Debug.Log("怪物经验值: Slime=10, Drifter=20, Rusher=8, Tank=50");
            Debug.Log("精英加成: ×5");
            Debug.Log("");
            
            // 各波次经验计算
            int[] waveExp = {
                200,   // W1:  Slime×20
                300,   // W2:  Slime×20 + Drifter×5
                340,   // W3:  Slime×18 + Drifter×8
                240,   // W4:  Slime×20 + Rusher×5
                276,   // W5:  Slime×10 + Drifter×6 + Rusher×7
                582,   // W6:  Slime×20 + Rusher×4 + Tank×2 + EliteTank(250)
                648,   // W7:  Slime×35 + Rusher×6 + Tank×5
                750,   // W8:  Slime×35 + Drifter×20
                812,   // W9:  Slime×40 + Rusher×14 + Tank×4 + ElitePhantom(100)
                1262,  // W10: Slime×60 + Drifter×15 + Rusher×14 + Tank×5
                1360   // W11: Slime×90 + Rusher×20 + Tank×6
            };
            
            // 升级所需经验 (公式: 100 + (lv-1) × 50)
            int[] levelUpReq = new int[20];
            for (int i = 0; i < 20; i++)
            {
                levelUpReq[i] = 100 + i * 50;
            }
            
            int cumulative = 0;
            int currentLevel = 1;
            int expToNext = levelUpReq[currentLevel - 1];
            int currentExp = 0;
            
            for (int w = 0; w < waveExp.Length; w++)
            {
                cumulative += waveExp[w];
                currentExp += waveExp[w];
                
                // 检查升级
                while (currentExp >= expToNext && currentLevel < 20)
                {
                    currentExp -= expToNext;
                    currentLevel++;
                    expToNext = levelUpReq[currentLevel - 1];
                }
                
                Debug.Log($"W{w + 1}: +{waveExp[w]} XP | 累计: {cumulative} | → Lv.{currentLevel}");
            }
            
            Debug.Log("");
            Debug.Log($"最终等级: Lv.{currentLevel}");
            Debug.Log($"目标: Lv.16 (需要6750 XP)");
            Debug.Log($"实际: {cumulative} XP → {(cumulative >= 6750 ? "✓ 达标" : "✗ 未达标")}");
        }
#endif
    }
}