// ============================================================
// CatalystBerserkDebuff.cs
// 文件位置: Assets/Scripts/Logic/Enemy/CatalystBerserkDebuff.cs
// 用途：极寒催化者死亡爆发给周围怪物施加的暴走视觉效果
//   - 在怪物身上显示冰刺覆盖 Sprite
//   - 持续时间结束后自动隐藏
//   - 与 EnemyBlob.ApplyBerserk() 配合使用
// ============================================================

using UnityEngine;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// 催化者暴走 Debuff 视觉组件。
    /// 由 EnemyBlob.Awake() 自动添加；由 EnemyBlob.BerserkCoroutine() 激活。
    /// </summary>
    public class CatalystBerserkDebuff : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("冰刺覆盖配置")]
        [Tooltip("冰刺效果 Sprite（Inspector 中指定，留空则复用怪物本体 Sprite）")]
        [SerializeField] private Sprite iceSpikeSprite;

        [Tooltip("冰刺覆盖颜色（带透明度）")]
        [SerializeField] private Color overlayColor = new Color(0.4f, 0.88f, 1f, 0.82f);

        [Header("脉冲动画")]
        [Tooltip("脉冲频率（次/秒）")]
        [SerializeField] private float pulseSpeed = 5f;

        [Tooltip("脉冲幅度（0 = 不脉冲）")]
        [SerializeField] private float pulseAmplitude = 0.06f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private GameObject overlay;
        private SpriteRenderer overlayRenderer;
        private bool isActive = false;
        private float remainingDuration = 0f;
        private float pulseTimer = 0f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            CreateOverlay();
        }

        private void Update()
        {
            if (!isActive) return;

            remainingDuration -= Time.deltaTime;
            if (remainingDuration <= 0f)
            {
                HideOverlay();
                return;
            }

            // 脉冲动画：冰刺轻微呼吸感
            pulseTimer += Time.deltaTime * pulseSpeed;
            float s = 1f + Mathf.Sin(pulseTimer) * pulseAmplitude;
            overlay.transform.localScale = new Vector3(s, s, 1f);
        }

        private void OnDisable()
        {
            ResetBerserk();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void CreateOverlay()
        {
            overlay = new GameObject("BerserkOverlay");
            overlay.transform.SetParent(transform);
            overlay.transform.localPosition = Vector3.zero;
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = Vector3.one;

            overlayRenderer = overlay.AddComponent<SpriteRenderer>();

            if (iceSpikeSprite != null)
            {
                overlayRenderer.sprite = iceSpikeSprite;
            }
            else
            {
                // 留空时复用父物体上的第一个 SpriteRenderer 的 Sprite 作为占位
                SpriteRenderer parentSR = GetComponentInChildren<SpriteRenderer>();
                if (parentSR != null)
                    overlayRenderer.sprite = parentSR.sprite;
            }

            overlayRenderer.color = overlayColor;
            overlayRenderer.sortingLayerName = "Enemy";
            overlayRenderer.sortingOrder = -1;   // 冰刺在身体下层

            overlay.SetActive(false);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 激活暴走冰刺视觉。由 EnemyBlob.BerserkCoroutine() 调用。
        /// </summary>
        /// <param name="duration">与暴走 Buff 相同的持续时间（秒）</param>
        public void ApplyBerserk(float duration)
        {
            remainingDuration = duration;
            pulseTimer = 0f;
            isActive = true;
            overlay.SetActive(true);
        }

        /// <summary>
        /// 重置状态（对象池回收时由 OnDisable 调用）。
        /// </summary>
        public void ResetBerserk()
        {
            isActive = false;
            remainingDuration = 0f;
            if (overlay != null)
                overlay.SetActive(false);
        }

        private void HideOverlay()
        {
            isActive = false;
            remainingDuration = 0f;
            overlay.SetActive(false);
        }
    }
}
