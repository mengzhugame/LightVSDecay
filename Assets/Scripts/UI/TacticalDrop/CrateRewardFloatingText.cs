// ============================================================
// CrateRewardFloatingText.cs
// 文件位置: Assets/Scripts/UI/TacticalDrop/CrateRewardFloatingText.cs
// 用途：宝箱奖励飘字（飞向光棱塔效果）
// ============================================================

using UnityEngine;
using TMPro;
using DG.Tweening;

namespace LightVsDecay.UI.TacticalDrop
{
    /// <summary>
    /// 宝箱奖励飘字
    /// 特效：
    /// - 从宝箱位置弹出
    /// - 放大/缩小动画
    /// - 飞向光棱塔后消失
    /// - 大奖有特殊金色闪烁效果
    /// </summary>
    public class CrateRewardFloatingText : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 组件引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("组件引用")]
        [Tooltip("文本组件")]
        [SerializeField] private TextMeshPro textMesh;
        
        [Tooltip("发光/外描边（可选）")]
        [SerializeField] private SpriteRenderer glowSprite;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("动画配置")]
        [Tooltip("弹出持续时间")]
        [SerializeField] private float popDuration = 0.3f;
        
        [Tooltip("悬停持续时间")]
        [SerializeField] private float hoverDuration = 0.5f;
        
        [Tooltip("飞行持续时间")]
        [SerializeField] private float flyDuration = 0.8f;
        
        [Tooltip("弹出高度")]
        [SerializeField] private float popHeight = 1f;
        
        [Tooltip("大奖缩放")]
        [SerializeField] private float jackpotScale = 1.5f;
        
        [Tooltip("普通缩放")]
        [SerializeField] private float normalScale = 1f;
        
        [Header("颜色配置")]
        [Tooltip("大奖闪烁颜色1")]
        [SerializeField] private Color jackpotColor1 = new Color(1f, 0.84f, 0f); // 金色
        
        [Tooltip("大奖闪烁颜色2")]
        [SerializeField] private Color jackpotColor2 = new Color(1f, 1f, 0.5f); // 浅金色
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private bool isPlaying = false;
        private Sequence animSequence;
        private System.Action onCompleteCallback;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            if (textMesh == null)
            {
                textMesh = GetComponentInChildren<TextMeshPro>();
            }
        }
        
        private void OnDestroy()
        {
            animSequence?.Kill();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放奖励飘字动画
        /// </summary>
        /// <param name="text">显示文本</param>
        /// <param name="color">文字颜色</param>
        /// <param name="isJackpot">是否大奖</param>
        /// <param name="targetPos">飞行目标位置</param>
        /// <param name="onComplete">完成回调</param>
        public void Play(string text, Color color, bool isJackpot, Vector3 targetPos, System.Action onComplete = null)
        {
            if (isPlaying) return;
            
            isPlaying = true;
            onCompleteCallback = onComplete;
            
            // 设置文本
            if (textMesh != null)
            {
                textMesh.text = text;
                textMesh.color = color;
            }
            
            // 初始状态
            transform.localScale = Vector3.zero;
            
            // 创建动画序列
            animSequence?.Kill();
            animSequence = DOTween.Sequence();
            
            Vector3 startPos = transform.position;
            Vector3 hoverPos = startPos + Vector3.up * popHeight;
            float scale = isJackpot ? jackpotScale : normalScale;
            
            // 阶段1：弹出
            animSequence.Append(transform.DOScale(Vector3.one * scale, popDuration).SetEase(Ease.OutBack));
            animSequence.Join(transform.DOMoveY(hoverPos.y, popDuration).SetEase(Ease.OutQuad));
            
            // 大奖特效：颜色闪烁
            if (isJackpot && textMesh != null)
            {
                animSequence.Join(
                    textMesh.DOColor(jackpotColor2, popDuration * 0.5f)
                        .SetLoops(4, LoopType.Yoyo)
                );
            }
            
            // 阶段2：悬停
            animSequence.AppendInterval(hoverDuration);
            
            // 阶段3：飞向目标
            animSequence.Append(transform.DOMove(targetPos, flyDuration).SetEase(Ease.InQuad));
            animSequence.Join(transform.DOScale(Vector3.zero, flyDuration).SetEase(Ease.InQuad));
            
            // 完成回调
            animSequence.OnComplete(() =>
            {
                isPlaying = false;
                onCompleteCallback?.Invoke();
                Destroy(gameObject);
            });
        }
        
        /// <summary>
        /// 播放简单向上飘出动画（用于嘲讽文字）
        /// </summary>
        public void PlaySimple(string text, Color color, System.Action onComplete = null)
        {
            if (isPlaying) return;
            
            isPlaying = true;
            onCompleteCallback = onComplete;
            
            // 设置文本
            if (textMesh != null)
            {
                textMesh.text = text;
                textMesh.color = color;
            }
            
            // 初始状态
            transform.localScale = Vector3.one * normalScale;
            
            // 打字机效果
            if (textMesh != null)
            {
                textMesh.maxVisibleCharacters = 0;
                DOTween.To(
                    () => textMesh.maxVisibleCharacters,
                    x => textMesh.maxVisibleCharacters = x,
                    text.Length,
                    text.Length * 0.05f
                );
            }
            
            // 创建动画序列
            animSequence?.Kill();
            animSequence = DOTween.Sequence();
            
            float totalDuration = popDuration + hoverDuration + flyDuration;
            
            // 向上飘动并淡出
            animSequence.Append(transform.DOMoveY(transform.position.y + 2f, totalDuration).SetEase(Ease.OutQuad));
            animSequence.Join(textMesh.DOFade(0f, totalDuration * 0.5f).SetDelay(totalDuration * 0.5f));
            
            // 完成回调
            animSequence.OnComplete(() =>
            {
                isPlaying = false;
                onCompleteCallback?.Invoke();
                Destroy(gameObject);
            });
        }
        
        /// <summary>
        /// 立即停止并销毁
        /// </summary>
        public void Stop()
        {
            animSequence?.Kill();
            Destroy(gameObject);
        }
    }
}
