// ============================================================
// BattleStatistics.cs
// 文件位置: Assets/Scripts/Logic/BattleStatistics.cs
// 用途：战斗数据采集管理器 - 采集整局游戏数据并导出 CSV
// 版本：v1.0
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
    /// <summary>
    /// 流派类型枚举
    /// </summary>
    public enum BuildType
    {
        A,      // 重炮击退流：Focus + Impact + Crit
        B,      // 广域折射流：Reflex + Prism + Wide
        C,      // 异常控制流：Frost + Impact + DataBreach（待实现）
        Mix     // 混合流派
    }
    
    /// <summary>
    /// 单波次统计数据
    /// </summary>
    [Serializable]
    public class WaveStatData
    {
        public int wave;                    // 波次 (1-12)
        public string buildType;            // 流派 (A/B/C/Mix)
        public int playerLevel;             // 玩家等级 (1-16)
        public string result;               // 结果 (Win/Loss/InProgress)
        public float timeToClear;           // 本波耗时(秒)
        public float dpsPeak;               // 最高瞬时DPS
        public float enemyTotalHP;          // 本波怪物总血量
        public float dmgDealtTotal;         // 玩家总伤害（含溢出）
        public float dmgOverkill;           // 溢出伤害
        public float tankAbsorbedRatio;     // 坦克承伤比 (0.0-1.0)
        public float playerHPLost;          // 玩家该波扣血量
        public float maxExplosionDmg;       // 最大爆炸伤害
    }
    
    /// <summary>
    /// 战斗数据采集管理器
    /// 职责：
    /// - 实时采集伤害、DPS、血量变化等数据
    /// - 每波结束时生成统计报告
    /// - 游戏结束时导出 CSV 文件
    /// </summary>
    public class BattleStatistics : Singleton<BattleStatistics>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("组件引用")]
        [Tooltip("塔本体生命管理器")]
        [SerializeField] private TurretHealth turretHealth;
        
        [Tooltip("护盾控制器")]
        [SerializeField] private ShieldController shieldController;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool logToConsole = true;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - DPS 追踪
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float _currentSecondDamage = 0f;    // 当前秒累计伤害
        private float _dpsTimer = 0f;               // DPS 计时器
        private float _peakDPS = 0f;                // 本波最高 DPS
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 波次统计
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int _currentWave = 0;
        private float _waveStartTime = 0f;
        private float _waveTotalEnemyHP = 0f;           // 本波生成的怪物总血量
        private float _waveTotalDamage = 0f;            // 本波玩家总伤害（含溢出）
        private float _waveEffectiveDamage = 0f;        // 本波有效伤害（不含溢出）
        private float _waveOverkillDamage = 0f;         // 本波溢出伤害
        private float _waveTankDamage = 0f;             // 本波打在 Tank 身上的伤害
        private float _wavePlayerHPLost = 0f;           // 本波玩家扣血量
        private float _waveMaxExplosionDmg = 0f;        // 本波最大爆炸伤害
        
        // 血量追踪
        private int _waveStartHullHP = 0;
        private int _waveStartShieldHP = 0;
        
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
        public float WaveOverkillDamage => _waveOverkillDamage;
        public float WaveMaxExplosionDmg => _waveMaxExplosionDmg;
        public bool IsTracking => _isTracking;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Start()
        {
            // 自动查找组件
            if (turretHealth == null)
            {
                turretHealth = FindObjectOfType<TurretHealth>();
            }
            if (shieldController == null)
            {
                shieldController = FindObjectOfType<ShieldController>();
            }
        }
        
        private void OnEnable()
        {
            // 订阅事件
            GameEvents.OnGameStart += OnGameStart;
            GameEvents.OnWaveStateChanged += OnWaveStateChanged;
            GameEvents.OnWaveComplete += OnWaveComplete;
            GameEvents.OnGameVictory += OnGameVictory;
            GameEvents.OnGameDefeat += OnGameDefeat;       // 注意：使用 OnGameDefeat 而非 OnGameOver
            GameEvents.OnSkillApplied += OnSkillApplied;   // 注意：使用 OnSkillApplied 而非 OnSkillSelected
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
            
            // DPS 计算（1秒累积重置法）
            _dpsTimer += Time.deltaTime;
            if (_dpsTimer >= 1.0f)
            {
                // 结算这一秒的伤害
                if (_currentSecondDamage > _peakDPS)
                {
                    _peakDPS = _currentSecondDamage;
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"[BattleStatistics] 📈 新 Peak DPS: {_peakDPS:F0}");
                    }
                }
                
                // 重置计时器
                _currentSecondDamage = 0f;
                _dpsTimer -= 1.0f; // 保持精确时间
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnGameStart()
        {
            ResetSession();
            _isTracking = true;
            _sessionStartTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            
            if (showDebugInfo)
            {
                Debug.Log("[BattleStatistics] 🎮 开始采集数据...");
            }
        }
        
        private void OnWaveStateChanged(WaveState state, int waveNumber)
        {
            if (state == WaveState.Spawning)
            {
                // 新波次开始
                StartNewWave(waveNumber);
            }
        }
        
        private void OnWaveComplete(int waveNumber, int totalWaves)
        {
            // 波次完成，记录数据
            RecordWaveData(waveNumber, "Win");
        }
        
        private void OnGameVictory()
        {
            // 游戏胜利
            _isTracking = false;
            ExportToCSV("Victory");
            
            if (showDebugInfo)
            {
                Debug.Log("[BattleStatistics] 🏆 游戏胜利！数据已导出");
            }
        }
        
        private void OnGameDefeat()
        {
            // 游戏失败，记录当前波次
            RecordWaveData(_currentWave, "Loss");
            _isTracking = false;
            ExportToCSV("Defeat");
            
            if (showDebugInfo)
            {
                Debug.Log("[BattleStatistics] 💀 游戏失败！数据已导出");
            }
        }
        
        private void OnSkillApplied(SkillType type, int newLevel)
        {
            _skillLevels[type] = newLevel;
            
            if (showDebugInfo)
            {
                Debug.Log($"[BattleStatistics] 技能记录: {type} -> Lv.{newLevel}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 波次管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void StartNewWave(int waveNumber)
        {
            _currentWave = waveNumber;
            _waveStartTime = Time.time;
            
            // 重置波次统计
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
            
            // 记录波次开始时的血量
            _waveStartHullHP = turretHealth != null ? turretHealth.CurrentHullHP : 0;
            _waveStartShieldHP = shieldController != null ? shieldController.CurrentShieldHP : 0;
            
            if (showDebugInfo)
            {
                Debug.Log($"[BattleStatistics] ========== 波次 {waveNumber} 开始 ==========");
                Debug.Log($"[BattleStatistics] 初始血量 - 护盾:{_waveStartShieldHP} 本体:{_waveStartHullHP}");
            }
        }
        
        private void RecordWaveData(int waveNumber, string result)
        {
            float duration = Time.time - _waveStartTime;
            
            // 计算玩家扣血量
            int currentHullHP = turretHealth != null ? turretHealth.CurrentHullHP : 0;
            int currentShieldHP = shieldController != null ? shieldController.CurrentShieldHP : 0;
            int totalHPLost = (_waveStartHullHP - currentHullHP) + (_waveStartShieldHP - currentShieldHP);
            _wavePlayerHPLost = Mathf.Max(0, totalHPLost);
            
            // 计算坦克承伤比
            float tankRatio = _waveTotalDamage > 0 ? _waveTankDamage / _waveTotalDamage : 0f;
            
            // 获取流派
            string buildType = DetermineBuildType();
            
            // 获取玩家等级
            int playerLevel = ProgressManager.Instance != null ? 
                ProgressManager.Instance.CurrentLevel : 1;
            
            // 创建统计数据
            WaveStatData data = new WaveStatData
            {
                wave = waveNumber,
                buildType = buildType,
                playerLevel = playerLevel,
                result = result,
                timeToClear = duration,
                dpsPeak = _peakDPS,
                enemyTotalHP = _waveTotalEnemyHP,
                dmgDealtTotal = _waveTotalDamage,
                dmgOverkill = _waveOverkillDamage,
                tankAbsorbedRatio = tankRatio,
                playerHPLost = _wavePlayerHPLost,
                maxExplosionDmg = _waveMaxExplosionDmg
            };
            
            _allWaveStats.Add(data);
            
            // 打印到控制台
            if (logToConsole)
            {
                PrintWaveStats(data);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部数据上报接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 上报伤害数据（由 EnemyBlob.TakeDamage 调用）
        /// </summary>
        /// <param name="effectiveDamage">有效伤害（实际扣血）</param>
        /// <param name="overkillDamage">溢出伤害</param>
        /// <param name="enemyType">敌人类型（用于判断 Tank）</param>
        public void RecordDamage(float effectiveDamage, float overkillDamage, EnemyType enemyType)
        {
            if (!_isTracking) return;
            
            float totalDamage = effectiveDamage + overkillDamage;
            
            // 累加伤害
            _waveTotalDamage += totalDamage;
            _waveEffectiveDamage += effectiveDamage;
            _waveOverkillDamage += overkillDamage;
            
            // DPS 累加
            _currentSecondDamage += totalDamage;
            
            // Tank 承伤统计
            if (enemyType == EnemyType.Tank || enemyType == EnemyType.EliteTank)
            {
                _waveTankDamage += totalDamage;
            }
        }
        
        /// <summary>
        /// 上报敌人总血量（由 WaveManager 在生成敌人时调用）
        /// </summary>
        /// <param name="hp">敌人血量</param>
        public void RecordEnemyHP(float hp)
        {
            if (!_isTracking) return;
            _waveTotalEnemyHP += hp;
        }
        
        /// <summary>
        /// 上报爆炸伤害（由 SkillEffectManager.TriggerFocusExplosion 调用）
        /// </summary>
        /// <param name="explosionDamage">爆炸伤害值</param>
        public void RecordExplosionDamage(float explosionDamage)
        {
            if (!_isTracking) return;
            
            if (explosionDamage > _waveMaxExplosionDmg)
            {
                _waveMaxExplosionDmg = explosionDamage;
                
                if (showDebugInfo)
                {
                    Debug.Log($"[BattleStatistics] 💥 新 Max 爆炸伤害: {_waveMaxExplosionDmg:F0}");
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 流派判定
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 根据已选技能判定流派
        /// </summary>
        private string DetermineBuildType()
        {
            // 流派A：重炮击退流 - Focus + Impact + Crit
            bool hasFocus = _skillLevels.ContainsKey(SkillType.Focus) && _skillLevels[SkillType.Focus] > 0;
            bool hasImpact = _skillLevels.ContainsKey(SkillType.Impact) && _skillLevels[SkillType.Impact] > 0;
            bool hasCrit = _skillLevels.ContainsKey(SkillType.Crit) && _skillLevels[SkillType.Crit] > 0;
            
            // 流派B：广域折射流 - Reflex + Prism + Wide
            bool hasReflex = _skillLevels.ContainsKey(SkillType.Reflex) && _skillLevels[SkillType.Reflex] > 0;
            bool hasPrism = _skillLevels.ContainsKey(SkillType.Prism) && _skillLevels[SkillType.Prism] > 0;
            bool hasWide = _skillLevels.ContainsKey(SkillType.Wide) && _skillLevels[SkillType.Wide] > 0;
            
            // 流派C：异常控制流 - Frost + Impact + DataBreach（待实现）
            bool hasFrost = _skillLevels.ContainsKey(SkillType.Frost) && _skillLevels[SkillType.Frost] > 0;
            // TODO: 当 DataBreach 技能实现后，添加判定
            // bool hasDataBreach = _skillLevels.ContainsKey(SkillType.DataBreach) && _skillLevels[SkillType.DataBreach] > 0;
            bool hasDataBreach = false; // 暂时设为 false
            
            // 判定流派（需要同时拥有3个技能）
            if (hasFocus && hasImpact && hasCrit)
            {
                return "A";
            }
            else if (hasReflex && hasPrism && hasWide)
            {
                return "B";
            }
            else if (hasFrost && hasImpact && hasDataBreach)
            {
                return "C";
            }
            else
            {
                return "Mix";
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // CSV 导出
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 导出数据到 CSV 文件
        /// </summary>
        /// <param name="gameResult">游戏结果（Victory/Defeat）</param>
        private void ExportToCSV(string gameResult)
        {
            StringBuilder csv = new StringBuilder();
            
            // 表头
            csv.AppendLine("Wave,Build_Type,Player_Level,Result,Time_To_Clear,DPS_Peak,Enemy_Total_HP,Dmg_Dealt_Total,Dmg_Overkill,Tank_Absorbed_Ratio,Player_HP_Lost,Max_Explosion_Dmg");
            
            // 数据行
            foreach (var data in _allWaveStats)
            {
                string line = string.Format("{0},{1},{2},{3},{4:F1},{5:F0},{6:F0},{7:F0},{8:F0},{9:F2},{10:F0},{11:F0}",
                    data.wave,
                    data.buildType,
                    data.playerLevel,
                    data.result,
                    data.timeToClear,
                    data.dpsPeak,
                    data.enemyTotalHP,
                    data.dmgDealtTotal,
                    data.dmgOverkill,
                    data.tankAbsorbedRatio,
                    data.playerHPLost,
                    data.maxExplosionDmg
                );
                csv.AppendLine(line);
            }
            
            // 保存文件
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
            
            // 同时打印到控制台（方便复制）
            if (logToConsole)
            {
                Debug.Log("=== BATTLE LOG CSV (COPY BELOW) ===\n" + csv.ToString());
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 辅助方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
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
            Debug.Log($"[BattleStatistics] ========== 波次 {data.wave} 统计 ==========");
            Debug.Log($"  流派: {data.buildType} | 等级: Lv.{data.playerLevel} | 结果: {data.result}");
            Debug.Log($"  耗时: {data.timeToClear:F1}s | Peak DPS: {data.dpsPeak:F0}");
            Debug.Log($"  敌人总HP: {data.enemyTotalHP:F0} | 总伤害: {data.dmgDealtTotal:F0}");
            Debug.Log($"  溢出伤害: {data.dmgOverkill:F0} | Tank承伤比: {data.tankAbsorbedRatio:P0}");
            Debug.Log($"  玩家扣血: {data.playerHPLost:F0} | 最大爆炸: {data.maxExplosionDmg:F0}");
            Debug.Log("================================================");
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [ContextMenu("打印当前统计")]
        public void DebugPrintCurrentStats()
        {
            Debug.Log($"=== 当前波次 {_currentWave} 实时数据 ===");
            Debug.Log($"Peak DPS: {_peakDPS:F0}");
            Debug.Log($"总伤害: {_waveTotalDamage:F0}");
            Debug.Log($"溢出伤害: {_waveOverkillDamage:F0}");
            Debug.Log($"Tank承伤: {_waveTankDamage:F0}");
            Debug.Log($"敌人总HP: {_waveTotalEnemyHP:F0}");
            Debug.Log($"最大爆炸: {_waveMaxExplosionDmg:F0}");
            Debug.Log($"流派: {DetermineBuildType()}");
        }
        
        [ContextMenu("测试导出 CSV")]
        public void DebugExportCSV()
        {
            // 添加测试数据
            _allWaveStats.Add(new WaveStatData
            {
                wave = 1,
                buildType = "A",
                playerLevel = 3,
                result = "Win",
                timeToClear = 45.2f,
                dpsPeak = 1500f,
                enemyTotalHP = 5000f,
                dmgDealtTotal = 5500f,
                dmgOverkill = 500f,
                tankAbsorbedRatio = 0.35f,
                playerHPLost = 200f,
                maxExplosionDmg = 750f
            });
            
            ExportToCSV("Test");
        }
        
        [ContextMenu("打开 CSV 文件夹")]
        public void DebugOpenDataFolder()
        {
            string path = Application.persistentDataPath;
            Debug.Log($"数据保存路径: {path}");
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(path);
            #endif
        }
#endif
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试 GUI
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo || !_isTracking) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 290, 10, 280, 200));
            GUILayout.Label("=== Battle Statistics ===");
            GUILayout.Label($"波次: {_currentWave}");
            GUILayout.Label($"流派: {DetermineBuildType()}");
            GUILayout.Label($"Peak DPS: {_peakDPS:F0}");
            GUILayout.Label($"当前秒DPS: {_currentSecondDamage:F0}");
            GUILayout.Label($"总伤害: {_waveTotalDamage:F0}");
            GUILayout.Label($"溢出: {_waveOverkillDamage:F0}");
            GUILayout.Label($"Tank承伤: {_waveTankDamage:F0}");
            GUILayout.Label($"最大爆炸: {_waveMaxExplosionDmg:F0}");
            GUILayout.EndArea();
        }
#endif
    }
}