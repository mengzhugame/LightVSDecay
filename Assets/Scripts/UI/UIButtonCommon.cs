using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LightVsDecay.Audio;

namespace LightVsDecay.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class UIButtonCommon : MonoBehaviour, IPointerDownHandler, ISubmitHandler
    {
        [Header("Disabled Visuals")]
        [SerializeField] private bool grayOutAllChildGraphics = true;
        [SerializeField] private float disabledBrightness = 0.7f;

        [Header("Pop Animation")]
        [SerializeField] private bool enablePopAnimation = true;
        [SerializeField] private float popScale = 1.08f;
        [SerializeField] private float popDuration = 0.18f;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Click Sound")]
        [SerializeField] private bool enableClickSfx = true;

        private Button _button;
        private RectTransform _rectTransform;
        private readonly List<Graphic> _graphics = new List<Graphic>();
        private readonly List<Color> _enabledColors = new List<Color>();
        private Coroutine _popCoroutine;
        private bool _disabledApplied;
        private bool _targetsDirty;
        private Vector3 _baseScale = Vector3.one;

        public bool EnablePopAnimation
        {
            get => enablePopAnimation;
            set => enablePopAnimation = value;
        }

        public bool GrayOutAllChildGraphics
        {
            get => grayOutAllChildGraphics;
            set
            {
                grayOutAllChildGraphics = value;
                SyncVisualState();
            }
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            _rectTransform = transform as RectTransform;
            if (_rectTransform != null)
            {
                _baseScale = _rectTransform.localScale;
            }
            RebuildTargets();
            CaptureEnabledColors();
            SyncVisualState();
        }

        private void OnEnable()
        {
            RegisterButtonSound();
            ResetScale();
            SyncVisualState();
        }

        private void OnDisable()
        {
            UnregisterButtonSound();

            if (_popCoroutine != null)
            {
                StopCoroutine(_popCoroutine);
                _popCoroutine = null;
            }

            ResetScale();
        }

        private void LateUpdate()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_button == null)
            {
                return;
            }

            if (_targetsDirty)
            {
                RebuildTargets();
                _targetsDirty = false;
                if (_button.interactable)
                {
                    CaptureEnabledColors();
                }
            }

            if (!_button.interactable)
            {
                if (!_disabledApplied)
                {
                    ApplyDisabledColors();
                }
                return;
            }

            if (_disabledApplied)
            {
                ApplyEnabledColors();
                _disabledApplied = false;
            }

            CaptureEnabledColors();
        }

        private void OnTransformChildrenChanged()
        {
            _targetsDirty = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            PlayPopIfNeeded();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            PlayPopIfNeeded();
        }

        public void SyncVisualState()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_button == null)
            {
                return;
            }

            if (_targetsDirty)
            {
                RebuildTargets();
                _targetsDirty = false;
            }

            if (_button.interactable)
            {
                ApplyEnabledColors();
                _disabledApplied = false;
                CaptureEnabledColors();
            }
            else
            {
                StopPopAnimation();
                ResetScale();
                ApplyDisabledColors();
            }
        }

        public void RefreshTargets()
        {
            RebuildTargets();
            SyncVisualState();
        }

        public void ResetVisualState()
        {
            StopPopAnimation();
            ResetScale();
            SyncVisualState();
        }

        private void PlayPopIfNeeded()
        {
            if (!enablePopAnimation || _button == null || !_button.interactable || _rectTransform == null)
            {
                return;
            }

            StopPopAnimation();
            ResetScale();

            _popCoroutine = StartCoroutine(PlayPopRoutine());
        }

        private IEnumerator PlayPopRoutine()
        {
            yield return UIAnimationHelper.PlayScalePunch(_rectTransform, popScale, popDuration, useUnscaledTime);
            _popCoroutine = null;
        }

        private void ResetScale()
        {
            if (_rectTransform != null)
            {
                _rectTransform.localScale = _baseScale;
            }
        }

        private void StopPopAnimation()
        {
            if (_popCoroutine == null)
            {
                return;
            }

            StopCoroutine(_popCoroutine);
            _popCoroutine = null;
        }

        private void RegisterButtonSound()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_button == null)
            {
                return;
            }

            _button.onClick.RemoveListener(PlayClickSound);
            _button.onClick.AddListener(PlayClickSound);
        }

        private void UnregisterButtonSound()
        {
            if (_button == null)
            {
                return;
            }

            _button.onClick.RemoveListener(PlayClickSound);
        }

        private void PlayClickSound()
        {
            if (!enableClickSfx || _button == null || !_button.interactable)
            {
                return;
            }

            AudioManager.Instance?.PlayButtonClick();
        }

        private void RebuildTargets()
        {
            _graphics.Clear();
            _enabledColors.Clear();

            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                _graphics.Add(graphic);
                _enabledColors.Add(graphic.color);
            }
        }

        private void CaptureEnabledColors()
        {
            if (!grayOutAllChildGraphics)
            {
                return;
            }

            if (_graphics.Count != _enabledColors.Count)
            {
                RebuildTargets();
            }

            for (int i = 0; i < _graphics.Count; i++)
            {
                if (_graphics[i] == null)
                {
                    continue;
                }

                _enabledColors[i] = _graphics[i].color;
            }
        }

        private void ApplyEnabledColors()
        {
            if (!grayOutAllChildGraphics)
            {
                return;
            }

            for (int i = 0; i < _graphics.Count; i++)
            {
                if (_graphics[i] == null)
                {
                    continue;
                }

                _graphics[i].color = _enabledColors[i];
            }
        }

        private void ApplyDisabledColors()
        {
            if (!grayOutAllChildGraphics)
            {
                return;
            }

            if (_graphics.Count != _enabledColors.Count)
            {
                RebuildTargets();
            }

            for (int i = 0; i < _graphics.Count; i++)
            {
                Graphic graphic = _graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                graphic.color = ToDisabledColor(_enabledColors[i]);
            }

            _disabledApplied = true;
        }

        private Color ToDisabledColor(Color source)
        {
            float gray = source.grayscale * disabledBrightness;
            return new Color(gray, gray, gray, source.a);
        }
    }

    public static class UIButtonCommonHelper
    {
        public static UIButtonCommon Ensure(Button button)
        {
            if (button == null)
            {
                return null;
            }

            UIButtonCommon common = button.GetComponent<UIButtonCommon>();
            if (common == null)
            {
                common = button.gameObject.AddComponent<UIButtonCommon>();
            }

            return common;
        }

        public static void SetInteractable(Button button, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
            Ensure(button)?.SyncVisualState();
        }

        public static void Sync(Button button)
        {
            Ensure(button)?.SyncVisualState();
        }

        public static void ResetVisualState(Button button)
        {
            Ensure(button)?.ResetVisualState();
        }

        public static void Configure(Button button, bool? grayOutAllChildGraphics = null, bool? enablePopAnimation = null)
        {
            UIButtonCommon common = Ensure(button);
            if (common == null)
            {
                return;
            }

            if (grayOutAllChildGraphics.HasValue)
            {
                common.GrayOutAllChildGraphics = grayOutAllChildGraphics.Value;
            }

            if (enablePopAnimation.HasValue)
            {
                common.EnablePopAnimation = enablePopAnimation.Value;
            }

            common.SyncVisualState();
        }
    }

    public sealed class UIButtonCommonAutoInstaller : MonoBehaviour
    {
        private const float ScanInterval = 1f;

        private float _nextScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            GameObject go = new GameObject("[UIButtonCommonAutoInstaller]");
            DontDestroyOnLoad(go);
            go.AddComponent<UIButtonCommonAutoInstaller>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ScanAllButtons();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.unscaledTime + ScanInterval;
            ScanAllButtons();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ScanAllButtons();
        }

        private void ScanAllButtons()
        {
            Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                GameObject go = button.gameObject;
                if (!IsSceneObject(go))
                {
                    continue;
                }

                UIButtonCommonHelper.Ensure(button);
            }
        }

        private static bool IsSceneObject(GameObject go)
        {
            Scene scene = go.scene;
            return scene.IsValid() && scene.isLoaded && (go.hideFlags & HideFlags.NotEditable) == 0;
        }
    }
}
