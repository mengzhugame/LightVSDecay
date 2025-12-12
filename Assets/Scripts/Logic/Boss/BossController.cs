// ============================================================
// BossController.cs
// 文件位置: Assets/Scripts/Logic/Boss/BossController.cs
// 用途：Boss 行为状态机主控制器 (The Corruptor - 污染之核)
// ============================================================

using UnityEngine;
using System.Collections;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Enemy;
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
        Charge,     // 冲撞（包含蓄力+冲锋）
        Stun        // 僵直/虚弱
    }
    
    /// <summary>
    /// Boss 控制器 - 污染之核 (The Corruptor)
    /// 状态机循环：Spawn -> Idle -> Summon/Charge -> Idle -> ...
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
        private bool isCharging = false;
        private bool chargeInterrupted = false;
        
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
        
        /// <summary>是否处于可被打断的蓄力阶段</summary>
        public bool IsInTelegraphPhase => currentState == BossState.Charge && !isCharging;
        
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
            
            // 缓存原始颜色
            CacheOriginalColors();
        }
        
        private void Start()
        {
            // 计算屏幕边界
            CalculateScreenBounds();
            
            // 记录生成位置
            spawnPosition = transform.position;
            
            // 计算战斗锚点
            if (config != null)
            {
                battleAnchorPosition = new Vector3(0f, config.battleAnchorY, 0f);
            }
            else
            {
                battleAnchorPosition = new Vector3(0f, 3.5f, 0f);
            }
            
            // 配置眼睛控制器
            if (eyeController != null && config != null)
            {
                eyeController.Configure(
                    config.eyeClosedScaleY,
                    config.eyeOpenColliderScale,
                    config.eyeTransitionDuration
                );
            }
            
            // 开始状态机
            ChangeState(BossState.Spawn);
        }
        
        private void Update()
        {
            // 状态特定更新
            switch (currentState)
            {
                case BossState.Idle:
                    UpdateIdle();
                    break;
            }
        }
        
        private void OnDestroy()
        {
            // 清理DOTween
#if DOTWEEN
            if (moveTweener != null && moveTweener.IsActive()) moveTweener.Kill();
            if (shakeTweener != null && shakeTweener.IsActive()) shakeTweener.Kill();
            if (chargeSequence != null && chargeSequence.IsActive()) chargeSequence.Kill();
#endif
            
            if (stateCoroutine != null)
            {
                StopCoroutine(stateCoroutine);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
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
            
            // 等待移动完成
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
            
            // 播放咆哮（TODO: 音效）
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
            
            // 隐藏红色特效
            if (redBodyEffect != null)
            {
                redBodyEffect.SetActive(false);
            }
            
            // 恢复颜色
            RestoreColors();
            
            // 设置Idle时长
            currentIdleDuration = config != null 
                ? config.GetIdleDuration(HealthPercent) 
                : Random.Range(3f, 5f);
            
            idleTimer = 0f;
            
            // 选择一个移动目标点
            idleMoveTargetX = Random.Range(screenMinX, screenMaxX);
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 进入 Idle，时长: {currentIdleDuration:F1}s");
            }
        }
        
        private void UpdateIdle()
        {
            idleTimer += Time.deltaTime;
            
            // 水平游走
            float moveSpeed = config != null ? config.idleMoveSpeed : 1.5f;
            float newX = Mathf.MoveTowards(transform.position.x, idleMoveTargetX, moveSpeed * Time.deltaTime);
            transform.position = new Vector3(newX, transform.position.y, transform.position.z);
            
            // 到达目标点后选择新目标
            if (Mathf.Abs(transform.position.x - idleMoveTargetX) < 0.1f)
            {
                idleMoveTargetX = Random.Range(screenMinX, screenMaxX);
            }
            
            // 检查是否该切换状态
            if (idleTimer >= currentIdleDuration)
            {
                DecideNextAction();
            }
        }
        
        private void DecideNextAction()
        {
            // 获取场上小怪数量
            int mobCount = GetCurrentMobCount();
            
            // 决定下一个动作
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
            // 从 EnemyPoolManager 获取活跃敌人数量
            if (EnemyPoolManager.Instance != null)
            {
                return EnemyPoolManager.Instance.TotalActiveEnemies;  // ✅ 正确
            }
    
            // Fallback: 查找场景中的敌人
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
            float elapsed = 0f;
            
#if DOTWEEN
            // DOTween 抖动
            if (bodyTransform != null)
            {
                shakeTweener = bodyTransform
                    .DOShakePosition(duration, shakeIntensity, Mathf.RoundToInt(shakeFrequency), 90f, false, true);
            }
            
            yield return new WaitForSeconds(duration);
            
            // 恢复位置
            if (bodyTransform != null)
            {
                bodyTransform.localPosition = originalBodyPos;
            }
#else
            // 协程抖动
            float interval = 1f / shakeFrequency;
            
            while (elapsed < duration)
            {
                if (bodyTransform != null)
                {
                    float x = Random.Range(-shakeIntensity, shakeIntensity);
                    float y = Random.Range(-shakeIntensity, shakeIntensity);
                    bodyTransform.localPosition = originalBodyPos + new Vector3(x, y, 0f);
                }
                
                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }
            
            // 恢复位置
            if (bodyTransform != null)
            {
                bodyTransform.localPosition = originalBodyPos;
            }
#endif
            
            // 召唤小怪
            SpawnMinions();
            
            // 返回 Idle
            ChangeState(BossState.Idle);
        }
        
        private void SpawnMinions()
        {
            int count = config != null ? config.summonMinionCount : 3;
            
            if (EnemyPoolManager.Instance != null)
            {
                for (int i = 0; i < count; i++)
                {
                    if (EnemyPoolManager.Instance.IsAtGlobalCapacity) break;
                    
                    // 从Boss位置附近生成
                    Vector3 spawnPos = transform.position + new Vector3(
                        Random.Range(-2f, 2f),
                        Random.Range(-1f, 1f),
                        0f
                    );
                    
                    // 随机选择 Rusher 或 Slime
                    EnemyType type = Random.value > 0.5f ? EnemyType.Rusher : EnemyType.Slime;
                    EnemyPoolManager.Instance.Spawn(type, spawnPos);
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 召唤了 {count} 个小怪！");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Charge (冲撞)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator ChargeRoutine()
        {
            chargeInterrupted = false;
            isCharging = false;
            
            float telegraphDuration = config != null ? config.chargeTelegraphDuration : 2.0f;
            float windupDistance = config != null ? config.chargeWindupDistance : 0.5f;
            
            // ═══════════════════════════════════════════════════
            // Phase A: Telegraph (蓄力预警) - DPS窗口！
            // ═══════════════════════════════════════════════════
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] ⚠️ 蓄力开始！眼睛睁开！");
            }
            
            // 眼睛猛然睁开！
            if (eyeController != null)
            {
                eyeController.Open();
            }
            
            // 显示红色特效
            if (redBodyEffect != null)
            {
                redBodyEffect.SetActive(true);
            }
            
            // 稍微后退（像拉弹弓）
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
            
            // 蓄力等待（玩家DPS窗口）
            float telegraphElapsed = 0f;
            while (telegraphElapsed < telegraphDuration && !chargeInterrupted)
            {
                telegraphElapsed += Time.deltaTime;
                yield return null;
            }
            
            // 检查是否被打断
            if (chargeInterrupted)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[BossController] 冲撞被打断！进入僵直！");
                }
                ChangeState(BossState.Stun);
                yield break;
            }
            
            // ═══════════════════════════════════════════════════
            // Phase B: Dash (冲锋)
            // ═══════════════════════════════════════════════════
            
            isCharging = true;
            
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🔴 冲锋开始！");
            }
            
            // 冲向玩家
            Vector3 targetPos = playerTower != null 
                ? playerTower.position 
                : new Vector3(transform.position.x, -4f, 0f);
            
            float dashSpeed = config != null ? config.chargeDashSpeed : 15f;
            float maxDashTime = 2f; // 最大冲撞时间防止卡住
            float dashElapsed = 0f;
            
            // 给予冲撞力
            Vector2 dashDirection = (targetPos - transform.position).normalized;
            
            while (dashElapsed < maxDashTime)
            {
                dashElapsed += Time.deltaTime;
                
                // 使用Rigidbody移动
                rb.velocity = dashDirection * dashSpeed;
                
                // 检查是否撞到玩家（Y坐标低于某个阈值）
                if (transform.position.y < -3f)
                {
                    OnChargeHitPlayer();
                    break;
                }
                
                // 检查是否被推住（速度接近0）
                if (rb.velocity.magnitude < 1f && dashElapsed > 0.5f)
                {
                    OnChargeBlocked();
                    break;
                }
                
                yield return null;
            }
            
            // 冲撞结束，弹回原位
            rb.velocity = Vector2.zero;
            isCharging = false;
            
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
#endif
            
            // 返回 Idle
            ChangeState(BossState.Idle);
        }
        
        private void OnChargeHitPlayer()
        {
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 💥 撞击玩家！");
            }
            
            // 对玩家造成伤害
            float damage = config != null ? config.chargeHitDamage : 300f;
            
            // TODO: 调用玩家受伤接口
            // PlayerHealth.Instance?.TakeDamage(damage);
            
            // 屏幕震动
            float shakeIntensity = config != null ? config.chargeHitShakeIntensity : 0.8f;
            float shakeDuration = config != null ? config.chargeHitShakeDuration : 0.3f;
            
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.ImpactShake(Vector2.down, shakeIntensity, shakeDuration);
            }
        }
        
        private void OnChargeBlocked()
        {
            if (showDebugInfo)
            {
                Debug.Log("[BossController] 🛡️ 冲撞被激光推住！");
            }
            
            // 可以进入僵直作为奖励
            // ChangeState(BossState.Stun);
        }
        
        /// <summary>
        /// 打断冲撞（由外部调用，如僵直技能命中核心）
        /// </summary>
        public void InterruptCharge()
        {
            if (currentState == BossState.Charge && !isCharging)
            {
                chargeInterrupted = true;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // State: Stun (僵直)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator StunRoutine()
        {
            float duration = config != null ? config.stunDuration : 2.5f;
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossController] 💫 进入僵直！持续 {duration}s - 这是奖励时间！");
            }
            
            // 眼睛保持睁开（奖励玩家）
            if (eyeController != null)
            {
                eyeController.Open();
            }
            
            // 身体变暗
            DarkenColors();
            
            // 停止移动
            rb.velocity = Vector2.zero;
            
            // 等待僵直结束
            yield return new WaitForSeconds(duration);
            
            // 恢复颜色
            RestoreColors();
            
            // 返回 Idle
            ChangeState(BossState.Idle);
        }
        
        private void DarkenColors()
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
            
            GUILayout.BeginArea(new Rect(10, 150, 220, 140));
            GUILayout.Label("=== Boss Controller ===");
            GUILayout.Label($"State: {currentState}");
            GUILayout.Label($"HP: {HealthPercent:P1}");
            GUILayout.Label($"Eye: {(eyeController != null ? eyeController.CurrentState.ToString() : "N/A")}");
            GUILayout.Label($"Mobs: {GetCurrentMobCount()}");
            
            if (currentState == BossState.Idle)
            {
                GUILayout.Label($"Idle Timer: {idleTimer:F1}/{currentIdleDuration:F1}");
            }
            
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
        }
#endif
    }
}