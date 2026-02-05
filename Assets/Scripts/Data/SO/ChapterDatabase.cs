// ============================================================
// ChapterDatabase.cs
// 文件位置: Assets/Scripts/Data/SO/ChapterDatabase.cs
// 用途：章节数据库（ScriptableObject）- 管理所有章节配置
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace LightVsDecay.Data.SO
{
    /// <summary>
    /// 章节数据库 (ScriptableObject)
    /// 统一管理所有章节的配置引用
    /// </summary>
    [CreateAssetMenu(fileName = "ChapterDatabase", menuName = "LightVsDecay/Chapter Database", order = 11)]
    public class ChapterDatabase : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 章节列表
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 章节列表 ═══")]
        [Tooltip("所有章节配置（按顺序排列）")]
        [SerializeField] private List<ChapterConfig> chapters = new List<ChapterConfig>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 默认资源
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 默认资源 ═══")]
        [Tooltip("锁定图标（显示在未解锁章节上）")]
        public Sprite lockIcon;
        
        [Tooltip("难度图标 - 已通关（亮）")]
        public Sprite difficultyIconActive;
        
        [Tooltip("难度图标 - 未通关（暗）")]
        public Sprite difficultyIconInactive;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Dictionary<int, ChapterConfig> _indexCache;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 章节总数
        /// </summary>
        public int ChapterCount => chapters?.Count ?? 0;
        
        /// <summary>
        /// 所有章节配置（只读）
        /// </summary>
        public IReadOnlyList<ChapterConfig> Chapters => chapters;
        
        /// <summary>
        /// 最后一个章节的索引
        /// </summary>
        public int LastChapterIndex => Mathf.Max(0, ChapterCount - 1);
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 根据章节索引获取配置
        /// </summary>
        /// <param name="chapterIndex">章节索引 (0-based)</param>
        /// <returns>章节配置，如果不存在返回null</returns>
        public ChapterConfig GetChapter(int chapterIndex)
        {
            if (chapterIndex >= 0 && chapterIndex < chapters.Count)
            {
                return chapters[chapterIndex];
            }
            
            Debug.LogWarning($"[ChapterDatabase] 章节索引 {chapterIndex} 超出范围！(总数: {ChapterCount})");
            return null;
        }
        
        /// <summary>
        /// 根据章节编号获取配置（1-based）
        /// </summary>
        /// <param name="chapterNumber">章节编号 (1-based，对应UI显示)</param>
        /// <returns>章节配置，如果不存在返回null</returns>
        public ChapterConfig GetChapterByNumber(int chapterNumber)
        {
            return GetChapter(chapterNumber - 1);
        }
        
        /// <summary>
        /// 检查章节索引是否有效
        /// </summary>
        public bool IsValidChapterIndex(int chapterIndex)
        {
            return chapterIndex >= 0 && chapterIndex < ChapterCount;
        }
        
        /// <summary>
        /// 检查是否为第一章
        /// </summary>
        public bool IsFirstChapter(int chapterIndex)
        {
            return chapterIndex == 0;
        }
        
        /// <summary>
        /// 检查是否为最后一章
        /// </summary>
        public bool IsLastChapter(int chapterIndex)
        {
            return chapterIndex == LastChapterIndex;
        }
        
        /// <summary>
        /// 获取下一章节索引（如果存在）
        /// </summary>
        /// <param name="currentIndex">当前章节索引</param>
        /// <returns>下一章节索引，如果已是最后一章返回-1</returns>
        public int GetNextChapterIndex(int currentIndex)
        {
            int next = currentIndex + 1;
            return IsValidChapterIndex(next) ? next : -1;
        }
        
        /// <summary>
        /// 获取上一章节索引（如果存在）
        /// </summary>
        /// <param name="currentIndex">当前章节索引</param>
        /// <returns>上一章节索引，如果已是第一章返回-1</returns>
        public int GetPreviousChapterIndex(int currentIndex)
        {
            int prev = currentIndex - 1;
            return prev >= 0 ? prev : -1;
        }
        
        /// <summary>
        /// 遍历所有章节
        /// </summary>
        public System.Collections.Generic.IEnumerable<(int index, ChapterConfig config)> EnumerateChapters()
        {
            for (int i = 0; i < chapters.Count; i++)
            {
                yield return (i, chapters[i]);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器工具
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        /// <summary>
        /// 验证所有章节配置
        /// </summary>
        [ContextMenu("验证所有章节")]
        public void ValidateAllChapters()
        {
            Debug.Log($"[ChapterDatabase] 开始验证 {ChapterCount} 个章节...");
            
            bool allValid = true;
            
            for (int i = 0; i < chapters.Count; i++)
            {
                var chapter = chapters[i];
                
                if (chapter == null)
                {
                    Debug.LogError($"[ChapterDatabase] 章节 {i} 为空！");
                    allValid = false;
                    continue;
                }
                
                // 检查索引是否匹配
                if (chapter.chapterIndex != i)
                {
                    Debug.LogWarning($"[ChapterDatabase] 章节 '{chapter.chapterName}' 的 chapterIndex ({chapter.chapterIndex}) 与数组位置 ({i}) 不匹配！");
                }
                
                // 检查编号是否正确
                if (chapter.chapterNumber != i + 1)
                {
                    Debug.LogWarning($"[ChapterDatabase] 章节 '{chapter.chapterName}' 的 chapterNumber ({chapter.chapterNumber}) 应为 {i + 1}");
                }
                
                // 调用章节自身的验证
                chapter.ValidateConfig();
            }
            
            // 检查默认资源
            if (lockIcon == null)
            {
                Debug.LogWarning("[ChapterDatabase] 锁定图标未设置！");
                allValid = false;
            }
            
            if (difficultyIconActive == null)
            {
                Debug.LogWarning("[ChapterDatabase] 难度图标（亮）未设置！");
                allValid = false;
            }
            
            if (difficultyIconInactive == null)
            {
                Debug.LogWarning("[ChapterDatabase] 难度图标（暗）未设置！");
                allValid = false;
            }
            
            if (allValid)
            {
                Debug.Log($"[ChapterDatabase] 所有章节验证通过 ✓");
            }
        }
        
        /// <summary>
        /// 自动修复章节索引
        /// </summary>
        [ContextMenu("自动修复章节索引")]
        public void AutoFixChapterIndices()
        {
            for (int i = 0; i < chapters.Count; i++)
            {
                if (chapters[i] != null)
                {
                    chapters[i].chapterIndex = i;
                    chapters[i].chapterNumber = i + 1;
                    UnityEditor.EditorUtility.SetDirty(chapters[i]);
                }
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ChapterDatabase] 已自动修复 {ChapterCount} 个章节的索引");
        }
        
        /// <summary>
        /// 打印章节信息
        /// </summary>
        [ContextMenu("打印章节信息")]
        public void PrintChapterInfo()
        {
            Debug.Log($"[ChapterDatabase] ========== 章节信息 ==========");
            Debug.Log($"总章节数: {ChapterCount}");
            
            for (int i = 0; i < chapters.Count; i++)
            {
                var ch = chapters[i];
                if (ch != null)
                {
                    Debug.Log($"  [{i}] {ch.DisplayTitle} - 机制: {ch.mechanicType}");
                }
                else
                {
                    Debug.Log($"  [{i}] (空)");
                }
            }
            
            Debug.Log($"[ChapterDatabase] ================================");
        }
#endif
    }
}