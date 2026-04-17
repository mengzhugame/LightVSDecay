// ============================================================
// BaseBossController.cs
// 文件位置: Assets/Scripts/Logic/Boss/BaseBossController.cs
// 用途：所有Boss的行为基类 — 状态机、霸体/冰冻/狂暴系统、Charge/Press技能均在此
// Ch1 BossController / Ch2 VolcanoBossController / Ch3 FrostCoreBossController 均继承此类
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
    /// <summary>Boss 状态飘字类型</summary>
    public enum BossStatusTextType
    {
        Unstoppable, Frozen, Enraged, Interrupted, Countered, Overload, Exhausted
    }

    /// <summary>Boss 行为状态</summary>
    public enum BossState
    {
        Spawn,   // 入场
        Idle,    // 待机/游走
        Summon,  // 召唤爪牙
        Charge,  // 野蛮冲撞
        Press,   // 重力碾压
        Stun,    // 僵直
        Frozen   // 冰冻
    }

    /// <summary>
    /// 所有Boss的抽象基类。
    /// 包含共享系统：状态机、冰冻/霸体/控制递减、狂暴、Charge、Press。
    /// 子类实现 ExecuteSummonBehavior()，并可重写虚方法自定义行为。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BossHealth))]
    public abstract class BaseBossController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("配置")]
        [SerializeField] protected BossConfig config;

        [Header("组件引用")]
        [SerializeField] protected BossEyeController eyeController;
        [SerializeField] protected Transform bodyTransform;
        [SerializeField] protected SpriteRenderer[] bodyRenderers;
        [SerializeField] protected GameObject redBodyEffect;

        [Header("调试")]
        [SerializeField] protected bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态（共享）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected BossState currentState = BossState.Spawn;
        protected BossState stateBeforeFrozen = BossState.Idle;
        protected Rigidbody2D rb;
        protected BossHealth bossHealth;

        // 位置缓存
        protected Vector3 spawnPosition;
        protected Vector3 battleAnchorPosition;
        protected float screenMinX, screenMaxX;
        protected Color[] originalColors;

        // Idle 状态
        private float idleTimer = 0f;
        private float currentIdleDuration;
        private float idleMoveTargetX;

        // Summon 冷却
        private float summonCooldownTimer = 0f;
        private bool summonCooldownReady = false;

        // Charge 状态
        protected bool chargeInterrupted = false;
        private int chargeHitCount = 0;

        // Press 状态
        protected bool isPressing = false;
        private bool isPressPhase3Active = false;
        private float pressDownForce = 0f;
        private Vector2 accumulatedPushForce = Vector2.zero;

        // Press 角力物理
        private float currentPushForce = 0f;
        private float lastLaserHitTime = 0f;
        private bool isReceivingLaserHit = false;
        private const float laserHitTimeout = 0.15f;
        private float accumulatedPushForceThisTick = 0f;
        private bool pushForceUpdatedThisTick = false;

        // Press 摩擦伤害
        private float clashTimer = 0f;
        private bool isFrictionDamageActive = false;
        private float frictionDamageAccumulator = 0f;

        // Press 过载
        private float pressOverloadDamage = 0f;
        private float pressOverloadTimer = 0f;

        // 战术召唤
        private float continuousDamageTimer = 0f;
        private bool tacticalSummonCooldown = false;
        private const float TACTICAL_SUMMON_THRESHOLD = 5f;
        private const float TACTICAL_SUMMON_CD = 10f;

        // 短僵直
        private float stunDurationOverride = -1f;

        // 协程
        protected Coroutine stateCoroutine;

        // 连续受伤检测
        private float lastDamageTime = 0f;
        private const float DAMAGE_GAP_THRESHOLD = 0.5f;

        // 冰冻累积
        private float frostExposureTime = 0f;
        private const float FROST_EXPOSURE_RESET_DELAY = 0.3f;
        private float frostExposureResetTimer = 0f;

        // 控制递减
        private int controlStack = 0;

        // 霸体状态
        private bool isUnstoppable = false;
        private float unstoppableTimer = 0f;

        // 冰冻状态
        private bool isFrozen = false;
        private float frozenTimer = 0f;
        private Vector2 velocityBeforeFrozen;

        protected FrostDebuff frostDebuff;

        // 狂暴状态
        private bool hasTriggeredEnrage = false;
        private bool isEnrageEffectActive = false;

        // 缓存
        private ShieldController cachedShieldController;

#if DOTWEEN
        protected Tweener moveTweener;
        protected Tweener shakeTweener;
