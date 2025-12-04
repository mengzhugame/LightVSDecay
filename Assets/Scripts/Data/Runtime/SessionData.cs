// ============================================================
// SessionData.cs
// 文件位置: Assets/Scripts/Data/Runtime/SessionData.cs
// 用途：局内运行时数据（每局游戏重置）
// 从 ProgressManager 拆分出的运行时状态
// ============================================================

using LightVsDecay.Data.SO;
using UnityEngine;

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
    /// </summary>
    [System.Serializable]
    public class MetaData
    {
        /// <summary>宝石（付费货币）</summary>
        public int gems = 0;
        
        /// <summary>金币（永久货币）</summary>
        public int goldCoins = 0;
        
        /// <summary>当前能量</summary>
        public int energy = 5;
        
        /// <summary>最大能量</summary>
        public int maxEnergy = 5;
        
        /// <summary>当前章节</summary>
        public int currentChapter = 1;
        
        /// <summary>当前难度</summary>
        public int currentDifficulty = 1;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 存档键
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private const string KEY_GEMS = "PlayerGems";
        private const string KEY_GOLD = "PlayerGoldCoins";
        private const string KEY_ENERGY = "PlayerEnergy";
        private const string KEY_CHAPTER = "CurrentChapter";
        private const string KEY_DIFFICULTY = "CurrentDifficulty";
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 持久化方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 从 PlayerPrefs 加载
        /// </summary>
        public void Load(int defaultMaxEnergy = 5)
        {
            gems = PlayerPrefs.GetInt(KEY_GEMS, 0);
            goldCoins = PlayerPrefs.GetInt(KEY_GOLD, 0);
            energy = PlayerPrefs.GetInt(KEY_ENERGY, defaultMaxEnergy);
            maxEnergy = defaultMaxEnergy;
            currentChapter = PlayerPrefs.GetInt(KEY_CHAPTER, 1);
            currentDifficulty = PlayerPrefs.GetInt(KEY_DIFFICULTY, 1);
        }
        
        /// <summary>
        /// 保存到 PlayerPrefs
        /// </summary>
        public void Save()
        {
            PlayerPrefs.SetInt(KEY_GEMS, gems);
            PlayerPrefs.SetInt(KEY_GOLD, goldCoins);
            PlayerPrefs.SetInt(KEY_ENERGY, energy);
            PlayerPrefs.SetInt(KEY_CHAPTER, currentChapter);
            PlayerPrefs.SetInt(KEY_DIFFICULTY, currentDifficulty);
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// 重置所有数据
        /// </summary>
        public void Reset(int defaultMaxEnergy = 5)
        {
            gems = 0;
            goldCoins = 0;
            energy = defaultMaxEnergy;
            maxEnergy = defaultMaxEnergy;
            currentChapter = 1;
            currentDifficulty = 1;
            Save();
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