// ============================================================
// ProgressManager.cs
// 文件位置: Assets/Scripts/Logic/ProgressManager.cs
// 用途：玩家进度管理器 - 管理局内/局外进度 + 章节进度 + 体力系统
// 更新：添加体力自动恢复、离线补算、广告Mock接口
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;
using LightVsDecay.Data.Runtime;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Statistics;
using SettlementData = LightVsDecay.Core.SettlementData;

namespace LightVsDecay.Logic
{
    /// <summary>
    /// 玩家进度管理器
    /// 职责：
    /// - 管理局内进度（SessionData）
    /// - 管理局外进度（MetaData）
    /// - 管理章节进度（ChapterProgressData）
    /// - 体力自然恢复 + 离线补算 + 广告Mock
    /// - 响应游戏事件并更新数据
    /// - 广播数据变化给 UI
    /// </summary>
    public class ProgressManager : PersistentSingleton<ProgressManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("配置")]
        [SerializeField] private GameSettings settings;
        [SerializeField] private ChapterDatabase chapterDatabase;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private SessionData session = new SessionData();
        private MetaData    meta    = new MetaData();

        // ── 体力恢复计时器 ────────────────────────────────────
        /// <summary>距离下一次恢复已累积的秒数</summary>
        private float energyRecoveryTimer = 0f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>局外金币变化（新金额）</summary>
        public static event Action<int> OnGoldCoinsChanged;

        /// <summary>体力数量变化（current, max）</summary>
        public static event Action<int, int> OnEnergyChanged;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性 — 局内进度
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public int   CurrentLevel    => session.level;
        public int   CurrentExp      => session.exp;
        public int   ExpToNextLevel  => session.expToNextLevel;
        public float ExpProgress     => session.ExpProgress;
        public bool  IsMaxLevel      => session.level >= settings.maxLevel;
        public int   SessionCoins    => session.coins;
        public int   TotalCoins      => session.coins;
        public int   TotalKills      => session.totalKills;
        public int   CurrentCombo    => session.currentCombo;
        public int   MaxCombo        => session.maxCombo;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性 — 局外进度
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public int Gems       => meta.gems;
        public int GoldCoins  => meta.goldCoins;
        public int Energy     => meta.energy;
        public int MaxEnergy  => settings != null ? settings.maxEnergy : 5;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性 — 体力系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>是否处于溢出状态（能量 > 上限）</summary>
        public bool IsEnergyOverflow => meta.energy > settings.maxEnergy;

        /// <summary>体力是否已满（含溢出）</summary>
        public bool IsEnergyFull => meta.energy >= settings.maxEnergy;

        /// <summary>距下一点体力恢复还需的秒数（满/溢出时返回0）</summary>
        public float EnergyRecoverySecondsRemaining =>
            IsEnergyFull ? 0f : Mathf.Max(0f, settings.energyRecoveryInterval - energyRecoveryTimer);

        /// <summary>今日已看广告次数</summary>
        public int DailyAdWatchCount => meta.adWatchCountToday;

        /// <summary>每日广告上限</summary>
        public int MaxDailyAdWatches => settings != null ? settings.maxDailyAdWatches : 5;

        /// <summary>今日是否还可以看广告换体力</summary>
        public bool CanWatchAdForEnergy => meta.adWatchCountToday < MaxDailyAdWatches;

        /// <summary>每次广告给予的体力量</summary>
        public int AdEnergyReward => settings != null ? settings.adEnergyReward : 2;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性 — 章节进度
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public int TotalChapters         => meta.TotalChapters;
        public int UnlockedChapterIndex  => meta.UnlockedChapterIndex;

        public int CurrentViewChapterIndex
        {
            get => meta.currentViewChapterIndex;
            set
            {
                meta.currentViewChapterIndex = Mathf.Clamp(value, 0, TotalChapters - 1);
                meta.Save();
            }
        }

        public ChapterDatabase ChapterDatabase => chapterDatabase;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 数据访问
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public SessionData  Session  => session;
        public MetaData     Meta     => meta;
        public GameSettings Settings => settings;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void OnSingletonAwake()
        {
            ValidateSettings();

            int chapterCount = chapterDatabase != null ? chapterDatabase.ChapterCount : 3;
            meta.Load(settings.maxEnergy, chapterCount);

            // 离线恢复 & 广告每日重置（必须在 Load 之后）
            CalculateOfflineRecovery();
            CheckDailyAdReset();

            if (showDebugInfo)
            {
                GameLogger.Log($"[ProgressManager] 加载局外进度: Gems={meta.gems}, Gold={meta.goldCoins}, " +
                          $"Energy={meta.energy}, Chapters={meta.TotalChapters}");
                meta.PrintChapterProgress();
            }
        }

        private void Start()
        {
            SubscribeToGameEvents();
            SceneManager.sceneLoaded += OnSceneLoaded;
            ResetSession();
        }

        private void Update()
        {
            CheckComboTimeout();
            UpdateEnergyRecovery();
        }

        protected override void OnSingletonDestroy()
        {
            UnsubscribeFromGameEvents();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnApplicationQuit()
        {
            meta.lastSaveTimestamp = DateTime.UtcNow.ToString("O");
            meta.Save();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                meta.lastSaveTimestamp = DateTime.UtcNow.ToString("O");
                meta.Save();
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 体力系统 — 自然恢复
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void UpdateEnergyRecovery()
        {
            // 满载或溢出时不计时
            if (meta.energy >= settings.maxEnergy)
            {
                energyRecoveryTimer = 0f;
                return;
            }

            energyRecoveryTimer += Time.deltaTime;
            float interval = settings.energyRecoveryInterval;

            while (energyRecoveryTimer >= interval && meta.energy < settings.maxEnergy)
            {
                energyRecoveryTimer -= interval;
                meta.energy++;
                meta.Save();
                OnEnergyChanged?.Invoke(meta.energy, settings.maxEnergy);

                if (showDebugInfo)
                    GameLogger.Log($"[ProgressManager] ⚡ 自然恢复 +1 体力，当前: {meta.energy}/{settings.maxEnergy}");
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 体力系统 — 离线补算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void CalculateOfflineRecovery()
        {
            if (string.IsNullOrEmpty(meta.lastSaveTimestamp)) return;

            // 已满/溢出，无需补算
            if (meta.energy >= settings.maxEnergy) return;

            try
            {
                var lastSave      = DateTime.Parse(meta.lastSaveTimestamp, null,
                                        System.Globalization.DateTimeStyles.RoundtripKind);
                double elapsedSec = (DateTime.UtcNow - lastSave).TotalSeconds;
                if (elapsedSec <= 0) return;

                float  interval    = settings.energyRecoveryInterval;
                int    recoverable = Mathf.FloorToInt((float)elapsedSec / interval);

                if (recoverable > 0)
                {
                    int canRecover = settings.maxEnergy - meta.energy;
                    int recovered  = Mathf.Min(recoverable, canRecover);
                    meta.energy   += recovered;

                    // 计算本次恢复用完后剩余的余量，作为新计时器起点
                    float usedTime = recoverable * interval;
                    energyRecoveryTimer = Mathf.Clamp((float)elapsedSec - usedTime, 0f, interval);

                    meta.Save();
                    OnEnergyChanged?.Invoke(meta.energy, settings.maxEnergy);

                    if (showDebugInfo)
                        GameLogger.Log($"[ProgressManager] ⚡ 离线恢复 +{recovered} 体力，" +
                                  $"当前: {meta.energy}/{settings.maxEnergy}，" +
                                  $"计时器余量: {energyRecoveryTimer:F0}s");
                }
                else
                {
                    // 还不够一次恢复，但计时器要从上次时间戳延续
                    energyRecoveryTimer = Mathf.Clamp((float)elapsedSec, 0f, interval);
                }
            }
            catch (Exception e)
            {
                GameLogger.LogWarning($"[ProgressManager] 离线恢复计算失败: {e.Message}");
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 体力系统 — 广告Mock & 每日重置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 检查并重置广告每日计数（App启动时调用）
        /// </summary>
        private void CheckDailyAdReset()
        {
            string today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            if (meta.adLastResetDate != today)
            {
                meta.adWatchCountToday          = 0;
                meta.adGoldWatchCountToday      = 0;       // ★ 新增
                meta.adBlueprintWatchCountToday = 0;       // ★ 新增
                meta.adLastResetDate            = today;
                meta.Save();

                if (showDebugInfo)
                    GameLogger.Log($"[ProgressManager] 所有广告次数已重置（新的一天：{today}）");
            }
        }

        /// <summary>
        /// 看广告换体力（Mock实现）
        /// TODO: 接入真实广告SDK后，将奖励逻辑移至广告回调中调用 GrantAdEnergyReward()
        /// </summary>
        /// <returns>true=广告流程已启动；false=今日次数已达上限</returns>
        public bool WatchAdForEnergy()
        {
            if (!CanWatchAdForEnergy)
            {
                if (showDebugInfo)
                    GameLogger.Log("[ProgressManager] 今日广告次数已达上限");
                return false;
            }

            // ── Mock：直接给予奖励 ──
            // 真实SDK接入时：在此处调用广告展示，成功回调中调用 GrantAdEnergyReward()
            GrantAdEnergyReward();
            return true;
        }

        /// <summary>
        /// 广告奖励发放（供真实SDK回调调用）
        /// </summary>
        public void GrantAdEnergyReward()
        {
            meta.adWatchCountToday++;
            int reward = settings.adEnergyReward;

            // 溢出允许（不受 maxEnergy 限制）
            meta.energy += reward;
            meta.Save();

            OnEnergyChanged?.Invoke(meta.energy, settings.maxEnergy);

            if (showDebugInfo)
                GameLogger.Log($"[ProgressManager] 📺 广告奖励 +{reward} 体力，" +
                          $"当前: {meta.energy}/{settings.maxEnergy}，" +
                          $"今日次数: {meta.adWatchCountToday}/{settings.maxDailyAdWatches}");
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 广告换金币
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>今日金币广告是否还有剩余次数</summary>
        public bool CanWatchAdForGold => meta.adGoldWatchCountToday < MaxDailyAdWatches;

        /// <summary>金币广告奖励量</summary>
        public int AdGoldReward => settings != null ? settings.adGoldReward : 500;

        /// <summary>今日已看金币广告次数</summary>
        public int DailyAdGoldWatchCount => meta.adGoldWatchCountToday;

        /// <summary>
        /// 看广告换金币（Mock实现）
        /// TODO: 接入真实广告SDK后，在广告成功回调中调用 GrantAdGoldReward()
        /// </summary>
        public bool WatchAdForGold()
        {
            if (!CanWatchAdForGold)
            {
                if (showDebugInfo) GameLogger.Log("[ProgressManager] 今日金币广告次数已达上限");
                return false;
            }
            GrantAdGoldReward();
            return true;
        }

        /// <summary>广告金币奖励发放（供真实SDK回调）</summary>
        public void GrantAdGoldReward()
        {
            meta.adGoldWatchCountToday++;
            AddGoldCoins(settings.adGoldReward);

            if (showDebugInfo)
                GameLogger.Log($"[ProgressManager] 📺 广告金币 +{settings.adGoldReward}，" +
                          $"今日次数: {meta.adGoldWatchCountToday}/{MaxDailyAdWatches}");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 广告换图纸
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>今日图纸广告是否还有剩余次数</summary>
        public bool CanWatchAdForBlueprint => meta.adBlueprintWatchCountToday < MaxDailyAdWatches;

        /// <summary>图纸广告奖励量</summary>
        public int AdBlueprintReward => settings != null ? settings.adBlueprintReward : 3;

        /// <summary>今日已看图纸广告次数</summary>
        public int DailyAdBlueprintWatchCount => meta.adBlueprintWatchCountToday;

        /// <summary>
        /// 看广告换图纸（Mock实现）
        /// TODO: 接入真实广告SDK后，在广告成功回调中调用 GrantAdBlueprintReward()
        /// </summary>
        public bool WatchAdForBlueprint()
        {
            if (!CanWatchAdForBlueprint)
            {
                if (showDebugInfo) GameLogger.Log("[ProgressManager] 今日图纸广告次数已达上限");
                return false;
            }
            GrantAdBlueprintReward();
            return true;
        }

        /// <summary>广告图纸奖励发放（供真实SDK回调）</summary>
        public void GrantAdBlueprintReward()
        {
            meta.adBlueprintWatchCountToday++;
            Equipment.EquipmentManager.Instance?.AddBlueprints(settings.adBlueprintReward);

            if (showDebugInfo)
                GameLogger.Log($"[ProgressManager] 📺 广告图纸 +{settings.adBlueprintReward}，" +
                          $"今日次数: {meta.adBlueprintWatchCountToday}/{MaxDailyAdWatches}");
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

        public void AddGoldCoins(int amount)
        {
            if (amount <= 0) return;
            meta.goldCoins += amount;
            meta.Save();
            OnGoldCoinsChanged?.Invoke(meta.goldCoins);

            if (showDebugInfo)
                GameLogger.Log($"[ProgressManager] +{amount} 局外金币, 总计: {meta.goldCoins}");
        }

        public bool ConsumeGoldCoins(int amount)
        {
            if (meta.goldCoins < amount) return false;
            meta.goldCoins -= amount;
            meta.Save();
            OnGoldCoinsChanged?.Invoke(meta.goldCoins);

            if (showDebugInfo)
                GameLogger.Log($"[ProgressManager] -{amount} 局外金币, 剩余: {meta.goldCoins}");
            return true;
        }

        public bool ConsumeEnergy(int amount = 1)
        {
            if (meta.energy < amount) return false;
            meta.energy -= amount;
            meta.Save();
            OnEnergyChanged?.Invoke(meta.energy, settings.maxEnergy);
            return true;
        }

        public void RecoverEnergy(int amount = 1)
        {
            meta.energy = Mathf.Min(meta.energy + amount, settings.maxEnergy);
            meta.Save();
            OnEnergyChanged?.Invoke(meta.energy, settings.maxEnergy);
        }

        public void RefillEnergy()
        {
            meta.energy = settings.maxEnergy;
            meta.Save();
            OnEnergyChanged?.Invoke(meta.energy, settings.maxEnergy);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 章节进度操作
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public ChapterProgressData GetChapterProgress(int chapterIndex)
            => meta.GetChapterProgress(chapterIndex);

        public bool IsChapterUnlocked(int chapterIndex)
            => meta.IsChapterUnlocked(chapterIndex);

        public int GetChapterCompletedDifficulty(int chapterIndex)
            => meta.GetChapterCompletedDifficulty(chapterIndex);

        public int GetChapterNextDifficulty(int chapterIndex)
            => meta.GetChapterNextDifficulty(chapterIndex);

        public bool CompleteChapterDifficulty(int chapterIndex, int difficulty)
        {
            bool unlockedNew = meta.CompleteChapterDifficulty(chapterIndex, difficulty);
            meta.Save();

            if (showDebugInfo)
                GameLogger.Log($"[ProgressManager] 章节 {chapterIndex + 1} 难度 {difficulty} 完成" +
                          (unlockedNew ? $"，解锁章节{chapterIndex + 2}！" : ""));

            return unlockedNew;
        }

        public void UnlockChapter(int chapterIndex)
        {
            meta.UnlockChapter(chapterIndex);
            meta.Save();

            if (showDebugInfo)
                GameLogger.Log($"[ProgressManager] 解锁章节 {chapterIndex + 1}");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 局内进度操作
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void ResetSession()
        {
            session.Reset(settings);
            BroadcastAllStatus();

            if (showDebugInfo)
                GameLogger.Log("[ProgressManager] 局内进度已重置");
        }

        public void AddExp(int amount)
        {
            if (IsMaxLevel) return;

            session.exp += amount;

            while (session.exp >= session.expToNextLevel && !IsMaxLevel)
                LevelUp();

            GameEvents.TriggerExpChanged(session.exp, session.expToNextLevel);

            if (showDebugInfo)
                GameLogger.Log($"[ProgressManager] +{amount} XP, 当前: {session.exp}/{session.expToNextLevel}");
        }

        private void LevelUp()
        {
            session.exp -= session.expToNextLevel;
            session.level++;
            session.expToNextLevel = settings.CalculateExpToNextLevel(session.level);
            GameEvents.TriggerLevelUp(session.level);

            if (showDebugInfo)
                GameLogger.Log($"[ProgressManager] 升级! Lv.{session.level}");
        }

        public void ApplySkill(Data.SO.SkillType type)
        {
            int newLevel = session.GetSkillLevel(type) + 1;
            session.SetSkillLevel(type, newLevel);

            if (showDebugInfo)
                GameLogger.Log($"[ProgressManager] 技能 {type} 升至 Lv.{newLevel}");

            GameEvents.TriggerSkillApplied(type, newLevel);
        }
        /// <summary>
        /// 获取当前技能等级字典（供 SkillChooseOnePanel 生成三选一）
        /// </summary>
        public Dictionary<Data.SO.SkillType, int> GetSkillLevels()
        {
            return new Dictionary<Data.SO.SkillType, int>(session.skillLevels);
        }

        /// <summary>
        /// 获取指定技能的当前等级
        /// </summary>
        public int GetSkillLevel(Data.SO.SkillType type)
        {
            return session.skillLevels.TryGetValue(type, out int lv) ? lv : 0;
        }
        public void AddCoins(int amount)
        {
            session.coins += amount;
            GameEvents.TriggerCoinChanged(session.coins);

            if (showDebugInfo)
                GameLogger.Log($"[ProgressManager] +{amount} 金币, 总计: {session.coins}");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 击杀 / 连击系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void AddKill()
        {
            session.totalKills++;
            session.lastKillTime = Time.time;

            session.currentCombo++;
            if (session.currentCombo > session.maxCombo)
                session.maxCombo = session.currentCombo;

            GameEvents.TriggerKillCountChanged(session.totalKills);
            GameEvents.TriggerComboChanged(session.currentCombo);
        }

        private void CheckComboTimeout()
        {
            if (session.currentCombo > 0 && Time.time - session.lastKillTime > settings.comboTimeout)
                ResetCombo();
        }

        private void ResetCombo()
        {
            session.currentCombo = 0;
            GameEvents.TriggerComboReset();
            GameEvents.TriggerComboChanged(session.currentCombo);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 结算数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public SettlementData GetSettlementData(bool victory)
        {
            int wavesCleared = 0;
            int totalWaves   = 12;

            if (WaveManager.Instance != null)
            {
                int waveNum  = WaveManager.Instance.CurrentWaveNumber;
                totalWaves   = WaveManager.Instance.TotalWaves;
                wavesCleared = victory ? waveNum : Mathf.Max(0, waveNum - 1);
            }

            long totalDamage = 0;
            if (BattleStatistics.Instance != null)
                totalDamage = (long)BattleStatistics.Instance.GetTotalDamageDealt();

            return new SettlementData
            {
                isVictory    = victory,
                isPerfect    = victory,
                totalCoins   = session.coins,
                coinsEarned  = session.coins,
                survivalTime = GameManager.Instance != null ? GameManager.Instance.GameTimer : 0f,
                totalKills   = session.totalKills,
                killCount    = session.totalKills,
                maxCombo     = session.maxCombo,
                maxHitCount  = session.maxCombo,
                finalLevel   = session.level,
                totalDamage  = totalDamage,
                wavesCleared = wavesCleared,
                totalWaves   = totalWaves,
            };
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件订阅
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SubscribeToGameEvents();

            if (showDebugInfo)
                GameLogger.Log($"[ProgressManager] 场景 '{scene.name}' 加载，重新订阅事件");
        }

        private void SubscribeToGameEvents()
        {
            UnsubscribeFromGameEvents();
            GameEvents.OnEnemyDied      += OnEnemyDied;
            GameEvents.OnGameStart      += ResetSession;
            GameEvents.OnXPOrbCollected += OnXPOrbCollected;

            if (showDebugInfo)
                GameLogger.Log("[ProgressManager] 事件订阅完成");
        }

        private void UnsubscribeFromGameEvents()
        {
            GameEvents.OnEnemyDied      -= OnEnemyDied;
            GameEvents.OnGameStart      -= ResetSession;
            GameEvents.OnXPOrbCollected -= OnXPOrbCollected;
        }

        private void OnXPOrbCollected(int xp)
        {
            if (xp <= 0) return;
            AddExp(xp);
        }

        private void OnEnemyDied(EnemyType type, Vector3 pos, int xp, int coin)
        {
            AddKill();
            if (coin > 0) AddCoins(coin);

            if (showDebugInfo)
                GameLogger.Log($"[ProgressManager] 敌人死亡: {type}, XP:{xp}, Coin:{coin}");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试 / 重置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void ResetAllProgress()
        {
            int chapterCount = chapterDatabase != null ? chapterDatabase.ChapterCount : 3;
            meta.Reset(settings.maxEnergy, chapterCount);
            ResetSession();

            if (showDebugInfo)
                GameLogger.Log("[ProgressManager] 所有进度已重置");
        }

        public void ResetChapterProgress()
        {
            int chapterCount = chapterDatabase != null ? chapterDatabase.ChapterCount : 3;
            meta.ResetChapterProgress(chapterCount);

            if (showDebugInfo)
                GameLogger.Log("[ProgressManager] 章节进度已重置");
        }

        private void BroadcastAllStatus()
        {
            GameEvents.TriggerExpChanged(session.exp, session.expToNextLevel);
            GameEvents.TriggerCoinChanged(session.coins);
            GameEvents.TriggerKillCountChanged(session.totalKills);
            GameEvents.TriggerComboChanged(session.currentCombo);
        }

        private void ValidateSettings()
        {
            if (settings == null)
            {
                GameLogger.LogError("[ProgressManager] GameSettings 未设置！");
                settings = ScriptableObject.CreateInstance<GameSettings>();
            }
            if (chapterDatabase == null)
                GameLogger.LogWarning("[ProgressManager] ChapterDatabase 未设置！章节功能受限");
        }
    }
}