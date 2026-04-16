// ============================================================
// BossController.cs
// 文件位置: Assets/Scripts/Logic/Boss/BossController.cs
// 用途：第一章 Boss — 污染之核 (The Corruptor)
// 继承 BaseBossController，仅保留 Ch1 专属逻辑：
//   - 污秽球被动系统
//   - Rusher 召唤
//   - 连体Buff（Rusher数量加速/减伤）
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
    /// 第一章Boss：污染之核。
    /// 专属机制：污秽球被动、Rusher召唤、连体Buff。
    /// </summary>
    public class DarkBossController : BaseBossController
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 死亡演出
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 死亡演出 ═══")]
        [Tooltip("Boss 死亡时播放的专属爆炸特效预制体")]
        [SerializeField] private GameObject deathVFXPrefab;

        [Header("═══ 入场演出 ═══")]
        [Tooltip("Boss 落地咆哮时播放的黑线冲击特效预制体")]
        [SerializeField] private GameObject spawnVFXPrefab;

        public override GameObject DeathVFXPrefab => deathVFXPrefab;
        public override GameObject SpawnVFXPrefab => spawnVFXPrefab;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Ch1 专属：污秽球系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private List<BossPollutionProjectile> activePollutionBalls =
            new List<BossPollutionProjectile>();
        private float pollutionTimer = 0f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Ch1 专属：连体Buff（基于场上 Rusher 数量）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public int GetLinkedBuffStacks()
        {
            if (EnemyPoolManager.Instance == null) return 0;
            int rusherCount = EnemyPoolManager.Instance.GetActiveCount(EnemyType.Rusher);
            int maxStacks = config != null ? config.linkedBuffMaxStacks : 5;
            return Mathf.Min(rusherCount, maxStacks);
        }

        protected override float GetChargeSpeedMultiplier()
        {
            int stacks = GetLinkedBuffStacks();
            float bonusPerStack = config != null ? config.linkedBuffChargeSpeedPerStack : 0.1f;
            return 1f + stacks * bonusPerStack;
        }

        public override float GetLinkedBuffDamageMultiplier()
        {
            int stacks = GetLinkedBuffStacks();
            float reductionPerStack = config != null ? config.linkedBuffDamageReductionPerStack : 0.1f;
            return 1f - stacks * reductionPerStack;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 抽象方法实现：召唤 Rusher
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override IEnumerator ExecuteSummonBehavior()
        {
            SpawnRushers();
            yield break;
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
                SpawnRusherAt(new Vector3(-halfWidth - 1f - i * 0.5f, spawnY + i * 0.3f, 0f), speedBonus);
                SpawnRusherAt(new Vector3(halfWidth + 1f + i * 0.5f, spawnY - i * 0.3f, 0f), speedBonus);
            }

            if (showDebugInfo)
                GameLogger.Log($"[BossController] 生成 {perSide * 2} 只 Rusher (狂暴: {IsEnraged})");
        }

        private void SpawnRusherAt(Vector3 pos, float speedBonus)
        {
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

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 虚方法重写：Idle 被动（污秽球）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void OnEnterIdle()
        {
            pollutionTimer = 0f;
        }

        protected override void OnIdlePassiveUpdate(float deltaTime)
        {
            pollutionTimer += deltaTime;
            float interval = config != null ? config.pollutionInterval : 4f;
            if (pollutionTimer >= interval)
            {
                pollutionTimer = 0f;
                FirePollutionProjectile();
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 污秽球
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void FirePollutionProjectile()
        {
            GameObject prefab = config != null ? config.pollutionProjectilePrefab : null;
            if (prefab == null)
            {
                if (showDebugInfo) GameLogger.LogWarning("[BossController] Pollution Prefab 未设置！");
                return;
            }

            int maxCount = config != null ? config.pollutionMaxCount : 3;
            int burstCount = config != null ? config.GetPollutionBurstCount(HealthPercent) : 3;
            float spreadAngle = config != null ? config.pollutionSpreadAngle : 30f;

            activePollutionBalls.RemoveAll(b => b == null || b.IsDestroyed);
            int toSpawn = Mathf.Min(burstCount, maxCount - activePollutionBalls.Count);
            if (toSpawn <= 0)
            {
                if (showDebugInfo) GameLogger.Log("[BossController] 污秽球已达上限");
                return;
            }

            float startAngle = -spreadAngle * 0.5f;
            float angleStep = toSpawn > 1 ? spreadAngle / (toSpawn - 1) : 0f;

            for (int i = 0; i < toSpawn; i++)
            {
                float angle = startAngle + angleStep * i;
                Vector2 dir = Quaternion.Euler(0, 0, angle) * Vector2.down;

                GameObject go = Instantiate(prefab, transform.position, Quaternion.identity);
                var ball = go.GetComponent<BossPollutionProjectile>();
                if (ball != null && config != null)
                {
                    ball.InitializeV3(
                        config.pollutionSpeed,
                        config.pollutionTurnSpeed,
                        config.pollutionShieldDamage,
                        config.pollutionLifetime,
                        config.pollutionBallHP,
                        config.pollutionBallMass,
                        this);

                    float angleOffset = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
                    ball.SetInitialDirection(angleOffset);
                    RegisterPollutionBall(ball);
                }
            }

            if (showDebugInfo)
                GameLogger.Log($"[BossController] 喷射 {toSpawn} 个污秽球");
        }

        public void RegisterPollutionBall(BossPollutionProjectile ball)
        {
            if (ball != null && !activePollutionBalls.Contains(ball))
                activePollutionBalls.Add(ball);
        }

        public void UnregisterPollutionBall(BossPollutionProjectile ball)
        {
            activePollutionBalls.Remove(ball);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试（追加 Ch1 专属信息）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

#if UNITY_EDITOR
        protected override void OnGUI()
        {
            base.OnGUI();
            if (!showDebugInfo) return;
            GUILayout.BeginArea(new Rect(10, 460, 280, 80));
            GUILayout.Label("=== Ch1 污染之核 ===");
            GUILayout.Label($"连体Buff: {GetLinkedBuffStacks()} 层 → 减伤 {GetLinkedBuffDamageMultiplier():P0}");
            GUILayout.Label($"污秽球: {activePollutionBalls.Count} 活跃 | Timer: {pollutionTimer:F1}s");
            GUILayout.EndArea();
        }
#endif
    }
}
