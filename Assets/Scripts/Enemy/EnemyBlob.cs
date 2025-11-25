using UnityEngine;
using System.Collections;
using LightVsDecay.Core;

namespace LightVsDecay.Enemy
{
    /// <summary>
    /// 黑油怪物主逻辑
    /// 功能：移动、高压水枪效果（推力+变形+缩小）、碰撞自爆
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnemyBlob : MonoBehaviour
    {
        [Header("Enemy Stats")]
        [SerializeField] private float maxHealth = 30f;
        [SerializeField] private float moveSpeed = 1.0f;
        [SerializeField] private float mass = 1.0f; // 质量影响推力效果（<1.0无推力，直接秒杀）
        
        [Header("Shrink Settings")]
        [SerializeField] private float minScale = 0.3f; // 缩小到此值后蒸发
        
        [Header("VFX References")]
        [SerializeField] private GameObject steamVFX; // 蒸发特效预制体（直接拖拽）
        [SerializeField] private GameObject explosionVFX; // 自爆特效（碰撞塔时）
        
        [Header("Body Sprite (EnemyBody Layer)")]
        [SerializeField] private SpriteRenderer bodySprite; // 白色模糊圆球（用于Metaballs RT融合）
        
        [Header("Eyes & Decorations")]
        [SerializeField] private EnemyEyes eyesController; // 眼睛控制器
        [SerializeField] private Transform[] decorations; // 其他装饰物（会跟随Scale缩放）
        
        [Header("Shader Wobble Settings")]
        [SerializeField] private float normalFlowSpeed = 1.0f; // 平时的流动速度（每个怪物可不同）
        [SerializeField] private float normalNoiseScale = 0.5f; // 平时的噪波缩放
        [SerializeField] private float hitFlowSpeed = 10.0f; // 被击中时的流动速度
        [SerializeField] private float hitNoiseScale = 5.0f; // 被击中时的噪波缩放
        [SerializeField] private float wobbleReturnSpeed = 5.0f; // 抖动恢复速度（停止攻击后）
        
        [Header("Death Fade Settings")]
        [SerializeField] private float deathFadeDuration = 1.0f; // 死亡淡出时长（Alpha 1→0）
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float currentHealth;
        private Transform targetTower; // 光棱塔位置
        private Rigidbody2D rb;
        private Vector3 originalScale;
        private bool isDead = false;
        
        // Shader控制
        private Material bodyMaterial; // Body材质实例（重要：不用sharedMaterial）
        private bool isBeingHit = false; // 是否正在被激光击中
        private float targetFlowSpeed; // 目标流动速度
        private float targetNoiseScale; // 目标噪波缩放
        private float lastHitTime; // 上次被击中时间（用于判断停止攻击）
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            ConfigureRigidbody();
            
            currentHealth = maxHealth;
            originalScale = transform.localScale;
            
            // 获取Body材质实例（避免所有怪物共享材质）
            if (bodySprite != null)
            {
                bodyMaterial = bodySprite.material; // 自动创建实例
                InitializeShaderParameters();
            }
            else
            {
                Debug.LogError("[EnemyBlob] bodySprite未赋值！", this);
            }
            
            // 查找光棱塔（假设Tag为"Tower"）
            GameObject tower = GameObject.FindGameObjectWithTag("Tower");
            if (tower != null)
            {
                targetTower = tower.transform;
            }
            else
            {
                Debug.LogWarning("[EnemyBlob] Tower not found! Enemy won't move.");
            }
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
            // 清理材质实例，避免内存泄漏
            if (bodyMaterial != null)
            {
                Destroy(bodyMaterial);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 配置Rigidbody2D（质量、阻力等）
        /// </summary>
        private void ConfigureRigidbody()
        {
            rb.gravityScale = 0; // 2D俯视图，无重力
            rb.mass = mass;
            rb.drag = 0.5f; // 【修改】降低阻力（原2f太高，会抵消推力）
            rb.angularDrag = 0.5f; // 降低角阻力
            rb.interpolation = RigidbodyInterpolation2D.Interpolate; // 平滑物理插值
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 防止高速穿透
            
            // 确保不是Kinematic（否则完全不受力影响）
            rb.bodyType = RigidbodyType2D.Dynamic;
        }
        
        /// <summary>
        /// 初始化Shader参数（设置平时的默认值）
        /// </summary>
        private void InitializeShaderParameters()
        {
            if (bodyMaterial == null) return;
            
            bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed, normalFlowSpeed);
            bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidNoiseScale, normalNoiseScale);
            bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidAlpha, 1.0f); // 初始完全不透明
            
