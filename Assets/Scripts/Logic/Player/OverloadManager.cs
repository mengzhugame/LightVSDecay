// ============================================================
// OverloadManager.cs
// 文件位置: Assets/Scripts/Logic/Player/OverloadManager.cs
// 用途：超载模式（大招）管理器
// ============================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightVsDecay.Audio;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Logic.Boss;
using LightVsDecay.Logic.Enemy;
using LightVsDecay.Logic.Statistics;
using LightVsDecay.UI;

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
        [SerializeField] private float lengthBonus = 5f;
        
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

        [Header("首次提示气泡")]
        [SerializeField] private GameObject readyBubbleRoot;
        [SerializeField] private TextMeshProUGUI readyBubbleText;
        [SerializeField] private string readyBubbleMessage = "大招已就绪，点击立即释放！";
        
        [Header("UI 动画")]
        [Tooltip("进度条平滑速度")]
        [SerializeField] private float fillSmoothSpeed = 5f;

        [Tooltip("按钮点击时压缩到的最小缩放（0.85 = 压缩15%）")]
        [SerializeField] private float btnPressScale = 0.85f;

        [Tooltip("按压下去的时长（秒）")]
        [SerializeField] private float btnPressDuration = 0.08f;

        [Tooltip("弹回原始大小的时长（秒）")]
        [SerializeField] private float btnReleaseDuration = 0.18f;
        
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
        private RectTransform skillButtonRect;
        private Coroutine btnAnimCoroutine;
        private SettingsPanel[] settingsPanels;
        
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
            if (Time.timeScale == 0f)
            {
                RefreshReadyVFXVisibility();
                return;
            }
            
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
            RefreshReadyVFXVisibility();
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
                GameLogger.Log($"[OverloadManager] 组件缓存: TurretController={turretController != null}, LaserController={laserController != null}");
            }

            settingsPanels = FindObjectsOfType<SettingsPanel>(true);
        }
        
        private void SetupUI()
        {
            // 绑定按钮点击
            if (skillButton != null)
            {
                skillButton.onClick.RemoveListener(OnSkillButtonClicked);
                skillButton.onClick.AddListener(OnSkillButtonClicked);
                skillButtonRect = skillButton.GetComponent<RectTransform>();
            }

            ResolveReadyVFXReference();
            
            // 初始化进度条
            if (fillImage != null)
            {
                fillImage.fillAmount = 0f;
                displayedFillAmount = 0f;
            }
            
            // 隐藏就绪特效
            HideReadyVFX();
        }

        private void ResolveReadyVFXReference()
        {
            if (readyVFX != null || skillButtonRect == null)
            {
                return;
            }

            ParticleSystem[] particleSystems = skillButtonRect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (particleSystem == null)
                {
                    continue;
                }

                if (particleSystem.gameObject.name.Contains("DaZhaoChongNeng"))
                {
                    readyVFX = particleSystem;
                    break;
                }
            }

            if (showDebugInfo && readyVFX == null)
            {
                GameLogger.LogWarning("[OverloadManager] readyVFX was not found under SkillButton.");
            }
        }

        private void HideReadyVFX()
        {
            if (readyVFX == null)
            {
                return;
            }

            readyVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            readyVFX.Clear(true);
            readyVFX.gameObject.SetActive(false);
        }

        private void ShowReadyBubble()
        {
            // 注意：不在此处设置 hasSeenOverloadTutorial，
            // 该 flag 由 BattleTutorialDirector 在玩家点击大招后统一写入。
            if (readyBubbleRoot == null || ProgressManager.Instance == null || ProgressManager.Instance.Meta.hasSeenOverloadTutorial)
            {
                return;
            }

            if (readyBubbleText != null)
            {
                readyBubbleText.text = readyBubbleMessage;
            }

            readyBubbleRoot.SetActive(true);
        }

        private void HideReadyBubble()
        {
            if (readyBubbleRoot != null)
            {
                readyBubbleRoot.SetActive(false);
            }
        }

        private void ShowReadyVFX()
        {
            ResolveReadyVFXReference();
            if (readyVFX == null || !ShouldShowReadyVFX())
            {
                return;
            }

            readyVFX.gameObject.SetActive(true);
            readyVFX.Clear(true);
            readyVFX.Simulate(0f, true, true);
            readyVFX.Play(true);
        }

        private void RefreshReadyVFXVisibility()
        {
            if (ShouldShowReadyVFX())
            {
                if (readyVFX == null || !readyVFX.gameObject.activeSelf)
                {
                    ShowReadyVFX();
                }
                return;
            }

            HideReadyVFX();
        }

        private bool ShouldShowReadyVFX()
        {
            return currentState == OverloadState.Ready && !IsBlockingPanelVisible();
        }

        private bool IsBlockingPanelVisible()
        {
            if (UIManager.Instance != null && UIManager.Instance.IsAnyPanelActive())
            {
                return true;
            }

            if (settingsPanels == null || settingsPanels.Length == 0)
            {
                settingsPanels = FindObjectsOfType<SettingsPanel>(true);
            }

            if (settingsPanels == null)
            {
                return false;
            }

            foreach (SettingsPanel panel in settingsPanels)
            {
                if (panel != null && panel.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
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
            Transform nearestEnemy = FindNearestLockTarget(turretPos);
            
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
                GameLogger.Log($"[OverloadManager] 自动瞄准: 目标={enemyInfo}, 角度={currentAutoAimAngle:F1}°");
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
                
                // 获取 EnemyBlob 组件验证是否有效；跳过熔浆液等静止地形障碍
                var enemy = collider.GetComponent<EnemyBlob>();
                if (enemy == null || enemy.IsDead || enemy.IsStationary) continue;
                
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
        
        private Transform FindNearestLockTarget(Vector3 position)
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(position, detectionRange, nearbyEnemies, enemyLayerMask);

            Transform nearest = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var collider = nearbyEnemies[i];
                if (collider == null) continue;

                Transform candidate = null;

                var enemy = collider.GetComponentInParent<EnemyBlob>();
                if (enemy != null)
                {
                    if (enemy.IsDead || enemy.IsStationary) continue;
                    candidate = enemy.transform;
                }
                else
                {
                    var boss = collider.GetComponentInParent<BaseBossController>();
                    if (boss == null) continue;

                    var bossHealth = boss.GetComponent<BossHealth>();
                    if (bossHealth != null && bossHealth.IsDead) continue;

                    candidate = boss.transform;
                }

                float dist = Vector3.Distance(position, candidate.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private void ChangeState(OverloadState newState)
        {
            if (currentState == newState) return;
            
            OverloadState oldState = currentState;
            currentState = newState;
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[OverloadManager] 状态切换: {oldState} → {newState}");
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
            HideReadyVFX();
        }
        
        private void OnEnterReady()
        {
            // 显示就绪特效
            ShowReadyVFX();
            ShowReadyBubble();
            
            if (showDebugInfo)
            {
                GameLogger.Log("[OverloadManager] ⚡ 大招已就绪！");
            }
        }
        
        private void OnEnterActive()
        {
            // 隐藏就绪特效
            HideReadyVFX();
            
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
                laserController.SetOverloadActive(true, damageMultiplier, widthMultiplier, lengthBonus);
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
                GameLogger.Log($"[OverloadManager] 🔥 大招激活！持续 {activeDuration} 秒");
            }
        }
        
        private void DeactivateOverload()
        {
            HideReadyBubble();
            // 通知 TurretController 退出大招模式
            if (turretController != null)
            {
                turretController.SetUltActive(false);
            }
            
            // 通知 LaserController 移除大招倍率
            if (laserController != null)
            {
                laserController.SetOverloadActive(false, 1f, 1f, 0f);
            }
            
            // 触发结束事件
            GameEvents.TriggerOverloadDeactivated();
            
            // 重置充能
            ResetToCharging();
            
            if (showDebugInfo)
            {
                GameLogger.Log("[OverloadManager] 大招结束，开始重新充能");
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
                    GameLogger.Log($"[OverloadManager] 无法激活：当前状态为 {currentState}");
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
            AudioManager.Instance?.PlayButtonClick();

            if (skillButtonRect != null)
            {
                if (btnAnimCoroutine != null) StopCoroutine(btnAnimCoroutine);
                btnAnimCoroutine = StartCoroutine(ButtonPressAnim());
            }

            TryActivate();
        }

        private IEnumerator ButtonPressAnim()
        {
            // 压下：1.0 → btnPressScale
            float elapsed = 0f;
            while (elapsed < btnPressDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / btnPressDuration);
                skillButtonRect.localScale = Vector3.one * Mathf.Lerp(1f, btnPressScale, t);
                yield return null;
            }
            skillButtonRect.localScale = Vector3.one * btnPressScale;

            // 弹回：btnPressScale → 1.0（EaseOutBack 弹性）
            elapsed = 0f;
            while (elapsed < btnReleaseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / btnReleaseDuration);
                float scale = Mathf.LerpUnclamped(btnPressScale, 1f, EaseOutBack(t));
                skillButtonRect.localScale = Vector3.one * scale;
                yield return null;
            }
            skillButtonRect.localScale = Vector3.one;
            btnAnimCoroutine = null;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnGameStart()
        {
            // 重新缓存组件（场景可能重新加载）
            CacheComponents();
            SetupUI();

            if (btnAnimCoroutine != null)
            {
                StopCoroutine(btnAnimCoroutine);
                btnAnimCoroutine = null;
            }

            if (skillButtonRect != null)
            {
                skillButtonRect.localScale = Vector3.one;
            }

            Array.Clear(nearbyEnemies, 0, nearbyEnemies.Length);

            if (turretController != null)
            {
                turretController.SetUltActive(false);
                currentAutoAimAngle = turretController.GetCurrentAngle();
            }
            else
            {
                currentAutoAimAngle = defaultAngle;
            }

            if (laserController != null)
            {
                laserController.SetOverloadActive(false, 1f, 1f, 0f);
            }

            HideReadyVFX();
            ResetToCharging();
            RefreshReadyVFXVisibility();
            
            if (showDebugInfo)
            {
                GameLogger.Log("[OverloadManager] 游戏开始，重置充能");
            }
        }
        
        private void OnGamePaused()
        {
            // 暂停时 Update 中的 Time.timeScale 检查会自动停止更新
            RefreshReadyVFXVisibility();
            if (showDebugInfo)
            {
                GameLogger.Log("[OverloadManager] 游戏暂停，充能暂停");
            }
        }
        
        private void OnGameResumed()
        {
            RefreshReadyVFXVisibility();
            if (showDebugInfo)
            {
                GameLogger.Log("[OverloadManager] 游戏恢复，充能继续");
            }
        }
        
        private void OnGameEnd()
        {
            // 游戏结束时强制结束大招
            if (currentState == OverloadState.Active)
            {
                DeactivateOverload();
            }

            if (turretController != null)
            {
                turretController.SetUltActive(false);
            }

            if (laserController != null)
            {
                laserController.SetOverloadActive(false, 1f, 1f, 0f);
            }

            RefreshReadyVFXVisibility();
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
