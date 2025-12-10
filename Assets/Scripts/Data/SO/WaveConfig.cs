// ============================================================
// WaveConfig.cs
// 文件位置: Assets/Scripts/Data/SO/WaveConfig.cs
// 用途：波次配置数据（ScriptableObject）
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Core.Pool;

namespace LightVsDecay.Data.SO
{
    /// <summary>
    /// 游戏阶段枚举
    /// </summary>
    public enum GamePhase
    {
        Warmup,         // 热身期 (0:00 - 1:00)
        Wave1Climax,    // 第1波高潮 (1:00 - 1:30)
        Rest1,          // 休息期 (1:30 - 1:45)
        Variation,      // 变奏期 (1:45 - 2:30)
        Wave2Climax,    // 第2波高潮 (2:30 - 3:30)
        TreasureTime,   // 宝箱时刻 (3:30 - 3:45)
        FinalStand,     // 最终死守 (3:45 - 4:45)
        CalmBeforeStorm,// 暴风雨前的宁静 (4:45 - 5:00)
        BossFight       // BOSS战 (5:00+)
    }

    /// <summary>
    /// 单个敌人生成配置
    /// </summary>
    [System.Serializable]
    public class EnemySpawnEntry
    {
        [Tooltip("敌人类型")]
        public EnemyType enemyType = EnemyType.Slime;
        
        [Tooltip("生成间隔（秒）")]
        public float spawnInterval = 2f;
        
        [Tooltip("每次生成数量")]
        public int spawnCount = 1;
        
        [Tooltip("生成区域")]
        public SpawnZone spawnZone = SpawnZone.AllEdges;
        
        [Tooltip("速度倍率（用于狂暴模式）")]
        public float speedMultiplier = 1f;
    }

    /// <summary>
    /// 生成区域枚举
    /// </summary>
    public enum SpawnZone
    {
        AllEdges,       // 所有边缘
        TopOnly,        // 仅上方
        TopRandom,      // 上方随机
        SideRandom,     // 两侧随机
        BottomCorners,   // 底部角落
        LeftSideUpper,  // 左侧上半部分（宝箱怪用）
        RightSideUpper  // 右侧上半部分（宝箱怪用）
    }

    /// <summary>
    /// 单个阶段配置
    /// </summary>
    [System.Serializable]
    public class PhaseConfig
    {
        [Header("阶段信息")]
        [Tooltip("阶段类型")]
        public GamePhase phase;
        
        [Tooltip("阶段名称（显示用）")]
        public string displayName = "阶段";
        
        [Tooltip("阶段描述")]
        [TextArea(1, 2)]
        public string description;
        
        [Header("时间设置")]
        [Tooltip("阶段开始时间（秒）")]
        public float startTime = 0f;
        
        [Tooltip("阶段结束时间（秒）")]
        public float endTime = 60f;
        
        [Header("生成设置")]
        [Tooltip("是否启用生成")]
        public bool enableSpawning = true;
        
        [Tooltip("敌人生成配置列表")]
        public List<EnemySpawnEntry> spawnEntries = new List<EnemySpawnEntry>();
        
        [Header("密度调整")]
        [Tooltip("生成频率倍率（1.0=正常，1.5=+50%密度）")]
        [Range(0f, 3f)]
        public float spawnRateMultiplier = 1f;
        
        [Header("特殊事件")]
        [Tooltip("阶段开始时触发的事件")]
        public PhaseEvent onPhaseStart = PhaseEvent.None;
        
        [Tooltip("阶段结束时触发的事件")]
        public PhaseEvent onPhaseEnd = PhaseEvent.None;
        
        [Header("UI提示")]
        [Tooltip("是否显示阶段提示")]
        public bool showPhaseHint = false;
        
        [Tooltip("提示文本")]
        public string hintText = "";
        
        /// <summary>
        /// 阶段持续时间
        /// </summary>
        public float Duration => endTime - startTime;
    }

    /// <summary>
    /// 阶段事件枚举
    /// </summary>
    public enum PhaseEvent
    {
        None,
        ClearAllEnemies,    // 清除所有敌人
        PlayWarningSound,   // 播放警告音效
        SpawnBoss,          // 生成BOSS
        ShowBossHealthBar,  // 显示BOSS血条
        PauseSpawning,      // 暂停生成
        ResumeSpawning      // 恢复生成
    }

