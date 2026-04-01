// ============================================================
// LavaGunnerAI.cs  (v2.0)
// 文件位置: Assets/Scripts/Logic/Enemy/LavaGunnerAI.cs
// 行为流程：
//   Entering      → 从屏幕顶部入场，到达停驻 Y
//   Charging      → 蓄力阶段：Effect 图层从暗渐亮，计时结束触发发射
//   Shooting      → Y 轴弹压（压缩→弹起）→ 嘴部发射火球 → 换位
//   Repositioning → 横向移动到新 X，到达后回 Charging
// ============================================================

using System.Collections;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Statistics;

namespace LightVsDecay.Logic.Enemy
{
    [RequireComponent(typeof(EnemyBlob))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class LavaGunnerAI : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态枚举
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private enum GunnerState { Entering, Charging, Shooting, Repositioning }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("炮口位置")]
        [Tooltip("嘴巴处的子节点 Transform；未设置时自动查找名为 FirePoint 的子节点")]
        [SerializeField] private Transform firePoint;

        [Header("蓄力特效")]
        [Tooltip("蓄力期间发光的 Effect SpriteRenderer")]
        [SerializeField] private SpriteRenderer effectSprite;

        [Tooltip("蓄力开始时 Effect 的暗淡颜色")]
        [SerializeField] private Color effectDimColor  = new Color(1f, 0.35f, 0f, 0.15f);

        [Tooltip("蓄力结束时 Effect 的最亮颜色")]
        [SerializeField] private Color effectGlowColor = new Color(1f, 0.85f, 0.1f, 1f);

        [Header("入场首发延迟")]
        [Tooltip("入场停稳后多久开始第一次蓄力（秒）；之后每次使用 gunnerShootInterval")]
        [SerializeField] private float firstShotDelay = 1.2f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 弹压动画常量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        // 压缩阶段：X 变宽 + Y 变矮（蓄力压缩感）
        private const float COMPRESS_X = 1.28f;
        private const float COMPRESS_Y = 0.60f;
        private const float COMPRESS_DURATION = 0.14f;

        // 弹起阶段：X 变窄 + Y 变高（出球瞬间拉伸感）
        private const float BOUNCE_X = 0.80f;
        private const float BOUNCE_Y = 1.30f;
        private const float BOUNCE_DURATION = 0.10f;

        // 回弹到正常：平滑复原
        private const float RESTORE_DURATION = 0.18f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private EnemyBlob blob;
        private Rigidbody2D rb;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置（从 EnemyData 读取）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private float stopYPercent      = 0.35f;
        private float maxDistFromTower  = 6.5f;
        private float shootInterval     = 5f;
        private float repositionRange   = 2f;
        private GameObject projectilePrefab;
        private float projectileSpeed    = 5f;
        private int   projectileDamage   = 65;
        private float projectileHP       = 20f;
        private float projectileLifetime = 8f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private GunnerState state = GunnerState.Entering;
        private float targetWorldY;
        private float targetWorldX;
        private float chargeTimer    = 0f;
        private float chargeDuration = 5f;
        private bool  isActive       = false;
        private bool  isFirstShot    = true;

        private const float ENTRY_SPEED      = 3f;
        private const float REPOSITION_SPEED = 2f;
        private const float SNAP_DIST        = 0.08f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            blob = GetComponent<EnemyBlob>();
            rb   = GetComponent<Rigidbody2D>();

