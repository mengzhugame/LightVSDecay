// ============================================================
// TipsPanelController.cs
// 文件位置: Assets/Scripts/UI/TipsPanelController.cs
//
// 用途：主界面通用提示弹窗（科技树解锁、装备解锁、章节解锁等）
//       支持队列依次播放多条提示，每条播放完自动播下一条。
//
// UI 层级（在 MainScene Canvas 下绑定）：
//   TipsPanal  (RectTransform + CanvasGroup + TipsPanelController)
//   └── Text(TMP)  (TextMeshProUGUI)
//
// 动画设计："光脉冲飘入" (Light-Pulse Float-In)
//   Phase 1 [ENTER  0.45s] — 从屏幕上方滑入 (Y+slideInY → Y+0)
//                            + alpha 快速淡入
//                            缓动函数：EaseOutBack（有弹性回弹感）
//   Phase 2 [PUNCH  0.25s] — 文字 Scale 从 1.3 弹回 1.0
//                            缓动函数：EaseOutBack
//   Phase 3 [FLOAT  hold] — 文字做正弦上下微幅飘浮（赛博波纹感）
//   Phase 4 [EXIT   0.30s] — 整体上飘 + alpha 淡出
//                            缓动函数：EaseInQuad
//
// 使用方式：
//   TipsPanelController.Instance.ShowTip("🔓 恭喜解锁科技树！");
//   TipsPanelController.Instance.ShowTips(new[]{ "提示A", "提示B" });
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace LightVsDecay.UI
{
    public class TipsPanelController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例（非持久，仅 MainScene 存活）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public static TipsPanelController Instance { get; private set; }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 组件引用 ═══")]
        [Tooltip("面板的 RectTransform（通常是本 GameObject 本身）")]
        [SerializeField] private RectTransform    panelRect;

        [Tooltip("面板的 CanvasGroup（控制整体透明度）")]
        [SerializeField] private CanvasGroup      canvasGroup;

        [Tooltip("文字 TextMeshPro 组件")]
        [SerializeField] private TextMeshProUGUI  tipsText;

        [Tooltip("文字 RectTransform（用于 Scale punch 和飘浮）")]
        [SerializeField] private RectTransform    textRect;

        [Header("═══ 入场动画参数 ═══")]
        [Tooltip("面板从上方滑入的起始偏移像素")]
        [SerializeField] private float slideInY      = 150f;

        [Tooltip("入场动画时长（秒）")]
        [SerializeField] private float enterDuration = 0.45f;

        [Header("═══ 文字弹出参数 ═══")]
        [Tooltip("文字弹出动画在入场后的延迟（秒）")]
        [SerializeField] private float punchDelay    = 0.05f;

        [Tooltip("文字弹出动画时长（秒）")]
        [SerializeField] private float punchDuration = 0.25f;

        [Tooltip("文字弹出起始缩放比例（>1 为放大后弹回）")]
        [SerializeField] private float punchScale    = 1.35f;

        [Header("═══ 停留 & 飘浮参数 ═══")]
        [Tooltip("提示显示的停留时长（秒）")]
        [SerializeField] private float holdDuration  = 2.5f;

        [Tooltip("文字飘浮幅度（像素，上下各飘动的距离）")]
        [SerializeField] private float floatAmplitude = 5f;

        [Tooltip("飘浮频率（越大越快）")]
        [SerializeField] private float floatSpeed    = 1.8f;

        [Header("═══ 退出动画参数 ═══")]
        [Tooltip("退出时向上飘动的像素距离")]
        [SerializeField] private float slideOutY     = 70f;

        [Tooltip("退出动画时长（秒）")]
        [SerializeField] private float exitDuration  = 0.3f;

        [Header("═══ 多条提示间隔 ═══")]
        [Tooltip("连续两条提示之间的间隔时间（秒）")]
        [SerializeField] private float tipInterval   = 0.2f;

        [Header("═══ 文字颜色 ═══")]
        [Tooltip("提示文字颜色（赛博风默认：青蓝）")]
        [SerializeField] private Color textColor     = new Color(0.35f, 0.95f, 0.85f, 1f);

        [Header("═══ 调试 ═══")]
        [SerializeField] private bool showDebugInfo  = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private readonly Queue<string> _queue       = new Queue<string>();
        private bool                   _isPlaying   = false;
        private Vector2                _panelOrigin;   // 面板在场景中的初始锚点位置
        private Vector2                _textOrigin;    // 文字的初始锚点位置

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (panelRect   == null) panelRect   = GetComponent<RectTransform>();
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            if (tipsText == null) tipsText = GetComponentInChildren<TextMeshProUGUI>(true);
            if (textRect == null && tipsText != null) textRect = tipsText.rectTransform;

            if (panelRect != null) _panelOrigin = panelRect.anchoredPosition;
            if (textRect  != null) _textOrigin  = textRect.anchoredPosition;

            // ★ 不再 SetActive(false)！改用 CanvasGroup 完全透明+不响应输入
            //   GameObject 保持激活，协程才能正常启动
            canvasGroup.alpha          = 0f;
            canvasGroup.interactable   = false;
            canvasGroup.blocksRaycasts = false;
            // gameObject.SetActive(false);  ← 删除这行
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 显示单条提示（加入队列，自动依次播放）
        /// </summary>
        public void ShowTip(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
    
            gameObject.SetActive(true);   // ★ 先激活，协程才能跑
            _queue.Enqueue(message);
            if (!_isPlaying) StartCoroutine(PlayQueueRoutine());
        }

        /// <summary>
        /// 同时显示多条提示（加入队列，依次播放）
        /// </summary>
        public void ShowTips(IEnumerable<string> messages)
        {
            bool hasAny = false;
            foreach (var m in messages)
                if (!string.IsNullOrEmpty(m)) { _queue.Enqueue(m); hasAny = true; }

            if (!hasAny) return;
    
            gameObject.SetActive(true);   // ★ 先激活
            if (!_isPlaying) StartCoroutine(PlayQueueRoutine());
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 队列调度协程
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator PlayQueueRoutine()
        {
            _isPlaying = true;

            while (_queue.Count > 0)
            {
                string msg = _queue.Dequeue();

                if (showDebugInfo)
                    Debug.Log($"[TipsPanelController] 播放提示: \"{msg}\"");

                yield return StartCoroutine(PlayOneTipRoutine(msg));

                if (_queue.Count > 0)
                    yield return new WaitForSeconds(tipInterval);
            }

            _isPlaying = false;
            gameObject.SetActive(false);   // ★ 所有提示播完后自动隐藏
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单条提示完整动画
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator PlayOneTipRoutine(string message)
        {
            if (tipsText != null)
            {
                tipsText.text  = message;
                tipsText.color = textColor;
            }
            if (textRect != null)
            {
                textRect.localScale       = Vector3.one;
                textRect.anchoredPosition = _textOrigin;
            }

            // ★ 不用 SetActive，直接用 CanvasGroup 显示
            canvasGroup.interactable   = true;
            canvasGroup.blocksRaycasts = true;

            yield return StartCoroutine(SlideInRoutine());
            yield return StartCoroutine(TextPunchRoutine());
            yield return StartCoroutine(FloatHoldRoutine());
            yield return StartCoroutine(SlideOutRoutine());

            // 收尾：隐藏
            canvasGroup.alpha          = 0f;
            canvasGroup.interactable   = false;
            canvasGroup.blocksRaycasts = false;

            if (panelRect != null) panelRect.anchoredPosition = _panelOrigin;
            if (textRect  != null)
            {
                textRect.anchoredPosition = _textOrigin;
                textRect.localScale       = Vector3.one;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Phase 1 — 滑入
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator SlideInRoutine()
        {
            if (panelRect == null) yield break;

            // 起始位置：面板在当前位置上方 slideInY 像素
            Vector2 startPos = _panelOrigin + new Vector2(0f, slideInY);
            Vector2 endPos   = _panelOrigin;

            panelRect.anchoredPosition = startPos;
            canvasGroup.alpha          = 0f;

            float elapsed = 0f;
            while (elapsed < enterDuration)
            {
                elapsed           += Time.deltaTime;
                float t            = Mathf.Clamp01(elapsed / enterDuration);
                float eased        = EaseOutBack(t);

                panelRect.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eased);
                // 前 30% 快速淡入，然后保持全亮
                canvasGroup.alpha          = Mathf.Clamp01(t / 0.3f);
                yield return null;
            }

            panelRect.anchoredPosition = endPos;
            canvasGroup.alpha          = 1f;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Phase 2 — 文字 Scale Punch
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator TextPunchRoutine()
        {
            if (textRect == null) yield break;

            yield return new WaitForSeconds(punchDelay);

            textRect.localScale = Vector3.one * punchScale;

            float elapsed = 0f;
            while (elapsed < punchDuration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / punchDuration);
                // punchScale → 1.0，EaseOutBack 保证有轻微过冲再回弹
                float scale = Mathf.LerpUnclamped(punchScale, 1.0f, EaseOutBack(t));
                textRect.localScale = Vector3.one * Mathf.Max(0.01f, scale);
                yield return null;
            }

            textRect.localScale = Vector3.one;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Phase 3 — 文字正弦飘浮停留
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator FloatHoldRoutine()
        {
            float elapsed = 0f;

            while (elapsed < holdDuration)
            {
                elapsed += Time.deltaTime;

                if (textRect != null)
                {
                    float yOffset = Mathf.Sin(elapsed * floatSpeed * Mathf.PI) * floatAmplitude;
                    textRect.anchoredPosition = _textOrigin + new Vector2(0f, yOffset);
                }

                yield return null;
            }

            // 还原位置
            if (textRect != null) textRect.anchoredPosition = _textOrigin;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Phase 4 — 滑出 + 淡出
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator SlideOutRoutine()
        {
            if (panelRect == null) yield break;

            Vector2 startPos = panelRect.anchoredPosition;
            Vector2 endPos   = startPos + new Vector2(0f, slideOutY);

            float elapsed = 0f;
            while (elapsed < exitDuration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / exitDuration);

                // EaseInQuad：越来越快地飘走
                float eased = t * t;

                panelRect.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
                canvasGroup.alpha          = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            panelRect.anchoredPosition = startPos;   // 复位（Awake 的 Reset 也会做一次）
            canvasGroup.alpha          = 0f;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 缓动函数
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// EaseOutBack: 到达终点时略微过冲，再弹回——
        /// 产生"啪"一声弹入的弹性感，适合 UI 入场
        /// </summary>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

#if UNITY_EDITOR
        [ContextMenu("Test: 科技树解锁提示")]
        private void TestTechTree()   => ShowTip("🔓 科技树系统已解锁！快去升级分支能力吧！");

        [ContextMenu("Test: 装备系统提示")]
        private void TestEquipment()  => ShowTip("🔓 装备合成系统已解锁！打造属于你的装备！");

        [ContextMenu("Test: 连续两条提示")]
        private void TestMultiple()
        {
            ShowTips(new[]
            {
                "🔓 科技树系统已解锁！",
                "🔓 装备合成系统已解锁！"
            });
        }
#endif
    }
}