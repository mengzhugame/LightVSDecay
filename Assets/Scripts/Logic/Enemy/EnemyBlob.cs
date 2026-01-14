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
using LightVsDecay.UI.FloatingText;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// 黑油怪物主逻辑
    /// 配置数据从 EnemyData ScriptableObject 读取
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
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
        /// <summary>敌人类型（只读）</summary>
        public EnemyType Type => enemyType;
        // 精英怪视觉效果
        [Header("精英特效（可选）")]
        [SerializeField] private GameObject eliteEffectPrefab;
        [SerializeField] private Color eliteTintColor = new Color(1f, 0.5f, 0f, 1f); // 橙色
        private GameObject eliteEffectInstance;
        private Color originalColor;
        [Header("视觉组件")]
        [SerializeField] private SpriteRenderer bodySprite;
        [SerializeField] private EnemyEyes eyesController;
        [SerializeField] private Transform[] decorations;
        
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
        
        // 视觉设置
        private float minScale = 0.3f;
        private float deathFadeDuration = 1.0f;
        private float normalFlowSpeed = 1.0f;
        private float normalNoiseScale = 0.5f;
        private float hitFlowSpeed = 10.0f;
        private float hitNoiseScale = 5.0f;
        private float wobbleReturnSpeed = 5.0f;
        
        // 奖励
        private int xpReward = 10;
        private int coinReward = 1;
        // 行为设置
        private EnemyBehaviorType behaviorType = EnemyBehaviorType.Chase;
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
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float currentHealth;
        private Transform targetTower;
        private Rigidbody2D rb;
        private CircleCollider2D circleCollider;
        private Vector3 originalScale;
        private bool isDead = false;
        
        private Material bodyMaterial;
        private bool isBeingHit = false;
        private float targetFlowSpeed;
        private float targetNoiseScale;
        private float lastHitTime;
        
        private Coroutine deathCoroutine;
        
        // 速度倍率（狂暴模式）
        private float speedMultiplier = 1f;
