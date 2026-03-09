// ============================================================
// ProgressManager.cs (章节系统版)
// 文件位置: Assets/Scripts/Logic/ProgressManager.cs
// 用途：玩家进度管理器 - 管理局内/局外进度 + 章节进度
// 更新：添加章节进度管理功能
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;
using LightVsDecay.Data.Runtime;
using LightVsDecay.Data.SO;
using SettlementData = LightVsDecay.Core.SettlementData;
using UnityEngine.SceneManagement;

namespace LightVsDecay.Logic
{
    /// <summary>
    /// 玩家进度管理器
    /// 职责：
    /// - 管理局内进度（SessionData）
    /// - 管理局外进度（MetaData）
    /// - 管理章节进度（ChapterProgressData）
    /// - 响应游戏事件并更新数据
    /// - 广播数据变化给 UI
    /// </summary>
    public class ProgressManager : PersistentSingleton<ProgressManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("游戏设置（ScriptableObject）")]
        [SerializeField] private GameSettings settings;
        
        [Tooltip("章节数据库（ScriptableObject）")]
        [SerializeField] private ChapterDatabase chapterDatabase;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private SessionData session = new SessionData();
        private MetaData meta = new MetaData();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性 - 局内进度
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
        public int CurrentLevel => session.level;
        public int CurrentExp => session.exp;
        public int ExpToNextLevel => session.expToNextLevel;
        public float ExpProgress => session.ExpProgress;
        public bool IsMaxLevel => session.level >= settings.maxLevel;
        
        public int SessionCoins => session.coins;
        public int TotalCoins => session.coins; // 兼容旧代码

        public int TotalKills => session.totalKills;
        public int CurrentCombo => session.currentCombo;
        public int MaxCombo => session.maxCombo;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性 - 局外进度
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public int Gems => meta.gems;
        public int GoldCoins => meta.goldCoins;
        public int Energy => meta.energy;
        public int MaxEnergy => settings.maxEnergy;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性 - 章节进度（新增）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>章节总数</summary>
        public int TotalChapters => meta.TotalChapters;
        
        /// <summary>已解锁的最高章节索引 (0-based)</summary>
        public int UnlockedChapterIndex => meta.UnlockedChapterIndex;
        
        /// <summary>当前查看的章节索引 (UI状态)</summary>
        public int CurrentViewChapterIndex
        {
            get => meta.currentViewChapterIndex;
            set
            {
                meta.currentViewChapterIndex = Mathf.Clamp(value, 0, TotalChapters - 1);
                meta.Save();
            }
        }
        
        /// <summary>章节数据库引用</summary>
        public ChapterDatabase ChapterDatabase => chapterDatabase;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 数据访问
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>获取局内数据（只读）</summary>
        public SessionData Session => session;
        
        /// <summary>获取局外数据（只读）</summary>
        public MetaData Meta => meta;
        
        /// <summary>获取游戏设置</summary>
        public GameSettings Settings => settings;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            ValidateSettings();
            
            // 获取章节数量（从数据库或使用默认值）
            int chapterCount = chapterDatabase != null ? chapterDatabase.ChapterCount : 3;
            
            // 加载局外进度（包含章节进度）
            meta.Load(settings.maxEnergy, chapterCount);
            
            if (showDebugInfo)
            {
                Debug.Log($"[ProgressManager] 加载局外进度: Gems={meta.gems}, Gold={meta.goldCoins}, " +
                          $"Energy={meta.energy}, Chapters={meta.TotalChapters}");
                meta.PrintChapterProgress();
            }
        }
        
        private void Start()
        {
            // 订阅事件
            SubscribeToGameEvents();
    
            // 监听场景加载（应对 ClearAllEvents 清空订阅）
            SceneManager.sceneLoaded += OnSceneLoaded;
    
            // 初始化局内进度
            ResetSession();
        }
        
        private void Update()
        {
            CheckComboTimeout();
        }
        
        protected override void OnSingletonDestroy()
        {
            UnsubscribeFromGameEvents();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        /// <summary>
        /// 场景加载回调 —— 重新订阅被 ClearAllEvents 清空的事件
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 每次场景加载都重新订阅，防止 ClearAllEvents 清空
            SubscribeToGameEvents();
    
            if (showDebugInfo)
            {
                Debug.Log($"[ProgressManager] 场景 '{scene.name}' 加载，重新订阅事件");
            }
        }
        private void SubscribeToGameEvents()
        {
            // 先取消再订阅，防止重复注册
            UnsubscribeFromGameEvents();
    
            GameEvents.OnEnemyDied      += OnEnemyDied;
            GameEvents.OnGameStart      += ResetSession;
            GameEvents.OnXPOrbCollected += OnXPOrbCollected;
    
            if (showDebugInfo)
            {
                Debug.Log("[ProgressManager] 事件订阅完成");
            }
        }
        private void UnsubscribeFromGameEvents()
        {
            GameEvents.OnEnemyDied      -= OnEnemyDied;
            GameEvents.OnGameStart      -= ResetSession;
            GameEvents.OnXPOrbCollected -= OnXPOrbCollected;
        }
        /// <summary>
        /// 经验光点被收集
        /// </summary>
        private void OnXPOrbCollected(int xp)
        {
            if (xp <= 0) return;
    
            AddExp(xp);
        }
        
        private void OnApplicationQuit() => meta.Save();
        private void OnApplicationPause(bool pause) { if (pause) meta.Save(); }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置验证
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ValidateSettings()
        {
            if (settings == null)
            {
                Debug.LogError("[ProgressManager] GameSettings 未设置！创建默认配置...");
                settings = ScriptableObject.CreateInstance<GameSettings>();
            }
            
            if (chapterDatabase == null)
            {
                Debug.LogWarning("[ProgressManager] ChapterDatabase 未设置！章节功能将受限");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 章节进度操作（新增）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 检查章节是否已解锁
        /// </summary>
        public bool IsChapterUnlocked(int chapterIndex)
        {
            return meta.IsChapterUnlocked(chapterIndex);
        }
        
        /// <summary>
        /// 获取章节已通关的最高难度 (0-5, 0=未通关)
        /// </summary>
        public int GetChapterCompletedDifficulty(int chapterIndex)
        {
            return meta.GetChapterCompletedDifficulty(chapterIndex);
        }
        
        /// <summary>
        /// 获取章节下一个要挑战的难度 (1-5)
        /// </summary>
        public int GetChapterNextDifficulty(int chapterIndex)
        {
            return meta.GetChapterNextDifficulty(chapterIndex);
        }
        
        /// <summary>
        /// 获取章节进度数据
        /// </summary>
        public ChapterProgressData GetChapterProgress(int chapterIndex)
        {
            return meta.GetChapterProgress(chapterIndex);
        }
        
        /// <summary>
        /// 完成章节难度（通关时调用）
        /// </summary>
        /// <param name="chapterIndex">章节索引 (0-based)</param>
        /// <param name="difficulty">完成的难度等级 (1-5)</param>
        /// <returns>是否解锁了新章节</returns>
        public bool CompleteChapterDifficulty(int chapterIndex, int difficulty)
        {
            bool unlockedNew = meta.CompleteChapterDifficulty(chapterIndex, difficulty);
            meta.Save();
            
            if (showDebugInfo)
            {
                Debug.Log($"[ProgressManager] 完成章节{chapterIndex + 1}难度{difficulty}" +
                          (unlockedNew ? $"，解锁章节{chapterIndex + 2}！" : ""));
            }
            
            return unlockedNew;
        }
        
        /// <summary>
        /// 解锁指定章节（调试/购买用）
        /// </summary>
        public void UnlockChapter(int chapterIndex)
        {
            meta.UnlockChapter(chapterIndex);
            meta.Save();
            
            if (showDebugInfo)
            {
                Debug.Log($"[ProgressManager] 解锁章节 {chapterIndex + 1}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 局内进度操作
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>重置局内进度（新游戏开始）</summary>
        public void ResetSession()
        {
            session.Reset(settings);
            BroadcastAllStatus();
            
            if (showDebugInfo)
            {
                Debug.Log("[ProgressManager] 局内进度已重置");
            }
        }
        
        /// <summary>增加经验值</summary>
        public void AddExp(int amount)
        {
            if (IsMaxLevel) return;
            
            session.exp += amount;
            
            while (session.exp >= session.expToNextLevel && !IsMaxLevel)
            {
                LevelUp();
            }
            
            GameEvents.TriggerExpChanged(session.exp, session.expToNextLevel);
            
            if (showDebugInfo)
            {
                Debug.Log($"[ProgressManager] +{amount} XP, 当前: {session.exp}/{session.expToNextLevel}");
            }
        }
        
        private void LevelUp()
        {
            session.exp -= session.expToNextLevel;
            session.level++;
            session.expToNextLevel = settings.CalculateExpToNextLevel(session.level);
            
            GameEvents.TriggerLevelUp(session.level);
            
            if (showDebugInfo)
            {
                Debug.Log($"[ProgressManager] 升级! Lv.{session.level}");
            }
            
            // 暂停游戏，显示三选一界面
            Time.timeScale = 0f;
    
            // 通知 UIManager 显示技能选择面板
            if (UI.UIManager.Instance != null)
            {
                UI.UIManager.Instance.ShowSkillChoosePanel(session.level);
            }
        }
        
        /// <summary>增加局内金币</summary>
        public void AddCoins(int amount)
        {
            session.coins += amount;
            GameEvents.TriggerCoinChanged(session.coins);
            
            if (showDebugInfo)
            {
                Debug.Log($"[ProgressManager] +{amount} 金币, 总计: {session.coins}");
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 击杀/连击系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void AddKill()
        {
            session.totalKills++;
            session.lastKillTime = Time.time;
            
            session.currentCombo++;
            if (session.currentCombo > session.maxCombo)
            {
                session.maxCombo = session.currentCombo;
            }
            
            GameEvents.TriggerKillCountChanged(session.totalKills);
            GameEvents.TriggerComboChanged(session.currentCombo);
        }
        
        private void CheckComboTimeout()
        {
            if (session.currentCombo > 0 && Time.time - session.lastKillTime > settings.comboTimeout)
            {
                ResetCombo();
            }
        }
        
        private void ResetCombo()
        {
            session.currentCombo = 0;
            GameEvents.TriggerComboReset();
            GameEvents.TriggerComboChanged(session.currentCombo);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 敌人死亡处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnEnemyDied(EnemyType type, Vector3 pos, int xp, int coin)
        {
            AddKill();

            if (coin > 0) AddCoins(coin);

            if (showDebugInfo)
            {
                Debug.Log($"[ProgressManager] 敌人死亡: {type}, XP:{xp}, Coin:{coin}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能相关
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取当前技能等级字典（用于三选一生成）
        /// </summary>
        public Dictionary<SkillType, int> GetSkillLevels()
        {
            return new Dictionary<SkillType, int>(session.skillLevels);
        }

        /// <summary>
        /// 获取指定技能的当前等级
        /// </summary>
        public int GetSkillLevel(SkillType type)
        {
            return session.skillLevels.GetValueOrDefault(type, 0);
        }

        /// <summary>
        /// 应用选择的技能（升级或获得）
        /// </summary>
        public void ApplySkill(SkillType type)
        {
            // 更新等级
            if (session.skillLevels.ContainsKey(type))
            {
                session.skillLevels[type]++;
            }
            else
            {
                session.skillLevels[type] = 1;
            }
    
            int newLevel = session.skillLevels[type];
    
            if (showDebugInfo)
            {
                Debug.Log($"[ProgressManager] 技能升级: {type} -> Lv.{newLevel}");
            }
    
            // TODO: 后续在这里添加技能效果的实际应用逻辑
            GameEvents.TriggerSkillApplied(type, newLevel);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 局外进度操作
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void AddGems(int amount) { meta.gems += amount; meta.Save(); }
        public bool ConsumeGems(int amount)
        {
            if (meta.gems < amount) return false;
            meta.gems -= amount;
            meta.Save();
            return true;
        }
        
        public void AddGoldCoins(int amount) { meta.goldCoins += amount; meta.Save(); }
        public bool ConsumeGoldCoins(int amount)
        {
            if (meta.goldCoins < amount) return false;
            meta.goldCoins -= amount;
            meta.Save();
            return true;
        }
        
        public bool ConsumeEnergy(int amount = 1)
        {
            if (meta.energy < amount) return false;
            meta.energy -= amount;
            meta.Save();
            return true;
        }
        
        public void RecoverEnergy(int amount = 1)
        {
            meta.energy = Mathf.Min(meta.energy + amount, settings.maxEnergy);
            meta.Save();
        }
        
        public void RefillEnergy()
        {
            meta.energy = settings.maxEnergy;
            meta.Save();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 结算数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public SettlementData GetSettlementData()
        {
            return new SettlementData
            {
                totalCoins = session.coins,
                coinsEarned = session.coins,
                survivalTime = GameManager.Instance != null ? GameManager.Instance.GameTimer : 0f,
                totalKills = session.totalKills,
                killCount = session.totalKills,
                maxCombo = session.maxCombo,
                maxHitCount = session.maxCombo,
                finalLevel = session.level
            };
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void ResetAllProgress()
        {
            int chapterCount = chapterDatabase != null ? chapterDatabase.ChapterCount : 3;
            meta.Reset(settings.maxEnergy, chapterCount);
            ResetSession();
            
            if (showDebugInfo)
            {
                Debug.Log("[ProgressManager] 所有进度已重置");
            }
        }
        
        /// <summary>
        /// 仅重置章节进度
        /// </summary>
        public void ResetChapterProgress()
        {
            int chapterCount = chapterDatabase != null ? chapterDatabase.ChapterCount : 3;
            meta.ResetChapterProgress(chapterCount);
            
            if (showDebugInfo)
            {
                Debug.Log("[ProgressManager] 章节进度已重置");
            }
        }
        
        private void BroadcastAllStatus()
        {
            GameEvents.TriggerExpChanged(session.exp, session.expToNextLevel);
            GameEvents.TriggerCoinChanged(session.coins);
            GameEvents.TriggerKillCountChanged(session.totalKills);
            GameEvents.TriggerComboChanged(session.currentCombo);
        }
        
// #if UNITY_EDITOR
//         private void OnGUI()
//         {
//             if (!showDebugInfo) return;
//             
//             GUILayout.BeginArea(new Rect(10, 300, 250, 350));
//             
//             GUILayout.Label("=== Meta ===");
//             GUILayout.Label($"Gems: {meta.gems}");
//             GUILayout.Label($"Gold: {meta.goldCoins}");
//             GUILayout.Label($"Energy: {meta.energy}/{settings.maxEnergy}");
//             
//             GUILayout.Space(5);
//             GUILayout.Label("=== Chapters ===");
//             GUILayout.Label($"Total: {TotalChapters}, Unlocked: {UnlockedChapterIndex + 1}");
//             for (int i = 0; i < TotalChapters; i++)
//             {
//                 var p = GetChapterProgress(i);
//                 if (p != null)
//                 {
//                     string status = p.isUnlocked ? $"✓ {p.completedDifficulty}/5" : "🔒";
//                     GUILayout.Label($"  Ch.{i + 1}: {status}");
//                 }
//             }
//             
//             GUILayout.Space(5);
//             GUILayout.Label("=== Session ===");
//             GUILayout.Label($"Lv.{session.level} ({session.exp}/{session.expToNextLevel})");
//             GUILayout.Label($"Coins: {session.coins}");
//             GUILayout.Label($"Kills: {session.totalKills}");
//             GUILayout.Label($"Combo: {session.currentCombo} (Max:{session.maxCombo})");
//             
//             GUILayout.Space(5);
//             if (GUILayout.Button("+100 XP")) AddExp(100);
//             if (GUILayout.Button("+50 Coins")) AddCoins(50);
//             if (GUILayout.Button("+100 Gold")) AddGoldCoins(100);
//             
//             GUILayout.Space(5);
//             if (GUILayout.Button("Unlock All Chapters")) meta.DebugUnlockAllChapters();
//             if (GUILayout.Button("Complete All")) meta.DebugCompleteAllDifficulties();
//             
//             GUILayout.EndArea();
//         }
// #endif
    }
}