using System.Collections;
using UnityEngine;
using LightVsDecay.Audio;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Statistics;

namespace LightVsDecay.Logic.Enemy
{
    [RequireComponent(typeof(EnemyBlob))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class LavaGunnerAI : MonoBehaviour
    {
        private enum GunnerState
        {
            Entering,
            Charging,
            Shooting,
            Repositioning
        }

        [Header("Fire Point")]
        [SerializeField] private Transform firePoint;

        [Header("Charge Effect")]
        [SerializeField] private SpriteRenderer effectSprite;
        [SerializeField] private Color effectDimColor = new Color(1f, 0.35f, 0f, 0.15f);
        [SerializeField] private Color effectGlowColor = new Color(1f, 0.85f, 0.1f, 1f);

        [Header("First Shot Delay")]
        [SerializeField] private float firstShotDelay = 1.2f;

        private const float COMPRESS_X = 1.28f;
        private const float COMPRESS_Y = 0.60f;
        private const float COMPRESS_DURATION = 0.14f;
        private const float BOUNCE_X = 0.80f;
        private const float BOUNCE_Y = 1.30f;
        private const float BOUNCE_DURATION = 0.10f;
        private const float RESTORE_DURATION = 0.18f;

        private const float ENTRY_SPEED = 3f;
        private const float REPOSITION_SPEED = 2f;
        private const float SNAP_DIST = 0.08f;
        private const float TARGET_MARGIN = 1.5f;
        private const float SCREEN_SAFE_MARGIN = 0.8f;
        private const float OFFSCREEN_RECOVERY_TIMEOUT = 1.0f;
        private const float MOVEMENT_STUCK_TIMEOUT = 0.45f;
        private const float MOVEMENT_PROGRESS_EPSILON = 0.02f;
        private const float REPOSITION_DESCEND_STEP = 0.85f;
        private const float MIN_DESCEND_SCREEN_RATIO = 0.5f;

        private EnemyBlob blob;
        private Rigidbody2D rb;

        private float stopYPercent = 0.35f;
        private float maxDistFromTower = 6.5f;
        private float shootInterval = 5f;
        private float repositionRange = 2f;
        private GameObject projectilePrefab;
        private float projectileSpeed = 5f;
        private int projectileDamage = 65;
        private float projectileHP = 20f;
        private float projectileLifetime = 8f;

        private GunnerState state = GunnerState.Entering;
        private float targetWorldY;
        private float targetWorldX;
        private float minAdvanceWorldY;
        private float chargeTimer;
        private float chargeDuration = 5f;
        private bool isActive;
        private bool isFirstShot = true;
        private float offscreenTimer;
        private float movementStuckTimer;
        private float lastDistanceToTarget = float.MaxValue;

        private void Awake()
        {
            blob = GetComponent<EnemyBlob>();
            rb = GetComponent<Rigidbody2D>();

            if (firePoint == null)
            {
                firePoint = transform.Find("FirePoint")
                    ?? transform.Find("Mouth")
                    ?? transform.Find("MouthPoint");

                if (firePoint == null)
                {
                    GameLogger.LogWarning("[LavaGunnerAI] FirePoint not assigned. Projectile will spawn at root transform.");
                }
            }
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

            if (state == GunnerState.Charging)
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
                case GunnerState.Entering:
                    UpdateEntering();
                    break;
                case GunnerState.Repositioning:
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

            EnemyData data = blob.Data;
            if (data == null)
            {
                return;
            }

            stopYPercent = data.gunnerStopYPercent;
            maxDistFromTower = data.gunnerMaxDistFromTower;
            shootInterval = data.gunnerShootInterval;
            repositionRange = data.gunnerRepositionRange;
            projectilePrefab = data.gunnerProjectilePrefab;
            projectileSpeed = data.gunnerProjectileSpeed;
            projectileDamage = data.gunnerProjectileDamage;
            projectileHP = data.gunnerProjectileHP;
            projectileLifetime = data.gunnerProjectileLifetime;

            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                float worldTop = cam.transform.position.y + cam.orthographicSize;
                float worldBottom = cam.transform.position.y - cam.orthographicSize;
                float screenHeight = worldTop - worldBottom;
                targetWorldY = worldTop - screenHeight * stopYPercent;
                minAdvanceWorldY = worldTop - screenHeight * MIN_DESCEND_SCREEN_RATIO;
            }
            else
            {
                targetWorldY = 3f;
                minAdvanceWorldY = 0f;
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

            if (targetWorldY < minAdvanceWorldY)
            {
                targetWorldY = minAdvanceWorldY;
            }

            targetWorldX = transform.position.x;
            state = GunnerState.Entering;
            isActive = true;
            isFirstShot = true;
            offscreenTimer = 0f;
            ResetMovementRecovery();

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

            ResetMovementRecovery();
            ResetEffectGlow();
        }

        private void UpdateEntering()
        {
            Vector2 targetPos = new Vector2(transform.position.x, targetWorldY);
            float dy = transform.position.y - targetWorldY;
            if (dy > SNAP_DIST)
            {
                rb.velocity = Vector2.down * ENTRY_SPEED;

                if (IsMovementStuck(targetPos))
                {
                    CompleteEntering();
                }
                return;
            }

            CompleteEntering();
        }

        private void CompleteEntering()
        {
            rb.velocity = Vector2.zero;
            Vector3 pos = transform.position;
            pos.y = targetWorldY;
            transform.position = pos;
            targetWorldX = pos.x;
            ResetMovementRecovery();

            EnterCharging(isFirstShot ? firstShotDelay : shootInterval);
        }

        private void EnterCharging(float duration)
        {
            state = GunnerState.Charging;
            chargeTimer = 0f;
            chargeDuration = duration;
            rb.velocity = Vector2.zero;
            ResetMovementRecovery();
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
                state = GunnerState.Shooting;
                isFirstShot = false;
                StartCoroutine(ShootRoutine());
            }
        }

        private void ResetEffectGlow()
        {
            if (effectSprite != null)
            {
                effectSprite.color = effectDimColor;
            }
        }

        private IEnumerator ShootRoutine()
        {
            rb.velocity = Vector2.zero;

            Vector3 baseScale = transform.localScale;
            Vector3 compressScale = new Vector3(
                baseScale.x * COMPRESS_X,
                baseScale.y * COMPRESS_Y,
                baseScale.z);

            yield return StartCoroutine(LerpScale(baseScale, compressScale, COMPRESS_DURATION));

            FireProjectile();
            ResetEffectGlow();

            Vector3 bounceScale = new Vector3(
                baseScale.x * BOUNCE_X,
                baseScale.y * BOUNCE_Y,
                baseScale.z);

            yield return StartCoroutine(LerpScale(compressScale, bounceScale, BOUNCE_DURATION));
            yield return StartCoroutine(LerpScale(bounceScale, baseScale, RESTORE_DURATION));

            if (blob == null || blob.IsDead)
            {
                yield break;
            }

            ChooseNextRepositionTarget();
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
                GameLogger.LogWarning("[LavaGunnerAI] Projectile prefab not assigned.");
                return;
            }

            if (IsOutsideScreen())
            {
                return;
            }

            Transform target = blob.TargetTower;
            if (target == null)
            {
                return;
            }

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Vector2 dir = ((Vector2)(target.position - spawnPos)).normalized;

            GameObject go = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
            LavaProjectile proj = go.GetComponent<LavaProjectile>();
            if (proj != null)
            {
                proj.Initialize(dir, projectileSpeed, projectileDamage, projectileHP, projectileLifetime);
            }

            AudioManager.Instance?.PlayGunnerSpit();
            BattleStatistics.Instance?.RecordGunnerShot();
        }

