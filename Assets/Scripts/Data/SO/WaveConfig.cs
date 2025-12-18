// ============================================================
// WaveConfig.cs (重构版)
// 文件位置: Assets/Scripts/Data/SO/WaveConfig.cs
// 用途：波次配置数据（ScriptableObject）- 基于波次序列，非时间轴
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
        [Range(1, 20)]
        public int count = 3;
        
        [Header("位置与属性")]
        [Tooltip("生成区域")]
        public SpawnZone spawnZone = SpawnZone.AllEdges;
        
        [Tooltip("血量倍率（用于难度调整）")]
        [Range(0.5f, 5f)]
        public float healthMultiplier = 1f;
        
        [Tooltip("速度倍率")]
        [Range(0.5f, 3f)]
        public float speedMultiplier = 1f;
        
        [Tooltip("伤害倍率")]
        [Range(0.5f, 3f)]
        public float damageMultiplier = 1f;
        
        // 运行时标记（不序列化）
        [System.NonSerialized]
        public bool hasSpawned = false;
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
        /// 获取本波最后一组的生成时间（用于判断刷怪完成）
        /// </summary>
        public float LastSpawnTime
        {
            get
            {
                float maxTime = 0f;
                foreach (var group in spawnGroups)
                {
                    if (group.spawnTime > maxTime)
                        maxTime = group.spawnTime;
                }
                return maxTime;
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
        public float bossHealth = 50000f;
        
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
        [ContextMenu("生成默认12波配置")]
        public void GenerateDefaultConfig()
        {
            waves.Clear();
            
            // ========== Wave 1-3: 教学期 ==========
            // Wave 1: 纯 Slime 入门
            waves.Add(CreateWave(1, "初见", "只有 Slime，熟悉操作", 1.0f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 3, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 3f, enemyType = EnemyType.Slime, count = 3, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 6f, enemyType = EnemyType.Slime, count = 4, spawnZone = SpawnZone.AllEdges }
            ));
            
            // Wave 2: Slime 增量
            waves.Add(CreateWave(2, "热身", "Slime 数量增加", 1.0f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 4, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 2f, enemyType = EnemyType.Slime, count = 3, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 5f, enemyType = EnemyType.Slime, count = 5, spawnZone = SpawnZone.AllEdges }
            ));
            
            // Wave 3: Slime 密集
            waves.Add(CreateWave(3, "试炼", "Slime 密集波", 1.0f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 5, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 2f, enemyType = EnemyType.Slime, count = 4, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 4f, enemyType = EnemyType.Slime, count = 3, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 6f, enemyType = EnemyType.Slime, count = 3, spawnZone = SpawnZone.AllEdges }
            ));
            
            // ========== Wave 4-6: 成长期（引入 Rusher）==========
            // Wave 4: 首次出现 Rusher
            waves.Add(CreateWave(4, "冲锋", "⚡ Rusher 来袭！", 1.1f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 4, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 2f, enemyType = EnemyType.Rusher, count = 2, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 5f, enemyType = EnemyType.Slime, count = 3, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 7f, enemyType = EnemyType.Rusher, count = 3, spawnZone = SpawnZone.SideRandom }
            ));
            
            // Wave 5: Rusher 增量
            waves.Add(CreateWave(5, "突袭", "Rusher 增多", 1.1f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Rusher, count = 3, spawnZone = SpawnZone.LeftSide },
                new SpawnGroup { spawnTime = 2f, enemyType = EnemyType.Slime, count = 5, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 4f, enemyType = EnemyType.Rusher, count = 3, spawnZone = SpawnZone.RightSide },
                new SpawnGroup { spawnTime = 6f, enemyType = EnemyType.Slime, count = 4, spawnZone = SpawnZone.AllEdges }
            ));
            
            // Wave 6: 混合压力
            waves.Add(CreateWave(6, "围攻", "四面楚歌", 1.2f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 4, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 2f, enemyType = EnemyType.Rusher, count = 4, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 4f, enemyType = EnemyType.Slime, count = 5, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 6f, enemyType = EnemyType.Rusher, count = 4, spawnZone = SpawnZone.AllEdges }
            ));
            
            // ========== Wave 7-9: 高压期（引入 Tank）==========
            // Wave 7: 首次出现 Tank
            waves.Add(CreateWave(7, "重甲", "🛡 Tank 出现！", 1.3f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 1, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 2f, enemyType = EnemyType.Slime, count = 5, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 4f, enemyType = EnemyType.Rusher, count = 3, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 6f, enemyType = EnemyType.Tank, count = 1, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 8f, enemyType = EnemyType.Slime, count = 4, spawnZone = SpawnZone.AllEdges }
            ));
            
            // Wave 8: 三种混合
            waves.Add(CreateWave(8, "乱战", "三军混战", 1.4f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Slime, count = 5, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 2f, enemyType = EnemyType.Tank, count = 2, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 4f, enemyType = EnemyType.Rusher, count = 5, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 6f, enemyType = EnemyType.Slime, count = 4, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 8f, enemyType = EnemyType.Rusher, count = 4, spawnZone = SpawnZone.AllEdges }
            ));
            
            // Wave 9: 高压测试
            waves.Add(CreateWave(9, "炼狱", "极限测试", 1.5f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 2, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 1f, enemyType = EnemyType.Rusher, count = 4, spawnZone = SpawnZone.LeftSide },
                new SpawnGroup { spawnTime = 2f, enemyType = EnemyType.Rusher, count = 4, spawnZone = SpawnZone.RightSide },
                new SpawnGroup { spawnTime = 4f, enemyType = EnemyType.Slime, count = 6, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 6f, enemyType = EnemyType.Tank, count = 2, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 8f, enemyType = EnemyType.Rusher, count = 5, spawnZone = SpawnZone.SideRandom }
            ));
            
            // ========== Wave 10-11: 疯狂期 ==========
            // Wave 10: 大量 Rusher + Tank
            waves.Add(CreateWave(10, "狂潮", "🔥 全面进攻！", 1.6f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 3, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 1f, enemyType = EnemyType.Rusher, count = 5, spawnZone = SpawnZone.SideRandom },
                new SpawnGroup { spawnTime = 3f, enemyType = EnemyType.Slime, count = 6, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 5f, enemyType = EnemyType.Rusher, count = 6, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 7f, enemyType = EnemyType.Tank, count = 2, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 9f, enemyType = EnemyType.Rusher, count = 4, spawnZone = SpawnZone.SideRandom }
            ));
            
            // Wave 11: 最终试炼
            waves.Add(CreateWave(11, "死守", "💀 最后一搏！", 1.8f, false,
                new SpawnGroup { spawnTime = 0f, enemyType = EnemyType.Tank, count = 3, spawnZone = SpawnZone.TopOnly },
                new SpawnGroup { spawnTime = 1f, enemyType = EnemyType.Rusher, count = 6, spawnZone = SpawnZone.LeftSide },
                new SpawnGroup { spawnTime = 2f, enemyType = EnemyType.Rusher, count = 6, spawnZone = SpawnZone.RightSide },
                new SpawnGroup { spawnTime = 4f, enemyType = EnemyType.Slime, count = 8, spawnZone = SpawnZone.AllEdges },
                new SpawnGroup { spawnTime = 6f, enemyType = EnemyType.Tank, count = 2, spawnZone = SpawnZone.TopRandom },
                new SpawnGroup { spawnTime = 8f, enemyType = EnemyType.Rusher, count = 6, spawnZone = SpawnZone.AllEdges, speedMultiplier = 1.5f }
            ));
            
            // ========== Wave 12: BOSS 战 ==========
            waves.Add(CreateWave(12, "决战", "👹 BOSS 降临！", 2.0f, true));
            
            totalWaves = waves.Count;
            
            Debug.Log($"[WaveConfig] 默认配置已生成！共 {waves.Count} 波");
        }
        
        /// <summary>
        /// 创建波次辅助方法
        /// </summary>
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
            
            for (int i = 0; i < waves.Count; i++)
            {
                var wave = waves[i];
                
                if (wave.waveNumber != i + 1)
                {
                    Debug.LogWarning($"[{i}] 波次编号不匹配: 预期 {i + 1}, 实际 {wave.waveNumber}");
                    errorCount++;
                }
                
                Debug.Log($"[Wave {wave.waveNumber}] {wave.displayName}: " +
                          $"{wave.spawnGroups.Count} 组, {wave.TotalEnemyCount} 敌人, " +
                          $"难度x{wave.difficultyMultiplier:F1}, BOSS={wave.isBossWave}");
            }
            
            Debug.Log($"=== 验证完成: {waves.Count} 波, {errorCount} 个错误 ===");
        }
#endif
    }
}