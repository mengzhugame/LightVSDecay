// ============================================================
// BossController.cs
// 文件位置: Assets/Scripts/Logic/Boss/BossController.cs
// 用途：Boss 行为状态机主控制器 (The Corruptor - 污染之核)
// 【重构】分离 Charge(快招) 和 Press(慢招) 两个主动技能
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
using LightVsDecay.UI.FloatingText;
#if DOTWEEN
using DG.Tweening;
#endif

namespace LightVsDecay.Logic.Boss
{
    /// <summary>
    /// Boss 行为状态
    /// </summary>
    public enum BossState
    {
        Spawn,      // 入场
        Idle,       // 待机/游走
        Summon,     // 召唤爪牙（被动技能，可打断Idle）
        Charge,     // 野蛮冲撞（主动技能A - 快招/反应测试）
        Press,      // 重力碾压（主动技能B - 慢招/物理角力）【新增】
        Stun        // 僵直/虚弱
    }
    
    /// <summary>
    /// Boss 控制器 - 污染之核 (The Corruptor)
    /// 状态机循环：Spawn -> Idle -> (Summon可打断) -> Charge/Press(50/50) -> Stun -> Idle ...
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
        
        [Tooltip("身体Transform（用于抖动动画）")]
        [SerializeField] private Transform bodyTransform;
        
        [Tooltip("Body03 - 红色身体特效")]
        [SerializeField] private GameObject redBodyEffect;
        
        [Header("视觉引用")]
        [Tooltip("所有需要变暗的SpriteRenderer（僵直时使用）")]
        [SerializeField] private SpriteRenderer[] bodyRenderers;
        
        [Header("目标")]
        [Tooltip("玩家塔（冲撞目标）")]
        [SerializeField] private Transform playerTower;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = true;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 组件缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Rigidbody2D rb;
        private BossHealth bossHealth;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private BossState currentState = BossState.Spawn;
        private Vector3 battleAnchorPosition;   // 战斗锚点
        private Vector3 spawnPosition;          // 生成位置
        
        // Idle 相关
        private float idleTimer;
        private float currentIdleDuration;
        private float idleMoveTargetX;
        private float screenMinX;
        private float screenMaxX;
        
        // Summon 冷却
        private float summonCooldownTimer;
        private bool summonCooldownReady = false;
        
        // Pollution 发射
        private float pollutionTimer;
        // 战术召唤相关（新增）
        private float continuousDamageTimer = 0f;           // 连续受伤计时器
        private const float TACTICAL_SUMMON_THRESHOLD = 5f; // 触发阈值：连续受伤5秒
        private bool tacticalSummonCooldown = false;        // 战术召唤冷却标记
        private const float TACTICAL_SUMMON_CD = 10f;       // 战术召唤冷却时间
        // Press (慢招/角力) 相关
        private bool isPressing = false;            // 是否正在碾压/角力
        private float currentPushForce = 0f;           // 当前推力大小（持续施加直到更新）
        private bool isReceivingLaserHit = false;      // 是否正在被激光照射
        private float laserHitTimeout = 0.15f;         // 激光命中超时（略大于tick间隔）
        private float lastLaserHitTime = 0f;           // 上次被激光命中的时间
        // 【新增】多激光推力累加
        private float accumulatedPushForceThisTick = 0f;  // 本tick累积的推力
        private bool pushForceUpdatedThisTick = false;     // 本tick是否有推力更新
        // Charge (快招) 相关
        private bool chargeInterrupted = false;
        private int chargeHitCount = 0;                     // 蓄力+冲锋期间累计受击次数
        private Vector2 accumulatedPushForce;       // 累积的激光推力
        private bool isBeingPushed = false;         // 是否正在被推

        // Press 角力状态
        private bool isPressPhase3Active = false;    // Phase 3 角力是否激活
        private float pressDownForce = 0f;           // 当前下压力
        // ━━━ 角力摩擦伤害状态 【新增】 ━━━
        private bool isFrictionDamageActive = false;    // 是否正在造成摩擦伤害
        private float frictionDamageAccumulator = 0f;   // 摩擦伤害累计器
        private float clashTimer = 0f;                  // 角力总计时器
        private ShieldController cachedShieldController;
        // 颜色缓存
        private Color[] originalColors;


// Press 过载检测
        private float pressOverloadDamage = 0f;             // 过载窗口内累计伤害
        private float pressOverloadTimer = 0f;              // 过载计时器

// 污秽球管理
        private System.Collections.Generic.List<BossPollutionProjectile> activePollutionBalls = 
            new System.Collections.Generic.List<BossPollutionProjectile>();

// 短僵直标记
        private float stunDurationOverride = -1f;           // 如果>0，使用此值覆盖默认僵直时长
        // 协程引用
        private Coroutine stateCoroutine;
        // 在 Update 中检测连续受伤
        private float lastDamageTime = 0f;
        private const float DAMAGE_GAP_THRESHOLD = 0.5f; // 超过0.5秒未受伤则重置
#if DOTWEEN
        private Tweener moveTweener;
        private Tweener shakeTweener;
#endif
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前状态</summary>
        public BossState CurrentState => currentState;
        
        /// <summary>是否处于Charge蓄力阶段（可被Impact Lv.5打断）</summary>
        public bool IsInChargeTelegraph => currentState == BossState.Charge && !chargeInterrupted;
        