    /// <summary>
    /// 波次配置 (ScriptableObject)
    /// 定义整局游戏的敌人生成节奏
    /// </summary>
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "LightVsDecay/Wave Config", order = 3)]
    public class WaveConfig : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 全局设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("全局设置")]
        [Tooltip("单局总时长（秒）")]
        public float gameDuration = 300f;
        
        [Tooltip("BOSS战限时（秒）")]
        public float bossBattleTimeLimit = 60f;
        
        [Tooltip("全局敌人上限")]
        public int globalEnemyLimit = 200;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 阶段配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("阶段配置")]
        [Tooltip("所有阶段的配置")]
        public List<PhaseConfig> phases = new List<PhaseConfig>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BOSS设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("BOSS设置")]
        [Tooltip("BOSS生命值")]
        public float bossHealth = 50000f;
        
        [Tooltip("BOSS移动速度")]
        public float bossMoveSpeed = 0.2f;
        
        [Tooltip("BOSS召唤小怪间隔（秒）")]
        public float bossMinionSpawnInterval = 5f;
        
        [Tooltip("BOSS每次召唤小怪数量")]
        public int bossMinionCount = 3;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 便捷方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 根据游戏时间获取当前阶段
        /// </summary>
        public PhaseConfig GetPhaseAtTime(float gameTime)
        {
            if (phases == null || phases.Count == 0)
            {
                Debug.LogError("[WaveConfig] phases 列表为空！");
                return null;
            }
    
            foreach (var phase in phases)
            {
                if (phase == null) continue;
        
                if (gameTime >= phase.startTime && gameTime < phase.endTime)
                {
                    return phase;
                }
            }
    
            // 【修复】如果超过所有阶段时间，返回最后一个阶段
            // 但需要检查最后一个阶段是否有效
            var lastPhase = phases[phases.Count - 1];
            if (lastPhase != null && gameTime >= lastPhase.startTime)
            {
                return lastPhase;
            }
    
            // 如果都不匹配，打印警告帮助调试
            Debug.LogWarning($"[WaveConfig] gameTime={gameTime:F1}s 不在任何阶段范围内！");
            Debug.LogWarning($"[WaveConfig] 已配置阶段数量: {phases.Count}");
            for (int i = 0; i < phases.Count; i++)
            {
                var p = phases[i];
                if (p != null)
                {
                    Debug.LogWarning($"  [{i}] {p.phase}: {p.startTime}s - {p.endTime}s");
                }
            }
    
            return null;
        }
        
        /// <summary>
        /// 获取指定阶段的配置
        /// </summary>
        public PhaseConfig GetPhase(GamePhase phaseType)
        {
            return phases.Find(p => p.phase == phaseType);
        }
        
        /// <summary>
        /// 检查是否到达BOSS阶段
        /// </summary>
        public bool IsBossPhase(float gameTime)
        {
            var phase = GetPhaseAtTime(gameTime);
            return phase != null && phase.phase == GamePhase.BossFight;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器支持
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [ContextMenu("生成默认配置")]
        public void GenerateDefaultConfig()
        {
            phases.Clear();
            
            // 热身期 (0:00 - 1:00)
            phases.Add(new PhaseConfig
            {
                phase = GamePhase.Warmup,
                displayName = "热身期",
                description = "只有Slime，稀疏刷新。放松：适应操作，点亮前几个技能。",
                startTime = 0f,
                endTime = 60f,
                enableSpawning = true,
                spawnRateMultiplier = 0.7f,
                spawnEntries = new List<EnemySpawnEntry>
                {
                    new EnemySpawnEntry { enemyType = EnemyType.Slime, spawnInterval = 2f, spawnCount = 2, spawnZone = SpawnZone.AllEdges }
                }
            });
            
            // 第1波高潮 (1:00 - 1:30)
            phases.Add(new PhaseConfig
            {
                phase = GamePhase.Wave1Climax,
                displayName = "第1波高潮",
                description = "Tank出现，怪群密度+50%。紧张：第一次感受到推不动的压力。",
                startTime = 60f,
                endTime = 90f,
                enableSpawning = true,
                spawnRateMultiplier = 1.5f,
                showPhaseHint = true,
                hintText = "⚠ Tank 来袭！",
                spawnEntries = new List<EnemySpawnEntry>
                {
                    new EnemySpawnEntry { enemyType = EnemyType.Slime, spawnInterval = 1.5f, spawnCount = 3, spawnZone = SpawnZone.AllEdges },
                    new EnemySpawnEntry { enemyType = EnemyType.Tank, spawnInterval = 4f, spawnCount = 1, spawnZone = SpawnZone.TopRandom }
                }
            });
            
            // 休息期 (1:30 - 1:45)
            phases.Add(new PhaseConfig
            {
                phase = GamePhase.Rest1,
                displayName = "休息期",
                description = "停止刷怪或只刷极少量Slime。释放：处理残兵，喘口气。",
                startTime = 90f,
                endTime = 105f,
                enableSpawning = true,
                spawnRateMultiplier = 0.3f,
                spawnEntries = new List<EnemySpawnEntry>
                {
                    new EnemySpawnEntry { enemyType = EnemyType.Slime, spawnInterval = 5f, spawnCount = 1, spawnZone = SpawnZone.AllEdges }
                }
            });
            
            // 变奏期 (1:45 - 2:30)
            phases.Add(new PhaseConfig
            {
                phase = GamePhase.Variation,
                displayName = "变奏期",
                description = "Rusher(速攻)出现，快速冲脸。惊吓：考验反应速度。",
                startTime = 105f,
                endTime = 150f,
                enableSpawning = true,
                spawnRateMultiplier = 1.0f,
                showPhaseHint = true,
                hintText = "⚡ Rusher 出现！",
                spawnEntries = new List<EnemySpawnEntry>
                {
                    new EnemySpawnEntry { enemyType = EnemyType.Slime, spawnInterval = 2f, spawnCount = 2, spawnZone = SpawnZone.AllEdges },
                    new EnemySpawnEntry { enemyType = EnemyType.Rusher, spawnInterval = 2.5f, spawnCount = 4, spawnZone = SpawnZone.SideRandom }
                }
            });
            
            // 第2波高潮 (2:30 - 3:30)
            phases.Add(new PhaseConfig
            {
                phase = GamePhase.Wave2Climax,
                displayName = "第2波高潮",
                description = "三种怪混刷，精英怪出现。高压：技能成型，疯狂割草。",
                startTime = 150f,
                endTime = 210f,
                enableSpawning = true,
                spawnRateMultiplier = 1.5f,
                showPhaseHint = true,
                hintText = "🔥 全面进攻！",
                spawnEntries = new List<EnemySpawnEntry>
                {
                    new EnemySpawnEntry { enemyType = EnemyType.Slime, spawnInterval = 1.5f, spawnCount = 4, spawnZone = SpawnZone.AllEdges },
                    new EnemySpawnEntry { enemyType = EnemyType.Tank, spawnInterval = 3f, spawnCount = 2, spawnZone = SpawnZone.TopRandom },
                    new EnemySpawnEntry { enemyType = EnemyType.Rusher, spawnInterval = 2f, spawnCount = 5, spawnZone = SpawnZone.SideRandom },
                    new EnemySpawnEntry { enemyType = EnemyType.Drifter, spawnInterval = 2.5f, spawnCount = 3, spawnZone = SpawnZone.AllEdges }
                }
            });
            
            // 宝箱时刻 (3:30 - 3:45)
            phases.Add(new PhaseConfig
            {
                phase = GamePhase.TreasureTime,
                displayName = "宝箱时刻",
                description = "刷一群宝箱怪或金币怪（不攻击）。惊喜：纯爽，送资源。",
                startTime = 210f,
                endTime = 225f,
                enableSpawning = true,
                spawnRateMultiplier = 1.0f,
                showPhaseHint = true,
                hintText = "💰 宝箱时刻！",
                spawnEntries = new List<EnemySpawnEntry>
                {
                    // 宝箱怪从左侧或右侧上半部分生成
                    new EnemySpawnEntry { enemyType = EnemyType.Treasure, spawnInterval = 7f, spawnCount = 1, spawnZone = SpawnZone.LeftSideUpper },
                    new EnemySpawnEntry { enemyType = EnemyType.Treasure, spawnInterval = 7f, spawnCount = 1, spawnZone = SpawnZone.RightSideUpper }
                }
            });
            
            // 最终死守 (3:45 - 4:45)
            phases.Add(new PhaseConfig
            {
                phase = GamePhase.FinalStand,
                displayName = "最终死守",
                description = "刷新率MAX，全屏怪潮。极限：此时不看策略，只看火力覆盖。",
                startTime = 225f,
                endTime = 285f,
                enableSpawning = true,
                spawnRateMultiplier = 2.0f,
                showPhaseHint = true,
                hintText = "💀 最终死守！",
                spawnEntries = new List<EnemySpawnEntry>
                {
                    new EnemySpawnEntry { enemyType = EnemyType.Slime, spawnInterval = 1f, spawnCount = 5, spawnZone = SpawnZone.AllEdges, speedMultiplier = 1.3f },
                    new EnemySpawnEntry { enemyType = EnemyType.Tank, spawnInterval = 2.5f, spawnCount = 2, spawnZone = SpawnZone.TopRandom },
                    new EnemySpawnEntry { enemyType = EnemyType.Rusher, spawnInterval = 1.5f, spawnCount = 6, spawnZone = SpawnZone.SideRandom, speedMultiplier = 1.5f },
                    new EnemySpawnEntry { enemyType = EnemyType.Drifter, spawnInterval = 2f, spawnCount = 4, spawnZone = SpawnZone.AllEdges }
                }
            });
            
            // 暴风雨前的宁静 (4:45 - 5:00)
            phases.Add(new PhaseConfig
            {
                phase = GamePhase.CalmBeforeStorm,
                displayName = "暴风雨前的宁静",
                description = "全图清空/停止刷新。警报声起。恐惧：为BOSS登场做铺垫。",
                startTime = 285f,
                endTime = 300f,
                enableSpawning = false,
                spawnRateMultiplier = 0f,
                showPhaseHint = true,
                hintText = "...",
                onPhaseStart = PhaseEvent.ClearAllEnemies,
                onPhaseEnd = PhaseEvent.PlayWarningSound
            });
            
            // BOSS战 (5:00+)
            phases.Add(new PhaseConfig
            {
                phase = GamePhase.BossFight,
                displayName = "BOSS战",
                description = "只有BOSS和它召唤的小弟。决战：目标明确，击杀即胜利。",
                startTime = 300f,
                endTime = 360f,
                enableSpawning = false, // BOSS单独处理
                spawnRateMultiplier = 0f,
                showPhaseHint = true,
                hintText = "👹 BOSS 降临！",
                onPhaseStart = PhaseEvent.SpawnBoss
            });
            
            Debug.Log("[WaveConfig] 默认配置已生成！");
        }
        
        [ContextMenu("验证配置")]
        public void ValidateConfig()
        {
            Debug.Log("=== 波次配置验证 ===");
            
            float lastEndTime = 0f;
            int errorCount = 0;
            
            for (int i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                
                // 检查时间连续性
                if (phase.startTime < lastEndTime)
                {
                    Debug.LogWarning($"[{i}] {phase.displayName}: 开始时间 {phase.startTime} < 上一阶段结束时间 {lastEndTime}");
                    errorCount++;
                }
                
                if (phase.endTime <= phase.startTime)
                {
                    Debug.LogError($"[{i}] {phase.displayName}: 结束时间必须大于开始时间！");
                    errorCount++;
                }
                
                Debug.Log($"[{i}] {phase.phase}: {phase.startTime}s - {phase.endTime}s ({phase.Duration}s) | 生成:{phase.enableSpawning} | 倍率:{phase.spawnRateMultiplier}x");
                
                lastEndTime = phase.endTime;
            }
            
            Debug.Log($"=== 验证完成: {phases.Count} 个阶段, {errorCount} 个错误 ===");
        }
#endif
    }
}