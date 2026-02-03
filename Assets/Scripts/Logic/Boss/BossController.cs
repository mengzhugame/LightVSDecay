// ============================================================
// BossController.cs
// 文件位置: Assets/Scripts/Logic/Boss/BossController.cs
// 用途：Boss 行为状态机主控制器 (The Corruptor - 污染之核)
// 【重构】分离 Charge(快招) 和 Press(慢招) 两个主动技能
// 【新增】冰冻状态 + 霸体系统 + 控制递减机制
// ============================================================

using UnityEngine;
using System.Collections;
using LightVsDecay.Audio;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Enemy;
using LightVsDecay.Logic.Player;
using LightVsDecay.Logic.Statistics;
using LightVsDecay.UI.FloatingText;
#if DOTWEEN
using DG.Tweening;
#endif

namespace LightVsDecay.Logic.Boss
{
    /// <summary>
    /// Boss 状态飘字类型
    /// </summary>
    public enum BossStatusTextType
    {
        Unstoppable,    // 霸体
        Frozen,         // 冰冻
        Enraged,        // 狂暴
        Interrupted,    // 被打断
        Countered,      // 被反击
        Overload,       // 过载
        Exhausted       // 疲劳
    }
    /// <summary>
    /// Boss 行为状态
    /// </summary>
    public enum BossState
    {
        Spawn,      // 入场
        Idle,       // 待机/游走
        Summon,     // 召唤爪牙（被动技能，可打断Idle）
        Charge,     // 野蛮冲撞（主动技能A - 快招/反应测试）
        Press,      // 重力碾压（主动技能B - 慢招/物理角力）
        Stun,       // 僵直/虚弱
        Frozen      // 【新增】冰冻状态
    }
    
    /// <summary>
    /// Boss 控制器 - 污染之核 (The Corruptor)
    /// 状态机循环：Spawn -> Idle -> (Summon可打断) -> Charge/Press(50/50) -> Stun -> Idle ...
    /// 【新增】支持冰冻状态和霸体机制
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BossHealth))]
    public class BossController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("Boss行为配置")]
        [SerializeField] private BossConfig config;
        
        [Header("组件引用")]
        [Tooltip("眼睛控制器")]
        [SerializeField] private BossEyeController eyeController;
        
        [Tooltip("身体Transform（用于震动效果）")]
        [SerializeField] private Transform bodyTransform;
        
        [Tooltip("所有身体渲染器（用于颜色效果）")]
        [SerializeField] private SpriteRenderer[] bodyRenderers;
        
        [Tooltip("红色身体特效（怒目时显示）")]
        [SerializeField] private GameObject redBodyEffect;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private BossState currentState = BossState.Spawn;
        private BossState stateBeforeFrozen = BossState.Idle; // 【新增】冰冻前的状态
        private Rigidbody2D rb;
        private BossHealth bossHealth;
        
        // 位置缓存
        private Vector3 spawnPosition;
        private Vector3 battleAnchorPosition;
        private float screenMinX, screenMaxX;
        
        // 颜色缓存
        private Color[] originalColors;
        
        // Idle 状态
        private float idleTimer = 0f;
        private float currentIdleDuration;
        private float idleMoveTargetX;
        
        // Summon 冷却
        private float summonCooldownTimer = 0f;
        private bool summonCooldownReady = false;
        
        // Pollution 计时
        private float pollutionTimer = 0f;
        
        // Charge 状态
        private bool chargeInterrupted = false;
        private int chargeHitCount = 0;
        
        // Press 状态
        private bool isPressing = false;
        private bool isPressPhase3Active = false;
        private float pressDownForce = 0f;
        private Vector2 accumulatedPushForce = Vector2.zero;
        private bool isBeingPushed = false;
        
        // Press 角力物理
        private float currentPushForce = 0f;
        private float lastLaserHitTime = 0f;
        private bool isReceivingLaserHit = false;
        private const float laserHitTimeout = 0.15f;
        
        // Press 角力推力累加
        private float accumulatedPushForceThisTick = 0f;
        private bool pushForceUpdatedThisTick = false;
        
        // Press 角力摩擦伤害
        private float clashTimer = 0f;
        private bool isFrictionDamageActive = false;
        private float frictionDamageAccumulator = 0f;
        
        // 战术召唤
        private float continuousDamageTimer = 0f;
        private bool tacticalSummonCooldown = false;
        private const float TACTICAL_SUMMON_THRESHOLD = 5f;
        private const float TACTICAL_SUMMON_CD = 10f;
        
        // Press 过载
        private float pressOverloadDamage = 0f;
        private float pressOverloadTimer = 0f;

        // 污秽球管理
        private System.Collections.Generic.List<BossPollutionProjectile> activePollutionBalls = 
            new System.Collections.Generic.List<BossPollutionProjectile>();

        // 短僵直标记
        private float stunDurationOverride = -1f;
        
        // 协程引用
        private Coroutine stateCoroutine;
        
        // 连续受伤检测
        private float lastDamageTime = 0f;
        private const float DAMAGE_GAP_THRESHOLD = 0.5f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】冰冻与霸体系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        // 冰冻累积
        private float frostExposureTime = 0f;           // 累计照射时间
        private const float FROST_EXPOSURE_RESET_DELAY = 0.3f; // 停止照射后重置延迟
        private float frostExposureResetTimer = 0f;
        
        // 控制递减
        private int controlStack = 0;                    // 连续控制次数 (0-3)
        
        // 霸体状态
        private bool isUnstoppable = false;              // 是否处于霸体
        private float unstoppableTimer = 0f;             // 霸体剩余时间
        
        // 冰冻状态
        private bool isFrozen = false;                   // 是否冰冻中
        private float frozenTimer = 0f;                  // 冰冻剩余时间
        private Vector2 velocityBeforeFrozen;            // 冰冻前的速度

        private FrostDebuff frostDebuff;  // FrostDebuff 组件引用
        // ═══ 狂暴状态 ═══
        private bool hasTriggeredEnrage = false;  // 是否已触发过狂暴演出（防止重复触发）
        private bool isEnrageEffectActive = false; // 狂暴红光效果是否激活
        // 缓存
        private ShieldController cachedShieldController;

#if DOTWEEN
        private Tweener moveTweener;
        private Tweener shakeTweener;
