using System;
using System.Collections;
using LightVsDecay.Core;
using UnityEngine;

namespace LightVsDecay.Logic.Coin
{
    /// <summary>
    /// Single visual coin pickup used by the battle coin drop presentation.
    /// </summary>
    public class CoinPickup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TrailRenderer trailRenderer;

        [Header("Scatter")]
        [SerializeField] private float scatterDuration = 0.22f;
        [SerializeField] private float scatterDistanceMin = 0.35f;
        [SerializeField] private float scatterDistanceMax = 0.9f;
        [SerializeField] private float scatterArcHeight = 0.2f;

        [Header("Hover")]
        [SerializeField] private float hoverDurationMin = 0.45f;
        [SerializeField] private float hoverDurationMax = 0.7f;
        [SerializeField] private float hoverAmplitude = 0.06f;
        [SerializeField] private float hoverFrequency = 5f;

        [Header("Absorb")]
        [SerializeField] private float absorbDelayMin = 0f;
        [SerializeField] private float absorbDelayMax = 0.18f;
        [SerializeField] private float absorbDurationMin = 0.32f;
        [SerializeField] private float absorbDurationMax = 0.48f;
        [SerializeField] private float absorbArcHeight = 0.8f;
        [SerializeField] private float arriveThreshold = 0.15f;

        [Header("Scale")]
        [SerializeField] private Vector2 scaleRange = new Vector2(0.9f, 1.08f);

        [Header("Flipbook")]
        [SerializeField] private string frameOffsetProperty = "_FrameOffset";

        private MaterialPropertyBlock propertyBlock;

        private Func<Vector3> targetPositionGetter;
        private Action<CoinPickup, int> arriveCallback;
        private Action<CoinPickup> recycleCallback;
        private Coroutine playCoroutine;
        private Vector3 baseScale;
        private int visualValue;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            propertyBlock = new MaterialPropertyBlock();
            baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            if (playCoroutine != null)
            {
                StopCoroutine(playCoroutine);
                playCoroutine = null;
            }
        }

        public void Play(
            Vector3 startPosition,
            int value,
            Func<Vector3> targetGetter,
            Action<CoinPickup, int> onArrive,
            Action<CoinPickup> onRecycle)
        {
            visualValue = Mathf.Max(1, value);
            targetPositionGetter = targetGetter;
            arriveCallback = onArrive;
            recycleCallback = onRecycle;

            ResetState();

            transform.position = startPosition;
            gameObject.SetActive(true);

            playCoroutine = StartCoroutine(PlayRoutine(startPosition));
        }

        public void ResetState()
        {
            if (trailRenderer != null)
            {
                trailRenderer.Clear();
                trailRenderer.emitting = false;
            }

            float randomScale = UnityEngine.Random.Range(scaleRange.x, scaleRange.y);
            transform.localScale = baseScale * randomScale;

            if (spriteRenderer != null && propertyBlock != null)
            {
                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(frameOffsetProperty, UnityEngine.Random.Range(0f, 4f));
                spriteRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private IEnumerator PlayRoutine(Vector3 startPosition)
        {
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            if (randomDir.sqrMagnitude < 0.01f)
            {
                randomDir = Vector2.up;
            }

            float scatterDistance = UnityEngine.Random.Range(scatterDistanceMin, scatterDistanceMax);
            Vector3 scatterTarget = startPosition + (Vector3)(randomDir * scatterDistance);
            float hoverDuration = UnityEngine.Random.Range(hoverDurationMin, hoverDurationMax);
            float absorbDelay = UnityEngine.Random.Range(absorbDelayMin, absorbDelayMax);
            float absorbDuration = UnityEngine.Random.Range(absorbDurationMin, absorbDurationMax);

            yield return PlayScatter(startPosition, scatterTarget);
            yield return PlayHover(scatterTarget, hoverDuration);
            yield return new WaitForSeconds(absorbDelay);
            yield return PlayAbsorb(scatterTarget, absorbDuration);

            arriveCallback?.Invoke(this, visualValue);
            recycleCallback?.Invoke(this);
            playCoroutine = null;
        }

        private IEnumerator PlayScatter(Vector3 startPosition, Vector3 scatterTarget)
        {
            float elapsed = 0f;

            while (elapsed < scatterDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / scatterDuration);
                float eased = EaseOutCubic(t);

                Vector3 pos = Vector3.LerpUnclamped(startPosition, scatterTarget, eased);
                pos.y += Mathf.Sin(t * Mathf.PI) * scatterArcHeight;
                transform.position = pos;

                yield return null;
            }

            transform.position = scatterTarget;
        }

        private IEnumerator PlayHover(Vector3 hoverCenter, float hoverDuration)
        {
            float elapsed = 0f;

            while (elapsed < hoverDuration)
            {
                elapsed += Time.deltaTime;

                Vector3 pos = hoverCenter;
                pos.y += Mathf.Sin(elapsed * hoverFrequency * Mathf.PI * 2f) * hoverAmplitude;
                transform.position = pos;

                yield return null;
            }
        }

        private IEnumerator PlayAbsorb(Vector3 absorbStart, float absorbDuration)
        {
            Vector3 targetPos = targetPositionGetter != null ? targetPositionGetter() : absorbStart;
            Vector3 controlPoint = Vector3.Lerp(absorbStart, targetPos, 0.5f) + Vector3.up * absorbArcHeight;

            if (trailRenderer != null)
            {
                trailRenderer.emitting = true;
            }

            float elapsed = 0f;
            while (elapsed < absorbDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / absorbDuration);
                float eased = EaseInCubic(t);
                targetPos = targetPositionGetter != null ? targetPositionGetter() : targetPos;
                controlPoint = Vector3.Lerp(absorbStart, targetPos, 0.5f) + Vector3.up * absorbArcHeight;

                transform.position = EvaluateQuadraticBezier(absorbStart, controlPoint, targetPos, eased);

                if (Vector3.Distance(transform.position, targetPos) <= arriveThreshold)
                {
                    transform.position = targetPos;
                    yield break;
                }

                yield return null;
            }

            transform.position = targetPos;
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        private static float EaseInCubic(float t)
        {
            return t * t * t;
        }

        private static Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            float inv = 1f - t;
            return inv * inv * p0 + 2f * inv * t * p1 + t * t * p2;
        }
    }
}
