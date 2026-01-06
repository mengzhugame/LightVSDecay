// ============================================================
// TacticalDropManager.cs
// 文件位置: Assets/Scripts/Logic/TacticalDrop/TacticalDropManager.cs
// 用途：战术空投宝箱系统管理器
// ============================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using LightVsDecay.Core;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Player;
using LightVsDecay.UI.FloatingText;

namespace LightVsDecay.Logic.TacticalDrop
{
    /// <summary>
    /// 战术空投系统管理器
    /// 职责：
    /// - 监听波次完成事件
    /// - 生成并管理3个宝箱
    /// - 处理宝箱被击破时的奖励
    /// - 控制奖励飘字动画
    /// - 通知 WaveManager 开始下一波
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
        [SerializeField] private CrateRewardConfig rewardConfig;
        
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
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public bool IsDropPhase => isDropPhase;
        
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
        }
        
        private void OnDisable()
        {
            GameEvents.OnWaveComplete -= OnWaveComplete;
            GameEvents.OnGameStart -= OnGameStart;
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
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 游戏开始时重置
        /// </summary>
        private void OnGameStart()
        {
            gachaBadLuckCounter = 0;
            ClearAllCrates();
            CacheComponents();
            
            if (showDebugInfo)
            {
                Debug.Log("[TacticalDropManager] 游戏开始，重置状态");
            }
        }
        
        /// <summary>
        /// 波次完成时触发空投
        /// </summary>
        private void OnWaveComplete(int completedWave, int totalWaves)
        {
            // Boss 波不触发空投（如果需要的话可以在这里加条件）
            if (WaveManager.Instance != null && WaveManager.Instance.IsBossWave)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[TacticalDropManager] Boss 波，跳过空投");
                }
                return;
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[TacticalDropManager] 波次 {completedWave} 完成，开始空投！");
            }
            
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
            isDropPhase = true;
            
            // 清理旧的宝箱（以防万一）
            ClearAllCrates();
            
            // 生成3个宝箱
            SpawnCrates();
            
            // 等待所有宝箱落地
            float dropDuration = rewardConfig != null ? rewardConfig.dropDuration : 0.8f;
            yield return new WaitForSeconds(dropDuration + 0.1f);
            
            if (showDebugInfo)
            {
                Debug.Log("[TacticalDropManager] 所有宝箱已落地，等待玩家选择...");
            }
        }
        
        /// <summary>
        /// 生成3个宝箱
        /// </summary>
        private void SpawnCrates()
        {
            int crateHP = rewardConfig != null ? rewardConfig.crateHP : 500;
            float dropDuration = rewardConfig != null ? rewardConfig.dropDuration : 0.8f;
            
            // 左：蓝色补给箱
            SpawnCrate(supplyCratePrefab, CrateType.Supply, leftCrateX, crateHP, dropDuration);
            
            // 中：金色问号箱
            SpawnCrate(gachaCratePrefab, CrateType.Gacha, centerCrateX, crateHP, dropDuration);
            
            // 右：红色契约箱
            SpawnCrate(dealCratePrefab, CrateType.Deal, rightCrateX, crateHP, dropDuration);
        }
        
        /// <summary>
        /// 生成单个宝箱
        /// </summary>
        private void SpawnCrate(GameObject prefab, CrateType type, float xPos, int hp, float dropDuration)
        {
            if (prefab == null)
            {
                Debug.LogError($"[TacticalDropManager] 宝箱预制体为空: {type}");
                return;
            }
            
            Vector3 spawnPos = new Vector3(xPos, spawnStartY, 0f);
            GameObject crateObj = Instantiate(prefab, spawnPos, Quaternion.identity);
            
            TacticalCrate crate = crateObj.GetComponent<TacticalCrate>();
            if (crate == null)
            {
                Debug.LogError($"[TacticalDropManager] 预制体缺少 TacticalCrate 组件: {type}");
                Destroy(crateObj);
                return;
            }
            
            // 初始化
            crate.Initialize(type, hp);
            crate.PlayDropAnimation(spawnStartY, landingY, dropDuration);
            
            activeCrates.Add(crate);
            
            if (showDebugInfo)
            {
                Debug.Log($"[TacticalDropManager] 生成宝箱: {type} @ ({xPos}, {landingY})");
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
                Debug.Log($"[TacticalDropManager] 宝箱被击破: {destroyedCrate.CrateType}");
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
            
            RewardEntry reward = rewardConfig.GetRandomSupplyReward();
            if (reward == null)
            {
                Debug.LogWarning("[TacticalDropManager] 补给箱奖励池为空！");
                EndDropPhase();
                return;
            }
            
            // 应用奖励
            ApplyReward(reward);
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
            
            var (resultType, reward, mockText) = rewardConfig.GetGachaResult(forceEpic);
            
            if (showDebugInfo)
            {
                Debug.Log($"[TacticalDropManager] 金箱结果: {resultType}, 保底触发={forceEpic}");
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
            
            // 处理结果
            if (resultType != GachaResultType.Nothing && reward != null)
            {
                ApplyReward(reward);
            }
    
            // TODO: 后续添加飘字系统
    
            // 结束空投阶段
            EndDropPhase();
        }
        
        /// <summary>
        /// 处理契约箱奖励
        /// </summary>
        private void ProcessDealReward(TacticalCrate crate)
        {
            if (rewardConfig == null) return;
    
            DealEntry deal = rewardConfig.GetRandomDeal();
            if (deal == null)
            {
                Debug.LogWarning("[TacticalDropManager] 契约箱交易池为空！");
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
    
            // TODO: 后续添加飘字系统
    
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
                    
                case RewardType.CritRatePercent:
                    if (laserController != null)
                    {
                        laserController.AddCritRateBonus(reward.value);
                    }
                    break;
                    
                case RewardType.LaserWidthFlat:
                    if (laserController != null)
                    {
                        laserController.AddWidthPercent(reward.value);
                    }
                    break;
                    
                case RewardType.LaserLengthFlat:
                    // TODO: 添加激光长度修改接口
                    if (showDebugInfo)
                    {
                        Debug.Log($"[TacticalDropManager] 激光长度 +{reward.value} (待实现)");
                    }
                    break;
                    
                // ═══ 负面类 ═══
                case RewardType.HealthLoss:
                    if (turretHealth != null)
                    {
                        int loss = Mathf.RoundToInt(reward.value);
                        // 保底1血
                        if (turretHealth.CurrentHullHP - loss < 1)
                        {
                            loss = turretHealth.CurrentHullHP - 1;
                        }
                        if (loss > 0)
                        {
                            turretHealth.TakeBossDamage(loss);
                        }
                    }
                    break;
                    
                case RewardType.ShieldLoss:
                    if (shieldController != null)
                    {
                        shieldController.TakeDamage(Mathf.RoundToInt(reward.value));
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
                    
                case RewardType.LaserWidthLossFlat:
                    if (laserController != null)
                    {
                        // 检查下限保护
                        float minWidth = GameConstants.LASER_DEFAULT_WIDTH * 
                            (rewardConfig != null ? rewardConfig.laserWidthMinPercent : 0.8f);
                        // TODO: 实现宽度下限检查
                        laserController.AddWidthPercent(-reward.value);
                    }
                    break;
                    
                case RewardType.LaserLengthLossFlat:
                    // TODO: 添加激光长度修改接口
                    if (showDebugInfo)
                    {
                        Debug.Log($"[TacticalDropManager] 激光长度 -{reward.value} (待实现)");
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
                    
                case RewardType.Nothing:
                    // 无操作
                    break;
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[TacticalDropManager] 应用奖励: {reward.type} = {reward.value}");
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
                Debug.Log("[TacticalDropManager] 空投阶段结束，开始下一波");
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
            
            GUILayout.BeginArea(new Rect(Screen.width - 250, 300, 240, 150));
            GUILayout.Label("=== Tactical Drop ===");
            GUILayout.Label($"IsDropPhase: {isDropPhase}");
            GUILayout.Label($"ActiveCrates: {activeCrates.Count}");
            GUILayout.Label($"GachaBadLuck: {gachaBadLuckCounter}");
            GUILayout.EndArea();
        }
#endif
    }
}
