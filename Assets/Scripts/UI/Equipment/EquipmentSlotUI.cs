// ============================================================
// EquipmentSlotUI.cs
// 文件位置: Assets/Scripts/UI/Equipment/EquipmentSlotUI.cs
// 用途：装备槽位 UI（挂在 shuijing_button / xinpian_button / dizuo_button 上）
//
// 层级结构（每个按钮相同）：
//   shuijing_button  [Button + EquipmentSlotUI]
//   ├─ Image          ← 物品图标（iconImage，无装备时隐藏）
//   └─ Text (TMP)     ← "LV.5"（levelText，无装备时隐藏）
//
// 点击行为：
//   有装备 → 打开 UpgradePanel
//   无装备 → 不响应（或可自定义显示提示）
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightVsDecay.Data.SO;
using LightVsDecay.Data.Runtime;

namespace LightVsDecay.UI.Equipment
{
    /// <summary>
    /// 装备槽 UI 组件
    /// 挂在对应槽位的 Button GameObject 上
    /// </summary>
    public class EquipmentSlotUI : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("子节点引用（按层级拖入）")]
        [Tooltip("子节点 Image：显示已装备的物品图标")]
        [SerializeField] private Image iconImage;

        [Tooltip("子节点 Text(TMP)：显示装备等级，如 LV.5")]
        [SerializeField] private TextMeshProUGUI levelText;

        [Header("空槽提示（可选）")]
        [Tooltip("无装备时显示的占位提示对象（可为null）")]
        [SerializeField] private GameObject emptyHint;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private EquipmentSlotType           _slotType;
        private Action<EquipmentSlotType>   _onClickCallback;
        private Button                      _button;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公开方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 初始化槽位显示
        /// 由 EquipmentPanel 每次刷新时调用
        /// </summary>
        public void Setup(
            EquipmentSlotType     slotType,
            EquippedSlotData      slotData,         // null 或 IsEmpty=true 时为空槽
            EquipmentData         data,             // 对应的 SO（可为 null）
            Action<EquipmentSlotType> onSlotClicked)
        {
            _slotType        = slotType;
            _onClickCallback = onSlotClicked;

            // 获取按钮（组件在自身 GameObject 上）
            if (_button == null)
            {
                _button = GetComponent<Button>();
                if (_button != null)
                {
                    _button.onClick.RemoveAllListeners();
                    _button.onClick.AddListener(OnClicked);
                }
            }

            bool isEmpty = (slotData == null || slotData.IsEmpty);

            // 空槽提示
            if (emptyHint != null)
                emptyHint.SetActive(isEmpty);

            if (isEmpty)
            {
                ShowEmpty();
                if (_button != null) _button.interactable = false;
            }
            else
            {
                ShowEquipped(slotData, data);
                if (_button != null) _button.interactable = true;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void ShowEmpty()
        {
            if (iconImage != null)
            {
                iconImage.sprite  = null;
                iconImage.enabled = false;
            }
            if (levelText != null)
                levelText.gameObject.SetActive(false);
        }

        private void ShowEquipped(EquippedSlotData slotData, EquipmentData data)
        {
            // 图标
            if (iconImage != null)
            {
                iconImage.enabled = true;
                if (data?.icon != null)
                    iconImage.sprite = data.icon;
            }

            // 等级
            if (levelText != null)
            {
                levelText.gameObject.SetActive(true);
                levelText.text = $"LV.{slotData.upgradeLevel}";
            }
        }

        private void OnClicked()
        {
            _onClickCallback?.Invoke(_slotType);
        }
    }
}