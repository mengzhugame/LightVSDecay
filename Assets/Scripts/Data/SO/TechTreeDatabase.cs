// ============================================================
// TechTreeDatabase.cs
// 文件位置: Assets/Scripts/Data/SO/TechTreeDatabase.cs
// 用途：科技树节点数据库容器（ScriptableObject）
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Data.SO
{
    /// <summary>
    /// 科技树数据库（ScriptableObject）
    /// 统一管理所有节点配置，提供快速查找
    /// 创建路径：Assets → Create → LightVsDecay → TechTree → Database
    /// </summary>
    [CreateAssetMenu(
        fileName = "TechTreeDatabase",
        menuName  = "LightVsDecay/TechTree/Database",
        order     = 21)]
    public class TechTreeDatabase : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("所有节点（拖入所有 TechTreeNodeData SO）")]
        [SerializeField] private List<TechTreeNodeData> allNodes = new List<TechTreeNodeData>();

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private Dictionary<string, TechTreeNodeData> _cache;

        private Dictionary<string, TechTreeNodeData> Cache
        {
            get
            {
                if (_cache == null) BuildCache();
                return _cache;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public IReadOnlyList<TechTreeNodeData> AllNodes => allNodes;

        /// <summary>通过 ID 获取节点数据</summary>
        public TechTreeNodeData GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Cache.TryGetValue(id, out var data);
            return data;
        }

        /// <summary>获取指定分支的所有节点（按列表顺序）</summary>
        public List<TechTreeNodeData> GetByBranch(TechBranch branch)
        {
            var result = new List<TechTreeNodeData>();
            foreach (var n in allNodes)
            {
                if (n != null && n.branch == branch)
                    result.Add(n);
            }
            return result;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内部
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void BuildCache()
        {
            _cache = new Dictionary<string, TechTreeNodeData>();
            foreach (var n in allNodes)
            {
                if (n == null || string.IsNullOrEmpty(n.nodeId)) continue;
                if (_cache.ContainsKey(n.nodeId))
                {
                    GameLogger.LogWarning($"[TechTreeDatabase] 重复节点 ID: {n.nodeId}");
                    continue;
                }
                _cache[n.nodeId] = n;
            }
        }

        private void OnValidate() => _cache = null; // 编辑器修改后重建缓存
    }
}