// ============================================================
// EnemyBlob.cs (修复版)
// 文件位置: Assets/Scripts/Logic/Enemy/EnemyBlob.cs
// 用途：敌人主逻辑 - 修复 Shader 属性名
// ============================================================

using UnityEngine;
using System.Collections;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Audio;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Player;
using LightVsDecay.Logic.Statistics;
using LightVsDecay.UI.FloatingText;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// 黑油怪物主逻辑
    /// 配置数据从 EnemyData ScriptableObject 读取
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class EnemyBlob : MonoBehaviour, IPoolable
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 数据配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("数据配置")]
        [Tooltip("敌人数据库")]
        [SerializeField] private EnemyDatabase enemyDatabase;
        
        [Header("敌人类型")]
        [SerializeField] private EnemyType enemyType = EnemyType.Slime;
        // 精英怪视觉效果
        private GameObject eliteEffectInstance;
        private Color originalColor;
        [Header("视觉组件")]
        [SerializeField] private SpriteRenderer bodySprite;
        [SerializeField] private EnemyEyes eyesController;
        [SerializeField] private EnemyEyes[] extraEyes; // 多眼怪用：额外眼睛列表（分裂怪3只眼/精英分裂怪7只眼等）
        [SerializeField] private Transform[] decorations;
        [Header("受击闪烁设置")]
        [SerializeField] private float hitFlashDuration = 0.15f;        // 闪烁持续时间
        [SerializeField] private float hitSpeedBoostMultiplier = 3f;    // 抖动速度倍率
        [SerializeField] private Color defaultHitColor = Color.yellow;  // 默认受击颜色（激光初始黄色）
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时配置缓存（从 EnemyData 加载）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private EnemyData data;
        
        // 战斗属性
        private float maxHealth = 30f;
        private float baseMoveSpeed = 1.0f;
        private float mass = 1.0f;
        
        // 击退设置
        private bool canBeKnockedBack = true;
        private float knockbackMultiplier = 1.0f;
        private float knockbackDrag = 2.0f;
        private float knockbackStunDuration = 0.3f;
        private float knockbackStunMoveMultiplier = 0.3f;
        private float knockbackResistance = 0f;
        // Drifter 特殊设置
        private float drifterDeflectionAngle = 45f;
        private float drifterKnockbackMultiplier = 2.0f;
        // 【新增】数据破碎击杀标记
        private bool wasImpairedOnDeath = false;  // 死亡时是否处于受损状态
        // 视觉设置
        private float minScale = 0.3f;
        private float deathFadeDuration = 1.0f;
        private float normalFlowSpeed = 1.0f;
        private float hitFlowSpeed = 10.0f;
        private float wobbleReturnSpeed = 5.0f;
        private Coroutine hitFlashCoroutine;
        private Color currentHitColor;
        // 奖励
        private int xpReward = 10;
        private int coinReward = 1;
        // 行为设置
        private EnemyBehaviorType behaviorType = EnemyBehaviorType.Chase;
        // Boss 汲取融合：覆盖追击目标（null = 追玩家塔）
        private Transform overrideTarget = null;
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Frost 照射时间追踪【新增】
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private float frostExposureTime = 0f;
        private float frostExposureResetTimer = 0f;
        private const float FROST_EXPOSURE_RESET_DELAY = 0.2f; // 脱离激光0.2秒后重置
// 横穿屏幕相关
        private Vector3 crossScreenTarget;
        private Vector3 crossScreenStartPos;
        private float crossScreenProgress = 0f;
        private float waveAmplitude = 1.0f;
        private float waveFrequency = 1.5f;
        private float outOfBoundsLifetime = 1.0f;
        private float outOfBoundsTimer = 0f;
        private bool isOutOfBounds = false;

// 宝箱怪掉落
        private bool dropCoinOnHit = false;
        private int coinPerHit = 1;
        private int deathCoinBurst = 0;
        private int lowLevelBonusXP = 0;
        private int lowLevelThreshold = 12;

// Ch2 AI 组件（炮手）
        private LavaGunnerAI gunnerAI;

// Ch3 AI 组件（霜冻施法者）
        private FrostcasterAI frostcasterAI;

// Ch3 冰盾
        private IceShieldController iceShield;

// Ch3 催化者
        private bool isCatalyst = false;
        private float catalystBurstRadius = 5f;
        private float catalystBurstDuration = 5f;
        private float catalystSpeedMultiplier = 2f;
        private float catalystDamageTakenMultiplier = 1.5f;
        private float catalystIceWallHatchSpeedMult = 2f;

// Ch3 暴走状态
        private bool isBerserking = false;
        private float berserkSpeedMultiplier = 1f;
        private float berserkDamageTakenMultiplier = 1f;
        private Coroutine berserkCoroutine;

// Ch3 静止自动消失 + IceWall 周期性孵化
        private float autoDestroyTime = 0f;
        private float stationaryTimer = 0f;
        private float iceWallHatchInterval = 0f;
        private float iceWallHatchTimer = 0f;
        private EnemyType iceWallHatchType = EnemyType.Slime;

// Ch2 死亡特殊行为
        private bool splitOnDeath = false;
        private EnemyType splitEnemyType = EnemyType.Slime;
        private int splitCount = 2;
        private float splitImpulseSpeed = 4f;
        private bool spawnPuddleOnDeath = false;
        private EnemyType puddleEnemyType = EnemyType.LavaPuddle;
        private float puddleSizeMultiplier = 1f;
        private int   explosionAoeDamage   = 0;
        private float explosionAoeRadius   = 3f;
        private bool disableHitFlash = false;
        
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Drifter 弹飞状态
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private bool isBeingKnockedBack = false;      // 是否正在弹飞
        private float knockbackStartTime = 0f;        // 弹飞开始时间
        private float knockbackMinDuration = 0.3f;    // 最小弹飞时间（防止立即恢复）
        private float knockbackSpeedThreshold = 2.0f; // 速度低于此值视为弹飞结束
        private bool hasFullyEnteredScreen = false;
        private float drifterMaxSpeed = 15f;
// 僵直时的 Shader 参数缓存
        private float cachedFlowSpeed = 0f;
        private float cachedNoiseScale = 0f;    
        // 精英怪标记
        private bool isElite = false;
        // 精英怪经验倍率
        private const float ELITE_XP_MULTIPLIER = 5f;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // IPoolable 实现
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public string PoolKey => enemyType.ToString();
        // 【新增】最大血量属性（用于 BattleStatistics 统计）
        public float MaxHealth => maxHealth;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float currentHealth;
        private Transform targetTower;
        private Rigidbody2D rb;
        private CircleCollider2D circleCollider;
        private Vector3 originalScale;
        private bool isDead = false;
        private bool killedByExplosion = false;  // 【新增】是否被爆炸杀死
        // 接触伤害冷却（防止 OnCollisionStay2D 每帧重复造成伤害）
        private float lastContactDamageTime = -999f;
        private const float CONTACT_DAMAGE_INTERVAL = 0.5f;
        private bool killedByExecution = false;  // 【新增】是否被处决秒杀
        private Material[] bodyMaterials;
        private bool isBeingHit = false;
        private float targetFlowSpeed;
        private float lastHitTime;
        
        private Coroutine deathCoroutine;
        
        // 速度倍率（狂暴模式）
        private float speedMultiplier = 1f;
