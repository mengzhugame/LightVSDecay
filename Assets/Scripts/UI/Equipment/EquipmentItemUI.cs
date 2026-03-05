// ============================================================
// EquipmentItemUI.cs
// 文件位置: Assets/Scripts/UI/Equipment/EquipmentItemUI.cs
// 用途：背包格子 UI（堆叠版）
//
// 特性：
//   - 无等级显示（物品本身无等级）
//   - 无徽章
//   - 显示堆叠数量 ×N（N=1时可选择隐藏）
//   - 图标颜色已在 Sprite 图片中体现，代码不做着色
//   - 点击 → 打开 ItemInfoPanel
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightVsDecay.Data.Runtime;
using LightVsDecay.Data.SO;

namespace LightVsDecay.UI.Equipment
{
    /// <summary>
    /// 背包格子 UI（挂在背包 Grid 的子预制体上）
    /// </summary>
    public class EquipmentItemUI : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("子节点引用")]
        [Tooltip("物品图标 Image（图片本身已含品质颜色，代码不再着色）")]
        [SerializeField] private Image iconImage;

        [Tooltip("数量文字 TextMeshProUGUI，显示 ×3 等")]
        [SerializeField] private TextMeshProUGUI countText;

        [Tooltip("选中高亮框（可为 null）")]
        [SerializeField] private GameObject selectionHighlight;

        [Header("配置")]
        [Tooltip("数量为1时是否隐藏数量文字")]
        [SerializeField] private bool hideCountWhenOne = true;

        [Header("按钮")]
        [SerializeField] private Button itemButton;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private InventoryStack                 _stack;
        private Action<InventoryStack>         _onClickCallback;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公开方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>初始化格子</summary>
        public void Setup(
            InventoryStack         stack,
            EquipmentData          data,         // SO 数据（可为 null 做容错）
            bool                   isSelected,
            Action<InventoryStack> onClick)
        {
            _stack           = stack;
            _onClickCallback = onClick;

            // 图标
            if (iconImage != null)
            {
                bool hasIcon  = data?.icon != null;
                iconImage.enabled = hasIcon;
                if (hasIcon) iconImage.sprite = data.icon;
            }

            // 数量
            if (countText != null)
            {
                bool show = !hideCountWhenOne || stack.count > 1;
                countText.gameObject.SetActive(show);
                countText.text = $"×{stack.count}";
            }

            // 选中高亮
            if (selectionHighlight != null)
                selectionHighlight.SetActive(isSelected);

            // 按钮
            if (itemButton != null)
            {
                itemButton.onClick.RemoveAllListeners();
                itemButton.onClick.AddListener(OnClicked);
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnClicked() => _onClickCallback?.Invoke(_stack);
    }
}