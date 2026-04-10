// ============================================================
// LavaProjectile.cs
// 文件位置: Assets/Scripts/Logic/Enemy/LavaProjectile.cs
// 用途：熔岩炮手弹道投射物 / 火山Boss火球投射物
//   - 直线模式（Initialize）：直线飞向目标，命中护盾/塔造成伤害
//   - 贝塞尔模式（LaunchBezier）：沿二次贝塞尔曲线飞行，落地造成范围伤害
//   - 可被激光打爆（HP 耗尽即销毁）
//   - 使用 BossPollutionBall 层（已与 Shield/Tower 层配置碰撞）
// ============================================================

using System.Collections;
using UnityEngine;
using LightVsDecay.Audio;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Logic.Player;
using LightVsDecay.Logic.Statistics;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// 熔岩投射物：支持直线飞行和贝塞尔曲线飞行两种模式。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class LavaProjectile : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Tooltip("Sprite 默认朝向偏移（Sprite 朝右=0，朝上=-90，朝左=180）")]
        [SerializeField] private float rotationOffset = 0f;

        [Tooltip("贝塞尔模式落地伤害半径")]
        [SerializeField] private float bezierLandRadius = 1.2f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private Rigidbody2D rb;
        private AudioSource flightAudioSource;
        private float speed;
        private int   damage;
        private float maxHP;
        private float currentHP;
        private bool  isDead        = false;
        private float lifetime      = 8f;
        private float lifetimeTimer = 0f;
        private bool  audioPausedByOverlay = false;

        // 贝塞尔飞行期间禁用普通碰撞检测，落地由协程手动处理
        private bool isBezierFlight = false;

        private LayerMask shieldLayer;
        private LayerMask towerLayer;

        /// <summary>是否已销毁（供 LaserController 判断）</summary>
        public bool IsDestroyed => isDead;

        // Ch3：冰刺专用标记——命中光棱塔时降低其旋转灵敏度
        private bool enableRotationSlow = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            flightAudioSource = GetComponent<AudioSource>();

            shieldLayer = LayerMask.GetMask("Shield");
            towerLayer  = LayerMask.GetMask("Tower");

            // 确保子弹在 BossPollutionBall 层，使激光可以检测并击中它
            int layer = LayerMask.NameToLayer("BossPollutionBall");
            if (layer >= 0)
                gameObject.layer = layer;

            // 使用触发器模式：子弹穿透所有怪物，只对 Shield / Tower / Wall 响应
            var col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = true;

            if (flightAudioSource != null)
            {
                flightAudioSource.playOnAwake = false;
                flightAudioSource.loop = true;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 直线飞行模式（LavaGunnerAI / Boss 直射使用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 由 LavaGunnerAI 在射击时调用，设置方向、速度和属性。
        /// </summary>
        public void Initialize(Vector2 direction, float speed, int damage, float hp, float lifetime = 8f, bool rotationSlow = false)
        {
            this.speed    = speed;
            this.damage   = damage;
            this.maxHP    = hp;
            this.currentHP = hp;
            this.lifetime = lifetime;
            isDead        = false;
            lifetimeTimer = 0f;
            isBezierFlight = false;
            enableRotationSlow = rotationSlow;
            audioPausedByOverlay = false;

            // 根据飞行方向计算旋转，使 Sprite 朝向目标
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.velocity = direction.normalized * speed;
            }

            StartFlightAudio();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 贝塞尔曲线飞行模式（火山Boss火球喷发使用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 由 VolcanoBossController 调用，沿贝塞尔曲线飞行并落地造成伤害。
        /// </summary>
        /// <param name="p0">起点（火山口）</param>
        /// <param name="p1">控制点（决定弧顶高度）</param>
        /// <param name="p2">落点（目标位置）</param>
        /// <param name="travelTime">飞行时长（秒）</param>
        /// <param name="bezierDamage">落地伤害值</param>
        public void LaunchBezier(Vector3 p0, Vector3 p1, Vector3 p2, float travelTime, int bezierDamage)
        {
            damage        = bezierDamage;
            maxHP         = 999f;
            currentHP     = maxHP;
            isDead        = false;
            lifetimeTimer = 0f;
            lifetime      = travelTime + 5f; // 给足够余量，由协程负责销毁
            isBezierFlight = true;
            audioPausedByOverlay = false;

            if (rb != null)
            {
                rb.velocity    = Vector2.zero;
                rb.isKinematic = true; // 由协程直接控制位置，禁用物理推力
            }

            transform.position = p0;
            StartFlightAudio();
            StartCoroutine(BezierRoutine(p0, p1, p2, travelTime));
        }

        private IEnumerator BezierRoutine(Vector3 p0, Vector3 p1, Vector3 p2, float travelTime)
        {
            float elapsed = 0f;

            while (elapsed < travelTime)
            {
                if (isDead) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / travelTime);
                float u = 1f - t;

                // 二次贝塞尔位置：(1-t)²P0 + 2(1-t)tP1 + t²P2
                Vector3 pos = u * u * p0 + 2f * u * t * p1 + t * t * p2;
                transform.position = pos;

                // 切线方向 → 旋转朝向飞行方向
                Vector3 tangent = 2f * u * (p1 - p0) + 2f * t * (p2 - p1);
                if (tangent.sqrMagnitude > 0.001f)
                {
                    float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg + rotationOffset;
                    transform.rotation = Quaternion.Euler(0f, 0f, angle);
                }

                yield return null;
            }

            transform.position = p2;
            isBezierFlight = false;
            OnBezierLanded(p2);
        }

        private void OnBezierLanded(Vector3 targetPos)
        {
            // 落地范围伤害检测
            Collider2D[] shields = Physics2D.OverlapCircleAll(targetPos, bezierLandRadius, shieldLayer);
            bool hitShield = false;
            foreach (var col in shields)
            {
                var shield = col.GetComponent<ShieldController>();
                if (shield != null)
                {
                    BattleStatistics.Instance?.RecordPlayerDamage(damage, PlayerDamageSource.BossMeteor);
                    shield.TakeDamage(damage);
                    hitShield = true;
                }
            }

            if (!hitShield)
            {
                Collider2D[] towers = Physics2D.OverlapCircleAll(targetPos, bezierLandRadius, towerLayer);
                foreach (var col in towers)
                {
                    var turret = col.GetComponent<TurretHealth>();
                    if (turret != null)
                    {
                        BattleStatistics.Instance?.RecordPlayerDamage(damage, PlayerDamageSource.BossMeteor);
                        turret.TakeDamage(damage);
                    }
                }
            }

            // 火球落地计数（对应 CSV: Boss_Fireball_Hit_Count）
            BattleStatistics.Instance?.RecordBossMeteor();

            DestroyProjectile();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Update()
        {
            if (isDead) return;
            UpdateFlightAudioPauseState();
            lifetimeTimer += Time.deltaTime;
            if (lifetimeTimer >= lifetime)
                DestroyProjectile();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 激光伤害接口（由 LaserController 调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void TakeDamage(float amount)
        {
            if (isDead) return;
            currentHP -= amount;
            if (currentHP <= 0f)
                DestroyProjectile(byLaser: true);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 碰撞检测（仅直线模式有效，贝塞尔飞行期间跳过）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (isDead || isBezierFlight) return;
            HandleHit(collision.gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDead || isBezierFlight) return;
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
                    BattleStatistics.Instance?.RecordPlayerDamage(damage, PlayerDamageSource.MobBullet);
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
                    BattleStatistics.Instance?.RecordPlayerDamage(damage, PlayerDamageSource.MobBullet);
                    turret.TakeDamage(damage);
                }
                // Ch3：冰刺命中塔时降低旋转灵敏度（FindObjectOfType 适用于一次性命中事件）
                if (enableRotationSlow)
                {
                    BattleStatistics.Instance?.RecordIceSpikeHit();
                    var turretCtrl = Object.FindObjectOfType<TurretController>();
                    turretCtrl?.ApplyRotationSlow(2f);
                }
                DestroyProjectile();
                return;
            }

            if (layerName == GameConstants.WALL_LAYER)
                DestroyProjectile();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 销毁
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void DestroyProjectile(bool byLaser = false)
        {
            if (isDead) return;
            isDead = true;
            StopFlightAudio();

            if (byLaser)
            {
                VFXPoolManager.Instance?.PlayProjectileExplosion(transform.position);
                AudioManager.Instance?.PlayProjectileExplode();

                // 贝塞尔模式（Boss火球）被激光拦截时记录
                if (isBezierFlight)
                    BattleStatistics.Instance?.RecordBossFireballIntercepted();
                // Ch3：冰刺被激光拦截时记录
                else if (enableRotationSlow)
                    BattleStatistics.Instance?.RecordIceSpikeIntercepted();
            }

            Destroy(gameObject);
        }

        private void StartFlightAudio()
        {
            if (flightAudioSource == null || flightAudioSource.clip == null)
                return;

            if (!flightAudioSource.isPlaying)
                flightAudioSource.Play();
        }

        private void StopFlightAudio()
        {
            if (flightAudioSource == null)
                return;

            audioPausedByOverlay = false;
            if (flightAudioSource.isPlaying)
                flightAudioSource.Stop();
        }

        private void UpdateFlightAudioPauseState()
        {
            if (flightAudioSource == null || isDead)
                return;

            if (Time.timeScale <= 0f)
            {
                if (flightAudioSource.isPlaying)
                {
                    flightAudioSource.Pause();
                    audioPausedByOverlay = true;
                }
                return;
            }

            if (audioPausedByOverlay)
            {
                flightAudioSource.UnPause();
                audioPausedByOverlay = false;
            }
        }
    }
}
