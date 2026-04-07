using System.Collections;
using UnityEngine;
using LightVsDecay.Audio;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Logic.Statistics;

namespace LightVsDecay.Logic.Enemy
{
    [RequireComponent(typeof(EnemyBlob))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class FrostcasterAI : MonoBehaviour
    {
        private enum CasterState
        {
            Entering,
            Charging,
            Casting,
            Repositioning
        }

        [Header("Cast Effect")]
        [SerializeField] private SpriteRenderer effectSprite;
        [SerializeField][ColorUsage(true, true)] private Color effectDimColor = new Color(0.35f, 0.80f, 1f, 0f);
        [SerializeField][ColorUsage(true, true)] private Color effectGlowColor = new Color(0.35f, 0.90f, 1f, 1f);
        [SerializeField] private float glowFadeoutDuration = 0.4f;

        [Header("First Cast Delay")]
        [SerializeField] private float firstCastDelay = 1.5f;

        private const float COMPRESS_X = 1.25f;
        private const float COMPRESS_Y = 0.60f;
        private const float COMPRESS_DURATION = 0.13f;
        private const float BOUNCE_X = 0.82f;
        private const float BOUNCE_Y = 1.28f;
        private const float BOUNCE_DURATION = 0.09f;
        private const float RESTORE_DURATION = 0.16f;

        private const float ENTRY_SPEED = 3f;
        private const float REPOSITION_SPEED = 3f;
        private const float SNAP_DIST = 0.08f;
        private const float TARGET_MARGIN = 1.5f;
        private const float WALL_SPAWN_MAX_Y_RATIO = 0.75f;
        private const float SCREEN_SAFE_MARGIN = 0.8f;
        private const float OFFSCREEN_RECOVERY_TIMEOUT = 1.0f;

        private EnemyBlob blob;
        private Rigidbody2D rb;

        private float stopYPercent = 0.7f;
        private float maxDistFromTower = 6.5f;
        private float castInterval = 2f;
        private float repositionRange = 3f;
        private int iceWallCount = 1;
        private bool randomWallCount;
        private EnemyType iceWallType = EnemyType.IceWall;

        private CasterState state = CasterState.Entering;
        private float targetWorldY;
        private float targetWorldX;
        private float chargeTimer;
        private float chargeDuration = 2f;
        private bool isActive;
        private bool isFirstCast = true;
        private float offscreenTimer;

        private void Awake()
        {
            blob = GetComponent<EnemyBlob>();
            rb = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (!isActive || blob == null || blob.IsDead)
            {
                return;
            }

            if (RecoverIfOutsideScreen())
            {
                return;
            }

            if (state == CasterState.Charging)
            {
                UpdateChargeGlow();
            }
        }

        private void FixedUpdate()
        {
            if (!isActive || blob == null || blob.IsDead)
            {
                return;
            }

            if (RecoverIfOutsideScreen())
            {
                rb.velocity = Vector2.zero;
                return;
            }

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

        public void OnBlobSpawned()
        {
            StopAllCoroutines();

            var data = blob.Data;
            if (data == null)
            {
                return;
            }

            stopYPercent = data.gunnerStopYPercent;
            maxDistFromTower = data.gunnerMaxDistFromTower;
            castInterval = data.gunnerShootInterval;
            repositionRange = data.gunnerRepositionRange;
            iceWallCount = data.frostcasterIceWallCount;
            randomWallCount = data.frostcasterRandomWallCount;
            iceWallType = data.frostcasterIceWallType;

            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                float worldTop = cam.transform.position.y + cam.orthographicSize;
                float worldBottom = cam.transform.position.y - cam.orthographicSize;
                targetWorldY = Mathf.Lerp(worldTop, worldBottom, stopYPercent);
            }
            else
            {
                targetWorldY = 3f;
            }

            Transform tower = blob.TargetTower;
            if (tower != null)
            {
                float yLimit = tower.position.y + maxDistFromTower;
                if (targetWorldY > yLimit)
                {
                    targetWorldY = yLimit;
                }
            }

            targetWorldX = transform.position.x;
            state = CasterState.Entering;
            isActive = true;
            isFirstCast = true;
            offscreenTimer = 0f;

            ResetEffectGlow();
        }

        public void OnBlobDeactivated()
        {
            isActive = false;
            StopAllCoroutines();

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }

            ResetEffectGlow();
        }

        private void UpdateEntering()
        {
            float dy = transform.position.y - targetWorldY;
            if (dy > SNAP_DIST)
            {
                rb.velocity = Vector2.down * ENTRY_SPEED;
                return;
            }

            rb.velocity = Vector2.zero;
            Vector3 pos = transform.position;
            pos.y = targetWorldY;
            transform.position = pos;
            targetWorldX = pos.x;

            EnterCharging(isFirstCast ? firstCastDelay : castInterval);
        }

        private void EnterCharging(float duration)
        {
            state = CasterState.Charging;
            chargeTimer = 0f;
            chargeDuration = duration;
            rb.velocity = Vector2.zero;
            ResetEffectGlow();
        }

        private void UpdateChargeGlow()
        {
            chargeTimer += Time.deltaTime;

            float t = Mathf.Clamp01(chargeTimer / chargeDuration);
            if (effectSprite != null)
            {
                effectSprite.color = Color.Lerp(effectDimColor, effectGlowColor, t);
            }

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
            {
                effectSprite.color = effectDimColor;
            }
        }

        private IEnumerator CastRoutine()
        {
            rb.velocity = Vector2.zero;

            Vector3 baseScale = transform.localScale;
            Vector3 compressScale = new Vector3(
                baseScale.x * COMPRESS_X,
                baseScale.y * COMPRESS_Y,
                baseScale.z);

            yield return StartCoroutine(LerpScale(baseScale, compressScale, COMPRESS_DURATION));
            SpawnIceWalls();

            Vector3 bounceScale = new Vector3(
                baseScale.x * BOUNCE_X,
                baseScale.y * BOUNCE_Y,
                baseScale.z);

            yield return StartCoroutine(LerpScale(compressScale, bounceScale, BOUNCE_DURATION));
            yield return StartCoroutine(LerpScale(bounceScale, baseScale, RESTORE_DURATION));
            yield return StartCoroutine(FadeGlow(effectGlowColor, effectDimColor, glowFadeoutDuration));

            if (blob == null || blob.IsDead)
            {
                yield break;
            }

            ChooseNewTargetX();
            state = CasterState.Repositioning;
        }

        private void SpawnIceWalls()
        {
            if (EnemyPoolManager.Instance == null || IsOutsideScreen())
            {
                return;
            }

            int count = randomWallCount
                ? Random.Range(1, iceWallCount + 1)
                : iceWallCount;

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = GetRandomIceWallPosition();
                EnemyPoolManager.Instance.Spawn(iceWallType, spawnPos);
                VFXPoolManager.Instance?.PlayWinterImpact(spawnPos);
            }

            AudioManager.Instance?.PlayIceWallSpawn();
            BattleStatistics.Instance?.RecordFrostcasterCast();
        }

