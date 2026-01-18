// ============================================================
// BattleStatistics.cs
// 文件位置: Assets/Scripts/Logic/BattleStatistics.cs
// 用途：战斗数据采集管理器 v2.0
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    /// 单波次统计数据 v2.0
    /// </summary>
    [Serializable]
    public class WaveStatData
    {
        // 基础字段
        public int wave;
        public string buildType;
        public int playerLevel;
        public string result;
        public float timeToClear;
        
        // 玩家状态
        public float playerHPLost;
        public float tankAbsorbedRatio;
        public float overkillRatio;
        
        // 伤害来源细分
        public float dmgMainLaser;
        public float dmgSubLaser;
        public float dmgExplosion;
        
        // 无人机空投数据（波次开始前的选择）
        public string droneChoice;          // 选择的箱子 (Supply/Gacha/Deal/None)
        public string droneRewardType;      // 具体奖励类型 (HealthRestore/BaseDamagePercent/etc)
        public string droneRewardValue;     // 奖励数值 (+100/-5%/etc)
        
        // 暴击数据
        public float critRateActual;
        
        // Boss 战数据
        public float bossDmgCollision;      // Boss 撞击伤害（Charge/Press）
        public float bossDmgBullet;         // Boss 子弹 + 摩擦 + 召唤小怪伤害
        public float bossPushbackTime;      // Boss 被有效阻滞时长（B流派条件）
        
        // 旧字段（兼容）
        public float dpsPeak;
        public float enemyTotalHP;
        public float dmgDealtTotal;
        public float dmgOverkill;
        public float maxExplosionDmg;
    }
    
    /// <summary>
    /// 战斗数据采集管理器 v2.0
    /// </summary>
    public class BattleStatistics : Singleton<BattleStatistics>
    {
        [Header("组件引用")]
        [SerializeField] private TurretHealth turretHealth;
        [SerializeField] private ShieldController shieldController;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool logToConsole = true;
        
        // DPS 追踪
        private float _currentSecondDamage = 0f;
        private float _dpsTimer = 0f;
        private float _peakDPS = 0f;
        
        // 波次统计 - 基础
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
        
        // 波次统计 - 伤害来源
        private float _waveMainLaserDamage = 0f;
        private float _waveSubLaserDamage = 0f;
        private float _waveExplosionDamage = 0f;
        
        // 波次统计 - 暴击
        private int _waveTotalHitCount = 0;
        private int _waveCritHitCount = 0;
        
        // 波次统计 - 玩家受伤
        private float _waveBossCollisionDamage = 0f;
        private float _waveBossBulletDamage = 0f;
        
        // 波次统计 - Boss 阻滞
        private float _waveBossPushbackTime = 0f;
        private bool _isBossBeingPushed = false;
        
        // 波次统计 - 无人机选择（记录上一波结束时的选择，应用到下一波）
        private string _waveDroneChoice = "None";           // 箱子类型
        private string _waveDroneRewardType = "None";       // 奖励类型
        private string _waveDroneRewardValue = "None";      // 奖励数值
        
        // 整局统计
        private List<WaveStatData> _allWaveStats = new List<WaveStatData>();
        private Dictionary<SkillType, int> _skillLevels = new Dictionary<SkillType, int>();
        private bool _isTracking = false;
        private string _sessionStartTime;
        
        // 公共属性
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
            if (showDebugInfo) Debug.Log("[BattleStatistics] 🎮 开始采集数据 v2.0");
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
                
                droneChoice = _waveDroneChoice,
                droneRewardType = _waveDroneRewardType,
                droneRewardValue = _waveDroneRewardValue,
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
        // 外部数据上报接口 - 无人机选择
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 记录无人机选择（由 TacticalDropManager 调用）
        /// </summary>
        /// <param name="crateType">选择的箱子类型 (Supply/Gacha/Deal)</param>
        /// <param name="rewardType">获得的奖励类型 (HealthRestore/BaseDamagePercent/etc)</param>
        /// <param name="rewardValue">奖励显示值 (+100/-5%/etc)</param>
        public void RecordDroneChoice(string crateType, string rewardType, string rewardValue)
        {
            if (!_isTracking) return;
            _waveDroneChoice = crateType;
            _waveDroneRewardType = rewardType;
            _waveDroneRewardValue = rewardValue;
            
            if (showDebugInfo)
            {
                Debug.Log($"[BattleStatistics] 无人机选择: {crateType} | 奖励: {rewardType} ({rewardValue})");
            }
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
        // CSV 导出
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ExportToCSV(string gameResult)
        {
            StringBuilder csv = new StringBuilder();
            
            // CSV Header - 21 fields
            csv.AppendLine("Wave,Build_Type,Player_Level,Result,Time_To_Clear," +
                          "Player_HP_Lost,Tank_Absorbed_Ratio,Overkill_Ratio," +
                          "Dmg_Main_Laser,Dmg_Sub_Laser,Dmg_Explosion," +
                          "Drone_Choice,Drone_Reward_Type,Drone_Reward_Value,Crit_Rate_Actual," +
                          "Boss_Dmg_Collision,Boss_Dmg_Bullet,Boss_Pushback_Time," +
                          "DPS_Peak,Enemy_Total_HP,Dmg_Dealt_Total");
            
            foreach (var data in _allWaveStats)
            {
                string line = string.Format(
                    "{0},{1},{2},{3},{4:F1}," +
                    "{5:F0},{6:F2},{7:F2}," +
                    "{8:F0},{9:F0},{10:F0}," +
                    "{11},{12},{13},{14:F2}," +
                    "{15:F0},{16:F0},{17:F1}," +
                    "{18:F0},{19:F0},{20:F0}",
                    data.wave, data.buildType, data.playerLevel, data.result, data.timeToClear,
                    data.playerHPLost, data.tankAbsorbedRatio, data.overkillRatio,
                    data.dmgMainLaser, data.dmgSubLaser, data.dmgExplosion,
                    data.droneChoice, data.droneRewardType, data.droneRewardValue, data.critRateActual,
                    data.bossDmgCollision, data.bossDmgBullet, data.bossPushbackTime,
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
                Debug.Log("=== BATTLE LOG CSV v2.0 ===\n" + csv.ToString());
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
        }
        
        private void PrintWaveStats(WaveStatData data)
        {
            Debug.Log($"[BattleStatistics] ═══ 波次 {data.wave} 统计 ═══");
            Debug.Log($"  流派:{data.buildType} | Lv.{data.playerLevel} | {data.result}");
            Debug.Log($"  伤害 - 主:{data.dmgMainLaser:F0} 副:{data.dmgSubLaser:F0} 爆:{data.dmgExplosion:F0}");
            Debug.Log($"  暴击率:{data.critRateActual:P1}");
            Debug.Log($"  无人机:{data.droneChoice} | 奖励:{data.droneRewardType} ({data.droneRewardValue})");
            Debug.Log($"  Boss - 撞击:{data.bossDmgCollision:F0} 子弹:{data.bossDmgBullet:F0} 阻滞:{data.bossPushbackTime:F1}s");
        }
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo || !_isTracking) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 300, 10, 290, 200));
            GUILayout.Label($"═══ BattleStats v2.0 ═══");
            GUILayout.Label($"波次:{_currentWave} | 流派:{DetermineBuildType()}");
            GUILayout.Label($"主激光:{_waveMainLaserDamage:F0} | 副激光:{_waveSubLaserDamage:F0}");
            GUILayout.Label($"暴击率:{WaveCritRate:P1} ({_waveCritHitCount}/{_waveTotalHitCount})");
            GUILayout.Label($"Boss阻滞:{_waveBossPushbackTime:F1}s");
            GUILayout.EndArea();
        }
#endif
    }
}