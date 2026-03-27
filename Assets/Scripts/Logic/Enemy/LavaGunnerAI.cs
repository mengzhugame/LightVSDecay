// ============================================================
// LavaGunnerAI.cs
// 文件位置: Assets/Scripts/Logic/Enemy/LavaGunnerAI.cs
// 用途：熔岩炮手 AI 状态机
// 行为流程：
//   Entering → 从屏幕顶部入场，到达目标 Y 后停驻
//   Idle     → 等待 shootInterval 秒
//   Shooting → 朝玩家塔发射熔岩弹，0.3s 后转换
//   Repositioning → 横向移动至新位置，到达后回 Idle
// ============================================================

using System.Collections;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Data.SO;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// 熔岩炮手 AI 控制器。
    /// 挂载在与 EnemyBlob 相同的 GameObject 上，
    /// EnemyBlob.MoveTowardsTower 在 RangedGunner 模式下不执行，
    /// 此组件全权负责 Rigidbody2D 速度控制。
    /// </summary>
    [RequireComponent(typeof(EnemyBlob))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class LavaGunnerAI : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态枚举
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private enum GunnerState { Entering, Idle, Shooting, Repositioning }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private EnemyBlob blob;
        private Rigidbody2D rb;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置（从 EnemyData 读取）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private float stopYPercent = 0.6f;          // 0=顶部 1=底部，停驻位置（从顶部算）
        private float shootInterval = 8f;
        private float repositionRange = 2f;
        private GameObject projectilePrefab;
        private float projectileSpeed = 5f;
        private int projectileDamage = 40;
        private float projectileHP = 20f;
        private float projectileLifetime = 8f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private GunnerState state = GunnerState.Entering;
        private float targetWorldY;
        private float targetWorldX;
        private float idleTimer;
        private bool isActive = false;

        // 速度常量
        private const float ENTRY_SPEED = 3f;
        private const float REPOSITION_SPEED = 2f;
        private const float SNAP_DIST = 0.08f;
        private const float SHOOT_PAUSE = 0.35f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            blob = GetComponent<EnemyBlob>();
            rb   = GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate()
        {
            if (!isActive || blob == null || blob.IsDead) return;

            switch (state)
            {
                case GunnerState.Entering:
                    UpdateEntering();
                    break;
                case GunnerState.Idle:
                    UpdateIdle();
                    break;
                case GunnerState.Repositioning:
                    UpdateRepositioning();
                    break;
                // Shooting 由协程全权处理，FixedUpdate 期间速度为零
                case GunnerState.Shooting:
                    rb.velocity = Vector2.zero;
                    break;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 对象池事件（由 EnemyBlob 调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// EnemyBlob.OnSpawn() 时调用，重新读取配置并开始入场。
        /// </summary>
        public void OnBlobSpawned()
        {
            StopAllCoroutines();

            var data = blob.Data;
            if (data == null) return;

            // 读取配置
            stopYPercent      = data.gunnerStopYPercent;
            shootInterval     = data.gunnerShootInterval;
            repositionRange   = data.gunnerRepositionRange;
            projectilePrefab  = data.gunnerProjectilePrefab;
            projectileSpeed   = data.gunnerProjectileSpeed;
            projectileDamage  = data.gunnerProjectileDamage;
            projectileHP      = data.gunnerProjectileHP;
            projectileLifetime = data.gunnerProjectileLifetime;

            // 计算停驻 Y（世界坐标）
            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                float worldTop    = cam.transform.position.y + cam.orthographicSize;
                float worldBottom = cam.transform.position.y - cam.orthographicSize;
                targetWorldY = Mathf.Lerp(worldTop, worldBottom, stopYPercent);
            }
            else
            {
                targetWorldY = 3f; // fallback
            }

            // 初始 X 位置
            targetWorldX = transform.position.x;
            idleTimer    = shootInterval;
            state        = GunnerState.Entering;
            isActive     = true;
        }

        /// <summary>
        /// EnemyBlob.OnReturnToPool() 时调用，停止所有协程。
        /// </summary>
        public void OnBlobDeactivated()
        {
            isActive = false;
            StopAllCoroutines();
            if (rb != null) rb.velocity = Vector2.zero;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 入场
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void UpdateEntering()
        {
            float dy = transform.position.y - targetWorldY;
            if (dy > SNAP_DIST)
            {
                rb.velocity = Vector2.down * ENTRY_SPEED;
            }
            else
            {
                rb.velocity = Vector2.zero;
                Vector3 pos = transform.position;
                pos.y = targetWorldY;
                transform.position = pos;
                targetWorldX = pos.x;
                EnterIdle();
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 待机
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void EnterIdle()
        {
            state     = GunnerState.Idle;
            idleTimer = shootInterval;
            rb.velocity = Vector2.zero;
        }

        private void UpdateIdle()
        {
            rb.velocity = Vector2.zero;
            idleTimer -= Time.fixedDeltaTime;
            if (idleTimer <= 0f)
            {
                StartCoroutine(ShootRoutine());
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 射击
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator ShootRoutine()
        {
            state = GunnerState.Shooting;
            rb.velocity = Vector2.zero;

            FireProjectile();

            yield return new WaitForSeconds(SHOOT_PAUSE);

            // 射击后随机换位
            ChooseNewTargetX();
            state = GunnerState.Repositioning;
        }

        private void FireProjectile()
        {
            if (projectilePrefab == null)
            {
                GameLogger.LogWarning("[LavaGunnerAI] 弹道预制体未设置！");
                return;
            }

            // 目标：玩家塔
            Transform target = blob.TargetTower;
            if (target == null) return;

            Vector2 dir = (target.position - transform.position).normalized;

            GameObject go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
            var proj = go.GetComponent<LavaProjectile>();
            if (proj != null)
            {
                proj.Initialize(dir, projectileSpeed, projectileDamage, projectileHP, projectileLifetime);
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 换位
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void ChooseNewTargetX()
        {
            Camera cam = Camera.main;
            float halfWidth = cam != null ? cam.orthographicSize * cam.aspect : 8f;
            float margin = 1.5f;
            float minX   = -halfWidth + margin;
            float maxX   =  halfWidth - margin;

            float candidateMin = Mathf.Max(minX, transform.position.x - repositionRange);
            float candidateMax = Mathf.Min(maxX, transform.position.x + repositionRange);

            targetWorldX = (candidateMin < candidateMax)
                ? Random.Range(candidateMin, candidateMax)
                : transform.position.x;
        }

        private void UpdateRepositioning()
        {
            float dx = targetWorldX - transform.position.x;
            if (Mathf.Abs(dx) > SNAP_DIST)
            {
                rb.velocity = new Vector2(Mathf.Sign(dx) * REPOSITION_SPEED, 0f);
            }
            else
            {
                rb.velocity = Vector2.zero;
                Vector3 pos = transform.position;
                pos.x = targetWorldX;
                transform.position = pos;
                EnterIdle();
            }
        }
    }
}
