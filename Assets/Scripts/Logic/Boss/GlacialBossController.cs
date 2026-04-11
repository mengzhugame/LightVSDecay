// ============================================================
// GlacialBossController.cs
// 文件位置: Assets/Scripts/Logic/Boss/GlacialBossController.cs
// 用途：第三章 Boss ——《极寒之核 (The Glacial Core)》
// 继承 BaseBossController，实现以下专属机制：
//
//   阶段一（HP>65%）：冰墙构建 + 冰封射线 + 极寒冲撞
//   阶段二（30%<HP≤65%）：同上，额外每30秒召唤一只极寒坦克
//   阶段三（HP≤30%）：绝对零度冷却缩短至12秒，高频压迫
//
//   技能1 冰墙构建：RedBodyEffect发光蓄力 → 召唤2~3堵冰墙（30秒自动消失）
//   技能2 冰封射线：固定朝向光棱塔射出蓝色激光3秒；命中护盾→扣护盾血；命中塔→冻结1.5秒
//   技能3 极寒冲撞：霸体冲向塔，命中后冻结1.5秒
//   技能4 绝对零度：3秒蓄力后必定释放；玩家需击落冰刺以规避真伤、冻结和增援
// ============================================================

using UnityEngine;
using System.Collections;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Logic.Player;
using LightVsDecay.Logic.Statistics;

