// ============================================================
// BossController.cs
// 文件位置: Assets/Scripts/Logic/Boss/BossController.cs
// 用途：Boss 行为状态机主控制器 (The Corruptor - 污染之核)
// 【重构】实现完整的野蛮冲撞 + 持续角力物理系统
// ============================================================

using UnityEngine;
using System.Collections;
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
        Summon,     // 召唤
        Charge,     // 冲撞（包含蓄力+持续角力）
        Stun        // 僵直/虚弱
    }
    
    /// <summary>
    /// Boss 控制器 - 污染之核 (The Corruptor)
    /// 状态机循环：Spawn -> Idle -> Summon/Charge -> Idle -> ...
    /// 【核心玩法】野蛮冲撞 + 持续角力物理系统
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
        private Vector3 battleAnchorPosition;   // 战斗锚点（屏幕上方1/4）
        private Vector3 spawnPosition;          // 生成位置
        
        // Idle 相关
        private float idleTimer;
        private float currentIdleDuration;
        private float idleMoveTargetX;
        private float screenMinX;
        private float screenMaxX;
        
        // 技能相关
        private bool lastSkillWasCharge = false;
        
        // Charge 相关
        private bool isCharging = false;            // 是否正在冲锋阶段（非蓄力）
        private bool chargeInterrupted = false;     // 蓄力是否被打断
        
        // 【核心】角力物理相关
        private Vector2 accumulatedPushForce;       // 累积的激光推力
        private bool isBeingPushed = false;         // 是否正在被激光推（本帧）
        
        // 霸体状态
        private bool isSuperArmor = false;
        
        // 颜色缓存（用于僵直变暗）
        private Color[] originalColors;
        
        // 协程引用
        private Coroutine stateCoroutine;
        
#if DOTWEEN
        private Tweener moveTweener;
        private Tweener shakeTweener;
        private Sequence chargeSequence;
