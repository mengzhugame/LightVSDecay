// ============================================================
// TechTreeNodeUI.cs
// 文件位置: Assets/Scripts/UI/TechTree/TechTreeNodeUI.cs
// 用途：科技树单个节点按钮 UI（简化版）
//
// UI 结构对应（Inspector 中绑定）：
//   TechTreeNode01 (Image 六边形底图, Button, TechTreeNodeUI)
//   ├─ IconImage     (Image - 节点图标)
//   ├─ LockIcon      (Image - 上锁图标，上锁时显示)
//   └─ Text(TMP)     (等级文本 "Lv.3" / "MAX"，有等级时显示)
//
// 状态逻辑（通过 lockIcon 显隐 + alpha 控制，不改颜色）：
//   Locked     → LockIcon 显示，nodeImage & iconImage alpha=0.4
//   Unlockable → LockIcon 隐藏，alpha=1.0（可点击打开升级面板）
//   Active     → LockIcon 隐藏，levelText "Lv.N"，alpha=1.0
//   MaxLevel   → LockIcon 隐藏，levelText "MAX"，alpha=1.0
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.TechTree;

namespace LightVsDecay.UI.TechTree
{
    public class TechTreeNodeUI : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("节点数据（在 Inspector 中直接绑定对应 SO）")]
        [SerializeField] private TechTreeNodeData nodeData;

        [Header("组件引用")]
        [SerializeField] private Button          button;
        [SerializeField] private Image           nodeImage;   // 六边形底图（分支颜色固定在场景中，不修改）
        [SerializeField] private Image           iconImage;   // 节点功能图标
        [SerializeField] private GameObject      lockIcon;    // 上锁图标（整个 GameObject）
        [SerializeField] private TextMeshProUGUI levelText;   // "Lv.3" / "MAX"

        [Header("锁定状态透明度（0~1）")]
        [SerializeField] private float lockedAlpha = 0.4f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private Action<TechTreeNodeData> _onClickCallback;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(OnClicked);
        }

        /// <summary>
        /// 由 TechTreePanel.Initialize() 调用，注入点击回调。
        /// nodeData 已在 Inspector 中绑定，无需外部传入。
        /// </summary>
        public void Initialize(Action<TechTreeNodeData> onClickCallback)
        {
            _onClickCallback = onClickCallback;

            // 初始化图标（仅设一次）
            if (iconImage != null && nodeData != null && nodeData.icon != null)
                iconImage.sprite = nodeData.icon;

            Refresh();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 刷新外观（每次节点状态变化后调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void Refresh()
        {
            if (nodeData == null) return;

            var mgr       = TechTreeManager.Instance;
            int  lv       = mgr != null ? mgr.GetNodeLevel(nodeData.nodeId) : 0;
            bool canUp    = mgr != null && mgr.CanUpgrade(nodeData.nodeId);
            bool isMax    = lv >= nodeData.maxLevel;
            bool buttonDisabled = lv == 0 && !canUp;   // 按钮禁用（前置不足）
            bool showLock       = buttonDisabled;      // ★ 只有真正无法升级

            // 锁图标
            if (lockIcon != null) lockIcon.SetActive(showLock);

            // 透明度（锁定时变暗）
            SetAlpha(nodeImage, buttonDisabled ? lockedAlpha : 1f);
            SetAlpha(iconImage, buttonDisabled ? lockedAlpha : 1f);

            // 等级文本
            if (levelText != null)
            {
                bool show = lv > 0;
                levelText.gameObject.SetActive(show);
                if (show) levelText.text = isMax ? "MAX" : $"Lv.{lv}";
            }

            // 按钮：锁定时禁用（前置未满足），其余可点击
            if (button != null) button.interactable = !buttonDisabled;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public TechTreeNodeData NodeData => nodeData;
        public RectTransform RectTransform => transform as RectTransform;
        public Button Button => button;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内部
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnClicked()
        {
            if (nodeData == null) return;
            _onClickCallback?.Invoke(nodeData);
        }

        private static void SetAlpha(Image img, float alpha)
        {
            if (img == null) return;
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }
}