// ========== 新增：Frost 减速状态 ==========
        private float frostSpeedMultiplier = 1f;  // Frost 减速倍率
        private bool isFrozen = false;             // 是否完全冰冻
        private FrostDebuff frostDebuff;           // 缓存 FrostDebuff 组件
        private CatalystBerserkDebuff catalystBerserkDebuff; // 缓存暴走视觉组件
        
        private int shieldLayer;
        private int towerLayer;
        
        private DifficultyModifiers waveModifiers = DifficultyModifiers.Default;
        /// <summary>
        /// 是否为精英怪
        /// </summary>
        public bool IsElite => isElite;
        /// <summary>
        /// 是否已死亡
        /// </summary>
        public bool IsDead => isDead;
        /// <summary>
        /// 获取敌人类型的属性访问器 (修复 CS1061 错误)
        /// </summary>
        public EnemyType Type => enemyType;

        /// <summary>当前 EnemyData 配置（供 LavaGunnerAI 读取）</summary>
        public EnemyData Data => data;

        /// <summary>当前追击目标塔（供 LavaGunnerAI 瞄准）</summary>
        public Transform TargetTower => targetTower;

        /// <summary>是否为静止障碍（熔浆液等），用于激光穿透阻断判断</summary>
        public bool IsStationary => behaviorType == EnemyBehaviorType.Stationary;

        /// <summary>是否拥有存活的前置冰盾（精英冰甲卫士专用，用于激光穿透阻断）</summary>
        public bool HasActiveIceShield => iceShield != null && iceShield.IsActive;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Layer 切换（弹跳怪入境签证）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private bool isBouncing = false;
        private bool hasEnteredScreen = false;
        private int enemyLayerIndex;
        private int bouncingEnemyLayerIndex;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 【新增】初始化 FrostDebuff 组件
            frostDebuff = GetComponent<FrostDebuff>();
            if (frostDebuff == null)
            {
                frostDebuff = gameObject.AddComponent<FrostDebuff>();
            }
            // 暴走视觉组件（催化者死亡爆发使用，需在 Prefab 上手动挂载，否则无视觉效果）
            catalystBerserkDebuff = GetComponent<CatalystBerserkDebuff>();
            // Ch3 组件缓存
            frostcasterAI = GetComponent<FrostcasterAI>();
            iceShield = GetComponentInChildren<IceShieldController>(true);
            rb = GetComponent<Rigidbody2D>();
            circleCollider = GetComponent<CircleCollider2D>();
            originalScale = transform.localScale;
            
