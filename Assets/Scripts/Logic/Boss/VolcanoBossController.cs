// ============================================================
// VolcanoBossController.cs
// 文件位置: Assets/Scripts/Logic/Boss/VolcanoBossController.cs
// 用途：第二章 Boss ——《熔炉巨兽 (The Magma Furnace)》
// 继承 BaseBossController，实现以下专属机制：
//
//   阶段一（HP>70%）：陨石喷发（被动）+ 汲取融合（召唤）+ 熔岩冲撞（Charge）
//   阶段二（30%<HP≤70%）：同上，汲取融合升级为8只（含2只爆炸者）
//   阶段三（HP≤30%）：触发绝境碾压（Press），难度逐轮递增
//
//   汲取融合：召唤 LavaSplitter，向 Boss 汇聚，被吸收后
//             Boss 回血 2000，获得永久 5% 攻击力（最多6层）
//   陨石喷发：Idle 被动，每隔 meteorInterval 秒砸 3 颗陨石
//   熔岩冲撞：Base Charge（无敌冲撞）
//   绝境碾压：Base Press（激光角力），三轮递增难度
// ============================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;

namespace LightVsDecay.Logic.Boss
{
    /// <summary>
    /// 第二章Boss：熔炉巨兽。
    /// 专属机制：汲取融合（吸收小怪回血强化）、陨石喷发（地形压制）、
    ///           熔岩冲撞（Charge带燃烧轨迹）、绝境碾压（30%HP阈值三轮角力）。
    /// </summary>
    public class VolcanoBossController : BaseBossController
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置（Ch2 专属）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("汲取融合 · 配置")]
        [Tooltip("阶段一召唤数量（全部为自爆怪）")]
        [SerializeField] private int summonCountPhase1 = 6;

        [Tooltip("阶段二召唤数量（全部为自爆怪，数量增加）")]
        [SerializeField] private int summonCountPhase2 = 8;

        [Tooltip("吸收半径（小怪进入此范围被吸收）")]
        [SerializeField] private float absorptionRadius = 1.2f;

        [Tooltip("每次吸收回复的 HP")]
        [SerializeField] private float absorptionHealPerSlime = 2000f;

        [Tooltip("每次吸收增加的攻击力（叠乘，每层 10%）")]
        [SerializeField] private float absorptionATKPerStack = 0.10f;

        [Tooltip("吸收层数上限")]
        [SerializeField] private int absorptionMaxStacks = 6;

        [Header("陨石喷发 · 配置")]
        [Tooltip("陨石预制体（需挂 VolcanoMeteor 组件，Layer = BossPollutionBall）")]
        [SerializeField] private GameObject meteorPrefab;

        [Tooltip("每次喷发陨石数量")]
        [SerializeField] private int meteorCount = 3;

        [Tooltip("被动喷发间隔（秒）")]
        [SerializeField] private float meteorInterval = 12f;

        [Tooltip("陨石落点散布半径（以玩家塔为中心）")]
        [SerializeField] private float meteorSpreadRadius = 3f;

        [Header("火山冲撞 · 岩浆隔离带")]
        [Tooltip("冲撞路径岩浆水坑间距（越小越密集，建议 1.5）")]
        [SerializeField] private float chargeTrailSpacing = 1.5f;

        [Header("绝境碾压 · 配置")]
        [Tooltip("触发阈值（HP 百分比）")]
        [SerializeField] private float desperatePressThreshold = 0.3f;

        [Tooltip("绝境碾压次数（对应激光角力轮次）")]
        [SerializeField] private int desperatePressRounds = 3;

        [Tooltip("角力期间每次喷射火球的间隔（秒）")]
        [SerializeField] private float pressFireballInterval = 2f;

        [Tooltip("角力火球预制体（需挂 LavaProjectile，Layer = BossPollutionBall）")]
        [SerializeField] private GameObject pressFireballPrefab;

        [Tooltip("角力火球单发伤害")]
        [SerializeField] private int pressFireballDamage = 25;

        [Tooltip("角力火球飞行速度")]
        [SerializeField] private float pressFireballSpeed = 6f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        // 阶段
        private int currentPhase = 1;
        private bool phase3Triggered = false;

        // 陨石被动计时
        private float meteorTimer = 0f;

        // 汲取融合
        private List<LightVsDecay.Logic.Enemy.EnemyBlob> summonedSlimes =
            new List<LightVsDecay.Logic.Enemy.EnemyBlob>();
        private int absorptionStacks = 0;

        // 绝境碾压轮次
        private int desperatePressRoundsDone = 0;

        // 绝境碾压火球计时
        private float pressFireballTimer = 0f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BaseBossController 钩子实现
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void OnBossInitialized()
        {
            currentPhase = 1;
            phase3Triggered = false;
            absorptionStacks = 0;
            desperatePressRoundsDone = 0;
            meteorTimer = 0f;
            pressFireballTimer = 0f;
            summonedSlimes.Clear();
        }

        // ── 阶段检测（每帧在 OnExtraUpdate 中刷新）──────────────

        protected override void OnExtraUpdate()
        {
            UpdatePhase();
            CheckAbsorption();
        }

        private void UpdatePhase()
        {
            float hp = HealthPercent;

            // 阶段三触发（仅触发一次，进入后不退出）
            if (!phase3Triggered && hp <= desperatePressThreshold)
            {
                phase3Triggered = true;
                currentPhase = 3;
                if (showDebugInfo)
                    GameLogger.Log("[VolcanoBoss] 进入阶段三：绝境碾压！");
                // 中断当前行为，强制进入 Press
                InterruptCharge();
                ForceStun(); // 短暂硬直后自动进入 Idle → ChooseActiveSkill → Press
            }
            else if (currentPhase < 2 && hp <= 0.7f)
            {
                currentPhase = 2;
                if (showDebugInfo)
                    GameLogger.Log("[VolcanoBoss] 进入阶段二：熔炉爆发！");
            }
        }

        // ── 被动：陨石喷发 ───────────────────────────────────────

        protected override void OnEnterIdle()
        {
            meteorTimer = 0f;
        }

        protected override void OnIdlePassiveUpdate(float deltaTime)
        {
            meteorTimer += deltaTime;
            if (meteorTimer >= meteorInterval)
            {
                meteorTimer = 0f;
                StartCoroutine(MeteorBurstRoutine());
            }
        }

        private IEnumerator MeteorBurstRoutine()
        {
            if (meteorPrefab == null)
            {
                if (showDebugInfo) GameLogger.LogWarning("[VolcanoBoss] meteorPrefab 未设置！");
                yield break;
            }

            // 查找玩家塔位置
            GameObject towerGO = GameObject.FindGameObjectWithTag("Tower");
            Vector3 towerPos = towerGO != null ? towerGO.transform.position : Vector3.zero;

            for (int i = 0; i < meteorCount; i++)
            {
                // 在塔周围随机落点（但偏移量确保有意义的散布）
                float angle = (360f / meteorCount) * i + Random.Range(-30f, 30f);
                float dist  = Random.Range(0.5f, meteorSpreadRadius);
                Vector2 offset = Quaternion.Euler(0, 0, angle) * Vector2.up * dist;
                Vector3 targetPos = towerPos + (Vector3)offset;

                GameObject go = Instantiate(meteorPrefab, targetPos + Vector3.up * 12f, Quaternion.identity);
                var meteor = go.GetComponent<VolcanoMeteor>();
                if (meteor != null)
                    meteor.Launch(targetPos);

                yield return new WaitForSeconds(0.4f); // 陨石之间错开 0.4s
            }

            if (showDebugInfo)
                GameLogger.Log($"[VolcanoBoss] 陨石喷发：{meteorCount} 颗");
        }

        // ── 主动技能选择 ─────────────────────────────────────────

        protected override void ChooseActiveSkill()
        {
            // 阶段三：全部走 Press（绝境碾压）
            if (phase3Triggered)
            {
                if (desperatePressRoundsDone < desperatePressRounds)
                {
                    ChangeState(BossState.Press);
                    desperatePressRoundsDone++;
                }
                else
                {
                    // 全部轮次完成，回到普通行为
                    phase3Triggered = false;
                    ChangeState(BossState.Idle);
                }
                return;
            }

            // 阶段一/二：随机 Charge / Summon（不用 Press）
            float roll = Random.value;
            if (roll < 0.5f)
                ChangeState(BossState.Charge);
            else
                ChangeState(BossState.Summon);
        }

        // ── 汲取融合：召唤行为 ───────────────────────────────────

        protected override IEnumerator ExecuteSummonBehavior()
        {
            summonedSlimes.RemoveAll(e => e == null || e.IsDead);

            // 汲取融合全部召唤自爆岩浆怪（LavaExploder）
            int totalCount = (currentPhase >= 2) ? summonCountPhase2 : summonCountPhase1;

            if (EnemyPoolManager.Instance == null)
            {
                if (showDebugInfo) GameLogger.LogWarning("[VolcanoBoss] EnemyPoolManager 不存在！");
                yield break;
            }

            // 围绕 Boss 生成，均匀角度分布
            float angleStep = totalCount > 0 ? 360f / totalCount : 0f;
            float spawnRadius = 4f;

            for (int i = 0; i < totalCount; i++)
            {
                float angle = angleStep * i;
                Vector2 offset = Quaternion.Euler(0, 0, angle) * Vector2.up * spawnRadius;
                var enemy = EnemyPoolManager.Instance.Spawn(
                    EnemyType.LavaExploder, transform.position + (Vector3)offset);
                if (enemy != null)
                    summonedSlimes.Add(enemy);
            }

            if (showDebugInfo)
                GameLogger.Log($"[VolcanoBoss] 汲取融合：召唤 {totalCount} 只自爆岩浆怪");

            yield break;
        }

        // ── 汲取融合：吸收检测（每帧）───────────────────────────

        private void CheckAbsorption()
        {
            if (summonedSlimes.Count == 0) return;

            for (int i = summonedSlimes.Count - 1; i >= 0; i--)
            {
                var slime = summonedSlimes[i];
                if (slime == null || slime.IsDead)
                {
                    summonedSlimes.RemoveAt(i);
                    continue;
                }

                float dist = Vector2.Distance(transform.position, slime.transform.position);
                if (dist <= absorptionRadius)
                {
                    AbsorbSlime(slime);
                    summonedSlimes.RemoveAt(i);
                }
            }
        }

        private void AbsorbSlime(LightVsDecay.Logic.Enemy.EnemyBlob slime)
        {
            // 静默吸收（无经验/金币）
            slime.AbsorbedByBoss();

            // 回血
            if (bossHealth != null)
                bossHealth.HealHP(absorptionHealPerSlime);

            // 永久攻击力叠层（上限6层）
            if (absorptionStacks < absorptionMaxStacks)
            {
                absorptionStacks++;
            }

            if (showDebugInfo)
                GameLogger.Log($"[VolcanoBoss] 吸收一只！回血 {absorptionHealPerSlime}，攻击层数: {absorptionStacks}/{absorptionMaxStacks}");
        }

        // ── 连体Buff：汲取融合攻击加成转为防御减免（对称设计）──

        public override float GetLinkedBuffDamageMultiplier()
        {
            // 吸收层数越高，Boss 受到的伤害越少（每层减 3%，上限 -18%）
            float reductionPerStack = 0.03f;
            return Mathf.Max(0.1f, 1f - absorptionStacks * reductionPerStack);
        }

        // ── 火山冲撞：冲撞路径生成岩浆隔离带 ───────────────────

        protected override void OnChargeDashComplete(Vector3 startPos, Vector3 endPos)
        {
            if (EnemyPoolManager.Instance == null) return;

            float pathLength = Vector3.Distance(startPos, endPos);
            int puddleCount = Mathf.Max(1, Mathf.RoundToInt(pathLength / chargeTrailSpacing));

            for (int i = 0; i <= puddleCount; i++)
            {
                float t = puddleCount > 0 ? (float)i / puddleCount : 0f;
                Vector3 pos = Vector3.Lerp(startPos, endPos, t);
                EnemyPoolManager.Instance.Spawn(EnemyType.LavaPuddle, pos);
            }

            if (showDebugInfo)
                GameLogger.Log($"[VolcanoBoss] 火山冲撞：路径生成 {puddleCount + 1} 个岩浆水坑");
        }

        // ── 熔岩冲撞：冲撞速度随阶段提升 ───────────────────────

        protected override float GetChargeSpeedMultiplier()
        {
            return currentPhase >= 2 ? 1.2f : 1f;
        }

        // ── 绝境碾压：角力期间每2秒向两侧喷射2发火球 ──────────

        protected override void OnPressTick(float deltaTime)
        {
            pressFireballTimer += deltaTime;
            if (pressFireballTimer < pressFireballInterval) return;
            pressFireballTimer = 0f;

            if (pressFireballPrefab == null)
            {
                if (showDebugInfo) GameLogger.LogWarning("[VolcanoBoss] pressFireballPrefab 未设置！");
                return;
            }

            FirePressBall(Vector2.left);
            FirePressBall(Vector2.right);

            if (showDebugInfo)
                GameLogger.Log("[VolcanoBoss] 绝境碾压：喷射2发裂缝火球");
        }

        private void FirePressBall(Vector2 direction)
        {
            GameObject go = Instantiate(pressFireballPrefab, transform.position, Quaternion.identity);
            var proj = go.GetComponent<LightVsDecay.Logic.Enemy.LavaProjectile>();
            if (proj != null)
                proj.Initialize(direction, pressFireballSpeed, pressFireballDamage,
                                hp: 999f, lifetime: 5f); // 火球不被击落，5秒后消失
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试 GUI
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

#if UNITY_EDITOR
        protected override void OnGUI()
        {
            base.OnGUI();
            if (!showDebugInfo) return;
            GUILayout.BeginArea(new Rect(10, 460, 300, 100));
            GUILayout.Label("=== Ch2 熔炉巨兽 ===");
            GUILayout.Label($"阶段: {currentPhase}  |  汲取层数: {absorptionStacks}/{absorptionMaxStacks}");
            GUILayout.Label($"待吸收小怪: {summonedSlimes.Count}  |  绝境碾压轮次: {desperatePressRoundsDone}/{desperatePressRounds}");
            GUILayout.Label($"陨石计时: {meteorTimer:F1}s / {meteorInterval}s");
            GUILayout.EndArea();
        }
#endif
    }
}
