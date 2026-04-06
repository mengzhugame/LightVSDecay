// ============================================================
// VolcanoFireball.cs
// 文件位置: Assets/Scripts/Logic/Boss/VolcanoFireball.cs
// 用途：火山Boss技能——火球喷发投射物
//   - 从火山顶部沿二次贝塞尔曲线飞向光棱塔
//   - 飞行时自动旋转朝向切线方向
//   - 着陆时在圆形区域内造成伤害
//   - 可被激光拦截击落（HP归零则摧毁）
// ============================================================

using UnityEngine;
using System.Collections;
using LightVsDecay.Audio;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Logic.Player;
using LightVsDecay.Logic.Statistics;

namespace LightVsDecay.Logic.Boss
{
    /// <summary>
    /// 火山Boss火球投射物，沿贝塞尔曲线飞行。
    /// </summary>
    public class VolcanoFireball : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("伤害参数")]
        [Tooltip("着陆伤害（对护盾/塔）")]
        [SerializeField] private int landingDamage = 400;

        [Tooltip("着陆伤害半径")]
        [SerializeField] private float damageRadius = 1.2f;

        [Header("激光拦截")]
        [Tooltip("火球生命值（激光击落所需总伤害）")]
        [SerializeField] private float fireballHP = 10f;

        [Header("水坑生成")]
        [Tooltip("着陆后是否生成熔岩水坑")]
        [SerializeField] private bool spawnPuddleOnLand = false;

        [Header("Sprite 朝向")]
        [Tooltip("Sprite 默认朝向偏移（朝右=0，朝上=-90，朝下=90，朝左=180）")]
        [SerializeField] private float rotationOffset = 0f;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private float currentHP;
        private bool  isResolved;

        private LayerMask shieldLayer;
        private LayerMask towerLayer;

        public bool IsDestroyed => isResolved;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            shieldLayer = LayerMask.GetMask("Shield");
            towerLayer  = LayerMask.GetMask("Tower");

            // 设置到 BossPollutionBall 层，使激光可以检测并拦截
            int layer = LayerMask.NameToLayer("BossPollutionBall");
            if (layer >= 0) gameObject.layer = layer;
        }

        /// <summary>
        /// 由 VolcanoBossController 调用，启动贝塞尔曲线飞行。
        /// </summary>
        /// <param name="p0">起点（火山顶部）</param>
        /// <param name="p1">控制点（弧顶，决定曲线弧度）</param>
        /// <param name="p2">终点（目标落点）</param>
        /// <param name="travelTime">飞行时长（秒）</param>
        public void Launch(Vector3 p0, Vector3 p1, Vector3 p2, float travelTime)
        {
            currentHP  = fireballHP;
            isResolved = false;
            transform.position = p0;
            StartCoroutine(BezierMoveRoutine(p0, p1, p2, travelTime));
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 激光拦截接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>被激光命中时调用（由 LaserController 驱动）</summary>
        public void TakeDamage(float amount)
        {
            if (isResolved) return;
            currentHP -= amount;
            if (currentHP <= 0f)
            {
                isResolved = true;
                StopAllCoroutines();

                VFXPoolManager.Instance?.PlayProjectileExplosion(transform.position);
                AudioManager.Instance?.PlayProjectileExplode();

                if (showDebugInfo) GameLogger.Log("[VolcanoFireball] 被激光击落！");
                Destroy(gameObject, 0.1f);
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 贝塞尔曲线飞行
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator BezierMoveRoutine(Vector3 p0, Vector3 p1, Vector3 p2, float travelTime)
        {
            float elapsed = 0f;

            while (elapsed < travelTime)
            {
                if (isResolved) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / travelTime);
                float u = 1f - t;

                // 二次贝塞尔位置公式：(1-t)²P0 + 2(1-t)tP1 + t²P2
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
            OnLanded(p2);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 着陆
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnLanded(Vector3 targetPos)
        {
            if (isResolved) return;
            isResolved = true;

            ApplyLandingDamage(targetPos);

            if (spawnPuddleOnLand && EnemyPoolManager.Instance != null)
            {
                EnemyPoolManager.Instance.Spawn(EnemyType.LavaPuddle, targetPos);
                BattleStatistics.Instance?.RecordPuddleSpawned();
            }

            VFXPoolManager.Instance?.PlayProjectileExplosion(transform.position);
            AudioManager.Instance?.PlayProjectileExplode();

            if (showDebugInfo)
                GameLogger.Log($"[VolcanoFireball] 着陆！位置 {targetPos}，伤害 {landingDamage}");

            Destroy(gameObject, 0.1f);
        }

        private void ApplyLandingDamage(Vector3 targetPos)
        {
            Collider2D[] shields = Physics2D.OverlapCircleAll(targetPos, damageRadius, shieldLayer);
            foreach (var col in shields)
            {
                var shield = col.GetComponent<ShieldController>();
                if (shield != null)
                {
                    BattleStatistics.Instance?.RecordPlayerDamage(landingDamage, PlayerDamageSource.BossMeteor);
                    shield.TakeDamage(landingDamage);
                }
            }

            if (shields.Length == 0)
            {
                Collider2D[] towers = Physics2D.OverlapCircleAll(targetPos, damageRadius, towerLayer);
                foreach (var col in towers)
                {
                    var turret = col.GetComponent<TurretHealth>();
                    if (turret != null)
                    {
                        BattleStatistics.Instance?.RecordPlayerDamage(landingDamage, PlayerDamageSource.BossMeteor);
                        turret.TakeDamage(landingDamage);
                    }
                }
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器辅助
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, damageRadius);
        }
#endif
    }
}