            targetFlowSpeed = normalFlowSpeed;
            targetNoiseScale = normalNoiseScale;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 移动逻辑
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 向光棱塔移动（使用AddForce，不覆盖激光推力）
        /// </summary>
        private void MoveTowardsTower()
        {
            if (targetTower == null) return;
            
            Vector2 direction = (targetTower.position - transform.position).normalized;
            
            // 【关键修改】使用AddForce而非直接设置velocity
            // 这样不会覆盖激光施加的推力
            float moveForce = moveSpeed * 10f; // 转换为力的大小
            
            // 如果被击中，减少移动力（制造"被推开"的对抗感）
            if (Time.time - lastHitTime < 0.2f && rb.mass >= GameConstants.KNOCKBACK_MASS_THRESHOLD)
            {
                moveForce *= 0.3f; // 被激光照射时，移动力减少70%
            }
            
            rb.AddForce(direction * moveForce, ForceMode2D.Force);
            
            // 限制最大速度（防止加速过快）
            if (rb.velocity.magnitude > moveSpeed * 2f)
            {
                rb.velocity = rb.velocity.normalized * moveSpeed * 2f;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 高压水枪效果核心：伤害 + 推力 + 变形
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 受到激光伤害（由LaserController调用）
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <param name="knockbackForce">击退力向量</param>
        public void TakeDamage(float damage, Vector2 knockbackForce)
        {
            if (isDead) return;
            
            currentHealth -= damage;
            lastHitTime = Time.time;
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 1. 推力判断（基于mass）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            if (rb.mass >= GameConstants.KNOCKBACK_MASS_THRESHOLD)
            {
                // 中大型怪物：应用击退力（质量越大推力越小）
                float knockbackScale = Mathf.Clamp(
                    GameConstants.KNOCKBACK_MASS_SCALE / rb.mass,
                    GameConstants.KNOCKBACK_SCALE_MIN,
                    GameConstants.KNOCKBACK_SCALE_MAX
                );
                Vector2 finalForce = knockbackForce * knockbackScale;
                rb.AddForce(finalForce, ForceMode2D.Force);
                
                // 【调试日志】
                Debug.Log($"[EnemyBlob] 施加推力: {finalForce.magnitude:F2}, Mass: {rb.mass}, Scale: {knockbackScale:F2}");
            }
            else
            {
                // 【调试日志】
                Debug.Log($"[EnemyBlob] 小怪无推力 (mass={rb.mass} < {GameConstants.KNOCKBACK_MASS_THRESHOLD})");
            }
            // 小型怪物（mass < 1.0）：无推力，直接扣血即可
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 2. Shader抖动效果（持续型：被照射就一直抖）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            TriggerShaderWobble();
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 3. 眼睛眯眼动画
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            if (eyesController != null)
            {
                eyesController.TriggerSquint();
            }
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 4. 体积缩小（Scale缩放）
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            float healthRatio = currentHealth / maxHealth;
            float newScale = Mathf.Lerp(minScale, 1f, healthRatio);
            transform.localScale = originalScale * newScale;
            
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // 5. 检查是否死亡
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            if (currentHealth <= 0 || newScale <= minScale)
            {
                Die();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Shader抖动控制
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 触发Shader抖动（被激光击中时调用）
        /// </summary>
        private void TriggerShaderWobble()
        {
            if (bodyMaterial == null) return;
            
            // 设置目标值为"击中状态"
            targetFlowSpeed = hitFlowSpeed;
            targetNoiseScale = hitNoiseScale;
            isBeingHit = true;
        }
        
        /// <summary>
        /// 平滑更新Shader参数（持续型抖动）
        /// </summary>
        private void UpdateShaderWobble()
        {
            if (bodyMaterial == null) return;
            
            // 检查是否超过0.15秒没被击中（停止抖动）
            if (isBeingHit && Time.time - lastHitTime > 0.15f)
            {
                targetFlowSpeed = normalFlowSpeed;
                targetNoiseScale = normalNoiseScale;
                isBeingHit = false;
            }
            
            // 平滑过渡到目标值
            float currentFlow = bodyMaterial.GetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed);
            float currentNoise = bodyMaterial.GetFloat(GameConstants.ShaderProperties.LiquidNoiseScale);
            
            float newFlow = Mathf.Lerp(currentFlow, targetFlowSpeed, Time.deltaTime * wobbleReturnSpeed);
            float newNoise = Mathf.Lerp(currentNoise, targetNoiseScale, Time.deltaTime * wobbleReturnSpeed);
            
            bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidFlowSpeed, newFlow);
            bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidNoiseScale, newNoise);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 死亡逻辑
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 死亡（正常被激光击杀）
        /// </summary>
        private void Die()
        {
            if (isDead) return;
            isDead = true;
            
            // 停止移动
            rb.velocity = Vector2.zero;
            
            // 启动死亡淡出协程
            StartCoroutine(DeathFadeCoroutine());
        }
        
        /// <summary>
        /// 死亡淡出协程：Alpha 1→0（1秒），同时播放蒸汽特效
        /// </summary>
        private IEnumerator DeathFadeCoroutine()
        {
            // 播放蒸汽特效
            if (steamVFX != null)
            {
                Instantiate(steamVFX, transform.position, Quaternion.identity);
            }
            else
            {
                Debug.LogWarning("[EnemyBlob] steamVFX未赋值！请在Inspector中拖拽蒸汽特效预制体。", this);
            }
            
            // Alpha淡出（1秒）
            float elapsedTime = 0f;
            
            while (elapsedTime < deathFadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsedTime / deathFadeDuration);
                
                // 设置Body的Alpha
                if (bodyMaterial != null)
                {
                    bodyMaterial.SetFloat(GameConstants.ShaderProperties.LiquidAlpha, alpha);
                }
                
                // 设置装饰物和眼睛的Alpha（通过SpriteRenderer的Color）
                if (eyesController != null && eyesController.GetComponent<SpriteRenderer>() != null)
                {
                    Color eyeColor = eyesController.GetComponent<SpriteRenderer>().color;
                    eyeColor.a = alpha;
                    eyesController.GetComponent<SpriteRenderer>().color = eyeColor;
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
            
            // TODO: 掉落经验/光点
            
            // 销毁对象
            Destroy(gameObject);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 碰撞自爆
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 碰撞自爆（撞到塔或盾）
        /// </summary>
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (isDead) return;
            
            // 检查是否撞到塔或护盾
            if (collision.gameObject.CompareTag("Tower") || collision.gameObject.CompareTag("Shield"))
            {
                Explode();
            }
        }
        
        /// <summary>
        /// 自爆（碰撞塔后）
        /// </summary>
        private void Explode()
        {
            if (isDead) return;
            isDead = true;
            
            // 播放爆炸特效
            if (explosionVFX != null)
            {
                Instantiate(explosionVFX, transform.position, Quaternion.identity);
            }
            
            // TODO: 对塔造成伤害（通过事件或直接调用TowerHealth）
            
            Destroy(gameObject);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 外部接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 外部接口：仅施加击退力（不造成伤害）
        /// </summary>
        public void ApplyKnockback(Vector2 force)
        {
            if (isDead || rb.mass < GameConstants.KNOCKBACK_MASS_THRESHOLD) return;
            
            float knockbackScale = Mathf.Clamp(
                GameConstants.KNOCKBACK_MASS_SCALE / rb.mass,
                GameConstants.KNOCKBACK_SCALE_MIN,
                GameConstants.KNOCKBACK_SCALE_MAX
            );
            
            rb.AddForce(force * knockbackScale, ForceMode2D.Force);
        }
        
        /// <summary>
        /// 编辑器辅助：显示目标线
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (targetTower != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, targetTower.position);
            }
        }
    }
}