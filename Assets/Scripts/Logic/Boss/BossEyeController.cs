// ============================================================
// BossEyeController.cs
// 文件位置: Assets/Scripts/Logic/Boss/BossEyeController.cs
// 用途：Boss 眼睛状态控制（与Boss行为状态绑定）
// 【重构】添加缓慢睁开方法（用于Press技能）
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
        Opening,    // 正在睁开（过渡态）【新增】
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
        
        [Tooltip("快速开闭动画时长")]
        [SerializeField] private float transitionDuration = 0.2f;
        
        [Tooltip("缓慢睁开动画时长（Press用）")]
        [SerializeField] private float slowOpenDuration = 1.0f;
        
        [Header("颜色配置")]
        [Tooltip("闭眼时颜色（暗淡）")]
        [SerializeField] private Color closedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        
        [Tooltip("睁眼时颜色（高亮红/紫）")]
        [SerializeField] private Color openColor = new Color(1f, 0.2f, 0.2f, 1f);
        
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
        private Tweener colorTweener;
#endif
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前眼睛状态</summary>
        public BossEyeState CurrentState => currentState;
        
        /// <summary>是否可被攻击（睁眼或正在睁开）</summary>
        public bool IsVulnerable => currentState == BossEyeState.Open;
        /// <summary>【新增】眼睛是否睁开（用于弱点判定）</summary>
        public bool IsOpen => currentState == BossEyeState.Open || currentState == BossEyeState.Opening;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 缓存原始缩放
            if (eyeTransform != null)
            {
                originalEyeScale = eyeTransform.localScale;
            }
        }
        
        private void Start()
        {
            // 初始化为闭眼状态
            SetStateDirect(BossEyeState.Closed);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 快速睁眼（用于Charge蓄力、Stun状态）
        /// </summary>
        public void Open()
        {
            if (currentState == BossEyeState.Open) return;
            
            StopCurrentTransition();
            transitionCoroutine = StartCoroutine(TransitionToState(BossEyeState.Open, transitionDuration));
        }
        
        /// <summary>
        /// 快速闭眼（用于Idle、Spawn）
        /// </summary>
        public void Close()
        {
            if (currentState == BossEyeState.Closed) return;
            
            StopCurrentTransition();
            transitionCoroutine = StartCoroutine(TransitionToState(BossEyeState.Closed, transitionDuration));
        }
        
        /// <summary>
        /// 缓慢睁开眼睛（用于Press施压阶段）
        /// 视觉效果：像机械光圈缓缓打开
        /// </summary>
        /// <param name="duration">睁开持续时间（默认使用配置值）</param>
        public void OpenSlowly(float duration = -1f)
        {
            if (duration < 0) duration = slowOpenDuration;
            
            StopCurrentTransition();
            transitionCoroutine = StartCoroutine(SlowOpenRoutine(duration));
        }
        /// <summary>
        /// 被动眨眼（快速睁开后自动闭合）
        /// 用于召唤/喷吐时的弱点暴露窗口
        /// </summary>
        /// <param name="duration">睁眼持续时间（默认0.75秒）</param>
        public void Blink(float duration = 0.75f)
        {
            StopCurrentTransition();
            transitionCoroutine = StartCoroutine(BlinkRoutine(duration));
        }

        /// <summary>
        /// 眨眼协程
        /// </summary>
        private IEnumerator BlinkRoutine(float duration)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[BossEyeController] 👁️ 开始眨眼，持续 {duration}s");
            }
    
            // 快速睁眼
            yield return StartCoroutine(TransitionToState(BossEyeState.Open, transitionDuration));
    
            // 保持睁眼
            yield return new WaitForSeconds(duration);
    
            // 快速闭眼
            yield return StartCoroutine(TransitionToState(BossEyeState.Closed, transitionDuration));
    
            if (showDebugInfo)
            {
                Debug.Log("[BossEyeController] 👁️ 眨眼结束");
            }
        }
        /// <summary>
        /// 直接设置状态（无动画）
        /// </summary>
        public void SetStateDirect(BossEyeState state)
        {
            StopCurrentTransition();
            currentState = state;
            ApplyStateVisuals(state, 1f);
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossEyeController] 直接设置状态: {state}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 动画协程
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator TransitionToState(BossEyeState targetState, float duration)
        {
            BossEyeState startState = currentState;
            currentState = targetState == BossEyeState.Open ? BossEyeState.Opening : targetState;
            
            float elapsed = 0f;
            float startT = (startState == BossEyeState.Open) ? 1f : 0f;
            float endT = (targetState == BossEyeState.Open) ? 1f : 0f;
            
#if DOTWEEN
            // 使用 DOTween
            bool complete = false;
            
            if (eyeTransform != null)
            {
                Vector3 targetScale = GetEyeScale(endT);
                scaleTweener = eyeTransform.DOScale(targetScale, duration)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => complete = true);
            }
            
            if (eyeRenderer != null)
            {
                Color targetColor = Color.Lerp(closedColor, openColor, endT);
                colorTweener = eyeRenderer.DOColor(targetColor, duration).SetEase(Ease.OutQuad);
            }
            
            while (!complete && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
#else
            // 协程动画
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Lerp(startT, endT, elapsed / duration);
                ApplyStateVisuals(targetState, t);
                yield return null;
            }
