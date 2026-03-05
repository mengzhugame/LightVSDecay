// ============================================================
// TowerAppearanceController.cs
// 文件位置: Assets/Scripts/Logic/Player/TowerAppearanceController.cs
// 用途：光棱塔外观控制器（简化版）
//
// 设计：
//   - 塔只有1张整体图，不分层
//   - 共5档 Sprite：default(灰) + 绿/蓝/紫/橙
//   - 整体塔档次 = 三件装备中最低品质（方案A短板驱动）
//   - 完全在 Canvas UI 下运行，使用 Image 组件
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Equipment;

namespace LightVsDecay.Logic.Player
{
    public class TowerAppearanceController : MonoBehaviour
    {
        [Header("塔图片组件（Canvas UI Image）")]
        [SerializeField] private Image towerImage;

        [Header("5档塔 Sprites")]
        [SerializeField] private Sprite defaultSprite;
        [SerializeField] private Sprite uncommonSprite;
        [SerializeField] private Sprite rareSprite;
        [SerializeField] private Sprite epicSprite;
        [SerializeField] private Sprite legendarySprite;

        [Header("外发光颜色（可选）")]
        [SerializeField] private Image glowImage;
        [SerializeField] private Color[] glowColors = new Color[]
        {
            new Color(0.6f, 0.6f, 0.6f, 0.3f),
            new Color(0.3f, 0.9f, 0.3f, 0.6f),
            new Color(0.2f, 0.5f, 1.0f, 0.6f),
            new Color(0.7f, 0.2f, 1.0f, 0.6f),
            new Color(1.0f, 0.6f, 0.1f, 0.7f),
        };

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] [Range(-1, 4)] private int debugPreviewTier = -1;

        private void OnEnable()
        {
            EquipmentManager.OnEquipmentSlotChanged += OnSlotChanged;
        }

        private void OnDisable()
        {
            EquipmentManager.OnEquipmentSlotChanged -= OnSlotChanged;
        }

        private void OnSlotChanged(EquipmentSlotType _) => RefreshAppearance();

        private void Start()
        {
            if (towerImage == null)
                towerImage = GetComponent<Image>();
            RefreshAppearance();
        }

        public void RefreshAppearance()
        {
            int tier = debugPreviewTier >= 0 ? debugPreviewTier : GetCurrentTier();
            ApplyTier(tier);
            if (showDebugInfo)
                Debug.Log($"[TowerAppearance] 当前档次: {tier}");
        }

        private int GetCurrentTier()
        {
            var mgr = EquipmentManager.Instance;
            if (mgr == null) return 0;

            // 使用 GetSlot 替代旧的 GetEquipped
            var core  = mgr.GetSlot(EquipmentSlotType.PrismCore);
            var base_ = mgr.GetSlot(EquipmentSlotType.TowerBase);
            var proc  = mgr.GetSlot(EquipmentSlotType.Processor);

            // 任意槽位为空 → 灰色 default
            if (core.IsEmpty || base_.IsEmpty || proc.IsEmpty) return 0;

            // 短板驱动：最低品质决定整体档次
            int lowest = Mathf.Min(
                Mathf.Min((int)core.rarity, (int)base_.rarity),
                (int)proc.rarity);

            return Mathf.Clamp(lowest, 0, 4);
        }

        private void ApplyTier(int tier)
        {
            if (towerImage != null)
            {
                Sprite s = tier switch
                {
                    0 => defaultSprite,
                    1 => uncommonSprite  ?? defaultSprite,
                    2 => rareSprite      ?? uncommonSprite ?? defaultSprite,
                    3 => epicSprite      ?? rareSprite     ?? defaultSprite,
                    4 => legendarySprite ?? epicSprite     ?? defaultSprite,
                    _ => defaultSprite,
                };
                if (s != null) towerImage.sprite = s;
            }

            if (glowImage != null && glowColors != null)
            {
                int idx = Mathf.Clamp(tier, 0, glowColors.Length - 1);
                glowImage.color = glowColors[idx];
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && towerImage != null && debugPreviewTier >= 0)
                ApplyTier(debugPreviewTier);
        }
#endif
    }
}