        /// <summary>是否正在Press碾压中（可以被推）</summary>
        public bool IsPressing => currentState == BossState.Press && isPressing;
        
        /// <summary>血量百分比</summary>
        public float HealthPercent => bossHealth != null ? bossHealth.HealthPercent : 1f;
        
        /// <summary>是否狂暴</summary>
        public bool IsEnraged => HealthPercent <= (config != null ? config.rageHealthThreshold : 0.3f);
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            bossHealth = GetComponent<BossHealth>();
            
            // 配置 Rigidbody2D
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
            
            // 初始化计时器
            summonCooldownTimer = config != null ? config.summonCooldown : 15f;
            summonCooldownReady = false;
            pollutionTimer = 0f;
            
            // 通知UI Boss战开始
            GameEvents.TriggerBossFightStart();
            
            // 开始入场
            ChangeState(BossState.Spawn);
        }
        
        private void Update()
        {
            // 更新召唤冷却
            UpdateSummonCooldown();
            // 更新连续受伤计时器
            UpdateContinuousDamageTimer();
            
            // 检查战术召唤
            CheckTacticalSummon();
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
#endif
        }
        
        private void FixedUpdate()
        {
            // ═══════════════════════════════════════════════════
            // Press 角力物理（统一在 FixedUpdate 处理）
            // ═══════════════════════════════════════════════════
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
        
                // 3. 施加激光推力
                if (isReceivingLaserHit && currentPushForce > 0.01f)
                {
                    rb.AddForce(Vector2.up * currentPushForce, ForceMode2D.Force);
                }
                // 【新增】4. 摩擦伤害检测
                UpdateFrictionDamage();
                // 调试日志
                if (showDebugInfo && Time.frameCount % 30 == 0)
                {
                    float netForce = currentPushForce - pressDownForce;
                    Debug.Log($"[BossController] 角力: 下压={pressDownForce:F0}, 上推={currentPushForce:F0}, 净力={netForce:F0}, Y速度={rb.velocity.y:F2}");
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void InitializePositions()
        {
            // 生成位置（屏幕上方外侧）
            spawnPosition = new Vector3(0f, 10f, 0f);
            transform.position = spawnPosition;
            
            // 战斗锚点
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
            EnterState(newState);
        }
        
        private void ExitState(BossState state)
        {
            if (stateCoroutine != null)
            {
                StopCoroutine(stateCoroutine);
                stateCoroutine = null;
            }
            
            // 重置状态相关变量
            if (state == BossState.Charge)
            {
                chargeInterrupted = false;
                if (redBodyEffect != null) redBodyEffect.SetActive(false);
            }
            else if (state == BossState.Press)
            {
                isPressing = false;
                accumulatedPushForce = Vector2.zero;
                isBeingPushed = false;
                if (redBodyEffect != null) redBodyEffect.SetActive(false);
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
                    stateCoroutine = StartCoroutine(SummonRoutine());
                    break;
                case BossState.Charge:
                    stateCoroutine = StartCoroutine(ChargeRoutine());
                    break;
                case BossState.Press:
                    stateCoroutine = StartCoroutine(PressRoutine());
                    break;
                case BossState.Stun:
                    stateCoroutine = StartCoroutine(StunRoutine());
                    break;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Spawn (入场)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator SpawnRoutine()
        {
            float duration = config != null ? config.spawnDuration : 2.5f;
            
            // 眼睛闭合
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
            
            // 咆哮 + 震动
            if (showDebugInfo) Debug.Log("[BossController] BOSS 咆哮！");
            // 【新增】播放咆哮音效
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
            // 眼睛闭合
            if (eyeController != null) eyeController.Close();
            
            // 计算Idle时长
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
            float moveSpeed = config != null ? config.idleMoveSpeed : 1.5f;
            pollutionTimer = 0f;
            float pollutionInterval = config != null ? config.pollutionInterval : 4f;
            
            while (idleTimer < currentIdleDuration)
            {
                idleTimer += Time.deltaTime;
                
                // ═══ 检查召唤冷却（可打断Idle）═══
                if (summonCooldownReady)
                {
                    if (showDebugInfo) Debug.Log("[BossController] ⏰ 召唤冷却完成！打断Idle进入Summon");
                    ChangeState(BossState.Summon);
                    yield break;
                }
                
                // ═══ 污秽喷吐 ═══
                pollutionTimer += Time.deltaTime;
                if (pollutionTimer >= pollutionInterval)
                {
                    pollutionTimer = 0f;
                    FirePollutionProjectile();
                }
                
                // ═══ 水平游走 ═══
                float currentX = transform.position.x;
                float newX = Mathf.MoveTowards(currentX, idleMoveTargetX, moveSpeed * Time.deltaTime);
                transform.position = new Vector3(newX, transform.position.y, transform.position.z);
                
                if (Mathf.Abs(newX - idleMoveTargetX) < 0.1f)
                {
                    SetNextIdleMoveTarget();
                }
                
                yield return null;
            }
            
            // Idle 结束，选择主动技能（50/50 Charge 或 Press）
            ChooseActiveSkill();
        }
        
        private void SetNextIdleMoveTarget()
        {
            idleMoveTargetX = Random.Range(screenMinX, screenMaxX);
        }
        
        /// <summary>
        /// 选择主动技能（Charge 或 Press，各50%）
        /// </summary>
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
            // 身体震动效果
#if DOTWEEN
            if (bodyTransform != null)
            {
                float intensity = config != null ? config.summonShakeIntensity : 0.1f;
                shakeTweener = bodyTransform.DOShakePosition(duration, intensity, 30);
            }
#endif
            
            yield return new WaitForSeconds(duration);
            // 【新增】播放召唤音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBossSummon();
            }
            // 生成小怪（左右对称）
            SpawnMinions();
            
            // 重置冷却
            ResetSummonCooldown();
            
            // 返回 Idle
            ChangeState(BossState.Idle);
        }
        
        /// <summary>
        /// 记录受伤（供 BossHealth 调用）
        /// </summary>
        public void OnDamageReceived()
        {
            // 累加连续受伤时间
            // 如果每帧都受伤，计时器会持续增加
            // 实际实现：在 Update 中递增，这里重置"未受伤"标记
            lastDamageTime = Time.time;
        }
        /// <summary>
        /// 更新连续受伤计时器（在 Update 中调用）
        /// </summary>
        private void UpdateContinuousDamageTimer()
        {
            float timeSinceLastDamage = Time.time - lastDamageTime;
            
            if (timeSinceLastDamage < DAMAGE_GAP_THRESHOLD)
            {
                // 持续受伤中
                continuousDamageTimer += Time.deltaTime;
            }
            else
            {
                // 超时，重置计时器
                if (continuousDamageTimer > 0f)
                {
                    continuousDamageTimer = 0f;
                    if (showDebugInfo)
                    {
                        Debug.Log("[BossController] 连续受伤中断，计时器重置");
                    }
                }
            }
        }
        /// <summary>
        /// 生成小怪（支持狂暴状态动态调整）
        /// HP > 30%: 4只 Rusher
        /// HP < 30%: 6只 Rusher + 速度加成
        /// </summary>
        private void SpawnMinions()
        {
            if (EnemyPoolManager.Instance == null) return;
            
            Vector2 leftOffset = config != null ? config.summonLeftOffset : new Vector2(-3f, -1f);
            Vector2 rightOffset = config != null ? config.summonRightOffset : new Vector2(3f, -1f);
            
            // 根据血量决定召唤数量
            int perSide;
            float speedBonus = 1f;
            
            if (IsEnraged) // HP < 30%
            {
                perSide = 3; // 每侧3只 = 共6只
                speedBonus = 1.5f; // 速度+50%
                
                if (showDebugInfo)
                {
                    Debug.Log("[BossController] 🔥 狂暴召唤！数量增加，速度加快！");
                }
            }
            else
            {
                perSide = config != null ? config.summonRusherPerSide : 2; // 每侧2只 = 共4只
            }
            
            // 左侧
            for (int i = 0; i < perSide; i++)
            {
                if (EnemyPoolManager.Instance.IsAtGlobalCapacity) break;
                Vector3 pos = transform.position + new Vector3(leftOffset.x, leftOffset.y + i * 0.5f, 0);
                EnemyBlob enemy = EnemyPoolManager.Instance.Spawn(EnemyType.Rusher, pos);
                
                // 狂暴状态：加速
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
            
            // 右侧
            for (int i = 0; i < perSide; i++)
            {
                if (EnemyPoolManager.Instance.IsAtGlobalCapacity) break;
                Vector3 pos = transform.position + new Vector3(rightOffset.x, rightOffset.y + i * 0.5f, 0);
                EnemyBlob enemy = EnemyPoolManager.Instance.Spawn(EnemyType.Rusher, pos);
                
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
        /// <summary>
        /// 检查战术召唤（玩家太安逸时触发）
        /// 逻辑：如果 BOSS 连续 5秒 受到伤害（玩家一直站桩输出），
        ///       强制打断 Idle，触发一次召唤打断玩家节奏
        /// </summary>
        private void CheckTacticalSummon()
        {
            // 只在 Idle 状态检查
            if (currentState != BossState.Idle) return;
            
            // 冷却中
            if (tacticalSummonCooldown) return;
            
            // 检查是否达到阈值
            if (continuousDamageTimer >= TACTICAL_SUMMON_THRESHOLD)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[BossController] ⚡ 战术召唤触发！玩家输出太安逸了！");
                }
                
                // 重置计时器
                continuousDamageTimer = 0f;
                
                // 进入冷却
                tacticalSummonCooldown = true;
                StartCoroutine(TacticalSummonCooldownRoutine());
                
                // 强制进入召唤状态（不占用常规 CD）
                ChangeState(BossState.Summon);
            }
        }
        
        /// <summary>
        /// 战术召唤冷却协程
        /// </summary>
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
        
        /// <summary>
        /// 发射污秽投射物（散射版）
        /// </summary>
        private void FirePollutionProjectile()
        {
            GameObject prefab = config != null ? config.pollutionProjectilePrefab : null;
            if (prefab == null)
            {
                if (showDebugInfo) Debug.LogWarning("[BossController] Pollution Prefab 未设置！");
                return;
            }
            
            int maxCount = config != null ? config.pollutionMaxCount : 3;
            int burstCount = config != null ? config.pollutionBurstCount : 3;
            float spreadAngle = config != null ? config.pollutionSpreadAngle : 30f;
            
            // 清理已销毁的引用
            activePollutionBalls.RemoveAll(b => b == null);

            // 计算需要清理多少个旧球来为新球腾出空间
            int newTotalCount = activePollutionBalls.Count + burstCount;
            while (newTotalCount > maxCount && activePollutionBalls.Count > 0)
            {
                BossPollutionProjectile oldest = activePollutionBalls[0];
                activePollutionBalls.RemoveAt(0);
                if (oldest != null)
                {
                    oldest.ForceDestroy();
                }
                newTotalCount--;
            }

            // V3.0: 发射时触发眨眼
            float blinkDuration = config != null ? config.blinkDuration : 0.75f;
            if (eyeController != null)
            {
                eyeController.Blink(blinkDuration);
            }

            // 计算发射位置
            float bodyRadius = 1.5f;
            if (bossHealth != null && bossHealth.BodyCollider != null)
            {
                CircleCollider2D circleCol = bossHealth.BodyCollider as CircleCollider2D;
                if (circleCol != null)
                {
                    bodyRadius = circleCol.radius * transform.lossyScale.x;
                }
            }
            Vector3 spawnPos = transform.position + Vector3.down * (bodyRadius + 0.5f);

            // 计算每颗弹的角度
            float startAngle = -spreadAngle / 2f;
            float angleStep = burstCount > 1 ? spreadAngle / (burstCount - 1) : 0f;

            for (int i = 0; i < burstCount; i++)
            {
                float angle = burstCount == 1 ? 0f : startAngle + angleStep * i;

                // 生成投射物
                GameObject projectileObj = Instantiate(prefab, spawnPos, Quaternion.identity);

                BossPollutionProjectile projectile = projectileObj.GetComponent<BossPollutionProjectile>();
                if (projectile != null)
                {
                    // V3.0: 初始化带物理参数
                    if (config != null)
                    {
                        projectile.InitializeV3(
                            config.pollutionSpeed,
                            config.pollutionTurnSpeed,
                            config.pollutionShieldDamage,
                            config.pollutionLifetime,
                            config.pollutionBallHP,
                            config.pollutionBallMass,
                            this
                        );
                        
                        // 设置初始方向（散射角度）
                        projectile.SetInitialDirection(angle);
                    }

                    // 注册到管理列表
                    RegisterPollutionBall(projectile);
                }
            }

            // 播放喷吐音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBossSpit();
            }

            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 💜 污秽喷吐！发射 {burstCount} 颗，散射角度 {spreadAngle}°");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Charge (野蛮冲撞) - 主动技能A【快招】
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator ChargeRoutine()
        {
            chargeInterrupted = false;
            chargeHitCount = 0;

            float telegraphDuration = config != null ? config.chargeTelegraphDuration : 1.0f;
            float windupDistance = config != null ? config.chargeWindupDistance : 0.5f;
            
            float speedMultiplier = GetChargeSpeedMultiplier();
            // ═══════════════════════════════════════════════════
            // Phase 1: Telegraph (预警) - 1.0s
            // 视觉：全身高频红光闪烁，身体后缩
            // 眼睛：睁开（弱点暴露）
            // ═══════════════════════════════════════════════════
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] ⚠️ Charge 蓄力开始！眼睛睁开+红光闪烁！");
            }
            // 【新增】播放预警音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBossChargeWarning();
            }
            // 眼睛猛然睁开
            if (eyeController != null) eyeController.Open();
            
            // 显示红色特效
            if (redBodyEffect != null) redBodyEffect.SetActive(true);
            
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
            
            // 蓄力等待（可被打断）
            float telegraphElapsed = 0f;
            while (telegraphElapsed < telegraphDuration && !chargeInterrupted)
            {
                telegraphElapsed += Time.deltaTime;
                yield return null;
            }
            
            // 检查是否被打断
            if (chargeInterrupted)
            {
                OnChargeInterrupted();
                yield break;
            }
            
            // ═══════════════════════════════════════════════════
            // Phase 2: Dash (冲锋) - Lerp快速移动
            // ═══════════════════════════════════════════════════
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🔴 Charge 冲锋！瞬间高速冲向塔！");
            }
            // 【新增】播放破空音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBossDash();
            }
            float baseDashDuration = config != null ? config.chargeDashDuration : 0.3f;
            float dashDuration = baseDashDuration / speedMultiplier;  // V3.0: Buff越多，冲得越快
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
                t = t * t; // EaseInQuad
                transform.position = Vector3.Lerp(dashStart, dashTarget, t);
                yield return null;
            }
            transform.position = dashTarget;
