using UnityEngine;
using System.Collections;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Logic.Player;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// 黑油怪物主逻辑
    /// 【修改】添加可配置的击退系统
    /// 【修改】添加 Drifter 特殊击退行为
    /// 【修改】支持狂暴模式速度加成
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnemyBlob : MonoBehaviour, IPoolable
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("敌人类型")]
        [SerializeField] private EnemyType enemyType = EnemyType.Slime;
        
        [Header("Enemy Stats")]
        [SerializeField] private float maxHealth = 30f;
        [SerializeField] private float moveSpeed = 1.0f;
        [SerializeField] private float mass = 1.0f;
        
        [Header("Shrink Settings")]
        [SerializeField] private float minScale = 0.3f;
        
        [Header("Body Sprite (EnemyBody Layer)")]
        [SerializeField] private SpriteRenderer bodySprite;
        
        [Header("Eyes & Decorations")]
        [SerializeField] private EnemyEyes eyesController;
        [SerializeField] private Transform[] decorations;
        
        [Header("Shader Wobble Settings")]
        [SerializeField] private float normalFlowSpeed = 1.0f;
        [SerializeField] private float normalNoiseScale = 0.5f;
        [SerializeField] private float hitFlowSpeed = 10.0f;
        [SerializeField] private float hitNoiseScale = 5.0f;
        [SerializeField] private float wobbleReturnSpeed = 5.0f;
        
        [Header("Death Fade Settings")]
        [SerializeField] private float deathFadeDuration = 1.0f;
        [Header("奖励设置")]
        [Tooltip("击杀获得的经验值")]
        [SerializeField] private int xpReward = 10;

        [Tooltip("击杀获得的金币")]
        [SerializeField] private int coinReward = 1;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】击退系统配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("击退设置")]
        [Tooltip("是否可以被击退")]
        [SerializeField] private bool canBeKnockedBack = true;
        
        [Tooltip("击退力倍率（1.0=正常，0.5=难推，2.0=容易推）")]
        [Range(0f, 5f)]
        [SerializeField] private float knockbackMultiplier = 1.0f;
        
        [Tooltip("击退阻力（越大停得越快）")]
        [Range(0f, 10f)]
        [SerializeField] private float knockbackDrag = 2.0f;
        
        [Tooltip("受击后移动力减弱时间（秒）")]
        [SerializeField] private float knockbackStunDuration = 0.3f;
        
        [Tooltip("受击后移动力减弱倍率")]
        [Range(0f, 1f)]
        [SerializeField] private float knockbackStunMoveMultiplier = 0.3f;
        
        [Header("Drifter 特殊设置")]
        [Tooltip("Drifter被击退时的横向偏移角度（度）")]
        [SerializeField] private float drifterDeflectionAngle = 45f;
        
        [Tooltip("Drifter被击退时的额外力量倍率")]
        [SerializeField] private float drifterKnockbackMultiplier = 2.0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // IPoolable 实现
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public string PoolKey => enemyType.ToString();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
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
        
        // 狂暴模式速度加成
        private float speedMultiplier = 1.0f;
        private float baseMoveSpeed;
        
        // 【新增】原始阻力值（用于恢复）
        private float originalDrag;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            circleCollider = GetComponent<CircleCollider2D>();
            originalScale = transform.localScale;
            baseMoveSpeed = moveSpeed;
            
            if (bodySprite != null)
            {
                bodyMaterial = bodySprite.material;
            }
            
            ConfigureRigidbody();
        }
        
        private void FixedUpdate()
        {
            if (isDead) return;
            MoveTowardsTower();
        }
        
        private void Update()
        {
            if (isDead) return;
            UpdateShaderWobble();
        }
        
        private void OnDestroy()
        {
            if (bodyMaterial != null)
            {
                Destroy(bodyMaterial);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // IPoolable 接口实现
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void OnSpawn()
        {
            isDead = false;
            currentHealth = maxHealth;
            transform.localScale = originalScale;
            speedMultiplier = 1.0f;
            
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = true;
                rb.drag = originalDrag; // 恢复原始阻力
            }
            
            if (circleCollider != null)
            {
                circleCollider.enabled = true;
            }
            
            InitializeShaderParameters();
            FindTower();
            
            isBeingHit = false;
            lastHitTime = 0f;
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
        
        private void ConfigureRigidbody()
        {
            rb.gravityScale = 0;
            rb.mass = mass;
            rb.drag = knockbackDrag; // 【修改】使用配置的击退阻力
            rb.angularDrag = 0.5f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; // 【新增】防止旋转
            
            originalDrag = rb.drag; // 保存原始阻力值
        }
        
        private void InitializeShaderParameters()
        {
            if (bodyMaterial == null) return;
            
            bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed, normalFlowSpeed);
            bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidNoiseScale, normalNoiseScale);
            bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidAlpha, 1.0f);
            
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
            
            // 【修改】受击后短暂减弱移动力
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
            
            // 【修改】根据敌人类型和配置处理击退
            if (canBeKnockedBack)
            {
                ApplyKnockbackByType(knockbackForce);
            }
            
            TriggerShaderWobble();
            
            if (eyesController != null)
            {
                eyesController.TriggerSquint();
            }
            
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
        /// 【重写】更清晰的击退逻辑
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
                // 随机选择左偏或右偏
                float deflectionDirection = Random.value > 0.5f ? 1f : -1f;
                float angleRad = drifterDeflectionAngle * Mathf.Deg2Rad * deflectionDirection;
                
                // 旋转击退向量
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
                // 其他敌人：正常击退 × 质量缩放 × 击退倍率
                finalForce = knockbackForce * massScale * knockbackMultiplier;
            }
            
            // 应用击退力（使用 Force 模式，因为激光是持续照射）
            rb.AddForce(finalForce, ForceMode2D.Force);
        }
        /// <summary>
        /// 被冲击波杀死（由 ShieldController 调用）
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
            // 【新增】触发敌人死亡事件
            GameEvents.TriggerEnemyDied(enemyType, transform.position, xpReward, coinReward);
            deathCoroutine = StartCoroutine(DeathFadeCoroutine());
        }
        /// <summary>
        /// 获取质量（供外部判断大小怪）
        /// </summary>
        public float GetMass()
        {
            return rb != null ? rb.mass : mass;
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Shader抖动
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
            
            float currentFlow = bodyMaterial.GetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed);
            float currentNoise = bodyMaterial.GetFloat(GameConstants.ShaderProperties.LiquidNoiseScale);
            
            float newFlow = Mathf.Lerp(currentFlow, targetFlowSpeed, Time.deltaTime * wobbleReturnSpeed);
            float newNoise = Mathf.Lerp(currentNoise, targetNoiseScale, Time.deltaTime * wobbleReturnSpeed);
            
            bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed, newFlow);
            bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidNoiseScale, newNoise);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 死亡逻辑（使用VFX对象池）
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
            // 【新增】触发敌人死亡事件
            GameEvents.TriggerEnemyDied(enemyType, transform.position, xpReward, coinReward);
            deathCoroutine = StartCoroutine(DeathFadeCoroutine());
        }
        
        private IEnumerator DeathFadeCoroutine()
        {
            // 使用VFX对象池播放蒸汽特效
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayEnemySteam(transform.position);
            }
            
            float elapsedTime = 0f;
            
            while (elapsedTime < deathFadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsedTime / deathFadeDuration);
                
                if (bodyMaterial != null)
                {
                    bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidAlpha, alpha);
                }
                
                if (eyesController != null)
                {
                    SpriteRenderer eyesSR = eyesController.GetComponent<SpriteRenderer>();
                    if (eyesSR != null)
                    {
                        Color eyeColor = eyesSR.color;
                        eyeColor.a = alpha;
                        eyesSR.color = eyeColor;
                    }
                }
                
                foreach (Transform decoration in decorations)
                {
                    if (decoration != null)
                    {
                        SpriteRenderer sr = decoration.GetComponent<SpriteRenderer>();
                        if (sr != null)
                        {
                            Color color = sr.color;
                            color.a = alpha;
                            sr.color = color;
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
        // 碰撞自爆（使用VFX对象池）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnCollisionEnter2D(Collision2D collision)
        {
            // 【调试】打印所有碰撞
            //Debug.Log($"[EnemyBlob] 碰撞到: {collision.gameObject.name}, Layer: {LayerMask.LayerToName(collision.gameObject.layer)}");
            if (isDead) return;
    
            int otherLayer = collision.gameObject.layer;
    
            // 碰到护盾
            if (otherLayer == LayerMask.NameToLayer("Shield"))
            {
                HandleShieldCollision(collision.gameObject);
                return;
            }
    
            // 碰到塔本体
            if (otherLayer == LayerMask.NameToLayer("Tower"))
            {
                HandleTowerCollision(collision.gameObject);
                return;
            }
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
           // Debug.Log($"[EnemyBlob] Trigger进入: {other.gameObject.name}, Layer: {LayerMask.LayerToName(other.gameObject.layer)}");
            if (isDead) return;
    
            int otherLayer = other.gameObject.layer;
    
            // 护盾碰撞（Trigger模式）
            if (otherLayer == LayerMask.NameToLayer("Shield"))
            {
                HandleShieldCollision(other.gameObject);
            }
        }
        /// <summary>
        /// 处理与护盾的碰撞
        /// </summary>
        private void HandleShieldCollision(GameObject shieldObj)
        {
            var shield = shieldObj.GetComponent<ShieldController>();
            if (shield == null) return;
    
            // 尝试对护盾造成伤害
            bool damaged = shield.TakeDamage(1);
    
            if (damaged)
            {
                // 小怪：自爆
                if (IsSmallEnemy())
                {
                    Explode();
                }
                // 大怪会被冲击波弹开，不需要额外处理
            }
            // 如果护盾无敌中，什么都不发生
        }
        /// <summary>
        /// 判断是否为小怪（根据质量）
        /// </summary>
        private bool IsSmallEnemy()
        {
            return rb.mass < 2.0f;
        }
        /// <summary>
        /// 处理与塔本体的碰撞
        /// </summary>
        private void HandleTowerCollision(GameObject towerObj)
        {
            //Explode();
            var turretHealth = towerObj.GetComponent<TurretHealth>();
            if (turretHealth == null) return;
            
            // 尝试对塔造成伤害
            bool damaged = turretHealth.TakeDamage(1);
            
            if (damaged)
            {
                // 判断大小怪
                if (turretHealth.IsSmallEnemy(GetMass()))
                {
                    // 小怪自爆
                    Explode();
                }
                else
                {
                    // 大怪被弹开
                    Vector2 direction = (transform.position - towerObj.transform.position).normalized;
                    rb.AddForce(direction * turretHealth.GetBounceForce(), ForceMode2D.Impulse);
                }
            }
        }
        private void Explode()
        {
            if (isDead) return;
            isDead = true;
            
            // 使用VFX对象池播放爆炸特效
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayEnemyExplosion(transform.position);
            }
            
            // TODO: 对塔造成伤害
            // 【新增】自爆也触发敌人死亡事件
            GameEvents.TriggerEnemyDied(enemyType, transform.position, xpReward, coinReward);
            ReturnToPool();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public EnemyType GetEnemyType() => enemyType;
        
        /// <summary>
        /// 设置速度倍率（用于狂暴模式）
        /// </summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = multiplier;
        }
        
        /// <summary>
        /// 获取当前速度倍率
        /// </summary>
        public float GetSpeedMultiplier() => speedMultiplier;
        
        /// <summary>
        /// 【新增】设置是否可被击退
        /// </summary>
        public void SetCanBeKnockedBack(bool canKnockback)
        {
            canBeKnockedBack = canKnockback;
        }
        
        /// <summary>
        /// 【新增】设置击退力倍率
        /// </summary>
        public void SetKnockbackMultiplier(float multiplier)
        {
            knockbackMultiplier = multiplier;
        }
        
        /// <summary>
        /// 【新增】获取是否可被击退
        /// </summary>
        public bool CanBeKnockedBack => canBeKnockedBack;
        
        /// <summary>
        /// 【新增】获取击退力倍率
        /// </summary>
        public float KnockbackMultiplier => knockbackMultiplier;
        
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
        
        private void OnDrawGizmosSelected()
        {
            if (targetTower != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, targetTower.position);
            }
            
            // 【新增】显示当前速度向量
            if (Application.isPlaying && rb != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, transform.position + (Vector3)rb.velocity);
            }
        }
    }
}