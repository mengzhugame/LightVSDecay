// ============================================================
// LavaProjectile.cs
// 文件位置: Assets/Scripts/Logic/Enemy/LavaProjectile.cs
// 用途：熔岩炮手弹道投射物
//   - 直线飞向目标方向
//   - 命中护盾/塔造成固定伤害后销毁
//   - 可被激光打爆（HP 耗尽即销毁）
//   - 使用 BossPollutionBall 层（已与 Shield/Tower 层配置碰撞）
// ============================================================

using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Logic.Player;
using LightVsDecay.Logic.Statistics;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// 熔岩炮手投射物：直线飞行，命中玩家塔/护盾造成伤害。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class LavaProjectile : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时参数（由 LavaGunnerAI 在射击时注入）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private Rigidbody2D rb;
        private float speed;
        private int damage;
        private float maxHP;
        private float currentHP;
        private bool isDead = false;
        private float lifetime = 8f;
        private float lifetimeTimer = 0f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>是否已销毁（供 LaserController 判断）</summary>
        public bool IsDestroyed => isDead;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;

            // 确保子弹在 BossPollutionBall 层，使激光可以检测并击中它
            int layer = LayerMask.NameToLayer("BossPollutionBall");
            if (layer >= 0)
                gameObject.layer = layer;

            // 使用触发器模式：子弹穿透所有怪物，只对 Shield / Tower / Wall 响应
            var col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = true;
        }

        /// <summary>
        /// 由 LavaGunnerAI 在射击时调用，设置方向、速度和属性。
        /// </summary>
        public void Initialize(Vector2 direction, float speed, int damage, float hp, float lifetime = 8f)
        {
            this.speed = speed;
            this.damage = damage;
            this.maxHP = hp;
            this.currentHP = hp;
            this.lifetime = lifetime;
            isDead = false;
            lifetimeTimer = 0f;

            if (rb != null)
                rb.velocity = direction.normalized * speed;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Update()
        {
            if (isDead) return;
            lifetimeTimer += Time.deltaTime;
            if (lifetimeTimer >= lifetime)
            {
                DestroyProjectile();
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 激光伤害接口（由 LaserController 调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 受到激光伤害（无推力版，投射物由激光直接削血）。
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (isDead) return;
            currentHP -= amount;
            if (currentHP <= 0f)
            {
                DestroyProjectile();
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 碰撞检测
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (isDead) return;
            HandleHit(collision.gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDead) return;
            HandleHit(other.gameObject);
        }

        private void HandleHit(GameObject target)
        {
            string layerName = LayerMask.LayerToName(target.layer);

            if (layerName == "Shield")
            {
                var shield = target.GetComponent<ShieldController>();
                if (shield != null)
                {
                    // 上报伤害统计
                    if (BattleStatistics.Instance != null)
                        BattleStatistics.Instance.RecordPlayerDamage(damage, PlayerDamageSource.BossBullet);
                    shield.TakeDamage(damage);
                }
                DestroyProjectile();
                return;
            }

            if (layerName == "Tower")
            {
                var turret = target.GetComponent<TurretHealth>();
                if (turret != null)
                {
                    if (BattleStatistics.Instance != null)
                        BattleStatistics.Instance.RecordPlayerDamage(damage, PlayerDamageSource.BossBullet);
                    turret.TakeDamage(damage);
                }
                DestroyProjectile();
                return;
            }

            // 飞出边界（墙）
            if (layerName == GameConstants.WALL_LAYER)
            {
                DestroyProjectile();
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 销毁
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DestroyProjectile()
        {
            if (isDead) return;
            isDead = true;
            // TODO: 播放熔岩弹爆炸特效（Stage 5 美术资源到位后添加）
            Destroy(gameObject);
        }
    }
}
