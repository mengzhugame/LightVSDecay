using UnityEngine;

namespace LightVsDecay.Core
{
    /// <summary>
    /// 玩家进度管理器 (单例)
    /// 负责：经验值、等级、金币、大招能量、击杀数、连击数
    /// </summary>
    public class PlayerProgressManager : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public static PlayerProgressManager Instance { get; private set; }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 经验系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("经验系统")]
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
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        // 经验/等级
        private int currentLevel = 1;
        private int currentExp = 0;
        private int expToNextLevel;
        
        // 金币
        private int totalCoins = 0;
        
        // 大招能量
        private int ultEnergy = 0;
        private bool ultReady = false;
        
        // 击杀/连击
        private int totalKills = 0;
        private int currentCombo = 0;
        private int maxCombo = 0;
        private float lastKillTime = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        // 经验/等级
        public int CurrentLevel => currentLevel;
        public int CurrentExp => currentExp;
        public int ExpToNextLevel => expToNextLevel;
        public float ExpProgress => expToNextLevel > 0 ? (float)currentExp / expToNextLevel : 0f;
        public bool IsMaxLevel => currentLevel >= maxLevel;
        
        // 金币
        public int TotalCoins => totalCoins;
        
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
        
        private void Awake()
        {
            // 单例设置
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void Start()
        {
            // 订阅敌人死亡事件
            GameEvents.OnEnemyDied += OnEnemyDied;
            
            // 订阅游戏开始事件
            GameEvents.OnGameStart += ResetProgress;
            
            // 初始化
            ResetProgress();
        }
        
        private void Update()
        {
            // 检查连击超时
            CheckComboTimeout();
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            
            // 取消订阅
            GameEvents.OnEnemyDied -= OnEnemyDied;
            GameEvents.OnGameStart -= ResetProgress;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 重置所有进度（新游戏开始时调用）
        /// </summary>
        public void ResetProgress()
        {
            currentLevel = 1;
            currentExp = 0;
            expToNextLevel = CalculateExpToNextLevel(currentLevel);
            
            totalCoins = 0;
            
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
                Debug.Log("[PlayerProgressManager] 进度已重置");
            }
        }
        
        /// <summary>
        /// 广播所有状态（UI初始化用）
        /// </summary>
        private void BroadcastAllStatus()
        {
            GameEvents.TriggerExpChanged(currentExp, expToNextLevel);
            GameEvents.TriggerCoinChanged(totalCoins);
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
            // 目前先跳过，只触发事件
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 金币系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 增加金币
        /// </summary>
        public void AddCoins(int amount)
        {
            totalCoins += amount;
            GameEvents.TriggerCoinChanged(totalCoins);
            
            if (showDebugInfo)
            {
                Debug.Log($"[PlayerProgressManager] +{amount} 金币, 总计: {totalCoins}");
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
        private void OnEnemyDied(Pool.EnemyType type, Vector3 pos, int xp, int coin)
        {
            // 增加击杀
            AddKill();
            
            // 增加经验
            if (xp > 0)
            {
                AddExp(xp);
            }
            
            // 增加金币
            if (coin > 0)
            {
                AddCoins(coin);
            }
            
            // 增加大招能量
            int ultGain = (type == Pool.EnemyType.Tank) ? ultEnergyPerEliteKill : ultEnergyPerKill;
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
                totalCoins = this.totalCoins,
                survivalTime = GameManager.Instance != null ? GameManager.Instance.GameTimer : 0f,
                totalKills = this.totalKills,
                maxCombo = this.maxCombo,
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
            
            GUILayout.BeginArea(new Rect(10, 300, 200, 220));
            GUILayout.Label($"=== Progress ===");
            GUILayout.Label($"Lv.{currentLevel} ({currentExp}/{expToNextLevel})");
            GUILayout.Label($"Coins: {totalCoins}");
            GUILayout.Label($"Ult: {ultEnergy}/{ultMaxEnergy} {(ultReady ? "[READY]" : "")}");
            GUILayout.Label($"Kills: {totalKills}");
            GUILayout.Label($"Combo: {currentCombo} (Max:{maxCombo})");
            
            GUILayout.Space(5);
            if (GUILayout.Button("+100 XP")) AddExp(100);
            if (GUILayout.Button("+50 Coins")) AddCoins(50);
            if (GUILayout.Button("+50 Ult Energy")) AddUltEnergy(50);
            
            GUILayout.EndArea();
        }
#endif
    }
}