// ========== 新增：Frost 减速状态 ==========
        private float frostSpeedMultiplier = 1f;  // Frost 减速倍率
        private bool isFrozen = false;             // 是否完全冰冻
        private FrostDebuff frostDebuff;           // 缓存 FrostDebuff 组件
        
        private int shieldLayer;
        private int towerLayer;
        
        private DifficultyModifiers waveModifiers = DifficultyModifiers.Default;
        /// <summary>
        /// 是否为精英怪
        /// </summary>
        public bool IsElite => isElite;
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
            rb = GetComponent<Rigidbody2D>();
            circleCollider = GetComponent<CircleCollider2D>();
            originalScale = transform.localScale;
            
            // 获取材质实例
            if (bodySprite != null)
            {
                bodyMaterial = bodySprite.material;
            }
            
            // 加载配置
            LoadDataFromConfig();
            ConfigureRigidbody();
            // 【新增】缓存 Layer（避免每次碰撞都调用 NameToLayer）
            shieldLayer = LayerMask.NameToLayer("Shield");
            towerLayer = LayerMask.NameToLayer("Tower");
            
            enemyLayerIndex = LayerMask.NameToLayer(GameConstants.ENEMY_LAYER);
            bouncingEnemyLayerIndex = LayerMask.NameToLayer(GameConstants.BOUNCING_ENEMY_LAYER);
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
                normalNoiseScale = data.normalNoiseScale;
                hitFlowSpeed = data.hitFlowSpeed;
                hitNoiseScale = data.hitNoiseScale;
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

            }
            // 否则使用默认值（已在字段声明时初始化）
        }
        
        private void ConfigureRigidbody()
        {
            rb.gravityScale = 0;
            rb.mass = mass;
            rb.drag = knockbackDrag;
            rb.angularDrag = 0.5f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            // 【新增】Drifter 特殊配置：低阻力，保持动量
            if (enemyType == EnemyType.Drifter)
            {
                rb.drag = 0f;                    // 线性阻力 = 0
                rb.angularDrag = 0f;             // 角阻力 = 0
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 防止高速穿墙
            }
            else
            {
                rb.drag = knockbackDrag;         // 普通怪使用配置的阻力
                rb.angularDrag = 0.5f;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 对象池接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void OnSpawn()
        {
            isDead = false;
            // 重置精英状态（新增）
            isElite = false;
            // 重置波次难度为默认（等待 WaveManager 设置）
            waveModifiers = DifficultyModifiers.Default;
            
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
            
            if (circleCollider != null)
            {
                circleCollider.enabled = true;
            }
            
            if (rb != null)
            {
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
            if (deathCoroutine != null)
            {
                StopCoroutine(deathCoroutine);
                deathCoroutine = null;
            }
            
            if (eyesController != null)
            {
                eyesController.StopBlink();
            }
            
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;
            }
            
            isDead = true;
            ResetVisuals();
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
            if (bodyMaterial != null)
            {
                // 使用正确的 Shader 属性名 (LiquidFlowSpeed, LiquidNoiseScale)
                bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed, normalFlowSpeed);
                bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidNoiseScale, normalNoiseScale);
            }
            
            targetFlowSpeed = normalFlowSpeed;
            targetNoiseScale = normalNoiseScale;
        }
        
        private void ResetVisuals()
        {
            if (bodyMaterial != null)
            {
                bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidAlpha, 1.0f);
            }
            
            if (eyesController != null)
            {
                SpriteRenderer eyesSR = eyesController.GetComponent<SpriteRenderer>();
                if (eyesSR != null)
                {
                    Color c = eyesSR.color;
                    c.a = 1f;
                    eyesSR.color = c;
                }
            }
            
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
            if (isFrozen) return;// 僵直或冰冻时不移动
            if (targetTower == null) return;
    
            // 【新增】完全冰冻时不移动
            if (isFrozen)
            {
                rb.velocity = Vector2.zero;
                return;
            }
    
            // 根据行为类型选择移动方式
            if (behaviorType == EnemyBehaviorType.CrossScreen)
            {
                MoveCrossScreen();
            }
            else
            {
                MoveChase();
            }
        }
        /// <summary>
        /// 追击移动（原逻辑）
        /// </summary>
        private void MoveChase()
        {
            if (targetTower == null) return;

            Vector2 direction = (targetTower.position - transform.position).normalized;

            float currentMoveSpeed = baseMoveSpeed * speedMultiplier * frostSpeedMultiplier;
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
        public void TakeDamage(float damage, Vector2 knockbackForce, bool isCrit = false)
        {
            if (isDead) return;
            // 【新增】显示伤害飘字
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.ShowDamage(transform.position, damage, isCrit);
            }
            currentHealth -= damage;
            lastHitTime = Time.time;
            
            // 根据敌人类型和配置处理击退
            if (canBeKnockedBack)
            {
                ApplyKnockbackByType(knockbackForce);
            }
            
            TriggerShaderWobble();

            if (eyesController != null)
            {
                eyesController.TriggerSquint();
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
                Debug.Log($"[EnemyBlob] Drifter 弹飞! Force: {finalForce.magnitude:F1}, FullyEntered: {hasFullyEnteredScreen}");
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
                Debug.Log($"[EnemyBlob] {gameObject.name} 尚未完全入境，不触发弹飞");
#endif
                return;
            }
    
            isBeingKnockedBack = true;
            knockbackStartTime = Time.time;
    
            // 设置 drag = 0，保持动量
            rb.drag = 0f;
            rb.angularDrag = 0f;
    
#if UNITY_EDITOR
            Debug.Log($"[EnemyBlob] {gameObject.name} 进入弹飞状态");
#endif
        }

        /// <summary>
        /// 退出弹飞状态（恢复移动AI）
        /// </summary>
        private void ExitKnockbackState()
        {
            isBeingKnockedBack = false;
#if UNITY_EDITOR
            Debug.Log($"[EnemyBlob] Drifter 弹飞结束，恢复移动");
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
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Shader 效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void TriggerShaderWobble()
        {
            if (bodyMaterial == null) return;
            
            targetFlowSpeed = hitFlowSpeed;
            targetNoiseScale = hitNoiseScale;
            isBeingHit = true;
        }
        
        private void UpdateShaderWobble()
        {
            if (bodyMaterial == null) return;
            
            if (isBeingHit && Time.time - lastHitTime > 0.15f)
            {
                targetFlowSpeed = normalFlowSpeed;
                targetNoiseScale = normalNoiseScale;
                isBeingHit = false;
            }
            
            // 使用正确的 Shader 属性名
            float currentFlow = bodyMaterial.GetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed);
            float currentNoise = bodyMaterial.GetFloat(GameConstants.ShaderProperties.LiquidNoiseScale);
            
            float newFlow = Mathf.Lerp(currentFlow, targetFlowSpeed, Time.deltaTime * wobbleReturnSpeed);
            float newNoise = Mathf.Lerp(currentNoise, targetNoiseScale, Time.deltaTime * wobbleReturnSpeed);
            
            bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed, newFlow);
            bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidNoiseScale, newNoise);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 死亡处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Die()
        {
            if (isDead) return;
            isDead = true;
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
            
            // 播放死亡特效
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayEnemySteam(transform.position);
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
                
                if (bodyMaterial != null)
                {
                    bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidAlpha, alpha);
                }
                
                if (eyesController != null)
                {
                    SpriteRenderer eyesSR = eyesController.GetComponent<SpriteRenderer>();
                    if (eyesSR != null)
                    {
                        Color c = eyesSR.color;
                        c.a = alpha;
                        eyesSR.color = c;
                    }
                }
                
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
            // 【新增】宝箱怪不处理碰撞（无碰撞行为）
            if (data != null && data.collisionBehavior == EnemyCollisionBehavior.None)
            {
                return;
            }
            int collisionLayer = collision.gameObject.layer;
    
            // 【修改】使用 Layer 判断而非 Tag
            if (collisionLayer == shieldLayer)
            {
                HandleShieldCollision(collision.gameObject);
            }
            else if (collisionLayer == towerLayer)
            {
                HandleTowerCollision(collision.gameObject);
            }
        }
        
        private void HandleShieldCollision(GameObject shieldObj)
        {
            var shieldController = shieldObj.GetComponent<ShieldController>();
            if (shieldController == null)
            {
                shieldController = shieldObj.GetComponentInParent<ShieldController>();
            }

            if (shieldController == null) return;

            // 【修改】删除无敌检查，只检查护盾是否已破
            if (shieldController.CurrentShieldHP <= 0)
            {
                return;
            }

            // 【修改】从配置获取碰撞伤害值（字段名是 contactDamage）
            int damageAmount = data != null ? data.contactDamage : 25;
    
            // 【修改】TakeDamage 现在返回溢出伤害（int），不再返回 bool
            // overflow == 0 表示护盾完全吸收
            // overflow > 0 表示有溢出伤害
            int overflow = shieldController.TakeDamage(damageAmount);

            // 只要调用了 TakeDamage，就算造成了伤害（无论是否溢出）
            if (IsSmallEnemy())
            {
                // 小怪撞击后自爆
                Explode();
            }
            else
            {
                // 大怪被反弹
                Vector2 direction = (transform.position - shieldObj.transform.position).normalized;
                rb.AddForce(direction * 500f, ForceMode2D.Impulse);
            }
        }

        
        private void HandleTowerCollision(GameObject towerObj)
        {
            // 尝试从碰撞对象或其父对象获取 TurretHealth
            var turretHealth = towerObj.GetComponent<TurretHealth>();
            if (turretHealth == null)
            {
                turretHealth = towerObj.GetComponentInParent<TurretHealth>();
            }

            if (turretHealth == null)
            {
                Debug.LogWarning($"[EnemyBlob] 找不到 TurretHealth: {towerObj.name}");
                return;
            }

            // 【修改】从配置获取碰撞伤害值（字段名是 contactDamage）
            int damageAmount = data != null ? data.contactDamage : 25;
    
            // 对塔造成伤害（TurretHealth.TakeDamage 仍返回 bool）
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
        
        /// <summary>
        /// 被冲击波杀死
        /// </summary>
        public void KillByShockwave()
        {
            if (isDead) return;
            isDead = true;
            
            rb.velocity = Vector2.zero;
            
            if (circleCollider != null)
            {
                circleCollider.enabled = false;
            }
            
            GameEvents.TriggerEnemyDied(enemyType, transform.position, xpReward, coinReward);
            deathCoroutine = StartCoroutine(DeathFadeCoroutine());
        }
        
        private void Explode()
        {
            if (isDead) return;
            isDead = true;
            // 【新增】播放自爆音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayEnemyExplode();
            }
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayEnemyExplosion(transform.position);
            }
            
            GameEvents.TriggerEnemyDied(enemyType, transform.position, xpReward, coinReward);
            ReturnToPool();
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
        
        public EnemyType GetEnemyType() => enemyType;
        public float GetMass() => rb != null ? rb.mass : mass;
        public float GetSpeedMultiplier() => speedMultiplier;
        public bool CanBeKnockedBack => canBeKnockedBack;
        public float KnockbackMultiplier => knockbackMultiplier;
        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = multiplier;
        }
        
        public void SetCanBeKnockedBack(bool canKnockback)
        {
            canBeKnockedBack = canKnockback;
        }
        
        public void SetKnockbackMultiplier(float multiplier)
        {
            knockbackMultiplier = multiplier;
        }
        
        public void ApplyKnockback(Vector2 force)
        {
            if (isDead || !canBeKnockedBack) return;
    
            // 【新增】击退抗性计算：实际推力 = 原推力 * (1 - 抗性)
            float resistanceMultiplier = 1f - knockbackResistance;
    
            // 完全免疫时直接返回
            if (resistanceMultiplier <= 0.001f) return;
    
            // 质量缩放
            float knockbackScale = Mathf.Clamp(
                GameConstants.KNOCKBACK_MASS_SCALE / rb.mass,
                GameConstants.KNOCKBACK_SCALE_MIN,
                GameConstants.KNOCKBACK_SCALE_MAX
            );
    
            // 最终推力 = 力 * 质量缩放 * 抗性倍率 * 技能倍率
            Vector2 finalForce = force * knockbackScale * resistanceMultiplier * knockbackMultiplier;
    
            rb.AddForce(finalForce, ForceMode2D.Force);
        }
        /// <summary>
        /// 设置横穿屏幕的目标位置（由 WaveManager 调用）
        /// </summary>
        public void SetCrossScreenTarget(Vector3 target)
        {
            crossScreenTarget = target;
            crossScreenStartPos = transform.position;
            crossScreenProgress = 0f;
            isOutOfBounds = false;
            outOfBoundsTimer = 0f;
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
            Debug.Log($"[EnemyBlob] {gameObject.name} 已完全入境，可以弹飞");
#endif
        }

        /// <summary>
        /// 检查是否可以触发弹飞（必须完全入境）
        /// </summary>
        public bool CanBeKnockedBackAsDrifter()
        {
            return enemyType == EnemyType.Drifter && hasFullyEnteredScreen;
        }
        
        /// <summary>累加 Frost 照射时间</summary>
        public void AddFrostExposureTime(float deltaTime)
        {
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
    }
}