// ============================================================
// CameraShake.cs
// 文件位置: Assets/Scripts/Core/CameraShake.cs
// 用途：轻量级相机震动系统（支持DOTween和协程双模式）
// ============================================================

using UnityEngine;
using System.Collections;
#if DOTWEEN
using DG.Tweening;
#endif

namespace LightVsDecay.Core
{
    /// <summary>
    /// 轻量级相机震动管理器
    /// 单例模式，支持多种震动效果
    /// 优先使用DOTween，无DOTween时自动降级为协程
    /// </summary>
    public class CameraShake : Singleton<CameraShake>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("引用")]
        [Tooltip("要震动的相机（默认使用Main Camera）")]
        [SerializeField] private Camera targetCamera;
        
        [Header("默认参数")]
        [Tooltip("默认震动强度")]
        [SerializeField] private float defaultIntensity = 0.3f;
        
        [Tooltip("默认震动时长")]
        [SerializeField] private float defaultDuration = 0.2f;
        
        [Tooltip("震动频率（每秒抖动次数）")]
        [SerializeField] private float shakeFrequency = 25f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Vector3 originalPosition;
        private bool isShaking = false;
        private Coroutine shakeCoroutine;
        
#if DOTWEEN
        private Tweener shakeTweener;
#endif
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            // 自动获取主相机
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
            
            if (targetCamera != null)
            {
                originalPosition = targetCamera.transform.localPosition;
            }
        }
        
        private void OnDisable()
        {
            // 确保停止时恢复位置
            StopShake();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 触发屏幕震动（使用默认参数）
        /// </summary>
        public void Shake()
        {
            Shake(defaultIntensity, defaultDuration);
        }
        
        /// <summary>
        /// 触发屏幕震动
        /// </summary>
        /// <param name="intensity">震动强度</param>
        /// <param name="duration">震动时长</param>
        public void Shake(float intensity, float duration)
        {
            if (targetCamera == null)
            {
                GameLogger.LogWarning("[CameraShake] 目标相机未设置！");
                return;
            }
            
            // 停止当前震动
            StopShake();
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[CameraShake] 开始震动 - 强度: {intensity}, 时长: {duration}s");
            }
            
#if DOTWEEN
            // 使用DOTween
            ShakeWithDOTween(intensity, duration);
#else
            // 使用协程
            shakeCoroutine = StartCoroutine(ShakeCoroutine(intensity, duration));
#endif
        }
        
        /// <summary>
        /// 触发冲击震动（适用于Boss冲撞命中）
        /// </summary>
        /// <param name="direction">冲击方向</param>
        /// <param name="intensity">强度</param>
        /// <param name="duration">时长</param>
        public void ImpactShake(Vector2 direction, float intensity, float duration)
        {
            if (targetCamera == null) return;
            
            StopShake();
            
#if DOTWEEN
            // 方向性震动
            Vector3 punchDir = new Vector3(direction.x, direction.y, 0f).normalized * intensity;
            targetCamera.transform.DOPunchPosition(punchDir, duration, 10, 0.5f)
                .OnComplete(() => {
                    targetCamera.transform.localPosition = originalPosition;
                    isShaking = false;
                });
            isShaking = true;
#else
            shakeCoroutine = StartCoroutine(DirectionalShakeCoroutine(direction, intensity, duration));
#endif
        }
        
        /// <summary>
        /// 停止当前震动
        /// </summary>
        public void StopShake()
        {
            if (!isShaking) return;
            
#if DOTWEEN
            if (shakeTweener != null && shakeTweener.IsActive())
            {
                shakeTweener.Kill();
                shakeTweener = null;
            }
            // 确保DOTween的震动也停止
            targetCamera.transform.DOKill();
#endif
            
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
                shakeCoroutine = null;
            }
            
            // 恢复原始位置
            if (targetCamera != null)
            {
                targetCamera.transform.localPosition = originalPosition;
            }
            
            isShaking = false;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // DOTween 实现
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if DOTWEEN
        private void ShakeWithDOTween(float intensity, float duration)
        {
            isShaking = true;
            
            int vibrato = Mathf.RoundToInt(shakeFrequency * duration);
            
            shakeTweener = targetCamera.transform
                .DOShakePosition(duration, intensity, vibrato, 90f, false, true)
                .OnComplete(() => {
                    targetCamera.transform.localPosition = originalPosition;
                    isShaking = false;
                    
                    if (showDebugInfo)
                    {
                        GameLogger.Log("[CameraShake] 震动结束 (DOTween)");
                    }
                });
        }
#endif
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 协程实现（Fallback）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator ShakeCoroutine(float intensity, float duration)
        {
            isShaking = true;
            float elapsed = 0f;
            float interval = 1f / shakeFrequency;
            
            while (elapsed < duration)
            {
                // 随机偏移
                float x = Random.Range(-1f, 1f) * intensity;
                float y = Random.Range(-1f, 1f) * intensity;
                
                // 强度随时间衰减
                float decay = 1f - (elapsed / duration);
                targetCamera.transform.localPosition = originalPosition + new Vector3(x, y, 0f) * decay;
                
                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }
            
            // 恢复原位
            targetCamera.transform.localPosition = originalPosition;
            isShaking = false;
            shakeCoroutine = null;
            
            if (showDebugInfo)
            {
                GameLogger.Log("[CameraShake] 震动结束 (Coroutine)");
            }
        }
        
        private IEnumerator DirectionalShakeCoroutine(Vector2 direction, float intensity, float duration)
        {
            isShaking = true;
            float elapsed = 0f;
            Vector3 dir3D = new Vector3(direction.x, direction.y, 0f).normalized;
            
            // 先向冲击方向位移
            targetCamera.transform.localPosition = originalPosition + dir3D * intensity;
            
            yield return new WaitForSeconds(duration * 0.2f);
            elapsed = duration * 0.2f;
            
            // 然后震动衰减
            float interval = 1f / shakeFrequency;
            while (elapsed < duration)
            {
                float decay = 1f - (elapsed / duration);
                float x = Random.Range(-1f, 1f) * intensity * decay * 0.5f;
                float y = Random.Range(-1f, 1f) * intensity * decay * 0.5f;
                
                targetCamera.transform.localPosition = originalPosition + new Vector3(x, y, 0f);
                
                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }
            
            targetCamera.transform.localPosition = originalPosition;
            isShaking = false;
            shakeCoroutine = null;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [ContextMenu("Test Shake")]
        private void TestShake()
        {
            Shake(0.5f, 0.5f);
        }
        
        [ContextMenu("Test Impact Shake")]
        private void TestImpactShake()
        {
            ImpactShake(Vector2.down, 0.8f, 0.3f);
        }
#endif
    }
}