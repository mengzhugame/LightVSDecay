// ============================================================
// LaserVFXColorSync.cs
// 文件位置: Assets/Scripts/Logic/Player/LaserVFXColorSync.cs
// 用途：同步激光颜色到粒子特效（StartVFX / EndVFX）
// ============================================================

using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 激光VFX颜色同步组件
    /// 挂载在 LaserBeam 上，负责同步激光颜色到子物体的粒子材质
    /// </summary>
    public class LaserVFXColorSync : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("VFX 引用")]
        [Tooltip("起始特效父物体")]
        [SerializeField] private Transform startVFX;
        
        [Tooltip("结束特效父物体")]
        [SerializeField] private Transform endVFX;
        
        [Header("默认颜色")]
        [Tooltip("默认激光颜色（HDR）")]
        [ColorUsage(true, true)]
        [SerializeField] private Color defaultLaserColor = new Color(0f, 3f, 3f, 1f); // 青色 HDR
        
        [Tooltip("默认特效颜色（HDR）")]
        [ColorUsage(true, true)]
        [SerializeField] private Color defaultVFXColor = new Color(0f, 3f, 3f, 1f); // 青色 HDR
        
        [Header("Shader 属性")]
        [Tooltip("粒子材质发光颜色属性名")]
        [SerializeField] private string emissionColorProperty = "_EmissionColor";
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有变量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private ParticleSystemRenderer[] startVFXRenderers;
        private ParticleSystemRenderer[] endVFXRenderers;
        private MaterialPropertyBlock propertyBlock;
        private int emissionColorID;
        
        // 当前颜色状态
        private Color currentLaserColor;
        private Color currentVFXColor;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            CacheComponents();
            propertyBlock = new MaterialPropertyBlock();
            emissionColorID = Shader.PropertyToID(emissionColorProperty);
            
            // 初始化为默认颜色
            currentLaserColor = defaultLaserColor;
            currentVFXColor = defaultVFXColor;
        }
        
        private void Start()
        {
            // 应用默认颜色
            ApplyVFXColor(currentVFXColor);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void CacheComponents()
        {
            // 自动查找 StartVFX 和 EndVFX
            if (startVFX == null)
            {
                startVFX = transform.Find("StartVFX");
            }
            
            if (endVFX == null)
            {
                endVFX = transform.Find("EndVFX");
            }
            
            // 缓存所有粒子渲染器
            if (startVFX != null)
            {
                startVFXRenderers = startVFX.GetComponentsInChildren<ParticleSystemRenderer>(true);
                if (showDebugInfo)
                {
                    GameLogger.Log($"[LaserVFXColorSync] StartVFX 找到 {startVFXRenderers.Length} 个粒子渲染器");
                }
            }
            
            if (endVFX != null)
            {
                endVFXRenderers = endVFX.GetComponentsInChildren<ParticleSystemRenderer>(true);
                if (showDebugInfo)
                {
                    GameLogger.Log($"[LaserVFXColorSync] EndVFX 找到 {endVFXRenderers.Length} 个粒子渲染器");
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置VFX特效颜色（用于 Frost 等技能）
        /// </summary>
        /// <param name="color">HDR颜色</param>
        public void SetVFXColor(Color color)
        {
            currentVFXColor = color;
            ApplyVFXColor(color);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[LaserVFXColorSync] VFX颜色设置为: {color}");
            }
        }
        
        /// <summary>
        /// 重置VFX颜色为默认值
        /// </summary>
        public void ResetVFXColor()
        {
            SetVFXColor(defaultVFXColor);
        }
        
        /// <summary>
        /// 获取当前VFX颜色
        /// </summary>
        public Color GetCurrentVFXColor() => currentVFXColor;
        
        /// <summary>
        /// 获取默认VFX颜色
        /// </summary>
        public Color GetDefaultVFXColor() => defaultVFXColor;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内部方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 应用颜色到所有VFX粒子
        /// </summary>
        private void ApplyVFXColor(Color color)
        {
            ApplyColorToRenderers(startVFXRenderers, color);
            ApplyColorToRenderers(endVFXRenderers, color);
        }
        
        /// <summary>
        /// 应用颜色到指定的渲染器数组
        /// </summary>
        private void ApplyColorToRenderers(ParticleSystemRenderer[] renderers, Color color)
        {
            if (renderers == null) return;
            
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                
                // 使用 MaterialPropertyBlock 避免创建材质实例
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(emissionColorID, color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器工具
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [ContextMenu("测试: 设置红色")]
        private void TestSetRed()
        {
            CacheComponents();
            propertyBlock = new MaterialPropertyBlock();
            emissionColorID = Shader.PropertyToID(emissionColorProperty);
            SetVFXColor(new Color(3f, 0.3f, 0.2f, 1f));
        }
        
        [ContextMenu("测试: 设置蓝色")]
        private void TestSetBlue()
        {
            CacheComponents();
            propertyBlock = new MaterialPropertyBlock();
            emissionColorID = Shader.PropertyToID(emissionColorProperty);
            SetVFXColor(new Color(0.3f, 0.7f, 3f, 1f));
        }
        
        [ContextMenu("测试: 重置默认")]
        private void TestReset()
        {
            CacheComponents();
            propertyBlock = new MaterialPropertyBlock();
            emissionColorID = Shader.PropertyToID(emissionColorProperty);
            ResetVFXColor();
        }
#endif
    }
}