#endif
            
            // ═══════════════════════════════════════════════════
            // Phase 3: 结算 - 撞塔
            // ═══════════════════════════════════════════════════
            
            OnChargeHitPlayer();
        }
        
        /// <summary>
        /// 打断Charge蓄力（由 LaserController 调用）
        /// 条件：Impact Lv.5 或 大招
        /// </summary>
        public void InterruptCharge()
        {
            if (currentState == BossState.Charge && !chargeInterrupted)
            {
                chargeInterrupted = true;
                
                if (showDebugInfo)
                {
                    Debug.Log("[BossController] ⚡ Charge 被 Impact Lv.5 打断！");
                }
            }
        }
        
        /// <summary>
        /// Charge撞击玩家
        /// </summary>
        private void OnChargeHitPlayer()
        {
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 💥 Charge 撞击玩家！");
            }
            
            // 对玩家造成伤害
            float damage = config != null ? config.chargeHitDamage : 300f;
            ApplyDamageToPlayer(damage);
            
            // 显示伤害飘字
            ShowCounterText($"-{(int)damage}");
            
            // 屏幕震动
            float shakeIntensity = config != null ? config.chargeHitShakeIntensity : 0.8f;
            float shakeDuration = config != null ? config.chargeHitShakeDuration : 0.3f;
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.ImpactShake(Vector2.down, shakeIntensity, shakeDuration);
            }
            
            // 弹回
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
            
            // 短暂僵直后返回Idle
            if (eyeController != null) eyeController.Close();
            yield return new WaitForSeconds(1.0f);
            
            ChangeState(BossState.Idle);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Press (重力碾压) - 主动技能B【慢招/角力】
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator PressRoutine()
        {
            isPressing = false;
            accumulatedPushForce = Vector2.zero;
            clashTimer = 0f;                    // 【新增】重置角力计时
            isFrictionDamageActive = false;     // 【新增】重置摩擦状态
            frictionDamageAccumulator = 0f;     // 【新增】重置摩擦伤害累计
            
            // ═══════════════════════════════════════════════════
            // Phase 1: Jump Scare (突进贴脸) - 0.5s
            // 眼睛：闭合
            // 动作：瞬移/极速冲到塔前
            // ═══════════════════════════════════════════════════
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🟣 Press Phase 1: 突进贴脸！（闭眼）");
            }
            // 【新增】播放破空音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBossDash();
            }
            // 眼睛保持闭合
            if (eyeController != null) eyeController.Close();
            
            float jumpDuration = config != null ? config.pressJumpDuration : 0.5f;
            float hoverY = config != null ? config.pressHoverY : -5f;
            Vector3 hoverPosition = new Vector3(0f, hoverY, 0f); // 塔前悬停
            
