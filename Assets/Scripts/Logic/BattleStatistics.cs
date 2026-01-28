// ============================================================
// BattleStatistics.cs (V3.0 - 完整数据采样升级版)
// 文件位置: Assets/Scripts/Logic/BattleStatistics.cs
// 更新内容:
//   - 修复W12(Boss战)数据缺失问题
//   - 新增敌人击杀统计(按类型)
//   - 新增技能选择路径记录(选择顺序 + 最终等级)
//   - 新增Frost效果统计(合并主副激光)
//   - 新增暴击详细统计
//   - 新增玩家状态详细(护盾/本体分离)
//   - 新增面板数据快照
//   - 新增Boss战专用字段
//   - CSV字段优化重排(60字段)
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Enemy;
using LightVsDecay.Logic.Player;

namespace LightVsDecay.Logic
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 伤害来源枚举
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 玩家对敌人的伤害来源
    /// </summary>
    public enum DamageSource
    {
        MainLaser,      // 主激光（直射段）
        SubLaser,       // 副激光（分裂/反射段）
        Explosion,      // 爆炸（Focus Lv5）
        Other
    }
    
    /// <summary>
    /// 玩家受伤来源
    /// </summary>
    public enum PlayerDamageSource
    {
        MobCollision,       // 小怪碰撞
        BossCollision,      // Boss 撞击（Charge/Press）
        BossBullet,         // Boss 子弹（污秽球）
        BossFriction,       // Boss 摩擦伤害
        Other
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // V3.0 数据结构
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 单波次统计数据 V3.0
    /// </summary>
    [Serializable]
    public class WaveStatData
    {
        // ═══ 基础信息 (5) ═══
        public int wave;
        public string buildType;
        public int playerLevel;
        public string result;           // Win / Loss
        public float timeToClear;
        
        // ═══ 击杀统计 (6) ═══
        public int killSlime;
        public int killRusher;
        public int killTank;
        public int killDrifter;
        public int killElite;
        public int killTotal;
        
        // ═══ 玩家状态 (8) ═══
        public int hpStartHull;
        public int hpStartShield;
        public int hpEndHull;
        public int hpEndShield;
        public float dmgFromMobs;
        public float dmgFromBoss;
        public float playerHPLost;
        public float tankAbsorbedRatio;
        
        // ═══ 伤害输出 (8) ═══
        public float dmgMainLaser;
        public float dmgSubLaser;
        public float dmgExplosion;
        public float critDamageTotal;
        public int critHitCount;
        public int normalHitCount;
        public float overkillRatio;
        public float dmgDealtTotal;
        
        // ═══ Frost统计 (3) ═══
        public int frostSlowCount;
        public int frostFreezeCount;
        public float frostSlowDuration;
        
        // ═══ 面板快照 (3) ═══
        public float panelDPS;
        public float panelCritRate;
        public float panelLaserWidth;
        
        // ═══ 无人机数据 (12) ═══
        public string droneChoice;
        public string droneRewardType;
        public string droneRewardValue;
        public float droneAccHealth;
        public float droneAccShield;
        public float droneAccDamagePct;
        public float droneAccCritPct;
        public float droneAccLaserWidth;
        public float droneAccLaserLength;
        public int droneCountSupply;
        public int droneCountGacha;
        public int droneCountDeal;
        
        // ═══ Boss战数据 (9) ═══
        public float bossDmgCollision;
        public float bossDmgBullet;
        public float bossPushbackTime;
        public string bossPhase;
        public float bossHPRemaining;
        public int bossChargeCount;
        public int bossPressCount;
        public int bossSummonCount;
        public int bossStunCount;
        
        // ═══ 技能路径 (2) ═══
        public string skillPath;        // 选择顺序: "Prism→Focus→Power→Prism"
        public string skillLevels;      // 最终等级: "Prism:2|Focus:1|Power:1"
        
        // ═══ 其他 (2) ═══
        public float dpsPeak;
        public float enemyTotalHP;
    }
    
    /// <summary>
    /// 敌人击杀计数器
    /// </summary>
    [Serializable]
    public class KillCounter
    {
        public int slime = 0;
        public int rusher = 0;
        public int tank = 0;
        public int drifter = 0;
        public int elite = 0;
        
        public int Total => slime + rusher + tank + drifter + elite;
        
        public void Reset()
        {
            slime = rusher = tank = drifter = elite = 0;
        }
        
        public void Add(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Slime: slime++; break;
                case EnemyType.Rusher: rusher++; break;
                case EnemyType.Tank: tank++; break;
                case EnemyType.Drifter: drifter++; break;
                case EnemyType.EliteTank:
                case EnemyType.EliteDrifter:
                    elite++; break;
            }
        }
    }
    
    /// <summary>
    /// Frost效果统计器
    /// </summary>
    [Serializable]
    public class FrostStats
    {
        public int slowCount = 0;
        public int freezeCount = 0;
        public float slowDuration = 0f;
        
        public void Reset()
        {
            slowCount = freezeCount = 0;
            slowDuration = 0f;
        }
    }
    
    /// <summary>
    /// Boss战统计器
    /// </summary>
    [Serializable]
    public class BossStats
    {
        public int chargeCount = 0;
        public int pressCount = 0;
        public int summonCount = 0;
        public int stunCount = 0;
        public float hpRemaining = 1f;
        public string lastPhase = "None";
        
        public void Reset()
        {
            chargeCount = pressCount = summonCount = stunCount = 0;
            hpRemaining = 1f;
            lastPhase = "None";
        }
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 战斗数据采集管理器 V3.0
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    public class BattleStatistics : Singleton<BattleStatistics>
    {
        [Header("组件引用")]
        [SerializeField] private TurretHealth turretHealth;
        [SerializeField] private ShieldController shieldController;
        [SerializeField] private LaserController laserController;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool logToConsole = true;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // DPS 追踪
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private float _currentSecondDamage = 0f;
        private float _dpsTimer = 0f;
        private float _peakDPS = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 波次统计 - 基础
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private int _currentWave = 0;
        private float _waveStartTime = 0f;
        private float _waveTotalEnemyHP = 0f;
        private float _waveTotalDamage = 0f;
        private float _waveOverkillDamage = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【V3.0】击杀统计
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private KillCounter _waveKills = new KillCounter();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【V3.0】玩家状态详细
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private int _waveStartHullHP = 0;
        private int _waveStartShieldHP = 0;
        private float _waveDmgFromMobs = 0f;
        private float _waveDmgFromBoss = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害来源
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private float _waveMainLaserDamage = 0f;
        private float _waveSubLaserDamage = 0f;
        private float _waveExplosionDamage = 0f;
        private float _waveTankDamage = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【V3.0】暴击统计
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private int _waveCritHitCount = 0;
        private int _waveNormalHitCount = 0;
        private float _waveCritDamageTotal = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【V3.0】Frost统计
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private FrostStats _waveFrost = new FrostStats();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【V3.0】面板快照
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private float _wavePanelDPS = 0f;
        private float _wavePanelCritRate = 0f;
        private float _wavePanelLaserWidth = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Boss战统计
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private float _waveBossCollisionDamage = 0f;
        private float _waveBossBulletDamage = 0f;
        private float _waveBossPushbackTime = 0f;
        private bool _isBossBeingPushed = false;
        private BossStats _bossStats = new BossStats();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【V3.0】技能路径记录
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private List<string> _skillSelectionPath = new List<string>();
        private Dictionary<SkillType, int> _skillLevels = new Dictionary<SkillType, int>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 无人机统计
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private string _waveDroneChoice = "None";
        private string _waveDroneRewardType = "None";
        private string _waveDroneRewardValue = "None";
        private float _sessionDroneAccHealth = 0f;
        private float _sessionDroneAccShield = 0f;
        private float _sessionDroneAccDamagePct = 0f;
        private float _sessionDroneAccCritPct = 0f;
        private float _sessionDroneAccLaserWidth = 0f;
        private float _sessionDroneAccLaserLength = 0f;
        private int _sessionDroneCountSupply = 0;
        private int _sessionDroneCountGacha = 0;
        private int _sessionDroneCountDeal = 0;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 整局统计
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private List<WaveStatData> _allWaveStats = new List<WaveStatData>();
        private bool _isTracking = false;
        private string _sessionStartTime;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        public float PeakDPS => _peakDPS;
        public float WaveTotalDamage => _waveTotalDamage;
        public bool IsTracking => _isTracking;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnEnable()
        {
            GameEvents.OnGameStart += OnGameStart;
            GameEvents.OnWaveStateChanged += OnWaveStateChanged;
            GameEvents.OnWaveComplete += OnWaveComplete;
            GameEvents.OnGameVictory += OnGameVictory;
            GameEvents.OnGameDefeat += OnGameDefeat;
            GameEvents.OnSkillApplied += OnSkillApplied;
            GameEvents.OnEnemyDied += OnEnemyDied;
        }
        
        private void OnDisable()
        {
            GameEvents.OnGameStart -= OnGameStart;
            GameEvents.OnWaveStateChanged -= OnWaveStateChanged;
            GameEvents.OnWaveComplete -= OnWaveComplete;
            GameEvents.OnGameVictory -= OnGameVictory;
            GameEvents.OnGameDefeat -= OnGameDefeat;
            GameEvents.OnSkillApplied -= OnSkillApplied;
            GameEvents.OnEnemyDied -= OnEnemyDied;
        }
        
        private void Update()
        {
            if (!_isTracking) return;
            
            _dpsTimer += Time.deltaTime;
            if (_dpsTimer >= 1.0f)
            {
                if (_currentSecondDamage > _peakDPS)
                {
                    _peakDPS = _currentSecondDamage;
                }
                _currentSecondDamage = 0f;
                _dpsTimer -= 1.0f;
            }
            
            if (_isBossBeingPushed)
            {
                _waveBossPushbackTime += Time.deltaTime;
            }
        }
        
        private void FixedUpdate()
        {
            _isBossBeingPushed = false;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnGameStart()
        {
            ResetSession();
            _isTracking = true;
            _sessionStartTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            if (showDebugInfo) Debug.Log("[BattleStatistics] 🎮 开始采集数据 V3.0");
        }
        
        private void OnWaveStateChanged(WaveState state, int waveNumber)
        {
            if (state == WaveState.Spawning || state == WaveState.BossFight)
            {
                StartNewWave(waveNumber);
            }
        }
        
        private void OnWaveComplete(int waveNumber, int totalWaves)
        {
            RecordWaveData(waveNumber, "Win");
        }
        
        private void OnGameVictory()
        {
            _isTracking = false;
            ExportToCSV("Victory");
        }
        
        private void OnGameDefeat()
        {
            RecordWaveData(_currentWave, "Loss");
            _isTracking = false;
            ExportToCSV("Defeat");
        }
        
        private void OnSkillApplied(SkillType type, int newLevel)
        {
            _skillSelectionPath.Add(type.ToString());
            _skillLevels[type] = newLevel;
            
            if (showDebugInfo)
            {
                Debug.Log($"[BattleStatistics] 技能选择: {type} → Lv.{newLevel}");
            }
        }
        
        private void OnEnemyDied(EnemyType type, Vector3 pos, int xp, int coin)
        {
            if (!_isTracking) return;
            _waveKills.Add(type);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 波次管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void StartNewWave(int waveNumber)
        {
            _currentWave = waveNumber;
            _waveStartTime = Time.time;
            
            // 重置基础统计
            _peakDPS = 0f;
            _currentSecondDamage = 0f;
            _dpsTimer = 0f;
            _waveTotalEnemyHP = 0f;
            _waveTotalDamage = 0f;
            _waveOverkillDamage = 0f;
            
            // 重置击杀统计
            _waveKills.Reset();
            
            // 重置玩家受伤来源
            _waveDmgFromMobs = 0f;
            _waveDmgFromBoss = 0f;
            
            // 重置伤害来源
            _waveMainLaserDamage = 0f;
            _waveSubLaserDamage = 0f;
            _waveExplosionDamage = 0f;
            _waveTankDamage = 0f;
            
            // 重置暴击统计
            _waveCritHitCount = 0;
            _waveNormalHitCount = 0;
            _waveCritDamageTotal = 0f;
            
            // 重置Frost统计
            _waveFrost.Reset();
            
            // 重置Boss统计
            _waveBossCollisionDamage = 0f;
            _waveBossBulletDamage = 0f;
            _waveBossPushbackTime = 0f;
            _isBossBeingPushed = false;
            
            // 如果是Boss波，重置Boss统计器
            if (waveNumber == 12)
            {
                _bossStats.Reset();
            }
            
            // 重置无人机选择
            _waveDroneChoice = "None";
            _waveDroneRewardType = "None";
            _waveDroneRewardValue = "None";
            
            // 记录波次开始时的HP
            _waveStartHullHP = turretHealth != null ? turretHealth.CurrentHullHP : 0;
            _waveStartShieldHP = shieldController != null ? shieldController.CurrentShieldHP : 0;
            
            // 记录面板快照
            SnapshotPanelData();
            
            if (showDebugInfo)
            {
                Debug.Log($"[BattleStatistics] 📊 开始记录 Wave {waveNumber}");
            }
        }
        
        /// <summary>
        /// 【V3.0】记录面板数据快照
        /// </summary>
        private void SnapshotPanelData()
        {
            // 使用 LaserController 已有的公共属性
            if (laserController != null)
            {
                _wavePanelDPS = laserController.CurrentPanelDPS;
                _wavePanelCritRate = laserController.CurrentCritRate;
                _wavePanelLaserWidth = laserController.CurrentLaserWidth;
            }
            else
            {
                // 默认值
                _wavePanelDPS = 100f;
                _wavePanelCritRate = 0.1f;
                _wavePanelLaserWidth = 0.5f;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 数据记录
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void RecordWaveData(int waveNumber, string result)
        {
            float duration = Time.time - _waveStartTime;
            
            // 计算当前HP
            int currentHullHP = turretHealth != null ? turretHealth.CurrentHullHP : 0;
            int currentShieldHP = shieldController != null ? shieldController.CurrentShieldHP : 0;
            float totalHPLost = Mathf.Max(0, (_waveStartHullHP - currentHullHP) + (_waveStartShieldHP - currentShieldHP));
            
            // 计算比率
            float tankRatio = _waveTotalDamage > 0 ? _waveTankDamage / _waveTotalDamage : 0f;
            float overkillRatio = _waveTotalDamage > 0 ? _waveOverkillDamage / _waveTotalDamage : 0f;
            
            string buildType = DetermineBuildType();
            int playerLevel = ProgressManager.Instance != null ? ProgressManager.Instance.CurrentLevel : 1;
            
            // 构建技能路径字符串
            string skillPath = string.Join("→", _skillSelectionPath);
            string skillLevels = BuildSkillLevelsString();
            
            WaveStatData data = new WaveStatData
            {
                // 基础信息
                wave = waveNumber,
                buildType = buildType,
                playerLevel = playerLevel,
                result = result,
                timeToClear = duration,
                
                // 击杀统计
                killSlime = _waveKills.slime,
                killRusher = _waveKills.rusher,
                killTank = _waveKills.tank,
                killDrifter = _waveKills.drifter,
                killElite = _waveKills.elite,
                killTotal = _waveKills.Total,
                
                // 玩家状态
                hpStartHull = _waveStartHullHP,
                hpStartShield = _waveStartShieldHP,
                hpEndHull = currentHullHP,
                hpEndShield = currentShieldHP,
                dmgFromMobs = _waveDmgFromMobs,
                dmgFromBoss = _waveBossCollisionDamage + _waveBossBulletDamage,
                playerHPLost = totalHPLost,
                tankAbsorbedRatio = tankRatio,
                
                // 伤害输出
                dmgMainLaser = _waveMainLaserDamage,
                dmgSubLaser = _waveSubLaserDamage,
                dmgExplosion = _waveExplosionDamage,
                critDamageTotal = _waveCritDamageTotal,
                critHitCount = _waveCritHitCount,
                normalHitCount = _waveNormalHitCount,
                overkillRatio = overkillRatio,
                dmgDealtTotal = _waveTotalDamage,
                
                // Frost统计
                frostSlowCount = _waveFrost.slowCount,
                frostFreezeCount = _waveFrost.freezeCount,
                frostSlowDuration = _waveFrost.slowDuration,
                
                // 面板快照
                panelDPS = _wavePanelDPS,
                panelCritRate = _wavePanelCritRate,
                panelLaserWidth = _wavePanelLaserWidth,
                
                // 无人机数据
                droneChoice = _waveDroneChoice,
                droneRewardType = _waveDroneRewardType,
                droneRewardValue = _waveDroneRewardValue,
                droneAccHealth = _sessionDroneAccHealth,
                droneAccShield = _sessionDroneAccShield,
                droneAccDamagePct = _sessionDroneAccDamagePct,
                droneAccCritPct = _sessionDroneAccCritPct,
                droneAccLaserWidth = _sessionDroneAccLaserWidth,
                droneAccLaserLength = _sessionDroneAccLaserLength,
                droneCountSupply = _sessionDroneCountSupply,
                droneCountGacha = _sessionDroneCountGacha,
                droneCountDeal = _sessionDroneCountDeal,
                
                // Boss战数据
                bossDmgCollision = _waveBossCollisionDamage,
                bossDmgBullet = _waveBossBulletDamage,
                bossPushbackTime = _waveBossPushbackTime,
                bossPhase = _bossStats.lastPhase,
                bossHPRemaining = _bossStats.hpRemaining,
                bossChargeCount = _bossStats.chargeCount,
                bossPressCount = _bossStats.pressCount,
                bossSummonCount = _bossStats.summonCount,
                bossStunCount = _bossStats.stunCount,
                
                // 技能路径
                skillPath = skillPath,
                skillLevels = skillLevels,
                
                // 其他
                dpsPeak = _peakDPS,
                enemyTotalHP = _waveTotalEnemyHP
            };
            
            _allWaveStats.Add(data);
            
            if (logToConsole)
            {
                PrintWaveStats(data);
            }
        }
        
        private string BuildSkillLevelsString()
        {
            List<string> parts = new List<string>();
            foreach (var kvp in _skillLevels)
            {
                if (kvp.Value > 0)
                {
                    parts.Add($"{kvp.Key}:{kvp.Value}");
                }
            }
            return string.Join("|", parts);
        }
        
        private string DetermineBuildType()
        {
            bool hasFocus = _skillLevels.ContainsKey(SkillType.Focus) && _skillLevels[SkillType.Focus] >= 1;
            bool hasImpact = _skillLevels.ContainsKey(SkillType.Impact) && _skillLevels[SkillType.Impact] >= 1;
            bool hasCrit = _skillLevels.ContainsKey(SkillType.Crit) && _skillLevels[SkillType.Crit] >= 1;
            bool hasPrism = _skillLevels.ContainsKey(SkillType.Prism) && _skillLevels[SkillType.Prism] >= 1;
            bool hasWide = _skillLevels.ContainsKey(SkillType.Wide) && _skillLevels[SkillType.Wide] >= 1;
            bool hasFrost = _skillLevels.ContainsKey(SkillType.Frost) && _skillLevels[SkillType.Frost] >= 1;
            bool hasShatter = _skillLevels.ContainsKey(SkillType.Shatter) && _skillLevels[SkillType.Shatter] >= 1;
            
            if (hasFocus && hasImpact && hasCrit) return "A";
            if (hasPrism && hasWide) return "B";
            if (hasFrost && hasImpact && hasShatter) return "C";
            return "Mix";
        }
        
        private void PrintWaveStats(WaveStatData data)
        {
            Debug.Log($"[BattleStatistics] ═══ Wave {data.wave} 统计 ═══");
            Debug.Log($"  流派:{data.buildType} | Lv.{data.playerLevel} | 结果:{data.result} | 耗时:{data.timeToClear:F1}s");
            Debug.Log($"  击杀: S{data.killSlime} R{data.killRusher} T{data.killTank} D{data.killDrifter} E{data.killElite} = {data.killTotal}");
            Debug.Log($"  HP: {data.hpStartHull}+{data.hpStartShield} → {data.hpEndHull}+{data.hpEndShield} (损失:{data.playerHPLost})");
            Debug.Log($"  伤害: 主{data.dmgMainLaser:F0} 副{data.dmgSubLaser:F0} 爆{data.dmgExplosion:F0} = {data.dmgDealtTotal:F0}");
            Debug.Log($"  暴击: {data.critHitCount}次 共{data.critDamageTotal:F0}伤害");
            Debug.Log($"  技能路径: {data.skillPath}");
        }
        
        private void ResetSession()
        {
            _allWaveStats.Clear();
            _skillLevels.Clear();
            _skillSelectionPath.Clear();
            _currentWave = 0;
            _peakDPS = 0f;
            _currentSecondDamage = 0f;
            _dpsTimer = 0f;
            
            _sessionDroneAccHealth = 0f;
            _sessionDroneAccShield = 0f;
            _sessionDroneAccDamagePct = 0f;
            _sessionDroneAccCritPct = 0f;
            _sessionDroneAccLaserWidth = 0f;
            _sessionDroneAccLaserLength = 0f;
            _sessionDroneCountSupply = 0;
            _sessionDroneCountGacha = 0;
            _sessionDroneCountDeal = 0;
            
            _bossStats.Reset();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // CSV 导出 V3.0 (60字段)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ExportToCSV(string gameResult)
        {
            StringBuilder csv = new StringBuilder();
            
            // CSV Header V3.0
            csv.AppendLine(
                // 基础信息 (5)
                "Wave,Build_Type,Player_Level,Result,Time_To_Clear," +
                // 击杀统计 (6)
                "Kill_Slime,Kill_Rusher,Kill_Tank,Kill_Drifter,Kill_Elite,Kill_Total," +
                // 玩家状态 (8)
                "HP_Start_Hull,HP_Start_Shield,HP_End_Hull,HP_End_Shield," +
                "Dmg_From_Mobs,Dmg_From_Boss,Player_HP_Lost,Tank_Absorbed_Ratio," +
                // 伤害输出 (8)
                "Dmg_Main_Laser,Dmg_Sub_Laser,Dmg_Explosion," +
                "Crit_Damage_Total,Crit_Hit_Count,Normal_Hit_Count,Overkill_Ratio,Dmg_Dealt_Total," +
                // Frost统计 (3)
                "Frost_Slow_Count,Frost_Freeze_Count,Frost_Slow_Duration," +
                // 面板快照 (3)
                "Panel_DPS,Panel_Crit_Rate,Panel_Laser_Width," +
                // 无人机数据 (12)
                "Drone_Choice,Drone_Reward_Type,Drone_Reward_Value," +
                "Drone_Acc_Health,Drone_Acc_Shield,Drone_Acc_Damage_Pct,Drone_Acc_Crit_Pct," +
                "Drone_Acc_Laser_Width,Drone_Acc_Laser_Length," +
                "Drone_Count_Supply,Drone_Count_Gacha,Drone_Count_Deal," +
                // Boss战数据 (9)
                "Boss_Dmg_Collision,Boss_Dmg_Bullet,Boss_Pushback_Time," +
                "Boss_Phase,Boss_HP_Remaining,Boss_Charge_Count,Boss_Press_Count,Boss_Summon_Count,Boss_Stun_Count," +
                // 技能路径 (2)
                "Skill_Path,Skill_Levels," +
                // 其他 (2)
                "DPS_Peak,Enemy_Total_HP"
            );
            
            foreach (var d in _allWaveStats)
            {
                // 转义技能路径中的逗号
                string escapedPath = $"\"{d.skillPath}\"";
                string escapedLevels = $"\"{d.skillLevels}\"";
                
                string line = string.Format(
                    "{0},{1},{2},{3},{4:F1}," +
                    "{5},{6},{7},{8},{9},{10}," +
                    "{11},{12},{13},{14},{15:F0},{16:F0},{17:F0},{18:F2}," +
                    "{19:F0},{20:F0},{21:F0},{22:F0},{23},{24},{25:F2},{26:F0}," +
                    "{27},{28},{29:F1}," +
                    "{30:F0},{31:F2},{32:F2}," +
                    "{33},{34},{35},{36:F0},{37:F0},{38:F1},{39:F1},{40:F1},{41:F1},{42},{43},{44}," +
                    "{45:F0},{46:F0},{47:F1},{48},{49:F2},{50},{51},{52},{53}," +
                    "{54},{55}," +
                    "{56:F0},{57:F0}",
                    // 基础信息
                    d.wave, d.buildType, d.playerLevel, d.result, d.timeToClear,
                    // 击杀统计
                    d.killSlime, d.killRusher, d.killTank, d.killDrifter, d.killElite, d.killTotal,
                    // 玩家状态
                    d.hpStartHull, d.hpStartShield, d.hpEndHull, d.hpEndShield,
                    d.dmgFromMobs, d.dmgFromBoss, d.playerHPLost, d.tankAbsorbedRatio,
                    // 伤害输出
                    d.dmgMainLaser, d.dmgSubLaser, d.dmgExplosion,
                    d.critDamageTotal, d.critHitCount, d.normalHitCount, d.overkillRatio, d.dmgDealtTotal,
                    // Frost统计
                    d.frostSlowCount, d.frostFreezeCount, d.frostSlowDuration,
                    // 面板快照
                    d.panelDPS, d.panelCritRate, d.panelLaserWidth,
                    // 无人机数据
                    d.droneChoice, d.droneRewardType, d.droneRewardValue,
                    d.droneAccHealth, d.droneAccShield, d.droneAccDamagePct, d.droneAccCritPct,
                    d.droneAccLaserWidth, d.droneAccLaserLength,
                    d.droneCountSupply, d.droneCountGacha, d.droneCountDeal,
                    // Boss战数据
                    d.bossDmgCollision, d.bossDmgBullet, d.bossPushbackTime,
                    d.bossPhase, d.bossHPRemaining, d.bossChargeCount, d.bossPressCount, d.bossSummonCount, d.bossStunCount,
                    // 技能路径
                    escapedPath, escapedLevels,
                    // 其他
                    d.dpsPeak, d.enemyTotalHP
                );
                csv.AppendLine(line);
            }
            
            // 保存文件
            string fileName = $"BattleLog_{_sessionStartTime}_{gameResult}.csv";
            string filePath = Path.Combine(Application.persistentDataPath, fileName);
            
            try
            {
                File.WriteAllText(filePath, csv.ToString());
                Debug.Log($"[BattleStatistics] ✅ CSV V3.0 已保存: {filePath}");
                Debug.Log($"[BattleStatistics] 📊 共记录 {_allWaveStats.Count} 波数据，{CountCSVFields()} 个字段");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BattleStatistics] ❌ CSV 保存失败: {e.Message}");
            }
            
            if (logToConsole)
            {
                Debug.Log("=== BATTLE LOG CSV V3.0 ===\n" + csv.ToString());
            }
        }
        
        private int CountCSVFields()
        {
            return 5 + 6 + 8 + 8 + 3 + 3 + 12 + 9 + 2 + 2; // = 58
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部数据上报接口 - 伤害
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 上报伤害数据（V3.0 - 增加暴击伤害统计）
        /// </summary>
        public void RecordDamage(float effectiveDamage, float overkillDamage, EnemyType enemyType, 
            DamageSource source, bool isCrit)
        {
            if (!_isTracking) return;
            
            float totalDamage = effectiveDamage + overkillDamage;
            
            _waveTotalDamage += totalDamage;
            _waveOverkillDamage += overkillDamage;
            _currentSecondDamage += totalDamage;
            
            if (enemyType == EnemyType.Tank || enemyType == EnemyType.EliteTank)
            {
                _waveTankDamage += totalDamage;
            }
            
            switch (source)
            {
                case DamageSource.MainLaser:
                    _waveMainLaserDamage += totalDamage;
                    break;
                case DamageSource.SubLaser:
                    _waveSubLaserDamage += totalDamage;
                    break;
                case DamageSource.Explosion:
                    _waveExplosionDamage += totalDamage;
                    break;
            }
            
            // 暴击统计
            if (isCrit)
            {
                _waveCritHitCount++;
                _waveCritDamageTotal += totalDamage;
            }
            else
            {
                _waveNormalHitCount++;
            }
        }
        
        /// <summary>
        /// 上报敌人总血量
        /// </summary>
        public void RecordEnemyHP(float hp)
        {
            if (!_isTracking) return;
            _waveTotalEnemyHP += hp;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部数据上报接口 - 玩家受伤
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 上报玩家受伤数据（V3.0 - 区分小怪/Boss）
        /// </summary>
        public void RecordPlayerDamage(float damage, PlayerDamageSource source)
        {
            if (!_isTracking) return;
            
            switch (source)
            {
                case PlayerDamageSource.MobCollision:
                    _waveDmgFromMobs += damage;
                    break;
                case PlayerDamageSource.BossCollision:
                    _waveBossCollisionDamage += damage;
                    _waveDmgFromBoss += damage;
                    break;
                case PlayerDamageSource.BossBullet:
                case PlayerDamageSource.BossFriction:
                    _waveBossBulletDamage += damage;
                    _waveDmgFromBoss += damage;
                    break;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部数据上报接口 - Frost效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 【V3.0 新增】上报Frost减速触发
        /// </summary>
        public void RecordFrostSlow()
        {
            if (!_isTracking) return;
            _waveFrost.slowCount++;
        }
        
        /// <summary>
        /// 【V3.0 新增】上报Frost冰冻触发
        /// </summary>
        public void RecordFrostFreeze()
        {
            if (!_isTracking) return;
            _waveFrost.freezeCount++;
        }
        
        /// <summary>
        /// 【V3.0 新增】上报Frost减速持续时间
        /// </summary>
        public void RecordFrostSlowDuration(float duration)
        {
            if (!_isTracking) return;
            _waveFrost.slowDuration += duration;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部数据上报接口 - Boss战
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 标记 Boss 当前正在被有效推动
        /// </summary>
        public void MarkBossBeingPushed()
        {
            if (!_isTracking) return;
            _isBossBeingPushed = true;
        }
        
        /// <summary>
        /// 【V3.0 新增】上报Boss技能释放
        /// </summary>
        public void RecordBossSkill(string skillName)
        {
            if (!_isTracking) return;
            
            switch (skillName.ToLower())
            {
                case "charge":
                    _bossStats.chargeCount++;
                    break;
                case "press":
                    _bossStats.pressCount++;
                    break;
                case "summon":
                    _bossStats.summonCount++;
                    break;
                case "stun":
                    _bossStats.stunCount++;
                    break;
            }
        }
        
        /// <summary>
        /// 【V3.0 新增】上报Boss状态变化
        /// </summary>
        public void RecordBossPhase(string phase)
        {
            if (!_isTracking) return;
            _bossStats.lastPhase = phase;
        }
        
        /// <summary>
        /// 【V3.0 新增】上报Boss血量变化
        /// </summary>
        public void RecordBossHP(float hpPercent)
        {
            if (!_isTracking) return;
            _bossStats.hpRemaining = hpPercent;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部数据上报接口 - 无人机选择
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 记录无人机选择（由 TacticalDropManager 调用）
        /// </summary>
        public void RecordDroneChoice(string crateType, string rewardType, string rewardValue)
        {
            if (!_isTracking) return;
            
            _waveDroneChoice = crateType;
            _waveDroneRewardType = rewardType;
            _waveDroneRewardValue = rewardValue;
            
            // 回溯更新最后一条波次记录
            if (_allWaveStats.Count > 0)
            {
                var lastWave = _allWaveStats[_allWaveStats.Count - 1];
                lastWave.droneChoice = crateType;
                lastWave.droneRewardType = rewardType;
                lastWave.droneRewardValue = rewardValue;
                
                // 更新箱子选择计数
                UpdateCrateCount(crateType);
                
                // 解析奖励值并累加
                AccumulateDroneReward(rewardType, rewardValue);
                
                // 更新累计字段
                lastWave.droneAccHealth = _sessionDroneAccHealth;
                lastWave.droneAccShield = _sessionDroneAccShield;
                lastWave.droneAccDamagePct = _sessionDroneAccDamagePct;
                lastWave.droneAccCritPct = _sessionDroneAccCritPct;
                lastWave.droneAccLaserWidth = _sessionDroneAccLaserWidth;
                lastWave.droneAccLaserLength = _sessionDroneAccLaserLength;
                lastWave.droneCountSupply = _sessionDroneCountSupply;
                lastWave.droneCountGacha = _sessionDroneCountGacha;
                lastWave.droneCountDeal = _sessionDroneCountDeal;
                
                if (showDebugInfo)
                {
                    Debug.Log($"[BattleStatistics] ✅ 无人机选择: {crateType} | {rewardType} ({rewardValue})");
                }
            }
        }
        
        private void UpdateCrateCount(string crateType)
        {
            if (crateType.StartsWith("Supply"))
                _sessionDroneCountSupply++;
            else if (crateType.StartsWith("Gacha"))
                _sessionDroneCountGacha++;
            else if (crateType.StartsWith("Deal"))
                _sessionDroneCountDeal++;
        }
        
        private void AccumulateDroneReward(string rewardType, string rewardValue)
        {
            // 解析奖励值
            // 支持格式: HP-100|ATK+10%, SHLD-50|CRIT+2%, HP+500, etc.
            
            if (string.IsNullOrEmpty(rewardValue) || rewardValue == "None")
                return;
            
            string[] parts = rewardValue.Split('|');
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                
                // 解析HP变化
                if (trimmed.StartsWith("HP"))
                {
                    Match match = Regex.Match(trimmed, @"HP([+-]?\d+)");
                    if (match.Success)
                    {
                        _sessionDroneAccHealth += float.Parse(match.Groups[1].Value);
                    }
                }
                // 解析护盾变化
                else if (trimmed.StartsWith("SHLD"))
                {
                    Match match = Regex.Match(trimmed, @"SHLD([+-]?\d+)");
                    if (match.Success)
                    {
                        _sessionDroneAccShield += float.Parse(match.Groups[1].Value);
                    }
                }
                // 解析攻击力变化
                else if (trimmed.StartsWith("ATK"))
                {
                    Match match = Regex.Match(trimmed, @"ATK([+-]?\d+)%?");
                    if (match.Success)
                    {
                        _sessionDroneAccDamagePct += float.Parse(match.Groups[1].Value);
                    }
                }
                // 解析暴击率变化
                else if (trimmed.StartsWith("CRIT"))
                {
                    Match match = Regex.Match(trimmed, @"CRIT([+-]?\d+)%?");
                    if (match.Success)
                    {
                        _sessionDroneAccCritPct += float.Parse(match.Groups[1].Value);
                    }
                }
            }
        }
        /// <summary>
        /// 上报爆炸伤害（用于记录最大爆炸伤害）
        /// 由 SkillEffectManager 的 Focus/Shatter 爆炸调用
        /// </summary>
        public void RecordExplosionDamage(float explosionDamage)
        {
            if (!_isTracking) return;
    
            // 可选：记录最大爆炸伤害（如果需要这个数据）
            // 目前 V3.0 不再单独记录 maxExplosionDmg，但保留此方法以兼容调用
        }
    }
}