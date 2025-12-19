// ============================================================
// DifficultyManager.cs (简化版)
// 文件位置: Assets/Scripts/Logic/DifficultyManager.cs
// 用途：难度管理器 - 只使用波次难度，删除时间难度
// 更新：根据数值框架确认，避免"死亡螺旋"
// ============================================================

using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Logic
{
    /// <summary>
    /// 难度数据结构（传递给敌人）
    /// 现在只返回默认值 1.0，实际难度由 WaveConfig.difficultyMultiplier 控制
    /// </summary>
    public struct DifficultyModifiers
    {
        public float hpMultiplier;      // 血量倍率
        public float speedMultiplier;   // 速度倍率
        public float massMultiplier;    // 质量倍率
        public float damageMultiplier;  // 伤害倍率（新增）
        
        public static DifficultyModifiers Default => new DifficultyModifiers
        {
            hpMultiplier = 1f,
            speedMultiplier = 1f,
            massMultiplier = 1f,
            damageMultiplier = 1f
        };
    }
    
    /// <summary>
    /// 难度管理器（简化版）
    /// 
    /// 设计决策：只使用波次难度，删除时间难度
    /// 理由：避免"死亡螺旋" - 当玩家 DPS 不足时，
    ///       时间拖得越久怪越强，形成无解死局。
    /// 
    /// 现在：
    /// - 敌人强度由 WaveConfig.difficultyMultiplier 决定
    /// - Wave 1 的怪永远是 Wave 1 的强度
    /// - 压力来源是"怪越堆越多"而非"单只怪变强"
    /// </summary>
    public class DifficultyManager : Singleton<DifficultyManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置（保留但不再使用，便于未来可能的需求）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态（保留用于统计）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>战斗计时器（仅用于统计，不影响难度）</summary>
        private float battleTimer = 0f;
        
        /// <summary>是否正在战斗</summary>
        private bool isInBattle = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前战斗时间（秒）- 仅用于统计</summary>
        public float BattleTime => battleTimer;
        
        /// <summary>当前战斗时间（分钟）</summary>
        public float BattleTimeMinutes => battleTimer / 60f;
        
        /// <summary>HP 倍率 - 始终返回 1.0</summary>
        public float CurrentHPMultiplier => 1f;
        
        /// <summary>速度倍率 - 始终返回 1.0</summary>
        public float CurrentSpeedMultiplier => 1f;
        
        /// <summary>质量倍率 - 始终返回 1.0</summary>
        public float CurrentMassMultiplier => 1f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            // 订阅游戏事件
            GameEvents.OnGameStart += OnGameStart;
            GameEvents.OnGameStateChanged += OnGameStateChanged;
        }
        
        protected override void OnSingletonDestroy()
        {
            GameEvents.OnGameStart -= OnGameStart;
            GameEvents.OnGameStateChanged -= OnGameStateChanged;
        }
        
        private void Update()
        {
            // 仅用于统计战斗时间，不影响难度
            if (isInBattle && Time.timeScale > 0f)
            {
                battleTimer += Time.deltaTime;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnGameStart()
        {
            battleTimer = 0f;
            isInBattle = true;
            
            if (showDebugInfo)
            {
                Debug.Log("[DifficultyManager] 战斗开始，计时器重置（注：时间难度已禁用）");
            }
        }
        
        private void OnGameStateChanged(GameState state)
        {
            isInBattle = (state == GameState.Playing);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取当前难度系数
        /// 现在始终返回默认值 1.0
        /// 实际难度由 WaveConfig.difficultyMultiplier 在生成时控制
        /// </summary>
        public DifficultyModifiers GetCurrentModifiers()
        {
            // 返回默认值，不再基于时间计算
            return DifficultyModifiers.Default;
        }
        
        /// <summary>
        /// 获取指定波次的难度修正
        /// 供 WaveManager 调用
        /// </summary>
        /// <param name="waveDifficultyMultiplier">波次配置的难度倍率</param>
        public DifficultyModifiers GetWaveModifiers(float waveDifficultyMultiplier)
        {
            return new DifficultyModifiers
            {
                hpMultiplier = waveDifficultyMultiplier,
                speedMultiplier = Mathf.Min(1f + (waveDifficultyMultiplier - 1f) * 0.3f, 1.5f), // 速度增幅较小，封顶1.5
                massMultiplier = 1f + (waveDifficultyMultiplier - 1f) * 0.2f, // 质量增幅更小
                damageMultiplier = 1f + (waveDifficultyMultiplier - 1f) * 0.5f // 伤害增幅中等
            };
        }
        
        /// <summary>
        /// 重置难度（用于重新开始）
        /// </summary>
        public void ResetDifficulty()
        {
            battleTimer = 0f;
            
            if (showDebugInfo)
            {
                Debug.Log("[DifficultyManager] 计时器已重置");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 580, 220, 80));
            GUILayout.Label("=== Difficulty (波次模式) ===");
            GUILayout.Label($"Battle Time: {battleTimer:F1}s ({BattleTimeMinutes:F2} min)");
            GUILayout.Label($"时间难度: 已禁用");
            GUILayout.Label($"难度来源: WaveConfig.difficultyMultiplier");
            GUILayout.EndArea();
        }
#endif
    }
}