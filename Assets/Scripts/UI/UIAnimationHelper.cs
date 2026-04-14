// ============================================================
// UIAnimationHelper.cs
// 文件位置: Assets/Scripts/UI/UIAnimationHelper.cs
//
// 用途：UI 通用动画工具（Scale Punch Q弹缩放 + Rolling Number 数字滚动）
//
// 使用方式（均为协程，需要 MonoBehaviour 调用 StartCoroutine）：
//
//   // Q弹缩放（Scale Punch）
//   StartCoroutine(UIAnimationHelper.PlayScalePunch(rectTransform));
//   StartCoroutine(UIAnimationHelper.PlayScalePunch(rectTransform, punchScale: 1.3f, duration: 0.25f));
//
//   // 数字滚动（Rolling Number）
//   StartCoroutine(UIAnimationHelper.RollInt(text, from: 100, to: 150, duration: 0.5f, prefix: "攻击力："));
//   StartCoroutine(UIAnimationHelper.RollFloat(text, from: 10f, to: 15.5f, prefix: "暴击率：", suffix: "%"));
//
//   // Q弹 + 滚动组合（装备属性更新标准用法）
//   StartCoroutine(UIAnimationHelper.PunchThenRollInt(text, from, to, label: "攻击力："));
// ============================================================

using System.Collections;
using TMPro;
using UnityEngine;

namespace LightVsDecay.UI
{
    public static class UIAnimationHelper
    {
        // ─── Scale Punch（Q弹缩放） ───────────────────────────────────────

        /// <summary>
        /// Q弹缩放效果：Scale 1.0 → punchScale → 1.0
        /// </summary>
        /// <param name="rect">目标 RectTransform</param>
        /// <param name="punchScale">放大倍率（1.2 = 放大20%后弹回）</param>
        /// <param name="duration">总时长（秒），两段各占一半</param>
        /// <param name="useUnscaledTime">true = 不受 TimeScale 影响（推荐用于 UI）</param>
        public static IEnumerator PlayScalePunch(
            RectTransform rect,
            float punchScale = 1.2f,
            float duration = 0.2f,
            bool useUnscaledTime = true)
        {
            if (rect == null) yield break;

            Vector3 baseScale = rect.localScale;
            float half = Mathf.Max(0.01f, duration * 0.5f);
            float elapsed = 0f;

            // Phase 1: 放大
            while (elapsed < half)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                rect.localScale = Vector3.Lerp(baseScale, baseScale * punchScale, t);
                yield return null;
            }

            // Phase 2: 弹回
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                rect.localScale = Vector3.Lerp(baseScale * punchScale, baseScale, t);
                yield return null;
            }

            rect.localScale = baseScale;
        }

        // ─── Rolling Number（数字滚动） ───────────────────────────────────

        /// <summary>
        /// 整数滚动：文字在 duration 秒内从 from 滚动到 to
        /// </summary>
        public static IEnumerator RollInt(
            TextMeshProUGUI text,
            int from,
            int to,
            float duration = 0.5f,
            string prefix = "",
            string suffix = "")
        {
            if (text == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                int value = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                text.text = $"{prefix}{value}{suffix}";
                yield return null;
            }

            text.text = $"{prefix}{to}{suffix}";
        }

        /// <summary>
        /// 浮点数滚动：文字在 duration 秒内从 from 滚动到 to
        /// </summary>
        public static IEnumerator RollFloat(
            TextMeshProUGUI text,
            float from,
            float to,
            float duration = 0.5f,
            string format = "F1",
            string prefix = "",
            string suffix = "")
        {
            if (text == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float value = Mathf.Lerp(from, to, t);
                text.text = $"{prefix}{value.ToString(format)}{suffix}";
                yield return null;
            }

            text.text = $"{prefix}{to.ToString(format)}{suffix}";
        }

        // ─── 组合效果 ─────────────────────────────────────────────────────

        /// <summary>
        /// 先 Q弹缩放 再 整数滚动（装备属性更新标准用法）
        /// </summary>
        public static IEnumerator PunchThenRollInt(
            TextMeshProUGUI text,
            int from,
            int to,
            float punchScale = 1.2f,
            float punchDuration = 0.2f,
            float rollDuration = 0.5f,
            string prefix = "",
            string suffix = "")
        {
            if (text == null) yield break;
            yield return PlayScalePunch(text.transform as RectTransform, punchScale, punchDuration);
            yield return RollInt(text, from, to, rollDuration, prefix, suffix);
        }

        /// <summary>
        /// 先 Q弹缩放 再 浮点数滚动（装备属性更新标准用法）
        /// </summary>
        public static IEnumerator PunchThenRollFloat(
            TextMeshProUGUI text,
            float from,
            float to,
            float punchScale = 1.2f,
            float punchDuration = 0.2f,
            float rollDuration = 0.5f,
            string format = "F1",
            string prefix = "",
            string suffix = "")
        {
            if (text == null) yield break;
            yield return PlayScalePunch(text.transform as RectTransform, punchScale, punchDuration);
            yield return RollFloat(text, from, to, rollDuration, format, prefix, suffix);
        }
    }
}
