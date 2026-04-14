using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LightVsDecay.Data.SO;

namespace LightVsDecay.UI.Tutorial
{
    /// <summary>
    /// 新手引导聚光灯遮罩
    /// 使用 SDF Shader 单图层挖孔，支持圆角、软边抗锯齿，孔洞内点击穿透。
    /// 对外接口：Show() / Hide() / SetMessage()
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TutorialSpotlightOverlay : MonoBehaviour, ICanvasRaycastFilter
    {
        [Header("遮罩配置")]
        [SerializeField] private Color maskColor = new Color(0f, 0f, 0f, 0.8f);
        [SerializeField] private float cornerRadius = 20f;
        [SerializeField] private Vector2 holePadding = new Vector2(24f, 24f);

        [Header("指引元素")]
        [SerializeField] private RectTransform ringRect;
        [SerializeField] private RectTransform fingerRect;
        [SerializeField] private Vector2 fingerOffset = new Vector2(42f, -42f);
        [SerializeField] private float ringPulseScale = 0.08f;
        [SerializeField] private float ringPulseSpeed = 2.6f;
        [SerializeField] private float fingerFloatDistance = 10f;
        [SerializeField] private float fingerFloatSpeed = 2f;

        [Header("文案")]
        [SerializeField] private RectTransform messageRoot;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Vector2 messageOffset = new Vector2(0f, 110f);

        [Header("调试模式（运行时调整孔洞参数）")]
        [Tooltip("勾选后遮罩始终可见，可在 Play 模式下实时调整以下参数，找到满意值后填入 TutorialStepConfigSO")]
        [SerializeField] private bool debugMode = false;
        [Tooltip("调试目标：拖入任意 UI 元素，孔洞会自动对齐到该元素")]
        [SerializeField] private RectTransform debugTarget;
        [Tooltip("调试边距（像素）→ 填入 TutorialStepConfigSO.holePadding")]
        [SerializeField] private Vector2 debugPadding = new Vector2(24f, 24f);
        [Tooltip("调试形状 → 填入 TutorialStepConfigSO.holeShape")]
        [SerializeField] private HoleShape debugShape = HoleShape.RoundedRect;
        [Tooltip("调试圆角半径（像素，仅 RoundedRect 时生效）→ 填入 TutorialStepConfigSO.cornerRadius\n0=直角矩形，调大→圆角矩形，超过孔洞短边一半→正圆（建议直接选 Circle）")]
        [SerializeField] private float debugCornerRadius = 20f;

        private RectTransform _rootRect;
        private Image _maskImage;
        private Material _holeMaskMaterial;

        private RectTransform _target;
        private bool _trackTarget;
        private Vector3 _ringBaseScale = Vector3.one;
        private Vector2 _fingerBasePos;

        // C# 端点击穿透检测：直接保存屏幕像素空间，避免归一化误差
        private Vector2 _holeScreenCenter;    // 屏幕像素坐标（与 WorldToScreenPoint 同空间）
        private Vector2 _holeScreenHalfSize;  // 屏幕像素半宽高

        // 运行时当前激活的挖孔参数（由 Show() 传入或由 Debug 覆盖）
        private HoleShape _activeShape       = HoleShape.RoundedRect;
        private float     _activeCornerRadius = 20f;

        private void Awake()
        {
            EnsureInitialized();
            // 非调试模式下保持隐藏；调试模式下保持可见，方便运行时调参
            if (!debugMode)
                gameObject.SetActive(false);
        }

        /// <summary>
        /// 懒初始化：支持 GameObject 在 Scene 中以 inactive 状态放置。
        /// Unity 对 inactive 的 GameObject 不调用 Awake，因此 Show() 里必须先调此方法。
        /// </summary>
        private void EnsureInitialized()
        {
            if (_rootRect != null) return;           // 已初始化过，直接跳过

            _rootRect  = transform as RectTransform;
            _maskImage = GetComponent<Image>();

            if (ringRect != null)
                _ringBaseScale = ringRect.localScale;

            InitializeMaterial();
        }

        private void InitializeMaterial()
        {
            Shader shader = Shader.Find("UI/HoleMask");
            if (shader == null)
            {
                Debug.LogError("[TutorialSpotlightOverlay] 找不到 Shader \"UI/HoleMask\"，请确认 UIHoleMask.shader 已导入到 Assets/Shaders/");
                return;
            }

            _holeMaskMaterial = new Material(shader);
            _holeMaskMaterial.SetColor("_Color", maskColor);
            _maskImage.material = _holeMaskMaterial;
        }

        private void Update()
        {
            if (debugMode)
            {
                // 调试模式：用 debugTarget / debugPadding / debugShape / debugCornerRadius 实时驱动孔洞
                EnsureInitialized();
                if (_rootRect != null && debugTarget != null)
                {
                    _target               = debugTarget;
                    holePadding           = debugPadding;
                    _activeShape          = debugShape;
                    _activeCornerRadius   = debugCornerRadius;
                    RefreshLayout();
                }
                UpdateAnimations();
                return;
            }

            if (_trackTarget && _target != null)
                RefreshLayout();

            UpdateAnimations();
        }

        public void Show(RectTransform target, string message = null, Vector2? padding = null,
            float cornerRadius = 20f, HoleShape holeShape = HoleShape.RoundedRect, bool trackTarget = true)
        {
            EnsureInitialized();    // GameObject 在 Scene 中为 inactive 时 Awake 未执行，需在此补初始化
            if (target == null || _rootRect == null)
                return;

            _target               = target;
            _activeShape          = holeShape;
            _activeCornerRadius   = cornerRadius;
            if (padding.HasValue)
                holePadding = padding.Value;
            _trackTarget = trackTarget;

            gameObject.SetActive(true);
            SetMessage(message);
            RefreshLayout();
        }

        public void Hide()
        {
            _target = null;
            _trackTarget = false;
            gameObject.SetActive(false);
        }

        public void SetMessage(string message)
        {
            if (messageRoot == null || messageText == null)
                return;

            bool hasMessage = !string.IsNullOrWhiteSpace(message);
            messageRoot.gameObject.SetActive(hasMessage);
            if (hasMessage)
                messageText.text = message;
        }

        private void RefreshLayout()
        {
            Rect hole = GetTargetRectInOverlay();
            UpdateShaderHole();

            // ring / finger / message 定位（本地坐标中心原点）
            Vector2 center = hole.center - _rootRect.rect.size * 0.5f;

            if (ringRect != null)
            {
                ringRect.anchoredPosition = center;
                ringRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, hole.width);
                ringRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, hole.height);
            }

