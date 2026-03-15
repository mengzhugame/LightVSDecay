// ============================================================
// TechTreePanel.cs
// 文件位置: Assets/Scripts/UI/TechTree/TechTreePanel.cs
// 用途：科技树主面板控制器（简化版）
//
// UI 层级（示例）：
//   TechTreePanel
//   ├─ Background
//   ├─ FirepowerBranches
//   │   ├─ Line02 (Image + TechTreeLineUI)
//   │   ├─ Line03 (Image + TechTreeLineUI)
//   │   ├─ TechTreeNode01 (Image + Button + TechTreeNodeUI)  ← SO 在 NodeUI 上绑定
//   │   └─ ...
//   ├─ SurvivalBranches    (同上)
//   ├─ ResourcesBranches   (同上)
//   ├─ TacticsBranches     (同上)
//   └─ DetailPanel         (TechTreeNodeDetailPanel，默认 inactive)
//
// 关键设计：
//   ① 每个 TechTreeNodeUI 在 Inspector 中直接绑定自己的 nodeData SO，
//      TechTreePanel 通过 GetComponentsInChildren 自动查找，无需手动维护列表。
//   ② 每条 TechTreeLineUI 同理，自动查找并刷新。
//   ③ 没有金币条（金币在 TopArea/GoldCoinBar 显示）。
//   ④ 没有关闭按钮（返回按钮逻辑由父级 MainSceneUIManager 管理）。
// ============================================================

using UnityEngine;
using LightVsDecay.Logic.TechTree;
using LightVsDecay.Core;

namespace LightVsDecay.UI.TechTree
{
    public class TechTreePanel : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("升级详情弹窗（TechTreeNodeDetailPanel）")]
        [SerializeField] private TechTreeNodeDetailPanel detailPanel;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private TechTreeNodeUI[] _allNodeUIs;
        private TechTreeLineUI[] _allLineUIs;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            // 自动查找所有节点 UI 和线段 UI（包含非激活的子节点）
            _allNodeUIs = GetComponentsInChildren<TechTreeNodeUI>(true);
            _allLineUIs = GetComponentsInChildren<TechTreeLineUI>(true);

            // 隐藏详情弹窗
            if (detailPanel != null)
                detailPanel.gameObject.SetActive(false);

            // 初始化所有节点（注入点击回调）
            foreach (var node in _allNodeUIs)
                node.Initialize(OnNodeClicked);

            if (showDebugInfo)
                GameLogger.Log($"[TechTreePanel] Awake: 找到 {_allNodeUIs.Length} 个节点, {_allLineUIs.Length} 条线段");
        }

        private void OnEnable()
        {
            // 订阅升级事件
            TechTreeManager.OnNodeUpgraded += OnNodeUpgraded;
            // 面板激活时刷新所有状态
            RefreshAll();
        }

        private void OnDisable()
        {
            TechTreeManager.OnNodeUpgraded -= OnNodeUpgraded;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 刷新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>刷新所有节点和线段外观（公开，供 DetailPanel 升级后调用）</summary>
        public void RefreshAll()
        {
            if (_allNodeUIs != null)
                foreach (var n in _allNodeUIs) n?.Refresh();

            if (_allLineUIs != null)
                foreach (var l in _allLineUIs) l?.Refresh();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnNodeClicked(Data.SO.TechTreeNodeData data)
        {
            if (detailPanel == null)
            {
                GameLogger.LogWarning("[TechTreePanel] detailPanel 未设置，无法弹出升级窗口！");
                return;
            }

            detailPanel.Show(data, this);

            if (showDebugInfo)
                GameLogger.Log($"[TechTreePanel] 点击节点: {data.displayName}");
        }

        private void OnNodeUpgraded(string nodeId, int newLevel)
        {
            // 单个节点升级后刷新所有（连接线状态可能联动变化）
            RefreshAll();

            if (showDebugInfo)
                GameLogger.Log($"[TechTreePanel] 节点升级: {nodeId} → Lv.{newLevel}");
        }

        /// <summary>DetailPanel 关闭后回调</summary>
        public void OnDetailPanelClosed()
        {
            RefreshAll();
        }
    }
}