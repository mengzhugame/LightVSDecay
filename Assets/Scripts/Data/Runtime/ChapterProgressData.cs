// ============================================================
// ChapterProgressData.cs
// 文件位置: Assets/Scripts/Data/Runtime/ChapterProgressData.cs
// 用途：章节进度数据结构
// ============================================================

using UnityEngine;

namespace LightVsDecay.Data.Runtime
{
    /// <summary>
    /// 单个章节的进度数据
    /// </summary>
    [System.Serializable]
    public class ChapterProgressData
    {
        /// <summary>章节索引 (0-based)</summary>
        public int chapterIndex;
        
        /// <summary>是否已解锁</summary>
        public bool isUnlocked;
        
        /// <summary>
        /// 已通关的最高难度等级 (0-5)
        /// 0 = 未通关任何难度
        /// 1-5 = 已通关对应难度
        /// </summary>
        public int completedDifficulty;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 构造函数
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public ChapterProgressData()
        {
            chapterIndex = 0;
            isUnlocked = false;
            completedDifficulty = 0;
        }
        
        public ChapterProgressData(int index, bool unlocked = false, int completed = 0)
        {
            chapterIndex = index;
            isUnlocked = unlocked;
            completedDifficulty = completed;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 便捷属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 下一个要挑战的难度等级 (1-5)
        /// 如果已全部通关，返回最高难度5
        /// </summary>
        public int NextDifficulty => Mathf.Min(completedDifficulty + 1, 5);
        
        /// <summary>
        /// 是否已通关至少一个难度
        /// </summary>
        public bool HasCompletedAny => completedDifficulty > 0;
        
        /// <summary>
        /// 是否已通关所有5个难度
        /// </summary>
        public bool HasCompletedAll => completedDifficulty >= 5;
        
        /// <summary>
        /// 检查指定难度是否已通关
        /// </summary>
        /// <param name="difficulty">难度等级 (1-5)</param>
        public bool IsDifficultyCompleted(int difficulty)
        {
            return difficulty <= completedDifficulty;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 完成指定难度（只有当难度高于已完成时才更新）
        /// </summary>
        /// <param name="difficulty">完成的难度等级 (1-5)</param>
        /// <returns>是否有更新</returns>
        public bool CompleteDifficulty(int difficulty)
        {
            if (difficulty > completedDifficulty && difficulty <= 5)
            {
                completedDifficulty = difficulty;
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 解锁章节
        /// </summary>
        public void Unlock()
        {
            isUnlocked = true;
        }
        
        /// <summary>
        /// 重置进度
        /// </summary>
        /// <param name="keepUnlocked">是否保持解锁状态</param>
        public void Reset(bool keepUnlocked = false)
        {
            if (!keepUnlocked)
            {
                isUnlocked = false;
            }
            completedDifficulty = 0;
        }
        
        /// <summary>
        /// 创建副本
        /// </summary>
        public ChapterProgressData Clone()
        {
            return new ChapterProgressData(chapterIndex, isUnlocked, completedDifficulty);
        }
        
        /// <summary>
        /// 调试信息
        /// </summary>
        public override string ToString()
        {
            return $"Chapter[{chapterIndex}]: Unlocked={isUnlocked}, Completed={completedDifficulty}/5";
        }
    }
    
    /// <summary>
    /// 章节进度数据包装器（用于JSON序列化）
    /// </summary>
    [System.Serializable]
    public class ChapterProgressWrapper
    {
        public ChapterProgressData[] chapters;
        
        public ChapterProgressWrapper()
        {
            chapters = new ChapterProgressData[0];
        }
        
        public ChapterProgressWrapper(ChapterProgressData[] data)
        {
            chapters = data ?? new ChapterProgressData[0];
        }
    }
}