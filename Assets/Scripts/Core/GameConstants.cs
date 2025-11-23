using UnityEngine;

namespace LightVsDecay.Core
{
    /// <summary>
    /// 游戏全局常量配置
    /// 对应 GDD 中的数值系统
    /// </summary>
    public static class GameConstants
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 激光系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>基础DPS（每秒伤害）</summary>
        public const float BASE_DPS = 100f;
        
        /// <summary>伤害判定频率（秒）- 对应0.1秒判定间隔</summary>
        public const float DAMAGE_TICK_RATE = 0.1f;
        
        /// <summary>单次伤害（DPS / 每秒判定次数）</summary>
        public const float DAMAGE_PER_TICK = BASE_DPS * DAMAGE_TICK_RATE; // = 10
        
        /// <summary>基础击退力</summary>
        public const float BASE_KNOCKBACK_FORCE = 10f;
        
        /// <summary>激光最大长度（Unity单位）</summary>
        public const float LASER_MAX_LENGTH = 15f;
        
        /// <summary>激光默认宽度</summary>
        public const float LASER_DEFAULT_WIDTH = 0.5f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 物理检测
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>敌人Layer名称</summary>
        public const string ENEMY_LAYER = "Enemy";
        
        /// <summary>Raycast检测间隔（秒）- 性能优化</summary>
        public const float RAYCAST_INTERVAL = 0.1f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Shader属性名（避免硬编码字符串）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public static class ShaderProperties
        {
            public static readonly int HitHeight = Shader.PropertyToID("_HitHeight");
            public static readonly int GlowColor = Shader.PropertyToID("_GlowColor");
            public static readonly int FlowSpeed = Shader.PropertyToID("_FlowSpeed");
            public static readonly int NoiseStrength = Shader.PropertyToID("_NoiseStrength");
        }
    }
}