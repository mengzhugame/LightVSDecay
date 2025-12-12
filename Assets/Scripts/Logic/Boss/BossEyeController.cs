// ============================================================
// BossEyeController.cs
// 文件位置: Assets/Scripts/Logic/Boss/BossEyeController.cs
// 用途：Boss 眼睛状态控制（与Boss行为状态绑定）
// ============================================================

using UnityEngine;
using System.Collections;
#if DOTWEEN
using DG.Tweening;
#endif

namespace LightVsDecay.Logic.Boss
{
    /// <summary>
    /// Boss 眼睛状态
    /// </summary>
    public enum BossEyeState
    {
        Closed,     // 闭眼（防御态）
        Open        // 睁眼（攻击态/虚弱态）
    }
    
    /// <summary>
    /// Boss 眼睛控制器
    /// 控制眼睛的开闭状态、Collider激活、视觉效果
    /// 与BossController的行为状态绑定
    /// </summary>
    public class BossEyeController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("眼睛组件")]
        [Tooltip("眼睛Transform（用于缩放动画）")]
        [SerializeField] private Transform eyeTransform;
        
        [Tooltip("眼睛SpriteRenderer")]
        [SerializeField] private SpriteRenderer eyeRenderer;
        
        [Tooltip("眼睛Collider（弱点判定）")]
        [SerializeField] private Collider2D eyeCollider;
        
        [Header("怒目特效")]
        [Tooltip("Body03 - 红色身体特效（怒目时显示）")]
        [SerializeField] private GameObject redBodyEffect;
        
        [Tooltip("瞳孔发光特效（可选）")]
        [SerializeField] private GameObject pupilGlowEffect;
        
        [Header("动画参数")]
        [Tooltip("闭眼时Y轴缩放")]
        [Range(0f, 0.3f)]
        [SerializeField] private float closedScaleY = 0.1f;
        
        [Tooltip("睁眼时Collider放大倍数")]
        [SerializeField] private float openColliderScale = 1.5f;
        
        [Tooltip("开闭动画时长")]
        [SerializeField] private float transitionDuration = 0.2f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private BossEyeState currentState = BossEyeState.Closed;
        private Vector3 originalEyeScale;
        private Vector3 originalColliderScale;
        private Coroutine transitionCoroutine;
        
#if DOTWEEN
        private Tweener scaleTweener;
#endif
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前眼睛状态</summary>
        public BossEyeState CurrentState => currentState;
        
        /// <summary>眼睛是否睁开</summary>
        public bool IsOpen => currentState == BossEyeState.Open;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 自动查找组件
            if (eyeTransform == null)
            {
                eyeTransform = transform;
            }
            
            if (eyeRenderer == null)
            {
                eyeRenderer = GetComponent<SpriteRenderer>();
            }
            
            if (eyeCollider == null)
            {
                eyeCollider = GetComponent<Collider2D>();
            }
            
            // 记录原始缩放
            originalEyeScale = eyeTransform.localScale;
            
            if (eyeCollider != null)
            {
                // 对于CircleCollider2D，使用radius作为缩放基准
                originalColliderScale = Vector3.one;
            }
        }
        
        private void Start()
        {
            // 初始状态：闭眼
            SetStateDirect(BossEyeState.Closed);
        }
        
        private void OnDestroy()
        {
#if DOTWEEN
            if (scaleTweener != null && scaleTweener.IsActive())
            {
                scaleTweener.Kill();
            }
#endif
            
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置眼睛状态（带动画）
        /// </summary>
        public void SetState(BossEyeState newState)
        {
            if (currentState == newState) return;
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossEyeController] 状态切换: {currentState} -> {newState}");
            }
            
            currentState = newState;
            
#if DOTWEEN
            TransitionWithDOTween(newState);
