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
//   冰墙构建：召唤 2~3 堵冰墙阻断激光，可被击毁（Summon状态）
//   冰封射线：停驻后旋转冰蓝射线3秒，命中光棱塔冻结1.5秒
//   极寒冲撞：霸体冲向塔，命中后冻结1.5秒，路径不留伤害区域
//   绝对零度：最高优先，3秒蓄力；5000伤害可打断；否则塔受真伤+冻结+增援
// ============================================================

using UnityEngine;
using System.Collections;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Logic.Player;

namespace LightVsDecay.Logic.Boss
{
    /// <summary>
    /// 第三章Boss：极寒之核。
    /// 专属机制：冰墙构建（地形封锁）、冰封射线（范围冻结）、
    ///           极寒冲撞（冲撞冻结）、绝对零度（最高优先打断机制）。
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

        [Header("冰封射线 · 配置")]
        [Tooltip("射线旋转速度（度/秒）")]
        [SerializeField] private float freezeRayRotationSpeed = 60f;

        [Tooltip("冰封射线持续时间（秒）")]
        [SerializeField] private float freezeRayDuration = 3f;

        [Tooltip("冰封射线冷却时间（秒）")]
        [SerializeField] private float freezeRayCooldownTime = 15f;

        [Tooltip("射线命中光棱塔的冻结时长（秒）")]
        [SerializeField] private float freezeRayTurretFreezeDuration = 1.5f;

        [Tooltip("射线检测长度（世界单位）")]
        [SerializeField] private float freezeRayLength = 20f;

        [Tooltip("射线视觉 GameObject（旋转控制）。为空则跳过视觉")]
        [SerializeField] private Transform freezeRayVisual;

        [Header("极寒冲撞 · 配置")]
        [Tooltip("冲撞命中光棱塔的冻结时长（秒）")]
        [SerializeField] private float chargeTurretFreezeDuration = 1.5f;

        [Header("绝对零度 · 配置")]
        [Tooltip("蓄力时间（秒）")]
        [SerializeField] private float absoluteZeroChannelDuration = 3f;

        [Tooltip("打断所需总伤害量")]
        [SerializeField] private float absoluteZeroInterruptThreshold = 5000f;

        [Tooltip("打断失败时，光棱塔承受当前血量的百分比真实伤害")]
        [SerializeField] private float absoluteZeroTrueDamagePercent = 0.30f;

        [Tooltip("打断失败时，光棱塔冻结时长（秒）")]
        [SerializeField] private float absoluteZeroTurretFreezeDuration = 3f;

        [Tooltip("打断失败时，额外召唤的小怪数量")]
        [SerializeField] private int absoluteZeroSummonCount = 15;

        [Tooltip("阶段1/2的绝对零度冷却（秒）")]
        [SerializeField] private float absoluteZeroCooldownPhase12 = 30f;

        [Tooltip("阶段3的绝对零度冷却（秒）（≤30%HP）")]
        [SerializeField] private float absoluteZeroCooldownPhase3 = 12f;

        [Tooltip("绝对零度视觉 GameObject（蓄力警告）。为空则跳过")]
        [SerializeField] private GameObject absoluteZeroWarningEffect;

        [Header("阶段二 · 极寒坦克召唤")]
        [Tooltip("阶段2每隔多少秒召唤一只极寒坦克")]
        [SerializeField] private float phase2TankSummonInterval = 30f;

        [Header("冰封射线触发概率")]
        [Tooltip("冷却就绪且选到主动技能时，触发冰封射线的概率（0~1）")]
        [SerializeField] private float freezeRayTriggerChance = 0.4f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        // 阶段
        private int currentPhase = 1;

        // 冰封射线冷却
        private float freezeRayCooldownTimer = 0f;
        private bool freezeRayReady = false;

        // 绝对零度冷却
        private float absoluteZeroCooldownTimer = 0f;
        private bool absoluteZeroTriggered = false;
        private bool absoluteZeroActive = false;

        // 阶段2额外FrostTank计时
        private float phase2TankTimer = 0f;

        // 玩家塔组件缓存
        private TurretController turretController;
        private TurretHealth turretHealth;

