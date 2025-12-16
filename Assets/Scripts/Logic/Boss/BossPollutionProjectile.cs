// ============================================================
// BossPollutionProjectile.cs
// 文件位置: Assets/Scripts/Logic/Boss/BossPollutionProjectile.cs
// 用途：BOSS 技能 C - 污秽喷吐投射物（惰性追踪弹）
// 状态：【新建文件】
// ============================================================

using UnityEngine;
using LightVsDecay.Logic.Player;

namespace LightVsDecay.Logic.Boss
{
    /// <summary>
    /// BOSS 污秽喷吐投射物
    /// 【惰性追踪】慢速追踪弹，转弯半径大，像沉重的导弹
    /// 【一击即爆】被激光命中立即销毁
    /// 【污染伤害】命中塔扣除护盾
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
        
        [Header("伤害参数")]
        [Tooltip("命中护盾伤害")]
        [SerializeField] private int shieldDamage = 100;
        
        [Header("视觉效果")]
        //[Tooltip("拖尾粒子系统")]
        //[SerializeField] private ParticleSystem trailParticle;
        
        [Tooltip("爆炸粒子系统")]
        [SerializeField] private ParticleSystem explosionParticle;
        
        [Tooltip("主体 SpriteRenderer")]
        [SerializeField] private SpriteRenderer bodyRenderer;
        
        [Header("Layer 设置")]
        [Tooltip("玩家塔所在 Layer（用于命中检测）")]
        [SerializeField] private LayerMask playerTowerLayer;
        
        [Tooltip("激光所在 Layer（用于拦截检测）")]
        [SerializeField] private LayerMask laserLayer;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 组件缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Rigidbody2D rb;
        private Collider2D col;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Transform target;
        private Vector2 currentDirection;
        private float spawnTime;
        private bool isDestroyed = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            
            // 配置刚体
            rb.gravityScale = 0f;
            rb.drag = 0f;
            rb.angularDrag = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            
            // 配置碰撞器为触发器
            col.isTrigger = true;
            
            // 自动查找组件
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            // if (trailParticle == null)
            // {
            //     trailParticle = GetComponentInChildren<ParticleSystem>();
            // }
        }
        
        private void Start()
        {
            spawnTime = Time.time;
            
            // 查找目标（玩家塔）
            FindTarget();
            
            // 初始方向：朝向目标
            if (target != null)
            {
                currentDirection = ((Vector2)target.position - (Vector2)transform.position).normalized;
            }
            else
            {
                currentDirection = Vector2.down; // 默认向下
            }
            
            // // 启动拖尾特效
            // if (trailParticle != null)
            // {
            //     trailParticle.Play();
            // }
            
            if (showDebugInfo)
            {
                Debug.Log($"[PollutionProjectile] 生成 @ {transform.position}, 目标: {(target != null ? target.name : "无")}");
            }
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
            
            // 惰性追踪逻辑
            UpdateTracking();
            
            // 移动
            rb.velocity = currentDirection * moveSpeed;
            
            // 旋转朝向移动方向
            float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);
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
            
            // 惰性追踪：缓慢转向（转弯半径大的效果）
            float maxTurnAngle = turnSpeed * Time.fixedDeltaTime;
            
            // 当前角度
            float currentAngle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
            // 目标角度
            float targetAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
            
            // 计算角度差（-180 到 180）
            float angleDiff = Mathf.DeltaAngle(currentAngle, targetAngle);
            
            // 限制最大转向角度
            float turnAngle = Mathf.Clamp(angleDiff, -maxTurnAngle, maxTurnAngle);
            
            // 应用转向
            float newAngle = currentAngle + turnAngle;
            currentDirection = new Vector2(
                Mathf.Cos(newAngle * Mathf.Deg2Rad),
                Mathf.Sin(newAngle * Mathf.Deg2Rad)
            );
        }
        
        private void FindTarget()
        {
            // 查找玩家塔（优先查找 ShieldController，然后 TurretHealth）
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
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 碰撞检测
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDestroyed) return;
            
            int otherLayer = other.gameObject.layer;
            
            // 检查是否被激光命中（一击即爆）
            if (IsInLayerMask(otherLayer, laserLayer))
            {
                if (showDebugInfo)
                {
                    Debug.Log("[PollutionProjectile] 被激光拦截！");
                }
                DestroyProjectile(true);
                return;
            }
            
            // 检查是否命中玩家塔/护盾（通过 Layer）
            if (IsInLayerMask(otherLayer, playerTowerLayer))
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[PollutionProjectile] 命中玩家！造成 {shieldDamage} 点护盾伤害");
                }
                
                ApplyDamage();
                DestroyProjectile(true);
                return;
            }
            
            // 检查 Shield 或 Turret 组件（作为后备检测）
            ShieldController shield = other.GetComponent<ShieldController>();
            TurretHealth turret = other.GetComponent<TurretHealth>();
            
            if (shield != null || turret != null)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[PollutionProjectile] 命中玩家组件！造成 {shieldDamage} 点护盾伤害");
                }
                
                ApplyDamage();
                DestroyProjectile(true);
            }
        }
        
        private bool IsInLayerMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ApplyDamage()
        {
            // 查找护盾控制器
            ShieldController shield = FindObjectOfType<ShieldController>();
            TurretHealth turret = FindObjectOfType<TurretHealth>();
            
            if (shield != null)
            {
                // 先扣护盾
                int remaining = shield.TakeBossDamage(shieldDamage);
                
                // 剩余伤害扣本体
                if (remaining > 0 && turret != null)
                {
                    turret.TakeBossDamage(remaining);
                }
            }
            else if (turret != null)
            {
                // 没护盾，直接扣本体
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
            
            // 停止移动
            rb.velocity = Vector2.zero;
            
            // 禁用碰撞
            col.enabled = false;
            
            // 隐藏主体
            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = false;
            }
            
            // // 停止拖尾
            // if (trailParticle != null)
            // {
            //     trailParticle.Stop();
            // }
            
            // 播放爆炸特效
            if (playExplosion && explosionParticle != null)
            {
                explosionParticle.transform.SetParent(null); // 脱离父物体
                explosionParticle.Play();
                Destroy(explosionParticle.gameObject, explosionParticle.main.duration + 0.5f);
            }
            
            // // 延迟销毁（等待拖尾消失）
            // float destroyDelay = (trailParticle != null) ? trailParticle.main.duration : 0.1f;
            // Destroy(gameObject, destroyDelay);
            Destroy(gameObject, 1.0f);
            
            if (showDebugInfo)
            {
                Debug.Log($"[PollutionProjectile] 销毁 (爆炸: {playExplosion})");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 初始化投射物参数（由 BossController 调用）
        /// </summary>
        public void Initialize(float speed, float turn, int damage, float life)
        {
            moveSpeed = speed;
            turnSpeed = turn;
            shieldDamage = damage;
            lifetime = life;
        }
        
        /// <summary>
        /// 被激光击中（外部调用的替代入口）
        /// </summary>
        public void OnHitByLaser()
        {
            if (!isDestroyed)
            {
                if (showDebugInfo)
                {
                    Debug.Log("[PollutionProjectile] 被激光击中！");
                }
                DestroyProjectile(true);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Gizmos 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            
            // 绘制当前方向
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position, currentDirection * 2f);
            
            // 绘制到目标的线
            if (target != null)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
                Gizmos.DrawLine(transform.position, target.position);
            }
        }
#endif
    }
}