#endif
            
            // 完成
            currentState = targetState;
            ApplyStateVisuals(targetState, endT);
            UpdateEffects(targetState == BossEyeState.Open);
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossEyeController] 状态过渡完成: {targetState}");
            }
        }
        
        /// <summary>
        /// 缓慢睁开动画（带戏剧性效果）
        /// </summary>
        private IEnumerator SlowOpenRoutine(float duration)
        {
            currentState = BossEyeState.Opening;
            
            float elapsed = 0f;
            
            if (showDebugInfo)
            {
                Debug.Log($"[BossEyeController] 🔴 开始缓慢睁眼... {duration}秒");
            }
            
#if DOTWEEN
            // DOTween 实现
            if (eyeTransform != null)
            {
                Vector3 targetScale = GetEyeScale(1f);
                scaleTweener = eyeTransform
                    .DOScale(targetScale, duration)
                    .SetEase(Ease.InOutSine); // 使用更戏剧性的缓动
            }
            
            if (eyeRenderer != null)
            {
                colorTweener = eyeRenderer
                    .DOColor(openColor, duration)
                    .SetEase(Ease.InOutSine);
            }
            
            // 等待动画完成
            yield return new WaitForSeconds(duration);
#else
            // 协程实现（带缓动）
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // 使用 SmoothStep 缓动，产生"慢-快-慢"的效果
                float rawT = elapsed / duration;
                float t = rawT * rawT * (3f - 2f * rawT); // SmoothStep
                
                ApplyStateVisuals(BossEyeState.Open, t);
                
                // 在一半进度时激活 Collider（可以开始造成伤害）
                if (rawT >= 0.5f && eyeCollider != null && !eyeCollider.enabled)
                {
                    eyeCollider.enabled = true;
                    
                    if (showDebugInfo)
                    {
                        Debug.Log("[BossEyeController] 👁️ 弱点碰撞器激活！");
                    }
                }
                
                yield return null;
            }
#endif
            
            // 完成
            currentState = BossEyeState.Open;
            ApplyStateVisuals(BossEyeState.Open, 1f);
            UpdateEffects(true);
            
            if (showDebugInfo)
            {
                Debug.Log("[BossEyeController] 👁️ 眼睛完全睁开！弱点暴露！");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 视觉效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ApplyStateVisuals(BossEyeState state, float t)
        {
            // t = 0 表示完全闭眼，t = 1 表示完全睁眼
            
            // 缩放动画
            if (eyeTransform != null)
            {
                eyeTransform.localScale = GetEyeScale(t);
            }
            
            // 颜色动画
            if (eyeRenderer != null)
            {
                eyeRenderer.color = Color.Lerp(closedColor, openColor, t);
            }
        }
        
        private Vector3 GetEyeScale(float t)
        {
            float scaleY = Mathf.Lerp(closedScaleY, originalEyeScale.y, t);
            return new Vector3(originalEyeScale.x, scaleY, originalEyeScale.z);
        }

        private void UpdateEffects(bool isOpen)
        {
            // 红色身体特效（怒目）
            if (redBodyEffect != null)
            {
                redBodyEffect.SetActive(isOpen);
            }
            
            // 瞳孔发光
            if (pupilGlowEffect != null)
            {
                pupilGlowEffect.SetActive(isOpen);
            }
        }
        
        private void StopCurrentTransition()
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }
            
#if DOTWEEN
            if (scaleTweener != null && scaleTweener.IsActive())
            {
                scaleTweener.Kill();
            }
            if (colorTweener != null && colorTweener.IsActive())
            {
                colorTweener.Kill();
            }
#endif
        }
    }
}