        // 射线检测层
        private LayerMask rayHitLayers;

        // 冰封射线已冻结标志（单次触发）
        private bool freezeRayHitThisCast = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BaseBossController 钩子实现
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void OnBossInitialized()
        {
            currentPhase = 1;
            absoluteZeroActive = false;
            absoluteZeroTriggered = false;

            // 冰封射线：初始无冷却（立即可用，鼓励早期施压）
            freezeRayCooldownTimer = 0f;
            freezeRayReady = true;

            // 绝对零度：初始冷却为阶段1时长
            absoluteZeroCooldownTimer = absoluteZeroCooldownPhase12;
            absoluteZeroTriggered = false;

            phase2TankTimer = phase2TankSummonInterval;

            // 缓存玩家组件
            FindTurretComponents();

            // 射线检测层
            rayHitLayers = LayerMask.GetMask("Shield", "Tower");

            // 冰封射线视觉初始隐藏
            if (freezeRayVisual != null)
                freezeRayVisual.gameObject.SetActive(false);

            if (absoluteZeroWarningEffect != null)
                absoluteZeroWarningEffect.SetActive(false);
        }

        // ── 阶段检测 ────────────────────────────────────────

        protected override void OnExtraUpdate()
        {
            UpdatePhase();
            UpdateFreezeRayCooldown();
            UpdateAbsoluteZeroCooldown();

            // 阶段2：额外 FrostTank 召唤
            if (currentPhase == 2)
                UpdatePhase2TankSummon();
        }

        private void UpdatePhase()
        {
            float hp = HealthPercent;

            if (currentPhase == 1 && hp <= 0.65f)
            {
                currentPhase = 2;
                // 进入阶段2重置阶段2坦克计时
                phase2TankTimer = phase2TankSummonInterval;
                if (showDebugInfo)
                    GameLogger.Log("[GlacialBoss] 进入阶段二：极寒爆发！");
            }
            else if (currentPhase < 3 && hp <= 0.3f)
            {
                currentPhase = 3;
                // 阶段3：绝对零度冷却缩短
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
                // 在屏幕随机边缘召唤一只极寒坦克
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
            // 绝对零度已在 OnExtraUpdate 触发，此处直接用 Charge 兜底
            // 冰封射线：冷却就绪时按概率触发
            if (freezeRayReady && Random.value < freezeRayTriggerChance)
            {
                StartFreezeRay();
                return;
            }

            // 兜底：极寒冲撞
            ChangeState(BossState.Charge);
        }

        // ── 技能1：冰墙构建 ─────────────────────────────────

        protected override IEnumerator ExecuteSummonBehavior()
        {
            if (EnemyPoolManager.Instance == null)
            {
                if (showDebugInfo) GameLogger.LogWarning("[GlacialBoss] EnemyPoolManager 不存在！");
                yield break;
            }

            int count = Random.Range(iceWallCountMin, iceWallCountMax + 1);

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomIceWallPosition();
                EnemyPoolManager.Instance.Spawn(EnemyType.IceWall, pos);
                // 多堵冰墙略微错开
                if (count > 1) yield return new WaitForSeconds(0.2f);
            }

            if (showDebugInfo)
                GameLogger.Log($"[GlacialBoss] 冰墙构建：召唤 {count} 堵冰墙");
        }

        // ── 技能2：冰封射线 ─────────────────────────────────

        private void StartFreezeRay()
        {
            freezeRayReady = false;
            freezeRayCooldownTimer = freezeRayCooldownTime;

            // 停止 Boss 移动：进入 Stun（杀掉旧 stateCoroutine），立即覆盖为射线协程
            ChangeState(BossState.Stun);
            if (stateCoroutine != null) StopCoroutine(stateCoroutine);
            stateCoroutine = StartCoroutine(FreezeRayRoutine());
        }