#endif
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>Boss 配置（供 BossHealth 读取）</summary>
        public BossConfig Config => config;
        
        /// <summary>所有身体渲染器（供 BossHealth 使用受击效果）</summary>
        public SpriteRenderer[] BodyRenderers => bodyRenderers;
        
        /// <summary>原始颜色缓存（供 BossHealth 使用）</summary>
        public Color[] OriginalColors => originalColors;
        
        /// <summary>当前状态</summary>
        public BossState CurrentState => currentState;
        
        /// <summary>是否处于Charge蓄力阶段（可被Impact Lv.5打断）</summary>
        public bool IsInChargeTelegraph => currentState == BossState.Charge && !chargeInterrupted && !isUnstoppable;
        
        /// <summary>是否正在Press碾压中（可以被推）</summary>
        public bool IsPressing => currentState == BossState.Press && isPressing;
        
        /// <summary>血量百分比</summary>
        public float HealthPercent => bossHealth != null ? bossHealth.HealthPercent : 1f;
        
        /// <summary>是否狂暴</summary>
        public bool IsEnraged => HealthPercent <= (config != null ? config.rageHealthThreshold : 0.3f);
        
        /// <summary>【新增】是否处于霸体状态</summary>
        public bool IsUnstoppable => isUnstoppable;
        
        /// <summary>【新增】是否处于冰冻状态</summary>
        public bool IsFrozen => currentState == BossState.Frozen;
        
        /// <summary>【新增】当前控制递减层数</summary>
        public int ControlStack => controlStack;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            bossHealth = GetComponent<BossHealth>();
            
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }
        
        private void Start()
        {
            InitializePositions();
            CalculateScreenBounds();
            CacheOriginalColors();
            // 【新增】初始化 FrostDebuff 并设置渲染器
            frostDebuff = GetComponent<FrostDebuff>();
            if (frostDebuff == null)
            {
                frostDebuff = gameObject.AddComponent<FrostDebuff>();
            }
            // 传入 Boss 的身体渲染器
            if (bodyRenderers != null && bodyRenderers.Length > 0)
            {
                frostDebuff.SetTargetRenderers(bodyRenderers);
            }
            
            summonCooldownTimer = config != null ? config.summonCooldown : 15f;
            summonCooldownReady = false;
            pollutionTimer = 0f;
            
            GameEvents.TriggerBossFightStart();
            ChangeState(BossState.Spawn);
        }
        
        private void Update()
        {
            UpdateSummonCooldown();
            UpdateContinuousDamageTimer();
            CheckTacticalSummon();
            
            // 【新增】更新霸体计时
            UpdateUnstoppableTimer();
            
            // 【新增】更新冰冻照射重置计时
            UpdateFrostExposureReset();
            CheckEnrageTrigger();
#if UNITY_EDITOR
            // 调试快捷键
            if (Input.GetKeyDown(KeyCode.K))
            {
                if (bossHealth != null)
                {
                    bossHealth.TakeCoreDamage(1000f, transform.position, false);
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ChangeState(BossState.Charge);
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ChangeState(BossState.Press);
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                // 【新增】测试冰冻
                TryApplyFreeze(2f);
            }
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                // 【新增】测试霸体
                EnterUnstoppable();
            }
#endif
        }
        
        private void FixedUpdate()
        {
            // Press 角力物理
            if (isPressing && isPressPhase3Active)
            {
                // 1. 施加下压力
                if (pressDownForce > 0)
                {
                    rb.AddForce(Vector2.down * pressDownForce, ForceMode2D.Force);
                }
        
                // 2. 检查激光命中是否超时
                if (isReceivingLaserHit && Time.time - lastLaserHitTime > laserHitTimeout)
                {
                    isReceivingLaserHit = false;
                    currentPushForce = 0f;
                }
        
                // 3. 施加激光推力（【修改】霸体时削弱）
                if (isReceivingLaserHit && currentPushForce > 0.01f)
                {
                    float actualPushForce = currentPushForce;
                    
                    // 霸体期间推力削弱
                    if (isUnstoppable)
                    {
                        float multiplier = config != null ? config.unstoppablePushMultiplier : 0.3f;
                        actualPushForce *= multiplier;
                    }
                    
                    rb.AddForce(Vector2.up * actualPushForce, ForceMode2D.Force);
    
                    if (BattleStatistics.Instance != null)
                    {
                        BattleStatistics.Instance.MarkBossBeingPushed();
                    }
                }
                
                // 4. 摩擦伤害检测
                UpdateFrictionDamage();
                
                if (showDebugInfo && Time.frameCount % 30 == 0)
                {
                    float netForce = currentPushForce - pressDownForce;
                    Debug.Log($"[BossController] 角力: 下压={pressDownForce:F0}, 上推={currentPushForce:F0}, 净力={netForce:F0}, Y速度={rb.velocity.y:F2}, 霸体={isUnstoppable}");
                }
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】冰冻与霸体系统 - 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 累加 Frost 照射时间（由 LaserController 调用）
        /// </summary>
        public void AddFrostExposureTime(float deltaTime)
        {
            // 霸体期间不累积
            if (isUnstoppable) return;
            
            // 冰冻期间不累积
            if (currentState == BossState.Frozen) return;
            // 【新增】Press角力期间不累积冰冻（但减速仍然生效）
            if (currentState == BossState.Press) return;

            frostExposureTime += deltaTime;
            frostExposureResetTimer = FROST_EXPOSURE_RESET_DELAY;
            
            // 检查是否达到冰冻阈值
            float threshold = config != null ? config.bossFrostFreezeThreshold : 1.0f;
            if (frostExposureTime >= threshold)
            {
                float baseDuration = config != null ? config.bossFrostFreezeDuration : 2.0f;
                TryApplyFreeze(baseDuration);
                frostExposureTime = 0f; // 重置累积
            }
        }
        
        /// <summary>
        /// 获取当前 Frost 照射时间
        /// </summary>
        public float GetFrostExposureTime() => frostExposureTime;
        
        /// <summary>
        /// 重置 Frost 照射时间
        /// </summary>
        public void ResetFrostExposureTime()
        {
            frostExposureTime = 0f;
        }
        
        /// <summary>
        /// 尝试应用冰冻效果（考虑控制递减）
        /// </summary>
        /// <param name="baseDuration">基础冰冻时长</param>
        /// <returns>是否成功应用冰冻</returns>
        public bool TryApplyFreeze(float baseDuration)
        {
            // 霸体期间免疫
            if (isUnstoppable)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[BossController] ❄️ 冰冻被霸体免疫！");
                }
                ShowStatusText(BossStatusTextType.Unstoppable);
                return false;
            }
            // 【新增】Press角力期间免疫冰冻
            if (currentState == BossState.Press)
            {
                if (showDebugInfo)
                    Debug.Log("[BossController] ❄️ 冰冻被角力状态免疫！");
                return false;
            }
            // 已经冰冻中
            if (currentState == BossState.Frozen)
            {
                return false;
            }
            
            // 计算控制递减后的实际时长
            float actualDuration = CalculateControlDuration(baseDuration);
            
            if (actualDuration <= 0f)
            {
                // 触发霸体
                EnterUnstoppable();
                return false;
            }
            
            // 应用冰冻
            ApplyFreeze(actualDuration);
            return true;
        }
        
        /// <summary>
        /// 尝试应用僵直效果（考虑控制递减）
        /// </summary>
        public bool TryApplyStun(float baseDuration)
        {
            // 霸体期间免疫
            if (isUnstoppable)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[BossController] 💫 僵直被霸体免疫！");
                }
                ShowStatusText(BossStatusTextType.Unstoppable);
                return false;
            }
            
            // 计算控制递减
            float actualDuration = CalculateControlDuration(baseDuration);
            
            if (actualDuration <= 0f)
            {
                EnterUnstoppable();
                return false;
            }
            
            // 应用僵直
            stunDurationOverride = actualDuration;
            ChangeState(BossState.Stun);
            
            // 增加控制计数
            controlStack++;
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 💫 僵直生效！时长: {actualDuration:F2}s, 控制层数: {controlStack}");
            }
            
            return true;
        }
        
        /// <summary>
        /// 计算控制递减后的实际持续时间
        /// </summary>
        private float CalculateControlDuration(float baseDuration)
        {
            int triggerCount = config != null ? config.unstoppableTriggerCount : 3;
            
            if (controlStack >= triggerCount)
            {
                // 超过阈值，触发霸体
                return 0f;
            }
            
            float multiplier = 1f;
            
            switch (controlStack)
            {
                case 0:
                    multiplier = 1f; // 100%
                    break;
                case 1:
                    multiplier = config != null ? config.controlDiminish2nd : 0.5f; // 50%
                    break;
                case 2:
                    multiplier = config != null ? config.controlDiminish3rd : 0.25f; // 25%
                    break;
                default:
                    return 0f; // 触发霸体
            }
            
            // 狂暴状态下，控制效果持续时间额外减半
            if (IsEnraged && config != null)
            {
                multiplier *= config.rageControlDurationMultiplier;
            }
            return baseDuration * multiplier;
        }
        
        /// <summary>
        /// 进入霸体状态
        /// </summary>
        public void EnterUnstoppable()
        {
            if (isUnstoppable) return;
            
            isUnstoppable = true;
            unstoppableTimer = config != null ? config.unstoppableDuration : 6f;
            controlStack = 0; // 重置控制计数
            
            // 视觉效果：全身发红
            if (redBodyEffect != null)
            {
                redBodyEffect.SetActive(true);
            }
            
            // 显示飘字
            ShowStatusText(BossStatusTextType.Unstoppable);
            
            // 震动反馈
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.5f, 0.3f);
            }
            
            // 播放音效（可选）
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBossRoar();
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 🔴 进入霸体状态！持续 {unstoppableTimer:F1}s");
            }
        }
        
        /// <summary>
        /// 退出霸体状态
        /// </summary>
        private void ExitUnstoppable()
        {
            if (!isUnstoppable) return;
            
            isUnstoppable = false;
            unstoppableTimer = 0f;
            controlStack = 0; // 重置控制计数
            
            // 关闭红色特效
            if (redBodyEffect != null)
            {
                redBodyEffect.SetActive(false);
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🔴 霸体状态结束！控制计数已重置");
            }
        }
        
        /// <summary>
        /// 更新霸体计时器
        /// </summary>
        private void UpdateUnstoppableTimer()
        {
            if (!isUnstoppable) return;
            
            unstoppableTimer -= Time.deltaTime;
            
            if (unstoppableTimer <= 0f)
            {
                ExitUnstoppable();
            }
        }
        
        /// <summary>
        /// 更新冰冻照射重置计时
        /// </summary>
        private void UpdateFrostExposureReset()
        {
            if (frostExposureResetTimer > 0f)
            {
                frostExposureResetTimer -= Time.deltaTime;
                
                if (frostExposureResetTimer <= 0f && frostExposureTime > 0f)
                {
                    // 停止照射一段时间后重置累积
                    frostExposureTime = 0f;
                    
                    if (showDebugInfo)
                    {
                        Debug.Log("[BossController] ❄️ 冰冻累积已重置（照射中断）");
                    }
                }
            }
        }
        
        /// <summary>
        /// 应用冰冻效果
        /// </summary>
        private void ApplyFreeze(float duration)
        {
            // 增加控制计数
            controlStack++;
            
            // 记录冰冻前状态
            stateBeforeFrozen = currentState;
            frozenTimer = duration;
            
            // 【修改】使用 FrostDebuff 处理视觉效果
            if (frostDebuff != null)
            {
                frostDebuff.ApplyFreeze(duration);
            }
            
            // 切换到冰冻状态
            ChangeState(BossState.Frozen);
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] ❄️ 冰冻生效！时长: {duration:F2}s, 控制层数: {controlStack}");
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void InitializePositions()
        {
            spawnPosition = new Vector3(0f, 10f, 0f);
            transform.position = spawnPosition;
            
            float anchorY = config != null ? config.battleAnchorY : 3.0f;
            battleAnchorPosition = new Vector3(0f, anchorY, 0f);
        }
        
        private void CalculateScreenBounds()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            
            float height = cam.orthographicSize * 2f;
            float width = height * cam.aspect;
            
            float rangePercent = config != null ? config.idleMoveRangePercent : 0.8f;
            screenMinX = -width * 0.5f * rangePercent;
            screenMaxX = width * 0.5f * rangePercent;
        }
        
        private void CacheOriginalColors()
        {
            if (bodyRenderers == null || bodyRenderers.Length == 0)
            {
                bodyRenderers = GetComponentsInChildren<SpriteRenderer>();
            }
            
            originalColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null)
                {
                    originalColors[i] = bodyRenderers[i].color;
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态机
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ChangeState(BossState newState)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 状态切换: {currentState} -> {newState}");
            }
            
            ExitState(currentState);
            currentState = newState;
            // 【新增】上报Boss状态变化
            BattleStatistics.Instance?.RecordBossPhase(newState.ToString());
            EnterState(newState);
        }
        
        private void ExitState(BossState state)
        {
            if (stateCoroutine != null)
            {
                StopCoroutine(stateCoroutine);
                stateCoroutine = null;
            }
            
            if (state == BossState.Charge)
            {
                chargeInterrupted = false;
                if (redBodyEffect != null && !isUnstoppable && !isEnrageEffectActive)
                {
                    redBodyEffect.SetActive(false);
                }
            }
            else if (state == BossState.Press)
            {
                isPressing = false;
                accumulatedPushForce = Vector2.zero;
                isBeingPushed = false;
                // 霸体或狂暴时保持红光
                if (redBodyEffect != null && !isUnstoppable && !isEnrageEffectActive)
                {
                    redBodyEffect.SetActive(false);
                }
            }
            else if (state == BossState.Frozen)
            {
                // 【新增】退出冰冻状态时恢复
                isFrozen = false;
                SetBodyFrozenVisual(false);
            }
            
#if DOTWEEN
            if (moveTweener != null && moveTweener.IsActive()) moveTweener.Kill();
            if (shakeTweener != null && shakeTweener.IsActive()) shakeTweener.Kill();
#endif
        }
        
        private void EnterState(BossState state)
        {
            switch (state)
            {
                case BossState.Spawn:
                    stateCoroutine = StartCoroutine(SpawnRoutine());
                    break;
                case BossState.Idle:
                    EnterIdle();
                    break;
                case BossState.Summon:
                    BattleStatistics.Instance?.RecordBossSkill("summon");
                    stateCoroutine = StartCoroutine(SummonRoutine());
                    break;
                case BossState.Charge:
                    BattleStatistics.Instance?.RecordBossSkill("charge");
                    stateCoroutine = StartCoroutine(ChargeRoutine());
                    break;
                case BossState.Press:
                    BattleStatistics.Instance?.RecordBossSkill("press");
                    stateCoroutine = StartCoroutine(PressRoutine());
                    break;
                case BossState.Stun:
                    BattleStatistics.Instance?.RecordBossSkill("stun");
                    stateCoroutine = StartCoroutine(StunRoutine());
                    break;
                case BossState.Frozen:
                    stateCoroutine = StartCoroutine(FrozenRoutine());
                    break;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】State: Frozen (冰冻)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator FrozenRoutine()
        {
            isFrozen = true;
            
            // 保存并停止速度
            velocityBeforeFrozen = rb.velocity;
            rb.velocity = Vector2.zero;

            // 显示飘字
            ShowStatusText(BossStatusTextType.Frozen);
            
            // 播放冰冻音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayEnemyFreeze();
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] ❄️ 进入冰冻状态！时长: {frozenTimer:F2}s");
            }
            
            // 等待冰冻结束
            while (frozenTimer > 0f)
            {
                frozenTimer -= Time.deltaTime;
                yield return null;
            }
            
            // 冰冻结束
            isFrozen = false;
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] ❄️ 冰冻结束！返回 Idle");
            }
            
            // 检查是否触发霸体
            int triggerCount = config != null ? config.unstoppableTriggerCount : 3;
            if (controlStack >= triggerCount)
            {
                EnterUnstoppable();
            }
            
            // 返回 Idle（根据配置）
            bool returnToIdle = config != null ? config.frozenReturnToIdle : true;
            if (returnToIdle)
            {
                ChangeState(BossState.Idle);
            }
            else
            {
                // 尝试恢复之前的状态（如果合理）
                if (stateBeforeFrozen == BossState.Idle || 
                    stateBeforeFrozen == BossState.Spawn ||
                    stateBeforeFrozen == BossState.Frozen)
                {
                    ChangeState(BossState.Idle);
                }
                else
                {
                    ChangeState(stateBeforeFrozen);
                }
            }
        }
        
        /// <summary>
        /// 设置冰冻视觉效果
        /// </summary>
        /// <summary>
        /// 设置冰冻视觉效果（冰冻结束时调用）
        /// </summary>
        private void SetBodyFrozenVisual(bool frozen)
        {
            // 冰冻视觉效果已由 FrostDebuff 管理
            // 这里只处理冰冻结束时重置 FrostDebuff
            if (!frozen && frostDebuff != null)
            {
                frostDebuff.ResetDebuff();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Spawn (入场)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator SpawnRoutine()
        {
            float duration = config != null ? config.spawnDuration : 2.5f;
            
            if (eyeController != null) eyeController.SetStateDirect(BossEyeState.Closed);
            if (redBodyEffect != null) redBodyEffect.SetActive(false);
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 入场: {spawnPosition} -> {battleAnchorPosition}");
            }
            
#if DOTWEEN
            bool moveComplete = false;
            moveTweener = transform.DOMove(battleAnchorPosition, duration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => moveComplete = true);
            while (!moveComplete) yield return null;
#else
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Pow(1f - elapsed / duration, 2f);
                transform.position = Vector3.Lerp(startPos, battleAnchorPosition, t);
                yield return null;
            }
            transform.position = battleAnchorPosition;
#endif
            
            if (showDebugInfo) Debug.Log("[BossController] BOSS 咆哮！");
            
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBossRoar();
            }

            float shakeIntensity = config != null ? config.spawnShakeIntensity : 0.5f;
            float shakeDuration = config != null ? config.spawnShakeDuration : 0.5f;
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(shakeIntensity, shakeDuration);
            }
            
            yield return new WaitForSeconds(shakeDuration);
            
            ChangeState(BossState.Idle);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Idle (待机/游走)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void EnterIdle()
        {
            if (eyeController != null) eyeController.Close();
            
            currentIdleDuration = config != null ? config.GetIdleDuration(HealthPercent) : Random.Range(3f, 5f);
            idleTimer = 0f;
            
            SetNextIdleMoveTarget();
            stateCoroutine = StartCoroutine(IdleRoutine());
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 进入 Idle, 时长: {currentIdleDuration:F1}s, 狂暴: {IsEnraged}");
            }
        }
        
        private IEnumerator IdleRoutine()
        {
            float moveSpeed = config != null ? config.GetMoveSpeed(HealthPercent) : 1.5f;
            pollutionTimer = 0f;
            float pollutionInterval = config != null ? config.pollutionInterval : 4f;
            
            while (idleTimer < currentIdleDuration)
            {
                idleTimer += Time.deltaTime;
                
                if (summonCooldownReady)
                {
                    if (showDebugInfo) Debug.Log("[BossController] ⏰ 召唤冷却完成！打断Idle进入Summon");
                    ChangeState(BossState.Summon);
                    yield break;
                }
                
                pollutionTimer += Time.deltaTime;
                if (pollutionTimer >= pollutionInterval)
                {
                    pollutionTimer = 0f;
                    FirePollutionProjectile();
                }
                
                float currentX = transform.position.x;
                float newX = Mathf.MoveTowards(currentX, idleMoveTargetX, moveSpeed * Time.deltaTime);
                transform.position = new Vector3(newX, transform.position.y, transform.position.z);
                
                if (Mathf.Abs(newX - idleMoveTargetX) < 0.1f)
                {
                    SetNextIdleMoveTarget();
                }
                
                yield return null;
            }
            
            ChooseActiveSkill();
        }
        
        private void SetNextIdleMoveTarget()
        {
            idleMoveTargetX = Random.Range(screenMinX, screenMaxX);
        }
        
        private void ChooseActiveSkill()
        {
            bool useCharge = Random.value < 0.5f;
            
            if (useCharge)
            {
                if (showDebugInfo) Debug.Log("[BossController] 🔴 选择技能: Charge (野蛮冲撞 - 快招)");
                ChangeState(BossState.Charge);
            }
            else
            {
                if (showDebugInfo) Debug.Log("[BossController] 🟣 选择技能: Press (重力碾压 - 慢招)");
                ChangeState(BossState.Press);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Summon 冷却管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateSummonCooldown()
        {
            if (summonCooldownTimer > 0)
            {
                summonCooldownTimer -= Time.deltaTime;
                if (summonCooldownTimer <= 0)
                {
                    summonCooldownReady = true;
                }
            }
        }
        
        private void ResetSummonCooldown()
        {
            float cooldown = config != null ? config.GetSummonCooldown(HealthPercent) : 15f;
            summonCooldownTimer = cooldown;
            summonCooldownReady = false;
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 召唤冷却重置: {cooldown}s");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Summon (召唤爪牙) - 被动技能
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator SummonRoutine()
        {
            float duration = config != null ? config.summonDuration : 1.0f;
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🐙 召唤爪牙！身体收缩震动...");
            }
            
            float blinkDuration = config != null ? config.blinkDuration : 0.75f;
            if (eyeController != null)
            {
                eyeController.Blink(blinkDuration);
            }
            
#if DOTWEEN
            if (bodyTransform != null)
            {
                float intensity = config != null ? config.summonShakeIntensity : 0.1f;
                shakeTweener = bodyTransform.DOShakePosition(duration, intensity, 20, 90, false, true);
            }
#endif
            
            yield return new WaitForSeconds(duration);
            
            SpawnRushers();
            ResetSummonCooldown();
            
            ChangeState(BossState.Idle);
        }
        
        private void SpawnRushers()
        {
            if (EnemyPoolManager.Instance == null) return;
            
            int perSide = config != null ? config.summonRusherPerSide : 2;
            float speedBonus = config != null ? config.GetRusherSpeedBonus(HealthPercent) : 1f;
            
            Camera cam = Camera.main;
            if (cam == null) return;
            
            float halfWidth = cam.orthographicSize * cam.aspect;
            float spawnY = Random.Range(-2f, 2f);
            
            for (int i = 0; i < perSide; i++)
            {
                Vector3 pos = new Vector3(-halfWidth - 1f - i * 0.5f, spawnY + i * 0.3f, 0f);
                var enemy = EnemyPoolManager.Instance.Spawn(EnemyType.Rusher, pos);
                
                if (enemy != null && speedBonus > 1f)
                {
                    enemy.SetWaveModifiers(new DifficultyModifiers
                    {
                        hpMultiplier = 1f,
                        speedMultiplier = speedBonus,
                        massMultiplier = 1f,
                        damageMultiplier = 1f
                    });
                }
            }
            
            for (int i = 0; i < perSide; i++)
            {
                Vector3 pos = new Vector3(halfWidth + 1f + i * 0.5f, spawnY - i * 0.3f, 0f);
                var enemy = EnemyPoolManager.Instance.Spawn(EnemyType.Rusher, pos);
                
                if (enemy != null && speedBonus > 1f)
                {
                    enemy.SetWaveModifiers(new DifficultyModifiers
                    {
                        hpMultiplier = 1f,
                        speedMultiplier = speedBonus,
                        massMultiplier = 1f,
                        damageMultiplier = 1f
                    });
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 生成 {perSide * 2} 只 Rusher (狂暴: {IsEnraged})");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 战术召唤
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateContinuousDamageTimer()
        {
            if (Time.time - lastDamageTime > DAMAGE_GAP_THRESHOLD)
            {
                continuousDamageTimer = 0f;
            }
        }
        
        public void OnDamageReceived()
        {
            lastDamageTime = Time.time;
            continuousDamageTimer += Time.deltaTime;
        }
        
        private void CheckTacticalSummon()
        {
            if (currentState != BossState.Idle) return;
            if (tacticalSummonCooldown) return;
            
            if (continuousDamageTimer >= TACTICAL_SUMMON_THRESHOLD)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[BossController] ⚡ 战术召唤触发！玩家输出太安逸了！");
                }
                
                continuousDamageTimer = 0f;
                tacticalSummonCooldown = true;
                StartCoroutine(TacticalSummonCooldownRoutine());
                
                ChangeState(BossState.Summon);
            }
        }
        
        private IEnumerator TacticalSummonCooldownRoutine()
        {
            yield return new WaitForSeconds(TACTICAL_SUMMON_CD);
            tacticalSummonCooldown = false;
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 战术召唤冷却结束");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Pollution (污秽喷吐)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void FirePollutionProjectile()
        {
            GameObject prefab = config != null ? config.pollutionProjectilePrefab : null;
            if (prefab == null)
            {
                if (showDebugInfo) Debug.LogWarning("[BossController] Pollution Prefab 未设置！");
                return;
            }
            
            int maxCount = config != null ? config.pollutionMaxCount : 3;
            int burstCount = config != null ? config.GetPollutionBurstCount(HealthPercent) : 3;
            float spreadAngle = config != null ? config.pollutionSpreadAngle : 30f;
            
            activePollutionBalls.RemoveAll(b => b == null || b.IsDestroyed);
            
            int available = maxCount - activePollutionBalls.Count;
            int toSpawn = Mathf.Min(burstCount, available);
            
            if (toSpawn <= 0)
            {
                if (showDebugInfo) Debug.Log("[BossController] 污秽球已达上限，跳过喷吐");
                return;
            }
            
            Vector3 spawnPos = transform.position;
            Vector2 baseDir = Vector2.down;
            
            float startAngle = -spreadAngle * 0.5f;
            float angleStep = toSpawn > 1 ? spreadAngle / (toSpawn - 1) : 0f;
            
            for (int i = 0; i < toSpawn; i++)
            {
                float angle = startAngle + angleStep * i;
                Vector2 dir = Quaternion.Euler(0, 0, angle) * baseDir;
                
                GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);
                var ball = go.GetComponent<BossPollutionProjectile>();
                
                if (ball != null)
                {
                    // V3.0: 初始化带物理参数
                    if (config != null)
                    {
                        ball.InitializeV3(
                            config.pollutionSpeed,
                            config.pollutionTurnSpeed,
                            config.pollutionShieldDamage,
                            config.pollutionLifetime,
                            config.pollutionBallHP,
                            config.pollutionBallMass,
                            this
                        );
        
                        // 设置初始方向（计算角度偏移）
                        float angleOffset = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
                        ball.SetInitialDirection(angleOffset);
                    }
                    RegisterPollutionBall(ball);
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 喷射 {toSpawn} 个污秽球");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Charge (野蛮冲撞) - 主动技能A【快招】
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private IEnumerator ChargeRoutine()
        {
            chargeInterrupted = false;
            chargeHitCount = 0;
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🔴 Charge Phase 1: 蓄力！");
            }
            
            if (redBodyEffect != null && !isUnstoppable)
            {
                redBodyEffect.SetActive(true);
            }
            
            if (eyeController != null) eyeController.Open();
            
            float telegraphDuration = config != null ? config.chargeTelegraphDuration : 1.0f;
            float windupDistance = config != null ? config.chargeWindupDistance : 0.5f;
            
            // 播放预警音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBossChargeWarning();
            }
            
            // 身体后退（像拉弓）
            Vector3 windupPos = transform.position + Vector3.up * windupDistance;
            
        #if DOTWEEN
            transform.DOMove(windupPos, 0.2f).SetEase(Ease.OutQuad);
        #else
            float windupTime = 0f;
            Vector3 startPos = transform.position;
            while (windupTime < 0.2f)
            {
                windupTime += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, windupPos, windupTime / 0.2f);
                yield return null;
            }
        #endif
            
            float elapsed = 0f;
            while (elapsed < telegraphDuration)
            {
                if (chargeInterrupted)
                {
                    OnChargeInterrupted();
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🔴 Charge Phase 2: 冲锋！");
            }
            
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBossDash();
            }
            
            float speedMultiplier = GetChargeSpeedMultiplier();
            float baseDashDuration = config != null ? config.chargeDashDuration : 0.3f;
            float dashDuration = baseDashDuration / speedMultiplier;
            float targetY = config != null ? config.chargeTargetY : -10f;
            Vector3 dashTarget = new Vector3(transform.position.x, targetY, transform.position.z);
            
        #if DOTWEEN
            bool dashComplete = false;
            moveTweener = transform.DOMove(dashTarget, dashDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(() => dashComplete = true);
            while (!dashComplete) yield return null;
        #else
            float dashElapsed = 0f;
            Vector3 dashStart = transform.position;
            while (dashElapsed < dashDuration)
            {
                dashElapsed += Time.deltaTime;
                float t = dashElapsed / dashDuration;
                t = t * t;
                transform.position = Vector3.Lerp(dashStart, dashTarget, t);
                yield return null;
            }
            transform.position = dashTarget;
        #endif
            
            OnChargeHitPlayer();
        }
        
        public void OnHitReceived()
        {
            if (currentState != BossState.Charge) return;
            if (chargeInterrupted) return;
            
            chargeHitCount++;
            
            int threshold = config != null ? config.chargeHitCountThreshold : 30;
            if (chargeHitCount >= threshold)
            {
                chargeInterrupted = true;
                if (showDebugInfo)
                {
                    Debug.Log($"[BossController] 频率打断！受击次数: {chargeHitCount}");
                }
            }
        }
        
        public void InterruptCharge()
        {
            // 【修改】霸体期间免疫打断
            if (isUnstoppable)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[BossController] ⚡ Charge 打断被霸体免疫！");
                }
                ShowStatusText(BossStatusTextType.Unstoppable);
                return;
            }
            
            if (currentState != BossState.Charge) return;
            if (chargeInterrupted) return;
            
            chargeInterrupted = true;
        }
        
        private void OnChargeHitPlayer()
        {
            float damage = config != null ? config.chargeHitDamage : 300f;
            ApplyDamageToPlayer(damage, PlayerDamageSource.BossCollision);
            
            if (showDebugInfo) Debug.Log($"[BossController] 💥 Charge 撞击玩家！伤害: {damage}");
            
            float shakeIntensity = config != null ? config.chargeHitShakeIntensity : 0.8f;
            float shakeDuration = config != null ? config.chargeHitShakeDuration : 0.3f;
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.ImpactShake(Vector2.down, shakeIntensity, shakeDuration);
            }
            
            StartCoroutine(ChargeBounceBackRoutine());
        }
        
        private IEnumerator ChargeBounceBackRoutine()
        {
            float duration = config != null ? config.chargeBounceBackDuration : 0.5f;
            
#if DOTWEEN
            yield return transform.DOMove(battleAnchorPosition, duration)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();
#else
            float elapsed = 0f;
            Vector3 start = transform.position;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, battleAnchorPosition, elapsed / duration);
                yield return null;
            }
            transform.position = battleAnchorPosition;