#if DOTWEEN
            bool jumpComplete = false;
            moveTweener = transform.DOMove(hoverPosition, jumpDuration)
                .SetEase(Ease.OutExpo) // 极速然后急刹车
                .OnComplete(() => jumpComplete = true);
            while (!jumpComplete) yield return null;
#else
            float jumpElapsed = 0f;
            Vector3 jumpStart = transform.position;
            while (jumpElapsed < jumpDuration)
            {
                jumpElapsed += Time.deltaTime;
                float t = 1f - Mathf.Pow(1f - jumpElapsed / jumpDuration, 3f); // EaseOutCubic
                transform.position = Vector3.Lerp(jumpStart, hoverPosition, t);
                yield return null;
            }
            transform.position = hoverPosition;
#endif
            
            // ═══════════════════════════════════════════════════
            // Phase 2: The Glare (施压) - 1.5s
            // 视觉：悬停不动，尾焰喷射，颜色转深紫/深红
            // 眼睛：缓缓睁大（弱点暴露）
            // ═══════════════════════════════════════════════════
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🟣 Press Phase 2: 施压！眼睛缓缓睁开...");
            }
            
            float glareDuration = config != null ? config.pressGlareDuration : 1.5f;
            
            // 显示红色特效
            if (redBodyEffect != null) redBodyEffect.SetActive(true);
            
            // 眼睛缓慢睁开
            if (eyeController != null)
            {
                eyeController.OpenSlowly(glareDuration);
            }
            
            yield return new WaitForSeconds(glareDuration);
            
            // ═══════════════════════════════════════════════════
            // Phase 3: Crushing (角力) - 持续直到胜负
            // 眼睛：完全睁开（可被攻击）
            // 物理：Boss 向下压，玩家用激光向上推
            // ═══════════════════════════════════════════════════

            if (showDebugInfo)
            {
                Debug.Log("[BossController] Press Phase 3: 开始碾压！角力进行中...");
            }
            pressOverloadDamage = 0f;
            pressOverloadTimer = 0f;
            
            isPressing = true;
            isPressPhase3Active = true;  // 激活 FixedUpdate 中的物理处理