        private IEnumerator FreezeRayRoutine()
        {
            if (showDebugInfo) GameLogger.Log("[GlacialBoss] 冰封射线开始！");

            freezeRayHitThisCast = false;

            // 初始射线角度：朝向玩家塔
            float startAngle = GetAngleToTurret();
            float currentAngle = startAngle;
            float totalRotation = freezeRayRotationSpeed * freezeRayDuration; // 总旋转角度

            // 启用视觉
            if (freezeRayVisual != null)
                freezeRayVisual.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < freezeRayDuration)
            {
                elapsed += Time.deltaTime;
                currentAngle = startAngle + elapsed * freezeRayRotationSpeed;

                // 更新视觉旋转
                if (freezeRayVisual != null)
                    freezeRayVisual.rotation = Quaternion.Euler(0f, 0f, currentAngle);

                // 射线检测（朝当前角度方向）
                if (!freezeRayHitThisCast)
                {
                    Vector2 rayDir = Quaternion.Euler(0f, 0f, currentAngle) * Vector2.down;
                    var hit = Physics2D.Raycast(transform.position, rayDir, freezeRayLength, rayHitLayers);
                    if (hit.collider != null)
                    {
                        freezeRayHitThisCast = true;
                        ApplyTurretFreeze(freezeRayTurretFreezeDuration);
                        if (showDebugInfo)
                            GameLogger.Log($"[GlacialBoss] 冰封射线命中！冻结 {freezeRayTurretFreezeDuration}s");
                    }
                }

                // Boss 在射线期间保持静止
                if (rb != null) rb.velocity = Vector2.zero;

                yield return null;
            }

            // 关闭视觉
            if (freezeRayVisual != null)
                freezeRayVisual.gameObject.SetActive(false);