            // 自动查找炮口节点
            if (firePoint == null)
            {
                firePoint = transform.Find("FirePoint")
                         ?? transform.Find("Mouth")
                         ?? transform.Find("MouthPoint");

                if (firePoint == null)
                    GameLogger.LogWarning("[LavaGunnerAI] FirePoint 未设置！子弹将从怪物根节点发射。" +
                                         "请在 Prefab 中添加名为 'FirePoint' 的子节点并拖入此字段。");
            }
        }

        private void Update()
        {
            if (!isActive || blob == null || blob.IsDead) return;

            // 蓄力发光效果（逐帧插值）
            if (state == GunnerState.Charging)
                UpdateChargeGlow();
        }

        private void FixedUpdate()
        {
            if (!isActive || blob == null || blob.IsDead) return;

            switch (state)
            {
                case GunnerState.Entering:
                    UpdateEntering();
                    break;
                case GunnerState.Repositioning:
                    UpdateRepositioning();
                    break;
                // Charging / Shooting 阶段物理静止
                default:
                    rb.velocity = Vector2.zero;
                    break;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 对象池事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void OnBlobSpawned()
        {
            StopAllCoroutines();

            var data = blob.Data;
            if (data == null) return;

            stopYPercent      = data.gunnerStopYPercent;
            maxDistFromTower  = data.gunnerMaxDistFromTower;
            shootInterval     = data.gunnerShootInterval;
            repositionRange   = data.gunnerRepositionRange;
            projectilePrefab  = data.gunnerProjectilePrefab;
            projectileSpeed   = data.gunnerProjectileSpeed;
            projectileDamage  = data.gunnerProjectileDamage;
            projectileHP      = data.gunnerProjectileHP;
            projectileLifetime = data.gunnerProjectileLifetime;

            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                float worldTop    = cam.transform.position.y + cam.orthographicSize;
                float worldBottom = cam.transform.position.y - cam.orthographicSize;
                targetWorldY = Mathf.Lerp(worldTop, worldBottom, stopYPercent);
            }
            else
            {
                targetWorldY = 3f;
            }

            // 用光棱塔 Y + 最大允许距离来硬性限制停驻高度，
            // 防止炮手超出激光射程。
            Transform tower = blob.TargetTower;
            if (tower != null)
            {
                float yLimit = tower.position.y + maxDistFromTower;
                if (targetWorldY > yLimit)
                    targetWorldY = yLimit;
            }

            targetWorldX = transform.position.x;
            state        = GunnerState.Entering;
            isActive     = true;
            isFirstShot  = true;

            ResetEffectGlow();
        }

        public void OnBlobDeactivated()
        {
            isActive = false;
            StopAllCoroutines();
            if (rb != null) rb.velocity = Vector2.zero;
            ResetEffectGlow();
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

                // 首次入场使用更短的首发延迟，让玩家有时间反应但不要等太久
                EnterCharging(isFirstShot ? firstShotDelay : shootInterval);
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 蓄力（替代原 Idle）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void EnterCharging(float duration)
        {
            state         = GunnerState.Charging;
            chargeTimer   = 0f;
            chargeDuration = duration;
            rb.velocity   = Vector2.zero;
            ResetEffectGlow();
        }

        private void UpdateChargeGlow()
        {
            chargeTimer += Time.deltaTime;

            float t = Mathf.Clamp01(chargeTimer / chargeDuration);
            if (effectSprite != null)
                effectSprite.color = Color.Lerp(effectDimColor, effectGlowColor, t);

            if (chargeTimer >= chargeDuration)
            {
                // 防止重复触发：先切换状态再启动协程
                state = GunnerState.Shooting;
                isFirstShot = false;
                StartCoroutine(ShootRoutine());
            }
        }

        private void ResetEffectGlow()
        {
            if (effectSprite != null)
                effectSprite.color = effectDimColor;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 发射（弹压动画 + 出球 + 换位）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator ShootRoutine()
        {
            rb.velocity = Vector2.zero;

            // 记录发射前的实际 scale（EnemyBlob 会随血量缩小，以此为基准）
            Vector3 baseScale = transform.localScale;

            // 1. 压缩（蓄力弹压感）
            Vector3 compressScale = new Vector3(
                baseScale.x * COMPRESS_X,
                baseScale.y * COMPRESS_Y,
                baseScale.z);
            yield return StartCoroutine(LerpScale(baseScale, compressScale, COMPRESS_DURATION));

            // 2. 在压缩峰值发射火球
            FireProjectile();
            ResetEffectGlow();

            // 3. 弹起（出球瞬间拉伸感）
            Vector3 bounceScale = new Vector3(
                baseScale.x * BOUNCE_X,
                baseScale.y * BOUNCE_Y,
                baseScale.z);
            yield return StartCoroutine(LerpScale(compressScale, bounceScale, BOUNCE_DURATION));

            // 4. 平滑复原
            yield return StartCoroutine(LerpScale(bounceScale, baseScale, RESTORE_DURATION));

            // 5. 换位
            ChooseNewTargetX();
            state = GunnerState.Repositioning;
        }

        private IEnumerator LerpScale(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            transform.localScale = to;
        }

        private void FireProjectile()
        {
            if (projectilePrefab == null)
            {
                GameLogger.LogWarning("[LavaGunnerAI] 弹道预制体未设置！");
                return;
            }

            Transform target = blob.TargetTower;
            if (target == null) return;

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Vector2 dir = ((Vector2)(target.position - spawnPos)).normalized;

            GameObject go = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
            var proj = go.GetComponent<LavaProjectile>();
            if (proj != null)
                proj.Initialize(dir, projectileSpeed, projectileDamage, projectileHP, projectileLifetime);

            BattleStatistics.Instance?.RecordGunnerShot();
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
                // 换位后进入正常蓄力间隔
                EnterCharging(shootInterval);
            }
        }
    }
}