        private void ChooseNextRepositionTarget()
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

            if (transform.position.y - minAdvanceWorldY > SNAP_DIST)
            {
                targetWorldY = Mathf.Max(minAdvanceWorldY, transform.position.y - REPOSITION_DESCEND_STEP);
            }
            else
            {
                targetWorldY = transform.position.y;
            }
        }

        private void UpdateRepositioning()
        {
            Vector2 currentPos = transform.position;
            Vector2 targetPos = new Vector2(targetWorldX, targetWorldY);
            Vector2 delta = targetPos - currentPos;

            if (delta.magnitude > SNAP_DIST)
            {
                rb.velocity = delta.normalized * REPOSITION_SPEED;

                if (IsMovementStuck(targetPos))
                {
                    CompleteReposition();
                }
                return;
            }

            CompleteReposition();
        }

        private void CompleteReposition()
        {
            rb.velocity = Vector2.zero;
            Vector3 pos = transform.position;
            pos.x = targetWorldX;
            pos.y = targetWorldY;
            transform.position = pos;
            ResetMovementRecovery();
            EnterCharging(shootInterval);
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
            state = GunnerState.Entering;
            chargeTimer = 0f;
            ResetMovementRecovery();
            ResetEffectGlow();

            return true;
        }

        private bool IsMovementStuck(Vector2 targetPos)
        {
            float currentDistance = Vector2.Distance(transform.position, targetPos);
            if (currentDistance + MOVEMENT_PROGRESS_EPSILON < lastDistanceToTarget)
            {
                lastDistanceToTarget = currentDistance;
                movementStuckTimer = 0f;
                return false;
            }

            movementStuckTimer += Time.fixedDeltaTime;
            lastDistanceToTarget = currentDistance;
            return movementStuckTimer >= MOVEMENT_STUCK_TIMEOUT;
        }

        private void ResetMovementRecovery()
        {
            movementStuckTimer = 0f;
            lastDistanceToTarget = float.MaxValue;
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
