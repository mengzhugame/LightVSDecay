// ============================================================
// BossPollutionProjectile.cs
// 文件位置: Assets/Scripts/Logic/Boss/BossPollutionProjectile.cs
// 用途：BOSS 技能 - 污秽喷吐投射物 V3.0
// 【重构】物理实体：有HP、可被推、可反弹伤害Boss
// ============================================================

using System.Collections;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Logic.Player;
using LightVsDecay.Logic.Enemy;

namespace LightVsDecay.Logic.Boss
{
    /// <summary>
    /// BOSS 污秽喷吐投射物 V3.0
    /// 特性：
    /// - 有HP（200），可被激光打爆
    /// - 有质量（3），可被推力推动
    /// - 撞到Boss造成5%HP伤害 + 短僵直
    /// - 最多同时存在3个
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class BossPollutionProjectile : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("移动参数")]
        [Tooltip("飞行速度")]
        [SerializeField] private float moveSpeed = 5f;
        
        [Tooltip("惰性追踪转向速度（度/秒）- 越小转弯越大")]
        [SerializeField] private float turnSpeed = 90f;
        
        [Tooltip("生命周期（秒）")]
        [SerializeField] private float lifetime = 8f;
        
        [Header("V3.0 物理参数")]
        [Tooltip("最大血量")]
        [SerializeField] private float maxHP = 200f;
        
        [Tooltip("质量（影响被推动难度）")]
        [SerializeField] private float mass = 3f;
        
        [Tooltip("被推动时的阻力")]
        [SerializeField] private float pushDrag = 1f;
        
        [Tooltip("被推动后恢复追踪的延迟（秒）")]
        [SerializeField] private float resumeTrackingDelay = 0.5f;
        
        [Header("伤害参数")]
        [Tooltip("命中护盾伤害")]
        [SerializeField] private int shieldDamage = 100;
        
        [Header("视觉效果")]
        [Tooltip("飞行拖尾粒子系统")]
        [SerializeField] private ParticleSystem orbParticle;
        
        [Tooltip("爆炸粒子系统")]
        [SerializeField] private ParticleSystem explosionParticle;
        
        [Tooltip("受击闪烁颜色")]
        [SerializeField] private Color hitFlashColor = Color.white;
        
        [Tooltip("闪烁持续时间")]
        [SerializeField] private float hitFlashDuration = 0.1f;

        [Header("Layer 设置")]
        [Tooltip("玩家塔所在 Layer")]
        [SerializeField] private LayerMask playerTowerLayer;
        
        [Tooltip("Boss 所在 Layer")]
        [SerializeField] private LayerMask bossLayer;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 组件缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Rigidbody2D rb;
        private Collider2D col;
        private SpriteRenderer spriteRenderer;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Transform target;
        private Vector2 currentDirection;
        private float spawnTime;
        private bool isDestroyed = false;
        
        // V3.0 状态
        private float currentHP;
        private BossController bossController;
        private BossHealth bossHealth;
        private bool isBeingPushed = false;
        private float pushRecoveryTimer = 0f;
        private Color originalColor;
        private Coroutine flashCoroutine;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            
            // 配置刚体
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            
            // V3.0: 不使用触发器，使用物理碰撞
            col.isTrigger = false;
            
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
        }
        
        private void Start()
        {
            spawnTime = Time.time;
            currentHP = maxHP;
            
            // 配置物理属性
            rb.mass = mass;
            rb.drag = 0f;  // 飞行时无阻力
            rb.angularDrag = 0f;
            
            // 查找目标（玩家塔）
            FindTarget();
            
            // 查找Boss引用
            FindBoss();
            
            // 初始方向：朝向目标
            if (target != null)
            {
                currentDirection = ((Vector2)target.position - (Vector2)transform.position).normalized;
            }
            else
            {
                currentDirection = Vector2.down;
            }

            if (showDebugInfo)
            {
                Debug.Log($"[PollutionProjectile] V3.0 生成 @ {transform.position}, HP={currentHP}, Mass={mass}");
            }
            PermanentIgnoreBossCollision();
        }
        
        private void FixedUpdate()
        {
            if (isDestroyed) return;
            
            // 检查生命周期
            if (Time.time - spawnTime > lifetime)
            {
                DestroyProjectile(false);
                return;
            }
            
            // V3.0: 被推动后恢复追踪
            if (isBeingPushed)
            {
                pushRecoveryTimer -= Time.fixedDeltaTime;
                if (pushRecoveryTimer <= 0)
                {
                    isBeingPushed = false;
                    rb.drag = 0f;  // 恢复无阻力
                    
                    if (showDebugInfo)
                    {
                        Debug.Log("[PollutionProjectile] 恢复追踪模式");
                    }
                }
                
                // 被推动时不主动追踪，让物理接管
                // 但仍然旋转朝向移动方向
                if (rb.velocity.magnitude > 0.1f)
                {
                    currentDirection = rb.velocity.normalized;
                    RotateToDirection();
                }
                return;
            }
            
            // 惰性追踪逻辑
            UpdateTracking();
            
            // 移动
            rb.velocity = currentDirection * moveSpeed;
            
            // 旋转朝向移动方向
            RotateToDirection();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 惰性追踪逻辑
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateTracking()
        {
            if (target == null)
            {
                FindTarget();
                return;
            }
            
            // 计算目标方向
            Vector2 targetDirection = ((Vector2)target.position - (Vector2)transform.position).normalized;
            
            // 惰性追踪：缓慢转向
            float maxTurnAngle = turnSpeed * Time.fixedDeltaTime;
            
            float currentAngle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
            
            float angleDiff = Mathf.DeltaAngle(currentAngle, targetAngle);
            float turnAngle = Mathf.Clamp(angleDiff, -maxTurnAngle, maxTurnAngle);
            
            float newAngle = currentAngle + turnAngle;
            currentDirection = new Vector2(
                Mathf.Cos(newAngle * Mathf.Deg2Rad),
                Mathf.Sin(newAngle * Mathf.Deg2Rad)
            );
        }
        
        private void RotateToDirection()
        {
            if (currentDirection.magnitude > 0.01f)
            {
                float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
        
        private void FindTarget()
        {
            ShieldController shield = FindObjectOfType<ShieldController>();
            if (shield != null)
            {
                target = shield.transform;
                return;
            }
            
            TurretHealth turret = FindObjectOfType<TurretHealth>();
            if (turret != null)
            {
                target = turret.transform;
            }
        }
        
        private void FindBoss()
        {
            bossController = FindObjectOfType<BossController>();
            bossHealth = FindObjectOfType<BossHealth>();
        }

        private void PermanentIgnoreBossCollision()
        {
            if (bossController == null)
            {
                return;
            }
    
            // 获取 Boss 的所有碰撞器
            Collider2D[] bossColliders = bossController.GetComponentsInChildren<Collider2D>();
    
            // 永久忽略碰撞
            foreach (var bossCol in bossColliders)
            {
                if (bossCol != null && col != null)
                {
                    Physics2D.IgnoreCollision(col, bossCol, true);
                }
            }
    
            if (showDebugInfo)
            {
                Debug.Log("[PollutionProjectile] 永久忽略Boss碰撞");
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // V3.0 伤害系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 受到激光伤害 + 推力
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <param name="pushForce">推力向量</param>
        public void TakeDamage(float damage, Vector2 pushForce)
        {
            if (isDestroyed) return;
            
            currentHP -= damage;
            
            // 应用推力
            if (pushForce.magnitude > 0.1f)
            {
                isBeingPushed = true;
                pushRecoveryTimer = resumeTrackingDelay;
                rb.drag = pushDrag;  // 增加阻力减缓被推速度
                
                rb.AddForce(pushForce, ForceMode2D.Impulse);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[PollutionProjectile] 受击！伤害={damage:F0}, 推力={pushForce.magnitude:F0}, 剩余HP={currentHP:F0}");
                }
            }
            
            // 闪白效果
            TriggerHitFlash();
            
            // 检查死亡
            if (currentHP <= 0)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[PollutionProjectile] 被击爆！");
                }
                DestroyProjectile(true);
            }
        }
        
        /// <summary>
        /// 触发受击闪白
        /// </summary>
        private void TriggerHitFlash()
        {
            if (spriteRenderer == null) return;
            
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            flashCoroutine = StartCoroutine(HitFlashCoroutine());
        }
        
        private System.Collections.IEnumerator HitFlashCoroutine()
        {
            spriteRenderer.color = hitFlashColor;
            yield return new WaitForSeconds(hitFlashDuration);
            spriteRenderer.color = originalColor;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 碰撞检测
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (isDestroyed) return;
    
            int otherLayer = collision.gameObject.layer;
            string layerName = LayerMask.LayerToName(otherLayer);
    
            if (showDebugInfo)
            {
                Debug.Log($"[PollutionProjectile] 碰撞: {collision.gameObject.name}, Layer: {layerName}");
            }
    
            // 撞到玩家塔/护盾
            if (layerName == "Shield" || layerName == "Tower")
            {
                OnHitPlayer();
                return;
            }
    
            // 撞到墙壁 - 销毁
            if (layerName == GameConstants.WALL_LAYER || collision.gameObject.CompareTag(GameConstants.WALL_TAG))
            {
                if (showDebugInfo)
                {
                    Debug.Log("[PollutionProjectile] 撞击空气墙，销毁");
                }
                DestroyProjectile(false);
                return;
            }
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDestroyed) return;
    
            int otherLayer = other.gameObject.layer;
            string layerName = LayerMask.LayerToName(otherLayer);
    
            // 撞到玩家塔/护盾
            if (layerName == "Shield" || layerName == "Tower")
            {
                OnHitPlayer();
                return;
            }
    
            // 撞到墙壁
            if (layerName == GameConstants.WALL_LAYER || other.CompareTag(GameConstants.WALL_TAG))
            {
                DestroyProjectile(false);
            }
        }

        /// <summary>
        /// 撞到玩家 - 造成护盾伤害
        /// </summary>
        private void OnHitPlayer()
        {
            if (showDebugInfo)
            {
                Debug.Log($"[PollutionProjectile] 🎯 命中玩家！造成 {shieldDamage} 点护盾伤害");
            }
            
            ApplyDamageToPlayer();
            DestroyProjectile(true);
        }
        
        /// <summary>
        /// 对玩家造成伤害
        /// </summary>
        private void ApplyDamageToPlayer()
        {
            ShieldController shield = FindObjectOfType<ShieldController>();
            TurretHealth turret = FindObjectOfType<TurretHealth>();
            
            if (shield != null)
            {
                int remaining = shield.TakeBossDamage(shieldDamage);
                if (remaining > 0 && turret != null)
                {
                    turret.TakeBossDamage(remaining);
                }
            }
            else if (turret != null)
            {
                turret.TakeBossDamage(shieldDamage);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 销毁处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void DestroyProjectile(bool playExplosion)
        {
            if (isDestroyed) return;
            isDestroyed = true;
            
            // 从Boss管理列表中移除
            if (bossController != null)
            {
                bossController.UnregisterPollutionBall(this);
            }
            
            // 停止移动
            rb.velocity = Vector2.zero;
            
            // 禁用碰撞
            col.enabled = false;
            
            // 停止飞行拖尾粒子
            if (orbParticle != null)
            {
                orbParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                orbParticle.gameObject.SetActive(false);
            }
            
            // 播放爆炸特效
            if (playExplosion && explosionParticle != null)
            {
                Vector3 explosionPos = transform.position;
                
                explosionParticle.transform.SetParent(null);
                explosionParticle.transform.position = explosionPos;
                explosionParticle.Play();
                
                Destroy(explosionParticle.gameObject, explosionParticle.main.duration + 0.5f);
            }
            
            // 销毁主体
            Destroy(gameObject, 0.05f);
            
            if (showDebugInfo)
            {
                Debug.Log($"[PollutionProjectile] 销毁 (爆炸: {playExplosion})");
            }
        }
        
        /// <summary>
        /// 强制销毁（由BossController调用，用于清理最老的球）
        /// </summary>
        public void ForceDestroy()
        {
            if (showDebugInfo)
            {
                Debug.Log("[PollutionProjectile] 强制销毁（数量限制）");
            }
            DestroyProjectile(false);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 兼容旧版初始化
        /// </summary>
        public void Initialize(float speed, float turn, int damage, float life)
        {
            moveSpeed = speed;
            turnSpeed = turn;
            shieldDamage = damage;
            lifetime = life;
        }
        
        /// <summary>
        /// V3.0 初始化（带物理参数）
        /// </summary>
        public void InitializeV3(float speed, float turn, int damage, float life, 
            float hp, float ballMass, BossController controller)
        {
            moveSpeed = speed;
            turnSpeed = turn;
            shieldDamage = damage;
            lifetime = life;
            maxHP = hp;
            currentHP = hp;
            mass = ballMass;
            bossController = controller;
            
            if (rb != null)
            {
                rb.mass = mass;
            }
            
            // 查找BossHealth
            if (controller != null)
            {
                bossHealth = controller.GetComponent<BossHealth>();
            }
        }
        
        /// <summary>
        /// 当前HP
        /// </summary>
        public float CurrentHP => currentHP;
        
        /// <summary>
        /// 是否已销毁
        /// </summary>
        public bool IsDestroyed => isDestroyed;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Gizmos 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            
            // 绘制当前方向
            Gizmos.color = isBeingPushed ? Color.red : Color.magenta;
            Gizmos.DrawRay(transform.position, currentDirection * 2f);
            
            // 绘制到目标的线
            if (target != null)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
                Gizmos.DrawLine(transform.position, target.position);
            }
            
            // 绘制HP条
            Vector3 hpBarPos = transform.position + Vector3.up * 0.5f;
            float hpPercent = currentHP / maxHP;
            Gizmos.color = Color.Lerp(Color.red, Color.green, hpPercent);
            Gizmos.DrawWireCube(hpBarPos, new Vector3(hpPercent, 0.1f, 0));
        }
#endif
    }
}