#endif
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前状态</summary>
        public BossState CurrentState => currentState;
        
        /// <summary>是否处于可被打断的蓄力阶段（非霸体时）</summary>
        public bool IsInTelegraphPhase => currentState == BossState.Charge && !isCharging && !isSuperArmor;
        
        /// <summary>是否处于霸体状态</summary>
        public bool IsSuperArmor => isSuperArmor;
        
        /// <summary>是否正在冲撞/角力中（可以被推）</summary>
        public bool IsCharging => currentState == BossState.Charge && isCharging;
        
        /// <summary>血量百分比</summary>
        public float HealthPercent => bossHealth != null ? bossHealth.HealthPercent : 1f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            bossHealth = GetComponent<BossHealth>();
            
            // 自动查找眼睛控制器
            if (eyeController == null)
            {
                Transform eyes = transform.Find("Eyes");
                if (eyes != null)
                {
                    eyeController = eyes.GetComponent<BossEyeController>();
                    if (eyeController == null)
                    {
                        eyeController = eyes.gameObject.AddComponent<BossEyeController>();
                    }
                }
            }
            
            // 自动查找Body
            if (bodyTransform == null)
            {
                Transform body = transform.Find("Body");
                if (body != null)
                {
                    bodyTransform = body;
                }
            }
            
            // 自动查找红色特效
            if (redBodyEffect == null)
            {
                Transform body03 = transform.Find("Body03");
                if (body03 != null)
                {
                    redBodyEffect = body03.gameObject;
                }
            }
            
            // 自动查找玩家塔
            if (playerTower == null)
            {
                GameObject tower = GameObject.FindGameObjectWithTag("Tower");
                if (tower != null)
                {
                    playerTower = tower.transform;
                }
            }
        }
        
        private void Start()
        {
            // 记录初始位置
            spawnPosition = transform.position;
            
            // 计算战斗锚点（屏幕上方1/4）
            CalculateBattleAnchor();
            
            // 计算屏幕边界
            CalculateScreenBounds();
            
            // 缓存原始颜色
            CacheOriginalColors();
            
            // 开始入场状态
            ChangeState(BossState.Spawn);
        }
        
        private void FixedUpdate()
        {
            // 【核心】在冲撞/角力阶段应用累积的推力
            if (isCharging && accumulatedPushForce.sqrMagnitude > 0.01f)
            {
                // 推力抵消冲撞速度
                rb.AddForce(accumulatedPushForce, ForceMode2D.Force);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[BossController] 受到推力: {accumulatedPushForce.magnitude:F2}, 当前Y速度: {rb.velocity.y:F2}");
                }
                
                // 重置累积推力（每帧重新计算）
                accumulatedPushForce = Vector2.zero;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 角力物理系统 - 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 【核心】接收来自激光的推力（仅在冲撞阶段生效）
        /// 由 LaserController 在命中核心时调用
        /// </summary>
        /// <param name="pushForce">推力向量（方向 + 大小）</param>
        public void ApplyLaserPushForce(Vector2 pushForce)
        {
            // 只在冲撞/角力阶段接受推力
            if (!isCharging) return;
            
            // 累积推力（FixedUpdate 中应用）
            accumulatedPushForce += pushForce;
            isBeingPushed = true;
        }
        
        /// <summary>
        /// 计算当前激光对Boss的推力大小
        /// </summary>
        /// <param name="impactLevel">Impact技能等级 (0-5)</param>
        /// <param name="isUltMode">是否开启大招</param>
        /// <returns>推力大小</returns>
        public float CalculatePushForce(int impactLevel, bool isUltMode)
        {
            float baseForce = config != null ? config.baseLaserPushForce : 80f;
            
            // Impact 等级倍率
            float impactMultiplier = 0.3f; // 默认 Lv0
            if (config != null && config.impactPushMultipliers != null && impactLevel < config.impactPushMultipliers.Length)
            {
                impactMultiplier = config.impactPushMultipliers[impactLevel];
            }
            else
            {
                // 回退值
                switch (impactLevel)
                {
                    case 0: impactMultiplier = 0.3f; break;
                    case 1: impactMultiplier = 0.5f; break;
                    case 2: impactMultiplier = 0.7f; break;
                    case 3: impactMultiplier = 1.0f; break;
                    case 4: impactMultiplier = 1.3f; break;
                    case 5: impactMultiplier = 1.6f; break;
                    default: impactMultiplier = 0.3f; break;
                }
            }
            
            float totalForce = baseForce * impactMultiplier;
            
            // 大招加成
            if (isUltMode)
            {
                float ultMultiplier = config != null ? config.ultPushMultiplier : 2.5f;
                totalForce *= ultMultiplier;
            }
            
            return totalForce;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void CalculateBattleAnchor()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            
            float anchorY = config != null ? config.battleAnchorY : 3.5f;
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
                // 自动收集子物体的SpriteRenderer
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
            
            // 退出当前状态
            ExitState(currentState);
            
            currentState = newState;
            
            // 进入新状态
            EnterState(newState);
        }
        
        private void ExitState(BossState state)
        {
            // 停止当前状态协程
            if (stateCoroutine != null)
            {
                StopCoroutine(stateCoroutine);
                stateCoroutine = null;
            }
            
            // 重置冲撞相关状态
            if (state == BossState.Charge)
            {
                isCharging = false;
                chargeInterrupted = false;
                isSuperArmor = false;
                accumulatedPushForce = Vector2.zero;
                isBeingPushed = false;
                
                // 关闭红色特效
                if (redBodyEffect != null)
                {
                    redBodyEffect.SetActive(false);
                }
            }
            
#if DOTWEEN
            // 停止移动动画
            if (moveTweener != null && moveTweener.IsActive())
            {
                moveTweener.Kill();
            }
            if (shakeTweener != null && shakeTweener.IsActive())
            {
                shakeTweener.Kill();
            }
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
            if (eyeController != null)
            {
                eyeController.SetStateDirect(BossEyeState.Closed);
            }
            
            // 隐藏红色特效
            if (redBodyEffect != null)
            {
                redBodyEffect.SetActive(false);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 开始入场动画，从 {spawnPosition} 移动到 {battleAnchorPosition}");
            }
            
#if DOTWEEN
            // DOTween 入场动画
            bool moveComplete = false;
            moveTweener = transform
                .DOMove(battleAnchorPosition, duration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => moveComplete = true);
            
            while (!moveComplete)
            {
                yield return null;
            }
#else
            // 协程入场动画
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float easeT = 1f - Mathf.Pow(1f - t, 2f); // EaseOutQuad
                
                transform.position = Vector3.Lerp(startPos, battleAnchorPosition, easeT);
                yield return null;
            }
            
            transform.position = battleAnchorPosition;
#endif
            
            // 播放咆哮
            if (showDebugInfo)
            {
                Debug.Log("[BossController] BOSS 咆哮！");
            }
            
            // 屏幕震动
            float shakeIntensity = config != null ? config.spawnShakeIntensity : 0.5f;
            float shakeDuration = config != null ? config.spawnShakeDuration : 0.5f;
            
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(shakeIntensity, shakeDuration);
            }
            
            yield return new WaitForSeconds(shakeDuration);
            
            // 进入 Idle
            ChangeState(BossState.Idle);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Idle (待机)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void EnterIdle()
        {
            // 眼睛闭合
            if (eyeController != null)
            {
                eyeController.Close();
            }
            
            // 根据血量决定Idle时长
            bool isRage = HealthPercent < (config != null ? config.rageHealthThreshold : 0.3f);
            
            if (isRage)
            {
                float minDur = config != null ? config.rageIdleDurationMin : 1.5f;
                float maxDur = config != null ? config.rageIdleDurationMax : 2.5f;
                currentIdleDuration = Random.Range(minDur, maxDur);
            }
            else
            {
                float minDur = config != null ? config.idleDurationMin : 3.0f;
                float maxDur = config != null ? config.idleDurationMax : 5.0f;
                currentIdleDuration = Random.Range(minDur, maxDur);
            }
            
            idleTimer = 0f;
            
            // 设置下一个移动目标
            SetNextIdleMoveTarget();
            
            // 启动 Idle 更新协程
            stateCoroutine = StartCoroutine(IdleRoutine());
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 进入 Idle, 时长: {currentIdleDuration:F1}s, 狂暴: {isRage}");
            }
        }
        
        private IEnumerator IdleRoutine()
        {
            float moveSpeed = config != null ? config.idleMoveSpeed : 1.5f;
            
            while (idleTimer < currentIdleDuration)
            {
                idleTimer += Time.deltaTime;
                
                // 水平游走
                float currentX = transform.position.x;
                float newX = Mathf.MoveTowards(currentX, idleMoveTargetX, moveSpeed * Time.deltaTime);
                transform.position = new Vector3(newX, transform.position.y, transform.position.z);
                
                // 到达目标后设置新目标
                if (Mathf.Abs(newX - idleMoveTargetX) < 0.1f)
                {
                    SetNextIdleMoveTarget();
                }
                
                yield return null;
            }
            
            // Idle 结束，决定下一个技能
            DecideNextSkill();
        }
        
        private void SetNextIdleMoveTarget()
        {
            // 在屏幕范围内随机选择一个目标X坐标
            idleMoveTargetX = Random.Range(screenMinX, screenMaxX);
        }
        
        private void DecideNextSkill()
        {
            int mobCount = GetCurrentMobCount();
            
            // 使用配置决定技能
            bool shouldSummon = config != null 
                ? config.ShouldSummon(mobCount, lastSkillWasCharge)
                : (mobCount < 3);
            
            if (shouldSummon)
            {
                lastSkillWasCharge = false;
                ChangeState(BossState.Summon);
            }
            else
            {
                lastSkillWasCharge = true;
                ChangeState(BossState.Charge);
            }
        }
        
        private int GetCurrentMobCount()
        {
            if (EnemyPoolManager.Instance != null)
            {
                return EnemyPoolManager.Instance.TotalActiveEnemies;
            }
            
            return GameObject.FindGameObjectsWithTag("Enemy").Length;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Summon (召唤)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator SummonRoutine()
        {
            float duration = config != null ? config.summonDuration : 1.5f;
            
            // 眼睛闭合
            if (eyeController != null)
            {
                eyeController.Close();
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 开始召唤！");
            }
            
            // 身体抖动动画
            float shakeIntensity = config != null ? config.summonShakeIntensity : 0.1f;
            float shakeFrequency = config != null ? config.summonShakeFrequency : 30f;
            
            Vector3 originalBodyPos = bodyTransform != null ? bodyTransform.localPosition : Vector3.zero;
            
#if DOTWEEN
            if (bodyTransform != null)
            {
                shakeTweener = bodyTransform
                    .DOShakePosition(duration, shakeIntensity, (int)shakeFrequency)
                    .SetEase(Ease.Linear);
            }
#endif
            
            yield return new WaitForSeconds(duration * 0.5f);
            
            // 召唤小怪
            SpawnMinions();
            
            yield return new WaitForSeconds(duration * 0.5f);
            
            // 恢复位置
            if (bodyTransform != null)
            {
                bodyTransform.localPosition = originalBodyPos;
            }
            
            // 返回 Idle
            ChangeState(BossState.Idle);
        }
        
        private void SpawnMinions()
        {
            if (EnemyPoolManager.Instance == null) return;
            
            int count = config != null ? config.summonMinionCount : 3;
            
            for (int i = 0; i < count; i++)
            {
                if (EnemyPoolManager.Instance.IsAtGlobalCapacity) break;
                
                // 在Boss周围随机位置生成
                Vector2 offset = Random.insideUnitCircle * 2f;
                Vector3 spawnPos = transform.position + new Vector3(offset.x, offset.y, 0);
                
                // 随机类型
                EnemyType type = Random.value > 0.5f ? EnemyType.Rusher : EnemyType.Slime;
                EnemyPoolManager.Instance.Spawn(type, spawnPos);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 召唤了 {count} 个小怪！");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Charge (冲撞) - 【重构】持续角力物理系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator ChargeRoutine()
        {
            chargeInterrupted = false;
            isCharging = false;
            accumulatedPushForce = Vector2.zero;
            
            float telegraphDuration = config != null ? config.chargeTelegraphDuration : 2.0f;
            float windupDistance = config != null ? config.chargeWindupDistance : 0.5f;
            
            // ═══════════════════════════════════════════════════
            // Phase 0: 先召唤一波小怪（制造困境）
            // ═══════════════════════════════════════════════════
            
            int summonCount = config != null ? config.crushingSummonCount : 4;
            if (summonCount > 0 && EnemyPoolManager.Instance != null)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[BossController] 🐙 冲撞前召唤 {summonCount} 只小怪！");
                }
                
                for (int i = 0; i < summonCount; i++)
                {
                    if (EnemyPoolManager.Instance.IsAtGlobalCapacity) break;
                    
                    // 在屏幕两侧生成（制造分散玩家注意力的效果）
                    float side = (i % 2 == 0) ? -1f : 1f;
                    float offsetX = side * Random.Range(2f, 4f);
                    float offsetY = Random.Range(-1f, 1f);
                    Vector3 spawnPos = transform.position + new Vector3(offsetX, offsetY, 0);
                    
                    // 优先生成 Rusher（速攻怪）
                    EnemyType type = (i < summonCount / 2) ? EnemyType.Rusher : EnemyType.Slime;
                    EnemyPoolManager.Instance.Spawn(type, spawnPos);
                }
                
                // 短暂等待，让玩家注意到小怪
                yield return new WaitForSeconds(0.5f);
            }
            
            // ═══════════════════════════════════════════════════
            // Phase A: Telegraph (蓄力预警) - 带霸体机制
            // ═══════════════════════════════════════════════════
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] ⚠️ 蓄力开始！眼睛睁开变红！");
            }
            
            // 眼睛猛然睁开！
            if (eyeController != null)
            {
                eyeController.Open();
            }
            
            // 显示红色特效（怒目）
            if (redBodyEffect != null)
            {
                redBodyEffect.SetActive(true);
            }
            
            // 稍微后退（像拉弓）
            Vector3 windupPos = transform.position + Vector3.up * windupDistance;
            
