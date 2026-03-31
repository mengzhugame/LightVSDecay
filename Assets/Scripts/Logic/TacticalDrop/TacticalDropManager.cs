// ============================================================
// TacticalDropManager.cs
// 文件位置: Assets/Scripts/Logic/TacticalDrop/TacticalDropManager.cs
// 用途：战术空投宝箱系统管理器
// 更新：v2.1 - 新增金币奖励、下一波怪物增强效果
// ============================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using LightVsDecay.Core;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Player;
using LightVsDecay.Logic.Statistics;
using LightVsDecay.UI.FloatingText.TacticalDrop;

namespace LightVsDecay.Logic.TacticalDrop
{
    /// <summary>
    /// 下一波怪物增强数据
    /// </summary>
    public struct NextWaveBuffData
    {
        public float speedMultiplier;   // 移速倍率（1.0 = 无加成）
        public float healthMultiplier;  // 血量倍率
        public float damageMultiplier;  // 伤害倍率
        
        public static NextWaveBuffData Default => new NextWaveBuffData
        {
            speedMultiplier = 1f,
            healthMultiplier = 1f,
            damageMultiplier = 1f
        };
        
        public bool HasBuff => speedMultiplier > 1f || healthMultiplier > 1f || damageMultiplier > 1f;
    }
    
