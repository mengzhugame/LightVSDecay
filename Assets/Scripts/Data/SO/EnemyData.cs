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
        [Header("═══ 击退抗性 ═══")]
        [Tooltip("击退抗性 (0=无抗性，1=完全免疫)")]
        [Range(0f, 1f)]
        public float knockbackResistance = 0f;
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
        [Range(1f, 20f)]
        public float drifterKnockbackMultiplier = 5.0f;
        // 【新增】弹飞状态参数
        [Tooltip("最小弹飞时间（秒）- 防止立即恢复移动")]
        [Range(0.1f, 2f)]
        public float knockbackMinDuration = 0.5f;
        [Tooltip("Drifter 最大速度限制")]
        [Range(5f, 30f)]
        public float drifterMaxSpeed = 15f;
        [Tooltip("弹飞结束速度阈值 - 速度低于此值时恢复移动")]
        [Range(0.5f, 10f)]
        public float knockbackSpeedThreshold = 2.0f;
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
        // 死亡特殊行为（Ch2 熔岩怪）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("死亡分裂（分裂者专用）")]
        [Tooltip("死亡时是否分裂为小单位")]
        public bool splitOnDeath = false;

        [Tooltip("分裂后生成的敌人类型")]
        public EnemyType splitEnemyType = EnemyType.Slime;

        [Tooltip("分裂数量")]
        [Min(1)]
        public int splitCount = 2;

        [Tooltip("分裂子体的向外冲量速度（越大爆开感越强）")]
        [Min(0f)]
        public float splitImpulseSpeed = 4f;

        [Header("死亡留坑（爆炸者专用）")]
        [Tooltip("死亡时是否在原地生成熔岩水坑")]
        public bool spawnPuddleOnDeath = false;

        [Tooltip("生成的水坑敌人类型")]
        public EnemyType puddleEnemyType = EnemyType.LavaPuddle;

        [Tooltip("留坑大小倍率（1=正常；精英爆炸者填2.5）")]
        [Min(0.1f)]
        public float puddleSizeMultiplier = 1f;

        [Header("自爆范围伤害（爆炸者专用）")]
        [Tooltip("死亡/碰撞自爆时对周围的无差别伤害（0=无AoE）")]
        [Min(0)]
        public int explosionAoeDamage = 0;

        [Tooltip("自爆AoE半径（世界单位）")]
        [Min(0f)]
        public float explosionAoeRadius = 3f;

        [Header("静止障碍设置（水坑专用）")]
        [Tooltip("是否禁用受击闪烁（水坑不应有受击反馈）")]
        public bool disableHitFlash = false;

        [Tooltip("是否禁用击退（静止物体）")]
        public bool disableKnockback = false;

        [Header("远程炮手设置（RangedGunner 专用）")]
        [Tooltip("停驻位置：从屏幕顶部往下的比例（0=顶部，1=底部）")]
        [Range(0.1f, 0.9f)]
        public float gunnerStopYPercent = 0.6f;

        [Tooltip("射击间隔（秒）")]
        [Min(1f)]
        public float gunnerShootInterval = 8f;

        [Tooltip("换位时的横向移动范围（单位：米）")]
        [Min(0f)]
        public float gunnerRepositionRange = 2f;

        [Tooltip("弹道预制体（需挂 LavaProjectile 组件，放在 BossPollutionBall 层）")]
        public GameObject gunnerProjectilePrefab;

        [Tooltip("弹道飞行速度")]
        [Min(1f)]
        public float gunnerProjectileSpeed = 5f;

        [Tooltip("弹道命中伤害")]
        [Min(0)]
        public int gunnerProjectileDamage = 40;

        [Tooltip("弹道生命值（激光打爆所需伤害）")]
        [Min(1f)]
        public float gunnerProjectileHP = 20f;

        [Tooltip("弹道最大生命周期（秒）")]
        [Min(1f)]
        public float gunnerProjectileLifetime = 8f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 第三章：静止单位自动消失（IceWall 通用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("静止单位自动消失（IceWall / Stationary）")]
        [Tooltip("大于0时，Stationary 单位在存活N秒后自动消失（0=永不自动消失）")]
        [Min(0f)]
        public float autoDestroyTime = 0f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 第三章：极寒催化者（FrostCatalyst 专用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("催化暴走（FrostCatalyst 专用）")]
        [Tooltip("是否为催化者（死亡时触发范围暴走效果）")]
        public bool isCatalyst = false;

        [Tooltip("催化爆发半径（世界单位）")]
        [Min(0f)]
        public float catalystBurstRadius = 5f;

        [Tooltip("暴走状态持续时间（秒）")]
        [Min(0f)]
        public float catalystBurstDuration = 5f;

        [Tooltip("暴走速度加成倍率（1=无加成，2=速度翻倍）")]
        [Min(1f)]
        public float catalystSpeedMultiplier = 2f;

        [Tooltip("暴走期间受到伤害增加倍率（1=无加成，1.5=多受50%伤害）")]
        [Min(1f)]
        public float catalystDamageTakenMultiplier = 1.5f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 第三章：霜冻施法者（FrostCaster 专用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("霜冻施法者设置（FrostCaster 专用）")]
        [Tooltip("停驻位置：从屏幕顶部往下的比例（0=顶部，1=底部）")]
        [Range(0.1f, 0.9f)]
        public float frostcasterStopYPercent = 0.7f;

        [Tooltip("施法间隔（秒）")]
        [Min(1f)]
        public float frostcasterCastInterval = 10f;

        [Tooltip("每次施法召唤的冰墙数量（精英版可配置为随机上限，运行时取 1~此值）")]
        [Min(1)]
        public int frostcasterIceWallCount = 1;

        [Tooltip("是否随机冰墙数量（精英版：在 1~frostcasterIceWallCount 之间随机）")]
        public bool frostcasterRandomWallCount = false;

        [Tooltip("召唤的冰墙敌人类型")]
        public EnemyType frostcasterIceWallType = EnemyType.IceWall;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 第三章：冰甲卫士前置冰盾（EliteIceShieldGuard 专用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("前置冰盾设置（EliteIceShieldGuard 专用）")]
        [Tooltip("是否拥有前置冰盾（激光伤害在冰盾存在时完全重定向到冰盾）")]
        public bool hasIceShield = false;

        [Tooltip("冰盾最大 HP")]
        [Min(1f)]
        public float iceShieldMaxHP = 15000f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 碰撞行为
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("碰撞行为")]
        [Tooltip("撞击玩家后的行为")]
        public EnemyCollisionBehavior collisionBehavior = EnemyCollisionBehavior.Suicide;
        [Tooltip("是否为弹跳怪（进入屏幕后会与空气墙碰撞）")]
        public bool isBouncing = false;
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

        /// <summary>
        /// 是否为静止地形障碍
        /// </summary>
        public bool IsStationary => behaviorType == EnemyBehaviorType.Stationary;

        /// <summary>
        /// 是否为远程炮手
        /// </summary>
        public bool IsRangedGunner => behaviorType == EnemyBehaviorType.RangedGunner;

        /// <summary>
        /// 是否为霜冻施法者
        /// </summary>
        public bool IsFrostCaster => behaviorType == EnemyBehaviorType.FrostCaster;
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
        CrossScreen,

        /// <summary>静止 - 不移动，作为地形障碍（熔岩水坑）</summary>
        Stationary,

        /// <summary>远程炮手 - 进入屏幕后停留在上方，定时射击</summary>
        RangedGunner,

        /// <summary>霜冻施法者 - 进入屏幕后停驻，定时召唤冰墙</summary>
        FrostCaster,
    }
}