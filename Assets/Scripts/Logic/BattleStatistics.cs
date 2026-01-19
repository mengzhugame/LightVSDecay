// ============================================================
// BattleStatistics.cs
// 文件位置: Assets/Scripts/Logic/BattleStatistics.cs
// 用途：战斗数据采集管理器 v2.1
// 更新：修复无人机数据记录时序问题 + 新增累计统计字段
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
    /// 玩家受伤来源（用于 Boss 战统计）
    /// </summary>
    public enum PlayerDamageSource
    {
        MobCollision,       // 小怪碰撞（包含 Boss 召唤的小怪）
        BossCollision,      // Boss 撞击（Charge/Press 角力失败）
        BossBullet,         // Boss 子弹（污秽球）
        BossFriction,       // Boss 摩擦伤害
        Other
    }
    
    /// <summary>
    /// 流派类型枚举
    /// </summary>
    public enum BuildType
    {
        A,      // 重炮击退流
        B,      // 广域折射流
        C,      // 异常控制流
        Mix
    }
    
    /// <summary>
    /// 单波次统计数据 v2.1
    /// </summary>
    [Serializable]
    public class WaveStatData
    {
        // ═══ 基础字段 ═══
        public int wave;
        public string buildType;
        public int playerLevel;
        public string result;
        public float timeToClear;
        
        // ═══ 玩家状态 ═══
        public float playerHPLost;
        public float tankAbsorbedRatio;
        public float overkillRatio;
        
        // ═══ 伤害来源细分 ═══
        public float dmgMainLaser;
        public float dmgSubLaser;
        public float dmgExplosion;
        
        // ═══ 无人机空投数据（该波次结束后的选择）═══
        public string droneChoice;          // 选择的箱子 (Supply/Gacha/Gacha_Epic/Gacha_Negative/Deal/None)
        public string droneRewardType;      // 具体奖励类型 (HealthRestore/BaseDamagePercent/etc)
        public string droneRewardValue;     // 奖励数值 (+100/-5%/etc)
        
        // ═══ 【v2.1 新增】无人机累计统计（截至该波次）═══
        public float droneAccHealth;        // 累计血量变化（正=获得，负=损失）
        public float droneAccShield;        // 累计护盾变化
        public float droneAccDamagePct;     // 累计攻击力%加成
        public float droneAccCritPct;       // 累计暴击率%加成
        public float droneAccLaserWidth;    // 累计激光宽度变化
        public float droneAccLaserLength;   // 累计激光长度变化
        public int droneCountSupply;        // 选择补给箱次数
        public int droneCountGacha;         // 选择金箱次数
        public int droneCountDeal;          // 选择契约箱次数
        
        // ═══ 暴击数据 ═══
        public float critRateActual;
        
        // ═══ Boss 战数据 ═══
        public float bossDmgCollision;      // Boss 撞击伤害（Charge/Press）
        public float bossDmgBullet;         // Boss 子弹 + 摩擦 + 召唤小怪伤害
        public float bossPushbackTime;      // Boss 被有效阻滞时长（B流派条件）
        
        // ═══ 旧字段（兼容）═══
        public float dpsPeak;
        public float enemyTotalHP;
        public float dmgDealtTotal;
        public float dmgOverkill;
        public float maxExplosionDmg;
    }
    
    /// <summary>
    /// 战斗数据采集管理器 v2.1
    /// </summary>
    public class BattleStatistics : Singleton<BattleStatistics>
    {
        [Header("组件引用")]
        [SerializeField] private TurretHealth turretHealth;
        [SerializeField] private ShieldController shieldController;
        
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
        private float _waveEffectiveDamage = 0f;
        private float _waveOverkillDamage = 0f;
        private float _waveTankDamage = 0f;
        private float _wavePlayerHPLost = 0f;
        private float _waveMaxExplosionDmg = 0f;
        private int _waveStartHullHP = 0;
        private int _waveStartShieldHP = 0;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 波次统计 - 伤害来源
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private float _waveMainLaserDamage = 0f;
        private float _waveSubLaserDamage = 0f;
        private float _waveExplosionDamage = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 波次统计 - 暴击
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private int _waveTotalHitCount = 0;
        private int _waveCritHitCount = 0;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 波次统计 - 玩家受伤
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private float _waveBossCollisionDamage = 0f;
        private float _waveBossBulletDamage = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 波次统计 - Boss 阻滞
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private float _waveBossPushbackTime = 0f;
        private bool _isBossBeingPushed = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 波次统计 - 无人机选择（临时存储）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private string _waveDroneChoice = "None";
        private string _waveDroneRewardType = "None";
        private string _waveDroneRewardValue = "None";
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【v2.1 新增】整局无人机累计统计
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
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
        private Dictionary<SkillType, int> _skillLevels = new Dictionary<SkillType, int>();
        private bool _isTracking = false;
        private string _sessionStartTime;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        public float PeakDPS => _peakDPS;
        public float WaveTotalDamage => _waveTotalDamage;
        public bool IsTracking => _isTracking;
        public float WaveCritRate => _waveTotalHitCount > 0 ? (float)_waveCritHitCount / _waveTotalHitCount : 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Start()
        {
            if (turretHealth == null) turretHealth = FindObjectOfType<TurretHealth>();
            if (shieldController == null) shieldController = FindObjectOfType<ShieldController>();
        }
        
        private void OnEnable()
        {
            GameEvents.OnGameStart += OnGameStart;
            GameEvents.OnWaveStateChanged += OnWaveStateChanged;
            GameEvents.OnWaveComplete += OnWaveComplete;
            GameEvents.OnGameVictory += OnGameVictory;
            GameEvents.OnGameDefeat += OnGameDefeat;
            GameEvents.OnSkillApplied += OnSkillApplied;
        }
        
        private void OnDisable()
        {
            GameEvents.OnGameStart -= OnGameStart;
            GameEvents.OnWaveStateChanged -= OnWaveStateChanged;
            GameEvents.OnWaveComplete -= OnWaveComplete;
            GameEvents.OnGameVictory -= OnGameVictory;
            GameEvents.OnGameDefeat -= OnGameDefeat;
            GameEvents.OnSkillApplied -= OnSkillApplied;
        }
        
        private void Update()
        {
            if (!_isTracking) return;
            
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
            
            // Boss 阻滞时间累加
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
            if (showDebugInfo) Debug.Log("[BattleStatistics] 🎮 开始采集数据 v2.1");
        }
        
        private void OnWaveStateChanged(WaveState state, int waveNumber)
        {
            if (state == WaveState.Spawning) StartNewWave(waveNumber);
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
            _skillLevels[type] = newLevel;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 波次管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void StartNewWave(int waveNumber)
        {
            _currentWave = waveNumber;
            _waveStartTime = Time.time;
            
            // 重置所有波次统计
            _peakDPS = 0f;
            _currentSecondDamage = 0f;
            _dpsTimer = 0f;
            _waveTotalEnemyHP = 0f;
            _waveTotalDamage = 0f;
            _waveEffectiveDamage = 0f;
            _waveOverkillDamage = 0f;
            _waveTankDamage = 0f;
            _wavePlayerHPLost = 0f;
            _waveMaxExplosionDmg = 0f;
            
            _waveMainLaserDamage = 0f;
            _waveSubLaserDamage = 0f;
            _waveExplosionDamage = 0f;
            
            _waveTotalHitCount = 0;
            _waveCritHitCount = 0;
            
            _waveBossCollisionDamage = 0f;
            _waveBossBulletDamage = 0f;
            
            _waveBossPushbackTime = 0f;
            _isBossBeingPushed = false;
            
            // 【重要】不重置无人机选择，因为它会在 RecordDroneChoice 中回溯更新
            _waveDroneChoice = "None";
            _waveDroneRewardType = "None";
            _waveDroneRewardValue = "None";
            
            _waveStartHullHP = turretHealth != null ? turretHealth.CurrentHullHP : 0;
            _waveStartShieldHP = shieldController != null ? shieldController.CurrentShieldHP : 0;
        }
        
        private void RecordWaveData(int waveNumber, string result)
        {
            float duration = Time.time - _waveStartTime;
            
            int currentHullHP = turretHealth != null ? turretHealth.CurrentHullHP : 0;
            int currentShieldHP = shieldController != null ? shieldController.CurrentShieldHP : 0;
            int totalHPLost = (_waveStartHullHP - currentHullHP) + (_waveStartShieldHP - currentShieldHP);
            _wavePlayerHPLost = Mathf.Max(0, totalHPLost);
            
            float tankRatio = _waveTotalDamage > 0 ? _waveTankDamage / _waveTotalDamage : 0f;
            float overkillRatio = _waveTotalDamage > 0 ? _waveOverkillDamage / _waveTotalDamage : 0f;
            float critRate = _waveTotalHitCount > 0 ? (float)_waveCritHitCount / _waveTotalHitCount : 0f;
            
            string buildType = DetermineBuildType();
            int playerLevel = ProgressManager.Instance != null ? ProgressManager.Instance.CurrentLevel : 1;
            
            WaveStatData data = new WaveStatData
            {
                wave = waveNumber,
                buildType = buildType,
                playerLevel = playerLevel,
                result = result,
                timeToClear = duration,
                
                playerHPLost = _wavePlayerHPLost,
                tankAbsorbedRatio = tankRatio,
                overkillRatio = overkillRatio,
                
                dmgMainLaser = _waveMainLaserDamage,
                dmgSubLaser = _waveSubLaserDamage,
                dmgExplosion = _waveExplosionDamage,
                
                // 无人机数据先填 None，稍后由 RecordDroneChoice 回溯更新
                droneChoice = "None",
                droneRewardType = "None",
                droneRewardValue = "None",
                
                // 【v2.1】填入当前累计值（无人机选择发生后会更新）
                droneAccHealth = _sessionDroneAccHealth,
                droneAccShield = _sessionDroneAccShield,
                droneAccDamagePct = _sessionDroneAccDamagePct,
                droneAccCritPct = _sessionDroneAccCritPct,
                droneAccLaserWidth = _sessionDroneAccLaserWidth,
                droneAccLaserLength = _sessionDroneAccLaserLength,
                droneCountSupply = _sessionDroneCountSupply,
                droneCountGacha = _sessionDroneCountGacha,
                droneCountDeal = _sessionDroneCountDeal,
                
                critRateActual = critRate,
                
                bossDmgCollision = _waveBossCollisionDamage,
                bossDmgBullet = _waveBossBulletDamage,
                bossPushbackTime = _waveBossPushbackTime,
                
                dpsPeak = _peakDPS,
                enemyTotalHP = _waveTotalEnemyHP,
                dmgDealtTotal = _waveTotalDamage,
                dmgOverkill = _waveOverkillDamage,
                maxExplosionDmg = _waveMaxExplosionDmg
            };
            
            _allWaveStats.Add(data);
            if (logToConsole) PrintWaveStats(data);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部数据上报接口 - 伤害
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 上报伤害数据（v2.0 - 区分伤害来源）
        /// </summary>
        public void RecordDamage(float effectiveDamage, float overkillDamage, EnemyType enemyType, 
            DamageSource source, bool isCrit)
        {
            if (!_isTracking) return;
            
            float totalDamage = effectiveDamage + overkillDamage;
            
            _waveTotalDamage += totalDamage;
            _waveEffectiveDamage += effectiveDamage;
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
            
            _waveTotalHitCount++;
            if (isCrit) _waveCritHitCount++;
        }
        
        /// <summary>
        /// 上报敌人总血量
        /// </summary>
        public void RecordEnemyHP(float hp)
        {
            if (!_isTracking) return;
            _waveTotalEnemyHP += hp;
        }
        
        /// <summary>
        /// 上报爆炸伤害
        /// </summary>
        public void RecordExplosionDamage(float explosionDamage)
        {
            if (!_isTracking) return;
            if (explosionDamage > _waveMaxExplosionDmg)
            {
                _waveMaxExplosionDmg = explosionDamage;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部数据上报接口 - 玩家受伤
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 上报玩家受伤数据
        /// </summary>
        public void RecordPlayerDamage(float damage, PlayerDamageSource source)
        {
            if (!_isTracking) return;
            
            switch (source)
            {
                case PlayerDamageSource.BossCollision:
                    _waveBossCollisionDamage += damage;
                    break;
                case PlayerDamageSource.BossBullet:
                case PlayerDamageSource.BossFriction:
                case PlayerDamageSource.MobCollision:
                    // Boss 子弹、摩擦伤害、召唤小怪 都计入 BossBullet
                    _waveBossBulletDamage += damage;
                    break;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部数据上报接口 - Boss 阻滞
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 标记 Boss 当前正在被有效推动
        /// 条件：currentPushForce > 0 且 Boss 实际在被推
        /// </summary>
        public void MarkBossBeingPushed()
        {
            if (!_isTracking) return;
            _isBossBeingPushed = true;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部数据上报接口 - 无人机选择（v2.1 修复版）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 记录无人机选择（由 TacticalDropManager 调用）
        /// 【v2.1】会回溯更新最后一条波次记录，确保数据关联正确
        /// </summary>
        /// <param name="crateType">选择的箱子类型 (Supply/Gacha/Gacha_Epic/Gacha_Negative/Deal)</param>
        /// <param name="rewardType">获得的奖励类型 (HealthRestore/BaseDamagePercent/etc)</param>
        /// <param name="rewardValue">奖励显示值 (+100/-5%/etc)</param>
        public void RecordDroneChoice(string crateType, string rewardType, string rewardValue)
        {
            if (!_isTracking) return;
            
            // 设置临时变量（兼容旧逻辑）
            _waveDroneChoice = crateType;
            _waveDroneRewardType = rewardType;
            _waveDroneRewardValue = rewardValue;
            
            // 【v2.1 核心修复】回溯更新最后一条波次记录
            if (_allWaveStats.Count > 0)
            {
                var lastWave = _allWaveStats[_allWaveStats.Count - 1];
                lastWave.droneChoice = crateType;
                lastWave.droneRewardType = rewardType;
                lastWave.droneRewardValue = rewardValue;
                
                // 更新箱子选择计数
                UpdateCrateCount(crateType);
                
                // 解析奖励值并累加到整局统计
                AccumulateDroneReward(rewardType, rewardValue);
                
                // 更新该波次的累计字段
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
                    Debug.Log($"[BattleStatistics] ✅ 回溯更新波次 {lastWave.wave} 的无人机数据: {crateType} | {rewardType} ({rewardValue})");
                }
            }
            else
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("[BattleStatistics] ⚠️ 无人机选择时没有波次数据可更新！");
                }
            }
        }
        
        /// <summary>
        /// 更新箱子选择计数
        /// </summary>
        private void UpdateCrateCount(string crateType)
        {
            if (crateType.StartsWith("Supply"))
            {
                _sessionDroneCountSupply++;
            }
            else if (crateType.StartsWith("Gacha"))
            {
                _sessionDroneCountGacha++;
            }
            else if (crateType.StartsWith("Deal"))
            {
                _sessionDroneCountDeal++;
            }
        }
        
        /// <summary>
        /// 解析奖励值并累加到整局统计
        /// 支持格式：+100, -50, +5%, -3%, HP +100, ATK +10%, etc.
        /// </summary>
        private void AccumulateDroneReward(string rewardType, string rewardValue)
        {
            // 解析数值
            float value = ParseRewardValue(rewardValue);
            
            // 根据奖励类型累加
            switch (rewardType)
            {
                // ═══ 正面效果 ═══
                case "HealthRestore":
                    _sessionDroneAccHealth += value;
                    break;
                    
                case "ShieldRestore":
                case "ShieldFull":
                    _sessionDroneAccShield += value;
                    break;
                    
                case "BaseDamagePercent":
                    _sessionDroneAccDamagePct += value;
                    break;
                    
                case "CritRatePercent":
                    _sessionDroneAccCritPct += value;
                    break;
                    
                case "LaserWidthFlat":
                    _sessionDroneAccLaserWidth += value;
                    break;
                    
                case "LaserLengthFlat":
                    _sessionDroneAccLaserLength += value;
                    break;
                    
                // ═══ 负面效果（数值已经是负数或需要取负）═══
                case "HealthLoss":
                case "MaxHealthLoss":
                    // 损失用负数表示
                    _sessionDroneAccHealth -= Mathf.Abs(value);
                    break;
                    
                case "ShieldLoss":
                    _sessionDroneAccShield -= Mathf.Abs(value);
                    break;
                    
                case "BaseDamageLossPercent":
                    _sessionDroneAccDamagePct -= Mathf.Abs(value);
                    break;
                    
                case "CritRateLossPercent":
                    _sessionDroneAccCritPct -= Mathf.Abs(value);
                    break;
                    
                case "LaserWidthLossFlat":
                    _sessionDroneAccLaserWidth -= Mathf.Abs(value);
                    break;
                    
                case "LaserLengthLossFlat":
                    _sessionDroneAccLaserLength -= Mathf.Abs(value);
                    break;
                    
                // ═══ 契约箱特殊处理（格式：CostType→GainType）═══
                default:
                    if (rewardType.Contains("→"))
                    {
                        // 契约箱的 rewardValue 格式：-100|+10%
                        ParseDealReward(rewardType, rewardValue);
                    }
                    break;
            }
        }
        
        /// <summary>
        /// 解析契约箱的代价和收益
        /// rewardType 格式：HealthLoss→BaseDamagePercent
        /// rewardValue 格式：HP -100|ATK +10%
        /// </summary>
        private void ParseDealReward(string rewardType, string rewardValue)
        {
            // 解析类型
            string[] types = rewardType.Split('→');
            if (types.Length != 2) return;
            
            string costType = types[0].Trim();
            string gainType = types[1].Trim();
            
            // 解析数值
            string[] values = rewardValue.Split('|');
            if (values.Length != 2) return;
            
            string costValue = values[0].Trim();
            string gainValue = values[1].Trim();
            
            // 累加代价（负面）
            AccumulateDroneReward(costType, costValue);
            
            // 累加收益（正面）
            AccumulateDroneReward(gainType, gainValue);
            
            if (showDebugInfo)
            {
                Debug.Log($"[BattleStatistics] 契约箱解析: 代价={costType}({costValue}), 收益={gainType}({gainValue})");
            }
        }
        
        /// <summary>
        /// 从显示文本中解析数值
        /// 支持格式：+100, -50, +5%, -3%, HP +100, ATK +10%, 护盾恢复 etc.
        /// </summary>
        private float ParseRewardValue(string displayText)
        {
            if (string.IsNullOrEmpty(displayText)) return 0f;
            
            // 使用正则表达式提取数字和符号
            // 匹配模式：可选符号 + 数字 + 可选百分号
            Match match = Regex.Match(displayText, @"([+-]?\d+\.?\d*)(%)?");
            
            if (match.Success)
            {
                float value = 0f;
                if (float.TryParse(match.Groups[1].Value, out value))
                {
                    // 如果是百分比，转换为小数（但保留原值用于显示）
                    // 注意：这里不转换，因为我们要记录的是原始显示值
                    // 例如 +5% 就记录 5，而不是 0.05
                    return value;
                }
            }
            
            return 0f;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 流派判定
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private string DetermineBuildType()
        {
            bool hasFocus = _skillLevels.ContainsKey(SkillType.Focus) && _skillLevels[SkillType.Focus] > 0;
            bool hasImpact = _skillLevels.ContainsKey(SkillType.Impact) && _skillLevels[SkillType.Impact] > 0;
            bool hasCrit = _skillLevels.ContainsKey(SkillType.Crit) && _skillLevels[SkillType.Crit] > 0;
            
            bool hasReflex = _skillLevels.ContainsKey(SkillType.Reflex) && _skillLevels[SkillType.Reflex] > 0;
            bool hasPrism = _skillLevels.ContainsKey(SkillType.Prism) && _skillLevels[SkillType.Prism] > 0;
            bool hasWide = _skillLevels.ContainsKey(SkillType.Wide) && _skillLevels[SkillType.Wide] > 0;
            
            bool hasFrost = _skillLevels.ContainsKey(SkillType.Frost) && _skillLevels[SkillType.Frost] > 0;
            bool hasDataBreach = false; // TODO
            
            if (hasFocus && hasImpact && hasCrit) return "A";
            if (hasReflex && hasPrism && hasWide) return "B";
            if (hasFrost && hasImpact && hasDataBreach) return "C";
            return "Mix";
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // CSV 导出（v2.1 - 新增累计字段）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ExportToCSV(string gameResult)
        {
            StringBuilder csv = new StringBuilder();
            
            // CSV Header - 30 fields (原21 + 新增9)
            csv.AppendLine(
                "Wave,Build_Type,Player_Level,Result,Time_To_Clear," +
                "Player_HP_Lost,Tank_Absorbed_Ratio,Overkill_Ratio," +
                "Dmg_Main_Laser,Dmg_Sub_Laser,Dmg_Explosion," +
                "Drone_Choice,Drone_Reward_Type,Drone_Reward_Value," +
                "Drone_Acc_Health,Drone_Acc_Shield,Drone_Acc_Damage_Pct,Drone_Acc_Crit_Pct," +
                "Drone_Acc_Laser_Width,Drone_Acc_Laser_Length," +
                "Drone_Count_Supply,Drone_Count_Gacha,Drone_Count_Deal," +
                "Crit_Rate_Actual," +
                "Boss_Dmg_Collision,Boss_Dmg_Bullet,Boss_Pushback_Time," +
                "DPS_Peak,Enemy_Total_HP,Dmg_Dealt_Total"
            );
            
            foreach (var data in _allWaveStats)
            {
                // 格式化每行数据
                string line = string.Format(
                    "{0},{1},{2},{3},{4:F1}," +
                    "{5:F0},{6:F2},{7:F2}," +
                    "{8:F0},{9:F0},{10:F0}," +
                    "{11},{12},{13}," +
                    "{14:F0},{15:F0},{16:F1},{17:F1}," +
                    "{18:F1},{19:F1}," +
                    "{20},{21},{22}," +
                    "{23:F2}," +
                    "{24:F0},{25:F0},{26:F1}," +
                    "{27:F0},{28:F0},{29:F0}",
                    // 基础字段 (0-4)
                    data.wave, data.buildType, data.playerLevel, data.result, data.timeToClear,
                    // 玩家状态 (5-7)
                    data.playerHPLost, data.tankAbsorbedRatio, data.overkillRatio,
                    // 伤害来源 (8-10)
                    data.dmgMainLaser, data.dmgSubLaser, data.dmgExplosion,
                    // 无人机选择 (11-13)
                    data.droneChoice, data.droneRewardType, data.droneRewardValue,
                    // 【新增】无人机累计 (14-22)
                    data.droneAccHealth, data.droneAccShield, data.droneAccDamagePct, data.droneAccCritPct,
                    data.droneAccLaserWidth, data.droneAccLaserLength,
                    data.droneCountSupply, data.droneCountGacha, data.droneCountDeal,
                    // 暴击 (23)
                    data.critRateActual,
                    // Boss 战 (24-26)
                    data.bossDmgCollision, data.bossDmgBullet, data.bossPushbackTime,
                    // 其他 (27-29)
                    data.dpsPeak, data.enemyTotalHP, data.dmgDealtTotal
                );
                csv.AppendLine(line);
            }
            
            string fileName = $"BattleLog_{_sessionStartTime}_{gameResult}.csv";
            string filePath = Path.Combine(Application.persistentDataPath, fileName);
            
            try
            {
                File.WriteAllText(filePath, csv.ToString());
                Debug.Log($"[BattleStatistics] ✅ CSV 已保存: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BattleStatistics] ❌ CSV 保存失败: {e.Message}");
            }
            
            if (logToConsole)
            {
                Debug.Log("=== BATTLE LOG CSV v2.1 ===\n" + csv.ToString());
            }
        }
        
        private void ResetSession()
        {
            _allWaveStats.Clear();
            _skillLevels.Clear();
            _currentWave = 0;
            _peakDPS = 0f;
            _currentSecondDamage = 0f;
            _dpsTimer = 0f;
            
            // 【v2.1】重置整局无人机累计统计
            _sessionDroneAccHealth = 0f;
            _sessionDroneAccShield = 0f;
            _sessionDroneAccDamagePct = 0f;
            _sessionDroneAccCritPct = 0f;
            _sessionDroneAccLaserWidth = 0f;
            _sessionDroneAccLaserLength = 0f;
            _sessionDroneCountSupply = 0;
            _sessionDroneCountGacha = 0;
            _sessionDroneCountDeal = 0;
        }
        
        private void PrintWaveStats(WaveStatData data)
        {
            Debug.Log($"[BattleStatistics] ═══ 波次 {data.wave} 统计 ═══");
            Debug.Log($"  流派:{data.buildType} | Lv.{data.playerLevel} | {data.result}");
            Debug.Log($"  伤害 - 主:{data.dmgMainLaser:F0} 副:{data.dmgSubLaser:F0} 爆:{data.dmgExplosion:F0}");
            Debug.Log($"  暴击率:{data.critRateActual:P1}");
            Debug.Log($"  无人机:{data.droneChoice} | 奖励:{data.droneRewardType} ({data.droneRewardValue})");
            Debug.Log($"  无人机累计 - HP:{data.droneAccHealth:F0} 护盾:{data.droneAccShield:F0} ATK%:{data.droneAccDamagePct:F1} CRIT%:{data.droneAccCritPct:F1}");
            Debug.Log($"  无人机选择次数 - 补给:{data.droneCountSupply} 金箱:{data.droneCountGacha} 契约:{data.droneCountDeal}");
            Debug.Log($"  Boss - 撞击:{data.bossDmgCollision:F0} 子弹:{data.bossDmgBullet:F0} 阻滞:{data.bossPushbackTime:F1}s");
        }
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo || !_isTracking) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 300, 10, 290, 250));
            GUILayout.Label($"═══ BattleStats v2.1 ═══");
            GUILayout.Label($"波次:{_currentWave} | 流派:{DetermineBuildType()}");
            GUILayout.Label($"主激光:{_waveMainLaserDamage:F0} | 副激光:{_waveSubLaserDamage:F0}");
            GUILayout.Label($"暴击率:{WaveCritRate:P1} ({_waveCritHitCount}/{_waveTotalHitCount})");
            GUILayout.Label($"Boss阻滞:{_waveBossPushbackTime:F1}s");
            GUILayout.Label($"─── 无人机累计 ───");
            GUILayout.Label($"HP:{_sessionDroneAccHealth:F0} | Shield:{_sessionDroneAccShield:F0}");
            GUILayout.Label($"ATK%:{_sessionDroneAccDamagePct:F1} | CRIT%:{_sessionDroneAccCritPct:F1}");
            GUILayout.Label($"选择: S{_sessionDroneCountSupply} G{_sessionDroneCountGacha} D{_sessionDroneCountDeal}");
            GUILayout.EndArea();
        }
#endif
    }
}