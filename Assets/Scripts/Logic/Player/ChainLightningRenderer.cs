// ============================================================
// ChainLightningRenderer.cs
// 文件位置: Assets/Scripts/Logic/Player/ChainLightningRenderer.cs
// 用途：单条连锁传导线的视觉渲染
// 方案：使用 SpriteRenderer 面片拉伸（支持UV流动材质）
// ============================================================

using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 单条连锁传导线渲染器
    /// 使用 SpriteRenderer 面片拉伸实现两点之间的连线效果
    /// </summary>
    public class ChainLightningRenderer : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 组件引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("组件")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private ParticleSystem[] particleSystems;
        [SerializeField] private ParticleSystemRenderer[] particleRenderers;
        
        [Header("视觉设置")]
        [Tooltip("基础宽度（Y轴缩放）")]
        [SerializeField] private float baseWidth = 0.2f;
        
        [Tooltip("基础颜色（乘以材质颜色）")]
        [ColorUsage(true, true)]
        [SerializeField] private Color baseColor = Color.white;
        
        [Header("闪电抖动")]
        [SerializeField] private bool enableJitter = true;
        [SerializeField] private float jitterAmount = 0.02f;
        [SerializeField] private float jitterSpeed = 15f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Transform cachedTransform;
        private Vector3 startPoint;
        private Vector3 endPoint;
        private float currentAlpha = 1f;
        private bool isActive = false;
        
        // 跳数（用于透明度和宽度计算）
        private int bounceIndex = 0;
        
        // 抖动相关
        private float jitterOffset = 0f;
        
        // 原始sprite尺寸（用于计算拉伸比例）
        private float originalSpriteWidth = 1f;
        private MaterialPropertyBlock particlePropertyBlock;

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorID = Shader.PropertyToID("_Color");
        private static readonly int TintColorID = Shader.PropertyToID("_TintColor");
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>是否激活</summary>
        public bool IsActive => isActive;
        
        /// <summary>起点位置</summary>
        public Vector3 StartPoint => startPoint;
        
        /// <summary>终点位置</summary>
        public Vector3 EndPoint => endPoint;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            cachedTransform = transform;
            particlePropertyBlock = new MaterialPropertyBlock();
            
            // 自动获取 SpriteRenderer
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            CacheParticleSystems();
            CacheParticleRenderers();
            ConfigureParticlesForPooling();
            
            // 缓存原始sprite宽度
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                // sprite的世界单位宽度 = bounds.size.x
                originalSpriteWidth = spriteRenderer.sprite.bounds.size.x;
                
                // 确保有有效值
                if (originalSpriteWidth <= 0.001f)
                {
                    originalSpriteWidth = 1f;
                }
            }
            
            // 随机抖动偏移（让每条线的抖动不同步）
            jitterOffset = Random.Range(0f, 100f);
            
            // 初始隐藏
            SetVisible(false);
            StopParticles();
        }
        
        private void Update()
        {
            if (!isActive) return;
            
            // 更新抖动效果
            if (enableJitter)
            {
                UpdateJitter();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 初始化传导线
        /// </summary>
        /// <param name="start">起点（敌人A位置）</param>
        /// <param name="end">终点（敌人B位置）</param>
        /// <param name="bounce">当前跳数（0-based，用于透明度）</param>
        /// <param name="isMainLaser">是否来自主激光</param>
        public void Initialize(Vector3 start, Vector3 end, int bounce, bool isMainLaser)
        {
            startPoint = start;
            endPoint = end;
            bounceIndex = bounce;
            
            // 根据跳数计算透明度
            currentAlpha = GetAlphaForBounce(bounce);
            
            // 根据主/副激光和跳数设置宽度
            float widthMultiplier = isMainLaser ? 1f : 0.7f;
            float bounceWidthFactor = Mathf.Lerp(1f, 0.6f, bounce / 4f); // 跳数越多越细
            baseWidth = GameConstants.CHAIN_LINE_WIDTH_MAIN * widthMultiplier * bounceWidthFactor;
            
            // 更新视觉
            UpdateVisual();
            SetVisible(true);
            PlayParticles();
            isActive = true;
        }
        
        /// <summary>
        /// 更新传导线位置
        /// </summary>
        /// <param name="start">新起点</param>
        /// <param name="end">新终点</param>
        public void UpdatePositions(Vector3 start, Vector3 end)
        {
            startPoint = start;
            endPoint = end;
            UpdateVisual();
        }
        
        /// <summary>
        /// 停用传导线
        /// </summary>
        public void Deactivate()
        {
            isActive = false;
            SetVisible(false);
            StopParticles();
        }
        
        /// <summary>
        /// 设置颜色
        /// </summary>
        public void SetColor(Color color)
        {
            baseColor = color;
            UpdateVisual();
            ApplyParticleColor(color);
        }
        
        /// <summary>
        /// 设置宽度
        /// </summary>
        public void SetWidth(float width)
        {
            baseWidth = width;
            UpdateVisual();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 更新视觉效果（面片拉伸）
        /// </summary>
        private void UpdateVisual()
        {
            if (spriteRenderer == null) return;
            
            // 1. 计算中点位置
            Vector3 midPoint = (startPoint + endPoint) / 2f;
            cachedTransform.position = midPoint;
            
            // 2. 计算方向和长度
            Vector3 direction = endPoint - startPoint;
            float length = direction.magnitude;
            
            // 3. 计算旋转角度（让sprite指向终点）
            if (length > 0.01f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                cachedTransform.rotation = Quaternion.Euler(0, 0, angle);
            }
            
            // 4. 计算缩放
            // X轴：拉伸到两点之间的距离
            // Y轴：宽度
            float scaleX = length / originalSpriteWidth;
            float scaleY = baseWidth;
            
            cachedTransform.localScale = new Vector3(scaleX, scaleY, 1f);
            
            // 5. 设置颜色和透明度
            Color finalColor = baseColor;
            finalColor.a = currentAlpha;
            spriteRenderer.color = finalColor;
            ApplyRendererMaterialColor(spriteRenderer, finalColor);
        }
        
        /// <summary>
        /// 更新抖动效果（轻微的宽度波动）
        /// </summary>
        private void UpdateJitter()
        {
            if (spriteRenderer == null) return;
            
            // 使用 Perlin 噪声产生平滑的抖动
            float noise = Mathf.PerlinNoise((Time.time + jitterOffset) * jitterSpeed, 0f);
            float jitterMultiplier = 1f + (noise - 0.5f) * 2f * jitterAmount;
            
            // 只影响Y轴（宽度）
            Vector3 scale = cachedTransform.localScale;
            scale.y = baseWidth * jitterMultiplier;
            cachedTransform.localScale = scale;
        }
        
        /// <summary>
        /// 设置可见性
        /// </summary>
        private void SetVisible(bool visible)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = visible;
            }
        }

        private void CacheParticleSystems()
        {
            if (particleSystems == null || particleSystems.Length == 0)
            {
                particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            }
        }

        private void CacheParticleRenderers()
        {
            if (particleRenderers == null || particleRenderers.Length == 0)
            {
                particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            }
        }

        private void ConfigureParticlesForPooling()
        {
            if (particleSystems == null) return;

            foreach (var ps in particleSystems)
            {
                if (ps == null) continue;

                var main = ps.main;
                main.playOnAwake = false;
            }
        }

        private void PlayParticles()
        {
            if (particleSystems == null) return;

            ApplyParticleColor(baseColor);

            foreach (var ps in particleSystems)
            {
                if (ps == null) continue;

                ps.Clear(true);
                ps.Play(true);
            }
        }

        private void StopParticles()
        {
            if (particleSystems == null) return;

            foreach (var ps in particleSystems)
            {
                if (ps == null) continue;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void ApplyParticleColor(Color color)
        {
            ApplyColorToParticleSystems(color);
            ApplyColorToParticleRenderers(color);
        }

        private void ApplyColorToParticleSystems(Color color)
        {
            if (particleSystems == null) return;

            foreach (var ps in particleSystems)
            {
                if (ps == null) continue;

                var main = ps.main;
                main.startColor = color;
            }
        }

        private void ApplyColorToParticleRenderers(Color color)
        {
            if (particleRenderers == null) return;

            foreach (var renderer in particleRenderers)
            {
                if (renderer == null) continue;

                ApplyRendererMaterialColor(renderer, color);
            }
        }

        private void ApplyRendererMaterialColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;

            if (particlePropertyBlock == null)
            {
                particlePropertyBlock = new MaterialPropertyBlock();
            }

            renderer.GetPropertyBlock(particlePropertyBlock);
            particlePropertyBlock.SetColor(EmissionColorID, color);
            particlePropertyBlock.SetColor(BaseColorID, color);
            particlePropertyBlock.SetColor(ColorID, color);
            particlePropertyBlock.SetColor(TintColorID, color);
            renderer.SetPropertyBlock(particlePropertyBlock);
        }
        
        /// <summary>
        /// 根据跳数获取透明度
        /// </summary>
        private float GetAlphaForBounce(int bounce)
        {
            if (bounce < 0) return 1f;
            if (bounce >= GameConstants.CHAIN_ALPHA_PER_BOUNCE.Length)
            {
                return GameConstants.CHAIN_ALPHA_PER_BOUNCE[GameConstants.CHAIN_ALPHA_PER_BOUNCE.Length - 1];
            }
            return GameConstants.CHAIN_ALPHA_PER_BOUNCE[bounce];
        }
    }
}