    /// <summary>
    /// 战术空投系统管理器
    /// 职责：
    /// - 监听波次完成事件
    /// - 生成并管理3个宝箱
    /// - 处理宝箱被击破时的奖励
    /// - 控制奖励飘字动画
    /// - 通知 WaveManager 开始下一波
    /// - 【新增】管理下一波怪物增强效果
    /// </summary>
    public class TacticalDropManager : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public static TacticalDropManager Instance { get; private set; }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("奖励配置")]
        [SerializeField] private DroneRewardConfig rewardConfig;
        
        [Header("预制体")]
        [Tooltip("蓝色补给箱预制体")]
        [SerializeField] private GameObject supplyCratePrefab;
        
        [Tooltip("金色问号箱预制体")]
        [SerializeField] private GameObject gachaCratePrefab;
        
        [Tooltip("红色契约箱预制体")]
        [SerializeField] private GameObject dealCratePrefab;
        
        [Header("位置设置")]
        [Tooltip("宝箱生成起始Y坐标（屏幕外）")]
        [SerializeField] private float spawnStartY = 8f;
        
        [Tooltip("宝箱落地Y坐标")]
        [SerializeField] private float landingY = 2f;
        
        [Tooltip("左侧宝箱X坐标")]
        [SerializeField] private float leftCrateX = -3f;
        
        [Tooltip("中间宝箱X坐标")]
        [SerializeField] private float centerCrateX = 0f;
        
        [Tooltip("右侧宝箱X坐标")]
        [SerializeField] private float rightCrateX = 3f;
        
        [Header("组件引用")]
        [Tooltip("光棱塔 Transform（飘字飞向目标）")]
        [SerializeField] private Transform turretTransform;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private List<TacticalCrate> activeCrates = new List<TacticalCrate>();
        private bool isDropPhase = false;
        private int gachaBadLuckCounter = 0; // 金箱连续负收益计数
        
        // 缓存的组件引用
        private TurretHealth turretHealth;
        private ShieldController shieldController;
        private LaserController laserController;
        
        // 【新增】下一波怪物增强数据
        private NextWaveBuffData nextWaveBuff = NextWaveBuffData.Default;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public bool IsDropPhase => isDropPhase;
        
        /// <summary>
        /// 【新增】获取下一波怪物增强数据
        /// </summary>
        public NextWaveBuffData NextWaveBuff => nextWaveBuff;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        private void Start()
        {
            CacheComponents();
        }
        
        private void OnEnable()
        {
            // 订阅波次完成事件
            GameEvents.OnWaveComplete += OnWaveComplete;
            GameEvents.OnGameStart += OnGameStart;
            GameEvents.OnGameDefeat += OnGameOver;      
            GameEvents.OnGameVictory += OnGameOver; 
        }
        
        private void OnDisable()
        {
            GameEvents.OnWaveComplete -= OnWaveComplete;
            GameEvents.OnGameStart -= OnGameStart;
            GameEvents.OnGameDefeat -= OnGameOver;     
            GameEvents.OnGameVictory -= OnGameOver; 
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 缓存组件引用
        /// </summary>
        private void CacheComponents()
        {
            if (turretHealth == null)
            {
                turretHealth = FindObjectOfType<TurretHealth>();
            }
            
            if (shieldController == null)
            {
                shieldController = FindObjectOfType<ShieldController>();
            }
            
            if (laserController == null)
            {
                laserController = FindObjectOfType<LaserController>();
            }
            
            if (turretTransform == null && turretHealth != null)
            {
                turretTransform = turretHealth.transform;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 下一波增强管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 【新增】消费并重置下一波增强数据（由 WaveManager 在波次开始时调用）
        /// </summary>
        public NextWaveBuffData ConsumeNextWaveBuff()
        {
            NextWaveBuffData buff = nextWaveBuff;
            nextWaveBuff = NextWaveBuffData.Default;
            
            if (showDebugInfo && buff.HasBuff)
            {
                GameLogger.Log($"[TacticalDropManager] 应用下波增强: Speed={buff.speedMultiplier:P0}, HP={buff.healthMultiplier:P0}, DMG={buff.damageMultiplier:P0}");
            }
            
            return buff;
        }
        
        /// <summary>
        /// 【新增】添加下一波移速增强
        /// </summary>
        private void AddNextWaveSpeedBuff(float percent)
        {
            nextWaveBuff.speedMultiplier += percent;
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[TacticalDropManager] 下一波移速增强: +{percent:P0} → 总计 {nextWaveBuff.speedMultiplier:P0}");
            }
        }
        
        /// <summary>
        /// 【新增】添加下一波血量增强
        /// </summary>
        private void AddNextWaveHealthBuff(float percent)
        {
            nextWaveBuff.healthMultiplier += percent;
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[TacticalDropManager] 下一波血量增强: +{percent:P0} → 总计 {nextWaveBuff.healthMultiplier:P0}");
            }
        }
        
        /// <summary>
        /// 【新增】添加下一波伤害增强
        /// </summary>
        private void AddNextWaveDamageBuff(float percent)
        {
            nextWaveBuff.damageMultiplier += percent;
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[TacticalDropManager] 下一波伤害增强: +{percent:P0} → 总计 {nextWaveBuff.damageMultiplier:P0}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 游戏开始时重置
        /// </summary>
        private void OnGameStart()
        {
            gachaBadLuckCounter = 0;
            nextWaveBuff = NextWaveBuffData.Default;
            ClearAllCrates();
            CacheComponents();

            if (showDebugInfo)
            {
                GameLogger.Log("[TacticalDropManager] 游戏开始，重置状态");
            }
        }
        // 3. 新增 OnGameOver 方法
        /// <summary>
        /// 游戏结束时重置（失败或胜利）
        /// </summary>
        private void OnGameOver()
        {
            // 重置激光空投加成
            if (laserController != null)
            {
                laserController.ResetDropBonuses();
            }
    
            // 重置暴击率加成
            if (laserController != null)
            {
                laserController.ResetCritRateBonus();
            }
    
            if (showDebugInfo)
            {
                GameLogger.Log("[TacticalDropManager] 游戏结束，重置空投加成");
            }
        }
        /// <summary>
        /// 波次完成时触发空投
        /// </summary>
        private void OnWaveComplete(int completedWave, int totalWaves)
        {
            bool isBoss = WaveManager.Instance != null && WaveManager.Instance.IsBossWave;
            GameLogger.Log($"[DROP_DIAG] OnWaveComplete 触发 wave={completedWave}/{totalWaves} | IsBossWave={isBoss} | isDropPhase={isDropPhase}");

            // Boss 波不触发空投（如果需要的话可以在这里加条件）
            if (isBoss)
            {
                GameLogger.Log("[DROP_DIAG] Boss 波，跳过空投");
                return;
            }

            GameLogger.Log($"[DROP_DIAG] 启动 StartDropPhase 协程...");
            StartCoroutine(StartDropPhase());
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 空投流程
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 开始空投阶段
        /// </summary>
        private IEnumerator StartDropPhase()
        {
            GameLogger.Log($"[DROP_DIAG] StartDropPhase 开始 | supplyCratePrefab={(supplyCratePrefab==null?"NULL":"OK")} | gachaCratePrefab={(gachaCratePrefab==null?"NULL":"OK")} | dealCratePrefab={(dealCratePrefab==null?"NULL":"OK")}");
            isDropPhase = true;

            // 清理旧的宝箱（以防万一）
            ClearAllCrates();

            // 获取配置参数
            float enterDuration = rewardConfig != null ? rewardConfig.enterDuration : 0.45f;
            float staggerDelay = rewardConfig != null ? rewardConfig.staggerDelay : 0.1f;

            GameLogger.Log($"[DROP_DIAG] 生成无人机 enterDuration={enterDuration} staggerDelay={staggerDelay}");
            // 错帧生成3个无人机
            yield return StartCoroutine(SpawnCratesStaggered(enterDuration, staggerDelay));

            // 统一启用所有无人机的伤害
            EnableAllCratesDamage();

            GameLogger.Log("[DROP_DIAG] ✅ 所有无人机已落地，等待玩家选择...");
        }
        
        /// <summary>
        /// 错帧生成3个无人机（左→中→右）
        /// </summary>
        private IEnumerator SpawnCratesStaggered(float enterDuration, float staggerDelay)
        {
            int crateHP = rewardConfig != null ? rewardConfig.crateHP : 500;
            int landedCount = 0;

            // 左侧 - 补给
            SpawnCrate(supplyCratePrefab, CrateType.Supply, leftCrateX, crateHP, enterDuration, () => landedCount++);
            yield return new WaitForSeconds(staggerDelay);

            // 中间 - 问号
            SpawnCrate(gachaCratePrefab, CrateType.Gacha, centerCrateX, crateHP, enterDuration, () => landedCount++);
            yield return new WaitForSeconds(staggerDelay);

            // 右侧 - 契约
            SpawnCrate(dealCratePrefab, CrateType.Deal, rightCrateX, crateHP, enterDuration, () => landedCount++);

            // 等待所有无人机落地
            float timeout = enterDuration + 1f;
            float timer = 0f;
            while (landedCount < 3 && timer < timeout)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
        
        /// <summary>
        /// 统一启用所有无人机的伤害检测
        /// </summary>
        private void EnableAllCratesDamage()
        {
            foreach (var crate in activeCrates)
            {
                if (crate != null)
                {
                    crate.EnableDamage();
                }
            }
        }
        
        /// <summary>
        /// 生成单个宝箱
        /// </summary>
        private void SpawnCrate(GameObject prefab, CrateType type, float xPos, int hp, float dropDuration, System.Action onLanded = null)
        {
            if (prefab == null)
            {
                GameLogger.LogError($"[TacticalDropManager] 无人机预制体为空: {type}");
                return;
            }
            
            Vector3 spawnPos = new Vector3(xPos, spawnStartY, 0f);
            GameObject crateObj = Instantiate(prefab, spawnPos, Quaternion.identity);
            
            TacticalCrate crate = crateObj.GetComponent<TacticalCrate>();
            if (crate == null)
            {
                GameLogger.LogError($"[TacticalDropManager] 预制体缺少 TacticalCrate 组件: {type}");
                Destroy(crateObj);
                return;
            }
            
            // 初始化
            crate.Initialize(type, hp);
            
            // 播放入场动画（传递落地回调）
            crate.PlayDropAnimation(spawnStartY, landingY, dropDuration, onLanded);
            
            activeCrates.Add(crate);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[TacticalDropManager] 生成无人机: {type} @ ({xPos}, {spawnStartY}) → Y={landingY}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 宝箱被击破处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 宝箱被击破回调
        /// </summary>
        public void OnCrateDestroyed(TacticalCrate destroyedCrate)
        {
            if (!isDropPhase) return;
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[TacticalDropManager] 宝箱被击破: {destroyedCrate.CrateType}");
            }
            
            // 让其他宝箱消失
            foreach (var crate in activeCrates)
            {
                if (crate != null && crate != destroyedCrate && !crate.IsDead)
                {
                    crate.Vanish();
                }
            }
            
            // 处理奖励
            ProcessReward(destroyedCrate);
        }
        
        /// <summary>
        /// 处理奖励
        /// </summary>
        private void ProcessReward(TacticalCrate crate)
        {
            switch (crate.CrateType)
            {
                case CrateType.Supply:
                    ProcessSupplyReward(crate);
                    break;
                    
                case CrateType.Gacha:
                    ProcessGachaReward(crate);
                    break;
                    
                case CrateType.Deal:
                    ProcessDealReward(crate);
                    break;
            }
        }
        
        /// <summary>
        /// 处理补给箱奖励
        /// </summary>
        private void ProcessSupplyReward(TacticalCrate crate)
        {
            if (rewardConfig == null) return;
    
            // 检查护盾状态
            bool isShieldBroken = shieldController == null || shieldController.CurrentShieldHP <= 0;

            // 激光长度状态（用于过滤或降权长度选项）
            bool laserAtCap = laserController != null && laserController.IsMainLaserAtLengthCap;
            bool laserHasLengthSkill = laserController != null && laserController.HasReflexLengthBonus;

            RewardEntry reward = rewardConfig.GetRandomSupplyReward(isShieldBroken, laserAtCap, laserHasLengthSkill);
            if (reward == null)
            {
                GameLogger.LogWarning("[TacticalDropManager] 补给箱奖励池为空！");
                EndDropPhase();
                return;
            }
    
            // 应用奖励
            ApplyReward(reward);
            if (BattleStatistics.Instance != null)
            {
                BattleStatistics.Instance.RecordDroneChoice(
                    "Supply",
                    reward.type.ToString(),
                    reward.displayText
                );
            }
            // 显示飘字
            if (DroneRewardTextManager.Instance != null)
            {
                DroneRewardTextManager.Instance.ShowSupplyReward(
                    crate.transform.position + Vector3.up * 0.5f,
                    reward.type,
                    reward.displayText
                );
            }
    
            // 结束空投阶段
            EndDropPhase();
        }
        
        /// <summary>
        /// 处理金箱奖励
        /// </summary>
        private void ProcessGachaReward(TacticalCrate crate)
        {
            if (rewardConfig == null) return;
    
            // 检查保底
            bool forceEpic = gachaBadLuckCounter >= rewardConfig.gachaBadLuckProtectionCount;

            // 激光长度状态
            bool laserAtCap = laserController != null && laserController.IsMainLaserAtLengthCap;
            bool laserHasLengthSkill = laserController != null && laserController.HasReflexLengthBonus;

            var (resultType, reward, mockText) = rewardConfig.GetGachaResult(forceEpic, laserAtCap, laserHasLengthSkill);
    
            if (showDebugInfo)
            {
                GameLogger.Log($"[TacticalDropManager] 金箱结果: {resultType}, 保底触发={forceEpic}");
            }
    
            // 更新保底计数
            switch (resultType)
            {
                case GachaResultType.Nothing:
                case GachaResultType.Negative:
                    gachaBadLuckCounter++;
                    break;
            
                case GachaResultType.Normal:
                case GachaResultType.Epic:
                    gachaBadLuckCounter = 0; // 重置
                    break;
            }
    
            // 处理结果并显示飘字
            Vector3 textPos = crate.transform.position + Vector3.up * 0.5f;
    
            if (resultType == GachaResultType.Nothing)
            {
                // 【v2.1】Nothing 现在改为金币奖励
                if (reward != null)
                {
                    ApplyReward(reward);
                    
                    // 上报
                    if (BattleStatistics.Instance != null)
                    {
                        BattleStatistics.Instance.RecordDroneChoice("Gacha_Coin", reward.type.ToString(), reward.displayText);
                    }
                    
                    // 显示金币飘字
                    if (DroneRewardTextManager.Instance != null)
                    {
                        DroneRewardTextManager.Instance.ShowCoinReward(textPos, reward.displayText);
                    }
                }
            }
            else if (reward != null)
            {
                ApplyReward(reward);
    
                // 上报
                if (BattleStatistics.Instance != null)
                {
                    string gachaType = resultType == GachaResultType.Epic ? "Gacha_Epic" : 
                        resultType == GachaResultType.Negative ? "Gacha_Negative" : "Gacha";
                    BattleStatistics.Instance.RecordDroneChoice(
                        gachaType,
                        reward.type.ToString(),
                        reward.displayText
                    );
                }

                // 判断是否为怪物增强效果
                bool isMonsterBuff = rewardConfig.IsMonsterBuffReward(reward.type);
                bool isEpic = resultType == GachaResultType.Epic;

                if (DroneRewardTextManager.Instance != null)
                {
                    if (isMonsterBuff)
                    {
                        // 显示怪物增强飘字
                        DroneRewardTextManager.Instance.ShowMonsterBuffEffect(textPos, reward.displayText);
                    }
                    else
                    {
                        DroneRewardTextManager.Instance.ShowGachaReward(
                            textPos,
                            reward.type,
                            reward.displayText,
                            isEpic
                        );
                    }
                }
            }
    
            // 结束空投阶段
            EndDropPhase();
        }
        
        /// <summary>
        /// 处理契约箱奖励
        /// </summary>
        private void ProcessDealReward(TacticalCrate crate)
        {
            if (rewardConfig == null) return;
    
            // 检查护盾状态
            bool isShieldBroken = shieldController == null || shieldController.CurrentShieldHP <= 0;

            DealEntry deal = rewardConfig.GetRandomDeal(isShieldBroken);
            if (deal == null)
            {
                GameLogger.LogWarning("[TacticalDropManager] 契约箱交易池为空！");
                EndDropPhase();
                return;
            }
    
            // 先应用代价
            if (deal.cost != null)
            {
                ApplyReward(deal.cost);
            }
    
            // 再应用收益
            if (deal.gain != null)
            {
                ApplyReward(deal.gain);
            }
            
            if (BattleStatistics.Instance != null && deal.cost != null && deal.gain != null)
            {
                BattleStatistics.Instance.RecordDroneChoice(
                    "Deal",
                    $"{deal.cost.type}→{deal.gain.type}",
                    $"{deal.cost.displayText}|{deal.gain.displayText}"
                );
            }
            
            // 显示飘字（代价 + 收益）
            if (DroneRewardTextManager.Instance != null && deal.cost != null && deal.gain != null)
            {
                DroneRewardTextManager.Instance.ShowDealReward(
                    crate.transform.position + Vector3.up * 0.5f,
                    deal.cost.type,
                    deal.cost.displayText,
                    deal.gain.type,
                    deal.gain.displayText,
                    deal.gain.isJackpot
                );
            }
    
            // 结束空投阶段
            EndDropPhase();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 奖励应用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 应用奖励效果
        /// </summary>
        private void ApplyReward(RewardEntry reward)
        {
            if (reward == null) return;
            
            switch (reward.type)
            {
                // ═══ 补给类 ═══
                case RewardType.HealthRestore:
                    if (turretHealth != null)
                    {
                        turretHealth.RestoreHealth(Mathf.RoundToInt(reward.value));
                    }
                    break;
                    
                case RewardType.ShieldRestore:
                    if (shieldController != null)
                    {
                        shieldController.RestoreShield(Mathf.RoundToInt(reward.value));
                    }
                    break;
                    
                case RewardType.ShieldFull:
                    if (shieldController != null)
                    {
                        shieldController.ResetShield();
                    }
                    break;
                    
                // ═══ 属性提升类 ═══
                case RewardType.BaseDamagePercent:
                    if (laserController != null)
                    {
                        laserController.AddDamagePercent(reward.value);
                    }
                    break;
                    
                case RewardType.BaseDamageFlat:
                    if (laserController != null)
                    {
                        laserController.AddDamageFlat(reward.value);
                    }
                    break;
                    
                case RewardType.CritRatePercent:
                    if (laserController != null)
                    {
                        laserController.AddCritRateBonus(reward.value);
                    }
                    break;
                    
                case RewardType.LaserWidthPercent:
                    if (laserController != null)
                    {
                        laserController.AddWidthPercent(reward.value);
                    }
                    break;
                    
                case RewardType.LaserLengthPercent:
                    if (laserController != null)
                    {
                        laserController.AddLengthPercent(reward.value);
                    }
                    break;
                    
                // ═══ 金币类 ═══
                case RewardType.CoinReward:
                    if (ProgressManager.Instance != null)
                    {
                        ProgressManager.Instance.AddCoins(Mathf.RoundToInt(reward.value));
                        
                        if (showDebugInfo)
                        {
                            GameLogger.Log($"[TacticalDropManager] 获得金币: +{reward.value}");
                        }
                    }
                    break;
                    
                // ═══ 负面类（削弱玩家）═══
                case RewardType.HealthLoss:
                    if (turretHealth != null && turretHealth.CurrentHullHP > 1)
                    {
                        int loss = Mathf.RoundToInt(reward.value);
                        int actualLoss = turretHealth.TakeDirectDamage(loss, 1);
        
                        if (showDebugInfo)
                        {
                            GameLogger.Log($"[TacticalDropManager] 血量扣除: -{actualLoss}, 剩余: {turretHealth.CurrentHullHP}");
                        }
                    }
                    break;
                    
                case RewardType.ShieldLoss:
                    if (shieldController != null && shieldController.CurrentShieldHP > 0)
                    {
                        int loss = Mathf.RoundToInt(reward.value);
                        if (shieldController.CurrentShieldHP - loss < 1)
                        {
                            loss = shieldController.CurrentShieldHP - 1;
                        }
                        if (loss > 0)
                        {
                            shieldController.TakeDamage(loss);
                        }
        
                        if (showDebugInfo)
                        {
                            GameLogger.Log($"[TacticalDropManager] 护盾扣除: -{loss}, 剩余: {shieldController.CurrentShieldHP}");
                        }
                    }
                    break;
                    
                case RewardType.BaseDamageLossPercent:
                    if (laserController != null)
                    {
                        laserController.AddDamagePercent(-reward.value);
                    }
                    break;
                    
                case RewardType.CritRateLossPercent:
                    if (laserController != null)
                    {
                        laserController.AddCritRateBonus(-reward.value);
                    }
                    break;
                    
                case RewardType.LaserWidthLossPercent:  
                    if (laserController != null)
                    {
                        float minPercent = rewardConfig != null ? rewardConfig.laserWidthMinPercent : 0.8f;
                        laserController.AddWidthPercent(-reward.value, minPercent);
                    }
                    break;
                    
                case RewardType.LaserLengthLossPercent:  // 改名
                    if (laserController != null)
                    {
                        float minPercent = rewardConfig != null ? rewardConfig.laserLengthMinPercent : 0.8f;
                        laserController.AddLengthPercent(-reward.value, minPercent);
                    }
                    break;
                    
                case RewardType.MaxHealthLoss:
                    if (turretHealth != null)
                    {
                        int newMax = turretHealth.MaxHullHP - Mathf.RoundToInt(reward.value);
                        if (newMax > 100) // 最低100血量
                        {
                            turretHealth.SetMaxHullHP(newMax);
                        }
                    }
                    break;
                    
                // ═══ 负面类（增强怪物，下一波生效）═══
                case RewardType.NextWaveSpeedBuff:
                    AddNextWaveSpeedBuff(reward.value);
                    break;
                    
                case RewardType.NextWaveHealthBuff:
                    AddNextWaveHealthBuff(reward.value);
                    break;
                    
                case RewardType.NextWaveDamageBuff:
                    AddNextWaveDamageBuff(reward.value);
                    break;
                    
                case RewardType.Nothing:
                    // 无操作
                    break;
            }
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[TacticalDropManager] 应用奖励: {reward.type} = {reward.value}");
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 结束空投
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 结束空投阶段，开始下一波
        /// </summary>
        private void EndDropPhase()
        {
            if (!isDropPhase) return;
            
            isDropPhase = false;
            
            // 清理残留宝箱
            ClearAllCrates();
            
            if (showDebugInfo)
            {
                GameLogger.Log("[TacticalDropManager] 空投阶段结束，开始下一波");
                if (nextWaveBuff.HasBuff)
                {
                    GameLogger.Log($"[TacticalDropManager] 下波增强待生效: Speed={nextWaveBuff.speedMultiplier:P0}, HP={nextWaveBuff.healthMultiplier:P0}, DMG={nextWaveBuff.damageMultiplier:P0}");
                }
            }
            
            // 通知 WaveManager 开始下一波
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.StartNextWave();
            }
        }
        
        /// <summary>
        /// 清理所有宝箱
        /// </summary>
        private void ClearAllCrates()
        {
            foreach (var crate in activeCrates)
            {
                if (crate != null)
                {
                    Destroy(crate.gameObject);
                }
            }
            activeCrates.Clear();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [ContextMenu("Test: Start Drop Phase")]
        private void TestStartDropPhase()
        {
            OnWaveComplete(1, 12);
        }
        
        [ContextMenu("Test: Skip Drop Phase")]
        private void TestSkipDropPhase()
        {
            EndDropPhase();
        }
        
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 250, 300, 240, 180));
            GUILayout.Label("=== Tactical Drop v2.1 ===");
            GUILayout.Label($"IsDropPhase: {isDropPhase}");
            GUILayout.Label($"ActiveCrates: {activeCrates.Count}");
            GUILayout.Label($"GachaBadLuck: {gachaBadLuckCounter}");
            GUILayout.Label("--- Next Wave Buff ---");
            GUILayout.Label($"Speed: {nextWaveBuff.speedMultiplier:P0}");
            GUILayout.Label($"HP: {nextWaveBuff.healthMultiplier:P0}");
            GUILayout.Label($"DMG: {nextWaveBuff.damageMultiplier:P0}");
            GUILayout.EndArea();
        }
#endif
    }
}