// 设置下压力（由 FixedUpdate 施加）
            pressDownForce = config != null ? config.GetPressForce(HealthPercent) : 100f;

            float safeLineY = config != null ? config.pressSafeLineY : 3.5f;
            float hitLineY = config != null ? config.pressHitLineY : -10f;
            float maxDuration = config != null ? config.pressMaxDuration : 15f;

            float crushingElapsed = 0f;

            while (crushingElapsed < maxDuration)
            {
                crushingElapsed += Time.deltaTime;
                clashTimer += Time.deltaTime;  // 【新增】角力总计时
                pressOverloadTimer += Time.deltaTime;
                float overloadWindow = config != null ? config.pressOverloadWindow : 1.5f;
                if (pressOverloadTimer >= overloadWindow)
                {
                    pressOverloadDamage = 0f;
                    pressOverloadTimer = 0f;
                }
                // 【新增】角力超时检测（Boss 疲劳撤退）
                float maxClash = config != null ? config.maxClashDuration : 6f;
                if (clashTimer >= maxClash && isFrictionDamageActive)
                {
                    isPressPhase3Active = false;
                    pressDownForce = 0f;
                    OnPressExhausted();  // 疲劳撤退
                    yield break;
                }
    
                // 注意：不再在这里施加力！力在 FixedUpdate 中统一施加
    
                // ─────────────────────────────────────────────────
                // 胜利条件：被推回安全线上方
                // ─────────────────────────────────────────────────
                if (transform.position.y > safeLineY)
                {
                    isPressPhase3Active = false;
                    pressDownForce = 0f;
                    OnPressPushedBack();
                    yield break;
                }
    
                // ─────────────────────────────────────────────────
                // 失败条件：撞到玩家
                // ─────────────────────────────────────────────────
                if (transform.position.y < hitLineY)
                {
                    isPressPhase3Active = false;
                    pressDownForce = 0f;
                    OnPressHitPlayer();
                    yield break;
                }
    
                yield return null;
            }