#endif

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>场景内当前活跃的 Boss 实例（供 GameManager 死亡演出序列获取位置和特效）</summary>
        public static BaseBossController Current { get; private set; }

        /// <summary>各章节专属死亡特效预制体（由子类通过 Inspector 配置并重写此属性）</summary>
        public virtual GameObject DeathVFXPrefab => null;

        /// <summary>各章节专属入场特效预制体（Boss 落地咆哮时播放的黑线特效，由子类配置）</summary>
        public virtual GameObject SpawnVFXPrefab => null;

        /// <summary>Boss 身体 Transform（供 DOPunchScale 使用；null 时回退到 transform 本身）</summary>
        public Transform BodyTransform => bodyTransform;

        public BossConfig Config => config;
        public SpriteRenderer[] BodyRenderers => bodyRenderers;
        public Color[] OriginalColors => originalColors;
        public BossState CurrentState => currentState;
        public bool IsInChargeTelegraph => currentState == BossState.Charge && !chargeInterrupted && !isUnstoppable;
        public bool IsPressing => currentState == BossState.Press && isPressing;
        public float HealthPercent => bossHealth != null ? bossHealth.HealthPercent : 1f;
        public bool IsEnraged => HealthPercent <= (config != null ? config.rageHealthThreshold : 0.3f);
        public bool IsUnstoppable => isUnstoppable;
        public bool IsFrozen => currentState == BossState.Frozen;
        public int ControlStack => controlStack;
        public bool IsSlowed => frostDebuff != null && frostDebuff.IsSlowed;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 抽象方法（子类必须实现）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>执行召唤实体逻辑（每个Boss召唤不同的爪牙）</summary>
        protected abstract IEnumerator ExecuteSummonBehavior();

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 虚方法（子类可选重写）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>Idle 状态下的被动技能（Ch1: 污秽球; Ch2/Ch3: 无或其他）</summary>
        protected virtual void OnIdlePassiveUpdate(float deltaTime) { }

        /// <summary>进入 Idle 状态时的回调（子类可重置被动计时器等）</summary>
        protected virtual void OnEnterIdle() { }

        /// <summary>选择主动技能（默认50/50 Charge/Press）</summary>
        protected virtual void ChooseActiveSkill()
        {
            if (Random.value < 0.5f)
                ChangeState(BossState.Charge);
            else
                ChangeState(BossState.Press);
        }

        /// <summary>冲撞速度倍率（Ch1: 连体Buff加成; 其他默认1f）</summary>
        protected virtual float GetChargeSpeedMultiplier() => 1f;

        /// <summary>连体Buff伤害减免倍率（Ch1: 基于Rusher数量; 其他默认1f=无减免）</summary>
        public virtual float GetLinkedBuffDamageMultiplier() => 1f;

        /// <summary>Boss初始化完成后的钩子</summary>
        protected virtual void OnBossInitialized() { }

        /// <summary>Update 的额外逻辑钩子</summary>
        protected virtual void OnExtraUpdate() { }

        /// <summary>Charge 冲撞落地后的钩子（可用于在路径上生成遗留物）</summary>
        protected virtual void OnChargeDashComplete(Vector3 startPos, Vector3 endPos) { }

        /// <summary>Press 角力每帧逐帧回调（可用于喷射副弹等持续行为）</summary>
        protected virtual void OnPressTick(float deltaTime) { }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected virtual void Awake()
        {
            Current = this;
            rb = GetComponent<Rigidbody2D>();
            bossHealth = GetComponent<BossHealth>();
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }

        protected virtual void Start()
        {
            InitializePositions();
            CalculateScreenBounds();
            CacheOriginalColors();

            frostDebuff = GetComponent<FrostDebuff>();
            if (frostDebuff == null)
                frostDebuff = gameObject.AddComponent<FrostDebuff>();
            if (bodyRenderers != null && bodyRenderers.Length > 0)
                frostDebuff.SetTargetRenderers(bodyRenderers);

            cachedShieldController = FindObjectOfType<ShieldController>();
            summonCooldownTimer = config != null ? config.summonCooldown : 15f;
            summonCooldownReady = false;

            OnBossInitialized();
            GameEvents.TriggerBossFightStart();
            ChangeState(BossState.Spawn);
        }

        protected virtual void Update()
        {
            UpdateSummonCooldown();
            UpdateContinuousDamageTimer();
            CheckTacticalSummon();
            UpdateUnstoppableTimer();
            UpdateFrostExposureReset();
            CheckEnrageTrigger();
            OnExtraUpdate();

#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.K) && bossHealth != null)
                bossHealth.TakeCoreDamage(1000f, transform.position, false);
            if (Input.GetKeyDown(KeyCode.Alpha1)) ChangeState(BossState.Charge);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ChangeState(BossState.Press);
            if (Input.GetKeyDown(KeyCode.Alpha3)) TryApplyFreeze(2f);
            if (Input.GetKeyDown(KeyCode.Alpha4)) EnterUnstoppable();
#endif
        }

        protected virtual void FixedUpdate()
        {
            if (!isPressing || !isPressPhase3Active) return;

            if (pressDownForce > 0)
                rb.AddForce(Vector2.down * pressDownForce, ForceMode2D.Force);

            if (isReceivingLaserHit && Time.time - lastLaserHitTime > laserHitTimeout)
            {
                isReceivingLaserHit = false;
                currentPushForce = 0f;
            }

            if (isReceivingLaserHit && currentPushForce > 0.01f)
            {
                float actualPushForce = isUnstoppable
                    ? currentPushForce * (config != null ? config.unstoppablePushMultiplier : 0.3f)
                    : currentPushForce;
                rb.AddForce(Vector2.up * actualPushForce, ForceMode2D.Force);
                BattleStatistics.Instance?.MarkBossBeingPushed();
            }

            UpdateFrictionDamage();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 冰冻与霸体系统 — 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void AddFrostExposureTime(float deltaTime)
        {
            if (isUnstoppable || currentState == BossState.Frozen || currentState == BossState.Press) return;

            frostExposureTime += deltaTime;
            frostExposureResetTimer = FROST_EXPOSURE_RESET_DELAY;

            float threshold = config != null ? config.bossFrostFreezeThreshold : 1.0f;
            if (frostExposureTime >= threshold)
            {
                float baseDuration = config != null ? config.bossFrostFreezeDuration : 2.0f;
                TryApplyFreeze(baseDuration);
                frostExposureTime = 0f;
            }
        }

        public float GetFrostExposureTime() => frostExposureTime;
        public void ResetFrostExposureTime() => frostExposureTime = 0f;

        public bool TryApplyFreeze(float baseDuration)
        {
            if (isUnstoppable) { ShowStatusText(BossStatusTextType.Unstoppable); return false; }
            if (currentState == BossState.Press || currentState == BossState.Frozen) return false;

            float actualDuration = CalculateControlDuration(baseDuration);
            if (actualDuration <= 0f) { EnterUnstoppable(); return false; }

            ApplyFreeze(actualDuration);
            return true;
        }

        public bool TryApplyStun(float baseDuration)
        {
            if (isUnstoppable) { ShowStatusText(BossStatusTextType.Unstoppable); return false; }

            float actualDuration = CalculateControlDuration(baseDuration);
            if (actualDuration <= 0f) { EnterUnstoppable(); return false; }

            stunDurationOverride = actualDuration;
            ChangeState(BossState.Stun);
            controlStack++;

            if (showDebugInfo)
                GameLogger.Log($"[{GetType().Name}] 💫 僵直生效 {actualDuration:F2}s, 控制层: {controlStack}");

            return true;
        }

        private float CalculateControlDuration(float baseDuration)
        {
            int triggerCount = config != null ? config.unstoppableTriggerCount : 3;
            if (controlStack >= triggerCount) return 0f;

            float multiplier = controlStack switch
            {
                0 => 1f,
                1 => config != null ? config.controlDiminish2nd : 0.5f,
                2 => config != null ? config.controlDiminish3rd : 0.25f,
                _ => 0f
            };

            if (IsEnraged && config != null)
                multiplier *= config.rageControlDurationMultiplier;

            return baseDuration * multiplier;
        }

        public void EnterUnstoppable()
        {
            if (isUnstoppable) return;
            isUnstoppable = true;
            unstoppableTimer = config != null ? config.unstoppableDuration : 6f;
            controlStack = 0;

            if (redBodyEffect != null) redBodyEffect.SetActive(true);
            ShowStatusText(BossStatusTextType.Unstoppable);
            CameraShake.Instance?.Shake(0.5f, 0.3f);
            AudioManager.Instance?.PlayBossRoar();

            if (showDebugInfo)
                GameLogger.Log($"[{GetType().Name}] 🔴 进入霸体！持续 {unstoppableTimer:F1}s");
        }

        private void ExitUnstoppable()
        {
            if (!isUnstoppable) return;
            isUnstoppable = false;
            unstoppableTimer = 0f;
            controlStack = 0;
            if (redBodyEffect != null && !isEnrageEffectActive)
                redBodyEffect.SetActive(false);

            if (showDebugInfo)
                GameLogger.Log($"[{GetType().Name}] 🔴 霸体结束");
        }

        private void UpdateUnstoppableTimer()
        {
            if (!isUnstoppable) return;
            unstoppableTimer -= Time.deltaTime;
            if (unstoppableTimer <= 0f) ExitUnstoppable();
        }

        private void UpdateFrostExposureReset()
        {
            if (frostExposureResetTimer <= 0f) return;
            frostExposureResetTimer -= Time.deltaTime;
            if (frostExposureResetTimer <= 0f && frostExposureTime > 0f)
                frostExposureTime = 0f;
        }

        private void ApplyFreeze(float duration)
        {
            controlStack++;
            stateBeforeFrozen = currentState;
            frozenTimer = duration;
            frostDebuff?.ApplyFreeze(duration);
            ChangeState(BossState.Frozen);

            if (showDebugInfo)
                GameLogger.Log($"[{GetType().Name}] ❄️ 冰冻 {duration:F2}s, 控制层: {controlStack}");
        }

        public void ApplyFrostSlow(float slowPercent, float duration)
        {
            if (isUnstoppable || currentState == BossState.Frozen || frostDebuff == null) return;
            float multiplier = config != null ? config.bossSlowEffectMultiplier : 0.5f;
            frostDebuff.ApplySlow(slowPercent * multiplier, duration * multiplier);
        }

        public float GetSlowedSpeedMultiplier() => frostDebuff != null ? frostDebuff.SpeedMultiplier : 1f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 狂暴系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void CheckEnrageTrigger()
        {
            if (hasTriggeredEnrage || !IsEnraged) return;
            hasTriggeredEnrage = true;
            StartCoroutine(EnrageTriggerRoutine());
        }

        private IEnumerator EnrageTriggerRoutine()
        {
            if (showDebugInfo)
                GameLogger.Log($"[{GetType().Name}] 🔥 进入狂暴！");

            AudioManager.Instance?.PlayBossRoar();
            float shakeIntensity = config != null ? config.enrageTriggerShakeIntensity : 0.6f;
            float shakeDuration = config != null ? config.enrageTriggerShakeDuration : 0.8f;
            CameraShake.Instance?.Shake(shakeIntensity, shakeDuration);
            ActivateEnrageEffect();

            float pitch = config != null ? config.rageBGMPitch : 1.15f;
            AudioManager.Instance?.SetBGMPitch(pitch);
            FloatingTextManager.Instance?.ShowStatus(transform.position, "ENRAGED!");

            float pauseDuration = config != null ? config.enrageTriggerPauseDuration : 0.5f;
            yield return new WaitForSeconds(pauseDuration);
        }

        protected void ActivateEnrageEffect()
        {
            isEnrageEffectActive = true;
            if (redBodyEffect != null)
            {
                redBodyEffect.SetActive(true);
                SpriteRenderer sr = redBodyEffect.GetComponent<SpriteRenderer>();
                if (sr != null && config != null)
                {
                    Color c = sr.color;
                    c.a = config.enrageRedGlowAlpha;
                    sr.color = c;
                }
            }
        }

        protected void DeactivateEnrageEffect()
        {
            isEnrageEffectActive = false;
            if (redBodyEffect != null) redBodyEffect.SetActive(false);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态机
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected void ChangeState(BossState newState)
        {
            if (showDebugInfo)
                GameLogger.Log($"[{GetType().Name}] 状态: {currentState} → {newState}");

            ExitState(currentState);
            currentState = newState;
            BattleStatistics.Instance?.RecordBossPhase(newState.ToString());
            EnterState(newState);
        }

        protected virtual void ExitState(BossState state)
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
                    redBodyEffect.SetActive(false);
            }
            else if (state == BossState.Press)
            {
                isPressing = false;
                accumulatedPushForce = Vector2.zero;
                if (redBodyEffect != null && !isUnstoppable && !isEnrageEffectActive)
                    redBodyEffect.SetActive(false);
            }
            else if (state == BossState.Frozen)
            {
                isFrozen = false;
                if (frostDebuff != null) frostDebuff.ResetDebuff();
            }

#if DOTWEEN
            if (moveTweener != null && moveTweener.IsActive()) moveTweener.Kill();
            if (shakeTweener != null && shakeTweener.IsActive()) shakeTweener.Kill();
#endif
        }

        protected virtual void EnterState(BossState state)
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
            float width = cam.orthographicSize * 2f * cam.aspect;
            float rangePercent = config != null ? config.idleMoveRangePercent : 0.8f;
            screenMinX = -width * 0.5f * rangePercent;
            screenMaxX = width * 0.5f * rangePercent;
        }

        private void CacheOriginalColors()
        {
            if (bodyRenderers == null || bodyRenderers.Length == 0)
                bodyRenderers = GetComponentsInChildren<SpriteRenderer>();
            originalColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null)
                    originalColors[i] = bodyRenderers[i].color;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Spawn
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator SpawnRoutine()
        {
            float duration = config != null ? config.spawnDuration : 2.5f;
            if (eyeController != null) eyeController.SetStateDirect(BossEyeState.Closed);
            if (redBodyEffect != null) redBodyEffect.SetActive(false);

#if DOTWEEN
            bool moveComplete = false;
            moveTweener = transform.DOMove(battleAnchorPosition, duration)
                .SetEase(Ease.OutQuad).OnComplete(() => moveComplete = true);
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
            AudioManager.Instance?.PlayBossRoar();

            // 播放入场专属特效（黑线冲击特效）
            // 优先使用子类 Inspector 配置的 SpawnVFXPrefab；未配置时从 Resources 加载默认黑线特效
            GameObject spawnVFXPrefab = SpawnVFXPrefab;
            if (spawnVFXPrefab == null)
                spawnVFXPrefab = Resources.Load<GameObject>("Effects/Boss/VFX_BlackLine");

            // ── DEBUG LOG（排查 VFX 不可见）──
            GameLogger.Log($"[VFX_DEBUG] SpawnVFXPrefab 是否为 null：{spawnVFXPrefab == null}，prefab 名称：{spawnVFXPrefab?.name}");
            GameLogger.Log($"[VFX_DEBUG] 生成位置（世界坐标）：{transform.position}，Boss localPosition：{transform.localPosition}");

            if (spawnVFXPrefab != null)
            {
                GameObject vfxInstance = Object.Instantiate(spawnVFXPrefab, transform.position, Quaternion.identity);

                GameLogger.Log($"[VFX_DEBUG] 已 Instantiate：{vfxInstance.name}，activeSelf={vfxInstance.activeSelf}，activeInHierarchy={vfxInstance.activeInHierarchy}");

                // 检查粒子系统状态
                ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
                if (ps == null) ps = vfxInstance.GetComponentInChildren<ParticleSystem>(true);

                if (ps != null)
                {
                    var main = ps.main;
                    float maxLifetime = main.startLifetime.mode == ParticleSystemCurveMode.Constant
                        ? main.startLifetime.constant
                        : main.startLifetime.constantMax;
                    float vfxLifetime = main.duration + maxLifetime;

                    GameLogger.Log($"[VFX_DEBUG] ParticleSystem 找到：{ps.name}，isPlaying={ps.isPlaying}，isEmitting={ps.isEmitting}，duration={main.duration}，maxLifetime={maxLifetime}，计算总时长={vfxLifetime}s");
                    GameLogger.Log($"[VFX_DEBUG] PS 位置={ps.transform.position}，useUnscaledTime={main.simulationSpace}，Time.timeScale={Time.timeScale}");

                    // 检查渲染器
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        GameLogger.Log($"[VFX_DEBUG] Renderer：enabled={renderer.enabled}，sortingLayer={renderer.sortingLayerName}，sortingOrder={renderer.sortingOrder}，material={renderer.material?.name}");
                    }
                    else
                    {
                        GameLogger.LogWarning("[VFX_DEBUG] ParticleSystemRenderer 为 null！");
                    }

                    Object.Destroy(vfxInstance, vfxLifetime);
                }
                else
                {
                    GameLogger.LogWarning($"[VFX_DEBUG] 未找到 ParticleSystem（包括子节点），vfxInstance 层级：{vfxInstance.transform.childCount} 个子节点");
                    Object.Destroy(vfxInstance, 5f);
                }

                if (showDebugInfo)
                    GameLogger.Log($"[{GetType().Name}] 入场特效已生成：{vfxInstance.name} @ {transform.position}");
            }
            else
            {
                GameLogger.LogWarning($"[{GetType().Name}] Resources.Load 也未找到 VFX_BlackLine，入场特效无法播放！");
            }

            float shakeIntensity = config != null ? config.spawnShakeIntensity : 0.5f;
            float shakeDuration = config != null ? config.spawnShakeDuration : 0.5f;
            CameraShake.Instance?.Shake(shakeIntensity, shakeDuration);
            HapticFeedback.Instance?.Trigger(HapticType.Critical);
            yield return new WaitForSeconds(shakeDuration);
            ChangeState(BossState.Idle);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Idle
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void EnterIdle()
        {
            if (eyeController != null) eyeController.Close();
            currentIdleDuration = config != null ? config.GetIdleDuration(HealthPercent) : Random.Range(3f, 5f);
            idleTimer = 0f;
            OnEnterIdle();
            SetNextIdleMoveTarget();
            stateCoroutine = StartCoroutine(IdleRoutine());
        }

        private IEnumerator IdleRoutine()
        {
            float moveSpeed = config != null ? config.GetMoveSpeed(HealthPercent) : 1.5f;

            while (idleTimer < currentIdleDuration)
            {
                idleTimer += Time.deltaTime;

                if (summonCooldownReady)
                {
                    if (showDebugInfo) GameLogger.Log($"[{GetType().Name}] ⏰ 召唤冷却完成→Summon");
                    ChangeState(BossState.Summon);
                    yield break;
                }

                OnIdlePassiveUpdate(Time.deltaTime);

                float newX = Mathf.MoveTowards(transform.position.x, idleMoveTargetX, moveSpeed * Time.deltaTime);
                transform.position = new Vector3(newX, transform.position.y, transform.position.z);
                if (Mathf.Abs(newX - idleMoveTargetX) < 0.1f)
                    SetNextIdleMoveTarget();

                yield return null;
            }

            ChooseActiveSkill();
        }

        private void SetNextIdleMoveTarget()
        {
            idleMoveTargetX = Random.Range(screenMinX, screenMaxX);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Summon 冷却管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void UpdateSummonCooldown()
        {
            if (IsFrozen) return;
            if (summonCooldownTimer > 0)
            {
                summonCooldownTimer -= Time.deltaTime;
                if (summonCooldownTimer <= 0) summonCooldownReady = true;
            }
        }

        protected void ResetSummonCooldown()
        {
            float cooldown = config != null ? config.GetSummonCooldown(HealthPercent) : 15f;
            summonCooldownTimer = cooldown;
            summonCooldownReady = false;
            if (showDebugInfo) GameLogger.Log($"[{GetType().Name}] 召唤冷却重置: {cooldown}s");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Summon
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator SummonRoutine()
        {
            float duration = config != null ? config.summonDuration : 1.0f;
            float blinkDuration = config != null ? config.blinkDuration : 0.75f;
            if (eyeController != null) eyeController.Blink(blinkDuration);

#if DOTWEEN
            if (bodyTransform != null)
            {
                float intensity = config != null ? config.summonShakeIntensity : 0.1f;
                shakeTweener = bodyTransform.DOShakePosition(duration, intensity, 20, 90, false, true);
            }
#endif
            yield return new WaitForSeconds(duration);
            yield return StartCoroutine(ExecuteSummonBehavior());
            ResetSummonCooldown();
            ChangeState(BossState.Idle);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 战术召唤
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void UpdateContinuousDamageTimer()
        {
            if (Time.time - lastDamageTime > DAMAGE_GAP_THRESHOLD)
                continuousDamageTimer = 0f;
        }

        public void OnDamageReceived()
        {
            lastDamageTime = Time.time;
            continuousDamageTimer += Time.deltaTime;
        }

        private void CheckTacticalSummon()
        {
            if (currentState != BossState.Idle || tacticalSummonCooldown) return;
            if (continuousDamageTimer < TACTICAL_SUMMON_THRESHOLD) return;

            if (showDebugInfo) GameLogger.Log($"[{GetType().Name}] ⚡ 战术召唤触发！");
            continuousDamageTimer = 0f;
            tacticalSummonCooldown = true;
            StartCoroutine(TacticalSummonCooldownRoutine());
            ChangeState(BossState.Summon);
        }

        private IEnumerator TacticalSummonCooldownRoutine()
        {
            yield return new WaitForSeconds(TACTICAL_SUMMON_CD);
            tacticalSummonCooldown = false;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Charge
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator ChargeRoutine()
        {
            chargeInterrupted = false;
            chargeHitCount = 0;

            if (redBodyEffect != null && !isUnstoppable) redBodyEffect.SetActive(true);
            if (eyeController != null) eyeController.Open();

            float telegraphDuration = config != null ? config.chargeTelegraphDuration : 1.0f;
            float windupDistance = config != null ? config.chargeWindupDistance : 0.5f;
            AudioManager.Instance?.PlayBossChargeWarning();

            Vector3 windupPos = transform.position + Vector3.up * windupDistance;
#if DOTWEEN
            transform.DOMove(windupPos, 0.2f).SetEase(Ease.OutQuad);
#else
            float wt = 0f;
            Vector3 ws = transform.position;
            while (wt < 0.2f) { wt += Time.deltaTime; transform.position = Vector3.Lerp(ws, windupPos, wt / 0.2f); yield return null; }
#endif
            float elapsed = 0f;
            while (elapsed < telegraphDuration)
            {
                if (chargeInterrupted) { OnChargeInterrupted(); yield break; }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (showDebugInfo) GameLogger.Log($"[{GetType().Name}] 🔴 Charge 冲锋！");
            AudioManager.Instance?.PlayBossDash();

            float speedMult = GetChargeSpeedMultiplier();
            float baseDashDuration = config != null ? config.chargeDashDuration : 0.3f;
            float dashDuration = baseDashDuration / speedMult;
            float targetY = config != null ? config.chargeTargetY : -10f;
            Vector3 chargeStartPos = transform.position;
            Vector3 dashTarget = new Vector3(transform.position.x, targetY, transform.position.z);

#if DOTWEEN
            bool dashComplete = false;
            moveTweener = transform.DOMove(dashTarget, dashDuration)
                .SetEase(Ease.InQuad).OnComplete(() => dashComplete = true);
            while (!dashComplete) yield return null;
#else
            float de = 0f; Vector3 ds = transform.position;
            while (de < dashDuration) { de += Time.deltaTime; float t = de / dashDuration; transform.position = Vector3.Lerp(ds, dashTarget, t * t); yield return null; }
            transform.position = dashTarget;
#endif
            OnChargeDashComplete(chargeStartPos, dashTarget);
            OnChargeHitPlayer();
        }

        public void OnHitReceived()
        {
            if (currentState != BossState.Charge || chargeInterrupted) return;
            chargeHitCount++;
            int threshold = config != null ? config.chargeHitCountThreshold : 30;
            if (chargeHitCount >= threshold) chargeInterrupted = true;
        }

        public void InterruptCharge()
        {
            if (isUnstoppable) { ShowStatusText(BossStatusTextType.Unstoppable); return; }
            if (currentState != BossState.Charge || chargeInterrupted) return;
            chargeInterrupted = true;
        }

        private void OnChargeHitPlayer()
        {
            float damage = config != null ? config.chargeHitDamage : 300f;
            ApplyDamageToPlayer(damage, PlayerDamageSource.BossCollision);
            if (showDebugInfo) GameLogger.Log($"[{GetType().Name}] 💥 Charge 撞击！伤害: {damage}");
            float si = config != null ? config.chargeHitShakeIntensity : 0.8f;
            float sd = config != null ? config.chargeHitShakeDuration : 0.3f;
            CameraShake.Instance?.ImpactShake(Vector2.down, si, sd);
            StartCoroutine(ChargeBounceBackRoutine());
        }

        private IEnumerator ChargeBounceBackRoutine()
        {
            float duration = config != null ? config.chargeBounceBackDuration : 0.5f;
#if DOTWEEN
            yield return transform.DOMove(battleAnchorPosition, duration).SetEase(Ease.OutQuad).WaitForCompletion();
#else
            float e = 0f; Vector3 s = transform.position;
            while (e < duration) { e += Time.deltaTime; transform.position = Vector3.Lerp(s, battleAnchorPosition, e / duration); yield return null; }
            transform.position = battleAnchorPosition;
#endif
            if (eyeController != null) eyeController.Close();
            yield return new WaitForSeconds(1.0f);
            ChangeState(BossState.Idle);
        }

        private void OnChargeInterrupted()
        {
            if (showDebugInfo) GameLogger.Log($"[{GetType().Name}] 💥 Charge 被打断！");
            ShowStatusText(BossStatusTextType.Interrupted);
            if (redBodyEffect != null && !isUnstoppable) redBodyEffect.SetActive(false);
            float baseDuration = config != null ? config.chargeInterruptStunDuration : 3.0f;
            if (!TryApplyStun(baseDuration)) ChangeState(BossState.Idle);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Press
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator PressRoutine()
        {
            isPressing = false;
            accumulatedPushForce = Vector2.zero;
            clashTimer = 0f;
            isFrictionDamageActive = false;
            frictionDamageAccumulator = 0f;

            AudioManager.Instance?.PlayBossDash();
            if (eyeController != null) eyeController.Close();

            float jumpDuration = config != null ? config.pressJumpDuration : 0.5f;
            float hoverY = config != null ? config.pressHoverY : -5f;
            Vector3 hoverPos = new Vector3(0f, hoverY, 0f);

#if DOTWEEN
            bool jc = false;
            moveTweener = transform.DOMove(hoverPos, jumpDuration).SetEase(Ease.OutExpo).OnComplete(() => jc = true);
            while (!jc) yield return null;
#else
            float je = 0f; Vector3 js = transform.position;
            while (je < jumpDuration) { je += Time.deltaTime; float t = 1f - Mathf.Pow(1f - je / jumpDuration, 3f); transform.position = Vector3.Lerp(js, hoverPos, t); yield return null; }
            transform.position = hoverPos;
#endif
            float glareDuration = config != null ? config.pressGlareDuration : 1.5f;
            if (redBodyEffect != null && !isUnstoppable) redBodyEffect.SetActive(true);
            eyeController?.OpenSlowly(glareDuration);
            yield return new WaitForSeconds(glareDuration);

            if (showDebugInfo) GameLogger.Log($"[{GetType().Name}] 🟣 Press Phase 3: 角力开始！");
            pressOverloadDamage = 0f;
            pressOverloadTimer = 0f;
            isPressing = true;
            isPressPhase3Active = true;
            pressDownForce = config != null ? config.GetPressForce(HealthPercent) : 100f;

            float safeLineY = config != null ? config.pressSafeLineY : 3.5f;
            float hitLineY = config != null ? config.pressHitLineY : -10f;
            float maxDuration = config != null ? config.pressMaxDuration : 15f;
            float maxClashTime = config != null ? config.maxClashDuration : 6f;
            float pressTimer = 0f;

            while (pressTimer < maxDuration)
            {
                pressTimer += Time.deltaTime;
                clashTimer += Time.deltaTime;
                accumulatedPushForceThisTick = 0f;
                pushForceUpdatedThisTick = false;

                if (transform.position.y >= safeLineY) { OnPressCountered(); yield break; }
                if (transform.position.y <= hitLineY) { OnPressHitPlayer(); yield break; }
                if (clashTimer >= maxClashTime) { OnPressExhausted(); yield break; }

                OnPressTick(Time.deltaTime);

                yield return null;
            }
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
                }
                float dps = config != null ? config.frictionDamagePerSecond : 50f;
                frictionDamageAccumulator += dps * Time.fixedDeltaTime;
                if (frictionDamageAccumulator >= 1f)
                {
                    int dmg = Mathf.FloorToInt(frictionDamageAccumulator);
                    frictionDamageAccumulator -= dmg;
                    ApplyDamageToPlayer(dmg, PlayerDamageSource.BossFriction);
                }
            }
            else if (isFrictionDamageActive)
            {
                isFrictionDamageActive = false;
                GameEvents.TriggerBossFrictionEnd();
            }

            float window = config != null ? config.pressOverloadWindow : 1.5f;
            pressOverloadTimer += Time.fixedDeltaTime;
            if (pressOverloadTimer >= window) { pressOverloadDamage = 0f; pressOverloadTimer = 0f; }
        }

        public void RecordPressOverloadDamage(float damage)
        {
            if (!isPressing) return;
            pressOverloadDamage += damage;
            float threshold = config != null ? config.pressOverloadDamageThreshold : 2000f;
            if (pressOverloadDamage >= threshold) OnPressOverload();
        }

        private void OnPressOverload()
        {
            ResetPressState();
            GameEvents.TriggerBossFrictionEnd();
            ShowStatusText(BossStatusTextType.Overload);
            if (showDebugInfo) GameLogger.Log($"[{GetType().Name}] 💥 Press 过载！");
            float shortDuration = config != null ? config.shortStunDuration : 1.5f;
            if (!TryApplyStun(shortDuration)) ChangeState(BossState.Idle);
            EnterShortStun();
        }

        private void OnPressCountered()
        {
            ResetPressState();
            GameEvents.TriggerBossFrictionEnd();
            ShowStatusText(BossStatusTextType.Countered);
            CameraShake.Instance?.Shake(0.5f, 0.3f);
            if (showDebugInfo) GameLogger.Log($"[{GetType().Name}] ✅ Press 被反推！");
            float baseDuration = config != null ? config.pressCounterStunDuration : 3.0f;
            if (!TryApplyStun(baseDuration)) ChangeState(BossState.Idle);
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
            CameraShake.Instance?.ImpactShake(Vector2.down, 1f, 0.5f);
            StartCoroutine(PressBounceBackRoutine());
        }

        private void OnPressExhausted()
        {
            ResetPressState();
            GameEvents.TriggerBossFrictionEnd();
            ShowStatusText(BossStatusTextType.Exhausted);
            CameraShake.Instance?.Shake(0.2f, 0.15f);
            if (showDebugInfo) GameLogger.Log($"[{GetType().Name}] 😤 Press 疲劳！");
            StartCoroutine(ExhaustedRetreatRoutine());
        }

        private void ResetPressState()
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
        }

        private IEnumerator ExhaustedRetreatRoutine()
        {
            float duration = 0.4f;
            Vector3 retreatTarget = battleAnchorPosition + Vector3.up * 1.5f;
#if DOTWEEN
            yield return transform.DOMove(retreatTarget, duration).SetEase(Ease.OutQuad).WaitForCompletion();
#else
            float e = 0f; Vector3 s = transform.position;
            while (e < duration) { e += Time.deltaTime; transform.position = Vector3.Lerp(s, retreatTarget, e / duration); yield return null; }
            transform.position = retreatTarget;
#endif
            eyeController?.Open();
            float baseDuration = config != null ? config.exhaustedStunDuration : 2.5f;
            if (!TryApplyStun(baseDuration)) ChangeState(BossState.Idle);
        }

        private IEnumerator PressBounceBackRoutine()
        {
            float duration = config != null ? config.chargeBounceBackDuration : 0.5f;
#if DOTWEEN
            yield return transform.DOMove(battleAnchorPosition, duration).SetEase(Ease.OutQuad).WaitForCompletion();
#else
            float e = 0f; Vector3 s = transform.position;
            while (e < duration) { e += Time.deltaTime; transform.position = Vector3.Lerp(s, battleAnchorPosition, e / duration); yield return null; }
            transform.position = battleAnchorPosition;
#endif
            if (eyeController != null) eyeController.Close();
            yield return new WaitForSeconds(1.0f);
            ChangeState(BossState.Idle);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 角力物理系统 — 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void ApplyLaserPushForce(float pushForce)
        {
            if (!isPressing) return;
            accumulatedPushForceThisTick += pushForce;
            pushForceUpdatedThisTick = true;
            lastLaserHitTime = Time.time;
        }

        public float CalculatePushForce(int impactLevel, int wideLevel)
        {
            float baseForce = config != null ? config.baseLaserPushForce : 120f;
            float impactMult = config != null ? config.GetPushMultiplier(impactLevel) : 0.4f;
            float wideMult = config != null ? config.GetWidePushMultiplier(wideLevel) : 0.4f;
            return baseForce * Mathf.Max(impactMult, wideMult);
        }

        public void FinalizePushForceThisTick()
        {
            if (!isPressing) return;
            if (pushForceUpdatedThisTick)
            {
                float maxForce = config != null ? config.maxTotalPushForce : 300f;
                currentPushForce = Mathf.Min(accumulatedPushForceThisTick, maxForce);
                isReceivingLaserHit = true;
            }
            accumulatedPushForceThisTick = 0f;
            pushForceUpdatedThisTick = false;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Stun
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator StunRoutine()
        {
            float duration;
            if (stunDurationOverride > 0)
            {
                duration = stunDurationOverride;
                stunDurationOverride = -1f;
            }
            else
            {
                duration = chargeInterrupted
                    ? (config != null ? config.chargeInterruptStunDuration : 3.0f)
                    : (config != null ? config.pressCounterStunDuration : 3.0f);
            }

            if (showDebugInfo) GameLogger.Log($"[{GetType().Name}] 💫 僵直 {duration:F1}s");

            if (eyeController != null) eyeController.Open();
            SetBodyDarken(true);
            yield return new WaitForSeconds(duration);
            SetBodyDarken(false);

            if (config != null && config.stunReturnToAnchor)
                yield return StartCoroutine(ReturnToAnchorRoutine());

            ChangeState(BossState.Idle);
        }

        private IEnumerator ReturnToAnchorRoutine()
        {
            float duration = 0.5f;
#if DOTWEEN
            yield return transform.DOMove(battleAnchorPosition, duration).SetEase(Ease.OutQuad).WaitForCompletion();
#else
            float e = 0f; Vector3 s = transform.position;
            while (e < duration) { e += Time.deltaTime; transform.position = Vector3.Lerp(s, battleAnchorPosition, e / duration); yield return null; }
            transform.position = battleAnchorPosition;
#endif
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Frozen
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator FrozenRoutine()
        {
            isFrozen = true;
            velocityBeforeFrozen = rb.velocity;
            rb.velocity = Vector2.zero;
            ShowStatusText(BossStatusTextType.Frozen);
            AudioManager.Instance?.PlayEnemyFreeze();

            while (frozenTimer > 0f) { frozenTimer -= Time.deltaTime; yield return null; }

            isFrozen = false;
            int triggerCount = config != null ? config.unstoppableTriggerCount : 3;
            if (controlStack >= triggerCount) EnterUnstoppable();

            bool returnToIdle = config == null || config.frozenReturnToIdle
                || stateBeforeFrozen == BossState.Idle
                || stateBeforeFrozen == BossState.Spawn
                || stateBeforeFrozen == BossState.Frozen;

            ChangeState(returnToIdle ? BossState.Idle : stateBeforeFrozen);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 工具方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected void ApplyDamageToPlayer(float damage, PlayerDamageSource source = PlayerDamageSource.BossCollision)
        {
            BattleStatistics.Instance?.RecordPlayerDamage(damage, source);

            if (cachedShieldController == null)
                cachedShieldController = FindObjectOfType<ShieldController>();
            TurretHealth turret = FindObjectOfType<TurretHealth>();

            if (cachedShieldController != null)
            {
                int remaining = cachedShieldController.TakeBossDamage((int)damage);
                if (remaining > 0 && turret != null)
                    turret.TakeBossDamage(remaining);
            }
            else if (turret != null)
            {
                turret.TakeBossDamage((int)damage);
            }
        }

        protected void SetBodyDarken(bool darken)
        {
            if (bodyRenderers == null) return;
            float darkenAmount = config != null ? config.stunDarkenAmount : 0.5f;
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null)
                    bodyRenderers[i].color = darken
                        ? originalColors[i] * (1f - darkenAmount)
                        : originalColors[i];
        }

        protected void ShowStatusText(BossStatusTextType textType)
        {
            if (FloatingTextManager.Instance == null) return;
            string text = config == null
                ? textType switch
                {
                    BossStatusTextType.Unstoppable => "UNSTOPPABLE!",
                    BossStatusTextType.Frozen => "FROZEN!",
                    BossStatusTextType.Enraged => "ENRAGED!",
                    BossStatusTextType.Interrupted => "INTERRUPTED!",
                    BossStatusTextType.Countered => "COUNTERED!",
                    BossStatusTextType.Overload => "OVERLOAD!",
                    BossStatusTextType.Exhausted => "EXHAUSTED!",
                    _ => ""
                }
                : textType switch
                {
                    BossStatusTextType.Unstoppable => config.unstoppableText,
                    BossStatusTextType.Frozen => config.frozenText,
                    BossStatusTextType.Enraged => config.enragedText,
                    BossStatusTextType.Interrupted => config.interruptedText,
                    BossStatusTextType.Countered => config.counteredText,
                    BossStatusTextType.Overload => config.overloadText,
                    BossStatusTextType.Exhausted => config.exhaustedText,
                    _ => ""
                };
            FloatingTextManager.Instance.ShowStatus(transform.position, text);
        }

        public void EnterShortStun()
        {
            float shortDuration = config != null ? config.shortStunDuration : 1.5f;
            if (!TryApplyStun(shortDuration))
                if (currentState != BossState.Idle)
                    ChangeState(BossState.Idle);
        }

        public void ForceStun() => ChangeState(BossState.Stun);

        /// <summary>
        /// 死亡演出准备：立即停止所有 AI 行为，防止在死亡演出窗口（约1.8s）内继续攻击玩家。
        /// 由 GameManager.BossDeathSequenceCoroutine() 在 T+0.00s 第一时间调用。
        /// </summary>
        public void PrepareForDeath()
        {
            // 停止所有协程（包括 stateCoroutine、EnrageTriggerRoutine 等所有子协程）
            StopAllCoroutines();
            stateCoroutine = null;

            // 冻结物理：停止 Press 下压力、Charge 冲速等
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.isKinematic = true;
            }

            // 关闭 Update / FixedUpdate，防止继续产生伤害
            this.enabled = false;

#if DOTWEEN
            // 终止当前所有 DOTween 动画（Charge 冲刺、Press 跳跃、摇晃等）
            transform.DOKill();
            if (bodyTransform != null) bodyTransform.DOKill();
#endif

            if (showDebugInfo)
                GameLogger.Log($"[{GetType().Name}] PrepareForDeath: AI 已停止，Boss 进入死亡演出状态");
        }

        protected virtual void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        protected int GetCurrentMobCount() =>
            EnemyPoolManager.Instance != null ? EnemyPoolManager.Instance.TotalActiveEnemies : 0;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

#if UNITY_EDITOR
        protected virtual void OnGUI()
        {
            if (!showDebugInfo) return;
            GUILayout.BeginArea(new Rect(10, 150, 280, 300));
            GUILayout.Label($"=== {GetType().Name} ===");
            GUILayout.Label($"State: {currentState}");
            GUILayout.Label($"HP: {HealthPercent:P1} | Enraged: {IsEnraged}");
            GUILayout.Label($"Mobs: {GetCurrentMobCount()}");
            GUILayout.Label($"IsPressing: {isPressing} | Y: {transform.position.y:F2}");
            GUILayout.Label($"Velocity Y: {(rb != null ? rb.velocity.y.ToString("F2") : "N/A")}");
            GUILayout.Space(5);
            GUILayout.Label("=== 控制系统 ===");
            GUILayout.Label($"Frost Exposure: {frostExposureTime:F2}s");
            GUILayout.Label($"Control Stack: {controlStack}");
            GUILayout.Label($"Unstoppable: {isUnstoppable} ({unstoppableTimer:F1}s)");
            GUILayout.Label($"Frozen: {isFrozen} ({frozenTimer:F1}s)");
            GUILayout.Label($"Summon CD: {summonCooldownTimer:F1}s {(summonCooldownReady ? "✓" : "")}");
            GUILayout.Space(5);
            if (GUILayout.Button("Force Stun")) ForceStun();
            if (GUILayout.Button("Force Charge")) ChangeState(BossState.Charge);
            if (GUILayout.Button("Force Press")) ChangeState(BossState.Press);
            if (GUILayout.Button("Force Freeze")) TryApplyFreeze(2f);
            if (GUILayout.Button("Force Unstoppable")) EnterUnstoppable();
            GUILayout.EndArea();
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(battleAnchorPosition, 0.5f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(screenMinX, battleAnchorPosition.y, 0), new Vector3(screenMaxX, battleAnchorPosition.y, 0));
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
