using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 单个激光光束组件
    /// 负责：长度伸缩、击中检测、Shader参数更新
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class LaserBeam : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 可配置参数
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("激光属性")]
        [Tooltip("激光最大长度")]
        [SerializeField] private float maxLength = GameConstants.LASER_MAX_LENGTH;
        
        [Tooltip("激光宽度")]
        [SerializeField] private float laserWidth = GameConstants.LASER_DEFAULT_WIDTH;
        
        [Tooltip("击中光晕颜色")]
        [SerializeField] private Color glowColor = new Color(1f, 1f, 0.5f, 1f); // 黄色
        
        [Header("性能优化")]
        [Tooltip("Raycast检测间隔（秒）")]
        [SerializeField] private float raycastInterval = GameConstants.RAYCAST_INTERVAL;
        
        [Header("调试")]
        [SerializeField] private bool showDebugGizmos = true;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有变量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Material laserMaterial;
        private Transform cachedTransform;
        
        // Raycast优化
        private float lastRaycastTime;
        private float cachedHitDistance;
        private LayerMask enemyLayerMask;
        
        // 当前击中的目标
        private RaycastHit2D currentHit;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            cachedTransform = transform;
            
            // 获取Material实例（重要：不要用sharedMaterial）
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            laserMaterial = renderer.material;
            
            // 获取敌人Layer
            enemyLayerMask = LayerMask.GetMask(GameConstants.ENEMY_LAYER);
            
            // 初始化缓存值
            cachedHitDistance = maxLength;
            
            // 设置初始Shader参数
            UpdateShaderProperties();
        }
        
        private void Update()
        {
            // 定期检测击中距离
            PerformRaycastCheck();
            
            // 更新激光长度和Shader
            UpdateLaserLength();
        }
        
        private void OnDestroy()
        {
            // 销毁Material实例，避免内存泄漏
            if (laserMaterial != null)
            {
                Destroy(laserMaterial);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 核心逻辑
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 定期执行Raycast检测（性能优化）
        /// </summary>
        private void PerformRaycastCheck()
        {
            // if (Time.time - lastRaycastTime < raycastInterval)
            //     return;
            
            lastRaycastTime = Time.time;
            
            // 从Quad的底部（Pivot）向上发射射线
            Vector2 origin = cachedTransform.position;
            Vector2 direction = cachedTransform.up; // Quad的up方向就是激光方向
            
            // 射线检测
            currentHit = Physics2D.Raycast(origin, direction, maxLength, enemyLayerMask);
            
            if (currentHit.collider != null)
            {
                // 击中敌人，记录距离
                cachedHitDistance = currentHit.distance;
            }
            else
            {
                // 未击中，激光延伸到最大长度
                cachedHitDistance = maxLength;
            }
        }
        
        /// <summary>
        /// 更新激光的视觉长度和Shader参数
        /// </summary>
        private void UpdateLaserLength()
        {
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // SpriteRenderer专用：需要考虑Sprite的原始尺寸
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            
            // 获取Sprite在Unity中的实际高度（Unity单位）
            // bounds.size.y = Sprite像素高度 / Pixels Per Unit
            float spriteUnitHeight = spriteRenderer.sprite.bounds.size.y;
            
            // 计算正确的Scale
            // Scale.y = 需要的Unity长度 / Sprite的Unity高度
            float scaleY = cachedHitDistance / spriteUnitHeight;
            
            cachedTransform.localScale = new Vector3(
                laserWidth,           // X轴宽度
                scaleY,               // Y轴长度（修正后）
                1f                    // Z轴不变
            );
            
            // 更新Shader的HitHeight参数
            // 归一化到0~1范围（UV坐标系）
            float normalizedHitHeight = Mathf.Clamp01(cachedHitDistance / maxLength);
            laserMaterial.SetFloat(GameConstants.ShaderProperties.HitHeight, normalizedHitHeight);
        }
        
        /// <summary>
        /// 更新Shader属性（初始化或动态修改时调用）
        /// </summary>
        private void UpdateShaderProperties()
        {
            if (laserMaterial == null) return;
            
            laserMaterial.SetColor(GameConstants.ShaderProperties.GlowColor, glowColor);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取当前击中的目标
        /// </summary>
        public RaycastHit2D GetCurrentHit() => currentHit;
        
        /// <summary>
        /// 设置激光宽度（用于技能升级）
        /// </summary>
        public void SetLaserWidth(float width)
        {
            laserWidth = width;
        }
        
        /// <summary>
        /// 设置最大长度（用于技能升级）
        /// </summary>
        public void SetMaxLength(float length)
        {
            maxLength = length;
        }
        
        /// <summary>
        /// 动态修改光晕颜色
        /// </summary>
        public void SetGlowColor(Color color)
        {
            glowColor = color;
            UpdateShaderProperties();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试可视化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying) return;
            
            // 绘制Raycast检测线
            Gizmos.color = currentHit.collider != null ? Color.red : Color.green;
            Vector3 origin = transform.position;
            Vector3 direction = transform.up;
            Gizmos.DrawLine(origin, origin + direction * cachedHitDistance);
            
            // 绘制击中点
            if (currentHit.collider != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentHit.point, 0.2f);
            }
        }
    }
}