        private void ChooseNewTargetX()
        {
            float minX;
            float maxX;

            if (ScreenBoundaryManager.Instance != null)
            {
                minX = ScreenBoundaryManager.Instance.ScreenLeft + TARGET_MARGIN;
                maxX = ScreenBoundaryManager.Instance.ScreenRight - TARGET_MARGIN;
            }
            else
            {
                Camera cam = Camera.main;
                float halfWidth = cam != null ? cam.orthographicSize * cam.aspect : 8f;
                float camX = cam != null ? cam.transform.position.x : 0f;
                minX = camX - halfWidth + TARGET_MARGIN;
                maxX = camX + halfWidth - TARGET_MARGIN;
            }

            float candidateMin = Mathf.Max(minX, transform.position.x - repositionRange);
            float candidateMax = Mathf.Min(maxX, transform.position.x + repositionRange);

            targetWorldX = candidateMin < candidateMax
                ? Random.Range(candidateMin, candidateMax)
                : Mathf.Clamp(transform.position.x, minX, maxX);
        }

        private void UpdateRepositioning()
        {
            float dx = targetWorldX - transform.position.x;
            if (Mathf.Abs(dx) > SNAP_DIST)
            {
                rb.velocity = new Vector2(Mathf.Sign(dx) * REPOSITION_SPEED, 0f);
                return;
            }

            rb.velocity = Vector2.zero;
            Vector3 pos = transform.position;
            pos.x = targetWorldX;
            transform.position = pos;
            EnterCharging(castInterval);
        }

