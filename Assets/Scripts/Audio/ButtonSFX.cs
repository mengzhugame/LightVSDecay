// ============================================================
// ButtonSFX.cs
// 文件位置: Assets/Scripts/Audio/ButtonSFX.cs
// 用途：自动为按钮添加点击音效
// ============================================================

using UnityEngine;
using UnityEngine.UI;

namespace LightVsDecay.Audio
{
    /// <summary>
    /// 按钮点击音效组件
    /// 挂载到任何 Button 上，自动播放点击音效
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonSFX : MonoBehaviour
    {
        private Button button;
        
        private void Awake()
        {
            button = GetComponent<Button>();
        }
        
        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(PlayClickSound);
            }
        }
        
        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(PlayClickSound);
            }
        }
        
        private void PlayClickSound()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButtonClick();
            }
        }
    }
}