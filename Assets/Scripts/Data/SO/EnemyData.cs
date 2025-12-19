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
        
        [Tooltip("接触伤害（碰撞玩家时造成的伤害）")]
        [Min(0)]
        public int contactDamage = 30;
        
        [Tooltip("攻击间隔（秒，0表示只攻击一次如自爆怪）")]
        [Min(0f)]
        public float attackInterval = 1.0f;
        
        [Tooltip("是否为自爆型（碰撞后自毁）")]
        public bool isSuicide = false;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 行为设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("行为设置")]
        [Tooltip("敌人行为类型")]
        public EnemyBehaviorType behaviorType = EnemyBehaviorType.Chase;
        
        [Header("横穿屏幕设置（仅 CrossScreen 类型有效）")]
        [Tooltip("波浪线振幅")]
        [Range(0f, 3f)]
        public float waveAmplitude = 1.0f;
        
        [Tooltip("波浪线频率")]
        [Range(0.5f, 5f)]
        public float waveFrequency = 1.5f;
        
        [Tooltip("出界后存活时间（秒）")]
        [Range(0f, 3f)]
        public float outOfBoundsLifetime = 1.0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 击退设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("击退设置")]
        [Tooltip("是否可被击退")]
        public bool canBeKnockedBack = true;
        
        [Tooltip("击退力度倍率")]
        [Range(0f, 3f)]
        public float knockbackMultiplier = 1.0f;
        
        [Tooltip("被击退后的阻力")]
        [Range(0f, 10f)]
        public float knockbackDrag = 2.0f;
        
        [Tooltip("被击退后的僵直时间")]
        [Range(0f, 1f)]
        public float knockbackStunDuration = 0.2f;
        
        [Tooltip("僵直期间移动力度倍率")]
        [Range(0f, 1f)]
        public float knockbackStunMoveMultiplier = 0.3f;
        
        [Header("Drifter 特殊击退")]
        [Tooltip("Drifter 偏移角度")]
        [Range(0f, 90f)]
        public float drifterDeflectionAngle = 45f;
        
        [Tooltip("Drifter 击退力度倍率")]
        [Range(1f, 3f)]
        public float drifterKnockbackMultiplier = 1.5f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 视觉设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("视觉设置")]
        [Tooltip("最小缩放（血量为0时）")]
        [Range(0.1f, 0.5f)]
        public float minScale = 0.3f;
        
        [Tooltip("死亡淡出时长")]
        [Range(0.1f, 1f)]
        public float deathFadeDuration = 0.3f;
        
        [Header("Shader 参数")]
        public float normalFlowSpeed = 0.3f;
        public float normalNoiseScale = 1.0f;
        public float hitFlowSpeed = 2.0f;
        public float hitNoiseScale = 2.0f;
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

        [Header("宝箱怪特殊掉落")]
        [Tooltip("被击中时掉落金币")]
        public bool dropCoinOnHit = false;
        
        [Tooltip("每次被击中掉落的金币数")]
        [Min(0)]
        public int coinPerHit = 1;
        
        [Tooltip("死亡时爆出的金币数量")]
        [Min(0)]
        public int deathCoinBurst = 0;
        
        [Tooltip("低保经验值（玩家等级<12时）")]
        [Min(0)]
        public int lowLevelBonusXP = 0;
        
        [Tooltip("低保触发等级")]
        public int lowLevelThreshold = 12;
        
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
        
        /// <summary>
        /// 是否为横穿屏幕类型
        /// </summary>
        public bool IsCrossScreen => behaviorType == EnemyBehaviorType.CrossScreen;
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
        WeakBounce,
        
        /// <summary>无碰撞（宝箱怪）- 不与塔碰撞</summary>
        None
    }
    
    /// <summary>
    /// 敌人行为类型枚举
    /// </summary>
    public enum EnemyBehaviorType
    {
        /// <summary>追击 - 向玩家移动（默认）</summary>
        Chase,
        
        /// <summary>横穿屏幕 - 从一侧到另一侧（宝箱怪）</summary>
        CrossScreen
    }
}