        private Vector3 GetRandomIceWallPosition()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return transform.position + Vector3.up * 2f;
            }

            float halfW = cam.orthographicSize * cam.aspect;
            float halfH = cam.orthographicSize;
            float camY = cam.transform.position.y;
            float camX = cam.transform.position.x;

            float minX = camX - halfW + TARGET_MARGIN;
            float maxX = camX + halfW - TARGET_MARGIN;
            float maxY = camY + halfH - TARGET_MARGIN;
            float minY = camY - halfH * WALL_SPAWN_MAX_Y_RATIO + TARGET_MARGIN;

            return new Vector3(
                Random.Range(minX, maxX),
                Random.Range(minY, maxY),
                0f);
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

        private IEnumerator FadeGlow(Color from, Color to, float duration)
        {
            if (effectSprite == null)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                effectSprite.color = Color.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            effectSprite.color = to;
        }

        private bool RecoverIfOutsideScreen()
        {
            if (!IsOutsideScreen())
            {
                offscreenTimer = 0f;
                return false;
            }

            offscreenTimer += Time.deltaTime;

            if (offscreenTimer >= OFFSCREEN_RECOVERY_TIMEOUT)
            {
                EnemyPoolManager.Instance?.Despawn(blob);
                return true;
            }

            StopAllCoroutines();
            rb.velocity = Vector2.zero;

            Vector2 safePosition = GetClampedScreenPosition();
            transform.position = new Vector3(safePosition.x, safePosition.y, transform.position.z);

            targetWorldX = safePosition.x;
            targetWorldY = safePosition.y;
            state = CasterState.Entering;
            chargeTimer = 0f;
            ResetEffectGlow();

            return true;
        }

        private bool IsOutsideScreen()
        {
            if (ScreenBoundaryManager.Instance != null)
            {
                return !ScreenBoundaryManager.Instance.IsPointInScreen(transform.position);
            }

            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return false;
            }

            Vector3 viewPos = cam.WorldToViewportPoint(transform.position);
            return viewPos.x < 0f || viewPos.x > 1f || viewPos.y < 0f || viewPos.y > 1f;
        }

        private Vector2 GetClampedScreenPosition()
        {
            if (ScreenBoundaryManager.Instance != null)
            {
                Vector2 clamped = ScreenBoundaryManager.Instance.ClampToScreen(transform.position);
                return new Vector2(
                    Mathf.Clamp(
                        clamped.x,
                        ScreenBoundaryManager.Instance.ScreenLeft + SCREEN_SAFE_MARGIN,
                        ScreenBoundaryManager.Instance.ScreenRight - SCREEN_SAFE_MARGIN),
                    Mathf.Clamp(
                        clamped.y,
                        ScreenBoundaryManager.Instance.ScreenBottom + SCREEN_SAFE_MARGIN,
                        ScreenBoundaryManager.Instance.ScreenTop - SCREEN_SAFE_MARGIN));
            }

            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return transform.position;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 camPos = cam.transform.position;

            return new Vector2(
                Mathf.Clamp(
                    transform.position.x,
                    camPos.x - halfWidth + SCREEN_SAFE_MARGIN,
                    camPos.x + halfWidth - SCREEN_SAFE_MARGIN),
                Mathf.Clamp(
                    transform.position.y,
                    camPos.y - halfHeight + SCREEN_SAFE_MARGIN,
                    camPos.y + halfHeight - SCREEN_SAFE_MARGIN));
        }
    }
}
