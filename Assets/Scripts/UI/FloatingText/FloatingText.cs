// ============================================================
// FloatingText.cs
// 文件位置: Assets/Scripts/UI/FloatingText/FloatingText.cs
// 用途：单个飘字组件，控制动画和自动回收
// ============================================================

using UnityEngine;
using TMPro;

namespace LightVsDecay.UI.FloatingText
{
    /// <summary>
    /// 单个飘字组件
    /// 挂载在飘字预制体上
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class FloatingText : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 组件引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private TextMeshProUGUI textMesh;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private FloatingTextType currentType;
        private FloatingTextStyle currentStyle;
        private int priority;
        
        private float elapsedTime;
        private float duration;
        private bool isPlaying;
        
        // 运动参数
        private Vector2 velocity;
        private float gravity;
        private float fadeStartPercent;
        
        // 缩放参数
        private bool useScaleAnimation;
        private float initialScale;
        private float peakScale;
        private float scalePeakPercent;
        
        // 回调
        private System.Action<FloatingText> onComplete;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前飘字类型</summary>
        public FloatingTextType CurrentType => currentType;
        
        /// <summary>优先级（越低越容易被回收）</summary>
        public int Priority => priority;
        
        /// <summary>是否正在播放</summary>
        public bool IsPlaying => isPlaying;
        
        /// <summary>剩余时间百分比</summary>
        public float RemainingPercent => isPlaying ? 1f - (elapsedTime / duration) : 0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            textMesh = GetComponent<TextMeshProUGUI>();
            rectTransform = GetComponent<RectTransform>();
            
            // 确保有 CanvasGroup（用于淡出）
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        private void Update()
        {
            if (!isPlaying) return;
            
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            
            if (t >= 1f)
            {
                Complete();
                return;
            }
            
            UpdatePosition();
            UpdateAlpha(t);
            UpdateScale(t);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 初始化并播放飘字
        /// </summary>
        /// <param name="text">显示文本</param>
        /// <param name="worldPosition">世界坐标位置</param>
        /// <param name="type">飘字类型</param>
        /// <param name="style">样式配置</param>
        /// <param name="typePriority">优先级</param>
        /// <param name="completeCallback">完成回调</param>
        public void Play(
            string text, 
            Vector3 worldPosition, 
            FloatingTextType type,
            FloatingTextStyle style,
            int typePriority,
            System.Action<FloatingText> completeCallback)
        {
            currentType = type;
            currentStyle = style;
            priority = typePriority;
            onComplete = completeCallback;
            
            // 设置文本
            SetupText(text, style);
            
            // 设置位置（世界坐标转屏幕坐标）
            SetupPosition(worldPosition);
            
            // 设置动画参数
            SetupAnimation(style);
            
            // 开始播放
            isPlaying = true;
            elapsedTime = 0f;
            
            gameObject.SetActive(true);
        }
        
        /// <summary>
        /// 强制停止并回收
        /// </summary>
        public void ForceStop()
        {
            Complete();
        }
        
        /// <summary>
        /// 重置状态（对象池回收时调用）
        /// </summary>
        public void Reset()
        {
            isPlaying = false;
            elapsedTime = 0f;
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
            
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one;
            }
            
            gameObject.SetActive(false);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法 - 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void SetupText(string text, FloatingTextStyle style)
        {
            if (textMesh == null) return;
            
            textMesh.text = text;
            textMesh.color = style.textColor;
            textMesh.fontSize = style.fontSize;
            
            // 字体样式
            textMesh.fontStyle = style.isBold ? FontStyles.Bold : FontStyles.Normal;
            
            // 描边
            textMesh.outlineColor = style.outlineColor;
            textMesh.outlineWidth = style.outlineWidth;
        }
        
        private void SetupPosition(Vector3 worldPosition)
        {
            // 世界坐标转屏幕坐标
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
            rectTransform.position = screenPos;
        }
        
        private void SetupAnimation(FloatingTextStyle style)
        {
            duration = style.duration;
            gravity = style.gravity;
            fadeStartPercent = style.fadeStartPercent;
            
            // 随机初始速度
            float horizontalSpeed = Random.Range(-style.horizontalRandomRange, style.horizontalRandomRange);
            velocity = new Vector2(horizontalSpeed, style.initialUpSpeed);
            
            // 缩放参数
            useScaleAnimation = style.useScaleAnimation;
            initialScale = style.initialScale;
            peakScale = style.peakScale;
            scalePeakPercent = style.scalePeakPercent;
            
            // 设置初始缩放
            if (useScaleAnimation)
            {
                rectTransform.localScale = Vector3.one * initialScale;
            }
            else
            {
                rectTransform.localScale = Vector3.one;
            }
            
            // 重置透明度
            canvasGroup.alpha = 1f;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法 - 更新动画
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdatePosition()
        {
            // 应用重力
            velocity.y -= gravity * Time.deltaTime;
            
            // 更新位置
            rectTransform.anchoredPosition += velocity * Time.deltaTime;
        }
        
        private void UpdateAlpha(float t)
        {
            if (t < fadeStartPercent)
            {
                canvasGroup.alpha = 1f;
            }
            else
            {
                // 淡出
                float fadeProgress = (t - fadeStartPercent) / (1f - fadeStartPercent);
                canvasGroup.alpha = 1f - fadeProgress;
            }
        }
        
        private void UpdateScale(float t)
        {
            if (!useScaleAnimation) return;
            
            float scale;
            
            if (t < scalePeakPercent)
            {
                // 放大阶段
                float scaleUpProgress = t / scalePeakPercent;
                scale = Mathf.Lerp(initialScale, peakScale, EaseOutBack(scaleUpProgress));
            }
            else
            {
                // 缩小阶段
                float scaleDownProgress = (t - scalePeakPercent) / (1f - scalePeakPercent);
                scale = Mathf.Lerp(peakScale, 1f, scaleDownProgress);
            }
            
            rectTransform.localScale = Vector3.one * scale;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 完成处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Complete()
        {
            isPlaying = false;
            gameObject.SetActive(false);
            
            onComplete?.Invoke(this);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 缓动函数
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// EaseOutBack 缓动（超出后回弹效果）
        /// </summary>
        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}