            if (showDebugInfo) GameLogger.Log("[GlacialBoss] 冰封射线结束");
            ChangeState(BossState.Idle);
        }

        // ── 技能3：极寒冲撞 ─────────────────────────────────

        protected override void OnChargeDashComplete(Vector3 startPos, Vector3 endPos)
        {
            // 冲撞落地后冻结光棱塔
            ApplyTurretFreeze(chargeTurretFreezeDuration);
            if (showDebugInfo)
                GameLogger.Log($"[GlacialBoss] 极寒冲撞落地，冻结光棱塔 {chargeTurretFreezeDuration}s");
        }

        protected override float GetChargeSpeedMultiplier()
        {
            return currentPhase >= 3 ? 1.15f : 1f;
        }

        // ── 技能4：绝对零度 ─────────────────────────────────

        private void TriggerAbsoluteZero()
        {
            if (absoluteZeroActive) return;
            absoluteZeroActive = true;

            if (showDebugInfo) GameLogger.Log("[GlacialBoss] 绝对零度启动！");

            // 中断当前冲撞（如果有）
            InterruptCharge();

            // 进入 Stun 停止Boss所有运动，立即覆盖为绝对零度协程
            ChangeState(BossState.Stun);
            if (stateCoroutine != null) StopCoroutine(stateCoroutine);
            stateCoroutine = StartCoroutine(AbsoluteZeroRoutine());
        }

        private IEnumerator AbsoluteZeroRoutine()
        {
            if (showDebugInfo) GameLogger.Log("[GlacialBoss] 绝对零度：3秒蓄力开始");

            // 全屏警告视觉
            if (absoluteZeroWarningEffect != null)
                absoluteZeroWarningEffect.SetActive(true);

            // 全屏警告文字（UI 层可监听 FloatingTextManager 实现更丰富的表现）
            LightVsDecay.UI.FloatingText.FloatingTextManager.Instance?.ShowStatus(
                transform.position, "绝对零度！打断需造成 5000 伤害！");

            // 记录蓄力开始时的 Boss HP 百分比（用于计算被打断时受到的伤害量）
            float hpPercentAtStart = bossHealth.HealthPercent;
            float maxHP = config != null ? config.maxHealth : 750000f;

            float elapsed = 0f;
            bool interrupted = false;

            while (elapsed < absoluteZeroChannelDuration)
            {
                elapsed += Time.deltaTime;
                rb.velocity = Vector2.zero;

                // 检测打断：Boss 在蓄力期间受到的累计伤害
                float damageDealt = (hpPercentAtStart - bossHealth.HealthPercent) * maxHP;
                if (damageDealt >= absoluteZeroInterruptThreshold)
                {
                    interrupted = true;
                    break;
                }

                yield return null;
            }

            // 关闭视觉
            if (absoluteZeroWarningEffect != null)
                absoluteZeroWarningEffect.SetActive(false);

            if (interrupted)
            {
                // 打断成功：Boss 眩晕
                if (showDebugInfo) GameLogger.Log("[GlacialBoss] 绝对零度被打断！Boss 眩晕");
                FloatingTextShowInterrupted();
                absoluteZeroActive = false;
                // 重置冷却
                absoluteZeroCooldownTimer = currentPhase >= 3
                    ? absoluteZeroCooldownPhase3
                    : absoluteZeroCooldownPhase12;
                absoluteZeroTriggered = false;

                ForceStun(); // 打断后短暂眩晕，然后回Idle
            }
            else
            {
                // 打断失败：施放完整绝对零度
                if (showDebugInfo) GameLogger.Log("[GlacialBoss] 绝对零度释放！");
                yield return StartCoroutine(AbsoluteZeroDetonateRoutine());

                absoluteZeroActive = false;
                absoluteZeroCooldownTimer = currentPhase >= 3
                    ? absoluteZeroCooldownPhase3
                    : absoluteZeroCooldownPhase12;
                absoluteZeroTriggered = false;
                ChangeState(BossState.Idle);
            }
        }

        private IEnumerator AbsoluteZeroDetonateRoutine()
        {
            // 真实伤害：光棱塔当前血量的 30%
            if (turretHealth != null)
            {
                // 通过 TurretHealth.TakeDamage 施加百分比真实伤害
                // 先尝试对护盾，再对本体
                int trueDamage = Mathf.RoundToInt(turretHealth.CurrentHullHP * absoluteZeroTrueDamagePercent);
                turretHealth.TakeDamage(trueDamage);
                if (showDebugInfo)
                    GameLogger.Log($"[GlacialBoss] 绝对零度真实伤害 {trueDamage}");
            }

            // 冻结光棱塔 3 秒
            ApplyTurretFreeze(absoluteZeroTurretFreezeDuration);

            // 短暂震屏强调
            CameraShake.Instance?.Shake(0.8f, 0.5f);

            // 召唤额外怪物波次（FrostSlime）
            if (EnemyPoolManager.Instance != null)
            {
                for (int i = 0; i < absoluteZeroSummonCount; i++)
                {
                    Vector3 pos = GetRandomSpawnEdgePosition();
                    EnemyPoolManager.Instance.Spawn(EnemyType.FrostSlime, pos);
                }
            }

            yield return new WaitForSeconds(0.5f);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 辅助方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

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

        /// <summary>获取 Boss 朝向玩家塔的角度（用于射线初始方向）</summary>
        private float GetAngleToTurret()
        {
            if (turretController == null) return -90f; // 默认朝下
            Vector2 dir = (turretController.transform.position - transform.position).normalized;
            return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; // Z轴旋转（0°=朝上）
        }

        /// <summary>在屏幕可见区域内随机取一个冰墙生成位置</summary>
        private Vector3 GetRandomIceWallPosition()
        {
            Camera cam = Camera.main;
            if (cam == null) return transform.position + Vector3.right * Random.Range(-3f, 3f);

            float halfW = cam.orthographicSize * cam.aspect;
            float halfH = cam.orthographicSize;
            float margin = 1.5f;

            float x = Random.Range(cam.transform.position.x - halfW + margin,
                                   cam.transform.position.x + halfW - margin);
            // 只在上半屏生成冰墙（避免紧贴塔）
            float y = Random.Range(cam.transform.position.y,
                                   cam.transform.position.y + halfH - margin);
            return new Vector3(x, y, 0f);
        }

        /// <summary>获取屏幕边缘随机位置（用于 Phase2 FrostTank 和绝对零度增援）</summary>
        private Vector3 GetRandomSpawnEdgePosition()
        {
            Camera cam = Camera.main;
            if (cam == null) return transform.position + Vector3.up * 8f;

            float halfW = cam.orthographicSize * cam.aspect;
            float halfH = cam.orthographicSize;
            float edge = 1.5f;

            // 从上方入场
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
}
