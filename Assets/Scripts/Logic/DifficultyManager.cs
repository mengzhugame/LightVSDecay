// ============================================================
// DifficultyManager.cs
// 文件位置: Assets/Scripts/Logic/DifficultyManager.cs
// 用途：时间难度系数管理 - 随游戏时间增加敌人属性
// ============================================================

using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Logic
{
    /// <summary>
    /// 难度数据结构（传递给敌人）
    /// </summary>
    public struct DifficultyModifiers
    {
        public float hpMultiplier;      // 血量倍率（无上限）
        public float speedMultiplier;   // 速度倍率（封顶 1.5）
        public float massMultiplier;    // 质量倍率（封顶 1.3）
        
        public static DifficultyModifiers Default => new DifficultyModifiers
        {
            hpMultiplier = 1f,
            speedMultiplier = 1f,
            massMultiplier = 1f
        };
    }
    
    /// <summary>
    /// 难度管理器
    /// 职责：
    /// - 维护战斗计时器（仅在战斗中且非暂停时累加）
    /// - 计算并提供当前难度系数
    /// </summary>
    public class DifficultyManager : Singleton<DifficultyManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 难度配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("HP 系数配置")]
        [Tooltip("HP 增长速率：每 60 秒增加的倍率")]
        [SerializeField] private float hpGrowthPerMinute = 0.5f;
        
        [Header("速度系数配置")]
        [Tooltip("速度增长速率：每 120 秒增加的倍率")]
        [SerializeField] private float speedGrowthPer2Minutes = 0.25f;
        
        [Tooltip("速度倍率上限")]
        [SerializeField] private float speedMaxMultiplier = 1.5f;
        
        [Header("质量系数配置")]
        [Tooltip("质量增长速率：每 180 秒增加的倍率")]
        [SerializeField] private float massGrowthPer3Minutes = 0.15f;
        
        [Tooltip("质量倍率上限")]
        [SerializeField] private float massMaxMultiplier = 1.3f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>战斗计时器（仅在战斗中累加）</summary>
        private float battleTimer = 0f;
        
        /// <summary>是否正在战斗</summary>
        private bool isInBattle = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前战斗时间（秒）</summary>
        public float BattleTime => battleTimer;
        
        /// <summary>当前战斗时间（分钟）</summary>
        public float BattleTimeMinutes => battleTimer / 60f;
        
        /// <summary>当前 HP 倍率</summary>
        public float CurrentHPMultiplier => 1f + (battleTimer / 60f) * hpGrowthPerMinute;
        
        /// <summary>当前速度倍率</summary>
        public float CurrentSpeedMultiplier => Mathf.Min(
            1f + (battleTimer / 120f) * speedGrowthPer2Minutes,
            speedMaxMultiplier
        );
        
        /// <summary>当前质量倍率</summary>
        public float CurrentMassMultiplier => Mathf.Min(
            1f + (battleTimer / 180f) * massGrowthPer3Minutes,
            massMaxMultiplier
        );
        
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
            // 仅在战斗中且非暂停时累加计时器
            // Time.timeScale == 0 时 Time.deltaTime 也为 0，但为了明确性还是检查一下
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
            // 重置战斗计时器
            battleTimer = 0f;
            isInBattle = true;
            
            if (showDebugInfo)
            {
                Debug.Log("[DifficultyManager] 战斗开始，计时器重置");
            }
        }
        
        private void OnGameStateChanged(GameState state)
        {
            // 只有 Playing 状态才算"战斗中"
            isInBattle = (state == GameState.Playing);
            
            if (showDebugInfo)
            {
                Debug.Log($"[DifficultyManager] 状态变化: {state}, 战斗中: {isInBattle}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取当前难度系数（供敌人生成时调用）
        /// </summary>
        public DifficultyModifiers GetCurrentModifiers()
        {
            return new DifficultyModifiers
            {
                hpMultiplier = CurrentHPMultiplier,
                speedMultiplier = CurrentSpeedMultiplier,
                massMultiplier = CurrentMassMultiplier
            };
        }
        
        /// <summary>
        /// 重置难度（用于测试或重新开始）
        /// </summary>
        public void ResetDifficulty()
        {
            battleTimer = 0f;
            
            if (showDebugInfo)
            {
                Debug.Log("[DifficultyManager] 难度已重置");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 580, 220, 120));
            GUILayout.Label("=== Difficulty ===");
            GUILayout.Label($"Battle Time: {battleTimer:F1}s ({BattleTimeMinutes:F2} min)");
            GUILayout.Label($"HP Mult: x{CurrentHPMultiplier:F2}");
            GUILayout.Label($"Speed Mult: x{CurrentSpeedMultiplier:F2} (cap {speedMaxMultiplier})");
            GUILayout.Label($"Mass Mult: x{CurrentMassMultiplier:F2} (cap {massMaxMultiplier})");
            GUILayout.Label($"In Battle: {isInBattle}");
            GUILayout.EndArea();
        }
#endif
    }
}