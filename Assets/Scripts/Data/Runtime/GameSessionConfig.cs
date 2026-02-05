// ============================================================
// GameSessionConfig.cs
// 文件位置: Assets/Scripts/Data/Runtime/GameSessionConfig.cs
// 用途：运行时游戏会话配置（静态类）
// 说明：用于在主菜单和战斗场景之间传递玩家选择的章节/难度
// ============================================================

using UnityEngine;

namespace LightVsDecay.Data.Runtime
{
    /// <summary>
    /// 游戏会话配置（静态类）
    /// 用于存储当前游戏会话的配置信息
    /// 在 MainMenuScene 设置，在 GameScene 读取
    /// </summary>
    public static class GameSessionConfig
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 当前会话数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 当前选择的章节索引 (0-based)
        /// </summary>
        public static int SelectedChapterIndex { get; private set; } = 0;
        
        /// <summary>
        /// 当前选择的难度等级 (1-5)
        /// </summary>
        public static int SelectedDifficulty { get; private set; } = 1;
        
        /// <summary>
        /// 是否已设置（用于检测是否从主菜单正常进入）
        /// </summary>
        public static bool IsConfigured { get; private set; } = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 便捷属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 当前章节编号 (1-based，用于UI显示)
        /// </summary>
        public static int SelectedChapterNumber => SelectedChapterIndex + 1;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置游戏会话配置（在主菜单点击开始游戏时调用）
        /// </summary>
        /// <param name="chapterIndex">章节索引 (0-based)</param>
        /// <param name="difficulty">难度等级 (1-5)</param>
        public static void Set(int chapterIndex, int difficulty)
        {
            SelectedChapterIndex = Mathf.Max(0, chapterIndex);
            SelectedDifficulty = Mathf.Clamp(difficulty, 1, 5);
            IsConfigured = true;
            
            Debug.Log($"[GameSessionConfig] 设置会话: 章节={SelectedChapterNumber}, 难度={SelectedDifficulty}");
        }
        
        /// <summary>
        /// 重置配置（游戏结束返回主菜单时调用）
        /// </summary>
        public static void Reset()
        {
            SelectedChapterIndex = 0;
            SelectedDifficulty = 1;
            IsConfigured = false;
            
            Debug.Log("[GameSessionConfig] 会话配置已重置");
        }
        
        /// <summary>
        /// 使用默认配置（用于直接进入GameScene调试时）
        /// </summary>
        public static void UseDefaultIfNotConfigured()
        {
            if (!IsConfigured)
            {
                Debug.LogWarning("[GameSessionConfig] 未配置会话，使用默认值: 章节1, 难度1");
                Set(0, 1);
            }
        }
        
        /// <summary>
        /// 获取调试信息字符串
        /// </summary>
        public static string GetDebugInfo()
        {
            return $"Chapter: {SelectedChapterNumber} (Index: {SelectedChapterIndex}), " +
                   $"Difficulty: {SelectedDifficulty}, " +
                   $"Configured: {IsConfigured}";
        }
    }
}