// ============================================================
// SettingsPanel.cs
// 文件位置: Assets/Scripts/UI/Panels/SettingsPanel.cs
// 用途：设置/暂停面板控制器
// 功能：音乐/音效开关、返回主页、重新开始
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LightVsDecay.Audio;
using LightVsDecay.Logic;

namespace LightVsDecay.UI
{
    /// <summary>
    /// 设置面板控制器
    /// 主界面：仅显示音乐/音效开关
    /// 战斗场景：显示音乐/音效开关 + 返回主页/重新开始按钮
    /// </summary>
    public class SettingsPanel : MonoBehaviour, IPointerClickHandler
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // UI 组件引用 - 音频开关
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 音频开关 ═══")]
        [Tooltip("音乐开关 Toggle")]
        [SerializeField] private Toggle musicToggle;
        
        [Tooltip("音效开关 Toggle")]
        [SerializeField] private Toggle soundToggle;
        [Header("═══ 开关滑块（Checkmark）═══")]
        [Tooltip("音乐开关滑块")]
        [SerializeField] private RectTransform musicCheckmark;

        [Tooltip("音效开关滑块")]
        [SerializeField] private RectTransform soundCheckmark;

        [Header("═══ 滑块位置配置 ═══")]
        [Tooltip("开启时的X位置")]
        [SerializeField] private float checkmarkOnPosX = 140f;

        [Tooltip("关闭时的X位置")]
        [SerializeField] private float checkmarkOffPosX = -140f;
        [Header("═══ 开关填充图（可选，用于视觉反馈）═══")]
        [Tooltip("音乐开关填充图（开启时显示）")]
        [SerializeField] private GameObject musicFillImage;
        
        [Tooltip("音效开关填充图（开启时显示）")]
        [SerializeField] private GameObject soundFillImage;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // UI 组件引用 - 底部按钮区域（战斗场景显示）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 底部按钮区域（BottomArea）═══")]
        [Tooltip("底部按钮区域根节点")]
        [SerializeField] private GameObject bottomArea;
        
        [Tooltip("返回主页按钮")]
        [SerializeField] private Button homeButton;
        
        [Tooltip("重新开始按钮")]
        [SerializeField] private Button restartButton;
        
        [Header("═══ 按钮禁用颜色 ═══")]
        [Tooltip("按钮禁用时的颜色")]
        [SerializeField] private Color disabledButtonColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // UI 组件引用 - 内容区域（用于点击空白关闭判断）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 内容区域 ═══")]
        [Tooltip("内容区域（点击此区域外关闭面板）")]
        [SerializeField] private RectTransform contentArea;
        
        [Header("═══ 调试 ═══")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private bool showBottomArea = false;  // 是否显示底部按钮区域
        private bool isInBattleScene = false; // 是否在战斗场景
        private Image restartButtonImage;
        private Color restartButtonOriginalColor;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 缓存重新开始按钮的图片组件
            if (restartButton != null)
            {
                restartButtonImage = restartButton.GetComponent<Image>();
                if (restartButtonImage != null)
                {
                    restartButtonOriginalColor = restartButtonImage.color;
                }
            }
        }
        
        private void Start()
        {
            SetupToggles();
            SetupButtons();
        }
        
        private void OnEnable()
        {
            // 每次显示时刷新 Toggle 状态
            RefreshToggleStates();
            
            // 如果在战斗场景，暂停游戏
            if (isInBattleScene)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.PauseGame();
                }
                
                // 更新重新开始按钮状态
                UpdateRestartButtonState();
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[SettingsPanel] OnEnable - 战斗场景: {isInBattleScene}, 显示底部: {showBottomArea}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void SetupToggles()
        {
            if (musicToggle != null)
            {
                musicToggle.onValueChanged.AddListener(OnMusicToggleChanged);
            }
            
            if (soundToggle != null)
            {
                soundToggle.onValueChanged.AddListener(OnSoundToggleChanged);
            }
            
            RefreshToggleStates();
        }
        
