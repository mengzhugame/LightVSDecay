// ============================================================
// FloatingTextConfig.cs
// 文件位置: Assets/Scripts/UI/FloatingText/FloatingTextConfig.cs
// 用途：飘字视觉配置（ScriptableObject）
// ============================================================

using UnityEngine;

namespace LightVsDecay.UI.FloatingText
{
    /// <summary>
    /// 单个飘字类型的配置
    /// </summary>
    [System.Serializable]
    public class FloatingTextStyle
    {
        [Header("颜色")]
        [Tooltip("文字颜色")]
        public Color textColor = Color.white;
        
        [Tooltip("描边颜色")]
        public Color outlineColor = Color.black;
        
        [Header("字体")]
        [Tooltip("字体大小")]
        [Range(16f, 72f)]
        public float fontSize = 32f;
        
        [Tooltip("是否加粗")]
        public bool isBold = false;
        
        [Tooltip("描边宽度")]
        [Range(0f, 0.5f)]
        public float outlineWidth = 0.2f;
        
        [Header("动画")]
        [Tooltip("持续时间")]
        [Range(0.3f, 2f)]
        public float duration = 0.6f;
        
        [Tooltip("初始向上速度")]
        [Range(0f, 300f)]
        public float initialUpSpeed = 150f;
        
        [Tooltip("水平随机范围")]
        [Range(0f, 200f)]
        public float horizontalRandomRange = 80f;
        
        [Tooltip("重力（下落加速度）")]
        [Range(0f, 500f)]
        public float gravity = 0f;
        
        [Tooltip("淡出开始时间（占总时长百分比）")]
        [Range(0.3f, 0.9f)]
        public float fadeStartPercent = 0.5f;
        
        [Header("缩放动画")]
        [Tooltip("是否启用缩放动画")]
        public bool useScaleAnimation = false;
        
        [Tooltip("初始缩放")]
        [Range(0.5f, 2f)]
        public float initialScale = 1f;
        
        [Tooltip("峰值缩放")]
        [Range(1f, 3f)]
        public float peakScale = 1.5f;
        
        [Tooltip("缩放峰值时间（占总时长百分比）")]
        [Range(0.1f, 0.5f)]
        public float scalePeakPercent = 0.2f;
        
        [Header("整体缩放倍率")]
        [Tooltip("整体大小倍率（影响字体和图标）")]
        [Range(0.5f, 2f)]
        public float sizeMultiplier = 1f;
    }
    