// 获取所有需要闪白的材质实例
            if (decorations != null && decorations.Length > 0)
            {
                bodyMaterials = new Material[decorations.Length];
                for (int i = 0; i < decorations.Length; i++)
                {
                    if (decorations[i] != null)
                    {
                        SpriteRenderer sr = decorations[i].GetComponent<SpriteRenderer>();
                        if (sr != null)
                        {
                            // 使用 .material 会自动创建实例
                            bodyMaterials[i] = sr.material;
                        }
                    }
                }
            }
            else if (bodySprite != null)
            {
                // 回退：只有 bodySprite
                bodyMaterials = new Material[] { bodySprite.material };
            }
            
            // 加载配置
            LoadDataFromConfig();
            ConfigureRigidbody();
            // 【新增】缓存 Layer（避免每次碰撞都调用 NameToLayer）
            shieldLayer = LayerMask.NameToLayer("Shield");
            towerLayer = LayerMask.NameToLayer("Tower");
            
            enemyLayerIndex = LayerMask.NameToLayer(GameConstants.ENEMY_LAYER);
            bouncingEnemyLayerIndex = LayerMask.NameToLayer(GameConstants.BOUNCING_ENEMY_LAYER);

            // 缓存可选 AI 组件（炮手专用）
            gunnerAI = GetComponent<LavaGunnerAI>();
        }
        
        private void Start()
        {
            FindTower();
        }
        
        private void Update()
        {
            if (isDead) return;
            
            UpdateShaderWobble();
            UpdateFrostExposure();
        }
        
        private void FixedUpdate()
        {
            if (isDead) return;
            // 【新增】冰冻时强制停止所有物理运动
            if (isFrozen)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                return;  // 跳过所有移动逻辑
            }
            // 【新增】检查弹飞状态
            if (enemyType == EnemyType.Drifter)
            {
                CheckKnockbackEnd();
                // 【新增】速度限制（防止异常加速）
                if (rb.velocity.magnitude > drifterMaxSpeed)
                {
                    rb.velocity = rb.velocity.normalized * drifterMaxSpeed;
                }
                // 弹飞中，跳过移动逻辑
                if (isBeingKnockedBack)
                {
                    ApplyFrostDragDuringKnockback();  // 【新增】
                    return;
                }
            }
            // Ch3：Stationary 单位自动消失计时 + IceWall 周期性孵化
            if (behaviorType == EnemyBehaviorType.Stationary && autoDestroyTime > 0f)
            {
                stationaryTimer += Time.fixedDeltaTime;

                // IceWall：周期性孵化（每 iceWallHatchInterval 秒生成一只小怪）
                if (enemyType == EnemyType.IceWall && iceWallHatchInterval > 0f)
                {
                    iceWallHatchTimer += Time.fixedDeltaTime;
                    if (iceWallHatchTimer >= iceWallHatchInterval)
                    {
                        iceWallHatchTimer -= iceWallHatchInterval; // 防止时间漂移
                        SpawnIceWallHatchling();
                    }
                }

                if (stationaryTimer >= autoDestroyTime)
                {
                    Die();
                    return;
                }
            }

            MoveTowardsTower();
        }

        // 在 Update 中添加：
        private void UpdateFrostExposure()
        {
            if (frostExposureResetTimer > 0f)
            {
                frostExposureResetTimer -= Time.deltaTime;
            }
            else if (frostExposureTime > 0f)
            {
                // 脱离激光后重置照射时间
                frostExposureTime = 0f;
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置加载
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 从 EnemyData 加载配置
        /// </summary>
        private void LoadDataFromConfig()
        {
            // 尝试从数据库获取配置
            if (enemyDatabase != null)
            {
                data = enemyDatabase.GetData(enemyType);
            }
            
            // 应用配置（有默认值保护）
            if (data != null)
            {
                // 战斗属性
                maxHealth = data.maxHealth;
                baseMoveSpeed = data.moveSpeed;
                mass = data.mass;
                
                // 击退设置
                canBeKnockedBack = data.canBeKnockedBack;
                knockbackMultiplier = data.knockbackMultiplier;
                knockbackDrag = data.knockbackDrag;
                knockbackStunDuration = data.knockbackStunDuration;
                knockbackStunMoveMultiplier = data.knockbackStunMoveMultiplier;
                knockbackResistance = data.knockbackResistance;
                // 弹跳设置
                isBouncing = data.isBouncing;
                // Drifter 特殊
                drifterDeflectionAngle = data.drifterDeflectionAngle;
                drifterKnockbackMultiplier = data.drifterKnockbackMultiplier;
                knockbackMinDuration = data.knockbackMinDuration;
                knockbackSpeedThreshold = data.knockbackSpeedThreshold;
                drifterMaxSpeed = data.drifterMaxSpeed;
                // 视觉
                minScale = data.minScale;
                deathFadeDuration = data.deathFadeDuration;
                normalFlowSpeed = data.normalFlowSpeed;
                hitFlowSpeed = data.hitFlowSpeed;
                wobbleReturnSpeed = data.wobbleReturnSpeed;
                
                // 奖励
                xpReward = data.xpReward;
                coinReward = data.coinReward;
                // 行为设置
                behaviorType = data.behaviorType;
                waveAmplitude = data.waveAmplitude;
                waveFrequency = data.waveFrequency;
                outOfBoundsLifetime = data.outOfBoundsLifetime;

// 宝箱怪掉落
                dropCoinOnHit = data.dropCoinOnHit;
                coinPerHit = data.coinPerHit;
                deathCoinBurst = data.deathCoinBurst;
                lowLevelBonusXP = data.lowLevelBonusXP;
                lowLevelThreshold = data.lowLevelThreshold;

// Ch2 死亡特殊行为
                splitOnDeath = data.splitOnDeath;
                splitEnemyType = data.splitEnemyType;
                splitCount = data.splitCount;
                splitImpulseSpeed = data.splitImpulseSpeed;
                spawnPuddleOnDeath    = data.spawnPuddleOnDeath;
                puddleEnemyType       = data.puddleEnemyType;
                puddleSizeMultiplier  = data.puddleSizeMultiplier;
                explosionAoeDamage    = data.explosionAoeDamage;
                explosionAoeRadius    = data.explosionAoeRadius;
                disableHitFlash = data.disableHitFlash;

                // disableKnockback 覆盖 canBeKnockedBack（静止障碍不可被推动）
                if (data.disableKnockback)
                    canBeKnockedBack = false;

// Ch3 催化者
                isCatalyst = data.isCatalyst;
                catalystBurstRadius = data.catalystBurstRadius;
                catalystBurstDuration = data.catalystBurstDuration;
                catalystSpeedMultiplier = data.catalystSpeedMultiplier;
                catalystDamageTakenMultiplier = data.catalystDamageTakenMultiplier;
                catalystIceWallHatchSpeedMult = data.catalystIceWallHatchSpeedMult;
// Ch3 静止自动消失 + IceWall 周期性孵化
                autoDestroyTime = data.autoDestroyTime;
                iceWallHatchInterval = data.iceWallHatchInterval;
                iceWallHatchType = data.iceWallHatchType;
            }
            // 否则使用默认值（已在字段声明时初始化）
        }
        
        private void ConfigureRigidbody()
        {
            rb.gravityScale = 0;
            rb.mass = mass;
            rb.angularDrag = 0.5f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // 静止障碍（水坑）：设为 Kinematic，物理系统无法推动
            if (behaviorType == EnemyBehaviorType.Stationary)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.drag = 0f;
                rb.angularDrag = 0f;
            }
            // Drifter 特殊配置：低阻力，保持动量
            else if (behaviorType == EnemyBehaviorType.Chase && enemyType == EnemyType.Drifter)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.drag = 0f;
                rb.angularDrag = 0f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.drag = knockbackDrag;
                rb.angularDrag = 0.5f;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 对象池接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void OnSpawn()
        {
            isDead = false;
            killedByExplosion = false;  // 重置爆炸死亡标记
            killedByExecution = false;  // 重置处决标记
            lastContactDamageTime = -999f;
            wasImpairedOnDeath = false;// 重置数据破碎标记
            isElite = false;// 重置精英状态
            waveModifiers = DifficultyModifiers.Default; // 重置波次难度为默认（等待 WaveManager 设置）
            
            // 使用配置数据的原始值
            if (data != null)
            {
                maxHealth = data.maxHealth;
                baseMoveSpeed = data.moveSpeed;
                if (rb != null) rb.mass = data.mass;
            }
            
            currentHealth = maxHealth;
            transform.localScale = originalScale;
            speedMultiplier = 1f;
            
            // Stationary 障碍物（熔浆液用 PolygonCollider2D，冰墙等）全部设为 Trigger
            // 效果：怪物可穿越（无物理响应），Raycast 仍能检测到（queriesHitTriggers=true），激光依然被阻挡
            if (behaviorType == EnemyBehaviorType.Stationary)
            {
                foreach (var col in GetComponents<Collider2D>())
                    col.isTrigger = true;
            }
            // Ch3：冰墙注册到全局上限追踪器
            if (enemyType == EnemyType.IceWall)
                IceWallTracker.Register(this);
            if (circleCollider != null)
            {
                circleCollider.enabled = true;
            }

            if (rb != null)
            {
                // 静止障碍恢复 Kinematic（对象池复用时重置）
                rb.bodyType = (behaviorType == EnemyBehaviorType.Stationary)
                    ? RigidbodyType2D.Kinematic
                    : RigidbodyType2D.Dynamic;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = true;
            }

            ResetShaderState();
            ResetVisuals();
            FindTower();
            // 【新增】重置 Frost 状态
            frostSpeedMultiplier = 1f;
            isFrozen = false;
            if (frostDebuff != null)
            {
                frostDebuff.ResetDebuff();
            }
            // 重置暴走视觉状态
            catalystBerserkDebuff?.ResetBerserk();
            // 重置横穿屏幕状态
            crossScreenTarget = Vector3.zero;
            crossScreenStartPos = Vector3.zero;
            crossScreenProgress = 0f;
            isOutOfBounds = false;
            outOfBoundsTimer = 0f;
            // 【新增】重置弹飞状态
            isBeingKnockedBack = false;
            knockbackStartTime = 0f;
            hasFullyEnteredScreen = false;
            // 【新增】弹跳怪入境签证：出生时使用 Enemy Layer
            hasEnteredScreen = false;
            if (isBouncing)
            {
                gameObject.layer = enemyLayerIndex;
            }

            // 炮手 AI 激活
            if (behaviorType == EnemyBehaviorType.RangedGunner && gunnerAI != null)
            {
                gunnerAI.OnBlobSpawned();
            }

            // Ch3：重置暴走状态
            if (berserkCoroutine != null)
            {
                StopCoroutine(berserkCoroutine);
                berserkCoroutine = null;
            }
            isBerserking = false;
            berserkSpeedMultiplier = 1f;
            berserkDamageTakenMultiplier = 1f;
            stationaryTimer = 0f;
            iceWallHatchTimer = 0f;

            // Ch3：霜冻施法者 AI 激活
            if (behaviorType == EnemyBehaviorType.FrostCaster && frostcasterAI != null)
            {
                frostcasterAI.OnBlobSpawned();
            }

            // Ch3：冰盾初始化
            if (iceShield != null && data != null && data.hasIceShield)
            {
                iceShield.Initialize(data.iceShieldMaxHP);
            }
            else if (iceShield != null)
            {
                iceShield.Deactivate();
            }
        }
        /// <summary>
        /// 应用难度系数（生成时调用）
        /// 现在使用波次难度而非时间难度
        /// </summary>
        private void ApplyDifficultyModifiers()
        {
            if (data == null) return;
            
            // 应用波次难度到血量
            maxHealth = Mathf.RoundToInt(data.maxHealth * waveModifiers.hpMultiplier);
            
            // 应用波次难度到速度（有上限保护）
            baseMoveSpeed = data.moveSpeed * waveModifiers.speedMultiplier;
            
            // 应用波次难度到质量
            if (rb != null)
            {
                rb.mass = data.mass * waveModifiers.massMultiplier;
            }
            
            // 重置当前血量为最大值
            currentHealth = maxHealth;
        }
        public void OnDespawn()
        {
            // 【新增】停止闪烁协程
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }
            if (deathCoroutine != null)
            {
                StopCoroutine(deathCoroutine);
                deathCoroutine = null;
            }
            
            ForEachEye(e => e.StopBlink());
            
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;
            }

            // Ch3：冰墙从全局上限追踪器注销
            if (enemyType == EnemyType.IceWall)
                IceWallTracker.Unregister(this);
            // 重置所有碰撞体的 isTrigger 状态（含 Stationary 设置过的）
            foreach (var col in GetComponents<Collider2D>())
                col.isTrigger = false;

            isDead = true;
            ResetVisuals();

            // 炮手 AI 停止
            if (gunnerAI != null)
                gunnerAI.OnBlobDeactivated();

            // Ch3：霜冻施法者 AI 停止
            if (frostcasterAI != null)
                frostcasterAI.OnBlobDeactivated();

            // Ch3：停止暴走协程
            if (berserkCoroutine != null)
            {
                StopCoroutine(berserkCoroutine);
                berserkCoroutine = null;
            }
            isBerserking = false;
            berserkSpeedMultiplier = 1f;
            berserkDamageTakenMultiplier = 1f;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void FindTower()
        {
            GameObject tower = GameObject.FindGameObjectWithTag("Tower");
            if (tower != null)
            {
                targetTower = tower.transform;
            }
        }
        
        private void ResetShaderState()
        {
            if (bodyMaterials != null)
            {
                foreach (var mat in bodyMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed, normalFlowSpeed);
                    }
                }
            }
        }
        
        private void ResetVisuals()
        {
            // 【修改】重置所有材质
            if (bodyMaterials != null)
            {
                foreach (var mat in bodyMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetFloat(GameConstants.ShaderProperties.LiquidAlpha, 1.0f);
                        mat.SetFloat(GameConstants.ShaderProperties.LiquidHitIntensity, 0f);
                    }
                }
            }
            SetEyesAlpha(1f);
            
            foreach (Transform decoration in decorations)
            {
                if (decoration != null)
                {
                    SpriteRenderer sr = decoration.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Color c = sr.color;
                        c.a = 1f;
                        sr.color = c;
                    }
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 移动逻辑
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void MoveTowardsTower()
        {
            // 【删除重复检查】冰冻检查已在 FixedUpdate 中处理
            if (targetTower == null) return;

            // 根据行为类型选择移动方式
            if (behaviorType == EnemyBehaviorType.CrossScreen)
            {
                MoveCrossScreen();
            }
            else if (behaviorType == EnemyBehaviorType.Stationary)
            {
                // 静止障碍：强制速度为零（Kinematic 时此处为冗余保护）
                rb.velocity = Vector2.zero;
            }
            else if (behaviorType == EnemyBehaviorType.RangedGunner)
            {
                // LavaGunnerAI 全权控制 Rigidbody，此处不干涉
            }
            else if (behaviorType == EnemyBehaviorType.FrostCaster)
            {
                // FrostcasterAI 全权控制 Rigidbody，此处不干涉
            }
            else
            {
                MoveChase();
            }
        }
        /// <summary>
        /// 追击移动（原逻辑）
        /// </summary>
        /// <summary>
        /// 设置移动覆盖目标（用于 Boss 汲取融合，让召唤小怪向 Boss 聚拢而非追玩家塔）。
        /// 传入 null 可解除覆盖，恢复默认追塔行为。
        /// </summary>
        public void SetOverrideTarget(Transform target) { overrideTarget = target; }

        private void MoveChase()
        {
            Transform chaseTarget = (overrideTarget != null) ? overrideTarget : targetTower;
            if (chaseTarget == null) return;

            Vector2 direction = (chaseTarget.position - transform.position).normalized;

            float currentMoveSpeed = baseMoveSpeed * speedMultiplier * frostSpeedMultiplier * berserkSpeedMultiplier;
            float moveForce = currentMoveSpeed * 10f;

            float timeSinceHit = Time.time - lastHitTime;
            if (timeSinceHit < knockbackStunDuration)
            {
                moveForce *= knockbackStunMoveMultiplier;
            }

            rb.AddForce(direction * moveForce, ForceMode2D.Force);

            if (rb.velocity.magnitude > currentMoveSpeed * 2f)
            {
                rb.velocity = rb.velocity.normalized * currentMoveSpeed * 2f;
            }
        }

        /// <summary>
        /// 横穿屏幕移动（宝箱怪）
        /// </summary>
        private void MoveCrossScreen()
        {
            if (crossScreenTarget == Vector3.zero) return;
            
            // 计算横向进度
            float totalDistance = Vector3.Distance(crossScreenStartPos, crossScreenTarget);
            float currentMoveSpeed = baseMoveSpeed * speedMultiplier * frostSpeedMultiplier;
            
            crossScreenProgress += (currentMoveSpeed / totalDistance) * Time.fixedDeltaTime;
            crossScreenProgress = Mathf.Clamp01(crossScreenProgress);
            
            // 基础位置（直线插值）
            Vector3 basePos = Vector3.Lerp(crossScreenStartPos, crossScreenTarget, crossScreenProgress);
            
            // 波浪线偏移（正弦波）
            float waveOffset = Mathf.Sin(crossScreenProgress * Mathf.PI * 2f * waveFrequency) * waveAmplitude;
            
            // 计算垂直于移动方向的偏移方向
            Vector3 moveDir = (crossScreenTarget - crossScreenStartPos).normalized;
            Vector3 perpendicular = new Vector3(-moveDir.y, moveDir.x, 0f);
            
            // 最终位置
            Vector3 finalPos = basePos + perpendicular * waveOffset;
            
            // 使用 Rigidbody 移动（保持物理交互）
            rb.MovePosition(finalPos);
            
            // 检查是否到达目标（出界）
            if (crossScreenProgress >= 1f)
            {
                OnReachCrossScreenTarget();
            }
        }

        /// <summary>
        /// 到达横穿目标（出界销毁，不给奖励）
        /// </summary>
        private void OnReachCrossScreenTarget()
        {
            if (isOutOfBounds) return;
            isOutOfBounds = true;
            
            // 延迟销毁（给点缓冲时间）
            StartCoroutine(OutOfBoundsDestroy());
        }

        private System.Collections.IEnumerator OutOfBoundsDestroy()
        {
            yield return new WaitForSeconds(outOfBoundsLifetime);
            
            // 不触发死亡事件，不给奖励，直接回收
            ReturnToPool();
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 受到伤害
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <param name="knockbackForce">击退力</param>
        /// <param name="isCrit">是否暴击</param>
        public void TakeDamage(float damage, Vector2 knockbackForce, bool isCrit = false,
            bool fromExplosion = false, DamageSource damageSource = DamageSource.MainLaser,
            bool isShatter = false)
        {
            if (isDead) return;
            // 静止地形障碍（如熔浆液）完全免疫所有伤害
            if (behaviorType == EnemyBehaviorType.Stationary) return;

            // Ch3：冰盾存在时，激光伤害重定向到冰盾（减免50%），本体保留20%物理推力
            if (iceShield != null && iceShield.IsActive)
            {
                iceShield.TakeDamage(damage * 0.5f);
                if (canBeKnockedBack)
                    ApplyKnockbackByType(knockbackForce * 0.2f);
                return;
            }

            // Ch3：暴走状态下受到伤害增加
            if (isBerserking)
                damage *= berserkDamageTakenMultiplier;

            // 【新增】计算有效伤害和溢出伤害
            float effectiveDamage = Mathf.Min(currentHealth, damage);
            float overkillDamage = damage - effectiveDamage;
            // 【修改】如果来自爆炸，伤害来源强制设为 Explosion
            DamageSource actualSource = fromExplosion ? DamageSource.Explosion : damageSource;
            // 【新增】上报伤害数据到 BattleStatistics
            if (BattleStatistics.Instance != null)
            {
                BattleStatistics.Instance.RecordDamage(effectiveDamage, overkillDamage, enemyType, actualSource, isCrit);
            }
            // 【修正】仅在爆炸伤害足以致死时标记，避免非致死爆炸污染 flag
            // （若仅受伤存活，后续被激光补刀时不应跳过死亡特效）
            if (fromExplosion && (currentHealth - damage) <= 0)
            {
                killedByExplosion = true;
            }
            // 【新增】显示伤害飘字（disableHitFlash 时同时屏蔽飘字，用于 LavaPuddle 等地形障碍）
            if (!disableHitFlash && FloatingTextManager.Instance != null)
            {
                if (isShatter)
                    FloatingTextManager.Instance.ShowShatterDamage(transform.position, damage, isCrit);
                else if (actualSource == DamageSource.Chain)
                    FloatingTextManager.Instance.ShowChainDamage(transform.position, damage, isCrit);
                else if (actualSource == DamageSource.Explosion)
                    FloatingTextManager.Instance.ShowExplosionDamage(transform.position, damage, isCrit);
                else
                    FloatingTextManager.Instance.ShowDamage(transform.position, damage, isCrit);
            }
            currentHealth -= damage;
            lastHitTime = Time.time;
            
            // 根据敌人类型和配置处理击退
            if (canBeKnockedBack)
            {
                ApplyKnockbackByType(knockbackForce);
            }
            
            TriggerHitEffect();

            if (!disableHitFlash)
            {
                ForEachEye(e => e.TriggerSquint());
            }
            
            // 缩放
            float healthRatio = currentHealth / maxHealth;
            float newScale = Mathf.Lerp(minScale, 1f, healthRatio);
            transform.localScale = originalScale * newScale;
            
            if (currentHealth <= 0 || newScale <= minScale)
            {
                Die();
            }
            // 【新增】宝箱怪被击中掉金币
            if (dropCoinOnHit && coinPerHit > 0)
            {
                // 触发金币掉落事件（复用 AddCoins）
                if (ProgressManager.Instance != null)
                {
                    ProgressManager.Instance.AddCoins(coinPerHit);
                }
    
                // TODO: 播放金币飞出特效
                // VFXPoolManager.Instance?.PlayCoinDrop(transform.position);
            }
        }
        
        /// <summary>
        /// 根据敌人类型应用不同的击退效果
        /// </summary>
        private void ApplyKnockbackByType(Vector2 knockbackForce)
        {
            // 【新增】Drifter 弹飞中不接受新的击退（防止速度叠加）
            if (enemyType == EnemyType.Drifter && isBeingKnockedBack)
            {
                return; // 忽略击退，只受伤不加速
            }
            // 计算基础击退力（考虑质量缩放）
            float massScale = 1f;
            if (rb.mass > GameConstants.KNOCKBACK_MASS_THRESHOLD)
            {
                massScale = Mathf.Clamp(
                    GameConstants.KNOCKBACK_MASS_SCALE / rb.mass,
                    GameConstants.KNOCKBACK_SCALE_MIN,
                    GameConstants.KNOCKBACK_SCALE_MAX
                );
            }
            
            Vector2 finalForce;
            
            // Drifter 特殊处理：随机往左后或右后漂移
            if (enemyType == EnemyType.Drifter)
            {
                // 【关键】只有完全入境才触发弹飞状态
                if (hasFullyEnteredScreen)
                {
                    EnterKnockbackState();
                }
    
                // 计算偏移方向
                float deflectionDirection = Random.value > 0.5f ? 1f : -1f;
                float angleRad = drifterDeflectionAngle * Mathf.Deg2Rad * deflectionDirection;
    
                float cos = Mathf.Cos(angleRad);
                float sin = Mathf.Sin(angleRad);
                Vector2 deflectedForce = new Vector2(
                    knockbackForce.x * cos - knockbackForce.y * sin,
                    knockbackForce.x * sin + knockbackForce.y * cos
                );
    
                finalForce = deflectedForce * massScale * knockbackMultiplier * drifterKnockbackMultiplier;
    
                // 使用 Impulse 产生瞬间弹飞
                rb.AddForce(finalForce, ForceMode2D.Impulse);
    
#if UNITY_EDITOR
                if(showDebugInfo)
                    GameLogger.Log($"[EnemyBlob] Drifter 弹飞! Force: {finalForce.magnitude:F1}, FullyEntered: {hasFullyEnteredScreen}");
#endif
    
                return; // 提前返回
            }
            else
            {
                finalForce = knockbackForce * massScale * knockbackMultiplier;
                float impulseRatio = 0.3f; // 30% 的力作为瞬时冲击
                rb.AddForce(finalForce * (1f - impulseRatio), ForceMode2D.Force);
                rb.AddForce(finalForce * impulseRatio, ForceMode2D.Impulse);
            }
        }
        /// <summary>
        /// 进入弹飞状态（暂停移动AI）
        /// </summary>
        private void EnterKnockbackState()
        {
            // 【关键】只有完全入境的 Drifter 才能弹飞
            if (!hasFullyEnteredScreen)
            {
#if UNITY_EDITOR
                if(showDebugInfo)
                    GameLogger.Log($"[EnemyBlob] {gameObject.name} 尚未完全入境，不触发弹飞");
#endif
                return;
            }
    
            isBeingKnockedBack = true;
            knockbackStartTime = Time.time;
    
            // 设置 drag = 0，保持动量
            rb.drag = 0f;
            rb.angularDrag = 0f;
    
#if UNITY_EDITOR
            if(showDebugInfo)
                GameLogger.Log($"[EnemyBlob] {gameObject.name} 进入弹飞状态");
#endif
        }

        /// <summary>
        /// 退出弹飞状态（恢复移动AI）
        /// </summary>
        private void ExitKnockbackState()
        {
            isBeingKnockedBack = false;
#if UNITY_EDITOR
            if(showDebugInfo)
                GameLogger.Log($"[EnemyBlob] Drifter 弹飞结束，恢复移动");
#endif
        }

        /// <summary>
        /// 检查弹飞是否结束
        /// </summary>
        private void CheckKnockbackEnd()
        {
            if (!isBeingKnockedBack) return;
    
            float elapsed = Time.time - knockbackStartTime;
            float currentSpeed = rb.velocity.magnitude;
    
            // 条件：经过最小时间 且 速度低于阈值
            if (elapsed >= knockbackMinDuration && currentSpeed < knockbackSpeedThreshold)
            {
                ExitKnockbackState();
            }
        }
        /// <summary>
        /// 弹飞状态下应用 Frost 阻力效果
        /// </summary>
        private void ApplyFrostDragDuringKnockback()
        {
            // 如果有减速效果，增加阻力
            if (frostSpeedMultiplier < 1f)
            {
                // 减速越强，阻力越大
                // 例如：50% 减速 -> frostSpeedMultiplier = 0.5 -> extraDrag = 2.0
                float extraDrag = (1f - frostSpeedMultiplier) * 4f;
                rb.drag = extraDrag;
            }
            else
            {
                rb.drag = 0f;  // 无减速时保持原样
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 眼睛 Helper（支持多眼怪）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>对所有眼睛（主眼 + extraEyes）执行同一操作。</summary>
        private void ForEachEye(System.Action<EnemyEyes> action)
        {
            if (eyesController != null) action(eyesController);
            if (extraEyes != null)
                foreach (var eye in extraEyes)
                    if (eye != null) action(eye);
        }

        /// <summary>统一设置所有眼睛 SpriteRenderer 的透明度（用于死亡淡出 / 重生还原）。</summary>
        private void SetEyesAlpha(float alpha)
        {
            void Apply(EnemyEyes eye)
            {
                SpriteRenderer sr = eye.GetComponent<SpriteRenderer>();
                if (sr != null) { Color c = sr.color; c.a = alpha; sr.color = c; }
            }
            if (eyesController != null) Apply(eyesController);
            if (extraEyes != null)
                foreach (var eye in extraEyes)
                    if (eye != null) Apply(eye);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Shader 效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 触发受击视觉效果（闪烁 + 抖动加速）
        /// </summary>
        private void TriggerHitEffect()
        {
            if (bodyMaterials == null || bodyMaterials.Length == 0) return;
    
            // 抖动加速：直接设置 FlowSpeed x10
            foreach (var mat in bodyMaterials)
            {
                if (mat != null)
                {
                    mat.SetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed, normalFlowSpeed * 10f);
                }
            }
    
            isBeingHit = true;
            lastHitTime = Time.time;
    
            // 闪烁效果（静止障碍禁用）
            if (!disableHitFlash)
            {
                if (hitFlashCoroutine != null)
                {
                    StopCoroutine(hitFlashCoroutine);
                }
                hitFlashCoroutine = StartCoroutine(HitFlashCoroutine());
            }
        }
        /// <summary>
        /// 受击闪烁协程（线性衰减）
        /// </summary>
        private IEnumerator HitFlashCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < hitFlashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / hitFlashDuration;
                float intensity = Mathf.Lerp(1f, 0f, t);
        
                // 【修改】设置所有材质
                foreach (var mat in bodyMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetFloat(GameConstants.ShaderProperties.LiquidHitIntensity, intensity);
                    }
                }
        
                yield return null;
            }
    
            // 确保最终值为 0
            foreach (var mat in bodyMaterials)
            {
                if (mat != null)
                {
                    mat.SetFloat(GameConstants.ShaderProperties.LiquidHitIntensity, 0f);
                }
            }
    
            hitFlashCoroutine = null;
        }
        private void UpdateShaderWobble()
        {
            if (bodyMaterials == null || bodyMaterials.Length == 0) return;
    
            // 检查是否脱离受击状态（0.15秒无新伤害）
            if (isBeingHit && Time.time - lastHitTime > 0.15f)
            {
                // 恢复正常 FlowSpeed
                foreach (var mat in bodyMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed, normalFlowSpeed);
                    }
                }
                isBeingHit = false;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 冰墙孵化（Ch3 专用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void SpawnIceWallHatchling()
        {
            var hatchling = EnemyPoolManager.Instance?.Spawn(iceWallHatchType, transform.position);
            if (hatchling != null)
            {
                // 孵化怪给少量奖励：不同于正常刷出的同类型怪物（如 FrostSlime 20XP）
                // 保留击杀反馈，但 XP 大幅降低以防止经验溢出
                hatchling.SetXPReward(1);
                hatchling.SetCoinReward(1);
            }
            BattleStatistics.Instance?.RecordIceWallHatch();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 死亡处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Die()
        {
            if (isDead) return;
            isDead = true;
            // 【新增】记录死亡时是否处于受损状态（用于漏洞扩散判断）
            wasImpairedOnDeath = IsImpaired;
            rb.velocity = Vector2.zero;
            
            if (circleCollider != null)
            {
                circleCollider.enabled = false;
            }
            
            // 【新增】宝箱怪死亡爆金币
            if (deathCoinBurst > 0)
            {
                if (ProgressManager.Instance != null)
                {
                    ProgressManager.Instance.AddCoins(deathCoinBurst);
                }
                // TODO: 播放金币爆炸特效
            }
            // 计算实际经验值（考虑低保机制）
            int actualXP = xpReward;
            int actualCoin = coinReward;
            // 精英怪经验加成
            if (isElite)
            {
                actualXP = Mathf.RoundToInt(xpReward * ELITE_XP_MULTIPLIER);
                actualCoin = Mathf.RoundToInt(coinReward * ELITE_XP_MULTIPLIER);
            }
            // 低保机制（如果有）
            if (lowLevelBonusXP > 0 && ProgressManager.Instance != null)
            {
                if (ProgressManager.Instance.CurrentLevel < lowLevelThreshold)
                {
                    actualXP = Mathf.Max(actualXP, lowLevelBonusXP);
                }
            }

            // 【修改】触发敌人死亡事件，使用计算后的经验值
            GameEvents.TriggerEnemyDied(enemyType, transform.position, actualXP, actualCoin);
            // 【新增】检查是否触发漏洞扩散爆炸（Shatter Lv5）
            // 条件：死亡时处于受损状态 + 启用漏洞扩散 + 非处决击杀 + 非爆炸击杀
            bool willTriggerShatterExplosion = false;
            if (wasImpairedOnDeath && 
                !killedByExecution && 
                !killedByExplosion &&
                SkillEffectManager.Instance != null && 
                SkillEffectManager.Instance.IsShatterExplosionEnabled)
            {
                willTriggerShatterExplosion = true;
                SkillEffectManager.Instance.TriggerShatterExplosion(transform.position);
            }
            
            // 静止障碍（水坑）：跳过所有死亡 VFX，直接进入淡出
            if (behaviorType == EnemyBehaviorType.Stationary)
            {
                // 水坑消失：跳过蒸汽特效
            }
            // 普通敌人死亡 VFX
            else if (VFXPoolManager.Instance != null)
            {
                // 检查是否会触发 Focus Lv5 爆炸
                bool willTriggerFocusExplosion = SkillEffectManager.Instance != null &&
                                                 SkillEffectManager.Instance.IsFocusExplosionEnabled &&
                                                 !killedByExecution;

                if (!willTriggerFocusExplosion && !willTriggerShatterExplosion)
                {
                    if (explosionAoeDamage > 0)
                    {
                        // Ch2 炸弹怪（LavaExploder / EliteLavaExploder）：播放专属爆炸特效
                        bool isGrenadeType = enemyType == EnemyType.LavaExploder ||
                                             enemyType == EnemyType.EliteLavaExploder;
                        if (isGrenadeType)
                        {
                            VFXPoolManager.Instance.PlayGrenadeExplosionFire(transform.position);
                            AudioManager.Instance?.PlayGrenadeExplosion();
                        }
                        else
                        {
                            // 其他自爆怪：使用通用爆炸特效和音效
                            VFXPoolManager.Instance.PlayEnemyExplosion(transform.position);
                            AudioManager.Instance?.PlayEnemyExplode();
                        }
                    }
                    else if (!killedByExplosion)
                    {
                        // 普通怪被爆炸杀死：来源爆炸特效已覆盖，跳过蒸汽避免重叠
                        VFXPoolManager.Instance.PlayEnemySteam(transform.position);
                    }
                }
            }
            
            // ── Ch2 死亡特殊行为 ──
            if (EnemyPoolManager.Instance != null)
            {
                // 分裂者：均匀角度 + 随机抖动，子体继承难度缩放并向外爆开
                if (splitOnDeath)
                {
                    float baseAngleStep = splitCount > 0 ? 360f / splitCount : 0f;
                    for (int i = 0; i < splitCount; i++)
                    {
                        float jitter = Random.Range(-15f, 15f);
                        float angle = baseAngleStep * i + jitter;
                        Vector2 dir = Quaternion.Euler(0, 0, angle) * Vector2.up;
                        Vector3 spawnPos = transform.position + (Vector3)(dir * 0.5f);

                        var child = EnemyPoolManager.Instance.Spawn(splitEnemyType, spawnPos);
                        if (child != null)
                        {
                            // 继承父体波次难度
                            child.SetWaveModifiers(waveModifiers);
                            // 向外冲量（爆开感）
                            if (splitImpulseSpeed > 0f)
                                child.ApplyInitialImpulse(dir * splitImpulseSpeed);
                        }
                    }

                    // 分裂死亡特效/音效（在 Inspector 配置，可留空）
                    VFXPoolManager.Instance?.PlayEnemySplit(transform.position);
                    AudioManager.Instance?.PlayEnemySplit();
                    BattleStatistics.Instance?.RecordSplitterDeath(splitCount);
                }

                // 爆炸者：死亡时在原位生成熔岩水坑（支持大小倍率）
                if (spawnPuddleOnDeath)
                {
                    var puddle = EnemyPoolManager.Instance.Spawn(puddleEnemyType, transform.position);
                    if (puddle != null && puddleSizeMultiplier != 1f)
                        puddle.transform.localScale *= puddleSizeMultiplier;
                    // 熔浆液生成音效
                    AudioManager.Instance?.PlayLavaPuddleSpawn();
                    BattleStatistics.Instance?.RecordPuddleSpawned();
                }

                // 自爆范围伤害（无差别 AoE）
                TriggerExplosionAoE();
            }

            // Ch3：催化者死亡时触发范围暴走
            if (isCatalyst)
            {
                TriggerCatalystBurst();
            }

            deathCoroutine = StartCoroutine(DeathFadeCoroutine());
        }
        
        private IEnumerator DeathFadeCoroutine()
        {
            float elapsed = 0f;
            float startAlpha = 1f;
            
            while (elapsed < deathFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / deathFadeDuration;
                float alpha = Mathf.Lerp(startAlpha, 0f, t);
                
                if (bodyMaterials != null)
                {
                    foreach (var mat in bodyMaterials)
                    {
                        if (mat != null)
                        {
                            mat.SetFloat(GameConstants.ShaderProperties.LiquidAlpha, alpha);
                        }
                    }
                }
                
                SetEyesAlpha(alpha);
                
                foreach (Transform decoration in decorations)
                {
                    if (decoration != null)
                    {
                        SpriteRenderer sr = decoration.GetComponent<SpriteRenderer>();
                        if (sr != null)
                        {
                            Color c = sr.color;
                            c.a = alpha;
                            sr.color = c;
                        }
                    }
                }
                
                yield return null;
            }
            
            ReturnToPool();
        }
        
        private void ReturnToPool()
        {
            if (EnemyPoolManager.Instance != null)
            {
                EnemyPoolManager.Instance.Despawn(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 碰撞处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (isDead) return;
            if (data != null && data.collisionBehavior == EnemyCollisionBehavior.None) return;

            int collisionLayer = collision.gameObject.layer;
            if (collisionLayer == shieldLayer)
            {
                lastContactDamageTime = Time.time;
                HandleShieldCollision(collision.gameObject);
            }
            else if (collisionLayer == towerLayer)
            {
                lastContactDamageTime = Time.time;
                HandleTowerCollision(collision.gameObject);
            }
        }

        /// <summary>
        /// 无敌结束后敌人仍贴着护盾/塔时补充伤害。
        /// OnCollisionEnter2D 在无敌期被拦截后不会重新触发，
        /// 由 Stay 带冷却补足，确保无敌解除后立刻恢复扣血。
        /// </summary>
        private void OnCollisionStay2D(Collision2D collision)
        {
            if (isDead) return;
            if (data != null && data.collisionBehavior == EnemyCollisionBehavior.None) return;
            if (OverloadManager.Instance != null && OverloadManager.Instance.IsActive) return;
            if (Time.time - lastContactDamageTime < CONTACT_DAMAGE_INTERVAL) return;

            int collisionLayer = collision.gameObject.layer;
            if (collisionLayer == shieldLayer)
            {
                lastContactDamageTime = Time.time;
                HandleShieldCollision(collision.gameObject);
            }
            else if (collisionLayer == towerLayer)
            {
                lastContactDamageTime = Time.time;
                HandleTowerCollision(collision.gameObject);
            }
        }
        
        private void HandleShieldCollision(GameObject shieldObj)
        {
            // 【新增】大招无敌检查 - 玩家无敌时不触发任何碰撞效果
            if (OverloadManager.Instance != null && OverloadManager.Instance.IsActive)
            {
                return;
            }
            var shieldController = shieldObj.GetComponent<ShieldController>();
            if (shieldController == null) return;

            int damageAmount = data != null ? data.contactDamage : 25;

            // 【v2.0】上报小怪碰撞伤害到 BattleStatistics
            if (BattleStatistics.Instance != null)
            {
                BattleStatistics.Instance.RecordPlayerDamage(damageAmount, PlayerDamageSource.MobCollision);
            }
            // 对护盾造成伤害，并记录护盾吸收量（V4.7）
            int shieldOverflow = shieldController.TakeDamage(damageAmount);
            float shieldAbsorbed = damageAmount - Mathf.Max(0, shieldOverflow);
            if (shieldAbsorbed > 0)
                BattleStatistics.Instance?.RecordShieldDamageFromMobs(shieldAbsorbed);
            if (IsSmallEnemy())
            {
                Explode();
            }
            else
            {
                Vector2 direction = (transform.position - shieldObj.transform.position).normalized;
                rb.AddForce(direction * 500f, ForceMode2D.Impulse);
            }
        }

        
        private void HandleTowerCollision(GameObject towerObj)
        {
            // 【新增】大招无敌检查 - 玩家无敌时不触发任何碰撞效果
            if (OverloadManager.Instance != null && OverloadManager.Instance.IsActive)
            {
                return;
            }
            var turretHealth = towerObj.GetComponent<TurretHealth>();
            if (turretHealth == null)
            {
                turretHealth = towerObj.GetComponentInParent<TurretHealth>();
            }

            if (turretHealth == null) return;

            int damageAmount = data != null ? data.contactDamage : 25;

            // 【v2.0】上报小怪碰撞伤害到 BattleStatistics
            if (BattleStatistics.Instance != null)
            {
                BattleStatistics.Instance.RecordPlayerDamage(damageAmount, PlayerDamageSource.MobCollision);
            }

            bool damaged = turretHealth.TakeDamage(damageAmount);

            if (damaged)
            {
                if (IsSmallEnemy())
                {
                    Explode();
                }
                else
                {
                    Vector2 direction = (transform.position - towerObj.transform.position).normalized;
                    rb.AddForce(direction * turretHealth.GetBounceForce(), ForceMode2D.Impulse);
                }
            }
        }

        private void Explode()
        {
            if (isDead) return;
            isDead = true;

            AudioManager.Instance?.PlayEnemyExplode();
            VFXPoolManager.Instance?.PlayEnemyExplosion(transform.position);

            // 碰撞死亡也要留坑（与激光击杀路径保持一致）
            if (EnemyPoolManager.Instance != null && spawnPuddleOnDeath)
            {
                var puddle = EnemyPoolManager.Instance.Spawn(puddleEnemyType, transform.position);
                if (puddle != null && puddleSizeMultiplier != 1f)
                    puddle.transform.localScale *= puddleSizeMultiplier;
                BattleStatistics.Instance?.RecordPuddleSpawned();
            }

            // 自爆范围伤害
            TriggerExplosionAoE();

            GameEvents.TriggerEnemyDied(enemyType, transform.position, xpReward, coinReward);
            ReturnToPool();
        }

        /// <summary>
        /// 自爆无差别范围伤害：对爆炸半径内所有敌人、护盾、光棱塔造成伤害。
        /// </summary>
        private void TriggerExplosionAoE()
        {
            if (explosionAoeDamage <= 0 || explosionAoeRadius <= 0f) return;
            BattleStatistics.Instance?.RecordExploderDeath();

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionAoeRadius);
            foreach (var col in hits)
            {
                if (col == null || col.gameObject == gameObject) continue;

                // 伤害其他敌人
                var enemy = col.GetComponent<EnemyBlob>();
                if (enemy != null && !enemy.IsDead)
                {
                    Vector2 blastDir = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
                    enemy.TakeDamage(explosionAoeDamage, blastDir * 280f, false, true);
                    continue;
                }

                // 伤害护盾
                var shield = col.GetComponent<ShieldController>();
                if (shield != null)
                {
                    BattleStatistics.Instance?.RecordPlayerDamage(explosionAoeDamage, PlayerDamageSource.MobExplosionAoE);
                    shield.TakeDamage(explosionAoeDamage);
                    continue;
                }

                // 伤害光棱塔
                var turret = col.GetComponent<TurretHealth>()
                          ?? col.GetComponentInParent<TurretHealth>();
                if (turret != null)
                {
                    BattleStatistics.Instance?.RecordPlayerDamage(explosionAoeDamage, PlayerDamageSource.MobExplosionAoE);
                    turret.TakeDamage(explosionAoeDamage);
                }
            }
        }
        
        private bool IsSmallEnemy()
        {
            return rb.mass < 2.0f;
        }

        /// <summary>
        /// 设置波次难度修正（由 WaveManager 在生成时调用）
        /// </summary>
        public void SetWaveModifiers(DifficultyModifiers modifiers)
        {
            waveModifiers = modifiers;
            ApplyDifficultyModifiers();
        }

        /// <summary>
        /// 获取接触伤害（考虑波次难度）
        /// </summary>
        public int GetContactDamage()
        {
            if (data == null) return 30; // 默认值
            return Mathf.RoundToInt(data.contactDamage * waveModifiers.damageMultiplier);
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        /// <summary>
        /// 设置受击时的闪烁颜色（由 LaserController 在伤害时调用）
        /// </summary>
        public void SetHitColor(Color color)
        {
            currentHitColor = color;
        }

        /// <summary>
        /// 施加初始速度（分裂者爆开冲量）
        /// 生成后立即调用，覆盖 OnSpawn 中归零的 velocity
        /// </summary>
        public void ApplyInitialImpulse(Vector2 velocity)
        {
            if (rb != null)
                rb.velocity = velocity;
        }

        /// <summary>
        /// 被 Boss 汲取融合吸收——缩小动画 + 蒸汽特效/音效，无奖励
        /// </summary>
        public void AbsorbedByBoss()
        {
            if (isDead) return;
            isDead = true;
            rb.velocity = Vector2.zero;
            if (circleCollider != null) circleCollider.enabled = false;
            StartCoroutine(AbsorbShrinkRoutine());
        }

        private IEnumerator AbsorbShrinkRoutine()
        {
            const float shrinkDuration = 0.4f;
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < shrinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / shrinkDuration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            transform.localScale = Vector3.zero;

            // 播放蒸汽特效和死亡音效（与激光击杀效果相同）
            VFXPoolManager.Instance?.PlayEnemySteam(transform.position);
            AudioManager.Instance?.PlayEnemyDeath();

            ReturnToPool();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Ch3：催化暴走系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 由催化者死亡爆发触发，对该怪物施加暴走状态。
        /// 体型缩小、移速大幅提升、受到伤害增加，持续 duration 秒后恢复。
        /// </summary>
        public void ApplyBerserk(float duration, float speedMult, float damageTakenMult)
        {
            if (isDead) return;
            if (behaviorType == EnemyBehaviorType.Stationary || enemyType == EnemyType.IceWall) return;
            // 已暴走时忽略再次触发：倍率已是 replace 语义，跳过可防止协程重启导致的意外叠加
            if (isBerserking) return;
            if (berserkCoroutine != null) StopCoroutine(berserkCoroutine);
            berserkCoroutine = StartCoroutine(BerserkCoroutine(duration, speedMult, damageTakenMult));
            BattleStatistics.Instance?.RecordBerserkApplied();
        }

        private IEnumerator BerserkCoroutine(float duration, float speedMult, float damageTakenMult)
        {
            isBerserking = true;
            berserkSpeedMultiplier = speedMult;
            berserkDamageTakenMultiplier = damageTakenMult;

            // 暴走视觉：体型缩小至 70%（激活冲刺感）+ 冰刺覆盖
            if (!isDead)
            {
                transform.localScale = originalScale * 0.7f;
                catalystBerserkDebuff?.ApplyBerserk(duration);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (isDead) yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 暴走结束，恢复
            isBerserking = false;
            berserkSpeedMultiplier = 1f;
            berserkDamageTakenMultiplier = 1f;
            if (!isDead)
            {
                // 根据当前血量重新计算缩放（不超过原始大小）
                float healthRatio = currentHealth / maxHealth;
                float scaleRatio = Mathf.Lerp(minScale, 1f, healthRatio);
                transform.localScale = originalScale * scaleRatio;
            }
            berserkCoroutine = null;
        }

        /// <summary>
        /// 催化者死亡时调用：对周围怪物施加暴走状态。
        /// </summary>
        private void TriggerCatalystBurst()
        {
            BattleStatistics.Instance?.RecordCatalystBurst();
            // 冷气烟雾特效（VFX_NovaFrost）
            VFXPoolManager.Instance?.Play(VFXType.CatalystBurst, transform.position);
            // 冷气释放音效
            AudioManager.Instance?.PlayCatalystBurst();

            LayerMask enemyMask = LayerMask.GetMask("Enemy", "BouncingEnemy");
            Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, catalystBurstRadius, enemyMask);
            int affectedCount = 0;
            foreach (var col in nearby)
            {
                if (col == null) continue;
                var blob = col.GetComponent<EnemyBlob>();
                if (blob != null && blob != this && !blob.IsDead)
                {
                    if (blob.enemyType == EnemyType.IceWall)
                    {
                        // 冰墙：催化者死亡时加速其孵化速率而非施加暴走
                        blob.AccelerateHatch(catalystIceWallHatchSpeedMult);
                        continue;
                    }

                    if (blob.behaviorType == EnemyBehaviorType.Stationary) continue;

                    blob.ApplyBerserk(catalystBurstDuration, catalystSpeedMultiplier, catalystDamageTakenMultiplier);
                    affectedCount++;
                }
            }
            BattleStatistics.Instance?.RecordCatalystBurstAffectedCount(affectedCount);
            if (showDebugInfo)
                GameLogger.Log($"[FrostCatalyst] 催化爆发！范围 {catalystBurstRadius}，命中 {affectedCount} 个可催化目标，冰墙加速已处理");
        }
        /// <summary>
        /// 设置已完全进入屏幕（由 DrifterSpawnHelper 调用）
        /// </summary>
        public void SetFullyEnteredScreen()
        {
            hasFullyEnteredScreen = true;
            hasEnteredScreen = true; // 同时设置原有的入境标记
    
            // Drifter 直接使用 BouncingEnemy Layer（已在 DrifterSpawnHelper 中设置）
    
#if UNITY_EDITOR
            if(showDebugInfo)
                GameLogger.Log($"[EnemyBlob] {gameObject.name} 已完全入境，可以弹飞");
#endif
        }

        /// <summary>累加 Frost 照射时间</summary>
        public void AddFrostExposureTime(float deltaTime)
        {
            if (behaviorType == EnemyBehaviorType.Stationary) return;
            frostExposureTime += deltaTime;
            frostExposureResetTimer = FROST_EXPOSURE_RESET_DELAY;
        }

        /// <summary>获取当前 Frost 照射时间</summary>
        public float GetFrostExposureTime() => frostExposureTime;

        /// <summary>重置 Frost 照射时间</summary>
        public void ResetFrostExposureTime()
        {
            frostExposureTime = 0f;
        }
        /// <summary>
        /// 追加额外击退力（Crit Lv5 暴击时调用）
        /// </summary>
        public void AddExtraKnockback(Vector2 force)
        {
            if (rb != null && !isFrozen)
            {
                rb.AddForce(force, ForceMode2D.Impulse);
            }
        }
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Frost Debuff 接口
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 当 Frost 状态改变时由 FrostDebuff 调用
        /// </summary>
        /// <param name="speedMult">速度倍率（0~1，0=完全冰冻）</param>
        /// <param name="frozen">是否完全冰冻</param>
        public void OnFrostStateChanged(float speedMult, bool frozen)
        {
            frostSpeedMultiplier = speedMult;
            isFrozen = frozen;
    
            // 冰冻时立即停止移动
            if (frozen && rb != null)
            {
                rb.velocity = Vector2.zero;
            }
        }

        /// <summary>
        /// 应用 Frost 减速效果（由 LaserController 调用）
        /// </summary>
        public void ApplyFrostSlow(float slowPercent, float duration)
        {
            if (behaviorType == EnemyBehaviorType.Stationary) return;
            if (frostDebuff != null)
            {
                frostDebuff.ApplySlow(slowPercent, duration);
            }
        }

        /// <summary>
        /// 应用 Frost 完全冰冻（由 LaserController 调用）
        /// </summary>
        public void ApplyFrostFreeze(float duration)
        {
            if (behaviorType == EnemyBehaviorType.Stationary) return;
            if (!isFrozen && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayEnemyFreeze();
            }
            if (frostDebuff != null)
            {
                frostDebuff.ApplyFreeze(duration);
            }
        }

        /// <summary>
        /// 获取 FrostDebuff 组件
        /// </summary>
        public FrostDebuff GetFrostDebuff() => frostDebuff;
        
        /// <summary>
        /// 获取装饰物的 SpriteRenderer 数组（用于 Frost 颜色染色）
        /// </summary>
        public SpriteRenderer[] GetDecorationRenderers()
        {
            if (decorations == null || decorations.Length == 0)
                return null;
        
            SpriteRenderer[] renderers = new SpriteRenderer[decorations.Length];
            for (int i = 0; i < decorations.Length; i++)
            {
                if (decorations[i] != null)
                {
                    renderers[i] = decorations[i].GetComponent<SpriteRenderer>();
                }
            }
            return renderers;
        }

        /// <summary>
        /// 获取敌人类型
        /// </summary>
        public EnemyType GetEnemyType() => enemyType;

        // 【新增】是否为精英或Boss（用于处决判定）
        /// <summary>
        /// 是否为精英或Boss（不可被处决）
        /// </summary>
        public bool IsEliteOrBoss => isElite || 
                                     enemyType == EnemyType.EliteTank || 
                                     enemyType == EnemyType.EliteDrifter;
        
        // 【新增】是否处于受控状态（减速或冰冻）
        /// <summary>
        /// 是否处于受控状态（减速或冰冻），用于碎冰判定
        /// </summary>
        public bool IsControlled
        {
            get
            {
                if (frostDebuff == null) return false;
                return frostDebuff.IsSlowed || frostDebuff.IsFrozen;
            }
        }
        
        // 【新增】是否完全冰冻（用于处决判定）
        /// <summary>
        /// 是否完全冰冻（用于LV5处决判定）
        /// </summary>
        public bool IsFullyFrozen
        {
            get
            {
                if (frostDebuff == null) return false;
                return frostDebuff.IsFrozen;
            }
        }
        /// <summary>
        /// 是否处于"受损"状态（Slow 或 Freeze）
        /// 用于数据破碎技能判断
        /// </summary>
        public bool IsImpaired
        {
            get
            {
                if (frostDebuff == null) return false;
                return frostDebuff.IsSlowed || frostDebuff.IsFrozen;
            }
        }
        // 【新增】标记为处决击杀
        /// <summary>
        /// 标记该敌人被处决击杀（不触发Focus爆炸）
        /// </summary>
        public void MarkAsExecuted()
        {
            killedByExecution = true;
        }
        
        // 【新增】是否被处决击杀
        /// <summary>
        /// 是否被处决击杀
        /// </summary>
        public bool WasExecuted => killedByExecution;
        /// <summary>
        /// 获取死亡时是否处于受损状态（用于漏洞扩散）
        /// </summary>
        public bool WasImpairedOnDeath => wasImpairedOnDeath;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Ch3 运行时奖励覆盖（供 IceWall 孵化使用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>覆盖该实例的经验奖励值（不影响 ScriptableObject 原始配置）</summary>
        public void SetXPReward(int value) { xpReward = value; }

        /// <summary>覆盖该实例的金币奖励值（不影响 ScriptableObject 原始配置）</summary>
        public void SetCoinReward(int value) { coinReward = value; }

        /// <summary>是否处于催化者暴走状态（供 AI 层查询以调整攻速）</summary>
        public bool IsBerserking => isBerserking;

        /// <summary>
        /// 催化者死亡时调用：加速冰墙孵化速率。
        /// speedMultiplier=2 表示孵化间隔减半（孵化速度翻倍）。
        /// </summary>
        public void AccelerateHatch(float speedMultiplier)
        {
            if (enemyType != EnemyType.IceWall || iceWallHatchInterval <= 0f) return;
            iceWallHatchInterval /= speedMultiplier;
            iceWallHatchInterval = Mathf.Max(iceWallHatchInterval, 0.5f); // 最快 0.5s/只
            BattleStatistics.Instance?.RecordIceWallAccelerated();
            if (showDebugInfo)
                GameLogger.Log($"[IceWall] 被催化者加速！新孵化间隔 {iceWallHatchInterval:F2}s");
        }
    }
}
