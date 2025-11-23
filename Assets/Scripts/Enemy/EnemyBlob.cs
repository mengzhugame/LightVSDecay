using UnityEngine;

namespace LightVsDecay.Enemy
{
    /// <summary>
    /// 黑油怪物主逻辑
    /// 功能：移动、受击缩小、碰撞自爆
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnemyBlob : MonoBehaviour
    {
        [Header("Enemy Stats")]
        [SerializeField] private float maxHealth = 30f;
        [SerializeField] private float moveSpeed = 1.0f;
        [SerializeField] private float mass = 1.0f; // 质量影响推力效果
        
        [Header("Shrink Settings")]
        [SerializeField] private float minScale = 0.3f; // 缩小到此值后蒸发
        [SerializeField] private float shrinkRate = 0.1f; // 每次受击缩小的比例
        
        [Header("VFX References")]
        [SerializeField] private GameObject evaporationVFX; // 蒸发粒子特效
        [SerializeField] private GameObject explosionVFX; // 自爆特效
        
        [Header("Body Sprite (EnemyBody Layer)")]
        [SerializeField] private SpriteRenderer bodySprite; // 白色模糊圆球
        
        [Header("Eyes Reference")]
        [SerializeField] private EnemyEyes eyesController; // 眼睛控制器
        
        // 运行时数据
        private float currentHealth;
        private Transform targetTower; // 光棱塔位置
        private Rigidbody2D rb;
        private Vector3 originalScale;
        private bool isDead = false;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            ConfigureRigidbody();
            
            currentHealth = maxHealth;
            originalScale = transform.localScale;
            
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
        
        /// <summary>
        /// 配置Rigidbody2D（质量、阻力等）
        /// </summary>
        private void ConfigureRigidbody()
        {
            rb.gravityScale = 0; // 2D俯视图，无重力
            rb.mass = mass;
            rb.drag = 2f; // 线性阻力（防止被推飞后永远滑行）
            rb.angularDrag = 1f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 防止高速穿透
        }
        
        /// <summary>
        /// 向光棱塔移动
        /// </summary>
        private void MoveTowardsTower()
        {
            if (targetTower == null) return;
            
            Vector2 direction = (targetTower.position - transform.position).normalized;
            rb.velocity = direction * moveSpeed;
        }
        
        /// <summary>
        /// 受到激光伤害（由LaserBeam调用）
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <param name="knockbackForce">击退力向量</param>
        public void TakeDamage(float damage, Vector2 knockbackForce)
        {
            if (isDead) return;
            
            currentHealth -= damage;
            
            // 应用击退力
            rb.AddForce(knockbackForce, ForceMode2D.Impulse);
            
            // 触发眼睛眯眼动画
            if (eyesController != null)
            {
                eyesController.TriggerSquint();
            }
            
            // 计算缩小比例
            float healthRatio = currentHealth / maxHealth;
            float newScale = Mathf.Lerp(minScale, 1f, healthRatio);
            transform.localScale = originalScale * newScale;
            
            // 检查是否蒸发
            if (currentHealth <= 0 || newScale <= minScale)
            {
                Evaporate();
            }
        }
        
        /// <summary>
        /// 蒸发（正常死亡）
        /// </summary>
        private void Evaporate()
        {
            if (isDead) return;
            isDead = true;
            
            // 播放蒸发特效
            if (evaporationVFX != null)
            {
                Instantiate(evaporationVFX, transform.position, Quaternion.identity);
            }
            
            // TODO: 掉落经验/光点
            
            Destroy(gameObject);
        }
        
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
        
        /// <summary>
        /// 外部接口：应用击退力（不造成伤害）
        /// </summary>
        public void ApplyKnockback(Vector2 force)
        {
            if (isDead) return;
            rb.AddForce(force, ForceMode2D.Impulse);
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