// ============================================================
// DroneRewardText.cs
// 文件位置: Assets/Scripts/UI/TacticalDrop/DroneRewardText.cs
// 用途：无人机奖励飘字组件（专用动画：弹出→漂浮→消失）
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace LightVsDecay.UI.FloatingText.TacticalDrop
{
    /// <summary>
    /// 无人机奖励飘字组件
    /// 动画阶段：
    /// 1. 弹出 (0s-0.1s): 缩放 0% → 120%
    /// 2. 回弹 (0.1s-0.2s): 缩放 120% → 100%
    /// 3. 漂浮 (0.2s-0.8s): Y轴 +50像素，Ease-Out
    /// 4. 消失 (0.6s-0.8s): 透明度 100% → 0%
    /// </summary>
    public class DroneRewardText : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 组件引用（单行模式：补给/问号）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("单行模式组件")]
        [Tooltip("图标 Image")]
        [SerializeField] private Image iconImage;
        
        [Tooltip("文本 TMP")]
        [SerializeField] private TextMeshProUGUI textMesh;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 组件引用（双行模式：契约）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("双行模式组件（契约箱专用）")]
        [Tooltip("代价行图标")]
        [SerializeField] private Image costIconImage;
        
        [Tooltip("代价行文本")]
        [SerializeField] private TextMeshProUGUI costTextMesh;
        
        [Tooltip("收益行图标")]
        [SerializeField] private Image gainIconImage;
        
        [Tooltip("收益行文本")]
        [SerializeField] private TextMeshProUGUI gainTextMesh;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 通用组件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("通用组件")]
        [Tooltip("CanvasGroup（用于淡出）")]
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Tooltip("RectTransform")]
        [SerializeField] private RectTransform rectTransform;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 动画参数
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("动画参数")]
        [Tooltip("弹出时间")]
        [SerializeField] private float popDuration = 0.1f;
        
        [Tooltip("弹出缩放")]
        [SerializeField] private float popScale = 1.2f;
        
        [Tooltip("回弹时间")]
        [SerializeField] private float bounceDuration = 0.1f;

        [Tooltip("弹出后停留时间")]
        [SerializeField] private float holdDuration = 0.25f;
        
        [Tooltip("漂浮时间")]
        [SerializeField] private float floatDuration = 0.9f;
        
        [Tooltip("漂浮距离（像素）")]
        [SerializeField] private float floatDistance = 42f;
        
        [Tooltip("淡出开始时间（相对于漂浮开始）")]
        [SerializeField] private float fadeStartDelay = 0.2f;
        
        [Tooltip("淡出时间")]
        [SerializeField] private float fadeDuration = 0.45f;

        [Tooltip("上浮结束时的缩放")]
        [SerializeField] private float floatEndScale = 0.72f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private bool isPlaying = false;
        private Sequence animSequence;
        private Canvas parentCanvas;
        private Camera worldCamera;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public bool IsPlaying => isPlaying;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }
            
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }
        
        private void OnDestroy()
        {
            animSequence?.Kill();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口 - 单行模式（补给/问号）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放单行飘字（补给/问号无人机）
        /// </summary>
        /// <param name="worldPosition">世界坐标</param>
        /// <param name="icon">图标 Sprite</param>
        /// <param name="text">文本（如 "+100"）</param>
        /// <param name="textColor">文本颜色</param>
        /// <param name="completeCallback">完成回调</param>
        public void PlaySingle(
            Vector3 worldPosition,
            Sprite icon,
            string text,
            Color textColor,
            Canvas targetCanvas,
            Camera projectionCamera)
        {
            if (isPlaying) return;
            parentCanvas = targetCanvas;
            worldCamera = projectionCamera;
            
            // 隐藏双行组件
            SetDualRowActive(false);
            
            // 设置单行内容
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(true);
                iconImage.sprite = icon;
            }
            
            if (textMesh != null)
            {
                textMesh.gameObject.SetActive(true);
                textMesh.text = text;
                textMesh.color = textColor;
            }
            
            // 设置位置
            SetupPosition(worldPosition);
            
            // 播放动画
            PlayAnimation();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口 - 双行模式（契约）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放双行飘字（契约无人机）
        /// </summary>
        /// <param name="worldPosition">世界坐标</param>
        /// <param name="costIcon">代价图标</param>
        /// <param name="costText">代价文本（如 "HP -100"）</param>
        /// <param name="costColor">代价颜色（红色）</param>
        /// <param name="gainIcon">收益图标</param>
        /// <param name="gainText">收益文本（如 "ATK +10%"）</param>
        /// <param name="gainColor">收益颜色（绿色）</param>
        /// <param name="completeCallback">完成回调</param>
        public void PlayDual(
            Vector3 worldPosition,
            Sprite costIcon,
            string costText,
            Color costColor,
            Sprite gainIcon,
            string gainText,
            Color gainColor,
            Canvas targetCanvas,
            Camera projectionCamera)
        {
            if (isPlaying) return;
            parentCanvas = targetCanvas;
            worldCamera = projectionCamera;
            
            // 隐藏单行组件
            SetSingleRowActive(false);
            
            // 设置代价行
            if (costIconImage != null)
            {
                costIconImage.gameObject.SetActive(true);
                costIconImage.sprite = costIcon;
            }
            
            if (costTextMesh != null)
            {
                costTextMesh.gameObject.SetActive(true);
                costTextMesh.text = costText;
                costTextMesh.color = costColor;
            }
            
            // 设置收益行
            if (gainIconImage != null)
            {
                gainIconImage.gameObject.SetActive(true);
                gainIconImage.sprite = gainIcon;
            }
            
            if (gainTextMesh != null)
            {
                gainTextMesh.gameObject.SetActive(true);
                gainTextMesh.text = gainText;
                gainTextMesh.color = gainColor;
            }
            
            // 设置位置
            SetupPosition(worldPosition);
            
            // 播放动画
            PlayAnimation();
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置位置
        /// </summary>
        private void SetupPosition(Vector3 worldPosition)
        {
            Camera projectionCamera = worldCamera != null ? worldCamera : Camera.main;
            Vector3 screenPos = projectionCamera != null
                ? projectionCamera.WorldToScreenPoint(worldPosition)
                : worldPosition;

            if (parentCanvas == null)
            {
                rectTransform.position = screenPos;
                return;
            }

            RectTransform canvasRect = parentCanvas.transform as RectTransform;
            Camera uiCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : parentCanvas.worldCamera;

            if (canvasRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out Vector2 localPoint))
            {
                rectTransform.anchoredPosition = localPoint;
                return;
            }

            rectTransform.position = screenPos;
        }
        
        /// <summary>
        /// 播放动画序列
        /// </summary>
        private void PlayAnimation()
        {
            isPlaying = true;
            gameObject.SetActive(true);
            
            // 初始状态
            rectTransform.localScale = Vector3.zero;
            canvasGroup.alpha = 1f;
            
            Vector3 startPos = rectTransform.position;
            float floatStartTime = popDuration + bounceDuration + holdDuration;
            
            // 创建动画序列
            animSequence?.Kill();
            animSequence = DOTween.Sequence();
            
            // 阶段1：弹出 (0s -> 0.1s) - 缩放 0% → 120%
            animSequence.Append(
                rectTransform.DOScale(popScale, popDuration)
                    .SetEase(Ease.OutBack)
            );
            
            // 阶段2：回弹 (0.1s -> 0.2s) - 缩放 120% → 100%
            animSequence.Append(
                rectTransform.DOScale(1f, bounceDuration)
                    .SetEase(Ease.OutQuad)
            );

            animSequence.AppendInterval(holdDuration);
            
            // 阶段3：漂浮 (0.2s -> 0.8s) - Y轴 +50像素
            animSequence.Append(
                rectTransform.DOMoveY(startPos.y + floatDistance, floatDuration)
                    .SetEase(Ease.OutQuad)
            );

            animSequence.Insert(
                floatStartTime,
                rectTransform.DOScale(floatEndScale, floatDuration)
                    .SetEase(Ease.InQuad)
            );
            
            // 阶段4：消失 (0.6s -> 0.8s) - 在漂浮期间淡出
            // 淡出从漂浮开始后 fadeStartDelay 秒启动
            animSequence.Insert(
                floatStartTime + fadeStartDelay,
                canvasGroup.DOFade(0f, fadeDuration)
                    .SetEase(Ease.InQuad)
            );
            
            // 完成回调
            animSequence.OnComplete(Complete);
        }
        
        /// <summary>
        /// 动画完成，自动销毁
        /// </summary>
        private void Complete()
        {
            isPlaying = false;
            Destroy(gameObject);
        }
        
        /// <summary>
        /// 设置单行组件显示/隐藏
        /// </summary>
        private void SetSingleRowActive(bool active)
        {
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(active);
            }
            if (textMesh != null)
            {
                textMesh.gameObject.SetActive(active);
            }
        }
        
        /// <summary>
        /// 设置双行组件显示/隐藏
        /// </summary>
        private void SetDualRowActive(bool active)
        {
            if (costIconImage != null)
            {
                costIconImage.gameObject.SetActive(active);
            }
            if (costTextMesh != null)
            {
                costTextMesh.gameObject.SetActive(active);
            }
            if (gainIconImage != null)
            {
                gainIconImage.gameObject.SetActive(active);
            }
            if (gainTextMesh != null)
            {
                gainTextMesh.gameObject.SetActive(active);
            }
        }
    }
}
