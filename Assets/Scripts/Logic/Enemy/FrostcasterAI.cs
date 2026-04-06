// ============================================================
// FrostcasterAI.cs
// 文件位置: Assets/Scripts/Logic/Enemy/FrostcasterAI.cs
// 行为流程：
//   Entering      → 从屏幕顶部入场，到达停驻 Y
//   Charging      → 蓄力阶段：BodyEffect 从暗渐亮，计时结束触发施法
//   Casting       → 压缩→召唤冰墙→弹起→复原→发光淡出→换位
//   Repositioning → 横向移动到新 X，到达后回 Charging
// ============================================================

using System.Collections;
using UnityEngine;
using LightVsDecay.Audio;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;

namespace LightVsDecay.Logic.Enemy
{
    [RequireComponent(typeof(EnemyBlob))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class FrostcasterAI : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态枚举
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private enum CasterState { Entering, Charging, Casting, Repositioning }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("施法特效")]
        [Tooltip("蓄力/施法时发光的 BodyEffect SpriteRenderer")]
        [SerializeField] private SpriteRenderer effectSprite;

        [Tooltip("蓄力开始时的暗淡颜色（透明度为 0 表示完全不可见）")]
        [SerializeField][ColorUsage(true, true)]
        private Color effectDimColor  = new Color(0.35f, 0.80f, 1f, 0f);

        [Tooltip("蓄力结束时的最亮颜色（可填 HDR 值触发 Bloom）")]
        [SerializeField][ColorUsage(true, true)]
        private Color effectGlowColor = new Color(0.35f, 0.90f, 1f, 1f);

        [Tooltip("施法后发光淡出时长（秒）")]
        [SerializeField] private float glowFadeoutDuration = 0.4f;

        [Header("入场首发延迟")]
        [Tooltip("入场停稳后多久开始第一次蓄力（秒）；之后每次使用 castInterval")]
        [SerializeField] private float firstCastDelay = 1.5f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 施法动画常量（压缩-弹起）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private const float COMPRESS_X        = 1.25f;
        private const float COMPRESS_Y        = 0.60f;
        private const float COMPRESS_DURATION = 0.13f;

        private const float BOUNCE_X          = 0.82f;
        private const float BOUNCE_Y          = 1.28f;
        private const float BOUNCE_DURATION   = 0.09f;

        private const float RESTORE_DURATION  = 0.16f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private EnemyBlob blob;
        private Rigidbody2D rb;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置（从 EnemyData 读取）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private float stopYPercent   = 0.7f;
        private float maxDistFromTower = 6.5f;
        private float castInterval   = 2f;
        private float repositionRange = 3f;

        private int       iceWallCount   = 1;
        private bool      randomWallCount = false;
        private EnemyType iceWallType    = EnemyType.IceWall;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private CasterState state      = CasterState.Entering;
        private float targetWorldY;
        private float targetWorldX;
        private float chargeTimer    = 0f;
        private float chargeDuration = 2f;
        private bool  isActive       = false;
        private bool  isFirstCast    = true;

        private const float ENTRY_SPEED      = 3f;
        private const float REPOSITION_SPEED = 3f;
        private const float SNAP_DIST        = 0.08f;
        private const float WALL_MARGIN      = 1.5f;
        private const float WALL_SPAWN_MAX_Y_RATIO = 0.75f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            blob = GetComponent<EnemyBlob>();
            rb   = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (!isActive || blob == null || blob.IsDead) return;