#endif
            
            if (eyeController != null) eyeController.Close();
            yield return new WaitForSeconds(1.0f);
            
            ChangeState(BossState.Idle);
        }
        
        private void OnChargeInterrupted()
        {
            if (showDebugInfo) 
                Debug.Log("[BossController] 💥 Charge 蓄力被打断！进入僵直！");
            
            ShowStatusText(BossStatusTextType.Interrupted);
    
            if (redBodyEffect != null && !isUnstoppable)
            {
                redBodyEffect.SetActive(false);
            }
    
            // 【修改】使用控制递减的僵直
            float baseDuration = config != null ? config.chargeInterruptStunDuration : 3.0f;
    
            // 修复：如果晕眩失败（被霸体免疫），必须强制切换状态，否则会卡在 Charge 状态
            if (!TryApplyStun(baseDuration))
            {
                if (showDebugInfo) Debug.Log("[BossController] 霸体免疫打断僵直，强制返回 Idle");
                ChangeState(BossState.Idle);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Press (重力碾压) - 主动技能B【慢招/角力】
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator PressRoutine()
        {
            isPressing = false;
            accumulatedPushForce = Vector2.zero;
            clashTimer = 0f;
            isFrictionDamageActive = false;
            frictionDamageAccumulator = 0f;
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🟣 Press Phase 1: 突进贴脸！（闭眼）");
            }
            
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBossDash();
            }
            
            if (eyeController != null) eyeController.Close();
            
            float jumpDuration = config != null ? config.pressJumpDuration : 0.5f;
            float hoverY = config != null ? config.pressHoverY : -5f;
            Vector3 hoverPosition = new Vector3(0f, hoverY, 0f);
            
#if DOTWEEN
            bool jumpComplete = false;
            moveTweener = transform.DOMove(hoverPosition, jumpDuration)
                .SetEase(Ease.OutExpo)
                .OnComplete(() => jumpComplete = true);
            while (!jumpComplete) yield return null;
#else
            float jumpElapsed = 0f;
            Vector3 jumpStart = transform.position;
            while (jumpElapsed < jumpDuration)
            {
                jumpElapsed += Time.deltaTime;
                float t = 1f - Mathf.Pow(1f - jumpElapsed / jumpDuration, 3f);
                transform.position = Vector3.Lerp(jumpStart, hoverPosition, t);
                yield return null;
            }
            transform.position = hoverPosition;
#endif
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🟣 Press Phase 2: 施压！眼睛缓缓睁开...");
            }
            
            float glareDuration = config != null ? config.pressGlareDuration : 1.5f;
            
            if (redBodyEffect != null && !isUnstoppable)
            {
                redBodyEffect.SetActive(true);
            }
            
            if (eyeController != null)
            {
                eyeController.OpenSlowly(glareDuration);
            }
            
            yield return new WaitForSeconds(glareDuration);
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] Press Phase 3: 开始碾压！角力进行中...");
            }
            
            pressOverloadDamage = 0f;
            pressOverloadTimer = 0f;
            
            isPressing = true;
            isPressPhase3Active = true;
            
            pressDownForce = config != null ? config.GetPressForce(HealthPercent) : 100f;
            
            float safeLineY = config != null ? config.pressSafeLineY : 3.5f;
            float hitLineY = config != null ? config.pressHitLineY : -10f;
            float maxDuration = config != null ? config.pressMaxDuration : 15f;
            
            float pressTimer = 0f;
            
            while (pressTimer < maxDuration)
            {
                pressTimer += Time.deltaTime;
                clashTimer += Time.deltaTime;
                
                accumulatedPushForceThisTick = 0f;
                pushForceUpdatedThisTick = false;
                
                if (transform.position.y >= safeLineY)
                {
                    if (showDebugInfo) Debug.Log("[BossController] ✅ 玩家推回Boss！角力胜利！");
                    OnPressCountered();
                    yield break;
                }
                
                if (transform.position.y <= hitLineY)
                {
                    if (showDebugInfo) Debug.Log("[BossController] ❌ Boss碾压成功！玩家失败！");
                    OnPressHitPlayer();
                    yield break;
                }
                
                float maxClashTime = config != null ? config.maxClashDuration : 6f;
                if (clashTimer >= maxClashTime)
                {
                    if (showDebugInfo) Debug.Log("[BossController] ⏰ 角力超时！Boss疲劳撤退！");
                    OnPressExhausted();
                    yield break;
                }
                
                yield return null;
            }
            
            if (showDebugInfo) Debug.Log("[BossController] ⏰ Press超时！自动结束");
            OnPressExhausted();
        }
        
        private void UpdateFrictionDamage()
        {
            float triggerY = config != null ? config.frictionTriggerY : -8.5f;
            
            if (transform.position.y <= triggerY)
            {
                if (!isFrictionDamageActive)
                {
                    isFrictionDamageActive = true;
                    GameEvents.TriggerBossFrictionStart();
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"[BossController] 🔥 摩擦伤害开始！Y={transform.position.y:F2}");
                    }
                }
                
                float dps = config != null ? config.frictionDamagePerSecond : 50f;
                frictionDamageAccumulator += dps * Time.fixedDeltaTime;
                
                if (frictionDamageAccumulator >= 1f)
                {
                    int damage = Mathf.FloorToInt(frictionDamageAccumulator);
                    frictionDamageAccumulator -= damage;
                    ApplyDamageToPlayer(damage, PlayerDamageSource.BossFriction);
                }
            }
            else if (isFrictionDamageActive)
            {
                isFrictionDamageActive = false;
                GameEvents.TriggerBossFrictionEnd();
                
                if (showDebugInfo)
                {
                    Debug.Log("[BossController] 🔥 摩擦伤害结束");
                }
            }
            
            float window = config != null ? config.pressOverloadWindow : 1.5f;
            pressOverloadTimer += Time.fixedDeltaTime;
            
            if (pressOverloadTimer >= window)
            {
                pressOverloadDamage = 0f;
                pressOverloadTimer = 0f;
            }
        }
        
        public void RecordPressOverloadDamage(float damage)
        {
            if (!isPressing) return;
            
            pressOverloadDamage += damage;
            
            float threshold = config != null ? config.pressOverloadDamageThreshold : 2000f;
            if (pressOverloadDamage >= threshold)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[BossController] 💥 Press 过载！累计伤害 {pressOverloadDamage:F0} >= {threshold:F0}");
                }
                OnPressOverload();
            }
        }
        
        private void OnPressOverload()
        {
            isPressPhase3Active = false;
            pressDownForce = 0f;
            currentPushForce = 0f;
            isReceivingLaserHit = false;
            isFrictionDamageActive = false;
            frictionDamageAccumulator = 0f;
            pressOverloadDamage = 0f;
            
            rb.velocity = Vector2.zero;
            isPressing = false;
            
            GameEvents.TriggerBossFrictionEnd();
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🔥 Press 过载！Boss 核心过热撤退！");
            }
            
            ShowStatusText(BossStatusTextType.Overload);
            
            float shortDuration = config != null ? config.shortStunDuration : 1.5f;
            if (!TryApplyStun(shortDuration))
            {
                if (showDebugInfo) Debug.Log("[BossController] 霸体免疫过载僵直，强制返回 Idle");
                ChangeState(BossState.Idle);
            }
            
            EnterShortStun();
        }
        
        private void OnPressCountered()
        {
            isPressPhase3Active = false;
            pressDownForce = 0f;
            currentPushForce = 0f;
            isReceivingLaserHit = false;
            isFrictionDamageActive = false;
            frictionDamageAccumulator = 0f;
            
            rb.velocity = Vector2.zero;
            isPressing = false;
            
            GameEvents.TriggerBossFrictionEnd();
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] ✅ Press 被反推！玩家胜利！");
            }
            
            ShowStatusText(BossStatusTextType.Countered);
            
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.5f, 0.3f);
            }
            
            // 【修改】使用控制递减的僵直
            float baseDuration = config != null ? config.pressCounterStunDuration : 3.0f;
            // 修复：如果晕眩失败（被霸体免疫），必须强制切换状态，否则会卡在 Press 循环中
            if (!TryApplyStun(baseDuration))
            {
                if (showDebugInfo) Debug.Log("[BossController] 霸体免疫反制僵直，强制返回 Idle");
                ChangeState(BossState.Idle);
            }
        }
        
        private void OnPressHitPlayer()
        {
            isPressPhase3Active = false;
            isPressing = false;
            isFrictionDamageActive = false;
            frictionDamageAccumulator = 0f;
            
            GameEvents.TriggerBossFrictionEnd();
            
            float damage = config != null ? config.chargeHitDamage : 300f;
            ApplyDamageToPlayer(damage, PlayerDamageSource.BossCollision);
            
            if (showDebugInfo) Debug.Log($"[BossController] 💥 Press 碾压成功！伤害: {damage}");
            
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.ImpactShake(Vector2.down, 1f, 0.5f);
            }
            
            StartCoroutine(PressBounceBackRoutine());
        }
        
        private void OnPressExhausted()
        {
            isPressPhase3Active = false;
            pressDownForce = 0f;
            currentPushForce = 0f;
            isReceivingLaserHit = false;
            isFrictionDamageActive = false;
            frictionDamageAccumulator = 0f;
            
            rb.velocity = Vector2.zero;
            isPressing = false;
            
            GameEvents.TriggerBossFrictionEnd();
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 😤 Boss 角力疲劳！强制撤退！");
            }
            
            ShowStatusText(BossStatusTextType.Exhausted);
            
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.2f, 0.15f);
            }
            
            StartCoroutine(ExhaustedRetreatRoutine());
        }
        
        private IEnumerator ExhaustedRetreatRoutine()
        {
            float duration = 0.4f;
            Vector3 retreatTarget = battleAnchorPosition + Vector3.up * 1.5f;
    
#if DOTWEEN
            yield return transform.DOMove(retreatTarget, duration)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();
#else
    float elapsed = 0f;
    Vector3 start = transform.position;
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        transform.position = Vector3.Lerp(start, retreatTarget, elapsed / duration);
        yield return null;
    }
    transform.position = retreatTarget;
