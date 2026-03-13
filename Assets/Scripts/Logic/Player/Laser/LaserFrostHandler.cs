// ============================================================
// LaserFrostHandler.cs
// 文件位置: Assets/Scripts/Logic/Player/Laser/LaserFrostHandler.cs
// 用途：激光寒气/冰冻系统 - 从 LaserController 拆分
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using LightVsDecay.Logic.Enemy;
using LightVsDecay.Logic.Boss;
using LightVsDecay.Core.Pool;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 激光寒气/冰冻处理器
    /// 负责：Frost减速、寒气扩散、冰冻累积
    /// </summary>
    [System.Serializable]
    public class LaserFrostHandler
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 常量配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>粒子播放间隔（秒）</summary>
        private const float FROST_VFX_INTERVAL = 0.3f;
        
        /// <summary>寒气扩散半径</summary>
        private const float FROST_SPREAD_RADIUS = 1.5f;
        
        /// <summary>扩散减速比例（扩散效果 = 直接效果 * 此比例）</summary>
        private const float FROST_SPREAD_RATIO = 0.5f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态追踪
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>每个敌人上次播放粒子的时间</summary>
        private Dictionary<int, float> lastFrostVFXTime = new Dictionary<int, float>();
        
        /// <summary>本 Tick 被直接命中的敌人列表（用于寒气扩散）</summary>
        private List<EnemyBlob> directHitEnemiesThisTick = new List<EnemyBlob>();
        
        /// <summary>已受到扩散减速的敌人 ID（本 Tick 去重）</summary>
        private HashSet<int> spreadAffectedEnemyIds = new HashSet<int>();
        
        /// <summary>扩散检测用的 Collider 数组（避免 GC）</summary>
        private Collider2D[] spreadCheckBuffer = new Collider2D[50];
        
        private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>扩散半径</summary>
        public float SpreadRadius => FROST_SPREAD_RADIUS;
        
        /// <summary>扩散减速比例</summary>
        public float SpreadRatio => FROST_SPREAD_RATIO;
        
        /// <summary>直接命中的敌人列表（只读）</summary>
        public IReadOnlyList<EnemyBlob> DirectHitEnemies => directHitEnemiesThisTick;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Tick 管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 每 Tick 开始时清空状态
        /// </summary>
        public void BeginTick()
        {
            directHitEnemiesThisTick.Clear();
            spreadAffectedEnemyIds.Clear();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Frost 效果应用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 对敌人应用 Frost 减速效果（直接命中）
        /// </summary>
        public void ApplyFrostEffect(EnemyBlob enemy, float slowPercent, float duration, float tickRate,
            float freezeThreshold, float freezeDuration)
        {
            if (enemy == null || slowPercent <= 0f) return;
            
            // 应用减速
            enemy.ApplyFrostSlow(slowPercent, duration);
            
            // 记录到直接命中列表（用于寒气扩散）
            if (!directHitEnemiesThisTick.Contains(enemy))
            {
                directHitEnemiesThisTick.Add(enemy);
            }
            
            // 播放 Frost 粒子特效（带间隔限制）
            PlayFrostVFX(enemy);
            
            // Lv.5 冰冻检测（基于累计照射时间）
            if (freezeThreshold > 0f && freezeDuration > 0f)
            {
                // 累加照射时间
                enemy.AddFrostExposureTime(tickRate);
                
                // 检查是否达到冰冻阈值
                if (enemy.GetFrostExposureTime() >= freezeThreshold)
                {
                    enemy.ApplyFrostFreeze(freezeDuration);
                    enemy.ResetFrostExposureTime();
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"[LaserFrostHandler] ❄️ 敌人冰冻! 照射时间达到 {freezeThreshold}s");
                    }
                }
            }
        }
        
        /// <summary>
        /// 对 Boss 应用 Frost 减速效果
        /// </summary>
        public void ApplyBossFrostEffect(BossController bossController, float slowPercent, float duration, 
            float tickRate, bool enableFreeze)
        {
            if (bossController == null || slowPercent <= 0f) return;
            
            // 应用减速
            bossController.ApplyFrostSlow(slowPercent, duration);
            
            // 播放 Frost 粒子特效
            PlayBossFrostVFX(bossController);
            
            // LV5 冰冻累积
            if (enableFreeze)
            {
                bossController.AddFrostExposureTime(tickRate);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 寒气扩散
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 应用寒气扩散效果（每 Tick 结束时调用，需要 FrostSpread 技能才生效）
        /// </summary>
        /// <param name="directSlowPercent">直接减速百分比（来自 Frost 技能）</param>
        /// <param name="directSlowDuration">直接减速持续时间</param>
        /// <param name="spreadRadius">扩散检测半径（来自 FrostSpread 技能配置）</param>
        /// <param name="spreadSlowRatio">扩散减速比例（占直接减速的比例）</param>
        /// <param name="detectionLayer">检测层</param>
        public void ApplyFrostSpread(float directSlowPercent, float directSlowDuration, 
            float spreadRadius, float spreadSlowRatio, LayerMask detectionLayer)
        {
            if (directHitEnemiesThisTick.Count == 0) return;
            if (directSlowPercent <= 0f) return;

            float spreadSlowPercent = directSlowPercent * spreadSlowRatio;

            foreach (var sourceEnemy in directHitEnemiesThisTick)
            {
                if (sourceEnemy == null) continue;

                int hitCount = Physics2D.OverlapCircleNonAlloc(
                    sourceEnemy.transform.position,
                    spreadRadius,
                    spreadCheckBuffer,
                    detectionLayer);

                for (int i = 0; i < hitCount; i++)
                {
                    var col = spreadCheckBuffer[i];
                    if (col == null) continue;

                    var enemy = col.GetComponent<LightVsDecay.Logic.Enemy.EnemyBlob>();
                    if (enemy == null || enemy.IsDead) continue;

                    int enemyId = enemy.GetInstanceID();

                    // 直接命中的敌人已在 ApplyFrostEffect 中处理，跳过
                    if (directHitEnemiesThisTick.Contains(enemy)) continue;

                    // 本 Tick 内去重
                    if (spreadAffectedEnemyIds.Contains(enemyId)) continue;
                    spreadAffectedEnemyIds.Add(enemyId);

                    enemy.ApplyFrostSlow(spreadSlowPercent, directSlowDuration);

                    if (showDebugInfo)
                    {
                        Debug.Log($"[LaserFrostHandler] 寒霜蔓延 → 敌人ID:{enemyId}, 减速:{spreadSlowPercent:P0}");
                    }
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // VFX 播放
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放 Frost 粒子特效（带间隔限制）
        /// </summary>
        private void PlayFrostVFX(EnemyBlob enemy)
        {
            if (VFXPoolManager.Instance == null) return;
            
            int enemyId = enemy.GetInstanceID();
            float currentTime = Time.time;
            
            // 检查间隔
            if (lastFrostVFXTime.TryGetValue(enemyId, out float lastTime))
            {
                if (currentTime - lastTime < FROST_VFX_INTERVAL)
                {
                    return; // 还在冷却中
                }
            }
            
            // 播放粒子
            VFXPoolManager.Instance.PlayFrostHit(enemy.transform.position);
            lastFrostVFXTime[enemyId] = currentTime;
        }
        
        /// <summary>
        /// 播放 Boss Frost 粒子特效（带间隔限制）
        /// </summary>
        private void PlayBossFrostVFX(BossController bossController)
        {
            if (VFXPoolManager.Instance == null) return;
            if (bossController == null) return;
            
            int bossId = bossController.GetInstanceID();
            float currentTime = Time.time;
            
            // 检查间隔
            if (lastFrostVFXTime.TryGetValue(bossId, out float lastTime))
            {
                if (currentTime - lastTime < FROST_VFX_INTERVAL)
                {
                    return; // 还在冷却中
                }
            }
            
            // 播放粒子
            VFXPoolManager.Instance.PlayFrostHit(bossController.transform.position);
            lastFrostVFXTime[bossId] = currentTime;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置调试模式
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            showDebugInfo = enabled;
        }
        
        /// <summary>
        /// 清理 VFX 时间缓存（场景切换时调用）
        /// </summary>
        public void ClearVFXCache()
        {
            lastFrostVFXTime.Clear();
        }
    }
}
