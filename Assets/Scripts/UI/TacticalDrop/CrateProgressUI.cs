// ============================================================
// CrateProgressUI.cs
// 文件位置: Assets/Scripts/UI/TacticalDrop/CrateProgressUI.cs
// 用途：宝箱圆环进度条UI（World Space Canvas）
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace LightVsDecay.UI.TacticalDrop
{
    /// <summary>
    /// 宝箱圆环进度条UI
    /// 功能：
    /// - 显示激光照射进度（0-100%）
    /// - 圆环填充动画
    /// - 中心百分比数字显示
    /// - 只在激光照射时显示
    /// </summary>
    public class CrateProgressUI : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 组件引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("组件引用")]
        [Tooltip("圆环填充 Image（FillImage，已设为 Radial 360）")]
        [SerializeField] private Image fillImage;

        [Tooltip("圆环背景 Image（Background）")]
        [SerializeField] private Image background;

        [Tooltip("百分比文本（FillText）")]
        [SerializeField] private TextMeshProUGUI fillText;

        [Tooltip("整个UI的 CanvasGroup（挂在 CrateProgressUI 节点上，用于淡入淡出）")]
        [SerializeField] private CanvasGroup canvasGroup;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("淡入时间")]
        [SerializeField] private float fadeInDuration = 0.2f;
        
        [Tooltip("淡出时间")]
        [SerializeField] private float fadeOutDuration = 0.3f;
        
        [Tooltip("隐藏延迟（停止照射后多久隐藏）")]
        [SerializeField] private float hideDelay = 0.5f;

        [Tooltip("进度条缩放动画强度")]
        [SerializeField] private float pulseScale = 1.1f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float currentProgress = 0f;
        private float targetProgress = 0f;
        private bool isVisible = false;
        private float lastUpdateTime = 0f;
        private Tween fadeTween;
        private Tween pulseTween;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 初始隐藏
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            
            // 初始化进度
            if (fillImage != null)
            {
                fillImage.fillAmount = 0f;
            }
    
            if (fillText != null)
            {
                fillText.text = "0%";
            }
        }
        
        private void Update()
        {
            // 平滑更新进度显示
            if (Mathf.Abs(currentProgress - targetProgress) > 0.001f)
            {
                currentProgress = Mathf.Lerp(currentProgress, targetProgress, Time.deltaTime * 10f);
                UpdateVisuals();
            }
            
            // 检查是否需要自动隐藏
            if (isVisible && Time.time - lastUpdateTime > hideDelay)
            {
                Hide();
            }
        }
        
        private void OnDestroy()
        {
            fadeTween?.Kill();
            pulseTween?.Kill();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置进度（0-1）
        /// </summary>
        /// <param name="progress">进度值（0-1，对应0%-100%）</param>
        public void SetProgress(float progress)
        {
            targetProgress = Mathf.Clamp01(progress);
            lastUpdateTime = Time.time;
            
            // 如果还没显示，则显示
            if (!isVisible)
            {
                Show();
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[CrateProgressUI] 设置进度: {progress:P0}");
            }
        }
        
        /// <summary>
        /// 显示进度条
        /// </summary>
        public void Show()
        {
            if (isVisible) return;
    
            isVisible = true;
            lastUpdateTime = Time.time;
    
            fadeTween?.Kill();
    
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                fadeTween = canvasGroup.DOFade(1f, fadeInDuration).SetEase(Ease.OutQuad);
            }
            else
            {
                gameObject.SetActive(true);
            }
    
            // 开始脉冲动画
            StartPulseAnimation();
    
            if (showDebugInfo)
            {
                Debug.Log("[CrateProgressUI] 显示进度条");
            }
        }
        
        /// <summary>
        /// 隐藏进度条
        /// </summary>
        public void Hide()
        {
            if (!isVisible) return;
    
            isVisible = false;
    
            fadeTween?.Kill();
            pulseTween?.Kill();
    
            if (canvasGroup != null)
            {
                fadeTween = canvasGroup.DOFade(0f, fadeOutDuration).SetEase(Ease.InQuad);
            }
            else
            {
                gameObject.SetActive(false);
            }
    
            if (showDebugInfo)
            {
                Debug.Log("[CrateProgressUI] 隐藏进度条");
            }
        }
        
        /// <summary>
        /// 立即隐藏（无动画）
        /// </summary>
        public void HideImmediate()
        {
            isVisible = false;
    
            fadeTween?.Kill();
            pulseTween?.Kill();
    
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// 重置进度
        /// </summary>
        public void ResetProgress()
        {
            currentProgress = 0f;
            targetProgress = 0f;
            UpdateVisuals();
            HideImmediate();
        }
        
        /// <summary>
        /// 播放完成动画
        /// </summary>
        public void PlayCompleteAnimation()
        {
            pulseTween?.Kill();
            
            // 放大后消失
            transform.DOScale(Vector3.one * 1.5f, 0.3f).SetEase(Ease.OutBack);
            
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(0f, 0.3f).SetDelay(0.1f);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内部方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 更新视觉显示
        /// </summary>
        private void UpdateVisuals()
        {
            // 更新圆环填充
            if (fillImage != null)
            {
                fillImage.fillAmount = currentProgress;
            }
    
            // 更新百分比文本
            if (fillText != null)
            {
                int percent = Mathf.RoundToInt(currentProgress * 100f);
                fillText.text = $"{percent}%";
            }
        }
        
        /// <summary>
        /// 开始脉冲动画
        /// </summary>
        private void StartPulseAnimation()
        {
            pulseTween?.Kill();
            
            transform.localScale = Vector3.one;
            pulseTween = transform.DOScale(Vector3.one * pulseScale, 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [ContextMenu("Test: Set 50%")]
        private void TestSet50()
        {
            SetProgress(0.5f);
        }
        
        [ContextMenu("Test: Set 100%")]
        private void TestSet100()
        {
            SetProgress(1f);
        }
        
        [ContextMenu("Test: Reset")]
        private void TestReset()
        {
            ResetProgress();
        }
#endif
    }
}
