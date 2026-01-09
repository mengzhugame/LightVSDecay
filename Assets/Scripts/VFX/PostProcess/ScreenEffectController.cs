// ============================================================
// ScreenEffectController.cs
// 文件位置: Assets/Scripts/VFX/PostProcess/ScreenEffectController.cs
// 用途：屏幕后处理效果控制器
// 功能：
//   - 低血量心跳脉冲效果（红色暗角）
//   - 护盾破碎玻璃裂纹效果
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using LightVsDecay.Core;

namespace LightVsDecay.VFX.PostProcess
{
    /// <summary>
    /// 屏幕效果控制器
    /// 管理低血量心跳和护盾破碎等全屏后处理效果
    /// </summary>
    public class ScreenEffectController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public static ScreenEffectController Instance { get; private set; }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 后处理引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("后处理引用")]
        [Tooltip("URP Volume")]
        [SerializeField] private Volume postProcessVolume;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // UI 覆盖层引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("UI 覆盖层")]
        [Tooltip("红色暗角 Image（用于低血量心跳效果）")]
        [SerializeField] private Image redVignetteImage;
        
        [Tooltip("玻璃裂纹 Image（用于护盾破碎效果）")]
        [SerializeField] private Image glassCrackImage;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 低血量心跳设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("低血量心跳设置")]
        [Tooltip("心跳周期（秒）")]
        [SerializeField] private float heartbeatPeriod = 1.0f;
        
        [Tooltip("暗角最大强度")]
        [SerializeField] private float vignetteMaxIntensity = 0.5f;
        
        [Tooltip("暗角最小强度")]
        [SerializeField] private float vignetteMinIntensity = 0.1f;
        
        [Tooltip("暗角颜色（红色）")]
        [SerializeField] private Color vignetteColor = new Color(0.8f, 0f, 0f, 1f);
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 护盾破碎设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("护盾破碎设置")]
        [Tooltip("玻璃裂纹持续时间（秒）")]
        [SerializeField] private float glassCrackDuration = 0.3f;
        
        [Tooltip("玻璃裂纹最大透明度")]
        [SerializeField] private float glassCrackMaxAlpha = 0.8f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 后处理组件缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Vignette vignette;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private bool isLowHealth = false;
        private Coroutine heartbeatCoroutine;
        private Coroutine glassCrackCoroutine;
        
        // 原始暗角设置（用于恢复）
        private float originalVignetteIntensity;
        private Color originalVignetteColor;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 单例设置
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            // 初始化后处理组件
            InitializePostProcess();
            
            // 初始化 UI 覆盖层
            InitializeUIOverlays();
        }
        
        private void OnEnable()
        {
            // 订阅事件
            GameEvents.OnLowHealthStart += OnLowHealthStart;
            GameEvents.OnLowHealthEnd += OnLowHealthEnd;
            GameEvents.OnShieldBroken += OnShieldBroken;
            // 【新增】订阅游戏结束事件
            GameEvents.OnGameVictory += OnGameEnd;
            GameEvents.OnGameDefeat += OnGameEnd;
        }
        
        private void OnDisable()
        {
            // 取消订阅
            GameEvents.OnLowHealthStart -= OnLowHealthStart;
            GameEvents.OnLowHealthEnd -= OnLowHealthEnd;
            GameEvents.OnShieldBroken -= OnShieldBroken;
            // 【新增】取消订阅游戏结束事件
            GameEvents.OnGameVictory -= OnGameEnd;
            GameEvents.OnGameDefeat -= OnGameEnd;
            // 停止协程
            StopAllEffects();
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void InitializePostProcess()
        {
            if (postProcessVolume == null)
            {
                // 尝试查找场景中的 Volume
                postProcessVolume = FindObjectOfType<Volume>();
            }
            
            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                // 获取 Vignette 组件
                if (postProcessVolume.profile.TryGet(out vignette))
                {
                    // 保存原始设置
                    originalVignetteIntensity = vignette.intensity.value;
                    originalVignetteColor = vignette.color.value;
                    
                    if (showDebugInfo)
                    {
                        Debug.Log("[ScreenEffectController] Vignette 组件已获取");
                    }
                }
                else
                {
                    Debug.LogWarning("[ScreenEffectController] Volume Profile 中没有 Vignette 组件");
                }
            }
            else
            {
                Debug.LogWarning("[ScreenEffectController] Post Process Volume 未设置");
            }
        }
        
        private void InitializeUIOverlays()
        {
            // 初始化红色暗角
            if (redVignetteImage != null)
            {
                Color c = redVignetteImage.color;
                c.a = 0f;
                redVignetteImage.color = c;
                redVignetteImage.gameObject.SetActive(false);
            }
            
            // 初始化玻璃裂纹
            if (glassCrackImage != null)
            {
                Color c = glassCrackImage.color;
                c.a = 0f;
                glassCrackImage.color = c;
                glassCrackImage.gameObject.SetActive(false);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnLowHealthStart()
        {
            if (showDebugInfo)
            {
                Debug.Log("[ScreenEffectController] 开始低血量心跳效果");
            }
            
            StartHeartbeat();
        }
        
        private void OnLowHealthEnd()
        {
            if (showDebugInfo)
            {
                Debug.Log("[ScreenEffectController] 停止低血量心跳效果");
            }
            
            StopHeartbeat();
        }
        
        private void OnShieldBroken()
        {
            if (showDebugInfo)
            {
                Debug.Log("[ScreenEffectController] 播放护盾破碎效果");
            }
            
            PlayGlassCrack();
        }
        /// <summary>
        /// 游戏结束时停止所有效果
        /// </summary>
        private void OnGameEnd()
        {
            if (showDebugInfo)
            {
                Debug.Log("[ScreenEffectController] 游戏结束，停止所有效果");
            }
    
            StopAllEffects();
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 低血量心跳效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void StartHeartbeat()
        {
            if (isLowHealth) return;
            isLowHealth = true;
            
            if (heartbeatCoroutine != null)
            {
                StopCoroutine(heartbeatCoroutine);
            }
            heartbeatCoroutine = StartCoroutine(HeartbeatCoroutine());
        }
        
        private void StopHeartbeat()
        {
            isLowHealth = false;
            
            if (heartbeatCoroutine != null)
            {
                StopCoroutine(heartbeatCoroutine);
                heartbeatCoroutine = null;
            }
            
            // 恢复原始设置
            RestoreVignette();
            
            // 隐藏 UI 暗角
            if (redVignetteImage != null)
            {
                redVignetteImage.gameObject.SetActive(false);
            }
        }
        
        private IEnumerator HeartbeatCoroutine()
        {
            // 启用 UI 暗角
            if (redVignetteImage != null)
            {
                redVignetteImage.gameObject.SetActive(true);
            }
            
            // 设置 Vignette 颜色
            if (vignette != null)
            {
                vignette.color.value = vignetteColor;
            }
            
            float timer = 0f;
            
            while (isLowHealth)
            {
                timer += Time.deltaTime;
                
                // 计算心跳脉冲（使用正弦波）
                // 心跳效果：快速收缩 + 慢速舒张
                float normalizedTime = (timer % heartbeatPeriod) / heartbeatPeriod;
                
                // 心跳波形：0->0.3 快速上升，0.3->1 慢速下降
                float intensity;
                if (normalizedTime < 0.15f)
                {
                    // 快速收缩（0->0.15）
                    intensity = Mathf.Lerp(vignetteMinIntensity, vignetteMaxIntensity, normalizedTime / 0.15f);
                }
                else if (normalizedTime < 0.3f)
                {
                    // 保持峰值
                    intensity = vignetteMaxIntensity;
                }
                else
                {
                    // 慢速舒张（0.3->1）
                    float t = (normalizedTime - 0.3f) / 0.7f;
                    intensity = Mathf.Lerp(vignetteMaxIntensity, vignetteMinIntensity, t);
                }
                
                // 应用到 Vignette
                if (vignette != null)
                {
                    vignette.intensity.value = intensity;
                }
                
                // 同时更新 UI 暗角
                if (redVignetteImage != null)
                {
                    Color c = redVignetteImage.color;
                    c.a = intensity * 0.6f; // UI 透明度稍低
                    redVignetteImage.color = c;
                }
                
                yield return null;
            }
        }
        
        private void RestoreVignette()
        {
            if (vignette != null)
            {
                vignette.intensity.value = originalVignetteIntensity;
                vignette.color.value = originalVignetteColor;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 护盾破碎玻璃裂纹效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void PlayGlassCrack()
        {
            if (glassCrackCoroutine != null)
            {
                StopCoroutine(glassCrackCoroutine);
            }
            glassCrackCoroutine = StartCoroutine(GlassCrackCoroutine());
        }
        
        private IEnumerator GlassCrackCoroutine()
        {
            if (glassCrackImage == null)
            {
                Debug.LogWarning("[ScreenEffectController] GlassCrack Image 未设置");
                yield break;
            }
            
            // 显示玻璃裂纹
            glassCrackImage.gameObject.SetActive(true);
            
            float elapsed = 0f;
            
            // 快速淡入（10% 时间）
            float fadeInDuration = glassCrackDuration * 0.1f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeInDuration;
                
                Color c = glassCrackImage.color;
                c.a = Mathf.Lerp(0f, glassCrackMaxAlpha, t);
                glassCrackImage.color = c;
                
                yield return null;
            }
            
            // 保持显示（20% 时间）
            float holdDuration = glassCrackDuration * 0.2f;
            yield return new WaitForSeconds(holdDuration);
            
            // 淡出（70% 时间）
            elapsed = 0f;
            float fadeOutDuration = glassCrackDuration * 0.7f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;
                
                Color c = glassCrackImage.color;
                c.a = Mathf.Lerp(glassCrackMaxAlpha, 0f, t);
                glassCrackImage.color = c;
                
                yield return null;
            }
            
            // 隐藏
            glassCrackImage.gameObject.SetActive(false);
            glassCrackCoroutine = null;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 停止所有效果
        /// </summary>
        public void StopAllEffects()
        {
            StopHeartbeat();
            
            if (glassCrackCoroutine != null)
            {
                StopCoroutine(glassCrackCoroutine);
                glassCrackCoroutine = null;
            }
            
            if (glassCrackImage != null)
            {
                glassCrackImage.gameObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// 手动触发护盾破碎效果（调试用）
        /// </summary>
        [ContextMenu("Test Glass Crack")]
        public void TestGlassCrack()
        {
            PlayGlassCrack();
        }
        
        /// <summary>
        /// 手动触发心跳效果（调试用）
        /// </summary>
        [ContextMenu("Test Heartbeat")]
        public void TestHeartbeat()
        {
            if (isLowHealth)
            {
                StopHeartbeat();
            }
            else
            {
                StartHeartbeat();
            }
        }
    }
}