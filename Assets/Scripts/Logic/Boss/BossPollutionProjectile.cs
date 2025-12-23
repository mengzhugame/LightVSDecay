// ============================================================
// BossPollutionProjectile.cs
// 文件位置: Assets/Scripts/Logic/Boss/BossPollutionProjectile.cs
// 用途：BOSS 技能 C - 污秽喷吐投射物（惰性追踪弹）
// 状态：【新建文件】
// ============================================================

using LightVsDecay.Core;
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
        [Tooltip("飞行拖尾粒子系统（VFX_Pollution_Orb）")]
        [SerializeField] private ParticleSystem orbParticle;
        [Tooltip("爆炸粒子系统")]
        [SerializeField] private ParticleSystem explosionParticle;

        [Header("Layer 设置")]
        [Tooltip("玩家塔所在 Layer（用于命中检测）")]
        [SerializeField] private LayerMask playerTowerLayer;
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
            string layerName = LayerMask.LayerToName(otherLayer);
    
            if (showDebugInfo)
            {
                Debug.Log($"[PollutionProjectile] 触发碰撞: {other.gameObject.name}, Layer: {layerName}");
                Debug.Log($"[PollutionProjectile] playerTowerLayer.value = {playerTowerLayer.value}, 检测位 = {(1 << otherLayer)}");
            }
            // 【新增】墙体碰撞 - 直接销毁
            if (layerName == GameConstants.WALL_LAYER || other.CompareTag(GameConstants.WALL_TAG))
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[PollutionProjectile] 撞击空气墙，销毁");
                }
                DestroyProjectile(false); // 不触发伤害效果
                return;
            }
            // 【方案A】直接用 Layer 名称判断（更可靠）
            if (layerName == "Shield" || layerName == "Tower")
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[PollutionProjectile] 🎯 命中 {layerName}！造成 {shieldDamage} 点护盾伤害");
                }
        
                ApplyDamage();
                DestroyProjectile(true);
                return;
            }
    
            // 【方案B】后备检测：通过组件
            ShieldController shield = other.GetComponent<ShieldController>();
            if (shield == null) shield = other.GetComponentInParent<ShieldController>();
    
            TurretHealth turret = other.GetComponent<TurretHealth>();
            if (turret == null) turret = other.GetComponentInParent<TurretHealth>();
    
            if (shield != null || turret != null)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[PollutionProjectile] 🎯 命中玩家组件！造成 {shieldDamage} 点护盾伤害");
                }
        
                ApplyDamage();
                DestroyProjectile(true);
            }
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
    
            // 【关键】停止飞行拖尾粒子
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
        
                if (showDebugInfo)
                {
                    Debug.Log($"[PollutionProjectile] 💥 爆炸特效播放 @ {explosionPos}");
                }
            }
    
            // 立即销毁主体
            Destroy(gameObject, 0.05f);
    
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