// 超时
            isPressPhase3Active = false;
            pressDownForce = 0f;

            if (showDebugInfo) Debug.Log("[BossController] Press 超时");
            OnPressTimeout();
        }
        
        /// <summary>
        /// 【玩家胜利】Press被推回安全线
        /// </summary>
        private void OnPressPushedBack()
        {
// 重置角力状态
            isPressPhase3Active = false;
            pressDownForce = 0f;
            currentPushForce = 0f;
            isReceivingLaserHit = false;
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🎉 Press 被推回！玩家角力胜利！");
            }
            
            rb.velocity = Vector2.zero;
            isPressing = false;
            
            ShowCounterText("STOPPED!");
            
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.3f, 0.2f);
            }
            
            // 进入僵直（奖励时间），眼睛保持睁开
            ChangeState(BossState.Stun);
        }
        
        /// <summary>
        /// 【玩家失败】Press撞到玩家
        /// </summary>
        private void OnPressHitPlayer()
        {
// 重置角力状态
            isPressPhase3Active = false;
            pressDownForce = 0f;
            currentPushForce = 0f;
            isReceivingLaserHit = false;
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 💥 Press 撞击玩家！");
            }
            
            rb.velocity = Vector2.zero;
            isPressing = false;
            
            // 造成伤害
            float damage = config != null ? config.chargeHitDamage : 300f;
            ApplyDamageToPlayer(damage);
            
            ShowCounterText($"-{(int)damage}");
            
            float shakeIntensity = config != null ? config.chargeHitShakeIntensity : 0.8f;
            float shakeDuration = config != null ? config.chargeHitShakeDuration : 0.3f;
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.ImpactShake(Vector2.down, shakeIntensity, shakeDuration);
            }
            
            // 弹回
            StartCoroutine(PressBounceBackRoutine());
        }
        
        /// <summary>
        /// Press超时
        /// </summary>
        private void OnPressTimeout()
        {
// 重置角力状态
            isPressPhase3Active = false;
            pressDownForce = 0f;
            currentPushForce = 0f;
            isReceivingLaserHit = false;
            rb.velocity = Vector2.zero;
            isPressing = false;
            StartCoroutine(PressBounceBackRoutine());
        }
        /// <summary>
        /// 【新增】Boss 角力疲劳，主动撤退
        /// </summary>
        private void OnPressExhausted()
        {
            // 重置状态
            isPressPhase3Active = false;
            pressDownForce = 0f;
            currentPushForce = 0f;
            isReceivingLaserHit = false;
            isFrictionDamageActive = false;
            frictionDamageAccumulator = 0f;
    
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 😤 Boss 角力疲劳！主动撤退！");
            }
    
            // 结束摩擦伤害事件
            GameEvents.TriggerBossFrictionEnd();
    
            rb.velocity = Vector2.zero;
            isPressing = false;
    
            // 显示状态文字
            ShowCounterText("EXHAUSTED!");
    
            // 屏幕震动（轻微）
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.2f, 0.15f);
            }
    
            // 快速飞回并进入僵直
            StartCoroutine(ExhaustedRetreatRoutine());
        }

        private IEnumerator ExhaustedRetreatRoutine()
        {
            float duration = 0.4f;  // 快速撤退
    
            // 飞回锚点上方一点（表示疲劳后退）
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
    
            // 进入僵直（奖励时间）- 眼睛睁开（弱点暴露）
            if (eyeController != null) eyeController.Open();
    
            ChangeState(BossState.Stun);
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
        // 角力物理系统 - 公共接口（供 LaserController 调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 接收来自激光的推力（仅在Press角力阶段生效）
        /// </summary>
        public void ApplyLaserPushForce(float pushForce)
        {
            if (!isPressing) return;
    
            // 更新推力值和命中时间
            accumulatedPushForceThisTick += pushForce;
            pushForceUpdatedThisTick = true;
            lastLaserHitTime = Time.time;
    
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 收到推力更新: {currentPushForce:F2}");
            }
        }
        
        /// <summary>
        /// 计算激光对Boss的推力大小
        /// </summary>
        public float CalculatePushForce(int impactLevel, int wideLevel)
        {
            float baseForce = config != null ? config.baseLaserPushForce : 120f;
    
            float impactMultiplier = config != null ? config.GetPushMultiplier(impactLevel) : 0.4f;
            float wideMultiplier = config != null ? config.GetWidePushMultiplier(wideLevel) : 0.4f;
    
            // 取较大值
            float multiplier = Mathf.Max(impactMultiplier, wideMultiplier);
    
            return baseForce * multiplier;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Stun (僵直) - 奖励时间
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator StunRoutine()
        {
            // 根据来源决定僵直时长
            float duration;
            if (stunDurationOverride > 0)
            {
                duration = stunDurationOverride;
                stunDurationOverride = -1f;  // 重置
        
                if (showDebugInfo)
                {
                    Debug.Log($"[BossController] 💫 进入短僵直！时长: {duration}s");
                }
            }
            else
            {
                // 原有逻辑
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
            
            // 眼睛保持睁开（最大化玩家输出）
            if (eyeController != null) eyeController.Open();
            
            // 身体变暗
            SetBodyDarken(true);
            
            yield return new WaitForSeconds(duration);
            
            // 恢复
            SetBodyDarken(false);
            
            // 返回战斗锚点或直接Idle
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
        /// <summary>
        /// 【新增】结束本tick的推力累加，应用到实际推力
        /// 由 LaserController 在每tick伤害检测结束后调用
        /// </summary>
        public void FinalizePushForceThisTick()
        {
            if (!isPressing) return;

            if (pushForceUpdatedThisTick)
            {
                // 应用推力上限
                float maxForce = config != null ? config.maxTotalPushForce : 300f;
                currentPushForce = Mathf.Min(accumulatedPushForceThisTick, maxForce);
                isReceivingLaserHit = true;

                if (showDebugInfo && accumulatedPushForceThisTick > maxForce)
                {
                    Debug.Log($"[BossController] 推力超限! 原始={accumulatedPushForceThisTick:F0}, 限制后={currentPushForce:F0}");
                }
            }

            // 重置本tick累加器
            accumulatedPushForceThisTick = 0f;
            pushForceUpdatedThisTick = false;
        }
        /// <summary>
        /// 计算副激光的推力（设为0）
        /// </summary>
        public float CalculateSubLaserPushForce()
        {
            return 0f;
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 角力摩擦伤害系统 【新增】
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 更新摩擦伤害（Boss 压在护盾上时）
        /// </summary>
        private void UpdateFrictionDamage()
        {
            float triggerY = config != null ? config.frictionTriggerY : -8.5f;
            
            // 检测是否进入摩擦伤害区域
            if (transform.position.y <= triggerY)
            {
                if (!isFrictionDamageActive)
                {
                    isFrictionDamageActive = true;
                    OnFrictionStart();
                }
                
                // 累计伤害
                float dps = config != null ? config.frictionDamagePerSecond : 50f;
                frictionDamageAccumulator += dps * Time.fixedDeltaTime;
                
                // 每累计1点伤害就应用一次（避免浮点误差）
                if (frictionDamageAccumulator >= 1f)
                {
                    int damageToApply = Mathf.FloorToInt(frictionDamageAccumulator);
                    ApplyFrictionDamage(damageToApply);
                    frictionDamageAccumulator -= damageToApply;
                }
            }
            else
            {
                if (isFrictionDamageActive)
                {
                    isFrictionDamageActive = false;
                    OnFrictionEnd();
                }
            }
        }

        /// <summary>
        /// 开始摩擦伤害
        /// </summary>
        private void OnFrictionStart()
        {
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🔥 摩擦伤害开始！Boss 压在护盾上！");
            }
            
            // 触发视觉效果事件（供 VFX 系统订阅）
            GameEvents.TriggerBossFrictionStart();
        }

        /// <summary>
        /// 结束摩擦伤害
        /// </summary>
        private void OnFrictionEnd()
        {
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 摩擦伤害结束");
            }
            
            frictionDamageAccumulator = 0f;
            GameEvents.TriggerBossFrictionEnd();
        }

        /// <summary>
        /// 应用摩擦伤害到护盾
        /// </summary>
        private void ApplyFrictionDamage(int damage)
        {
            if (cachedShieldController == null)
            {
                cachedShieldController = FindObjectOfType<ShieldController>();
            }
            
            if (cachedShieldController != null && cachedShieldController.CurrentShieldHP > 0)
            {
                cachedShieldController.TakeBossDamage(damage);
                
                if (showDebugInfo && Time.frameCount % 30 == 0)
                {
                    Debug.Log($"[BossController] 🔥 摩擦伤害: {damage}, 护盾剩余: {cachedShieldController.CurrentShieldHP}");
                }
            }
        }
         /// <summary>
        /// 记录受击次数（供 BossHealth 调用，用于 Charge 频率打断）
        /// </summary>
         public void OnHitReceived()
         {
             // 记录受伤时间（用于战术召唤等）
             lastDamageTime = Time.time;
         }

        /// <summary>
        /// 记录伤害值（重载版本，用于 Press 过载检测）
        /// </summary>
        public void OnDamageReceived(float damage)
        {
            // 原有功能：记录受伤时间
            lastDamageTime = Time.time;
            
            // V3.0: Press 过载累计
            if (currentState == BossState.Press && isPressing)
            {
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
        }
        /// <summary>
        /// Press 过载触发（玩家DPS过高，Boss撤退）
        /// </summary>
        private void OnPressOverload()
        {
            // 重置状态
            isPressPhase3Active = false;
            pressDownForce = 0f;
            currentPushForce = 0f;
            isReceivingLaserHit = false;
            isFrictionDamageActive = false;
            frictionDamageAccumulator = 0f;
            pressOverloadDamage = 0f;
    
            rb.velocity = Vector2.zero;
            isPressing = false;
    
            // 结束摩擦伤害事件
            GameEvents.TriggerBossFrictionEnd();
    
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🔥 Press 过载！Boss 核心过热撤退！");
            }
    
            ShowCounterText("OVERLOAD!");
    
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.4f, 0.2f);
            }
    
            // 进入短僵直
            EnterShortStun();
        }
        /// <summary>
        /// 获取连体Buff层数（Rusher数量，上限5）
        /// </summary>
        public int GetLinkedBuffStacks()
        {
            if (EnemyPoolManager.Instance == null) return 0;
            
            int rusherCount = EnemyPoolManager.Instance.GetActiveCount(EnemyType.Rusher);
            int maxStacks = config != null ? config.linkedBuffMaxStacks : 5;
            
            return Mathf.Min(rusherCount, maxStacks);
        }

        /// <summary>
        /// 获取Charge速度倍率（含连体Buff加成）
        /// </summary>
        public float GetChargeSpeedMultiplier()
        {
            int stacks = GetLinkedBuffStacks();
            float bonusPerStack = config != null ? config.linkedBuffChargeSpeedPerStack : 0.1f;
            
            return 1f + (stacks * bonusPerStack);
        }

        /// <summary>
        /// 进入短僵直（1.5秒，用于毒球反弹/过载撤退）
        /// </summary>
        public void EnterShortStun()
        {
            float shortDuration = config != null ? config.shortStunDuration : 1.5f;
            stunDurationOverride = shortDuration;
            ChangeState(BossState.Stun);
        }

        /// <summary>
        /// 注册污秽球（用于数量管理）
        /// </summary>
        public void RegisterPollutionBall(BossPollutionProjectile ball)
        {
            if (ball != null && !activePollutionBalls.Contains(ball))
            {
                activePollutionBalls.Add(ball);
            }
        }

        /// <summary>
        /// 注销污秽球
        /// </summary>
        public void UnregisterPollutionBall(BossPollutionProjectile ball)
        {
            if (ball != null)
            {
                activePollutionBalls.Remove(ball);
            }
        }

        private void OnChargeInterrupted()
        {
            if (showDebugInfo) Debug.Log("[BossController] 💥 Charge 蓄力被打断！进入僵直！");
            ShowCounterText("INTERRUPTED!");
            // 关闭红色特效
            if (redBodyEffect != null) redBodyEffect.SetActive(false);
            ChangeState(BossState.Stun);
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 工具方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ApplyDamageToPlayer(float damage)
        {
            ShieldController shield = FindObjectOfType<ShieldController>();
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
        
        private void ShowCounterText(string text)
        {
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.ShowStatus(transform.position, text);
            }
        }
        
        /// <summary>
        /// 强制进入僵直（调试用）
        /// </summary>
        public void ForceStun()
        {
            ChangeState(BossState.Stun);
        }
        
        private int GetCurrentMobCount()
        {
            return EnemyPoolManager.Instance != null ? EnemyPoolManager.Instance.TotalActiveEnemies : 0;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 150, 280, 250));
            GUILayout.Label("=== Boss Controller (重构版) ===");
            GUILayout.Label($"State: {currentState}");
            GUILayout.Label($"HP: {HealthPercent:P1} | Enraged: {IsEnraged}");
            GUILayout.Label($"Eye: {(eyeController != null ? eyeController.CurrentState.ToString() : "N/A")}");
            GUILayout.Label($"Mobs: {GetCurrentMobCount()}");
            GUILayout.Label($"IsPressing: {isPressing}");
            GUILayout.Label($"Position Y: {transform.position.y:F2}");
            GUILayout.Label($"Velocity Y: {(rb != null ? rb.velocity.y.ToString("F2") : "N/A")}");
            
            GUILayout.Space(5);
            GUILayout.Label($"Summon CD: {summonCooldownTimer:F1}s {(summonCooldownReady ? "✓ READY" : "")}");
            GUILayout.Label($"Pollution: {pollutionTimer:F1}s");
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("Force Stun")) ForceStun();
            if (GUILayout.Button("Force Charge")) ChangeState(BossState.Charge);
            if (GUILayout.Button("Force Press")) ChangeState(BossState.Press);
            
            GUILayout.EndArea();
        }
        
        private void OnDrawGizmosSelected()
        {
            // 战斗锚点
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(battleAnchorPosition, 0.5f);
            
            // 移动范围
            Gizmos.color = Color.cyan;
            float y = battleAnchorPosition.y;
            Gizmos.DrawLine(new Vector3(screenMinX, y, 0), new Vector3(screenMaxX, y, 0));
            
            // Press安全线
            float safeY = config != null ? config.pressSafeLineY : 3.5f;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(new Vector3(-10, safeY, 0), new Vector3(10, safeY, 0));
            
            // Press/Charge撞击线
            float hitY = config != null ? config.pressHitLineY : -10f;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(-10, hitY, 0), new Vector3(10, hitY, 0));
            
            // Press悬停位置
            float hoverY = config != null ? config.pressHoverY : -5f;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(new Vector3(-5, hoverY, 0), new Vector3(5, hoverY, 0));
        }
#endif
    }
}