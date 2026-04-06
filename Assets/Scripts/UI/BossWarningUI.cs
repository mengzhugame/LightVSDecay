// ============================================================
// BossWarningUI.cs
// 文件位置: Assets/Scripts/UI/BossWarningUI.cs
// 用途：BOSS 来袭警告横幅 UI
//   - 第10波（BOSS波）开始时由 WaveManager 触发
//   - 激活 JingGaoTip 节点，播放 Animator 动画和警告音效
//   - 动画播放完毕后自动隐藏节点
// 挂载位置：JingGaoTip 根节点
// 依赖：Animator 组件挂在同一 GameObject 上
// ============================================================

using System.Collections;
using UnityEngine;
using LightVsDecay.Audio;
using LightVsDecay.Core;

namespace LightVsDecay.UI
{
    /// <summary>
    /// BOSS 来袭警告横幅，由 WaveManager 通过 ShowAndWait() 协程驱动。
    /// </summary>
    public class BossWarningUI : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Tooltip("Animator 中播放的动画 State 名称（需与 Animator Controller 内一致）")]
        [SerializeField] private string showStateName = "Show";

        [Tooltip("若 Animator 未设置或获取时长失败时的兜底等待时长（秒）")]
        [SerializeField] private float fallbackDuration = 3.5f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private Animator warningAnimator;

        private void Awake()
        {
            warningAnimator = GetComponent<Animator>();
            // 初始隐藏状态由场景直接设置，Awake 只做组件缓存
        }

        /// <summary>
        /// 由 WaveManager 在 BOSS 波开始时调用。
        /// 协程：激活 UI → 播动画+音效 → 等动画结束 → 隐藏 UI。
        /// </summary>
        public IEnumerator ShowAndWait()
        {
            gameObject.SetActive(true);

            // 播放 BOSS 来袭警告音效
            AudioManager.Instance?.PlayBossWarning();

            if (warningAnimator != null)
            {
                warningAnimator.Play(showStateName, 0, 0f);

                // 等一帧让 Animator 更新到目标 State
                yield return null;

                // 等待动画播放完毕（normalizedTime 到达 1）
                while (warningAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                    yield return null;
            }
            else
            {
                // 没有 Animator 时，按兜底时长等待
                GameLogger.LogWarning("[BossWarningUI] 未找到 Animator 组件，使用兜底等待时长。");
                yield return new WaitForSeconds(fallbackDuration);
            }

            gameObject.SetActive(false);
        }
    }
}
