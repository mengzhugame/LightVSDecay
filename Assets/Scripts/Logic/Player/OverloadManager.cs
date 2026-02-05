// ============================================================
// OverloadManager.cs
// 文件位置: Assets/Scripts/Logic/Player/OverloadManager.cs
// 用途：超载模式（大招）管理器
// ============================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Logic.Enemy;
using LightVsDecay.Logic.Statistics;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 超载模式状态
    /// </summary>
    public enum OverloadState
    {
        Charging,   // 充能中
        Ready,      // 就绪（可释放）
        Active      // 激活中
    }
    
    /// <summary>
    /// 超载模式（大招）管理器
    /// 功能：60秒充能 → 5秒激活（无敌+伤害×2+宽度×2+自动瞄准）
    /// </summary>
    public class OverloadManager : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public static OverloadManager Instance { get; private set; }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("充能设置")]
        [Tooltip("充能所需时间（秒）")]
        [SerializeField] private float chargeTime = 60f;
        
        [Header("激活设置")]
        [Tooltip("大招持续时间（秒）")]
        [SerializeField] private float activeDuration = 5f;
        
        [Tooltip("伤害倍率")]
        [SerializeField] private float damageMultiplier = 2f;
        
        [Tooltip("宽度倍率")]
        [SerializeField] private float widthMultiplier = 2f;
        
        [Header("自动瞄准设置")]
        [Tooltip("自动瞄准转向速度（度/秒）")]
        [SerializeField] private float autoAimTurnSpeed = 180f;
        
        [Tooltip("敌人检测范围")]
        [SerializeField] private float detectionRange = 20f;
        
        [Tooltip("无敌人时的默认角度（0=朝上）")]
        [SerializeField] private float defaultAngle = 0f;
        
        [Header("UI 引用")]
        [Tooltip("大招按钮")]
        [SerializeField] private Button skillButton;
        
        [Tooltip("充能进度填充图")]
        [SerializeField] private Image fillImage;
        
        [Tooltip("充满特效粒子")]
        [SerializeField] private ParticleSystem readyVFX;
        
        [Header("UI 动画")]
        [Tooltip("进度条平滑速度")]
        [SerializeField] private float fillSmoothSpeed = 5f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private OverloadState currentState = OverloadState.Charging;
        private float currentCharge = 0f;           // 当前充能值
        private float activeTimer = 0f;             // 激活剩余时间
        private float displayedFillAmount = 0f;     // UI 显示的进度（平滑）
        
        // 组件缓存
        private TurretController turretController;
        private LaserController laserController;
        private LayerMask enemyLayerMask;
        
        // 自动瞄准缓存
        private Collider2D[] nearbyEnemies = new Collider2D[50];
        private float currentAutoAimAngle = 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前状态</summary>
        public OverloadState CurrentState => currentState;
        
        /// <summary>是否激活中</summary>
        public bool IsActive => currentState == OverloadState.Active;
        
        /// <summary>是否就绪（可释放）</summary>
        public bool IsReady => currentState == OverloadState.Ready;
        
        /// <summary>是否充能中</summary>
        public bool IsCharging => currentState == OverloadState.Charging;
        
        /// <summary>充能进度 (0-1)</summary>
        public float ChargeProgress => Mathf.Clamp01(currentCharge / chargeTime);
        
        /// <summary>激活剩余时间</summary>
        public float ActiveTimeRemaining => activeTimer;
        
        /// <summary>当前伤害倍率（未激活时为1）</summary>
        public float CurrentDamageMultiplier => IsActive ? damageMultiplier : 1f;
        
        /// <summary>当前宽度倍率（未激活时为1）</summary>
        public float CurrentWidthMultiplier => IsActive ? widthMultiplier : 1f;
        
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
            
            // 缓存 Layer
            enemyLayerMask = LayerMask.GetMask(GameConstants.ENEMY_LAYER, GameConstants.BOUNCING_ENEMY_LAYER);
        }
        
        private void Start()
        {
            CacheComponents();
            SetupUI();
            ResetToCharging();
        }
        
        private void OnEnable()
        {
            // 订阅游戏事件
            GameEvents.OnGameStart += OnGameStart;
            GameEvents.OnGamePaused += OnGamePaused;
            GameEvents.OnGameResumed += OnGameResumed;
            GameEvents.OnGameVictory += OnGameEnd;
            GameEvents.OnGameDefeat += OnGameEnd;
        }
        
        private void OnDisable()
        {
            GameEvents.OnGameStart -= OnGameStart;
            GameEvents.OnGamePaused -= OnGamePaused;
            GameEvents.OnGameResumed -= OnGameResumed;
            GameEvents.OnGameVictory -= OnGameEnd;
            GameEvents.OnGameDefeat -= OnGameEnd;
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        private void Update()
        {
            // 暂停时不更新
            if (Time.timeScale == 0f) return;
            
            switch (currentState)
            {
                case OverloadState.Charging:
                    UpdateCharging();
                    break;
                    
                case OverloadState.Ready:
                    // Ready 状态等待玩家点击，无需特殊更新
                    break;
                    
                case OverloadState.Active:
                    UpdateActive();
                    break;
            }
            
            // 更新 UI（平滑动画）
            UpdateUI();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void CacheComponents()
        {
            if (turretController == null)
            {
                turretController = FindObjectOfType<TurretController>();
            }
            
            if (laserController == null)
            {
                laserController = FindObjectOfType<LaserController>();
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[OverloadManager] 组件缓存: TurretController={turretController != null}, LaserController={laserController != null}");
            }
        }
        
        private void SetupUI()
        {
            // 绑定按钮点击
            if (skillButton != null)
            {
                skillButton.onClick.AddListener(OnSkillButtonClicked);
            }
            
            // 初始化进度条
            if (fillImage != null)
            {
                fillImage.fillAmount = 0f;
                displayedFillAmount = 0f;
            }
            
            // 隐藏就绪特效
            if (readyVFX != null)
            {
                readyVFX.Stop();
                readyVFX.gameObject.SetActive(false);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态更新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateCharging()
        {
            // 累加充能
            currentCharge += Time.deltaTime;
            
            // 检查是否充满
            if (currentCharge >= chargeTime)
            {
                currentCharge = chargeTime;
                ChangeState(OverloadState.Ready);
            }
        }
        
        private void UpdateActive()
        {
            // 更新激活计时器
            activeTimer -= Time.deltaTime;
            
            // 自动瞄准
            UpdateAutoAim();
            
            // 检查是否结束
            if (activeTimer <= 0f)
            {
                DeactivateOverload();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 自动瞄准
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateAutoAim()
        {
            if (turretController == null) return;
            
            // 查找最近敌人
            Vector3 turretPos = turretController.transform.position;
            Transform nearestEnemy = FindNearestEnemy(turretPos);
            
            // 计算目标角度
            float targetAngle;
            if (nearestEnemy != null)
            {
                Vector3 direction = nearestEnemy.position - turretPos;
                // 计算角度（Unity 2D 中，0度朝右，需要转换为朝上为0度）
                targetAngle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
                // 取反使得角度符合 TurretController 的约定（向右为负，向左为正）
                targetAngle = -targetAngle;
            }
            else
            {
                // 无敌人，朝向默认方向
                targetAngle = defaultAngle;
            }
            
            // 限制在旋转范围内
            targetAngle = Mathf.Clamp(targetAngle, -90f, 90f);
            
            // 平滑转向
            float maxDelta = autoAimTurnSpeed * Time.deltaTime;
            currentAutoAimAngle = Mathf.MoveTowardsAngle(currentAutoAimAngle, targetAngle, maxDelta);
            
            // 应用角度
            turretController.SetTargetAngle(currentAutoAimAngle);
            
            if (showDebugInfo)
            {
                string enemyInfo = nearestEnemy != null ? nearestEnemy.name : "None";
                Debug.Log($"[OverloadManager] 自动瞄准: 目标={enemyInfo}, 角度={currentAutoAimAngle:F1}°");
            }
        }
        
        private Transform FindNearestEnemy(Vector3 position)
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(position, detectionRange, nearbyEnemies, enemyLayerMask);
            
            Transform nearest = null;
            float nearestDist = float.MaxValue;
            
            for (int i = 0; i < hitCount; i++)
            {
                var collider = nearbyEnemies[i];
                if (collider == null) continue;
                
                // 获取 EnemyBlob 组件验证是否有效
                var enemy = collider.GetComponent<EnemyBlob>();
                if (enemy == null || enemy.IsDead) continue;
                
                float dist = Vector3.Distance(position, collider.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = collider.transform;
                }
            }
            
            return nearest;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态切换
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ChangeState(OverloadState newState)
        {
            if (currentState == newState) return;
            
            OverloadState oldState = currentState;
            currentState = newState;
            
            if (showDebugInfo)
            {
                Debug.Log($"[OverloadManager] 状态切换: {oldState} → {newState}");
            }
            
            // 进入新状态的处理
            switch (newState)
            {
                case OverloadState.Charging:
                    OnEnterCharging();
                    break;
                    
                case OverloadState.Ready:
                    OnEnterReady();
                    break;
                    
                case OverloadState.Active:
                    OnEnterActive();
                    break;
            }
            
            // 触发事件
            GameEvents.TriggerOverloadStateChanged(newState);
        }
        
        private void OnEnterCharging()
        {
            // 隐藏就绪特效
            if (readyVFX != null)
            {
                readyVFX.Stop();
                readyVFX.gameObject.SetActive(false);
            }
        }
        
        private void OnEnterReady()
        {
            // 显示就绪特效
            if (readyVFX != null)
            {
                readyVFX.gameObject.SetActive(true);
                readyVFX.Play();
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[OverloadManager] ⚡ 大招已就绪！");
            }
        }
        
        private void OnEnterActive()
        {
            // 隐藏就绪特效
            if (readyVFX != null)
            {
                readyVFX.Stop();
                readyVFX.gameObject.SetActive(false);
            }
            
            // 设置激活计时器
            activeTimer = activeDuration;
            
            // 记录当前角度作为自动瞄准起始角度
            if (turretController != null)
            {
                currentAutoAimAngle = turretController.GetCurrentAngle();
            }
            
            // 通知 TurretController 进入大招模式
            if (turretController != null)
            {
                turretController.SetUltActive(true);
            }
            
            // 通知 LaserController 应用大招倍率
            if (laserController != null)
            {
                laserController.SetOverloadActive(true, damageMultiplier, widthMultiplier);
            }
            
            // 触发激活事件
            GameEvents.TriggerOverloadActivated();
            // 【新增】数据埋点
            if (BattleStatistics.Instance != null)
            {
                BattleStatistics.Instance.RecordOverloadActivation();
            }
            if (showDebugInfo)
            {
                Debug.Log($"[OverloadManager] 🔥 大招激活！持续 {activeDuration} 秒");
            }
        }
        
        private void DeactivateOverload()
        {
            // 通知 TurretController 退出大招模式
            if (turretController != null)
            {
                turretController.SetUltActive(false);
            }
            
            // 通知 LaserController 移除大招倍率
            if (laserController != null)
            {
                laserController.SetOverloadActive(false, 1f, 1f);
            }
            
            // 触发结束事件
            GameEvents.TriggerOverloadDeactivated();
            
            // 重置充能
            ResetToCharging();
            
            if (showDebugInfo)
            {
                Debug.Log("[OverloadManager] 大招结束，开始重新充能");
            }
        }
        
        private void ResetToCharging()
        {
            currentCharge = 0f;
            activeTimer = 0f;
            ChangeState(OverloadState.Charging);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // UI 更新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateUI()
        {
            if (fillImage == null) return;
            
            // 目标进度
            float targetFill;
            
            switch (currentState)
            {
                case OverloadState.Charging:
                    targetFill = ChargeProgress;
                    break;
                    
                case OverloadState.Ready:
                    targetFill = 1f;
                    break;
                    
                case OverloadState.Active:
                    // 激活期间显示剩余时间比例
                    targetFill = activeTimer / activeDuration;
                    break;
                    
                default:
                    targetFill = 0f;
                    break;
            }
            
            // 平滑插值
            displayedFillAmount = Mathf.Lerp(displayedFillAmount, targetFill, Time.deltaTime * fillSmoothSpeed);
            
            // 应用到 UI
            fillImage.fillAmount = displayedFillAmount;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 尝试激活大招（由按钮调用）
        /// </summary>
        public void TryActivate()
        {
            if (currentState != OverloadState.Ready)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[OverloadManager] 无法激活：当前状态为 {currentState}");
                }
                return;
            }
            
            ChangeState(OverloadState.Active);
        }
        
        /// <summary>
        /// 按钮点击处理
        /// </summary>
        private void OnSkillButtonClicked()
        {
            TryActivate();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnGameStart()
        {
            // 重新缓存组件（场景可能重新加载）
            CacheComponents();
            ResetToCharging();
            
            if (showDebugInfo)
            {
                Debug.Log("[OverloadManager] 游戏开始，重置充能");
            }
        }
        
        private void OnGamePaused()
        {
            // 暂停时 Update 中的 Time.timeScale 检查会自动停止更新
            if (showDebugInfo)
            {
                Debug.Log("[OverloadManager] 游戏暂停，充能暂停");
            }
        }
        
        private void OnGameResumed()
        {
            if (showDebugInfo)
            {
                Debug.Log("[OverloadManager] 游戏恢复，充能继续");
            }
        }
        
        private void OnGameEnd()
        {
            // 游戏结束时强制结束大招
            if (currentState == OverloadState.Active)
            {
                DeactivateOverload();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [ContextMenu("Debug: Force Ready")]
        private void DebugForceReady()
        {
            currentCharge = chargeTime;
            ChangeState(OverloadState.Ready);
        }
        
        [ContextMenu("Debug: Force Activate")]
        private void DebugForceActivate()
        {
            currentCharge = chargeTime;
            ChangeState(OverloadState.Ready);
            TryActivate();
        }
        
        [ContextMenu("Debug: Reset")]
        private void DebugReset()
        {
            if (currentState == OverloadState.Active)
            {
                DeactivateOverload();
            }
            else
            {
                ResetToCharging();
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // 绘制检测范围
            if (turretController != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                Gizmos.DrawWireSphere(turretController.transform.position, detectionRange);
            }
        }
#endif
    }
}