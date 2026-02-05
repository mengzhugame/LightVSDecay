// ============================================================
// ChapterConfig.cs
// 文件位置: Assets/Scripts/Data/SO/ChapterConfig.cs
// 用途：单章节配置（ScriptableObject）
// ============================================================

using UnityEngine;

namespace LightVsDecay.Data.SO
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 章节特殊机制枚举
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 章节特殊机制类型
    /// </summary>
    public enum ChapterMechanicType
    {
        /// <summary>无特殊机制（章节1：深暗虚空）</summary>
        None = 0,
        
        /// <summary>岩浆机制（章节2：熔岩虚空）- 坦克死后残留岩浆斑，Boss喷火球</summary>
        Lava = 1,
        
        /// <summary>冰封机制（章节3：极寒虚空）- 怪物带冰甲，Boss可冰封炮塔</summary>
        Frozen = 2
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 难度倍率配置（内嵌类）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 难度倍率设置
    /// 用于调整同一章节不同难度下的数值
    /// </summary>
    [System.Serializable]
    public class DifficultySettings
    {
        [Tooltip("难度等级 (1-5)")]
        public int difficultyLevel = 1;
        
        [Tooltip("难度显示名称")]
        public string displayName = "普通";
        
        [Header("敌人倍率")]
        [Tooltip("敌人生命值倍率")]
        [Range(1f, 5f)]
        public float enemyHealthMultiplier = 1f;
        
        [Tooltip("敌人数量倍率")]
        [Range(1f, 3f)]
        public float enemyCountMultiplier = 1f;
        
        [Tooltip("敌人移动速度倍率")]
        [Range(1f, 2f)]
        public float enemySpeedMultiplier = 1f;
        
        [Header("Boss倍率")]
        [Tooltip("Boss生命值倍率")]
        [Range(1f, 5f)]
        public float bossHealthMultiplier = 1f;
        
        [Tooltip("Boss攻击力倍率")]
        [Range(1f, 3f)]
        public float bossAttackMultiplier = 1f;
        
        [Header("奖励倍率")]
        [Tooltip("金币掉落倍率")]
        [Range(1f, 3f)]
        public float coinDropMultiplier = 1f;
        
        [Tooltip("经验掉落倍率")]
        [Range(1f, 2f)]
        public float expDropMultiplier = 1f;
        
        /// <summary>
        /// 创建默认难度配置
        /// </summary>
        public static DifficultySettings CreateDefault(int level)
        {
            return new DifficultySettings
            {
                difficultyLevel = level,
                displayName = GetDefaultName(level),
                enemyHealthMultiplier = 1f + (level - 1) * 0.3f,      // 1.0, 1.3, 1.6, 1.9, 2.2
                enemyCountMultiplier = 1f + (level - 1) * 0.15f,      // 1.0, 1.15, 1.3, 1.45, 1.6
                enemySpeedMultiplier = 1f + (level - 1) * 0.05f,      // 1.0, 1.05, 1.1, 1.15, 1.2
                bossHealthMultiplier = 1f + (level - 1) * 0.4f,       // 1.0, 1.4, 1.8, 2.2, 2.6
                bossAttackMultiplier = 1f + (level - 1) * 0.2f,       // 1.0, 1.2, 1.4, 1.6, 1.8
                coinDropMultiplier = 1f + (level - 1) * 0.25f,        // 1.0, 1.25, 1.5, 1.75, 2.0
                expDropMultiplier = 1f + (level - 1) * 0.1f           // 1.0, 1.1, 1.2, 1.3, 1.4
            };
        }
        
        private static string GetDefaultName(int level)
        {
            return level switch
            {
                1 => "普通",
                2 => "困难",
                3 => "噩梦",
                4 => "地狱",
                5 => "深渊",
                _ => $"难度{level}"
            };
        }
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 章节配置 ScriptableObject
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 章节配置 (ScriptableObject)
    /// 定义单个章节的所有配置数据
    /// </summary>
    [CreateAssetMenu(fileName = "Chapter_New", menuName = "LightVsDecay/Chapter Config", order = 10)]
    public class ChapterConfig : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 基本信息
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 基本信息 ═══")]
        [Tooltip("章节索引 (0-based，用于程序逻辑)")]
        public int chapterIndex = 0;
        
        [Tooltip("章节显示编号 (1-based，用于UI显示)")]
        public int chapterNumber = 1;
        
        [Tooltip("章节名称（中文）")]
        public string chapterName = "新章节";
        
        [Tooltip("章节描述")]
        [TextArea(2, 4)]
        public string chapterDescription = "";
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 视觉资源
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 视觉资源 ═══")]
        [Tooltip("章节选择界面的背景图")]
        public Sprite chapterCardImage;
        
        [Tooltip("战斗场景背景图")]
        public Sprite battleBackgroundImage;
        
        [Tooltip("章节主题色（用于UI高亮等）")]
        public Color themeColor = new Color(0f, 1f, 1f, 1f); // 默认青色
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 音频资源
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 音频资源 ═══")]
        [Tooltip("章节战斗BGM（不设置则使用默认BGM）")]
        public AudioClip battleBGM;
        
        [Tooltip("Boss战BGM（不设置则继续播放战斗BGM）")]
        public AudioClip bossBGM;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 游戏配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 游戏配置 ═══")]
        [Tooltip("波次配置（定义敌人生成规则）")]
        public WaveConfig waveConfig;
        
        [Tooltip("Boss预制体（覆盖WaveConfig中的默认Boss）")]
        public GameObject bossPrefab;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 特殊机制
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 特殊机制 ═══")]
        [Tooltip("章节特殊机制类型")]
        public ChapterMechanicType mechanicType = ChapterMechanicType.None;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 难度设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 难度设置 ═══")]
        [Tooltip("5个难度等级的配置")]
        public DifficultySettings[] difficulties = new DifficultySettings[5];
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 常量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public const int MAX_DIFFICULTY = 5;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取指定难度的配置
        /// </summary>
        /// <param name="difficulty">难度等级 (1-5)</param>
        /// <returns>难度配置，如果不存在返回null</returns>
        public DifficultySettings GetDifficulty(int difficulty)
        {
            int index = difficulty - 1;
            if (index >= 0 && index < difficulties.Length)
            {
                return difficulties[index];
            }
            
            Debug.LogWarning($"[ChapterConfig] 难度 {difficulty} 不存在！");
            return null;
        }
        
        /// <summary>
        /// 获取章节显示标题（带编号）
        /// </summary>
        public string DisplayTitle => $"{chapterNumber}.{chapterName}";
        
        /// <summary>
        /// 检查是否有特殊机制
        /// </summary>
        public bool HasSpecialMechanic => mechanicType != ChapterMechanicType.None;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器工具
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        /// <summary>
        /// 生成默认5个难度配置
        /// </summary>
        [ContextMenu("生成默认难度配置")]
        public void GenerateDefaultDifficulties()
        {
            difficulties = new DifficultySettings[MAX_DIFFICULTY];
            for (int i = 0; i < MAX_DIFFICULTY; i++)
            {
                difficulties[i] = DifficultySettings.CreateDefault(i + 1);
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ChapterConfig] 已生成 {MAX_DIFFICULTY} 个默认难度配置");
        }
        
        /// <summary>
        /// 验证配置完整性
        /// </summary>
        [ContextMenu("验证配置")]
        public void ValidateConfig()
        {
            bool isValid = true;
            
            if (string.IsNullOrEmpty(chapterName))
            {
                Debug.LogWarning($"[ChapterConfig] 章节名称为空！");
                isValid = false;
            }
            
            if (chapterCardImage == null)
            {
                Debug.LogWarning($"[ChapterConfig] 章节卡片图片未设置！");
                isValid = false;
            }
            
            if (waveConfig == null)
            {
                Debug.LogWarning($"[ChapterConfig] 波次配置未设置！");
                isValid = false;
            }
            
            if (difficulties == null || difficulties.Length != MAX_DIFFICULTY)
            {
                Debug.LogWarning($"[ChapterConfig] 难度配置数量不正确！应为 {MAX_DIFFICULTY} 个");
                isValid = false;
            }
            
            if (isValid)
            {
                Debug.Log($"[ChapterConfig] 配置验证通过 ✓");
            }
        }
#endif
    }
}