#endif
    
            if (eyeController != null) eyeController.Open();
    
            // 【修改】使用控制递减的僵直
            float baseDuration = config != null ? config.exhaustedStunDuration : 2.5f;
    
            // 修复 Bug：如果霸体免疫了晕眩，必须强制返回 Idle，否则 Boss 会定住
            if (!TryApplyStun(baseDuration))
            {
                if (showDebugInfo) Debug.Log("[BossController] 霸体免疫疲劳僵直，强制返回 Idle");
                ChangeState(BossState.Idle);
            }
        }
        
        private IEnumerator PressBounceBackRoutine()
        {
            float duration = config != null ? config.chargeBounceBackDuration : 0.5f;
            
#if DOTWEEN
            yield return transform.DOMove(battleAnchorPosition, duration)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();
#else
            float elapsed = 0f;
            Vector3 start = transform.position;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, battleAnchorPosition, elapsed / duration);
                yield return null;
            }
            transform.position = battleAnchorPosition;
#endif
            
            if (eyeController != null) eyeController.Close();
            yield return new WaitForSeconds(1.0f);
            
            ChangeState(BossState.Idle);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 角力物理系统 - 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void ApplyLaserPushForce(float pushForce)
        {
            if (!isPressing) return;
            
            accumulatedPushForceThisTick += pushForce;
            pushForceUpdatedThisTick = true;
            lastLaserHitTime = Time.time;
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 收到推力更新: {currentPushForce:F2}");
            }
        }
        
        public float CalculatePushForce(int impactLevel, int wideLevel)
        {
            float baseForce = config != null ? config.baseLaserPushForce : 120f;
            
            float impactMultiplier = config != null ? config.GetPushMultiplier(impactLevel) : 0.4f;
            float wideMultiplier = config != null ? config.GetWidePushMultiplier(wideLevel) : 0.4f;
            
            float multiplier = Mathf.Max(impactMultiplier, wideMultiplier);
            
            return baseForce * multiplier;
        }
        
        public void FinalizePushForceThisTick()
        {
            if (!isPressing) return;
            
            if (pushForceUpdatedThisTick)
            {
                float maxForce = config != null ? config.maxTotalPushForce : 300f;
                currentPushForce = Mathf.Min(accumulatedPushForceThisTick, maxForce);
                isReceivingLaserHit = true;
                
                if (showDebugInfo && accumulatedPushForceThisTick > maxForce)
                {
                    Debug.Log($"[BossController] 推力超限! {accumulatedPushForceThisTick:F0} -> {maxForce:F0}");
                }
            }
            
            accumulatedPushForceThisTick = 0f;
            pushForceUpdatedThisTick = false;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Stun (僵直)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator StunRoutine()
        {
            float duration;
            if (stunDurationOverride > 0)
            {
                duration = stunDurationOverride;
                stunDurationOverride = -1f;
                
                if (showDebugInfo)
                {
                    Debug.Log($"[BossController] 💫 进入僵直！时长: {duration}s");
                }
            }
            else
            {
                if (chargeInterrupted)
                {
                    duration = config != null ? config.chargeInterruptStunDuration : 3.0f;
                }
                else
                {
                    duration = config != null ? config.pressCounterStunDuration : 3.0f;
                }
                
                if (showDebugInfo)
                {
                    Debug.Log($"[BossController] 💫 进入僵直！时长: {duration}s（奖励时间）");
                }
            }
            
            if (eyeController != null) eyeController.Open();
            SetBodyDarken(true);
            
            yield return new WaitForSeconds(duration);
            
            SetBodyDarken(false);
            
            bool returnToAnchor = config != null ? config.stunReturnToAnchor : true;
            
            if (returnToAnchor)
            {
                yield return StartCoroutine(ReturnToAnchorRoutine());
            }
            
            ChangeState(BossState.Idle);
        }
        
        private IEnumerator ReturnToAnchorRoutine()
        {
            float duration = 0.5f;
            
#if DOTWEEN
            yield return transform.DOMove(battleAnchorPosition, duration)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();
#else
            float elapsed = 0f;
            Vector3 start = transform.position;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, battleAnchorPosition, elapsed / duration);
                yield return null;
            }
            transform.position = battleAnchorPosition;