#if DOTWEEN
            transform.DOMove(windupPos, 0.3f).SetEase(Ease.OutQuad);
#else
            float windupTime = 0f;
            Vector3 startPos = transform.position;
            while (windupTime < 0.3f)
            {
                windupTime += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, windupPos, windupTime / 0.3f);
                yield return null;
            }
#endif
            
            // 霸体时间（前X秒不可打断）
            float superArmorDuration = config != null ? config.telegraphSuperArmorDuration : 1.0f;
            isSuperArmor = true;
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 🛡️ 霸体中... {superArmorDuration}秒");
            }
            
            yield return new WaitForSeconds(superArmorDuration);
            
            // 霸体结束，进入可打断窗口
            isSuperArmor = false;
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] ⚡ 霸体结束！可被打断！");
            }
            
            // 剩余蓄力时间（可打断窗口）
            float interruptWindowDuration = telegraphDuration - superArmorDuration;
            float telegraphElapsed = 0f;
            
            while (telegraphElapsed < interruptWindowDuration && !chargeInterrupted)
            {
                telegraphElapsed += Time.deltaTime;
                yield return null;
            }
            
            // 检查是否被打断
            if (chargeInterrupted)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[BossController] 💥 蓄力被打断！进入僵直！");
                }
                
                ShowCounterText("INTERRUPTED!");
                ChangeState(BossState.Stun);
                yield break;
            }
            
            // ═══════════════════════════════════════════════════
            // Phase B: Crushing Press (持续角力) - 核心玩法
            // ═══════════════════════════════════════════════════
            
            isCharging = true;
            rb.velocity = Vector2.zero; // 清空之前的速度
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🔴 野蛮冲撞开始！持续向下压迫！");
            }
            
            // 获取配置参数
            float chargeForce = config != null ? config.chargeForce : 100f;
            float safeLineY = config != null ? config.safeLineY : 3.0f;
            float hitLineY = config != null ? config.hitLineY : -3.0f;
            float maxDuration = config != null ? config.maxCrushingDuration : 15f;
            
            float crushingElapsed = 0f;
            
            // 【核心循环】持续角力
            while (crushingElapsed < maxDuration)
            {
                crushingElapsed += Time.deltaTime;
                
                // 持续施加向下的压迫力
                rb.AddForce(Vector2.down * chargeForce, ForceMode2D.Force);
                
                // ─────────────────────────────────────────────────
                // 胜利条件：被推回安全线上方
                // ─────────────────────────────────────────────────
                if (transform.position.y > safeLineY)
                {
                    OnChargePushedBack();
                    yield break;
                }
                
                // ─────────────────────────────────────────────────
                // 失败条件：撞到玩家（到达撞击线）
                // ─────────────────────────────────────────────────
                if (transform.position.y < hitLineY)
                {
                    OnChargeHitPlayer();
                    yield break;
                }
                
                // 调试信息
                if (showDebugInfo && Time.frameCount % 30 == 0)
                {
                    Debug.Log($"[BossController] 角力中... Y={transform.position.y:F2}, 速度Y={rb.velocity.y:F2}, 被推={isBeingPushed}");
                }
                
                // 重置被推标记（下一帧重新计算）
                isBeingPushed = false;
                
                yield return null;
            }
            
            // 超时：弹回原位
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 角力超时，弹回原位");
            }
            
            OnChargeComplete();
        }
        
        /// <summary>
        /// 【玩家胜利】Boss被推回安全线
        /// </summary>
        private void OnChargePushedBack()
        {
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🎉 被激光推回！玩家角力胜利！进入僵直奖励时间！");
            }
            
            rb.velocity = Vector2.zero;
            isCharging = false;
            
            // 显示 STOPPED! 飘字
            ShowCounterText("STOPPED!");
            
            // 屏幕轻微震动（成功反制的感觉）
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.3f, 0.2f);
            }
            
            // 进入僵直状态（奖励时间）- 眼睛保持睁开
            ChangeState(BossState.Stun);
        }
        
        /// <summary>
        /// 【玩家失败】Boss撞到玩家
        /// </summary>
        private void OnChargeHitPlayer()
        {
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 💥 撞击玩家！造成大量伤害！");
            }
            
            rb.velocity = Vector2.zero;
            isCharging = false;
            
            // 对玩家造成伤害
            float damage = config != null ? config.chargeHitDamage : 300f;
            ApplyDamageToPlayer(damage);
            
            // 显示伤害飘字
            ShowCounterText($"-{(int)damage}");
            
            // 屏幕震动（强烈）
            float shakeIntensity = config != null ? config.chargeHitShakeIntensity : 0.8f;
            float shakeDuration = config != null ? config.chargeHitShakeDuration : 0.3f;
            
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.ImpactShake(Vector2.down, shakeIntensity, shakeDuration);
            }
            
            // Boss弹回并进入短暂僵直
            StartCoroutine(BounceBackAndRecover());
        }
        
        /// <summary>
        /// 冲撞超时结束
        /// </summary>
        private void OnChargeComplete()
        {
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 冲撞结束，弹回原位");
            }
            
            rb.velocity = Vector2.zero;
            isCharging = false;
            
            StartCoroutine(BounceBackRoutine());
        }
        
        /// <summary>
        /// 撞击玩家后弹回并短暂僵直
        /// </summary>
        private IEnumerator BounceBackAndRecover()
        {
            // 弹回
            yield return BounceBackRoutine();
            
            // 撞击后短暂僵直（惩罚BOSS，但比被推住短）
            float shortStun = 1.0f;
            
            if (eyeController != null)
            {
                eyeController.Close();
            }
            
            yield return new WaitForSeconds(shortStun);
            
            // 返回 Idle
            ChangeState(BossState.Idle);
        }
        
        /// <summary>
        /// 弹回原位动画
        /// </summary>
        private IEnumerator BounceBackRoutine()
        {
            float bounceBackDuration = config != null ? config.chargeBounceBackDuration : 0.5f;
            
#if DOTWEEN
            yield return transform.DOMove(battleAnchorPosition, bounceBackDuration)
                .SetEase(Ease.OutQuad)
                .WaitForCompletion();
#else
            float bounceTime = 0f;
            Vector3 bounceStart = transform.position;
            while (bounceTime < bounceBackDuration)
            {
                bounceTime += Time.deltaTime;
                float t = bounceTime / bounceBackDuration;
                transform.position = Vector3.Lerp(bounceStart, battleAnchorPosition, t);
                yield return null;
            }
            transform.position = battleAnchorPosition;
#endif
            
            // 弹回后返回 Idle
            ChangeState(BossState.Idle);
        }
        
        /// <summary>
        /// 对玩家造成伤害
        /// </summary>
        private void ApplyDamageToPlayer(float damage)
        {
            // 查找护盾控制器
            ShieldController shield = FindObjectOfType<ShieldController>();
            TurretHealth turret = FindObjectOfType<TurretHealth>();
            
            if (shield != null)
            {
                // 先扣护盾
                int remainingDamage = shield.TakeBossDamage((int)damage);
                
                // 护盾不够，扣本体
                if (remainingDamage > 0 && turret != null)
                {
                    turret.TakeBossDamage(remainingDamage);
                }
            }
            else if (turret != null)
            {
                // 没护盾，直接扣本体
                turret.TakeBossDamage((int)damage);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 对玩家造成 {damage} 点伤害！");
            }
        }
        
        /// <summary>
        /// 显示反制飘字
        /// </summary>
        private void ShowCounterText(string text)
        {
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.ShowStatus(transform.position, text);
            }
        }
        
        /// <summary>
        /// 打断蓄力（由 LaserController 调用）
        /// 条件：Impact Lv.4+ 或大招，命中蓄力中的核心
        /// </summary>
        public void InterruptCharge()
        {
            if (currentState == BossState.Charge && !isCharging && !isSuperArmor)
            {
                chargeInterrupted = true;
                
                if (showDebugInfo)
                {
                    Debug.Log("[BossController] ⚡ 蓄力被 Impact 技能打断！");
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Stun (僵直) - 奖励时间
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator StunRoutine()
        {
            // 使用反制僵直时长（被推住的奖励）
            float duration = config != null ? config.counterStunDuration : 3.0f;
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 进入僵直！眼睛睁开 {duration} 秒！");
            }
            
            // 【关键】眼睛保持睁开（奖励玩家DPS窗口）
            if (eyeController != null)
            {
                eyeController.Open();
            }
            
            // 颜色变暗
            DarkenBody();
            
            // 关闭红色特效
            if (redBodyEffect != null)
            {
                redBodyEffect.SetActive(false);
            }
            
            yield return new WaitForSeconds(duration);
            
            // 恢复颜色
            RestoreColors();
            
            // 眼睛闭合
            if (eyeController != null)
            {
                eyeController.Close();
            }
            
            // 返回 Idle
            ChangeState(BossState.Idle);
        }
        
        private void DarkenBody()
        {
            float darkenAmount = config != null ? config.stunDarkenAmount : 0.5f;
            
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null)
                {
                    Color c = originalColors[i];
                    bodyRenderers[i].color = new Color(
                        c.r * darkenAmount,
                        c.g * darkenAmount,
                        c.b * darkenAmount,
                        c.a
                    );
                }
            }
        }
        
        private void RestoreColors()
        {
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null && i < originalColors.Length)
                {
                    bodyRenderers[i].color = originalColors[i];
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 强制进入僵直状态（由外部调用）
        /// </summary>
        public void ForceStun()
        {
            ChangeState(BossState.Stun);
        }
        
        /// <summary>
        /// 重置状态（用于复活等）
        /// </summary>
        public void ResetState()
        {
            transform.position = battleAnchorPosition;
            RestoreColors();
            
            if (eyeController != null)
            {
                eyeController.SetStateDirect(BossEyeState.Closed);
            }
            
            ChangeState(BossState.Idle);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 150, 280, 220));
            GUILayout.Label("=== Boss Controller (角力系统) ===");
            GUILayout.Label($"State: {currentState}");
            GUILayout.Label($"HP: {HealthPercent:P1}");
            GUILayout.Label($"Eye: {(eyeController != null ? eyeController.CurrentState.ToString() : "N/A")}");
            GUILayout.Label($"Mobs: {GetCurrentMobCount()}");
            GUILayout.Label($"IsCharging: {isCharging}");
            GUILayout.Label($"Position Y: {transform.position.y:F2}");
            GUILayout.Label($"Velocity Y: {(rb != null ? rb.velocity.y.ToString("F2") : "N/A")}");
            GUILayout.Label($"SafeLineY: {(config != null ? config.safeLineY : 3.0f):F1} | HitLineY: {(config != null ? config.hitLineY : -3.0f):F1}");
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("Force Stun"))
            {
                ForceStun();
            }
            
            if (GUILayout.Button("Force Charge"))
            {
                ChangeState(BossState.Charge);
            }
            
            GUILayout.EndArea();
        }
        
        private void OnDrawGizmosSelected()
        {
            // 绘制战斗锚点
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(battleAnchorPosition, 0.5f);
            
            // 绘制移动范围
            Gizmos.color = Color.cyan;
            float y = battleAnchorPosition.y;
            Gizmos.DrawLine(new Vector3(screenMinX, y, 0), new Vector3(screenMaxX, y, 0));
            
            // 绘制安全线
            float safeY = config != null ? config.safeLineY : 3.0f;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(new Vector3(-10, safeY, 0), new Vector3(10, safeY, 0));
            
            // 绘制撞击线
            float hitY = config != null ? config.hitLineY : -3.0f;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(-10, hitY, 0), new Vector3(10, hitY, 0));
        }
#endif
    }
}