            if (fingerRect != null)
            {
                _fingerBasePos = center + fingerOffset;
                fingerRect.anchoredPosition = _fingerBasePos;
            }

            if (messageRoot != null)
                messageRoot.anchoredPosition = center + messageOffset;
        }

        /// <summary>
        /// 通过目标 WorldCorners 直接换算屏幕坐标，更新 Shader 挖孔参数。
        /// 使用屏幕空间而非本地坐标，正确处理 Canvas Scaler 缩放。
        /// </summary>
        private void UpdateShaderHole()
        {
            if (_holeMaskMaterial == null || _target == null)
                return;

            Canvas canvas = _rootRect.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            Vector3[] worldCorners = new Vector3[4];
            _target.GetWorldCorners(worldCorners);

            Vector2 screenMin = new Vector2(float.MaxValue,  float.MaxValue);
            Vector2 screenMax = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < worldCorners.Length; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(uiCamera, worldCorners[i]);
                screenMin = Vector2.Min(screenMin, sp);
                screenMax = Vector2.Max(screenMax, sp);
            }

            screenMin -= holePadding;
            screenMax += holePadding;

            Vector2 screenCenter = (screenMin + screenMax) * 0.5f;
            Vector2 screenSize   = screenMax - screenMin;

            // 根据形状计算有效圆角半径（像素）
            float halfMin = Mathf.Min(screenSize.x, screenSize.y) * 0.5f;
            float effectiveRadius = _activeShape switch
            {
                HoleShape.Rect    => 0f,
                HoleShape.Circle  => halfMin,           // 短边半径 = 正圆
                _                 => Mathf.Min(_activeCornerRadius, halfMin), // RoundedRect，上限为短边半径
            };

            // C# 端点击检测：像素空间，与 IsRaycastLocationValid 的 sp 完全一致
            _holeScreenCenter   = screenCenter;
            _holeScreenHalfSize = screenSize * 0.5f;
            // 同步圆角到 cornerRadius 字段，让 IsPointInHole 使用
            cornerRadius = effectiveRadius;

            // Shader 端：直接传像素值（Shader 已换为像素坐标系，彻底解决椭圆问题）
            _holeMaskMaterial.SetVector("_HoleCenter",   new Vector4(screenCenter.x, screenCenter.y, 0f, 0f));
            _holeMaskMaterial.SetVector("_HoleSize",     new Vector4(screenSize.x,   screenSize.y,   0f, 0f));
            _holeMaskMaterial.SetFloat ("_CornerRadius", effectiveRadius);
        }

        // ── 点击穿透 ──────────────────────────────────────────────────────────

        /// <summary>
        /// ICanvasRaycastFilter 接口：在 GraphicRaycaster 射线检测阶段介入。
        /// 返回 false → 该点射线穿透（传给下层按钮）；返回 true → 遮罩拦截。
        /// 这是 Unity UI 实现"局部透明点击"的正确方式，而非在 OnPointerClick 里处理。
        /// </summary>
        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            // 孔洞内：穿透（返回 false）；孔洞外：拦截（返回 true）
            return !IsPointInHole(sp);
        }

        private bool IsPointInHole(Vector2 screenPoint)
        {
            // 直接在屏幕像素空间做 SDF，与 IsRaycastLocationValid 收到的 sp 坐标系完全一致
            // _holeScreenHalfSize 已包含 holePadding，cornerRadius 单位也是像素
            if (_holeScreenHalfSize == Vector2.zero) return false;
            Vector2 diff = screenPoint - _holeScreenCenter;
            return RoundedBoxSDF(diff, _holeScreenHalfSize, cornerRadius) < 0f;
        }

        private static float RoundedBoxSDF(Vector2 p, Vector2 halfSize, float r)
        {
            // 标准圆角矩形 SDF：负值 = 内部，0 = 边缘，正值 = 外部
            Vector2 d = new Vector2(
                Mathf.Max(Mathf.Abs(p.x) - halfSize.x + r, 0f),
                Mathf.Max(Mathf.Abs(p.y) - halfSize.y + r, 0f)
            );
            return d.magnitude - r;
        }

        // ── 动画 ──────────────────────────────────────────────────────────────

        private void UpdateAnimations()
        {
            float time = Time.unscaledTime;

            if (ringRect != null)
            {
                float scale = 1f + Mathf.Sin(time * ringPulseSpeed) * ringPulseScale;
                ringRect.localScale = _ringBaseScale * scale;
            }

            if (fingerRect != null)
            {
                float yOffset = Mathf.Sin(time * fingerFloatSpeed) * fingerFloatDistance;
                fingerRect.anchoredPosition = _fingerBasePos + new Vector2(0f, yOffset);
            }
        }

        // ── 获取本地坐标孔洞（用于 ring/finger/message 定位）─────────────────

        private Rect GetTargetRectInOverlay()
        {
            Vector3[] corners = new Vector3[4];
            _target.GetWorldCorners(corners);

            Canvas canvas = _rootRect.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            Vector2 min = new Vector2(float.MaxValue,  float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < corners.Length; i++)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rootRect,
                    RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]),
                    uiCamera,
                    out Vector2 localPoint);

                Vector2 overlayPoint = localPoint + _rootRect.rect.size * 0.5f;
                min = Vector2.Min(min, overlayPoint);
                max = Vector2.Max(max, overlayPoint);
            }

            min -= holePadding;
            max += holePadding;

            min = Vector2.Max(min, Vector2.zero);
            max = Vector2.Min(max, _rootRect.rect.size);

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void OnDestroy()
        {
            if (_holeMaskMaterial != null)
                Destroy(_holeMaskMaterial);
        }
    }
}
