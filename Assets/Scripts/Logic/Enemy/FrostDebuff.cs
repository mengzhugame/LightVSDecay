// ============================================================
// FrostDebuff.cs
// 文件位置: Assets/Scripts/Logic/Enemy/FrostDebuff.cs
// 用途：冰冻/减速 Debuff 组件 - 管理减速效果和冰冻视觉
// ============================================================

using UnityEngine;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// 冰冻 Debuff 组件
    /// 职责：
    /// - 管理减速/完全冰冻状态
    /// - 控制冰冻视觉效果（渐变 + 呼吸动画）
    /// - 自动挂载到敌人身上
    /// </summary>
    public class FrostDebuff : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("视觉效果配置")]
        [Tooltip("冰冻效果 Sprite（半透明蓝色）")]
        [SerializeField] private Sprite frostSprite;
        
        [Tooltip("冰冻颜色")]
        [SerializeField] private Color frostColor = new Color(0.3f, 0.7f, 1f, 0.6f);
        
        [Tooltip("渐变时间")]
        [SerializeField] private float fadeDuration = 0.2f;
        
        [Tooltip("呼吸动画 - 最小缩放")]
        [SerializeField] private float breathScaleMin = 0.95f;
        
        [Tooltip("呼吸动画 - 最大缩放")]
        [SerializeField] private float breathScaleMax = 1.05f;
        
        [Tooltip("呼吸动画 - 周期（秒）")]
        [SerializeField] private float breathCycleDuration = 1.5f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private bool isSlowed = false;
        private bool isFrozen = false;
        private float slowPercent = 0f;
        private float remainingDuration = 0f;
        private float frozenDuration = 0f;
        
        // 视觉组件
        private GameObject frostVisual;
        private SpriteRenderer frostRenderer;
        private float currentAlpha = 0f;
        private float targetAlpha = 0f;
        private float breathTimer = 0f;
        
        // 缓存
        private EnemyBlob enemyBlob;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>是否处于减速状态</summary>
        public bool IsSlowed => isSlowed;
        
        /// <summary>是否完全冰冻</summary>
        public bool IsFrozen => isFrozen;
        
        /// <summary>当前减速百分比（0~1）</summary>
        public float SlowPercent => slowPercent;
        
        /// <summary>当前速度倍率（1 = 正常，0.5 = 减速50%，0 = 冰冻）</summary>
        public float SpeedMultiplier
        {
            get
            {
                if (isFrozen) return 0f;
                if (isSlowed) return 1f - slowPercent;
                return 1f;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            enemyBlob = GetComponent<EnemyBlob>();
            CreateFrostVisual();
        }
        
        private void Update()
        {
            UpdateDebuffTimer();
            UpdateVisualAlpha();
            UpdateBreathAnimation();
        }
        
        private void OnDisable()
        {
            // 重置状态（对象池回收时）
            ResetDebuff();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 创建冰冻视觉效果（子物体）
        /// </summary>
        private void CreateFrostVisual()
        {
            // 创建子物体
            frostVisual = new GameObject("FrostVisual");
            frostVisual.transform.SetParent(transform);
            frostVisual.transform.localPosition = Vector3.zero;
            frostVisual.transform.localRotation = Quaternion.identity;
            frostVisual.transform.localScale = Vector3.one;
            
            // 添加 SpriteRenderer
            frostRenderer = frostVisual.AddComponent<SpriteRenderer>();
            
            // 设置 Sprite（如果没有配置，使用默认圆形）
            if (frostSprite != null)
            {
                frostRenderer.sprite = frostSprite;
            }
            else
            {
                // 尝试从父物体获取 Sprite 作为基础
                SpriteRenderer parentSR = GetComponentInChildren<SpriteRenderer>();
                if (parentSR != null && parentSR.sprite != null)
                {
                    frostRenderer.sprite = parentSR.sprite;
                }
            }
            
            // 设置颜色（初始透明）
            Color c = frostColor;
            c.a = 0f;
            frostRenderer.color = c;
            
            // 设置排序层（在怪物之上）
            frostRenderer.sortingLayerName = "Enemy";
            frostRenderer.sortingOrder = 10;
            
            // 初始隐藏
            frostVisual.SetActive(false);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 应用减速效果
        /// </summary>
        /// <param name="percent">减速百分比（0.2 = 减速20%）</param>
        /// <param name="duration">持续时间（秒）</param>
        public void ApplySlow(float percent, float duration)
        {
            // 如果已经冰冻，不覆盖
            if (isFrozen) return;
            
            // 刷新减速（取更强的效果）
            if (percent >= slowPercent || duration > remainingDuration)
            {
                slowPercent = Mathf.Max(slowPercent, percent);
                remainingDuration = Mathf.Max(remainingDuration, duration);
            }
            
            if (!isSlowed)
            {
                isSlowed = true;
                targetAlpha = frostColor.a;
                frostVisual.SetActive(true);
                
                // 通知 EnemyBlob 更新速度
                NotifySpeedChange();
            }
        }
        
        /// <summary>
        /// 应用完全冰冻效果
        /// </summary>
        /// <param name="duration">冰冻持续时间（秒）</param>
        public void ApplyFreeze(float duration)
        {
            isFrozen = true;
            frozenDuration = duration;
            
            // 冰冻时使用更高的透明度
            targetAlpha = Mathf.Min(frostColor.a + 0.2f, 0.9f);
            frostVisual.SetActive(true);
            
            // 通知 EnemyBlob 完全停止
            NotifySpeedChange();
        }
        
        /// <summary>
        /// 重置 Debuff 状态（对象池回收时调用）
        /// </summary>
        public void ResetDebuff()
        {
            isSlowed = false;
            isFrozen = false;
            slowPercent = 0f;
            remainingDuration = 0f;
            frozenDuration = 0f;
            
            currentAlpha = 0f;
            targetAlpha = 0f;
            breathTimer = 0f;
            
            if (frostRenderer != null)
            {
                Color c = frostRenderer.color;
                c.a = 0f;
                frostRenderer.color = c;
            }
            
            if (frostVisual != null)
            {
                frostVisual.SetActive(false);
                frostVisual.transform.localScale = Vector3.one;
            }
        }
        
        /// <summary>
        /// 设置冰冻 Sprite（可从外部配置）
        /// </summary>
        public void SetFrostSprite(Sprite sprite)
        {
            frostSprite = sprite;
            if (frostRenderer != null)
            {
                frostRenderer.sprite = sprite;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内部更新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 更新 Debuff 计时器
        /// </summary>
        private void UpdateDebuffTimer()
        {
            // 冰冻计时
            if (isFrozen)
            {
                frozenDuration -= Time.deltaTime;
                if (frozenDuration <= 0f)
                {
                    isFrozen = false;
                    
                    // 冰冻结束后检查是否还有减速
                    if (!isSlowed || remainingDuration <= 0f)
                    {
                        EndDebuff();
                    }
                    else
                    {
                        targetAlpha = frostColor.a;
                        NotifySpeedChange();
                    }
                }
                return; // 冰冻期间不更新减速计时
            }
            
            // 减速计时
            if (isSlowed)
            {
                remainingDuration -= Time.deltaTime;
                if (remainingDuration <= 0f)
                {
                    EndDebuff();
                }
            }
        }
        
        /// <summary>
        /// 结束 Debuff
        /// </summary>
        private void EndDebuff()
        {
            isSlowed = false;
            isFrozen = false;
            slowPercent = 0f;
            remainingDuration = 0f;
            frozenDuration = 0f;
            
            // 渐隐视觉效果
            targetAlpha = 0f;
            
            // 通知 EnemyBlob 恢复速度
            NotifySpeedChange();
        }
        
        /// <summary>
        /// 更新视觉透明度（渐变）
        /// </summary>
        private void UpdateVisualAlpha()
        {
            if (frostRenderer == null) return;
            
            // 平滑插值
            float fadeSpeed = 1f / fadeDuration;
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
            
            // 应用颜色
            Color c = frostRenderer.color;
            c.a = currentAlpha;
            frostRenderer.color = c;
            
            // 完全透明时隐藏
            if (currentAlpha <= 0.01f && targetAlpha <= 0f)
            {
                frostVisual.SetActive(false);
            }
        }
        
        /// <summary>
        /// 更新呼吸动画
        /// </summary>
        private void UpdateBreathAnimation()
        {
            if (!frostVisual.activeSelf) return;
            if (frostVisual == null) return;
            
            breathTimer += Time.deltaTime;
            
            // 正弦波呼吸
            float t = (Mathf.Sin(breathTimer * 2f * Mathf.PI / breathCycleDuration) + 1f) * 0.5f;
            float scale = Mathf.Lerp(breathScaleMin, breathScaleMax, t);
            
            frostVisual.transform.localScale = Vector3.one * scale;
        }
        
        /// <summary>
        /// 通知 EnemyBlob 速度变化
        /// </summary>
        private void NotifySpeedChange()
        {
            if (enemyBlob != null)
            {
                enemyBlob.OnFrostStateChanged(SpeedMultiplier, isFrozen);
            }
        }
    }
}