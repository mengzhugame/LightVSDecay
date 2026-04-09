// ============================================================
// IceWallTracker.cs
// 文件位置: Assets/Scripts/Logic/Enemy/IceWallTracker.cs
// 用途：全局跟踪场景中冰墙数量，防止堆积超过上限
// ============================================================

using System.Collections.Generic;
using LightVsDecay.Core.Pool;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// 冰墙全局上限管理器（静态工具类）
    /// 由 EnemyBlob.OnSpawn / OnDespawn 自动注册/注销
    /// 由 FrostcasterAI.SpawnIceWalls 在生成前检查上限
    /// </summary>
    public static class IceWallTracker
    {
        public const int MaxCount = 4;

        private static readonly List<EnemyBlob> activeWalls = new List<EnemyBlob>(8);

        /// <summary>当前场景中存活的冰墙数量</summary>
        public static int ActiveCount => activeWalls.Count;

        /// <summary>注册一面新冰墙（由 EnemyBlob.OnSpawn 调用）</summary>
        public static void Register(EnemyBlob wall)
        {
            if (wall != null && !activeWalls.Contains(wall))
                activeWalls.Add(wall);
        }

        /// <summary>注销一面冰墙（由 EnemyBlob.OnDespawn 调用）</summary>
        public static void Unregister(EnemyBlob wall)
        {
            activeWalls.Remove(wall);
        }

        /// <summary>
        /// 强制回收最旧的冰墙（当新冰墙要生成而数量已达上限时调用）
        /// </summary>
        public static void DespawnOldest()
        {
            // 清理列表中已经失效的引用
            for (int i = activeWalls.Count - 1; i >= 0; i--)
            {
                if (activeWalls[i] == null)
                    activeWalls.RemoveAt(i);
            }

            if (activeWalls.Count > 0 && EnemyPoolManager.Instance != null)
            {
                EnemyBlob oldest = activeWalls[0];
                if (oldest != null)
                    EnemyPoolManager.Instance.Despawn(oldest);
                // Unregister 会在 EnemyBlob.OnDespawn 中自动调用
            }
        }

        /// <summary>场景重置时清空（WaveManager 关卡结束时调用）</summary>
        public static void Clear()
        {
            activeWalls.Clear();
        }
    }
}
