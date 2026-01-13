// ============================================================
// SettingsPanel.cs
// 文件位置: Assets/Scripts/UI/Panels/SettingsPanel.cs
// 用途：设置面板控制器，包含音量滑块
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using LightVsDecay.Audio;

namespace LightVsDecay.UI
{
    /// <summary>
    /// 设置面板控制器
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // UI 组件引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 音量滑块 ═══")]
        [Tooltip("音乐音量滑块")]
        [SerializeField] private Slider bgmSlider;
        
        [Tooltip("音效音量滑块")]
        [SerializeField] private Slider sfxSlider;
        
        [Header("═══ 按钮 ═══")]
        [Tooltip("关闭按钮")]
        [SerializeField] private Button closeButton;
        
        [Header("═══ 可选：音量百分比文本 ═══")]
        [SerializeField] private TMPro.TextMeshProUGUI bgmVolumeText;
        [SerializeField] private TMPro.TextMeshProUGUI sfxVolumeText;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Start()
        {
            InitializeSliders();
            SetupButtonListeners();
        }
        
        private void OnEnable()
        {
            // 每次显示时刷新滑块值
            RefreshSliderValues();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void InitializeSliders()
        {
            if (bgmSlider != null)
            {
                bgmSlider.minValue = 0f;
                bgmSlider.maxValue = 1f;
                bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
            }
            
            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 1f;
                sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            }
            
            RefreshSliderValues();
        }
        
        private void SetupButtonListeners()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseButtonClicked);
            }
        }
        
        private void RefreshSliderValues()
        {
            if (AudioManager.Instance == null) return;
            
            if (bgmSlider != null)
            {
                bgmSlider.SetValueWithoutNotify(AudioManager.Instance.BGMVolume);
                UpdateBGMVolumeText(AudioManager.Instance.BGMVolume);
            }
            
            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(AudioManager.Instance.SFXVolume);
                UpdateSFXVolumeText(AudioManager.Instance.SFXVolume);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 滑块回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnBGMVolumeChanged(float value)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.BGMVolume = value;
            }
            
            UpdateBGMVolumeText(value);
        }
        
        private void OnSFXVolumeChanged(float value)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SFXVolume = value;
                
                // 播放按钮点击音效作为预览
                AudioManager.Instance.PlayButtonClick();
            }
            
            UpdateSFXVolumeText(value);
        }
        
        private void UpdateBGMVolumeText(float value)
        {
            if (bgmVolumeText != null)
            {
                bgmVolumeText.text = $"{Mathf.RoundToInt(value * 100)}%";
            }
        }
        
        private void UpdateSFXVolumeText(float value)
        {
            if (sfxVolumeText != null)
            {
                sfxVolumeText.text = $"{Mathf.RoundToInt(value * 100)}%";
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnCloseButtonClicked()
        {
            // 播放按钮音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButtonClick();
            }
            
            // 关闭面板
            gameObject.SetActive(false);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示设置面板
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }
        
        /// <summary>
        /// 隐藏设置面板
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}