            if (state == CasterState.Charging)
                UpdateChargeGlow();
        }

        private void FixedUpdate()
        {
            if (!isActive || blob == null || blob.IsDead) return;

            switch (state)
            {
                case CasterState.Entering:
                    UpdateEntering();
                    break;
                case CasterState.Repositioning:
                    UpdateRepositioning();
                    break;
                default:
                    rb.velocity = Vector2.zero;
                    break;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 对象池事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>EnemyBlob.OnSpawn() 时调用，重新读取配置并开始入场。</summary>
        public void OnBlobSpawned()
        {
            StopAllCoroutines();

            var data = blob.Data;
            if (data == null) return;

            // 共用远程怪参数
            stopYPercent    = data.gunnerStopYPercent;
            maxDistFromTower = data.gunnerMaxDistFromTower;
            castInterval    = data.gunnerShootInterval;
            repositionRange = data.gunnerRepositionRange;

            // 施法者专属参数
            iceWallCount    = data.frostcasterIceWallCount;
            randomWallCount = data.frostcasterRandomWallCount;
            iceWallType     = data.frostcasterIceWallType;

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
                targetWorldY = 3f;
            }

            // 限制在激光射程内
            Transform tower = blob.TargetTower;
            if (tower != null)
            {
                float yLimit = tower.position.y + maxDistFromTower;
                if (targetWorldY > yLimit)
                    targetWorldY = yLimit;
            }

            targetWorldX = transform.position.x;
            state        = CasterState.Entering;
            isActive     = true;
            isFirstCast  = true;

            ResetEffectGlow();
        }

        /// <summary>EnemyBlob.OnDespawn() 时调用，停止所有协程。</summary>
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

                EnterCharging(isFirstCast ? firstCastDelay : castInterval);
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 蓄力发光
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void EnterCharging(float duration)
        {
            state         = CasterState.Charging;
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
                state = CasterState.Casting;
                isFirstCast = false;
                StartCoroutine(CastRoutine());
            }
        }

        private void ResetEffectGlow()
        {
            if (effectSprite != null)
                effectSprite.color = effectDimColor;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 施法（压缩→召唤→弹起→复原→淡出→换位）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator CastRoutine()
        {
            rb.velocity = Vector2.zero;

            Vector3 baseScale = transform.localScale;

            // 1. 压缩（蓄力压缩感）
            Vector3 compressScale = new Vector3(
                baseScale.x * COMPRESS_X,
                baseScale.y * COMPRESS_Y,
                baseScale.z);
            yield return StartCoroutine(LerpScale(baseScale, compressScale, COMPRESS_DURATION));

            // 2. 压缩峰值：召唤冰墙
            SpawnIceWalls();

            // 3. 弹起（释放感）
            Vector3 bounceScale = new Vector3(
                baseScale.x * BOUNCE_X,
                baseScale.y * BOUNCE_Y,
                baseScale.z);
            yield return StartCoroutine(LerpScale(compressScale, bounceScale, BOUNCE_DURATION));

            // 4. 复原
            yield return StartCoroutine(LerpScale(bounceScale, baseScale, RESTORE_DURATION));

            // 5. 发光淡出
            yield return StartCoroutine(FadeGlow(effectGlowColor, effectDimColor, glowFadeoutDuration));

            // 6. 换位
            if (!blob.IsDead)
            {
                ChooseNewTargetX();
                state = CasterState.Repositioning;
            }
        }

        private void SpawnIceWalls()
        {
            if (EnemyPoolManager.Instance == null) return;

            int count = randomWallCount
                ? Random.Range(1, iceWallCount + 1)
                : iceWallCount;

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = GetRandomIceWallPosition();
                EnemyPoolManager.Instance.Spawn(iceWallType, spawnPos);
                // 每堵冰墙独立播放落地特效和音效
                VFXPoolManager.Instance?.PlayWinterImpact(spawnPos);
            }

            // 整组冰墙统一播放一次生成音效（避免多次叠加刺耳）
            AudioManager.Instance?.PlayIceWallSpawn();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 换位
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void ChooseNewTargetX()
        {
            Camera cam = Camera.main;
            float halfWidth = cam != null ? cam.orthographicSize * cam.aspect : 8f;
            float margin    = 1.5f;
            float minX      = -halfWidth + margin;
            float maxX      =  halfWidth - margin;

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
                EnterCharging(castInterval);
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 辅助：冰墙生成位置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private Vector3 GetRandomIceWallPosition()
        {
            Camera cam = Camera.main;
            if (cam == null) return transform.position + Vector3.up * 2f;

            float halfW = cam.orthographicSize * cam.aspect;
            float halfH = cam.orthographicSize;
            float camY  = cam.transform.position.y;
            float camX  = cam.transform.position.x;

            float minX = camX - halfW + WALL_MARGIN;
            float maxX = camX + halfW - WALL_MARGIN;
            float maxY = camY + halfH - WALL_MARGIN;
            float minY = camY - halfH * WALL_SPAWN_MAX_Y_RATIO + WALL_MARGIN;

            return new Vector3(
                Random.Range(minX, maxX),
                Random.Range(minY, maxY),
                0f);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 辅助：动画协程
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

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

        private IEnumerator FadeGlow(Color from, Color to, float duration)
        {
            if (effectSprite == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                effectSprite.color = Color.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            effectSprite.color = to;
        }
    }
}
