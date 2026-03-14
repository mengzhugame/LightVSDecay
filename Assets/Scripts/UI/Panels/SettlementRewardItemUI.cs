// ============================================================
// SettlementRewardItemUI.cs
// 文件位置: Assets/Scripts/UI/Panels/SettlementRewardItemUI.cs
//
// 预制体结构（建议）：
//   RewardItem  (RectTransform + LayoutElement: preferredWidth=130, preferredHeight=70)
//     ├── Icon  (Image, LayoutElement: preferredWidth=60, preferredHeight=60)
//     └── Text  (TextMeshProUGUI，对齐 MiddleLeft)
//
// 图标加载说明：
//   装备类型（core/base/proc × 4品质）→ 直接读 EquipmentData.icon（在EquipmentData SO里配置）
//   金币  → 在 Inspector 里手动拖入 goldCoinIcon Sprite
//   图纸  → 在 Inspector 里手动拖入 blueprintIcon Sprite
//
// 每种装备的图标不需要在这里配置，只需要在对应的 EquipmentData SO 里
// 给 icon 字段赋值即可（共12个 EquipmentData SO，各自配置自己的图标）。
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.BattleReward;

namespace LightVsDecay.UI.Panels
{
    public class SettlementRewardItemUI : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("子节点引用")]
        [SerializeField] private Image           iconImage;   // Icon(Image)
        [SerializeField] private TextMeshProUGUI labelText;   // Text(TMP)

        [Header("图标（Inspector 手动赋值）")]
        [Tooltip("图纸图标 Sprite（在Project里拖入）")]
        [SerializeField] private Sprite blueprintIcon;
        [Tooltip("金币图标 Sprite（在 Project 里拖入）")]
        [SerializeField] private Sprite coinIcon;
        [Tooltip("找不到装备图标时的默认图标")]
        [SerializeField] private Sprite defaultEquipmentIcon;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 数据绑定
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void Bind(BattleDropItem drop)
        {
            if (drop == null) return;

            switch (drop.type)
            {
                case DropItemType.Blueprint:
                    BindBlueprint(drop.count);
                    break;

                case DropItemType.Equipment:
                    BindEquipment(drop);
                    break;
                case DropItemType.Coin:
                    BindCoin(drop.count);
                    break;
            }
        }

        // ── 图纸 ─────────────────────────────────────────────

        private void BindBlueprint(int count)
        {
            SetIcon(blueprintIcon);
            SetLabel($"x{count}");
        }

        // ── 装备 ─────────────────────────────────────────────

        private void BindEquipment(BattleDropItem drop)
        {
            EquipmentData data = null;
            if (EquipmentDatabase.Instance != null)
                data = EquipmentDatabase.Instance.GetById(drop.equipmentId);

            Sprite icon = (data?.icon != null) ? data.icon : defaultEquipmentIcon;
            SetIcon(icon);

            // 只显示数量
            SetLabel($"x{drop.count}");
        }
        private void BindCoin(int count)
        {
            SetIcon(coinIcon);
            SetLabel($"x{count}");
        }

        // ── 通用 ──────────────────────────────────────────────

        private void SetIcon(Sprite sprite)
        {
            if (iconImage == null) return;
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        private void SetLabel(string text)
        {
            if (labelText != null)
                labelText.text = text;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 入场动画（缩放弹出，不受 TimeScale 影响）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void PlayAppearAnimation(float delay = 0f)
        {
            StartCoroutine(AppearCoroutine(delay));
        }

        private IEnumerator AppearCoroutine(float delay)
        {
            transform.localScale = Vector3.zero;

            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            float duration = 0.2f;
            float elapsed  = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // EaseOutBack：先弹大一点再收回 1.0
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                float scale = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

                transform.localScale = Vector3.one * Mathf.Max(0f, scale);
                cg.alpha = Mathf.Clamp01(t * 5f);
                yield return null;
            }

            transform.localScale = Vector3.one;
            cg.alpha = 1f;
        }
    }
}