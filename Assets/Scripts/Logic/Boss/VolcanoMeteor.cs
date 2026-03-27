// ============================================================
// VolcanoMeteor.cs
// 文件位置: Assets/Scripts/Logic/Boss/VolcanoMeteor.cs
// 用途：熔炉巨兽技能——陨石喷发投射物
//   - 从高处以恒定速度垂直下落至目标位置
//   - 着陆时在圆形区域内造成伤害
//   - 着陆后生成 LavaPuddle（熔岩水坑）
//   - 落前 1.5s 在目标位置显示红圈预警
// ============================================================

using UnityEngine;
using System.Collections;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Logic.Player;
using LightVsDecay.Logic.Statistics;

namespace LightVsDecay.Logic.Boss
{
    /// <summary>
    /// 熔炉巨兽陨石投射物。
    /// 由 VolcanoBossController 在技能触发时 Instantiate。
    /// </summary>
    public class VolcanoMeteor : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("下落参数")]
        [Tooltip("下落速度（米/秒）")]
        [SerializeField] private float fallSpeed = 8f;

        [Header("伤害参数")]
        [Tooltip("着陆伤害（对护盾）")]
        [SerializeField] private int landingDamage = 600;

        [Tooltip("着陆伤害半径")]
        [SerializeField] private float damageRadius = 1.2f;

        [Header("预警")]
        [Tooltip("落点预警圈 SpriteRenderer（由预制体提供）")]
        [SerializeField] private SpriteRenderer warningCircle;

        [Header("水坑生成")]
        [Tooltip("着陆后是否生成熔岩水坑")]
        [SerializeField] private bool spawnPuddleOnLand = true;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private Vector3 targetPosition;
        private bool isFalling = false;
        private bool hasLanded = false;

        // 检测层缓存
        private LayerMask shieldLayer;
        private LayerMask towerLayer;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            shieldLayer = LayerMask.GetMask("Shield");
            towerLayer  = LayerMask.GetMask("Tower");
        }

        /// <summary>
        /// 由 VolcanoBossController 调用，传入着陆目标世界坐标。
        /// 陨石从 targetPos + 高度偏移处开始下落。
        /// </summary>
        public void Launch(Vector3 targetPos, float spawnHeight = 12f)
        {
            targetPosition = new Vector3(targetPos.x, targetPos.y, targetPos.z);
            transform.position = new Vector3(targetPos.x, targetPos.y + spawnHeight, targetPos.z);

            // 预警圈显示在目标落点（世界坐标）
            if (warningCircle != null)
            {
                warningCircle.transform.position = new Vector3(targetPos.x, targetPos.y, targetPos.z);
                warningCircle.gameObject.SetActive(true);
            }

            isFalling = true;
            hasLanded = false;

            if (showDebugInfo)
                GameLogger.Log($"[VolcanoMeteor] 陨石启动，目标 {targetPos}");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 下落更新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Update()
        {
            if (!isFalling || hasLanded) return;

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                fallSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
            {
                transform.position = targetPosition;
                OnLanded();
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 着陆
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnLanded()
        {
            hasLanded = true;
            isFalling = false;

            if (warningCircle != null)
                warningCircle.gameObject.SetActive(false);

            // 范围伤害检测
            ApplyLandingDamage();

            // 生成熔岩水坑
            if (spawnPuddleOnLand && EnemyPoolManager.Instance != null)
            {
                EnemyPoolManager.Instance.Spawn(LightVsDecay.Core.Pool.EnemyType.LavaPuddle, targetPosition);
            }

            if (showDebugInfo)
                GameLogger.Log($"[VolcanoMeteor] 着陆！位置 {targetPosition}，伤害 {landingDamage}");

            Destroy(gameObject, 0.1f);
        }

        private void ApplyLandingDamage()
        {
            // 检测护盾
            Collider2D[] shields = Physics2D.OverlapCircleAll(targetPosition, damageRadius, shieldLayer);
            foreach (var col in shields)
            {
                var shield = col.GetComponent<ShieldController>();
                if (shield != null)
                {
                    if (BattleStatistics.Instance != null)
                        BattleStatistics.Instance.RecordPlayerDamage(landingDamage, PlayerDamageSource.BossBullet);
                    shield.TakeDamage(landingDamage);
                }
            }

            // 检测塔本体（护盾不在范围内时）
            if (shields.Length == 0)
            {
                Collider2D[] towers = Physics2D.OverlapCircleAll(targetPosition, damageRadius, towerLayer);
                foreach (var col in towers)
                {
                    var turret = col.GetComponent<TurretHealth>();
                    if (turret != null)
                    {
                        if (BattleStatistics.Instance != null)
                            BattleStatistics.Instance.RecordPlayerDamage(landingDamage, PlayerDamageSource.BossBullet);
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
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
            Gizmos.DrawWireSphere(targetPosition, damageRadius);
        }
#endif
    }
}
