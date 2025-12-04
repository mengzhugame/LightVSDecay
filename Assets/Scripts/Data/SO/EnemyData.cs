// ============================================================
// EnemyData.cs
// 文件位置: Assets/Scripts/Data/SO/EnemyData.cs
// 用途：单个敌人类型的配置数据（ScriptableObject）
// ============================================================

using UnityEngine;
using LightVsDecay.Core.Pool;

namespace LightVsDecay.Data.SO
{
    /// <summary>
    /// 敌人配置数据 (ScriptableObject)
    /// 每种敌人类型对应一个配置文件
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy_New", menuName = "LightVsDecay/Enemy Data", order = 1)]
    public class EnemyData : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 基础信息
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("基础信息")]
        [Tooltip("敌人类型")]
        public EnemyType type = EnemyType.Slime;
        
        [Tooltip("显示名称")]
        public string displayName = "粘液";
        
        [Tooltip("描述")]
        [TextArea(2, 4)]
        public string description = "基础敌人单位";
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 战斗属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("战斗属性")]
        [Tooltip("最大生命值")]
        [Min(1f)]
        public float maxHealth = 30f;
        
        [Tooltip("移动速度")]
        [Min(0.1f)]
        public float moveSpeed = 1.0f;
        
        [Tooltip("物理质量（影响击退效果）")]
        [Min(0.1f)]
        public float mass = 1.0f;
        
        [Tooltip("碰撞伤害（对玩家）")]
        public int contactDamage = 1;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 击退设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("击退设置")]
        [Tooltip("是否可以被击退")]
        public bool canBeKnockedBack = true;
        
        [Tooltip("击退力倍率（1.0=正常，0.5=难推，2.0=容易推）")]
        [Range(0f, 5f)]
        public float knockbackMultiplier = 1.0f;
        
        [Tooltip("击退阻力（越大停得越快）")]
        [Range(0f, 10f)]
        public float knockbackDrag = 2.0f;
        
        [Tooltip("受击后移动力减弱时间（秒）")]
        public float knockbackStunDuration = 0.3f;
        
        [Tooltip("受击后移动力减弱倍率")]
        [Range(0f, 1f)]
        public float knockbackStunMoveMultiplier = 0.3f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 特殊行为（Drifter专用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("Drifter 特殊设置")]
        [Tooltip("被击退时的横向偏移角度（度）")]
        public float drifterDeflectionAngle = 45f;
        
        [Tooltip("被击退时的额外力量倍率")]
        public float drifterKnockbackMultiplier = 2.0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 视觉设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("视觉设置")]
        [Tooltip("最小缩放比例（受伤后缩小到此值后死亡）")]
        [Range(0.1f, 0.5f)]
        public float minScale = 0.3f;
        
        [Tooltip("死亡淡出时间")]
        public float deathFadeDuration = 1.0f;
        
        [Header("Shader 抖动设置")]
        public float normalFlowSpeed = 1.0f;
        public float normalNoiseScale = 0.5f;
        public float hitFlowSpeed = 10.0f;
        public float hitNoiseScale = 5.0f;
        public float wobbleReturnSpeed = 5.0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 奖励设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("奖励设置")]
        [Tooltip("击杀获得的经验值")]
        [Min(0)]
        public int xpReward = 10;
        
        [Tooltip("击杀获得的金币")]
        [Min(0)]
        public int coinReward = 1;
        
        [Tooltip("击杀获得的大招能量")]
        [Min(0)]
        public int ultEnergyReward = 2;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 碰撞行为
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("碰撞行为")]
        [Tooltip("撞击玩家后的行为")]
        public EnemyCollisionBehavior collisionBehavior = EnemyCollisionBehavior.Suicide;
        
        [Tooltip("大怪被弹开时的力度")]
        public float bounceForce = 300f;
        
        [Tooltip("大怪被弹开后的僵直时间")]
        public float bounceStunDuration = 1.0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 便捷方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 是否为小型敌人（根据质量判断）
        /// </summary>
        public bool IsSmallEnemy => mass < 2.0f;
        
        /// <summary>
        /// 计算击杀所需时间（理论值，基于100DPS）
        /// </summary>
        public float EstimatedKillTime => maxHealth / 100f;
    }
    
    /// <summary>
    /// 敌人碰撞行为枚举
    /// </summary>
    public enum EnemyCollisionBehavior
    {
        /// <summary>自爆（小怪）- 立即销毁，播放特效</summary>
        Suicide,
        
        /// <summary>反弹（大怪）- 被弹开，进入僵直</summary>
        Bounce,
        
        /// <summary>微弱反弹（BOSS）- 稍微后退，无僵直</summary>
        WeakBounce
    }
}