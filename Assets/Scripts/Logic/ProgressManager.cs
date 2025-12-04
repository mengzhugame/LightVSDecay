using LightVsDecay.Core;
using UnityEngine;
using LightVsDecay.Core.Pool;

namespace LightVsDecay.Logic
{
    /// <summary>
    /// 玩家进度管理器 (单例)
    /// 负责：
    /// - 局内进度：经验值、等级、局内金币、大招能量、击杀数、连击数
    /// - 局外进度：宝石、金币（永久）、能量
    /// </summary>
    public class ProgressManager : PersistentSingleton<ProgressManager>
    {

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 局外进度（Meta）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("局外进度 - 能量系统")]
        [Tooltip("最大能量值")]
        [SerializeField] private int maxEnergy = 5;
        
        [Tooltip("能量恢复间隔（秒）")]
        [SerializeField] private float energyRecoveryInterval = 600f; // 10分钟
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 经验系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("局内经验系统")]
        [Tooltip("升级公式基础值: XP = Base + (Level * Growth)")]
        [SerializeField] private int expBase = 5;
        
        [Tooltip("升级公式增量系数")]
        [SerializeField] private int expGrowth = 5;
        
        [Tooltip("最大等级")]
        [SerializeField] private int maxLevel = 20;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 大招系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("大招系统")]
        [Tooltip("大招最大能量")]
        [SerializeField] private int ultMaxEnergy = 100;
        
        [Tooltip("每次击杀获得的大招能量（小怪）")]
        [SerializeField] private int ultEnergyPerKill = 2;
        
        [Tooltip("精英怪击杀获得的大招能量（Tank）")]
        [SerializeField] private int ultEnergyPerEliteKill = 5;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 连击系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("连击系统")]
        [Tooltip("连击超时时间（秒）")]
        [SerializeField] private float comboTimeout = 2.0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 局外进度（持久化）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int gems = 0;           // 宝石（付费货币）
        private int goldCoins = 0;      // 金币（永久货币）
        private int energy = 5;         // 当前能量
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 局内进度（每局重置）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        // 经验/等级
        private int currentLevel = 1;
        private int currentExp = 0;
        private int expToNextLevel;
        
        // 局内金币
        private int sessionCoins = 0;
        
        // 大招能量
        private int ultEnergy = 0;
        private bool ultReady = false;
        
        // 击杀/连击
        private int totalKills = 0;
        private int currentCombo = 0;
        private int maxCombo = 0;
        private float lastKillTime = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性 - 局外进度
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>宝石（付费货币）</summary>
        public int Gems => gems;
        
        /// <summary>金币（永久货币）</summary>
        public int GoldCoins => goldCoins;
        
        /// <summary>当前能量</summary>
        public int Energy => energy;
        
        /// <summary>最大能量</summary>
        public int MaxEnergy => maxEnergy;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性 - 局内进度
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        // 经验/等级
        public int CurrentLevel => currentLevel;
        public int CurrentExp => currentExp;
        public int ExpToNextLevel => expToNextLevel;
        public float ExpProgress => expToNextLevel > 0 ? (float)currentExp / expToNextLevel : 0f;
        public bool IsMaxLevel => currentLevel >= maxLevel;
        
        // 局内金币
        public int TotalCoins => sessionCoins;
        
        // 大招
        public int UltEnergy => ultEnergy;
        public int UltMaxEnergy => ultMaxEnergy;
        public float UltProgress => ultMaxEnergy > 0 ? (float)ultEnergy / ultMaxEnergy : 0f;
        public bool IsUltReady => ultReady;
        
        // 击杀/连击
        public int TotalKills => totalKills;
        public int CurrentCombo => currentCombo;
        public int MaxCombo => maxCombo;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            // 加载持久化数据
            LoadMetaProgress();
        }
        
        private void Start()
        {
            // 订阅敌人死亡事件
            GameEvents.OnEnemyDied += OnEnemyDied;
            
            // 订阅游戏开始事件
            GameEvents.OnGameStart += ResetSessionProgress;
            
            // 初始化局内进度
            ResetSessionProgress();
        }
        
        private void Update()
        {
            // 检查连击超时
            CheckComboTimeout();
        }
        
        protected override void OnSingletonDestroy()
        {
            // 取消订阅
            GameEvents.OnEnemyDied -= OnEnemyDied;
            GameEvents.OnGameStart -= ResetSessionProgress;
        }
        
        private void OnApplicationQuit()
        {
            // 退出时保存
            SaveMetaProgress();
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            // 后台时保存
            if (pauseStatus)
            {
                SaveMetaProgress();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 持久化（局外进度）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 加载局外进度
        /// </summary>
        private void LoadMetaProgress()
        {
            gems = PlayerPrefs.GetInt("PlayerGems", 0);
            goldCoins = PlayerPrefs.GetInt("PlayerGoldCoins", 0);
            energy = PlayerPrefs.GetInt("PlayerEnergy", maxEnergy);
            
            if (showDebugInfo)
            {
                Debug.Log($"[PlayerProgressManager] 加载局外进度: Gems={gems}, GoldCoins={goldCoins}, Energy={energy}");
            }
        }
        
        /// <summary>
        /// 保存局外进度
        /// </summary>
        private void SaveMetaProgress()
        {
            PlayerPrefs.SetInt("PlayerGems", gems);
            PlayerPrefs.SetInt("PlayerGoldCoins", goldCoins);
            PlayerPrefs.SetInt("PlayerEnergy", energy);
            PlayerPrefs.Save();
            
            if (showDebugInfo)
            {
                Debug.Log("[PlayerProgressManager] 局外进度已保存");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 局外货币操作
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 增加宝石
        /// </summary>
        public void AddGems(int amount)
        {
            gems += amount;
            SaveMetaProgress();
            
            if (showDebugInfo)
            {
                Debug.Log($"[PlayerProgressManager] +{amount} 宝石, 总计: {gems}");
            }
        }
        
        /// <summary>
        /// 消耗宝石
        /// </summary>
        public bool ConsumeGems(int amount)
        {
            if (gems < amount) return false;
            
            gems -= amount;
            SaveMetaProgress();
            return true;
        }
        
        /// <summary>
        /// 增加金币（永久）
        /// </summary>
        public void AddGoldCoins(int amount)
        {
            goldCoins += amount;
            SaveMetaProgress();
            
            if (showDebugInfo)
            {
                Debug.Log($"[PlayerProgressManager] +{amount} 金币（永久）, 总计: {goldCoins}");
            }
        }
        
        /// <summary>
        /// 消耗金币
        /// </summary>
        public bool ConsumeGoldCoins(int amount)
        {
            if (goldCoins < amount) return false;
            
            goldCoins -= amount;
            SaveMetaProgress();
            return true;
        }
        
        /// <summary>
        /// 消耗能量（开始游戏时调用）
        /// </summary>
        public bool ConsumeEnergy(int amount = 1)
        {
            if (energy < amount) return false;
            
            energy -= amount;
            SaveMetaProgress();
            
            if (showDebugInfo)
            {
                Debug.Log($"[PlayerProgressManager] 消耗 {amount} 能量, 剩余: {energy}/{maxEnergy}");
            }
            
            return true;
        }
        
        /// <summary>
        /// 恢复能量
        /// </summary>
        public void RecoverEnergy(int amount = 1)
        {
            energy = Mathf.Min(energy + amount, maxEnergy);
            SaveMetaProgress();
        }
        
        /// <summary>
        /// 满能量恢复
        /// </summary>
        public void RefillEnergy()
        {
            energy = maxEnergy;
            SaveMetaProgress();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 局内进度重置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 重置局内进度（新游戏开始时调用）
        /// </summary>
        public void ResetSessionProgress()
        {
            currentLevel = 1;
            currentExp = 0;
            expToNextLevel = CalculateExpToNextLevel(currentLevel);
            
            sessionCoins = 0;
            
            ultEnergy = 0;
            ultReady = false;
            
            totalKills = 0;
            currentCombo = 0;
            maxCombo = 0;
            lastKillTime = 0f;
            
            // 广播初始状态
            BroadcastAllStatus();
            
            if (showDebugInfo)
            {
                Debug.Log("[PlayerProgressManager] 局内进度已重置");
            }
        }
        
        /// <summary>
        /// 重置所有进度（包括局外）- 用于调试
        /// </summary>
        public void ResetAllProgress()
        {
            // 重置局外
            gems = 0;
            goldCoins = 0;
            energy = maxEnergy;
            SaveMetaProgress();
            
            // 重置局内
            ResetSessionProgress();
            
            if (showDebugInfo)
            {
                Debug.Log("[PlayerProgressManager] 所有进度已重置");
            }
        }
        
        /// <summary>
        /// 广播所有状态（UI初始化用）
        /// </summary>
        private void BroadcastAllStatus()
        {
            GameEvents.TriggerExpChanged(currentExp, expToNextLevel);
            GameEvents.TriggerCoinChanged(sessionCoins);
            GameEvents.TriggerUltEnergyChanged(ultEnergy, ultMaxEnergy);
            GameEvents.TriggerKillCountChanged(totalKills);
            GameEvents.TriggerComboChanged(currentCombo);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 经验系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 计算升到下一级所需经验
        /// 公式: XP = Base + (Level * Growth)
        /// </summary>
        private int CalculateExpToNextLevel(int level)
        {
            return expBase + (level * expGrowth);
        }
        
        /// <summary>
        /// 增加经验值
        /// </summary>
        public void AddExp(int amount)
        {
            if (IsMaxLevel) return;
            
            currentExp += amount;
            
            // 检查升级
            while (currentExp >= expToNextLevel && !IsMaxLevel)
            {
                LevelUp();
            }
            
            // 广播经验变化
            GameEvents.TriggerExpChanged(currentExp, expToNextLevel);
            
            if (showDebugInfo)
            {
                Debug.Log($"[PlayerProgressManager] +{amount} XP, 当前: {currentExp}/{expToNextLevel}");
            }
        }
        
        /// <summary>
        /// 升级
        /// </summary>
        private void LevelUp()
        {
            currentExp -= expToNextLevel;
            currentLevel++;
            expToNextLevel = CalculateExpToNextLevel(currentLevel);
            
            // 广播升级
            GameEvents.TriggerLevelUp(currentLevel);
            
            if (showDebugInfo)
            {
                Debug.Log($"[PlayerProgressManager] 升级! Lv.{currentLevel}");
            }
            
            // TODO: 暂停游戏，显示3选1界面
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 局内金币系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 增加局内金币
        /// </summary>
        public void AddCoins(int amount)
        {
            sessionCoins += amount;
            GameEvents.TriggerCoinChanged(sessionCoins);
            
            if (showDebugInfo)
            {
                Debug.Log($"[PlayerProgressManager] +{amount} 局内金币, 总计: {sessionCoins}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 大招系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 增加大招能量
        /// </summary>
        public void AddUltEnergy(int amount)
        {
            if (ultReady) return; // 已满不再充能
            
            ultEnergy = Mathf.Min(ultEnergy + amount, ultMaxEnergy);
            GameEvents.TriggerUltEnergyChanged(ultEnergy, ultMaxEnergy);
            
            // 检查是否充满
            if (!ultReady && ultEnergy >= ultMaxEnergy)
            {
                ultReady = true;
                GameEvents.TriggerUltReady();
                
                if (showDebugInfo)
                {
                    Debug.Log("[PlayerProgressManager] 大招已准备就绪！");
                }
            }
        }
        
        /// <summary>
        /// 使用大招
        /// </summary>
        public bool UseUlt()
        {
            if (!ultReady) return false;
            
            ultEnergy = 0;
            ultReady = false;
            
            GameEvents.TriggerUltUsed();
            GameEvents.TriggerUltEnergyChanged(ultEnergy, ultMaxEnergy);
            
            if (showDebugInfo)
            {
                Debug.Log("[PlayerProgressManager] 大招已使用！");
            }
            
            return true;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 击杀/连击系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 增加击杀数
        /// </summary>
        private void AddKill()
        {
            totalKills++;
            lastKillTime = Time.time;
            
            // 增加连击
            currentCombo++;
            if (currentCombo > maxCombo)
            {
                maxCombo = currentCombo;
            }
            
            GameEvents.TriggerKillCountChanged(totalKills);
            GameEvents.TriggerComboChanged(currentCombo);
        }
        
        /// <summary>
        /// 检查连击超时
        /// </summary>
        private void CheckComboTimeout()
        {
            if (currentCombo > 0 && Time.time - lastKillTime > comboTimeout)
            {
                ResetCombo();
            }
        }
        
        /// <summary>
        /// 重置连击
        /// </summary>
        private void ResetCombo()
        {
            currentCombo = 0;
            GameEvents.TriggerComboReset();
            GameEvents.TriggerComboChanged(currentCombo);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 敌人死亡处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 敌人死亡回调
        /// </summary>
        private void OnEnemyDied(EnemyType type, Vector3 pos, int xp, int coin)
        {
            // 增加击杀
            AddKill();
            
            // 增加经验
            if (xp > 0)
            {
                AddExp(xp);
            }
            
            // 增加局内金币
            if (coin > 0)
            {
                AddCoins(coin);
            }
            
            // 增加大招能量
            int ultGain = (type == EnemyType.Tank) ? ultEnergyPerEliteKill : ultEnergyPerKill;
            AddUltEnergy(ultGain);
            
            if (showDebugInfo)
            {
                Debug.Log($"[PlayerProgressManager] 敌人死亡: {type}, XP:{xp}, Coin:{coin}, UltEnergy:{ultGain}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 结算数据（给结算界面用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取结算数据
        /// </summary>
        public SettlementData GetSettlementData()
        {
            return new SettlementData
            {
                totalCoins = this.sessionCoins,
                coinsEarned = this.sessionCoins,
                survivalTime = GameManager.Instance != null ? GameManager.Instance.GameTimer : 0f,
                totalKills = this.totalKills,
                killCount = this.totalKills,
                maxCombo = this.maxCombo,
                maxHitCount = this.maxCombo,
                finalLevel = this.currentLevel
            };
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 300, 220, 300));
            GUILayout.Label($"=== Progress (Meta) ===");
            GUILayout.Label($"Gems: {gems}");
            GUILayout.Label($"GoldCoins: {goldCoins}");
            GUILayout.Label($"Energy: {energy}/{maxEnergy}");
            
            GUILayout.Space(5);
            GUILayout.Label($"=== Progress (Session) ===");
            GUILayout.Label($"Lv.{currentLevel} ({currentExp}/{expToNextLevel})");
            GUILayout.Label($"Session Coins: {sessionCoins}");
            GUILayout.Label($"Ult: {ultEnergy}/{ultMaxEnergy} {(ultReady ? "[READY]" : "")}");
            GUILayout.Label($"Kills: {totalKills}");
            GUILayout.Label($"Combo: {currentCombo} (Max:{maxCombo})");
            
            GUILayout.Space(5);
            if (GUILayout.Button("+100 XP")) AddExp(100);
            if (GUILayout.Button("+50 Session Coins")) AddCoins(50);
            if (GUILayout.Button("+50 Ult Energy")) AddUltEnergy(50);
            if (GUILayout.Button("+100 GoldCoins (Meta)")) AddGoldCoins(100);
            
            GUILayout.EndArea();
        }
#endif
    }
}