#else
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            transitionCoroutine = StartCoroutine(TransitionCoroutine(newState));
#endif
        }
        
        /// <summary>
        /// 直接设置状态（无动画，用于初始化）
        /// </summary>
        public void SetStateDirect(BossEyeState newState)
        {
            currentState = newState;
            
            if (newState == BossEyeState.Closed)
            {
                ApplyClosedState();
            }
            else
            {
                ApplyOpenState();
            }
        }
        
        /// <summary>
        /// 睁眼（便捷方法）
        /// </summary>
        public void Open()
        {
            SetState(BossEyeState.Open);
        }
        
        /// <summary>
        /// 闭眼（便捷方法）
        /// </summary>
        public void Close()
        {
            SetState(BossEyeState.Closed);
        }
        
        /// <summary>
        /// 配置参数（由BossController调用）
        /// </summary>
        public void Configure(float closedScale, float colliderScale, float duration)
        {
            closedScaleY = closedScale;
            openColliderScale = colliderScale;
            transitionDuration = duration;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态应用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ApplyClosedState()
        {
            // 眼睛缩扁（眯眼）
            if (eyeTransform != null)
            {
                Vector3 closedScale = originalEyeScale;
                closedScale.y = originalEyeScale.y * closedScaleY;
                eyeTransform.localScale = closedScale;
            }
            
            // 禁用Collider
            if (eyeCollider != null)
            {
                eyeCollider.enabled = false;
            }
            
            // 隐藏怒目特效
            if (redBodyEffect != null)
            {
                redBodyEffect.SetActive(false);
            }
            
            if (pupilGlowEffect != null)
            {
                pupilGlowEffect.SetActive(false);
            }
        }
        
        private void ApplyOpenState()
        {
            // 眼睛恢复原大小
            if (eyeTransform != null)
            {
                eyeTransform.localScale = originalEyeScale;
            }
            
            // 激活并放大Collider
            if (eyeCollider != null)
            {
                eyeCollider.enabled = true;
                
                // 放大Collider（对于CircleCollider2D）
                CircleCollider2D circleCollider = eyeCollider as CircleCollider2D;
                if (circleCollider != null)
                {
                    // 注意：我们通过修改transform.localScale来影响collider
                    // 或者可以直接修改radius，但需要记录原始值
                }
            }
            
            // 显示怒目特效（红色身体）
            if (redBodyEffect != null)
            {
                redBodyEffect.SetActive(true);
            }
            
            if (pupilGlowEffect != null)
            {
                pupilGlowEffect.SetActive(true);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // DOTween 实现
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if DOTWEEN
        private void TransitionWithDOTween(BossEyeState newState)
        {
            // 停止当前动画
            if (scaleTweener != null && scaleTweener.IsActive())
            {
                scaleTweener.Kill();
            }
            
            if (newState == BossEyeState.Open)
            {
                // 睁眼动画
                
                // 激活Collider
                if (eyeCollider != null)
                {
                    eyeCollider.enabled = true;
                }
                
                // 显示怒目特效
                if (redBodyEffect != null)
                {
                    redBodyEffect.SetActive(true);
                }
                
                if (pupilGlowEffect != null)
                {
                    pupilGlowEffect.SetActive(true);
                }
                
                // 眼睛放大动画（从眯眼到正常）
                if (eyeTransform != null)
                {
                    scaleTweener = eyeTransform
                        .DOScale(originalEyeScale, transitionDuration)
                        .SetEase(Ease.OutBack);
                }
            }
            else
            {
                // 闭眼动画
                
                // 隐藏怒目特效
                if (redBodyEffect != null)
                {
                    redBodyEffect.SetActive(false);
                }
                
                if (pupilGlowEffect != null)
                {
                    pupilGlowEffect.SetActive(false);
                }
                
                // 眼睛缩扁动画
                Vector3 closedScale = originalEyeScale;
                closedScale.y = originalEyeScale.y * closedScaleY;
                
                if (eyeTransform != null)
                {
                    scaleTweener = eyeTransform
                        .DOScale(closedScale, transitionDuration)
                        .SetEase(Ease.InQuad)
                        .OnComplete(() => {
                            // 动画结束后禁用Collider
                            if (eyeCollider != null)
                            {
                                eyeCollider.enabled = false;
                            }
                        });
                }
                else
                {
                    // 无动画时直接禁用
                    if (eyeCollider != null)
                    {
                        eyeCollider.enabled = false;
                    }
                }
            }
        }
#endif
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 协程实现（Fallback）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator TransitionCoroutine(BossEyeState newState)
        {
            float elapsed = 0f;
            Vector3 startScale = eyeTransform != null ? eyeTransform.localScale : Vector3.one;
            Vector3 endScale;
            
            if (newState == BossEyeState.Open)
            {
                endScale = originalEyeScale;
                
                // 立即激活相关效果
                if (eyeCollider != null) eyeCollider.enabled = true;
                if (redBodyEffect != null) redBodyEffect.SetActive(true);
                if (pupilGlowEffect != null) pupilGlowEffect.SetActive(true);
            }
            else
            {
                endScale = originalEyeScale;
                endScale.y = originalEyeScale.y * closedScaleY;
                
                // 立即隐藏特效
                if (redBodyEffect != null) redBodyEffect.SetActive(false);
                if (pupilGlowEffect != null) pupilGlowEffect.SetActive(false);
            }
            
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionDuration;
                
                // 简单缓动
                float easeT = newState == BossEyeState.Open 
                    ? EaseOutBack(t)  // 睁眼用弹性
                    : t * t;          // 闭眼用平滑
                
                if (eyeTransform != null)
                {
                    eyeTransform.localScale = Vector3.Lerp(startScale, endScale, easeT);
                }
                
                yield return null;
            }
            
            // 确保最终状态
            if (eyeTransform != null)
            {
                eyeTransform.localScale = endScale;
            }
            
            // 闭眼动画结束后禁用Collider
            if (newState == BossEyeState.Closed && eyeCollider != null)
            {
                eyeCollider.enabled = false;
            }
            
            transitionCoroutine = null;
        }
        
        /// <summary>
        /// EaseOutBack 缓动
        /// </summary>
        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [ContextMenu("Test Open")]
        private void TestOpen()
        {
            Open();
        }
        
        [ContextMenu("Test Close")]
        private void TestClose()
        {
            Close();
        }
#endif
    }
}