        private void SetupButtons()
        {
            if (homeButton != null)
            {
                homeButton.onClick.AddListener(OnHomeButtonClicked);
            }
            
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartButtonClicked);
            }
        }
        
        private void RefreshToggleStates()
        {
            if (AudioManager.Instance == null) return;
    
            // 同步 Toggle 状态
            if (musicToggle != null)
            {
                bool musicOn = AudioManager.Instance.BGMEnabled;
                musicToggle.SetIsOnWithoutNotify(musicOn);
                UpdateCheckmarkPosition(musicCheckmark, musicOn);
                UpdateFillImage(musicFillImage, musicOn);
            }
    
            if (soundToggle != null)
            {
                bool soundOn = AudioManager.Instance.SFXEnabled;
                soundToggle.SetIsOnWithoutNotify(soundOn);
                UpdateCheckmarkPosition(soundCheckmark, soundOn);
                UpdateFillImage(soundFillImage, soundOn);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Toggle 回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnMusicToggleChanged(bool isOn)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.BGMEnabled = isOn;
            }
            // 更新 Checkmark 位置
            UpdateCheckmarkPosition(musicCheckmark, isOn);
            UpdateFillImage(musicFillImage, isOn);
            
            // 播放按钮音效
            PlayButtonSound();
            
            if (showDebugInfo)
            {
                Debug.Log($"[SettingsPanel] 音乐开关: {isOn}");
            }
        }
        
        private void OnSoundToggleChanged(bool isOn)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SFXEnabled = isOn;
            }
            // 更新 Checkmark 位置
            UpdateCheckmarkPosition(soundCheckmark, isOn);
            UpdateFillImage(soundFillImage, isOn);
            
            PlayButtonSound();

            if (showDebugInfo)
            {
                Debug.Log($"[SettingsPanel] 音效开关: {isOn}");
            }
        }
        /// <summary>
        /// 更新 Checkmark 滑块位置
        /// </summary>
        private void UpdateCheckmarkPosition(RectTransform checkmark, bool isOn)
        {
            if (checkmark == null) return;
    
            Vector2 pos = checkmark.anchoredPosition;
            pos.x = isOn ? checkmarkOnPosX : checkmarkOffPosX;
            checkmark.anchoredPosition = pos;
        }
        private void UpdateFillImage(GameObject fillImage, bool isOn)
        {
            if (fillImage != null)
            {
                fillImage.SetActive(isOn);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnHomeButtonClicked()
        {
            PlayButtonSound();
            
            if (showDebugInfo)
            {
                Debug.Log("[SettingsPanel] 点击返回主页");
            }
            
            // 隐藏面板
            Hide();
            
            // 返回主菜单
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadMainMenu();
            }
        }
        
        private void OnRestartButtonClicked()
        {
            // 检查能量是否足够
            int currentEnergy = 0;
            if (ProgressManager.Instance != null)
            {
                currentEnergy = ProgressManager.Instance.Energy;
            }
            else
            {
                currentEnergy = PlayerPrefs.GetInt("PlayerEnergy", 5);
            }
            
            if (currentEnergy <= 0)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[SettingsPanel] 能量不足，无法重新开始");
                }
                return;
            }
            
            PlayButtonSound();
            
            if (showDebugInfo)
            {
                Debug.Log("[SettingsPanel] 点击重新开始");
            }
            
            // 扣除能量
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.ConsumeEnergy(1);
            }
            else
            {
                PlayerPrefs.SetInt("PlayerEnergy", currentEnergy - 1);
                PlayerPrefs.Save();
            }
            
            // 隐藏面板
            Hide();
            
            // 重新开始游戏
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
            }
        }
        
        /// <summary>
        /// 更新重新开始按钮状态（能量不足时置灰）
        /// </summary>
        private void UpdateRestartButtonState()
        {
            if (restartButton == null) return;
            
            int currentEnergy = 0;
            if (ProgressManager.Instance != null)
            {
                currentEnergy = ProgressManager.Instance.Energy;
            }
            else
            {
                currentEnergy = PlayerPrefs.GetInt("PlayerEnergy", 5);
            }
            
            bool hasEnergy = currentEnergy > 0;
            restartButton.interactable = hasEnergy;
            
            if (restartButtonImage != null)
            {
                restartButtonImage.color = hasEnergy ? restartButtonOriginalColor : disabledButtonColor;
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[SettingsPanel] 重新开始按钮状态: 能量={currentEnergy}, 可用={hasEnergy}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 点击空白处关闭
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void OnPointerClick(PointerEventData eventData)
        {
            // 检查点击位置是否在内容区域外
            if (contentArea != null)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                    contentArea, eventData.position, eventData.pressEventCamera))
                {
                    // 点击在内容区域外，关闭面板
                    PlayButtonSound();
                    Hide();
                    
                    if (showDebugInfo)
                    {
                        Debug.Log("[SettingsPanel] 点击空白处，关闭面板");
                    }
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示设置面板
        /// </summary>
        /// <param name="showBottom">是否显示底部按钮区域（战斗场景为true）</param>
        public void Show(bool showBottom = false)
        {
            showBottomArea = showBottom;
            isInBattleScene = showBottom;
            
            // 设置底部区域显隐
            if (bottomArea != null)
            {
                bottomArea.SetActive(showBottom);
            }
            
            // 显示面板
            gameObject.SetActive(true);
            
            if (showDebugInfo)
            {
                Debug.Log($"[SettingsPanel] Show - 显示底部区域: {showBottom}");
            }
        }
        
        /// <summary>
        /// 隐藏设置面板
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            
            // 如果在战斗场景，恢复游戏
            if (isInBattleScene)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ResumeGame();
                }
                
                // 重启激光音效
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.StartLaserLoop();
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[SettingsPanel] Hide");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 辅助方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void PlayButtonSound()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButtonClick();
            }
        }
    }
}