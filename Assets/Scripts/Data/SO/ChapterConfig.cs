// ============================================================
// ChapterConfig.cs
// 文件位置: Assets/Scripts/Data/SO/ChapterConfig.cs
// 用途：单章节配置（ScriptableObject）
// ============================================================

using UnityEngine;
using LightVsDecay.Core;

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

        [Tooltip("经验掉落倍率（D1 短关模式设为2.0，让6波内达到Lv17；D2-D5保持1.0，通过playerLevelUpExpMultiplier控制升级速度）")]
        [Range(0.5f, 3f)]
        public float expDropMultiplier = 1f;

        [Tooltip("升级所需经验倍率（D2-D5 提高升级门槛，替代经验压缩方案，保证W9=Lv17锚点）")]
        [Range(0.5f, 3f)]
        public float playerLevelUpExpMultiplier = 1.0f;

        [Header("波次配置覆盖")]
        [Tooltip("此难度专属波次文件（留空则使用章节默认 waveConfig）")]
        public WaveConfig overrideWaveConfig;
        
        /// <summary>
        /// 创建默认难度配置（数值已按章节难度方案 V1.1 确认）
        /// Boss HP 封顶 2.20×，Boss ATK 最高 2.20×
        /// 升级经验门槛随难度线性提升（playerLevelUpExpMultiplier = countMultiplier），保证 W9 约 Lv17
        /// </summary>
        public static DifficultySettings CreateDefault(int level)
        {
            float[] countMults       = { 1.00f, 1.15f, 1.30f, 1.45f, 1.60f };
            float[] hpMults          = { 1.00f, 1.30f, 1.60f, 1.90f, 2.20f };
            float[] speedMults       = { 1.00f, 1.05f, 1.10f, 1.15f, 1.20f };
            float[] bossHpMults      = { 1.00f, 1.40f, 1.75f, 2.00f, 2.20f };
            float[] bossAtkMults     = { 1.00f, 1.25f, 1.55f, 1.90f, 2.20f };
            float[] coinMults        = { 1.00f, 1.25f, 1.55f, 1.80f, 2.00f };
            // 升级门槛与怪物数量同比例提升，杀怪全额获得经验，但升级变慢，维持 W9=Lv17 锚点
            float[] levelUpExpMults  = { 1.00f, 1.15f, 1.30f, 1.45f, 1.60f };

            int idx = Mathf.Clamp(level - 1, 0, 4);

            return new DifficultySettings
            {
                difficultyLevel           = level,
                displayName               = GetDefaultName(level),
                enemyHealthMultiplier     = hpMults[idx],
                enemyCountMultiplier      = countMults[idx],
                enemySpeedMultiplier      = speedMults[idx],
                bossHealthMultiplier      = bossHpMults[idx],
                bossAttackMultiplier      = bossAtkMults[idx],
                coinDropMultiplier        = coinMults[idx],
                expDropMultiplier         = 1.0f,               // 不再压缩经验，全额掉落
                playerLevelUpExpMultiplier = levelUpExpMults[idx] // D2-D5 提高升级门槛
            };
        }

        private static string GetDefaultName(int level)
        {
            return level switch
            {
                1 => "虚空之触",
                2 => "侵蚀之潮",
                3 => "精英冲击",
                4 => "噩梦深渊",
                5 => "混沌崩解",
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
        
        [Tooltip("流体怪物底色（MetaballsThreshold shader 的 _Color）")]
        public Color enemyBlobColor = new Color(0.1f, 0f, 0.2f, 1f); // 默认深紫/黑（第1章
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
            
            GameLogger.LogWarning($"[ChapterConfig] 难度 {difficulty} 不存在！");
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
            GameLogger.Log($"[ChapterConfig] 已生成 {MAX_DIFFICULTY} 个默认难度配置");
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
                GameLogger.LogWarning($"[ChapterConfig] 章节名称为空！");
                isValid = false;
            }
            
            if (chapterCardImage == null)
            {
                GameLogger.LogWarning($"[ChapterConfig] 章节卡片图片未设置！");
                isValid = false;
            }
            
            if (waveConfig == null)
            {
                GameLogger.LogWarning($"[ChapterConfig] 波次配置未设置！");
                isValid = false;
            }
            
            if (difficulties == null || difficulties.Length != MAX_DIFFICULTY)
            {
                GameLogger.LogWarning($"[ChapterConfig] 难度配置数量不正确！应为 {MAX_DIFFICULTY} 个");
                isValid = false;
            }
            
            if (isValid)
            {
                GameLogger.Log($"[ChapterConfig] 配置验证通过 ✓");
            }
        }
#endif
    }
}