    /// <summary>
    /// 飘字系统配置（ScriptableObject）
    /// </summary>
    [CreateAssetMenu(fileName = "FloatingTextConfig", menuName = "LightVsDecay/FloatingTextConfig")]
    public class FloatingTextConfig : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 对象池设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("对象池设置")]
        [Tooltip("预热数量")]
        [Range(10, 50)]
        public int prewarmCount = 20;
        
        [Tooltip("最大数量上限")]
        [Range(20, 100)]
        public int maxPoolSize = 40;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Prefab 引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("Prefab 引用")]
        [Tooltip("普通伤害 Prefab")]
        public GameObject normalPrefab;
        
        [Tooltip("暴击伤害 Prefab")]
        public GameObject critPrefab;
        
        [Tooltip("Boss护甲伤害 Prefab")]
        public GameObject bossShieldPrefab;
        
        [Tooltip("Boss核心伤害 Prefab")]
        public GameObject bossCorePrefab;
        
        [Tooltip("状态文本 Prefab（可选，不设置则使用 Normal）")]
        public GameObject statusPrefab;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 优先级设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("优先级设置")]
        [Tooltip("普通伤害优先级（越低越容易被回收）")]
        public int normalPriority = 0;
        
        [Tooltip("暴击伤害优先级")]
        public int critPriority = 3;
        
        [Tooltip("状态文本优先级")]
        public int statusPriority = 1;
        
        [Tooltip("Boss护甲伤害优先级")]
        public int bossShieldPriority = 1;
        
        [Tooltip("Boss核心伤害优先级")]
        public int bossCorePriority = 2;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 样式配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("普通伤害样式")]
        public FloatingTextStyle normalStyle = new FloatingTextStyle
        {
            textColor = Color.white,
            outlineColor = new Color(0.2f, 0.2f, 0.2f, 1f),
            fontSize = 32f,
            isBold = false,
            outlineWidth = 0.15f,
            duration = 0.6f,
            initialUpSpeed = 120f,
            horizontalRandomRange = 60f,
            gravity = 100f,
            fadeStartPercent = 0.5f,
            useScaleAnimation = false,
            initialScale = 1f,
            peakScale = 1f,
            scalePeakPercent = 0.2f,
            sizeMultiplier = 1.0f  // 标准大小
        };
        
        [Header("暴击伤害样式")]
        public FloatingTextStyle critStyle = new FloatingTextStyle
        {
            textColor = new Color(1f, 0f, 0.33f, 1f), // #FF0055 霓虹红
            outlineColor = new Color(0.5f, 0f, 0.15f, 1f),
            fontSize = 48f,
            isBold = true,
            outlineWidth = 0.25f,
            duration = 1.0f,
            initialUpSpeed = 200f,
            horizontalRandomRange = 40f,
            gravity = 300f,
            fadeStartPercent = 0.6f,
            useScaleAnimation = true,
            initialScale = 0.8f,
            peakScale = 1.4f,
            scalePeakPercent = 0.15f,
            sizeMultiplier = 1.5f  // 1.5倍大小
        };
        
        [Header("状态文本样式")]
        public FloatingTextStyle statusStyle = new FloatingTextStyle
        {
            textColor = new Color(1f, 0.92f, 0.016f, 1f), // 黄色
            outlineColor = new Color(0.3f, 0.25f, 0f, 1f),
            fontSize = 36f,
            isBold = true,
            outlineWidth = 0.2f,
            duration = 0.8f,
            initialUpSpeed = 180f,
            horizontalRandomRange = 20f,
            gravity = 50f,
            fadeStartPercent = 0.6f,
            useScaleAnimation = true,
            initialScale = 0.6f,
            peakScale = 1.2f,
            scalePeakPercent = 0.25f,
            sizeMultiplier = 1.0f
        };
        
        [Header("Boss护甲伤害样式")]
        public FloatingTextStyle bossShieldStyle = new FloatingTextStyle
        {
            textColor = new Color(0.8f, 0.8f, 0.8f, 1f), // 银灰色 #CCCCCC
            outlineColor = new Color(0.3f, 0.3f, 0.3f, 1f),
            fontSize = 22f,  // 比普通小 30%
            isBold = false,
            outlineWidth = 0.1f,
            duration = 0.5f,
            initialUpSpeed = 100f,
            horizontalRandomRange = 40f,
            gravity = 80f,
            fadeStartPercent = 0.4f,
            useScaleAnimation = false,
            initialScale = 1f,
            peakScale = 1f,
            scalePeakPercent = 0.2f,
            sizeMultiplier = 0.7f  // 0.7倍大小
        };
        
        [Header("Boss核心伤害样式")]
        public FloatingTextStyle bossCoreStyle = new FloatingTextStyle
        {
            textColor = new Color(1f, 0.2f, 0.1f, 1f), // 深红色
            outlineColor = new Color(0.6f, 0f, 0f, 1f), // 红色描边
            fontSize = 42f,
            isBold = true,
            outlineWidth = 0.3f,
            duration = 0.9f,
            initialUpSpeed = 180f,
            horizontalRandomRange = 30f,
            gravity = 200f,
            fadeStartPercent = 0.55f,
            useScaleAnimation = true,
            initialScale = 0.7f,
            peakScale = 1.3f,
            scalePeakPercent = 0.2f,
            sizeMultiplier = 1.3f  // 1.3倍大小
        };
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取指定类型的样式配置
        /// </summary>
        public FloatingTextStyle GetStyle(FloatingTextType type)
        {
            switch (type)
            {
                case FloatingTextType.Crit:
                    return critStyle;
                case FloatingTextType.Status:
                    return statusStyle;
                case FloatingTextType.BossShield:
                    return bossShieldStyle;
                case FloatingTextType.BossCore:
                    return bossCoreStyle;
                case FloatingTextType.Normal:
                default:
                    return normalStyle;
            }
        }
        
        /// <summary>
        /// 获取指定类型的优先级
        /// </summary>
        public int GetPriority(FloatingTextType type)
        {
            switch (type)
            {
                case FloatingTextType.Crit:
                    return critPriority;
                case FloatingTextType.Status:
                    return statusPriority;
                case FloatingTextType.BossShield:
                    return bossShieldPriority;
                case FloatingTextType.BossCore:
                    return bossCorePriority;
                case FloatingTextType.Normal:
                default:
                    return normalPriority;
            }
        }
        
        /// <summary>
        /// 获取指定类型的 Prefab
        /// </summary>
        public GameObject GetPrefab(FloatingTextType type)
        {
            switch (type)
            {
                case FloatingTextType.Crit:
                    return critPrefab != null ? critPrefab : normalPrefab;
                case FloatingTextType.Status:
                    return statusPrefab != null ? statusPrefab : normalPrefab;
                case FloatingTextType.BossShield:
                    return bossShieldPrefab != null ? bossShieldPrefab : normalPrefab;
                case FloatingTextType.BossCore:
                    return bossCorePrefab != null ? bossCorePrefab : normalPrefab;
                case FloatingTextType.Normal:
                default:
                    return normalPrefab;
            }
        }
    }
}