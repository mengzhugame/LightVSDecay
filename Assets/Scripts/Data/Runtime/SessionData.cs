// ============================================================
// SessionData.cs
// 文件位置: Assets/Scripts/Data/Runtime/SessionData.cs
// 用途：局内运行时数据（每局游戏重置）+ 局外持久化数据
// 更新：添加章节进度存储功能
// ============================================================

using LightVsDecay.Data.SO;
using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Data.Runtime
{
    /// <summary>
    /// 局内运行时数据
    /// 每局游戏开始时重置，结束时用于结算
    /// </summary>
    [System.Serializable]
    public class SessionData
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 经验/等级
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前等级</summary>
        public int level = 1;
        
        /// <summary>当前经验值</summary>
        public int exp = 0;
        
        /// <summary>升级所需经验</summary>
        public int expToNextLevel;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 金币
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>局内获得的金币</summary>
        public int coins = 0;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 大招能量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前大招能量</summary>
        public int ultEnergy = 0;
        
        /// <summary>大招是否准备就绪</summary>
        public bool ultReady = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 击杀/连击
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>总击杀数</summary>
        public int totalKills = 0;
        
        /// <summary>当前连击数</summary>
        public int currentCombo = 0;
        
        /// <summary>最大连击数</summary>
        public int maxCombo = 0;
        
        /// <summary>上次击杀时间</summary>
        public float lastKillTime = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>已解锁的技能及等级</summary>
        public System.Collections.Generic.Dictionary<SkillType, int> skillLevels 
            = new System.Collections.Generic.Dictionary<SkillType, int>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>经验进度 (0-1)</summary>
        public float ExpProgress => expToNextLevel > 0 ? (float)exp / expToNextLevel : 0f;
        
        /// <summary>大招进度 (0-1)</summary>
        public float UltProgress(int maxEnergy) => maxEnergy > 0 ? (float)ultEnergy / maxEnergy : 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 重置所有数据
        /// </summary>
        public void Reset(GameSettings settings)
        {
            level = 1;
            exp = 0;
            expToNextLevel = settings.CalculateExpToNextLevel(1);
            
            coins = 0;
            
            ultEnergy = 0;
            ultReady = false;
            
            totalKills = 0;
            currentCombo = 0;
            maxCombo = 0;
            lastKillTime = 0f;
            
            skillLevels.Clear();
        }
        
        /// <summary>
        /// 获取技能等级
        /// </summary>
        public int GetSkillLevel(SkillType type)
        {
            return skillLevels.TryGetValue(type, out int level) ? level : 0;
        }
        
        /// <summary>
        /// 设置技能等级
        /// </summary>
        public void SetSkillLevel(SkillType type, int level)
        {
            skillLevels[type] = level;
        }
        
        /// <summary>
        /// 创建副本
        /// </summary>
        public SessionData Clone()
        {
            var clone = new SessionData
            {
                level = this.level,
                exp = this.exp,
                expToNextLevel = this.expToNextLevel,
                coins = this.coins,
                ultEnergy = this.ultEnergy,
                ultReady = this.ultReady,
                totalKills = this.totalKills,
                currentCombo = this.currentCombo,
                maxCombo = this.maxCombo,
                lastKillTime = this.lastKillTime
            };
            
            foreach (var kvp in this.skillLevels)
            {
                clone.skillLevels[kvp.Key] = kvp.Value;
            }
            
            return clone;
        }
    }
    
    /// <summary>
    /// 局外持久化数据
    /// 跨局保存，使用 PlayerPrefs
    /// 更新：添加章节进度存储
    /// </summary>
    [System.Serializable]
    public class MetaData
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 货币数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>宝石（付费货币）</summary>
        public int gems = 0;
        
        /// <summary>金币（永久货币）</summary>
        public int goldCoins = 0;
        
        /// <summary>当前能量</summary>
        public int energy = 5;
        
        /// <summary>最大能量</summary>
        public int maxEnergy = 5;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 体力系统 —— 离线恢复 & 广告计数
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>上次保存的 UTC 时间戳（ISO-8601 格式），用于离线体力补算</summary>
        public string lastSaveTimestamp = "";

        /// <summary>今日已看广告次数</summary>
        public int adWatchCountToday = 0;

        /// <summary>最近一次广告计数重置的日期（yyyy-MM-dd 格式）</summary>
        public string adLastResetDate = "";
        /// <summary>今日金币广告已看次数</summary>
        public int adGoldWatchCountToday = 0;

        /// <summary>今日图纸广告已看次数</summary>
        public int adBlueprintWatchCountToday = 0;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 章节进度数据（新增）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>章节进度数组</summary>
        public ChapterProgressData[] chapterProgress;
        
        /// <summary>当前查看的章节索引（UI状态，非进度）</summary>
        public int currentViewChapterIndex = 0;

        public bool isFirstPlay = true;
        public bool hasSeenLaserTutorial = false;
        public bool hasSeenSkillTutorial = false;
        public bool hasSeenOverloadTutorial = false;
        public bool hasSeenDroneTutorial = false;
        public bool hasViewedTechTreeTutorial = false;
        public bool hasViewedEquipmentTutorial = false;

        /// <summary>科技树解锁提示（TipsPanel）是否已显示过</summary>
        public bool hasShownTechTreeUnlockTip = false;

        /// <summary>装备系统解锁提示（TipsPanel）是否已显示过</summary>
        public bool hasShownEquipmentUnlockTip = false;

        /// <summary>科技树首次升级是否已使用免费机会</summary>
        public bool hasUsedFirstTechUpgradeFree = false;

        /// <summary>装备界面首次合成是否已使用免费机会</summary>
        public bool hasUsedFirstEquipMergeFree = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 存档键
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private const string KEY_GEMS = "PlayerGems";
        private const string KEY_GOLD = "PlayerGoldCoins";
        private const string KEY_ENERGY = "PlayerEnergy";
        private const string KEY_CHAPTER_PROGRESS = "ChapterProgress";  // JSON格式存储
        private const string KEY_VIEW_CHAPTER = "CurrentViewChapter";
        private const string KEY_LAST_SAVE_TIME = "StaminaLastSaveTime";
        private const string KEY_AD_WATCH_COUNT = "StaminaAdWatchCount";
        private const string KEY_AD_RESET_DATE  = "StaminaAdResetDate";
        private const string KEY_AD_GOLD_COUNT      = "AdGoldWatchCount";
        private const string KEY_AD_BLUEPRINT_COUNT = "AdBlueprintWatchCount";
        private const string KEY_FIRST_PLAY = "Tutorial_IsFirstPlay";
        private const string KEY_SEEN_LASER_TUTORIAL = "Tutorial_HasSeenLaser";
        private const string KEY_SEEN_SKILL_TUTORIAL = "Tutorial_HasSeenSkill";
        private const string KEY_SEEN_OVERLOAD_TUTORIAL = "Tutorial_HasSeenOverload";
        private const string KEY_SEEN_DRONE_TUTORIAL = "Tutorial_HasSeenDrone";
        private const string KEY_VIEWED_TECH_TREE_TUTORIAL = "Tutorial_HasViewedTechTree";
        private const string KEY_VIEWED_EQUIPMENT_TUTORIAL = "Tutorial_HasViewedEquipment";
        private const string KEY_SHOWN_TECH_TREE_TIP   = "Tutorial_HasShownTechTreeTip";
        private const string KEY_SHOWN_EQUIPMENT_TIP   = "Tutorial_HasShownEquipmentTip";
        private const string KEY_FIRST_TECH_FREE  = "Tutorial_FirstTechFree";
        private const string KEY_FIRST_MERGE_FREE = "Tutorial_FirstMergeFree";
        // 默认章节数量
        private const int DEFAULT_CHAPTER_COUNT = 3;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 章节进度便捷属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 已解锁的最高章节索引
        /// </summary>
        public int UnlockedChapterIndex
        {
            get
            {
                if (chapterProgress == null) return 0;
                
                for (int i = chapterProgress.Length - 1; i >= 0; i--)
                {
                    if (chapterProgress[i].isUnlocked)
                    {
                        return i;
                    }
                }
                return 0;
            }
        }
        
        /// <summary>
        /// 总章节数
        /// </summary>
        public int TotalChapters => chapterProgress?.Length ?? 0;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 章节进度方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取指定章节的进度数据
        /// </summary>
        public ChapterProgressData GetChapterProgress(int chapterIndex)
        {
            if (chapterProgress != null && chapterIndex >= 0 && chapterIndex < chapterProgress.Length)
            {
                return chapterProgress[chapterIndex];
            }
            return null;
        }
        
        /// <summary>
        /// 检查章节是否已解锁
        /// </summary>
        public bool IsChapterUnlocked(int chapterIndex)
        {
            var progress = GetChapterProgress(chapterIndex);
            return progress?.isUnlocked ?? false;
        }
        
        /// <summary>
        /// 获取章节已通关的难度
        /// </summary>
        public int GetChapterCompletedDifficulty(int chapterIndex)
        {
            var progress = GetChapterProgress(chapterIndex);
            return progress?.completedDifficulty ?? 0;
        }
        
        /// <summary>
        /// 获取章节下一个要挑战的难度
        /// </summary>
        public int GetChapterNextDifficulty(int chapterIndex)
        {
            var progress = GetChapterProgress(chapterIndex);
            return progress?.NextDifficulty ?? 1;
        }
        
        /// <summary>
        /// 完成章节难度
        /// </summary>
        /// <param name="chapterIndex">章节索引</param>
        /// <param name="difficulty">完成的难度</param>
        /// <returns>是否需要解锁下一章节</returns>
        public bool CompleteChapterDifficulty(int chapterIndex, int difficulty)
        {
            var progress = GetChapterProgress(chapterIndex);
            if (progress == null) return false;
            
            bool updated = progress.CompleteDifficulty(difficulty);
            
            // 如果是难度1通关，检查是否需要解锁下一章节
            bool shouldUnlockNext = false;
            if (difficulty == 1 && updated)
            {
                int nextIndex = chapterIndex + 1;
                if (nextIndex < chapterProgress.Length)
                {
                    var nextProgress = chapterProgress[nextIndex];
                    if (!nextProgress.isUnlocked)
                    {
                        nextProgress.Unlock();
                        shouldUnlockNext = true;
                        GameLogger.Log($"[MetaData] 章节 {nextIndex + 1} 已解锁！");
                    }
                }
            }
            
            return shouldUnlockNext;
        }
        
        /// <summary>
        /// 解锁指定章节
        /// </summary>
        public void UnlockChapter(int chapterIndex)
        {
            var progress = GetChapterProgress(chapterIndex);
            progress?.Unlock();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 持久化方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 从 PlayerPrefs 加载
        /// </summary>
        public void Load(int defaultMaxEnergy = 5, int chapterCount = DEFAULT_CHAPTER_COUNT)
        {
            // 加载货币数据
            gems = PlayerPrefs.GetInt(KEY_GEMS, 0);
            goldCoins = PlayerPrefs.GetInt(KEY_GOLD, 0);
            energy = PlayerPrefs.GetInt(KEY_ENERGY, defaultMaxEnergy);
            maxEnergy = defaultMaxEnergy;
            // 体力系统
            lastSaveTimestamp  = PlayerPrefs.GetString(KEY_LAST_SAVE_TIME, "");
            adWatchCountToday  = PlayerPrefs.GetInt   (KEY_AD_WATCH_COUNT, 0);
            adLastResetDate    = PlayerPrefs.GetString(KEY_AD_RESET_DATE,  "");
            adGoldWatchCountToday      = PlayerPrefs.GetInt(KEY_AD_GOLD_COUNT,      0);
            adBlueprintWatchCountToday = PlayerPrefs.GetInt(KEY_AD_BLUEPRINT_COUNT, 0);
            isFirstPlay = PlayerPrefs.GetInt(KEY_FIRST_PLAY, 1) == 1;
            hasSeenLaserTutorial = PlayerPrefs.GetInt(KEY_SEEN_LASER_TUTORIAL, 0) == 1;
            hasSeenSkillTutorial = PlayerPrefs.GetInt(KEY_SEEN_SKILL_TUTORIAL, 0) == 1;
            hasSeenOverloadTutorial = PlayerPrefs.GetInt(KEY_SEEN_OVERLOAD_TUTORIAL, 0) == 1;
            hasSeenDroneTutorial = PlayerPrefs.GetInt(KEY_SEEN_DRONE_TUTORIAL, 0) == 1;
            hasViewedTechTreeTutorial = PlayerPrefs.GetInt(KEY_VIEWED_TECH_TREE_TUTORIAL, 0) == 1;
            hasViewedEquipmentTutorial = PlayerPrefs.GetInt(KEY_VIEWED_EQUIPMENT_TUTORIAL, 0) == 1;
            hasShownTechTreeUnlockTip  = PlayerPrefs.GetInt(KEY_SHOWN_TECH_TREE_TIP,   0) == 1;
            hasShownEquipmentUnlockTip = PlayerPrefs.GetInt(KEY_SHOWN_EQUIPMENT_TIP,   0) == 1;
            hasUsedFirstTechUpgradeFree = PlayerPrefs.GetInt(KEY_FIRST_TECH_FREE,  0) == 1;
            hasUsedFirstEquipMergeFree  = PlayerPrefs.GetInt(KEY_FIRST_MERGE_FREE, 0) == 1;
            // 加载章节进度
            LoadChapterProgress(chapterCount);
            
            // 加载UI状态
            currentViewChapterIndex = PlayerPrefs.GetInt(KEY_VIEW_CHAPTER, 0);
            
            GameLogger.Log($"[MetaData] 加载完成: Gems={gems}, Gold={goldCoins}, Energy={energy}, " +
                      $"Chapters={chapterProgress?.Length ?? 0}, Unlocked={UnlockedChapterIndex + 1}");
        }
        
        /// <summary>
        /// 加载章节进度（从JSON）
        /// </summary>
        private void LoadChapterProgress(int chapterCount)
        {
            string json = PlayerPrefs.GetString(KEY_CHAPTER_PROGRESS, "");
            
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<ChapterProgressWrapper>(json);
                    if (wrapper?.chapters != null && wrapper.chapters.Length > 0)
                    {
                        chapterProgress = wrapper.chapters;
                        
                        // 如果章节数增加了，扩展数组
                        if (chapterProgress.Length < chapterCount)
                        {
                            ExpandChapterProgress(chapterCount);
                        }
                        
                        return;
                    }
                }
                catch (System.Exception e)
                {
                    GameLogger.LogWarning($"[MetaData] 章节进度解析失败: {e.Message}");
                }
            }
            
            // 初始化默认章节进度
            InitializeDefaultChapterProgress(chapterCount);
        }
        
        /// <summary>
        /// 初始化默认章节进度
        /// </summary>
        private void InitializeDefaultChapterProgress(int chapterCount)
        {
            chapterProgress = new ChapterProgressData[chapterCount];
            
            for (int i = 0; i < chapterCount; i++)
            {
                chapterProgress[i] = new ChapterProgressData(i, i == 0, 0);
                // 第一章默认解锁
            }
            
            GameLogger.Log($"[MetaData] 初始化 {chapterCount} 个章节，第1章已解锁");
        }
        
        /// <summary>
        /// 扩展章节进度数组（当添加新章节时）
        /// </summary>
        private void ExpandChapterProgress(int newCount)
        {
            var oldProgress = chapterProgress;
            chapterProgress = new ChapterProgressData[newCount];
            
            // 复制旧数据
            for (int i = 0; i < oldProgress.Length; i++)
            {
                chapterProgress[i] = oldProgress[i];
            }
            
            // 初始化新章节（默认锁定）
            for (int i = oldProgress.Length; i < newCount; i++)
            {
                chapterProgress[i] = new ChapterProgressData(i, false, 0);
            }
            
            GameLogger.Log($"[MetaData] 章节数组扩展: {oldProgress.Length} -> {newCount}");
        }
        
        /// <summary>
        /// 保存到 PlayerPrefs
        /// </summary>
        public void Save()
        {
            // 保存货币数据
            PlayerPrefs.SetInt(KEY_GEMS, gems);
            PlayerPrefs.SetInt(KEY_GOLD, goldCoins);
            PlayerPrefs.SetInt(KEY_ENERGY, energy);
            // 体力系统
            PlayerPrefs.SetString(KEY_LAST_SAVE_TIME, lastSaveTimestamp);
            PlayerPrefs.SetInt   (KEY_AD_WATCH_COUNT, adWatchCountToday);
            PlayerPrefs.SetString(KEY_AD_RESET_DATE,  adLastResetDate);
            PlayerPrefs.SetInt(KEY_AD_GOLD_COUNT,      adGoldWatchCountToday);
            PlayerPrefs.SetInt(KEY_AD_BLUEPRINT_COUNT, adBlueprintWatchCountToday);
            PlayerPrefs.SetInt(KEY_FIRST_PLAY, isFirstPlay ? 1 : 0);
            PlayerPrefs.SetInt(KEY_SEEN_LASER_TUTORIAL, hasSeenLaserTutorial ? 1 : 0);
            PlayerPrefs.SetInt(KEY_SEEN_SKILL_TUTORIAL, hasSeenSkillTutorial ? 1 : 0);
            PlayerPrefs.SetInt(KEY_SEEN_OVERLOAD_TUTORIAL, hasSeenOverloadTutorial ? 1 : 0);
            PlayerPrefs.SetInt(KEY_SEEN_DRONE_TUTORIAL, hasSeenDroneTutorial ? 1 : 0);
            PlayerPrefs.SetInt(KEY_VIEWED_TECH_TREE_TUTORIAL, hasViewedTechTreeTutorial ? 1 : 0);
            PlayerPrefs.SetInt(KEY_VIEWED_EQUIPMENT_TUTORIAL, hasViewedEquipmentTutorial ? 1 : 0);
            PlayerPrefs.SetInt(KEY_SHOWN_TECH_TREE_TIP,   hasShownTechTreeUnlockTip  ? 1 : 0);
            PlayerPrefs.SetInt(KEY_SHOWN_EQUIPMENT_TIP,   hasShownEquipmentUnlockTip ? 1 : 0);
            PlayerPrefs.SetInt(KEY_FIRST_TECH_FREE,  hasUsedFirstTechUpgradeFree ? 1 : 0);
            PlayerPrefs.SetInt(KEY_FIRST_MERGE_FREE, hasUsedFirstEquipMergeFree  ? 1 : 0);
            // 保存章节进度（JSON格式）
            SaveChapterProgress();
            
            // 保存UI状态
            PlayerPrefs.SetInt(KEY_VIEW_CHAPTER, currentViewChapterIndex);
            
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// 保存章节进度（转JSON）
        /// </summary>
        private void SaveChapterProgress()
        {
            if (chapterProgress != null && chapterProgress.Length > 0)
            {
                var wrapper = new ChapterProgressWrapper(chapterProgress);
                string json = JsonUtility.ToJson(wrapper);
                PlayerPrefs.SetString(KEY_CHAPTER_PROGRESS, json);
            }
        }
        
        /// <summary>
        /// 重置所有数据
        /// </summary>
        public void Reset(int defaultMaxEnergy = 5, int chapterCount = DEFAULT_CHAPTER_COUNT)
        {
            gems = 0;
            goldCoins = 0;
            energy = defaultMaxEnergy;
            maxEnergy = defaultMaxEnergy;
            currentViewChapterIndex = 0;
            adWatchCountToday          = 0;
            adGoldWatchCountToday      = 0;
            adBlueprintWatchCountToday = 0;
            adLastResetDate            = "";
            isFirstPlay = true;
            hasSeenLaserTutorial = false;
            hasSeenSkillTutorial = false;
            hasSeenOverloadTutorial = false;
            hasSeenDroneTutorial = false;
            hasViewedTechTreeTutorial  = false;
            hasViewedEquipmentTutorial = false;
            hasShownTechTreeUnlockTip  = false;
            hasShownEquipmentUnlockTip = false;
            hasUsedFirstTechUpgradeFree = false;
            hasUsedFirstEquipMergeFree  = false;
            // 重置章节进度
            InitializeDefaultChapterProgress(chapterCount);
            
            Save();
            
            GameLogger.Log("[MetaData] 所有数据已重置");
        }
        
        /// <summary>
        /// 仅重置章节进度（不重置货币）
        /// </summary>
        public void ResetChapterProgress(int chapterCount = DEFAULT_CHAPTER_COUNT)
        {
            currentViewChapterIndex = 0;
            InitializeDefaultChapterProgress(chapterCount);
            Save();
            
            GameLogger.Log("[MetaData] 章节进度已重置");
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 打印章节进度（调试用）
        /// </summary>
        public void PrintChapterProgress()
        {
            GameLogger.Log("[MetaData] ========== 章节进度 ==========");
            
            if (chapterProgress == null || chapterProgress.Length == 0)
            {
                GameLogger.Log("  (无数据)");
                return;
            }
            
            for (int i = 0; i < chapterProgress.Length; i++)
            {
                var p = chapterProgress[i];
                string status = p.isUnlocked ? (p.HasCompletedAll ? "★全通关" : $"已解锁({p.completedDifficulty}/5)") : "🔒锁定";
                GameLogger.Log($"  章节{i + 1}: {status}");
            }
            
            GameLogger.Log("[MetaData] ================================");
        }
        
        /// <summary>
        /// 解锁所有章节（调试用）
        /// </summary>
        public void DebugUnlockAllChapters()
        {
            if (chapterProgress == null) return;
            
            foreach (var p in chapterProgress)
            {
                p.Unlock();
            }
            
            Save();
            GameLogger.Log("[MetaData] 调试：已解锁所有章节");
        }
        
        /// <summary>
        /// 完成所有难度（调试用）
        /// </summary>
        public void DebugCompleteAllDifficulties()
        {
            if (chapterProgress == null) return;
            
            foreach (var p in chapterProgress)
            {
                p.isUnlocked = true;
                p.completedDifficulty = 5;
            }
            
            Save();
            GameLogger.Log("[MetaData] 调试：已完成所有章节所有难度");
        }
    }
    
    /// <summary>
    /// 结算数据（给结算界面用）
    /// </summary>
    [System.Serializable]
    public class SettlementData
    {
        public int totalCoins;
        public int coinsEarned;
        public float survivalTime;
        public int totalKills;
        public int killCount;
        public int maxCombo;
        public int maxHitCount;
        public int finalLevel;
        public bool isVictory;
    }
}
