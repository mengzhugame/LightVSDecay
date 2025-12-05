// ============================================================
// ProgressManager.cs (简化版)
// 文件位置: Assets/Scripts/Logic/ProgressManager.cs
// 用途：玩家进度管理器 - 简化后仅处理逻辑，数据存储在 Data 层
// ============================================================

using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;
using LightVsDecay.Data.Runtime;
using LightVsDecay.Data.SO;
using SettlementData = LightVsDecay.Core.SettlementData;

namespace LightVsDecay.Logic
{
    /// <summary>
    /// 玩家进度管理器 (简化版)
    /// 职责：
    /// - 管理局内进度（SessionData）
    /// - 管理局外进度（MetaData）
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
        
        public int UltEnergy => session.ultEnergy;
        public int UltMaxEnergy => settings.ultMaxEnergy;
        public float UltProgress => session.UltProgress(settings.ultMaxEnergy);
        public bool IsUltReady => session.ultReady;
        
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
            meta.Load(settings.maxEnergy);
            
            if (showDebugInfo)
            {
                Debug.Log($"[ProgressManager] 加载局外进度: Gems={meta.gems}, Gold={meta.goldCoins}, Energy={meta.energy}");
            }
        }
        
        private void Start()
        {
            // 订阅事件
            GameEvents.OnEnemyDied += OnEnemyDied;
            GameEvents.OnGameStart += ResetSession;
            GameEvents.OnXPOrbCollected += OnXPOrbCollected; 
            // 初始化局内进度
            ResetSession();
        }
        
        private void Update()
        {
            CheckComboTimeout();
        }
        
        protected override void OnSingletonDestroy()
        {
            GameEvents.OnEnemyDied -= OnEnemyDied;
            GameEvents.OnGameStart -= ResetSession;
            GameEvents.OnXPOrbCollected -= OnXPOrbCollected;
        }
        /// <summary>
        /// 经验光点被收集
        /// </summary>
        private void OnXPOrbCollected(int xp)
        {
            if (xp > 0)
            {
                AddExp(xp);
            }
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
            
            // 【新增】暂停游戏，显示三选一界面
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
        
        /// <summary>增加大招能量</summary>
        public void AddUltEnergy(int amount)
        {
            if (session.ultReady) return;
            
            session.ultEnergy = Mathf.Min(session.ultEnergy + amount, settings.ultMaxEnergy);
            GameEvents.TriggerUltEnergyChanged(session.ultEnergy, settings.ultMaxEnergy);
            
            if (!session.ultReady && session.ultEnergy >= settings.ultMaxEnergy)
            {
                session.ultReady = true;
                GameEvents.TriggerUltReady();
                
                if (showDebugInfo)
                {
                    Debug.Log("[ProgressManager] 大招已准备就绪！");
                }
            }
        }
        
        /// <summary>使用大招</summary>
        public bool UseUlt()
        {
            if (!session.ultReady) return false;
            
            session.ultEnergy = 0;
            session.ultReady = false;
            
            GameEvents.TriggerUltUsed();
            GameEvents.TriggerUltEnergyChanged(session.ultEnergy, settings.ultMaxEnergy);
            
            if (showDebugInfo)
            {
                Debug.Log("[ProgressManager] 大招已使用！");
            }
            
            return true;
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
            
            int ultGain = (type == EnemyType.Tank) 
                ? settings.ultEnergyPerEliteKill 
                : settings.ultEnergyPerKill;
            AddUltEnergy(ultGain);
            
            if (showDebugInfo)
            {
                Debug.Log($"[ProgressManager] 敌人死亡: {type}, XP:{xp}, Coin:{coin}, UltEnergy:{ultGain}");
            }
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
            meta.Reset(settings.maxEnergy);
            ResetSession();
            
            if (showDebugInfo)
            {
                Debug.Log("[ProgressManager] 所有进度已重置");
            }
        }
        
        private void BroadcastAllStatus()
        {
            GameEvents.TriggerExpChanged(session.exp, session.expToNextLevel);
            GameEvents.TriggerCoinChanged(session.coins);
            GameEvents.TriggerUltEnergyChanged(session.ultEnergy, settings.ultMaxEnergy);
            GameEvents.TriggerKillCountChanged(session.totalKills);
            GameEvents.TriggerComboChanged(session.currentCombo);
        }
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 300, 220, 280));
            GUILayout.Label("=== Meta ===");
            GUILayout.Label($"Gems: {meta.gems}");
            GUILayout.Label($"Gold: {meta.goldCoins}");
            GUILayout.Label($"Energy: {meta.energy}/{settings.maxEnergy}");
            
            GUILayout.Space(5);
            GUILayout.Label("=== Session ===");
            GUILayout.Label($"Lv.{session.level} ({session.exp}/{session.expToNextLevel})");
            GUILayout.Label($"Coins: {session.coins}");
            GUILayout.Label($"Ult: {session.ultEnergy}/{settings.ultMaxEnergy} {(session.ultReady ? "[READY]" : "")}");
            GUILayout.Label($"Kills: {session.totalKills}");
            GUILayout.Label($"Combo: {session.currentCombo} (Max:{session.maxCombo})");
            
            GUILayout.Space(5);
            if (GUILayout.Button("+100 XP")) AddExp(100);
            if (GUILayout.Button("+50 Coins")) AddCoins(50);
            if (GUILayout.Button("+50 Ult")) AddUltEnergy(50);
            if (GUILayout.Button("+100 Gold")) AddGoldCoins(100);
            
            GUILayout.EndArea();
        }
#endif
    }
}