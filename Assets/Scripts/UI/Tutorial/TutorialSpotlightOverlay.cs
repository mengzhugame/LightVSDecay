using TMPro;
using UnityEngine;

namespace LightVsDecay.UI.Tutorial
{
    public class TutorialSpotlightOverlay : MonoBehaviour
    {
        [Header("遮罩块")]
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private RectTransform topBlock;
        [SerializeField] private RectTransform bottomBlock;
        [SerializeField] private RectTransform leftBlock;
        [SerializeField] private RectTransform rightBlock;

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

        private RectTransform _target;
        private Vector2 _padding = new Vector2(24f, 24f);
        private bool _trackTarget;
        private Vector3 _ringBaseScale = Vector3.one;
        private Vector2 _fingerBasePos;

        private void Awake()
        {
            if (rootRect == null)
            {
                rootRect = transform as RectTransform;
            }

            if (ringRect != null)
            {
                _ringBaseScale = ringRect.localScale;
            }

            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_trackTarget && _target != null)
            {
                RefreshLayout();
            }

            UpdateAnimations();
        }

        public void Show(RectTransform target, string message = null, Vector2? padding = null, bool trackTarget = true)
        {
            if (target == null || rootRect == null)
            {
                return;
            }

            _target = target;
            _padding = padding ?? _padding;
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
            {
                return;
            }

            bool hasMessage = !string.IsNullOrWhiteSpace(message);
            messageRoot.gameObject.SetActive(hasMessage);
            if (hasMessage)
            {
                messageText.text = message;
            }
        }

        private void RefreshLayout()
        {
            Rect hole = GetTargetRectInOverlay();

            SetStretch(topBlock, 0f, hole.yMax, rootRect.rect.width, rootRect.rect.height - hole.yMax);
            SetStretch(bottomBlock, 0f, 0f, rootRect.rect.width, hole.yMin);
            SetStretch(leftBlock, 0f, hole.yMin, hole.xMin, hole.height);
            SetStretch(rightBlock, hole.xMax, hole.yMin, rootRect.rect.width - hole.xMax, hole.height);

            Vector2 center = hole.center - (Vector2)rootRect.rect.size * 0.5f;

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
            {
                messageRoot.anchoredPosition = center + messageOffset;
            }
        }

        private Rect GetTargetRectInOverlay()
        {
            Vector3[] corners = new Vector3[4];
            _target.GetWorldCorners(corners);

            Canvas canvas = rootRect.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < corners.Length; i++)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rootRect,
                    RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]),
                    uiCamera,
                    out Vector2 localPoint);

                Vector2 overlayPoint = localPoint + rootRect.rect.size * 0.5f;
                min = Vector2.Min(min, overlayPoint);
                max = Vector2.Max(max, overlayPoint);
            }

            min -= _padding;
            max += _padding;

            min = Vector2.Max(min, Vector2.zero);
            max = Vector2.Min(max, rootRect.rect.size);

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void SetStretch(RectTransform rect, float left, float bottom, float width, float height)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(left, bottom);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, width));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, height));
        }

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
    }
}
