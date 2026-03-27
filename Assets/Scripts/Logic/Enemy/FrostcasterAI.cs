// ============================================================
// FrostcasterAI.cs
// 文件位置: Assets/Scripts/Logic/Enemy/FrostcasterAI.cs
// 用途：霜冻施法者 AI 状态机（第三章）
// 行为流程：
//   Entering → 从屏幕顶部入场，到达目标 Y 后停驻
//   Idle     → 等待 castInterval 秒
//   Casting  → 在场地随机位置召唤 N 面冰墙，短暂停顿后回 Idle
// ============================================================

using System.Collections;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data.SO;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// 霜冻施法者 AI 控制器。
    /// 挂载在与 EnemyBlob 相同的 GameObject 上，
    /// EnemyBlob.MoveTowardsTower 在 FrostCaster 模式下不执行，
    /// 此组件全权负责 Rigidbody2D 速度控制。
    /// </summary>
    [RequireComponent(typeof(EnemyBlob))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class FrostcasterAI : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态枚举
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private enum CasterState { Entering, Idle, Casting }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private EnemyBlob blob;
        private Rigidbody2D rb;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置（从 EnemyData 读取）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private float stopYPercent = 0.7f;
        private float castInterval = 10f;
        private int iceWallCount = 1;
        private bool randomWallCount = false;
        private EnemyType iceWallType = EnemyType.IceWall;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private CasterState state = CasterState.Entering;
        private float targetWorldY;
        private float idleTimer;
        private bool isActive = false;

        // 常量
        private const float ENTRY_SPEED  = 3f;
        private const float SNAP_DIST    = 0.08f;
        private const float CAST_PAUSE   = 0.5f;  // 施法后停顿
        // 冰墙召唤区域（屏幕内随机，排除靠近玩家塔的下半屏）
        private const float WALL_MARGIN  = 1.5f;  // 距屏幕边缘最小距离
        private const float WALL_SPAWN_MAX_Y_RATIO = 0.75f; // 最低不超过屏幕 75% 处

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            blob = GetComponent<EnemyBlob>();
            rb   = GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate()
        {
            if (!isActive || blob == null || blob.IsDead) return;

            switch (state)
            {
                case CasterState.Entering:
                    UpdateEntering();
                    break;
                case CasterState.Idle:
                    UpdateIdle();
                    break;
                case CasterState.Casting:
                    rb.velocity = Vector2.zero;
                    break;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 对象池事件（由 EnemyBlob 调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// EnemyBlob.OnSpawn() 时调用，重新读取配置并开始入场。
        /// </summary>
        public void OnBlobSpawned()
        {
            StopAllCoroutines();

            var data = blob.Data;
            if (data == null) return;

            stopYPercent    = data.frostcasterStopYPercent;
            castInterval    = data.frostcasterCastInterval;
            iceWallCount    = data.frostcasterIceWallCount;
            randomWallCount = data.frostcasterRandomWallCount;
            iceWallType     = data.frostcasterIceWallType;

            // 计算停驻 Y（世界坐标）
            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                float worldTop    = cam.transform.position.y + cam.orthographicSize;
                float worldBottom = cam.transform.position.y - cam.orthographicSize;
                targetWorldY = Mathf.Lerp(worldTop, worldBottom, stopYPercent);
            }
            else
            {
                targetWorldY = 3f;
            }

            idleTimer = castInterval;
            state     = CasterState.Entering;
            isActive  = true;
        }

        /// <summary>
        /// EnemyBlob.OnDespawn() 时调用，停止所有协程。
        /// </summary>
        public void OnBlobDeactivated()
        {
            isActive = false;
            StopAllCoroutines();
            if (rb != null) rb.velocity = Vector2.zero;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 入场
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void UpdateEntering()
        {
            float dy = transform.position.y - targetWorldY;
            if (dy > SNAP_DIST)
            {
                rb.velocity = Vector2.down * ENTRY_SPEED;
            }
            else
            {
                rb.velocity = Vector2.zero;
                Vector3 pos = transform.position;
                pos.y = targetWorldY;
                transform.position = pos;
                EnterIdle();
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 待机
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void EnterIdle()
        {
            state     = CasterState.Idle;
            idleTimer = castInterval;
            rb.velocity = Vector2.zero;
        }

        private void UpdateIdle()
        {
            rb.velocity = Vector2.zero;
            idleTimer -= Time.fixedDeltaTime;
            if (idleTimer <= 0f)
            {
                StartCoroutine(CastRoutine());
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 施法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator CastRoutine()
        {
            state = CasterState.Casting;
            rb.velocity = Vector2.zero;

            if (EnemyPoolManager.Instance != null)
            {
                int count = randomWallCount
                    ? Random.Range(1, iceWallCount + 1)
                    : iceWallCount;

                for (int i = 0; i < count; i++)
                {
                    Vector3 spawnPos = GetRandomIceWallPosition();
                    EnemyPoolManager.Instance.Spawn(iceWallType, spawnPos);
                    // 多面冰墙时略微错开生成间隔，避免全部重叠
                    if (count > 1) yield return new WaitForSeconds(0.15f);
                }
            }

            yield return new WaitForSeconds(CAST_PAUSE);

            if (!blob.IsDead)
                EnterIdle();
        }

        /// <summary>
        /// 在场地上半部随机取一个合法的冰墙生成位置。
        /// </summary>
        private Vector3 GetRandomIceWallPosition()
        {
            Camera cam = Camera.main;
            if (cam == null) return transform.position + Vector3.up * 2f;

            float halfW = cam.orthographicSize * cam.aspect;
            float halfH = cam.orthographicSize;
            float camY  = cam.transform.position.y;
            float camX  = cam.transform.position.x;

            float minX = camX - halfW + WALL_MARGIN;
            float maxX = camX + halfW - WALL_MARGIN;
            float maxY = camY + halfH - WALL_MARGIN;
            float minY = camY - halfH * WALL_SPAWN_MAX_Y_RATIO + WALL_MARGIN; // 不超过 75% 以下

            float x = Random.Range(minX, maxX);
            float y = Random.Range(minY, maxY);

            return new Vector3(x, y, 0f);
        }
    }
}