namespace LightVsDecay.Logic.Boss
{
    /// <summary>
    /// 第三章Boss：极寒之核。
    /// </summary>
    public class GlacialBossController : BaseBossController
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置（Ch3 专属）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("冰墙构建 · 配置")]
        [Tooltip("每次召唤冰墙的最小数量")]
        [SerializeField] private int iceWallCountMin = 2;

        [Tooltip("每次召唤冰墙的最大数量")]
        [SerializeField] private int iceWallCountMax = 3;

        [Header("冰墙构建 · 视觉")]
        [Tooltip("蓄力发光淡入时长（秒）")]
        [SerializeField] private float iceWallGlowFadeInDuration = 1.5f;

        [Tooltip("发光淡出时长（秒）")]
        [SerializeField] private float iceWallGlowFadeOutDuration = 1f;

        [Header("冰封射线 · 配置")]
        [Tooltip("蓄力时长（秒），期间顶部水晶闪烁")]
        [SerializeField] private float freezeRayChargeUpDuration = 1f;

        [Tooltip("射线持续时长（秒）")]
        [SerializeField] private float freezeRayDuration = 3f;

        [Tooltip("射线冷却时长（秒）")]
        [SerializeField] private float freezeRayCooldownTime = 20f;

        [Tooltip("命中光棱塔本体的冻结时长（秒）")]
        [SerializeField] private float freezeRayTurretFreezeDuration = 1.5f;

        [Tooltip("命中护盾时每秒造成的护盾伤害")]
        [SerializeField] private float freezeRayShieldDamagePerSecond = 500f;

        [Tooltip("射线检测最大长度（世界单位）")]
        [SerializeField] private float freezeRayLength = 20f;

        [Tooltip("射线从 Boss 中心延伸至满长度所需时长（秒），给玩家反应时间，延伸期无伤害")]
        [SerializeField] private float freezeRayExtendDuration = 1.5f;

        [Tooltip("玩家激光与冰封射线角力时，将射线顶回的速度倍率")]
        [SerializeField] private float freezeRayClashPushSpeed = 7.5f;

        [Tooltip("玩家持续用激光压制时，打断冰封射线所需的累计角力值")]
        [SerializeField] private float freezeRayClashBreakThreshold = 16f;

        [Tooltip("Boss 施放冰封射线前回到中轴锚点所需时间（秒）")]
        [SerializeField] private float freezeRayRepositionDuration = 0.3f;

        [Tooltip("冰封射线相持的最大持续时间；超时后 Boss 会主动收束并后撤")]
        [SerializeField] private float freezeRayStalemateTimeout = 2.5f;

        [Tooltip("冰封射线结束后 Boss 快速后撤的距离")]
        [SerializeField] private float freezeRayRetreatDistance = 1.2f;

        [Tooltip("冰封射线结束后 Boss 快速后撤时长")]
        [SerializeField] private float freezeRayRetreatDuration = 0.22f;

        [Header("冰封射线 · 碰撞体（角力用）")]
        [Tooltip("挂在 Frost_Boss 任意子节点上的 BoxCollider2D（Enemy 层），供玩家激光碰撞止点以实现角力。\n" +
                 "若不赋值则运行时自动创建。")]
        [SerializeField] private BoxCollider2D laserHitbox;

        [Tooltip("冰封射线前端的角力点。建议使用 BossFreezeRayClashPoint 节点。")]
        [SerializeField] private Transform freezeRayClashPoint;

        [Header("冰封射线 · 视觉")]
        [Tooltip("Laser 节点上的 LineRenderer 组件（蓝色激光）")]
        [SerializeField] private LineRenderer laserLineRenderer;

        [Tooltip("激光末端特效 Transform（EndVFX 节点），跟随射线终点")]
        [SerializeField] private Transform laserEndVFX;

        [Tooltip("顶部水晶 SpriteRenderer（Body03 节点），蓄力时闪烁")]
        [SerializeField] private SpriteRenderer body03Crystal;

        [Header("极寒冲撞 · 配置")]
        [Tooltip("冲撞命中后光棱塔冻结时长（秒）")]
        [SerializeField] private float chargeTurretFreezeDuration = 1.5f;

        [Header("绝对零度 · 配置")]
        [Tooltip("蓄力时长（秒）")]
        [SerializeField] private float absoluteZeroChannelDuration = 3f;

        [Tooltip("绝对零度释放后，光棱塔当前血量的百分比真实伤害")]
        [SerializeField] private float absoluteZeroTrueDamagePercent = 0.30f;

        [Tooltip("打断失败时，光棱塔冻结时长（秒）")]
        [SerializeField] private float absoluteZeroTurretFreezeDuration = 3f;

        [Tooltip("打断失败时，额外召唤的 FrostSlime 数量")]
        [SerializeField] private int absoluteZeroSummonCount = 15;

        [Tooltip("阶段1/2的绝对零度冷却（秒）")]
        [SerializeField] private float absoluteZeroCooldownPhase12 = 30f;

        [Tooltip("阶段3的绝对零度冷却（秒）（≤30%HP）")]
        [SerializeField] private float absoluteZeroCooldownPhase3 = 12f;

        [Tooltip("绝对零度全屏警告特效 GameObject（为空则跳过）")]
        [SerializeField] private GameObject absoluteZeroWarningEffect;

        [Header("绝对零度 · 冰刺")]
        [Tooltip("BingCi01~04 节点 Transform（左上、左下、右上、右下）")]
        [SerializeField] private Transform[] bingCiTransforms = new Transform[4];

        [Tooltip("BingCi01~04 节点 SpriteRenderer（用于控制显隐）")]
        [SerializeField] private SpriteRenderer[] bingCiRenderers = new SpriteRenderer[4];

        [Tooltip("冰刺投射体 Prefab（需含 IceSpikeProjectile 脚本，层级 BossPollutionBall）")]
        [SerializeField] private GameObject iceSpikeProjectilePrefab;

        [Tooltip("冰刺拔出位移量（世界单位）")]
        [SerializeField] private float bingCiPullOutDistance = 0.4f;

        [Tooltip("冰刺拔出动画时长（秒）")]
        [SerializeField] private float bingCiPullOutDuration = 0.4f;

        [Tooltip("单枚冰刺旋转对准动画时长（秒）")]
        [SerializeField] private float bingCiRotateDuration = 0.6f;

        [Tooltip("四枚冰刺依次开始旋转的间隔（秒）")]
        [SerializeField] private float bingCiRotateStagger = 0.1f;

        [Tooltip("四枚冰刺依次射出的间隔（秒）")]
        [SerializeField] private float bingCiFireStagger = 0.15f;

        [Tooltip("冰刺射出后等待再生的延迟（秒）")]
        [SerializeField] private float bingCiRegenDelay = 2f;

        [Tooltip("冰刺再生淡入时长（秒）")]
        [SerializeField] private float bingCiRegenFadeDuration = 0.5f;

        [Header("阶段二 · 极寒坦克召唤")]
        [Tooltip("阶段2每隔多少秒召唤一只极寒坦克")]
        [SerializeField] private float phase2TankSummonInterval = 30f;

        [Header("冰封射线触发概率")]
        [Tooltip("冷却就绪且选到主动技能时触发冰封射线的概率（0~1）")]
        [SerializeField] private float freezeRayTriggerChance = 0.4f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private int currentPhase = 1;

        private float freezeRayCooldownTimer = 0f;
        private bool freezeRayReady = false;
        private bool freezeRayHitThisCast = false;
        private bool freezeRayActive = false;
        private float freezeRayClashMeter = 0f;
        private float freezeRayPlayerPressure = 0f;
        private float freezeRayLastPressureTime = -999f;
        private Collider2D freezeRayClashCollider;

        private float absoluteZeroCooldownTimer = 0f;
        private bool absoluteZeroTriggered = false;
        private bool absoluteZeroActive = false;

        private float phase2TankTimer = 0f;

        private TurretController turretController;
        private TurretHealth turretHealth;
        private LayerMask rayHitLayers;

        // BingCi 原始局部坐标（用于拔出计算与再生复位）
        private Vector3[] bingCiOriginalLocalPositions;
        private Quaternion[] bingCiOriginalLocalRotations;

        // 绝对零度冰刺结算（跨协程共享状态）
        private int bingCiResolvedCount;
        private bool bingCiAnyHitTower;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BaseBossController 钩子实现
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void OnBossInitialized()
        {
            currentPhase = 1;
            absoluteZeroActive = false;
            absoluteZeroTriggered = false;

            freezeRayCooldownTimer = 0f;
            freezeRayReady = true;

            absoluteZeroCooldownTimer = absoluteZeroCooldownPhase12;
            absoluteZeroTriggered = false;

            phase2TankTimer = phase2TankSummonInterval;

            FindTurretComponents();

            rayHitLayers = LayerMask.GetMask("Shield", "Tower");

            // 初始化 RedBodyEffect：alpha 设为 0
            if (redBodyEffect != null)
            {
                Color c = redBodyEffect.GetComponent<SpriteRenderer>().color;
                c.a = 0f;
                redBodyEffect.GetComponent<SpriteRenderer>().color = c;
            }

            // 初始隐藏激光 LineRenderer
            if (laserLineRenderer != null)
                laserLineRenderer.enabled = false;

            if (laserEndVFX != null)
                laserEndVFX.gameObject.SetActive(false);

            if (absoluteZeroWarningEffect != null)
                absoluteZeroWarningEffect.SetActive(false);

            // 缓存 BingCi 原始局部坐标
            if (bingCiTransforms != null)
            {
                bingCiOriginalLocalPositions = new Vector3[bingCiTransforms.Length];
                bingCiOriginalLocalRotations = new Quaternion[bingCiTransforms.Length];
                for (int i = 0; i < bingCiTransforms.Length; i++)
                {
                    if (bingCiTransforms[i] != null)
                    {
                        bingCiOriginalLocalPositions[i] = bingCiTransforms[i].localPosition;
                        bingCiOriginalLocalRotations[i] = bingCiTransforms[i].localRotation;
                    }
                }
            }

            InitializeFreezeRayClashPoint();
            DisableFreezeRayVisuals();
        }

        // ── 阶段与冷却更新 ──────────────────────────────────

        protected override void OnExtraUpdate()
        {
            UpdatePhase();
            UpdateFreezeRayCooldown();
            UpdateAbsoluteZeroCooldown();

            if (currentPhase == 2)
                UpdatePhase2TankSummon();
        }

        private void UpdatePhase()
        {
            float hp = HealthPercent;

            if (currentPhase == 1 && hp <= 0.65f)
            {
                currentPhase = 2;
                phase2TankTimer = phase2TankSummonInterval;
                if (showDebugInfo)
                    GameLogger.Log("[GlacialBoss] 进入阶段二：极寒爆发！");
            }
            else if (currentPhase < 3 && hp <= 0.3f)
            {
                currentPhase = 3;
                absoluteZeroCooldownTimer = Mathf.Min(absoluteZeroCooldownTimer, absoluteZeroCooldownPhase3);
                if (showDebugInfo)
                    GameLogger.Log("[GlacialBoss] 进入阶段三：绝对压制！");
            }
        }

        private void UpdateFreezeRayCooldown()
        {
            if (freezeRayReady) return;
            freezeRayCooldownTimer -= Time.deltaTime;
            if (freezeRayCooldownTimer <= 0f)
            {
                freezeRayCooldownTimer = 0f;
                freezeRayReady = true;
            }
        }

        private void UpdateAbsoluteZeroCooldown()
        {
            if (absoluteZeroActive || absoluteZeroTriggered) return;
            absoluteZeroCooldownTimer -= Time.deltaTime;
            if (absoluteZeroCooldownTimer <= 0f)
            {
                absoluteZeroTriggered = true;
                TriggerAbsoluteZero();
            }
        }

        private void UpdatePhase2TankSummon()
        {
            if (EnemyPoolManager.Instance == null) return;
            phase2TankTimer -= Time.deltaTime;
            if (phase2TankTimer <= 0f)
            {
                phase2TankTimer = phase2TankSummonInterval;
                Vector3 spawnPos = GetRandomSpawnEdgePosition();
                EnemyPoolManager.Instance.Spawn(EnemyType.FrostTank, spawnPos);
                if (showDebugInfo)
                    GameLogger.Log("[GlacialBoss] 阶段2额外召唤极寒坦克");
            }
        }

        // ── Idle 回调 ────────────────────────────────────────

        protected override void OnEnterIdle() { }
        protected override void OnIdlePassiveUpdate(float deltaTime) { }

        // ── 主动技能选择 ─────────────────────────────────────

        protected override void ChooseActiveSkill()
        {
            if (freezeRayReady && Random.value < freezeRayTriggerChance)
            {
                StartFreezeRay();
                return;
            }
            ChangeState(BossState.Charge);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能1：冰墙构建
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override IEnumerator ExecuteSummonBehavior()
        {
            if (EnemyPoolManager.Instance == null)
            {
                if (showDebugInfo) GameLogger.LogWarning("[GlacialBoss] EnemyPoolManager 不存在！");
                yield break;
            }

            // 蓄力发光（1.5s）
            yield return StartCoroutine(FadeAlpha(redBodyEffect.GetComponent<SpriteRenderer>(), 0f, 1f, iceWallGlowFadeInDuration));

            // 生成冰墙
            int count = Random.Range(iceWallCountMin, iceWallCountMax + 1);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomIceWallPosition();
                EnemyPoolManager.Instance.Spawn(EnemyType.IceWall, pos);
                if (count > 1) yield return new WaitForSeconds(0.2f);
            }

            BattleStatistics.Instance?.RecordGlacialBossIceWallBuild();

            if (showDebugInfo)
                GameLogger.Log($"[GlacialBoss] 冰墙构建：召唤 {count} 堵冰墙");

            // 发光淡出（1s）
            yield return StartCoroutine(FadeAlpha(redBodyEffect.GetComponent<SpriteRenderer>(), 1f, 0f, iceWallGlowFadeOutDuration));
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能2：冰封射线（固定方向，不扫射）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void StartFreezeRay()
        {
            freezeRayReady = false;
            freezeRayCooldownTimer = freezeRayCooldownTime;

            ChangeState(BossState.Stun);
            if (stateCoroutine != null) StopCoroutine(stateCoroutine);
            stateCoroutine = StartCoroutine(FreezeRayRoutine());
        }

        private IEnumerator FreezeRayRoutine()
        {
            BattleStatistics.Instance?.RecordGlacialBossFreezeRay();
            if (showDebugInfo) GameLogger.Log("[GlacialBoss] 冰封射线开始！");

            freezeRayHitThisCast = false;
            freezeRayActive = false;
            freezeRayClashMeter = 0f;
            freezeRayPlayerPressure = 0f;
            freezeRayLastPressureTime = -999f;

            yield return StartCoroutine(MoveToFreezeRayAnchor());

            // === 蓄力阶段：顶部水晶闪烁 ===
            yield return StartCoroutine(Body03BlinkRoutine(freezeRayChargeUpDuration));

            // 计算固定方向（Boss中心 → 光棱塔），整个射线阶段不变
            Vector2 rayDir;
            if (turretController != null)
                rayDir = (turretController.transform.position - transform.position).normalized;
            else
                rayDir = Vector2.down;

            // 启用 LineRenderer 和末端特效（初始长度为 0）
            if (laserLineRenderer != null)
            {
                laserLineRenderer.enabled = true;
                laserLineRenderer.SetPosition(0, transform.position);
                laserLineRenderer.SetPosition(1, transform.position);
            }
            if (laserEndVFX != null)
            {
                laserEndVFX.gameObject.SetActive(true);
                laserEndVFX.position = transform.position;
            }

            // === 延伸阶段（freezeRayExtendDuration 秒）：射线从 0 缓慢延伸至满长度，无伤害 ===
            float extendElapsed = 0f;
            while (extendElapsed < freezeRayExtendDuration)
            {
                extendElapsed += Time.deltaTime;
                float extendT  = Mathf.Clamp01(extendElapsed / freezeRayExtendDuration);
                float curLength = Mathf.Lerp(0f, freezeRayLength, extendT);

                Vector2 extendEnd = (Vector2)transform.position + rayDir * curLength;
                if (laserLineRenderer != null)
                {
                    laserLineRenderer.SetPosition(0, transform.position);
                    laserLineRenderer.SetPosition(1, extendEnd);
                }
                if (laserEndVFX != null)
                    laserEndVFX.position = extendEnd;

                UpdateFreezeRayClashPoint(extendEnd);

                if (rb != null) rb.velocity = Vector2.zero;
                yield return null;
            }

            freezeRayActive = true;

            // === 角力/伤害阶段（freezeRayDuration 秒）===
            float elapsed = 0f;
            bool wasCountered = false;
            bool endedByStalemate = false;
            while (elapsed < freezeRayDuration)
            {
                elapsed += Time.deltaTime;

                float timeSincePressure = Time.time - freezeRayLastPressureTime;
                bool playerClashing = timeSincePressure <= 0.18f && freezeRayPlayerPressure > 0f;
                if (playerClashing)
                {
                    freezeRayClashMeter += freezeRayPlayerPressure * freezeRayClashPushSpeed * Time.deltaTime;
                }
                else
                {
                    freezeRayClashMeter = Mathf.MoveTowards(freezeRayClashMeter, 0f, 6f * Time.deltaTime);
                }

                float clashRatio = Mathf.Clamp01(freezeRayClashMeter / freezeRayClashBreakThreshold);
                float effectiveRayLength = Mathf.Lerp(freezeRayLength, 1.25f, clashRatio);

                if (freezeRayClashMeter >= freezeRayClashBreakThreshold)
                {
                    if (showDebugInfo)
                        GameLogger.Log("[GlacialBoss] 冰封射线被玩家激光顶回并打断");
                    ShowStatusText(BossStatusTextType.Countered);
                    wasCountered = true;
                    break;
                }

                if (elapsed >= freezeRayStalemateTimeout && playerClashing && freezeRayClashMeter > 0.01f)
                {
                    endedByStalemate = true;
                    if (showDebugInfo)
                        GameLogger.Log("[GlacialBoss] 冰封射线相持超时，主动收束后撤");
                    break;
                }

                // Raycast：检测护盾层与塔本体层
                var hit = Physics2D.Raycast(transform.position, rayDir, effectiveRayLength, rayHitLayers);
                Vector2 endPoint = (Vector2)transform.position + rayDir * effectiveRayLength;

                if (hit.collider != null)
                {
                    endPoint = hit.point;

                    if (!playerClashing)
                    {
                        ShieldController sc = hit.collider.GetComponent<ShieldController>();
                        if (sc != null)
                        {
                            // 命中护盾：每帧扣血，不冻结
                            sc.TakeDamage(Mathf.RoundToInt(freezeRayShieldDamagePerSecond * Time.deltaTime));
                        }
                        else if (!freezeRayHitThisCast)
                        {
                            // 命中塔本体：触发冻结（本次施法只触发一次）
                            freezeRayHitThisCast = true;
                            ApplyTurretFreeze(freezeRayTurretFreezeDuration);
                            if (showDebugInfo)
                                GameLogger.Log($"[GlacialBoss] 冰封射线命中塔！冻结 {freezeRayTurretFreezeDuration}s");
                        }
                    }
                }

                // 更新 LineRenderer 端点
                if (laserLineRenderer != null)
                {
                    laserLineRenderer.SetPosition(0, transform.position);
                    laserLineRenderer.SetPosition(1, endPoint);
                }

                // 末端特效跟随
                if (laserEndVFX != null)
                    laserEndVFX.position = endPoint;

                UpdateFreezeRayClashPoint(endPoint);

                // 射线期间 Boss 保持静止
                if (rb != null) rb.velocity = Vector2.zero;

                yield return null;
            }

            if (!wasCountered && endedByStalemate)
            {
                yield return StartCoroutine(FreezeRayRetreatRoutine());
            }

            // === 结束：关闭视觉 ===
            DisableFreezeRayVisuals();

            if (showDebugInfo) GameLogger.Log("[GlacialBoss] 冰封射线结束");
            ChangeState(BossState.Idle);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能3：极寒冲撞
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void OnChargeDashComplete(Vector3 startPos, Vector3 endPos)
        {
            BattleStatistics.Instance?.RecordGlacialBossCharge();
            ApplyTurretFreeze(chargeTurretFreezeDuration);
            if (showDebugInfo)
                GameLogger.Log($"[GlacialBoss] 极寒冲撞落地，冻结光棱塔 {chargeTurretFreezeDuration}s");
        }

        protected override float GetChargeSpeedMultiplier()
        {
            return currentPhase >= 3 ? 1.15f : 1f;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能4：绝对零度
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void TriggerAbsoluteZero()
        {
            if (absoluteZeroActive) return;
            absoluteZeroActive = true;
            if (showDebugInfo) GameLogger.Log("[GlacialBoss] 绝对零度启动！");

            InterruptCharge();
            ChangeState(BossState.Stun);
            if (stateCoroutine != null) StopCoroutine(stateCoroutine);
            stateCoroutine = StartCoroutine(AbsoluteZeroRoutine());
        }

        private IEnumerator AbsoluteZeroRoutine()
        {
            BattleStatistics.Instance?.RecordGlacialBossAbsoluteZero();
            if (showDebugInfo) GameLogger.Log("[GlacialBoss] 绝对零度：3秒蓄力开始");

            if (absoluteZeroWarningEffect != null)
                absoluteZeroWarningEffect.SetActive(true);

            LightVsDecay.UI.FloatingText.FloatingTextManager.Instance?.ShowStatus(
                transform.position, "绝对零度！击落冰刺以规避伤害！");

            float elapsed = 0f;

            while (elapsed < absoluteZeroChannelDuration)
            {
                elapsed += Time.deltaTime;
                rb.velocity = Vector2.zero;
                yield return null;
            }

            if (absoluteZeroWarningEffect != null)
                absoluteZeroWarningEffect.SetActive(false);

            if (showDebugInfo) GameLogger.Log("[GlacialBoss] 绝对零度释放！");
            yield return StartCoroutine(AbsoluteZeroDetonateRoutine());

            absoluteZeroActive = false;
            absoluteZeroCooldownTimer = currentPhase >= 3
                ? absoluteZeroCooldownPhase3
                : absoluteZeroCooldownPhase12;
            absoluteZeroTriggered = false;
            ChangeState(BossState.Idle);
        }

        private IEnumerator AbsoluteZeroDetonateRoutine()
        {
            int totalSpikes = (bingCiTransforms != null) ? bingCiTransforms.Length : 0;
            bingCiResolvedCount = 0;
            bingCiAnyHitTower = false;

            // 无冰刺配置时直接惩罚（向下兼容）
            if (totalSpikes == 0 || iceSpikeProjectilePrefab == null)
            {
                ApplyAbsoluteZeroPenalty();
                yield return new WaitForSeconds(0.5f);
                yield break;
            }

            // Step 1：拔出（0.4s）
            yield return StartCoroutine(BingCiPullOut());

            // Step 2：旋转对准（0.6s，错开 0.1s）
            Vector2 dirToTurret = turretController != null
                ? (turretController.transform.position - transform.position).normalized
                : Vector2.down;
            yield return StartCoroutine(BingCiRotateTowardTower(dirToTurret));

            // Step 3：射出冰刺（错开 0.15s），隐藏 BingCi 精灵
            for (int i = 0; i < totalSpikes; i++)
            {
                if (i > 0) yield return new WaitForSeconds(bingCiFireStagger);

                if (bingCiTransforms[i] == null) { bingCiResolvedCount++; continue; }

                // 生成投射体
                Vector3 spawnPos = bingCiTransforms[i].position;
                // 每枚冰刺从自身位置独立计算朝向塔的方向
                Vector2 perFireDir = turretController != null
                    ? ((Vector2)(turretController.transform.position - spawnPos)).normalized
                    : dirToTurret;
                GameObject spikeGO = Instantiate(iceSpikeProjectilePrefab, spawnPos, Quaternion.identity);
                IceSpikeProjectile spike = spikeGO.GetComponent<IceSpikeProjectile>();
                if (spike != null)
                {
                    spike.OnReachedTower += () => { bingCiResolvedCount++; bingCiAnyHitTower = true; };
                    spike.OnDestroyedByLaser += () => { bingCiResolvedCount++; };
                    spike.Launch(perFireDir);
                }
                else
                {
                    bingCiResolvedCount++; // 防御性计数
                }

                // 隐藏对应 BingCi 精灵
                if (bingCiRenderers != null && i < bingCiRenderers.Length && bingCiRenderers[i] != null)
                {
                    Color c = bingCiRenderers[i].color;
                    c.a = 0f;
                    bingCiRenderers[i].color = c;
                }
            }

            // 等待所有冰刺结算（超时 10s）
            float waitTimer = 0f;
            while (bingCiResolvedCount < totalSpikes && waitTimer < 10f)
            {
                waitTimer += Time.deltaTime;
                yield return null;
            }

            // 惩罚结算：任意冰刺命中塔即触发
            if (bingCiAnyHitTower)
                ApplyAbsoluteZeroPenalty();

            // 延迟后冰刺再生
            yield return new WaitForSeconds(bingCiRegenDelay);
            yield return StartCoroutine(BingCiRegen());

            yield return new WaitForSeconds(0.5f);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 辅助方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void ApplyAbsoluteZeroPenalty()
        {
            // 真实伤害：光棱塔当前血量的 30%
            if (turretHealth != null)
            {
                int trueDamage = Mathf.RoundToInt(turretHealth.CurrentHullHP * absoluteZeroTrueDamagePercent);
                turretHealth.TakeDamage(trueDamage);
                if (showDebugInfo)
                    GameLogger.Log($"[GlacialBoss] 绝对零度真实伤害 {trueDamage}");
            }

            // 冻结光棱塔
            ApplyTurretFreeze(absoluteZeroTurretFreezeDuration);

            // 震屏
            CameraShake.Instance?.Shake(0.8f, 0.5f);

            // 额外召唤增援
            if (EnemyPoolManager.Instance != null)
            {
                for (int i = 0; i < absoluteZeroSummonCount; i++)
                {
                    Vector3 pos = GetRandomSpawnEdgePosition();
                    EnemyPoolManager.Instance.Spawn(EnemyType.FrostSlime, pos);
                }
            }
        }

        // ── BingCi 动画协程 ──────────────────────────────────

        /// <summary>Step1：冰刺拔出——各自向外位移 bingCiPullOutDistance（0.4s）</summary>
        private IEnumerator BingCiPullOut()
        {
            if (bingCiTransforms == null || bingCiOriginalLocalPositions == null || bingCiOriginalLocalRotations == null) yield break;

            int n = bingCiTransforms.Length;
            Vector3[] startPositions = new Vector3[n];
            Vector3[] targetPositions = new Vector3[n];

            for (int i = 0; i < n; i++)
            {
                if (bingCiTransforms[i] == null) continue;
                startPositions[i] = bingCiTransforms[i].localPosition;
                Vector3 outDir = bingCiOriginalLocalPositions[i] == Vector3.zero
                    ? Vector3.up
                    : bingCiOriginalLocalPositions[i].normalized;
                targetPositions[i] = bingCiOriginalLocalPositions[i] + outDir * bingCiPullOutDistance;
            }

            float elapsed = 0f;
            while (elapsed < bingCiPullOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / bingCiPullOutDuration);
                for (int i = 0; i < n; i++)
                {
                    if (bingCiTransforms[i] == null) continue;
                    bingCiTransforms[i].localPosition = Vector3.Lerp(startPositions[i], targetPositions[i], t);
                }
                yield return null;
            }
        }

        /// <summary>Step2：四枚冰刺错开 rotateStagger 依次旋转对准光棱塔（每枚 0.6s）。
        /// 每枚冰刺独立计算自身朝向光棱塔的方向，避免因与 Boss 中心偏移导致刺尖方向偏斜。</summary>
        private IEnumerator BingCiRotateTowardTower(Vector2 dirToTurretFromBoss)
        {
            if (bingCiTransforms == null) yield break;

            int n = bingCiTransforms.Length;
            // 并发启动，各自延迟 i * stagger；每枚冰刺从自身世界坐标计算方向
            for (int i = 0; i < n; i++)
            {
                if (bingCiTransforms[i] == null) continue;
                // 各冰刺独立方向：从自身位置指向光棱塔
                Vector2 perSpikeDir = turretController != null
                    ? ((Vector2)(turretController.transform.position - bingCiTransforms[i].position)).normalized
                    : dirToTurretFromBoss;
                // 刺尖方向对准 perSpikeDir：localRotation 0 时刺尖朝上，所以用 Atan2 - 90° 修正
                float angle = Mathf.Atan2(perSpikeDir.y, perSpikeDir.x) * Mathf.Rad2Deg - 90f;
                Quaternion targetRot = Quaternion.Euler(0f, 0f, angle);
                StartCoroutine(RotateSingleBingCi(i, targetRot, i * bingCiRotateStagger));
            }

            // 等待最后一枚完成
            float totalWait = (n - 1) * bingCiRotateStagger + bingCiRotateDuration;
            yield return new WaitForSeconds(totalWait);
        }

        private IEnumerator RotateSingleBingCi(int index, Quaternion targetRot, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (bingCiTransforms == null || index >= bingCiTransforms.Length || bingCiTransforms[index] == null)
                yield break;

            Quaternion startRot = bingCiTransforms[index].localRotation;
            float elapsed = 0f;
            while (elapsed < bingCiRotateDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / bingCiRotateDuration);
                bingCiTransforms[index].localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
        }

        /// <summary>再生：复位 BingCi 位置/旋转，alpha 0→1（0.5s）</summary>
        private IEnumerator BingCiRegen()
        {
            if (bingCiTransforms == null || bingCiOriginalLocalPositions == null) yield break;

            int n = bingCiTransforms.Length;

            // 复位变换
            for (int i = 0; i < n; i++)
            {
                if (bingCiTransforms[i] == null) continue;
                bingCiTransforms[i].localPosition = bingCiOriginalLocalPositions[i];
                bingCiTransforms[i].localRotation = bingCiOriginalLocalRotations[i];
            }

            // Alpha 淡入
            float elapsed = 0f;
            while (elapsed < bingCiRegenFadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / bingCiRegenFadeDuration);
                if (bingCiRenderers != null)
                {
                    for (int i = 0; i < bingCiRenderers.Length; i++)
                    {
                        if (bingCiRenderers[i] == null) continue;
                        Color c = bingCiRenderers[i].color;
                        c.a = alpha;
                        bingCiRenderers[i].color = c;
                    }
                }
                yield return null;
            }
        }

        // ── 通用视觉辅助 ─────────────────────────────────────

        /// <summary>顶部水晶蓄力闪烁协程（duration 秒）</summary>
        private IEnumerator Body03BlinkRoutine(float duration)
        {
            if (body03Crystal == null)
            {
                yield return new WaitForSeconds(duration);
                yield break;
            }

            float elapsed = 0f;
            bool visible = true;
            float blinkInterval = 0.2f;
            float nextBlink = blinkInterval;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                nextBlink -= Time.deltaTime;
                if (nextBlink <= 0f)
                {
                    visible = !visible;
                    body03Crystal.enabled = visible;
                    nextBlink = blinkInterval;
                }
                yield return null;
            }
            body03Crystal.enabled = true;
        }

        /// <summary>SpriteRenderer alpha 线性渐变（fromAlpha → toAlpha，duration 秒）</summary>
        private IEnumerator FadeAlpha(SpriteRenderer sr, float fromAlpha, float toAlpha, float duration)
        {
            if (sr == null) { yield return new WaitForSeconds(duration); yield break; }

            Color c = sr.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(fromAlpha, toAlpha, elapsed / duration);
                sr.color = c;
                yield return null;
            }
            c.a = toAlpha;
            sr.color = c;
        }

        // ── 通用逻辑辅助 ─────────────────────────────────────

        private void FindTurretComponents()
        {
            turretController = FindObjectOfType<TurretController>();
            turretHealth     = FindObjectOfType<TurretHealth>();

            if (showDebugInfo)
            {
                if (turretController == null) GameLogger.LogWarning("[GlacialBoss] 未找到 TurretController！");
                if (turretHealth     == null) GameLogger.LogWarning("[GlacialBoss] 未找到 TurretHealth！");
            }
        }

        private void ApplyTurretFreeze(float duration)
        {
            if (turretController != null)
                turretController.Freeze(duration);
        }

        public void NotifyFreezeRayLaserClash(float pressure)
        {
            if (!freezeRayActive || pressure <= 0f)
                return;

            freezeRayPlayerPressure = pressure;
            freezeRayLastPressureTime = Time.time;
        }

        protected override void ExitState(BossState state)
        {
            DisableFreezeRayVisuals();
            base.ExitState(state);
        }

        private void OnDisable()
        {
            DisableFreezeRayVisuals();
        }

        private void InitializeFreezeRayClashPoint()
        {
            if (freezeRayClashPoint == null)
            {
                freezeRayClashPoint = FindChildRecursive(transform, "BossFreezeRayClashPoint");
            }

            if (freezeRayClashPoint == null)
            {
                GameObject clashPointGO = new GameObject("BossFreezeRayClashPoint");
                clashPointGO.transform.SetParent(transform, false);
                freezeRayClashPoint = clashPointGO.transform;
            }

            freezeRayClashPoint.gameObject.layer = LayerMask.NameToLayer(GameConstants.ENEMY_LAYER);

            freezeRayClashCollider = freezeRayClashPoint.GetComponent<Collider2D>();
            if (freezeRayClashCollider == null)
            {
                CircleCollider2D clashCircle = freezeRayClashPoint.gameObject.AddComponent<CircleCollider2D>();
                clashCircle.radius = 0.22f;
                freezeRayClashCollider = clashCircle;
            }

            if (freezeRayClashPoint.GetComponent<FreezeRayClashPoint>() == null)
            {
                freezeRayClashPoint.gameObject.AddComponent<FreezeRayClashPoint>();
            }

            if (laserHitbox != null)
            {
                laserHitbox.enabled = false;
            }

            if (freezeRayClashCollider != null)
            {
                freezeRayClashCollider.enabled = false;
            }
        }

        private void DisableFreezeRayVisuals()
        {
            freezeRayActive = false;

            if (laserHitbox != null)
                laserHitbox.enabled = false;

            if (freezeRayClashCollider != null)
                freezeRayClashCollider.enabled = false;

            if (laserLineRenderer != null)
                laserLineRenderer.enabled = false;

            if (laserEndVFX != null)
                laserEndVFX.gameObject.SetActive(false);
        }

        private void UpdateFreezeRayClashPoint(Vector2 position)
        {
            if (freezeRayClashPoint == null)
                return;

            freezeRayClashPoint.position = new Vector3(position.x, position.y, freezeRayClashPoint.position.z);

            if (freezeRayClashCollider != null)
                freezeRayClashCollider.enabled = true;
        }

        private IEnumerator MoveToFreezeRayAnchor()
        {
            Vector3 start = transform.position;
            Vector3 target = battleAnchorPosition;
            target.x = 0f;

            float duration = Mathf.Max(0.01f, freezeRayRepositionDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(start, target, t);
                if (rb != null) rb.velocity = Vector2.zero;
                yield return null;
            }

            transform.position = target;
        }

        private IEnumerator FreezeRayRetreatRoutine()
        {
            Vector3 start = transform.position;
            Vector3 target = battleAnchorPosition + Vector3.up * freezeRayRetreatDistance;
            float duration = Mathf.Max(0.01f, freezeRayRetreatDuration);
            float elapsed = 0f;

            DisableFreezeRayVisuals();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(start, target, t);
                if (rb != null) rb.velocity = Vector2.zero;
                yield return null;
            }

            transform.position = target;
        }

        private Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
                return null;

            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private Vector3 GetRandomIceWallPosition()
        {
            Camera cam = Camera.main;
            if (cam == null) return transform.position + Vector3.right * Random.Range(-3f, 3f);

            float halfW = cam.orthographicSize * cam.aspect;
            float halfH = cam.orthographicSize;
            float margin = 1.5f;

            float x = Random.Range(cam.transform.position.x - halfW + margin,
                                   cam.transform.position.x + halfW - margin);
            float y = Random.Range(cam.transform.position.y,
                                   cam.transform.position.y + halfH - margin);
            return new Vector3(x, y, 0f);
        }

        private Vector3 GetRandomSpawnEdgePosition()
        {
            Camera cam = Camera.main;
            if (cam == null) return transform.position + Vector3.up * 8f;

            float halfW = cam.orthographicSize * cam.aspect;
            float halfH = cam.orthographicSize;
            float edge  = 1.5f;

            float x = Random.Range(-halfW + edge, halfW - edge) + cam.transform.position.x;
            float y = cam.transform.position.y + halfH + edge;
            return new Vector3(x, y, 0f);
        }

        private void FloatingTextShowInterrupted()
        {
            var ftm = LightVsDecay.UI.FloatingText.FloatingTextManager.Instance;
            if (ftm != null)
                ftm.ShowStatus(transform.position, "INTERRUPTED!");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试 GUI
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

#if UNITY_EDITOR
        protected override void OnGUI()
        {
            base.OnGUI();
            if (!showDebugInfo) return;
            GUILayout.BeginArea(new Rect(10, 460, 340, 120));
            GUILayout.Label("=== Ch3 极寒之核 ===");
            GUILayout.Label($"阶段: {currentPhase}  |  HP: {HealthPercent:P1}");
            GUILayout.Label($"冰封射线: {(freezeRayReady ? "就绪" : $"冷却 {freezeRayCooldownTimer:F1}s")}");
            GUILayout.Label($"绝对零度: {(absoluteZeroActive ? "激活中" : absoluteZeroTriggered ? "等待触发" : $"冷却 {absoluteZeroCooldownTimer:F1}s")}");
            if (currentPhase >= 2)
                GUILayout.Label($"阶段2坦克计时: {phase2TankTimer:F1}s / {phase2TankSummonInterval}s");
            GUILayout.EndArea();
        }
#endif
    }

    /// <summary>
    /// Marks the front clash point of the Glacial Boss freeze ray so player
    /// lasers can visually collide with it without treating it as a normal
    /// boss-body hit.
    /// </summary>
    public class FreezeRayClashPoint : MonoBehaviour
    {
    }
}
