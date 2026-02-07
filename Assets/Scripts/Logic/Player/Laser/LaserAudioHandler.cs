// ============================================================
// LaserAudioHandler.cs
// 文件位置: Assets/Scripts/Logic/Player/Laser/LaserAudioHandler.cs
// 用途：激光音效系统 - 从 LaserController 拆分
// ============================================================

using UnityEngine;
using LightVsDecay.Audio;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 激光音效处理器
    /// 负责：激光循环音效、命中类型音效
    /// 注意：LaserHitType 枚举定义在 LightVsDecay.Audio 命名空间
    /// </summary>
    [System.Serializable]
    public class LaserAudioHandler
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private bool isLaserAudioStarted = false;
        private LaserHitType frameHighestHitType = LaserHitType.None;
        
        private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>激光音效是否已启动</summary>
        public bool IsAudioStarted => isLaserAudioStarted;
        
        /// <summary>本帧最高优先级的命中类型</summary>
        public LaserHitType FrameHighestHitType => frameHighestHitType;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 激光循环音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 启动激光音效循环
        /// </summary>
        public void StartLaserAudio()
        {
            if (showDebugInfo)
                Debug.Log($"[LaserAudioHandler] StartLaserAudio 调用, AudioManager={AudioManager.Instance != null}, isStarted={isLaserAudioStarted}");
            
            if (AudioManager.Instance != null && !isLaserAudioStarted)
            {
                AudioManager.Instance.StartLaserLoop();
                isLaserAudioStarted = true;
                
                if (showDebugInfo)
                    Debug.Log("[LaserAudioHandler] 激光音效已启动");
            }
        }
        
        /// <summary>
        /// 停止激光音效循环
        /// </summary>
        public void StopLaserAudio()
        {
            if (AudioManager.Instance != null && isLaserAudioStarted)
            {
                AudioManager.Instance.StopLaserLoop();
                isLaserAudioStarted = false;
            }
        }
        
        /// <summary>
        /// 确保音效已启动
        /// </summary>
        public void EnsureAudioStarted()
        {
            if (!isLaserAudioStarted)
            {
                StartLaserAudio();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 命中类型管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 每帧开始时重置命中类型
        /// </summary>
        public void ResetFrameHitType()
        {
            frameHighestHitType = LaserHitType.None;
        }
        
        /// <summary>
        /// 更新本帧最高优先级的命中类型
        /// 优先级: Metal > Burn > None
        /// </summary>
        public void UpdateFrameHitType(LaserHitType hitType)
        {
            // 只保留最高优先级
            if ((int)hitType > (int)frameHighestHitType)
            {
                frameHighestHitType = hitType;
            }
        }
        
        /// <summary>
        /// 更新激光音效类型（每帧结束时调用）
        /// </summary>
        public void UpdateLaserAudioType()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.UpdateLaserHitType(frameHighestHitType);
            }
        }
        
        /// <summary>
        /// 游戏暂停/结束时重置音效
        /// </summary>
        public void ResetToIdle()
        {
            if (AudioManager.Instance != null && frameHighestHitType != LaserHitType.None)
            {
                frameHighestHitType = LaserHitType.None;
                AudioManager.Instance.UpdateLaserHitType(LaserHitType.None);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置调试模式
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            showDebugInfo = enabled;
        }
    }
}
