// ============================================================
// BattleStatistics.cs
// 文件位置: Assets/Scripts/Logic/Statistics/BattleStatistics.cs
// 用途：战斗数据采集系统 - 主控制器
// 版本：V4.0 重构版
// 
// 架构说明：
//   - BattleStatTypes.cs    : 枚举和数据结构
//   - BattleStatRecorder.cs : 外部数据上报接口 (partial)
//   - BattleStatExporter.cs : CSV导出逻辑 (partial)
//   - BattleStatistics.cs   : 主类（本文件）
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Player;
using System.Text.RegularExpressions;
using LightVsDecay.Core.Pool;
using System.IO;
using System.Text;

namespace LightVsDecay.Logic.Statistics
{
    /// <summary>
    /// 战斗数据采集管理器 V4.0
    /// </summary>
    public partial class BattleStatistics : Singleton<BattleStatistics>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("数据采集开关")]
        [Tooltip("是否启用数据采集（关闭可提升性能）")]
        [SerializeField] private bool enableDataCollection = false;
        
        [Header("组件引用")]
        [SerializeField] private TurretHealth turretHealth;
        [SerializeField] private ShieldController shieldController;
        [SerializeField] private LaserController laserController;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool logToConsole = true;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>数据采集是否启用</summary>
        public bool IsEnabled => enableDataCollection;
        
        /// <summary>当前是否正在追踪</summary>
        public bool IsTracking => _isTracking;
        
        /// <summary>峰值DPS</summary>
        public float PeakDPS => _peakDPS;
        
        /// <summary>当前波次总伤害</summary>
        public float WaveTotalDamage => _waveTotalDamage;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - DPS追踪
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float _currentSecondDamage = 0f;
        private float _dpsTimer = 0f;
        private float _peakDPS = 0f;
        private float _waveChainDamage = 0f;    // 连锁伤害累计
        private int _waveOverloadCount = 0;     // 大招释放次数
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 波次基础
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int _currentWave = 0;
        private float _waveStartTime = 0f;
        private float _waveTotalDamage = 0f;
        private float _waveOverkillDamage = 0f;
        private float _waveTotalEnemyHP = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 击杀与伤害
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private KillCounter _waveKills = new KillCounter();
        private SpawnCounter _waveSpawns = new SpawnCounter();
        private float _waveMainLaserDamage = 0f;
        private float _waveSubLaserDamage = 0f;
        private float _waveExplosionDamage = 0f;
        private float _waveTankDamage = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 暴击
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int _waveCritHitCount = 0;
        private int _waveNormalHitCount = 0;
        private float _waveCritDamageTotal = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 玩家受伤
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float _waveDmgFromMobs = 0f;
        private float _waveDmgShieldFromMobs = 0f;
        private float _waveDmgFromBoss = 0f;
        private float _waveBossDmgDealt = 0f;   // V4.7
        private float _waveDiffMult = 1f;        // V4.7
        private int _waveStartHullHP = 0;
        private int _waveStartShieldHP = 0;
        
        private int _wavePlayerHitCount = 0;     // 本波受击次数
        private int _waveExpGained = 0;           // 本波获得经验
        private int _waveGoldGained = 0;          // 本波获得金币
        private int _lastKnownTotalCoins = 0;     // 金币快照（差值法）
        private float _waveDangerTime = 0f;       // 濒死累计时长
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - Frost
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private FrostStats _waveFrost = new FrostStats();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - Boss
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private BossStats _bossStats = new BossStats();
        private float _waveBossCollisionDamage = 0f;
        private float _waveBossBulletDamage = 0f;
        private float _waveBossPushbackTime = 0f;
        private bool _isBossBeingPushed = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 面板快照
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float _wavePanelDPS = 0f;
        private float _wavePanelCritRate = 0f;
        private float _wavePanelLaserWidth = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 技能
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private List<string> _skillSelectionPath = new List<string>();
        private Dictionary<SkillType, int> _skillLevels = new Dictionary<SkillType, int>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 无人机
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
        // 运行时状态 - 整局
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private List<WaveStatData> _allWaveStats = new List<WaveStatData>();
        private bool _isTracking = false;
        private string _sessionStartTime;
        private string _sessionConfigId = "Unknown";
        
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
            GameEvents.OnXPOrbCollected += OnXPOrbCollectedForStats;
            GameEvents.OnCoinChanged += OnCoinChangedForStats;
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
            GameEvents.OnXPOrbCollected -= OnXPOrbCollectedForStats;
            GameEvents.OnCoinChanged -= OnCoinChangedForStats;
        }
        
        private void Update()
        {
            if (!enableDataCollection || !_isTracking) return;
            
            // DPS 计算
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
            
            // Boss 推动时间累计
            if (_isBossBeingPushed)
            {
                _waveBossPushbackTime += Time.deltaTime;
            }
            // 【V4.2】濒死时长追踪（血量 < 30%）
            if (turretHealth != null && turretHealth.CurrentHullHP > 0)
            {
                if (turretHealth.HullPercent < 0.2f)
                {
                    _waveDangerTime += Time.deltaTime;
                }
            }
        }
        
        private void FixedUpdate()
        {
            // 每帧重置Boss推动标记（由MarkBossBeingPushed重新设置）
            _isBossBeingPushed = false;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnGameStart()
        {
            if (!enableDataCollection) return;
            
            ResetSession();
            _isTracking = true;
            _sessionStartTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _lastKnownTotalCoins = 0; // 新游戏从 0 开始
            _sessionConfigId = WaveManager.Instance != null ? WaveManager.Instance.ConfigName : "Unknown";
            if (showDebugInfo)
            {
                GameLogger.Log("[BattleStatistics] 🎮 开始采集数据 V4.0");
            }
        }
        
        private void OnWaveStateChanged(WaveState state, int waveNumber)
        {
            if (!enableDataCollection) return;
            
            if (state == WaveState.Spawning || state == WaveState.BossFight)
            {
                StartNewWave(waveNumber);
            }
        }
        
        private void OnWaveComplete(int waveNumber, int totalWaves)
        {
            if (!enableDataCollection) return;
            RecordWaveData(waveNumber, "Win");
        }
        
        private void OnGameVictory()
        {
            if (!enableDataCollection) return;
            
            _isTracking = false;
            ExportToCSV("Victory");
        }
        
        private void OnGameDefeat()
        {
            if (!enableDataCollection) return;
            
            RecordWaveData(_currentWave, "Loss");
            _isTracking = false;
            ExportToCSV("Defeat");
        }
        
        private void OnSkillApplied(SkillType type, int newLevel)
        {
            if (!enableDataCollection) return;
            
            _skillSelectionPath.Add(type.ToString());
            _skillLevels[type] = newLevel;
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[BattleStatistics] 技能选择: {type} → Lv.{newLevel}");
            }
        }
        
        private void OnEnemyDied(EnemyType type, Vector3 pos, int xp, int coin)
        {
            if (!CanRecord()) return;
            _waveKills.Add(type);
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 经验 / 金币追踪
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnXPOrbCollectedForStats(int xp)
        {
            if (!CanRecord()) return;
            _waveExpGained += xp;
        }

        private void OnCoinChangedForStats(int totalCoins)
        {
            if (!CanRecord()) return;
            int delta = totalCoins - _lastKnownTotalCoins;
            if (delta > 0)
            {
                _waveGoldGained += delta;
            }
            _lastKnownTotalCoins = totalCoins;
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 波次管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void StartNewWave(int waveNumber)
        {
            _currentWave = waveNumber;
            _waveStartTime = Time.time;
            
            // 重置DPS
            _peakDPS = 0f;
            _currentSecondDamage = 0f;
            _dpsTimer = 0f;
            
            // 重置基础统计
            _waveTotalEnemyHP = 0f;
            _waveTotalDamage = 0f;
            _waveOverkillDamage = 0f;
            
            // 重置击杀/生成统计
            _waveKills.Reset();
            _waveSpawns.Reset();
            
            // 重置玩家受伤
            _waveDmgFromMobs = 0f;
            _waveDmgShieldFromMobs = 0f;
            _waveDmgFromBoss = 0f;
            _waveBossDmgDealt = 0f;
            _waveDiffMult = 1f;
            
            _wavePlayerHitCount = 0;
            _waveExpGained = 0;
            _waveGoldGained = 0;
            _waveDangerTime = 0f;
            
            _waveChainDamage = 0f;
            _waveOverloadCount = 0;
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
            
            // Boss波重置Boss统计器
            if (WaveManager.Instance != null && WaveManager.Instance.IsBossWave)
            {
                _bossStats.Reset();
            }
            
            // 重置无人机选择
            _waveDroneChoice = "None";
            _waveDroneRewardType = "None";
            _waveDroneRewardValue = "None";
            
            // 记录波次开始HP
            _waveStartHullHP = turretHealth != null ? turretHealth.CurrentHullHP : 0;
            _waveStartShieldHP = shieldController != null ? shieldController.CurrentShieldHP : 0;
            
            // 记录面板数据
            SnapshotPanelData();
        }
        
        private void SnapshotPanelData()
        {
            if (laserController != null)
            {
                _wavePanelDPS = laserController.CurrentPanelDPS;
                _wavePanelCritRate = laserController.CurrentCritRate;
                _wavePanelLaserWidth = laserController.CurrentLaserWidth;
            }
        }
        
        private void RecordWaveData(int waveNumber, string result)
        {
            float timeToClear = Time.time - _waveStartTime;
            
            int endHullHP = turretHealth != null ? turretHealth.CurrentHullHP : 0;
            int endShieldHP = shieldController != null ? shieldController.CurrentShieldHP : 0;
            
            float hpLost = (_waveStartHullHP + _waveStartShieldHP) - (endHullHP + endShieldHP);
            float tankRatio = _waveTotalDamage > 0 ? _waveTankDamage / _waveTotalDamage : 0f;
            float overkillRatio = _waveTotalDamage > 0 ? _waveOverkillDamage / _waveTotalDamage : 0f;
            
            string skillPath = string.Join(">", _skillSelectionPath);
            string skillLevels = BuildSkillLevelsString();
            
            var data = new WaveStatData
            {
                // 基础信息
                wave = waveNumber,
                buildType = DetermineBuildType(),
                playerLevel = ProgressManager.Instance != null ? ProgressManager.Instance.CurrentLevel : 1,
                result = result,
                timeToClear = timeToClear,
                
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
                hpEndHull = endHullHP,
                hpEndShield = endShieldHP,
                dmgFromMobs = _waveDmgFromMobs,
                shieldDmgFromMobs = _waveDmgShieldFromMobs,
                dmgFromBoss = _waveDmgFromBoss,
                playerHPLost = hpLost,
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
                dmgChain = _waveChainDamage,
                overloadCount = _waveOverloadCount,
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
                enemyTotalHP = _waveTotalEnemyHP,
                // V4.2 新增
                effectiveDPS = timeToClear > 0f ? _waveTotalDamage / timeToClear : 0f,
                playerHitCount = _wavePlayerHitCount,
                expGained = _waveExpGained,
                goldGained = _waveGoldGained,
                timeInDanger = _waveDangerTime,

                // V4.5 生成统计 + 配置标识
                spawnSlime = _waveSpawns.slime,
                spawnRusher = _waveSpawns.rusher,
                spawnTank = _waveSpawns.tank,
                spawnDrifter = _waveSpawns.drifter,
                spawnElite = _waveSpawns.elite,
                spawnTotal = _waveSpawns.Total,
                waveConfigId = _sessionConfigId,

                // V4.7
                bossDmgDealt = _waveBossDmgDealt,
                waveDiffMult = _waveDiffMult,
            };
            
            _allWaveStats.Add(data);
            
            if (logToConsole)
            {
                PrintWaveStats(data);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 辅助方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
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
        
        private void ResetSession()
        {
            _allWaveStats.Clear();
            _skillLevels.Clear();
            _skillSelectionPath.Clear();
            _currentWave = 0;
            _peakDPS = 0f;
            _currentSecondDamage = 0f;
            _dpsTimer = 0f;
            
            // 重置无人机累计
            _sessionDroneAccHealth = 0f;
            _sessionDroneAccShield = 0f;
            _sessionDroneAccDamagePct = 0f;
            _sessionDroneAccCritPct = 0f;
            _sessionDroneAccLaserWidth = 0f;
            _sessionDroneAccLaserLength = 0f;
            _sessionDroneCountSupply = 0;
            _sessionDroneCountGacha = 0;
            _sessionDroneCountDeal = 0;
            
            // 重置Boss统计
            _bossStats.Reset();
        }
               // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害上报
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 上报伤害数据
        /// </summary>
        /// <param name="effectiveDamage">有效伤害</param>
        /// <param name="overkillDamage">溢出伤害</param>
        /// <param name="enemyType">敌人类型</param>
        /// <param name="source">伤害来源</param>
        /// <param name="isCrit">是否暴击</param>
        public void RecordDamage(float effectiveDamage, float overkillDamage, EnemyType enemyType, 
            DamageSource source, bool isCrit)
        {
            if (!CanRecord()) return;
            
            float totalDamage = effectiveDamage + overkillDamage;
            
            _waveTotalDamage += totalDamage;
            _waveOverkillDamage += overkillDamage;
            _currentSecondDamage += totalDamage;
            
            // Tank 吸收统计
            if (enemyType == EnemyType.Tank || enemyType == EnemyType.EliteTank)
            {
                _waveTankDamage += totalDamage;
            }
            
            // 伤害来源统计
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
                case DamageSource.Chain: // 【新增】
                    _waveChainDamage += totalDamage;
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
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】大招上报接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        public void RecordOverloadActivation()
        {
            if (!CanRecord()) return;
            _waveOverloadCount++;
            if (showDebugInfo) GameLogger.Log("[BattleStatistics] ⚡ 记录大招释放！");
        }
        /// <summary>
        /// 获取整局战斗的总伤害（所有波次 dmgDealtTotal 之和）
        /// 供 ProgressManager.GetSettlementData() 调用
        /// </summary>
        public float GetTotalDamageDealt()
        {
            float total = 0f;
            foreach (var stat in _allWaveStats)
                total += stat.dmgDealtTotal;

            // 加上当前波次尚未结算的实时伤害
            total += _waveTotalDamage;
            return total;
        }
        /// <summary>
        /// 上报敌人生成（含类型）— V4.5 推荐接口
        /// </summary>
        public void RecordEnemySpawn(EnemyType type, float hp)
        {
            if (!CanRecord()) return;
            _waveTotalEnemyHP += hp;
            _waveSpawns.Add(type);
        }

        /// <summary>
        /// 上报敌人总血量（生成时调用）— 兼容接口，推荐改用 RecordEnemySpawn
        /// </summary>
        public void RecordEnemyHP(float hp)
        {
            if (!CanRecord()) return;
            _waveTotalEnemyHP += hp;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 玩家受伤上报
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 上报玩家受伤数据（区分小怪/Boss）
        /// </summary>
        public void RecordPlayerDamage(float damage, PlayerDamageSource source)
        {
            if (!CanRecord()) return;
            _wavePlayerHitCount++;
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
        
        /// <summary>
        /// 上报护盾从小怪吸收的伤害量（V4.6）
        /// </summary>
        public void RecordShieldDamageFromMobs(float shieldAbsorbed)
        {
            if (!CanRecord()) return;
            _waveDmgShieldFromMobs += shieldAbsorbed;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Frost效果上报
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 上报Frost减速触发
        /// </summary>
        public void RecordFrostSlow()
        {
            if (!CanRecord()) return;
            _waveFrost.slowCount++;
        }
        /// <summary>
        /// 上报爆炸伤害（Shatter Lv5 漏洞扩散）
        /// </summary>
        /// <param name="damage">爆炸造成的伤害</param>
        public void RecordExplosionDamage(float damage)
        {
            if (!CanRecord()) return;
    
            _waveExplosionDamage += damage;
            _waveTotalDamage += damage;
            _currentSecondDamage += damage;
        }
        /// <summary>
        /// 上报Frost冰冻触发
        /// </summary>
        public void RecordFrostFreeze()
        {
            if (!CanRecord()) return;
            _waveFrost.freezeCount++;
        }
        
        /// <summary>
        /// 上报Frost减速持续时间
        /// </summary>
        public void RecordFrostSlowDuration(float duration)
        {
            if (!CanRecord()) return;
            _waveFrost.slowDuration += duration;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Boss战上报
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 标记 Boss 当前正在被有效推动
        /// </summary>
        public void MarkBossBeingPushed()
        {
            if (!CanRecord()) return;
            _isBossBeingPushed = true;
        }
        
        /// <summary>
        /// 上报Boss技能释放
        /// </summary>
        /// <param name="skillName">技能名：charge/press/summon/stun</param>
        public void RecordBossSkill(string skillName)
        {
            if (!CanRecord()) return;
            
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
        /// 上报Boss状态变化
        /// </summary>
        public void RecordBossPhase(string phase)
        {
            if (!CanRecord()) return;
            _bossStats.lastPhase = phase;
        }
        
        /// <summary>
        /// 上报Boss血量变化
        /// </summary>
        public void RecordBossHP(float hpPercent)
        {
            if (!CanRecord()) return;
            _bossStats.hpRemaining = hpPercent;
        }

        /// <summary>
        /// 上报玩家对Boss造成的伤害（V4.7）
        /// </summary>
        public void RecordBossDamageTaken(float damage)
        {
            if (!CanRecord()) return;
            _waveBossDmgDealt += damage;
        }

        /// <summary>
        /// 设置当前波次的难度系数快照（V4.7，由WaveManager在波次开始时调用）
        /// </summary>
        public void SetWaveDifficultyMult(float mult)
        {
            if (!CanRecord()) return;
            _waveDiffMult = mult;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 无人机选择上报
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 记录无人机选择（由 TacticalDropManager 调用）
        /// </summary>
        public void RecordDroneChoice(string crateType, string rewardType, string rewardValue)
        {
            if (!CanRecord()) return;
            
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
                    GameLogger.Log($"[BattleStatistics] ✅ 无人机选择: {crateType} | {rewardType} ({rewardValue})");
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 无人机奖励解析（内部方法）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
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
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内部检查方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 检查是否可以记录数据（开关 + 追踪状态）
        /// </summary>
        private bool CanRecord()
        {
            return enableDataCollection && _isTracking;
        }
               // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // CSV 导出 V4.0 (58字段)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ExportToCSV(string gameResult)
        {
            if (!enableDataCollection) return;
            
            StringBuilder csv = new StringBuilder();
            
            // CSV Header
            csv.AppendLine(BuildCSVHeader());
            
            // 数据行
            foreach (var d in _allWaveStats)
            {
                csv.AppendLine(BuildCSVDataLine(d));
            }
            
            // 保存文件
            string fileName = $"BattleLog_{_sessionStartTime}_{gameResult}.csv";
            string filePath = Path.Combine(Application.persistentDataPath, fileName);
            
            try
            {
                File.WriteAllText(filePath, csv.ToString());
                GameLogger.Log($"[BattleStatistics] ✅ CSV V4.0 已保存: {filePath}");
                GameLogger.Log($"[BattleStatistics] 📊 共记录 {_allWaveStats.Count} 波数据，{CSV_FIELD_COUNT} 个字段");
            }
            catch (Exception e)
            {
                GameLogger.LogError($"[BattleStatistics] ❌ CSV 保存失败: {e.Message}");
            }
            
            if (logToConsole)
            {
                GameLogger.Log("=== BATTLE LOG CSV V4.0 ===\n" + csv.ToString());
            }
        }
        
        // CSV字段总数
        private const int CSV_FIELD_COUNT = 75;
        
        /// <summary>
        /// 构建CSV表头
        /// </summary>
        private string BuildCSVHeader()
        {
            return 
                // 基础信息 (5)
                "Wave,Build_Type,Player_Level,Result,Time_To_Clear," +
                // 击杀统计 (6)
                "Kill_Slime,Kill_Rusher,Kill_Tank,Kill_Drifter,Kill_Elite,Kill_Total," +
                // 玩家状态 (8)
                "HP_Start_Hull,HP_Start_Shield,HP_End_Hull,HP_End_Shield," +
                "Dmg_From_Mobs,Shield_Dmg_From_Mobs,Dmg_From_Boss,Player_HP_Lost,Tank_Absorbed_Ratio," +
                // 伤害输出 (8)
                "Dmg_Main_Laser,Dmg_Sub_Laser,Dmg_Explosion,Dmg_Chain," +
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
                "Skill_Path,Skill_Levels,Overload_Count," +
                // 其他 (2)
                "DPS_Peak,Enemy_Total_HP,"+
                "Effective_DPS,Player_Hit_Count,Exp_Gained,Gold_Gained,Time_In_Danger," +
                // V4.5 生成统计 + 配置标识 (7)
                "Spawn_Slime,Spawn_Rusher,Spawn_Tank,Spawn_Drifter,Spawn_Elite,Spawn_Total,Wave_Config_Id," +
                // V4.7 Boss伤害输出 + 波次难度系数 (2)
                "Boss_Dmg_Dealt,Wave_Diff_Mult";
        }
        
        /// <summary>
        /// 构建单行CSV数据 (V4.1)
        /// 新增: Dmg_Chain (索引22), Overload_Count (索引57)
        /// 总字段数: 60
        /// </summary>
        private string BuildCSVDataLine(WaveStatData d)
        {
            // 转义技能路径中的逗号，防止破坏CSV格式
            string escapedPath = $"\"{d.skillPath}\"";
            string escapedLevels = $"\"{d.skillLevels}\"";
            
            return string.Format(
                // 1. 基础信息 (0-4)
                "{0},{1},{2},{3},{4:F1}," +
                // 2. 击杀统计 (5-10)
                "{5},{6},{7},{8},{9},{10}," +
                // 3. 玩家状态 (11-19)
                "{11},{12},{13},{14},{15:F0},{16:F0},{17:F0},{18:F0},{19:F2}," +
                // 4. 伤害输出 (20-28)
                "{20:F0},{21:F0},{22:F0},{23:F0},{24:F0},{25},{26},{27:F2},{28:F0}," +
                // 5. Frost统计 (29-31)
                "{29},{30},{31:F1}," +
                // 6. 面板快照 (32-34)
                "{32:F0},{33:F2},{34:F2}," +
                // 7. 无人机数据 (35-46)
                "{35},{36},{37},{38:F0},{39:F0},{40:F1},{41:F1},{42:F1},{43:F1},{44},{45},{46}," +
                // 8. Boss战数据 (47-55)
                "{47:F0},{48:F0},{49:F1},{50},{51:F2},{52},{53},{54},{55}," +
                // 9. 技能与大招 (56-58)
                "{56},{57},{58}," +
                // 10. 其他 (59-60)
                "{59:F0},{60:F0}," +
                // 11. V4.2 新增 (61-65)
                "{61:F1},{62},{63},{64},{65:F2}," +
                // 12. V4.5 生成统计 (66-72)
                "{66},{67},{68},{69},{70},{71},{72}," +
                // 13. V4.7 Boss伤害 + 波次难度 (73-74)
                "{73:F0},{74:F2}",

                // ================= 参数列表 =================
                
                // 1. 基础信息
                d.wave, d.buildType, d.playerLevel, d.result, d.timeToClear,
                
                // 2. 击杀统计
                d.killSlime, d.killRusher, d.killTank, d.killDrifter, d.killElite, d.killTotal,
                
                // 3. 玩家状态
                d.hpStartHull, d.hpStartShield, d.hpEndHull, d.hpEndShield,
                d.dmgFromMobs, d.shieldDmgFromMobs, d.dmgFromBoss, d.playerHPLost, d.tankAbsorbedRatio,
                
                // 4. 伤害输出
                d.dmgMainLaser,     // 20
                d.dmgSubLaser,      // 21
                d.dmgExplosion,     // 22
                d.dmgChain,         // 23
                d.critDamageTotal,  // 24
                d.critHitCount,     // 25
                d.normalHitCount,   // 26
                d.overkillRatio,    // 27
                d.dmgDealtTotal,    // 28
                
                // 5. Frost统计
                d.frostSlowCount, d.frostFreezeCount, d.frostSlowDuration,
                
                // 6. 面板快照
                d.panelDPS, d.panelCritRate, d.panelLaserWidth,
                
                // 7. 无人机数据
                d.droneChoice, d.droneRewardType, d.droneRewardValue,
                d.droneAccHealth, d.droneAccShield, d.droneAccDamagePct, d.droneAccCritPct,
                d.droneAccLaserWidth, d.droneAccLaserLength,
                d.droneCountSupply, d.droneCountGacha, d.droneCountDeal,
                
                // 8. Boss战数据
                d.bossDmgCollision, d.bossDmgBullet, d.bossPushbackTime,
                d.bossPhase, d.bossHPRemaining, d.bossChargeCount, d.bossPressCount, d.bossSummonCount, d.bossStunCount,
                
                // 9. 技能与大招
                escapedPath,         // 56
                escapedLevels,       // 57
                d.overloadCount,     // 58

                // 10. 其他
                d.dpsPeak,           // 59
                d.enemyTotalHP,      // 60

                // 11. V4.2 新增
                d.effectiveDPS,      // 61
                d.playerHitCount,    // 62
                d.expGained,         // 63
                d.goldGained,        // 64
                d.timeInDanger,      // 65

                // 12. V4.5 生成统计
                d.spawnSlime,        // 66
                d.spawnRusher,       // 67
                d.spawnTank,         // 68
                d.spawnDrifter,      // 69
                d.spawnElite,        // 70
                d.spawnTotal,        // 71
                d.waveConfigId,      // 72

                // 13. V4.7
                d.bossDmgDealt,      // 73
                d.waveDiffMult       // 74
            );
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo || !enableDataCollection) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 200, 10, 190, 120));
            GUILayout.Label("=== BattleStatistics V4.0 ===");
            GUILayout.Label($"Enabled: {enableDataCollection}");
            GUILayout.Label($"Tracking: {_isTracking}");
            GUILayout.Label($"Wave: {_currentWave}");
            GUILayout.Label($"Peak DPS: {_peakDPS:F0}");
            GUILayout.Label($"Wave Damage: {_waveTotalDamage:F0}");
            GUILayout.EndArea();
        }
        private void PrintWaveStats(WaveStatData data)
        {
            GameLogger.Log($"[BattleStatistics] ═══ Wave {data.wave} 统计 ═══");
            GameLogger.Log($"  流派:{data.buildType} | Lv.{data.playerLevel} | 结果:{data.result} | 耗时:{data.timeToClear:F1}s");
            GameLogger.Log($"  击杀: S{data.killSlime} R{data.killRusher} T{data.killTank} D{data.killDrifter} E{data.killElite} = {data.killTotal}");
            GameLogger.Log($"  HP: {data.hpStartHull}+{data.hpStartShield} → {data.hpEndHull}+{data.hpEndShield} (损失:{data.playerHPLost})");
            GameLogger.Log($"  伤害: 主{data.dmgMainLaser:F0} 副{data.dmgSubLaser:F0} 爆{data.dmgExplosion:F0} = {data.dmgDealtTotal:F0}");
            GameLogger.Log($"  暴击: {data.critHitCount}次 共{data.critDamageTotal:F0}伤害");
            GameLogger.Log($"  技能路径: {data.skillPath}");
            GameLogger.Log($"  V4.2: DPS_eff:{data.effectiveDPS:F1} | 受击:{data.playerHitCount}次 | XP:{data.expGained} Gold:{data.goldGained} | 濒死:{data.timeInDanger:F1}s");
        }
#endif
    }
}