#endif
        }
        
        private void SetBodyDarken(bool darken)
        {
            float darkenAmount = config != null ? config.stunDarkenAmount : 0.5f;
            
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null)
                {
                    if (darken)
                    {
                        bodyRenderers[i].color = originalColors[i] * (1f - darkenAmount);
                    }
                    else
                    {
                        bodyRenderers[i].color = originalColors[i];
                    }
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 连体Buff / 辅助方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public int GetLinkedBuffStacks()
        {
            if (EnemyPoolManager.Instance == null) return 0;
            
            int rusherCount = EnemyPoolManager.Instance.GetActiveCount(EnemyType.Rusher);
            int maxStacks = config != null ? config.linkedBuffMaxStacks : 5;
            
            return Mathf.Min(rusherCount, maxStacks);
        }
        
        public float GetChargeSpeedMultiplier()
        {
            int stacks = GetLinkedBuffStacks();
            float bonusPerStack = config != null ? config.linkedBuffChargeSpeedPerStack : 0.1f;
            
            return 1f + (stacks * bonusPerStack);
        }
        
        public void EnterShortStun()
        {
            float shortDuration = config != null ? config.shortStunDuration : 1.5f;
    
            // 修复 Bug：如果霸体免疫晕眩，强制返回 Idle
            if (!TryApplyStun(shortDuration))
            {
                if (showDebugInfo) Debug.Log("[BossController] 霸体免疫短僵直，强制返回 Idle");
                // 只有当当前状态不是 Idle 时才切换，避免逻辑混乱
                if (currentState != BossState.Idle)
                {
                    ChangeState(BossState.Idle);
                }
            }
        }
        
        public void RegisterPollutionBall(BossPollutionProjectile ball)
        {
            if (ball != null && !activePollutionBalls.Contains(ball))
            {
                activePollutionBalls.Add(ball);
            }
        }
        
        public void UnregisterPollutionBall(BossPollutionProjectile ball)
        {
            if (ball != null)
            {
                activePollutionBalls.Remove(ball);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 工具方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ApplyDamageToPlayer(float damage, PlayerDamageSource source = PlayerDamageSource.BossCollision)
        {
            if (BattleStatistics.Instance != null)
            {
                BattleStatistics.Instance.RecordPlayerDamage(damage, source);
            }
            
            ShieldController shield = cachedShieldController ?? FindObjectOfType<ShieldController>();
            TurretHealth turret = FindObjectOfType<TurretHealth>();
            
            if (shield != null)
            {
                int remainingDamage = shield.TakeBossDamage((int)damage);
                if (remainingDamage > 0 && turret != null)
                {
                    turret.TakeBossDamage(remainingDamage);
                }
            }
            else if (turret != null)
            {
                turret.TakeBossDamage((int)damage);
            }
        }
        
        /// <summary>
        /// 显示状态飘字（使用配置文本）
        /// </summary>
        private void ShowStatusText(BossStatusTextType textType)
        {
            if (FloatingTextManager.Instance == null) return;
            
            string text = GetStatusText(textType);
            FloatingTextManager.Instance.ShowStatus(transform.position, text);
        }
        /// <summary>
        /// 获取配置化的状态文本
        /// </summary>
        private string GetStatusText(BossStatusTextType textType)
        {
            if (config == null)
            {
                switch (textType)
                {
                    case BossStatusTextType.Unstoppable: return "UNSTOPPABLE!";
                    case BossStatusTextType.Frozen: return "FROZEN!";
                    case BossStatusTextType.Enraged: return "ENRAGED!";
                    case BossStatusTextType.Interrupted: return "INTERRUPTED!";
                    case BossStatusTextType.Countered: return "COUNTERED!";
                    case BossStatusTextType.Overload: return "OVERLOAD!";
                    case BossStatusTextType.Exhausted: return "EXHAUSTED!";
                    default: return "";
                }
            }
            
            switch (textType)
            {
                case BossStatusTextType.Unstoppable: return config.unstoppableText;
                case BossStatusTextType.Frozen: return config.frozenText;
                case BossStatusTextType.Enraged: return config.enragedText;
                case BossStatusTextType.Interrupted: return config.interruptedText;
                case BossStatusTextType.Countered: return config.counteredText;
                case BossStatusTextType.Overload: return config.overloadText;
                case BossStatusTextType.Exhausted: return config.exhaustedText;
                default: return "";
            }
        }
        public void ForceStun()
        {
            ChangeState(BossState.Stun);
        }
        
        private int GetCurrentMobCount()
        {
            return EnemyPoolManager.Instance != null ? EnemyPoolManager.Instance.TotalActiveEnemies : 0;
        }
        /// <summary>
        /// 检查是否首次进入狂暴状态
        /// </summary>
        private void CheckEnrageTrigger()
        {
            // 已经触发过或未进入狂暴阈值
            if (hasTriggeredEnrage || !IsEnraged) return;
    
            // 标记已触发
            hasTriggeredEnrage = true;
    
            // 启动狂暴演出
            StartCoroutine(EnrageTriggerRoutine());
        }

        /// <summary>
        /// 狂暴触发演出协程
        /// </summary>
        private IEnumerator EnrageTriggerRoutine()
        {
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🔥 Boss 进入狂暴状态！触发演出...");
            }
    
            // 1. 短暂停顿（Boss停止当前动作）
            float pauseDuration = config != null ? config.enrageTriggerPauseDuration : 0.5f;
    
            // 2. 咆哮音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBossRoar();
            }
    
            // 3. 屏幕震动
            float shakeIntensity = config != null ? config.enrageTriggerShakeIntensity : 0.6f;
            float shakeDuration = config != null ? config.enrageTriggerShakeDuration : 0.8f;
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(shakeIntensity, shakeDuration);
            }
    
            // 4. 激活常驻红光效果
            ActivateEnrageEffect();
    
            // 5. BGM加速
            if (AudioManager.Instance != null)
            {
                float pitch = config != null ? config.rageBGMPitch : 1.15f;
                AudioManager.Instance.SetBGMPitch(pitch);
            }
    
            // 6. 显示狂暴提示文字
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.ShowStatus(transform.position, "ENRAGED!");
            }
    
            yield return new WaitForSeconds(pauseDuration);
    
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🔥 狂暴演出完成，Boss 变得更加狂暴！");
            }
        }

        /// <summary>
        /// 激活狂暴常驻红光效果（淡红光）
        /// </summary>
        private void ActivateEnrageEffect()
        {
            isEnrageEffectActive = true;
            if (redBodyEffect != null)
            {
                redBodyEffect.SetActive(true);
        
                // 调整红光透明度（狂暴时用淡红光，区别于Charge时的强红光）
                SpriteRenderer sr = redBodyEffect.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = config.enrageRedGlowAlpha;  // 40% 透明度，比 Charge 时更淡
                    sr.color = c;
                }
            }
        }

        /// <summary>
        /// 关闭狂暴红光效果（Boss死亡时调用）
        /// </summary>
        private void DeactivateEnrageEffect()
        {
            isEnrageEffectActive = false;
            if (redBodyEffect != null)
            {
                redBodyEffect.SetActive(false);
            }
        }

        /// <summary>
        /// 应用减速效果（由 LaserController 调用）
        /// </summary>
        public void ApplyFrostSlow(float slowPercent, float duration)
        {
            // 霸体期间免疫
            if (isUnstoppable) return;
            
            // 冰冻期间不叠加减速
            if (currentState == BossState.Frozen) return;
            
            if (frostDebuff == null) return;
            
            // 应用 Boss 减速削弱系数
            float multiplier = config != null ? config.bossSlowEffectMultiplier : 0.5f;
            float actualSlowPercent = slowPercent * multiplier;
            float actualDuration = duration * multiplier;
            
            // 调用 FrostDebuff 处理减速（包括视觉效果）
            frostDebuff.ApplySlow(actualSlowPercent, actualDuration);
            
            if (showDebugInfo && !frostDebuff.IsSlowed)
            {
                Debug.Log($"[BossController] ❄️ Boss 减速！{actualSlowPercent:P0} 持续 {actualDuration:F1}s");
            }
        }
        
        /// <summary>
        /// 获取当前减速后的速度倍率
        /// </summary>
        public float GetSlowedSpeedMultiplier()
        {
            if (frostDebuff == null) return 1f;
            return frostDebuff.SpeedMultiplier;
        }
        /// <summary>
        /// 是否处于减速状态
        /// </summary>
        public bool IsSlowed => frostDebuff != null && frostDebuff.IsSlowed;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 150, 280, 300));
            GUILayout.Label("=== Boss Controller (重构版) ===");
            GUILayout.Label($"State: {currentState}");
            GUILayout.Label($"HP: {HealthPercent:P1} | Enraged: {IsEnraged}");
            GUILayout.Label($"Eye: {(eyeController != null ? eyeController.CurrentState.ToString() : "N/A")}");
            GUILayout.Label($"Mobs: {GetCurrentMobCount()}");
            GUILayout.Label($"IsPressing: {isPressing}");
            GUILayout.Label($"Position Y: {transform.position.y:F2}");
            GUILayout.Label($"Velocity Y: {(rb != null ? rb.velocity.y.ToString("F2") : "N/A")}");
            
            GUILayout.Space(5);
            GUILayout.Label("=== 冰冻与霸体 ===");
            GUILayout.Label($"Frost Exposure: {frostExposureTime:F2}s");
            GUILayout.Label($"Control Stack: {controlStack}");
            GUILayout.Label($"Unstoppable: {isUnstoppable} ({unstoppableTimer:F1}s)");
            GUILayout.Label($"Frozen: {isFrozen} ({frozenTimer:F1}s)");
            
            GUILayout.Space(5);
            GUILayout.Label($"Summon CD: {summonCooldownTimer:F1}s {(summonCooldownReady ? "✓ READY" : "")}");
            GUILayout.Label($"Pollution: {pollutionTimer:F1}s");
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("Force Stun")) ForceStun();
            if (GUILayout.Button("Force Charge")) ChangeState(BossState.Charge);
            if (GUILayout.Button("Force Press")) ChangeState(BossState.Press);
            if (GUILayout.Button("Force Freeze")) TryApplyFreeze(2f);
            if (GUILayout.Button("Force Unstoppable")) EnterUnstoppable();
            
            GUILayout.EndArea();
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(battleAnchorPosition, 0.5f);
            
            Gizmos.color = Color.cyan;
            float y = battleAnchorPosition.y;
            Gizmos.DrawLine(new Vector3(screenMinX, y, 0), new Vector3(screenMaxX, y, 0));
            
            float safeY = config != null ? config.pressSafeLineY : 3.5f;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(new Vector3(-10, safeY, 0), new Vector3(10, safeY, 0));
            
            float hitY = config != null ? config.pressHitLineY : -10f;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(-10, hitY, 0), new Vector3(10, hitY, 0));
            
            float hoverY = config != null ? config.pressHoverY : -5f;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(new Vector3(-5, hoverY, 0), new Vector3(5, hoverY, 0));
        }
#endif
    }
}