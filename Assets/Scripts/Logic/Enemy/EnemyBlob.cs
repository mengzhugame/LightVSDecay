// ============================================================
// EnemyBlob.cs (修复版)
// 文件位置: Assets/Scripts/Logic/Enemy/EnemyBlob.cs
// 用途：敌人主逻辑 - 修复 Shader 属性名
// ============================================================

using UnityEngine;
using System.Collections;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Player;

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
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
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
        }
        
        private void Start()
        {
            FindTower();
        }
        
        private void Update()
        {
            if (isDead) return;
            
            UpdateShaderWobble();
        }
        
        private void FixedUpdate()
        {
            if (isDead) return;
            
            MoveTowardsTower();
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
                
                // Drifter 特殊
                drifterDeflectionAngle = data.drifterDeflectionAngle;
                drifterKnockbackMultiplier = data.drifterKnockbackMultiplier;
                
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
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 对象池接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void OnSpawn()
        {
            isDead = false;
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
            if (targetTower == null) return;
            
            Vector2 direction = (targetTower.position - transform.position).normalized;
            float currentMoveSpeed = baseMoveSpeed * speedMultiplier;
            float moveForce = currentMoveSpeed * 10f;
            
            // 受击后短暂减弱移动力
            float timeSinceHit = Time.time - lastHitTime;
            if (timeSinceHit < knockbackStunDuration)
            {
                moveForce *= knockbackStunMoveMultiplier;
            }
            
            rb.AddForce(direction * moveForce, ForceMode2D.Force);
            
            // 限制最大速度
            if (rb.velocity.magnitude > currentMoveSpeed * 2f)
            {
                rb.velocity = rb.velocity.normalized * currentMoveSpeed * 2f;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void TakeDamage(float damage, Vector2 knockbackForce)
        {
            if (isDead) return;
            
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
        }
        
        /// <summary>
        /// 根据敌人类型应用不同的击退效果
        /// </summary>
        private void ApplyKnockbackByType(Vector2 knockbackForce)
        {
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
                float deflectionDirection = Random.value > 0.5f ? 1f : -1f;
                float angleRad = drifterDeflectionAngle * Mathf.Deg2Rad * deflectionDirection;
                
                float cos = Mathf.Cos(angleRad);
                float sin = Mathf.Sin(angleRad);
                Vector2 deflectedForce = new Vector2(
                    knockbackForce.x * cos - knockbackForce.y * sin,
                    knockbackForce.x * sin + knockbackForce.y * cos
                );
                
                finalForce = deflectedForce * massScale * knockbackMultiplier * drifterKnockbackMultiplier;
            }
            else
            {
                finalForce = knockbackForce * massScale * knockbackMultiplier;
            }
            
            rb.AddForce(finalForce, ForceMode2D.Force);
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
            
            // 触发敌人死亡事件
            GameEvents.TriggerEnemyDied(enemyType, transform.position, xpReward, coinReward);
            
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
            
            if (collision.gameObject.CompareTag("Shield"))
            {
                HandleShieldCollision(collision.gameObject);
            }
            else if (collision.gameObject.CompareTag("Tower"))
            {
                HandleTowerCollision(collision.gameObject);
            }
        }
        
        private void HandleShieldCollision(GameObject shieldObj)
        {
            var shieldController = shieldObj.GetComponentInParent<ShieldController>();
            if (shieldController == null) return;
            
            if (!shieldController.IsInvincible && shieldController.CurrentShieldHP > 0)
            {
                if (IsSmallEnemy())
                {
                    Explode();
                }
            }
        }
        
        private void HandleTowerCollision(GameObject towerObj)
        {
            var turretHealth = towerObj.GetComponent<TurretHealth>();
            if (turretHealth == null) return;
            
            bool damaged = turretHealth.TakeDamage(1);
            
            if (damaged)
            {
                if (turretHealth.IsSmallEnemy(GetMass()))
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
            
            float knockbackScale = Mathf.Clamp(
                GameConstants.KNOCKBACK_MASS_SCALE / rb.mass,
                GameConstants.KNOCKBACK_SCALE_MIN,
                GameConstants.KNOCKBACK_SCALE_MAX
            );
            
            rb.AddForce(force * knockbackScale * knockbackMultiplier, ForceMode2D.Force);
        }
    }
}