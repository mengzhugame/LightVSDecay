// ============================================================
// FrostDebuff.cs
// 文件位置: Assets/Scripts/Logic/Enemy/FrostDebuff.cs
// 用途：冰冻/减速 Debuff 组件 - 管理减速效果和冰冻视觉
// 【重构】新增颜色染色系统 + 寒气扩散支持
// ============================================================

using UnityEngine;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// 冰冻 Debuff 组件
    /// 职责：
    /// - 管理减速/完全冰冻状态
    /// - 控制视觉效果：
    ///   - LV1-4：颜色染色（淡蓝色）
    ///   - LV5：颜色染色（深蓝色）+ 冰冻覆盖 Sprite
    /// </summary>
    public class FrostDebuff : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("颜色染色配置")]
        [Tooltip("减速时的染色颜色（淡蓝色）")]
        [SerializeField] private Color slowTintColor = new Color(0.1f, 0.65f, 1f, 1f);
        
        [Tooltip("冰冻时的染色颜色（深蓝色）")]
        [SerializeField] private Color frozenTintColor = new Color(0.1f, 0.65f, 1f, 1f);
        
        [Tooltip("颜色混合比例（0-1）")]
        [SerializeField] private float tintBlendRatio = 1f;
        
        [Tooltip("颜色渐变速度")]
        [SerializeField] private float colorFadeSpeed = 5f;
        
        [Header("冰冻覆盖配置（仅 LV5）")]
        [Tooltip("冰冻效果 Sprite（半透明蓝色覆盖）")]
        [SerializeField] private Sprite frostSprite;
        
        [Tooltip("冰冻覆盖颜色")]
        [SerializeField] private Color frostOverlayColor = new Color(0.3f, 0.7f, 1f, 0.6f);
        
        [Tooltip("覆盖渐变时间")]
        [SerializeField] private float overlayFadeDuration = 0.2f;
        
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
        
        // 颜色染色相关
        private SpriteRenderer[] targetRenderers;
        private Color[] originalColors;
        private Color currentTintTarget = Color.white;
        private bool hasInitializedRenderers = false;
        
        // 冰冻覆盖相关（仅 LV5）
        private GameObject frostOverlay;
        private SpriteRenderer frostOverlayRenderer;
        private float currentOverlayAlpha = 0f;
        private float targetOverlayAlpha = 0f;
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
            CreateFrostOverlay();
        }
        
        private void Start()
        {
            // 延迟初始化渲染器（确保 EnemyBlob 已完成初始化）
            InitializeTargetRenderers();
        }
        
        private void Update()
        {
            UpdateDebuffTimer();
            UpdateColorTint();
            UpdateOverlayAlpha();
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
        /// 初始化目标渲染器（从 EnemyBlob 获取）
        /// </summary>
        private void InitializeTargetRenderers()
        {
            if (hasInitializedRenderers) return;
            
            if (enemyBlob != null)
            {
                targetRenderers = enemyBlob.GetDecorationRenderers();
            }
            
            if (targetRenderers != null && targetRenderers.Length > 0)
            {
                // 保存原始颜色
                originalColors = new Color[targetRenderers.Length];
                for (int i = 0; i < targetRenderers.Length; i++)
                {
                    if (targetRenderers[i] != null)
                    {
                        originalColors[i] = targetRenderers[i].color;
                    }
                }
                hasInitializedRenderers = true;
            }
        }
        
        /// <summary>
        /// 创建冰冻覆盖效果（仅 LV5 使用）
        /// </summary>
        private void CreateFrostOverlay()
        {
            // 创建子物体
            frostOverlay = new GameObject("FrostOverlay");
            frostOverlay.transform.SetParent(transform);
            frostOverlay.transform.localPosition = Vector3.zero;
            frostOverlay.transform.localRotation = Quaternion.identity;
            frostOverlay.transform.localScale = Vector3.one;
            
            // 添加 SpriteRenderer
            frostOverlayRenderer = frostOverlay.AddComponent<SpriteRenderer>();
            
            // 设置 Sprite
            if (frostSprite != null)
            {
                frostOverlayRenderer.sprite = frostSprite;
            }
            else
            {
                // 尝试从父物体获取 Sprite 作为基础
                SpriteRenderer parentSR = GetComponentInChildren<SpriteRenderer>();
                if (parentSR != null && parentSR.sprite != null)
                {
                    frostOverlayRenderer.sprite = parentSR.sprite;
                }
            }
            
            // 设置颜色（初始透明）
            Color c = frostOverlayColor;
            c.a = 0f;
            frostOverlayRenderer.color = c;
            
            // 设置排序层（在怪物之上）
            frostOverlayRenderer.sortingLayerName = "Enemy";
            frostOverlayRenderer.sortingOrder = 10;
            
            // 初始隐藏
            frostOverlay.SetActive(false);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 应用减速效果
        /// </summary>
        /// <param name="percent">减速百分比（0.4 = 减速40%）</param>
        /// <param name="duration">持续时间（秒）</param>
        public void ApplySlow(float percent, float duration)
        {
            // 如果已经冰冻，不覆盖
            if (isFrozen) return;
            
            // 确保渲染器已初始化
            if (!hasInitializedRenderers)
            {
                InitializeTargetRenderers();
            }
            
            // 刷新减速（取更强的效果）
            if (percent >= slowPercent || duration > remainingDuration)
            {
                slowPercent = Mathf.Max(slowPercent, percent);
                remainingDuration = Mathf.Max(remainingDuration, duration);
            }
            
            if (!isSlowed)
            {
                isSlowed = true;
                
                // 设置颜色染色目标（淡蓝色）
                currentTintTarget = slowTintColor;
                
                // 通知 EnemyBlob 更新速度
                NotifySpeedChange();
            }
        }
        
        /// <summary>
        /// 应用完全冰冻效果（LV5）
        /// </summary>
        /// <param name="duration">冰冻持续时间（秒）</param>
        public void ApplyFreeze(float duration)
        {
            // 确保渲染器已初始化
            if (!hasInitializedRenderers)
            {
                InitializeTargetRenderers();
            }
            
            isFrozen = true;
            frozenDuration = duration;
            
            // 设置颜色染色目标（深蓝色）
            currentTintTarget = frozenTintColor;
            
            // 显示冰冻覆盖
            targetOverlayAlpha = frostOverlayColor.a;
            frostOverlay.SetActive(true);
            
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
            
            // 重置颜色
            currentTintTarget = Color.white;
            RestoreOriginalColors();
            
            // 重置覆盖
            currentOverlayAlpha = 0f;
            targetOverlayAlpha = 0f;
            breathTimer = 0f;
            
            if (frostOverlayRenderer != null)
            {
                Color c = frostOverlayRenderer.color;
                c.a = 0f;
                frostOverlayRenderer.color = c;
            }
            
            if (frostOverlay != null)
            {
                frostOverlay.SetActive(false);
                frostOverlay.transform.localScale = Vector3.one;
            }
        }
        
        /// <summary>
        /// 设置冰冻 Sprite（可从外部配置）
        /// </summary>
        public void SetFrostSprite(Sprite sprite)
        {
            frostSprite = sprite;
            if (frostOverlayRenderer != null)
            {
                frostOverlayRenderer.sprite = sprite;
            }
        }
        
        /// <summary>
        /// 外部设置目标渲染器（用于 Boss 等特殊情况）
        /// </summary>
        public void SetTargetRenderers(SpriteRenderer[] renderers)
        {
            targetRenderers = renderers;
            
            if (targetRenderers != null && targetRenderers.Length > 0)
            {
                originalColors = new Color[targetRenderers.Length];
                for (int i = 0; i < targetRenderers.Length; i++)
                {
                    if (targetRenderers[i] != null)
                    {
                        originalColors[i] = targetRenderers[i].color;
                    }
                }
                hasInitializedRenderers = true;
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
                    
                    // 隐藏冰冻覆盖
                    targetOverlayAlpha = 0f;
                    
                    // 冰冻结束后检查是否还有减速
                    if (!isSlowed || remainingDuration <= 0f)
                    {
                        EndDebuff();
                    }
                    else
                    {
                        // 恢复到减速状态的颜色
                        currentTintTarget = slowTintColor;
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
            
            // 渐隐颜色染色
            currentTintTarget = Color.white;
            
            // 渐隐覆盖
            targetOverlayAlpha = 0f;
            
            // 通知 EnemyBlob 恢复速度
            NotifySpeedChange();
        }
        
        /// <summary>
        /// 更新颜色染色效果
        /// </summary>
        private void UpdateColorTint()
        {
            if (targetRenderers == null || originalColors == null) return;
            
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] == null) continue;
                
                Color targetColor;
                if (currentTintTarget == Color.white)
                {
                    // 恢复原色
                    targetColor = originalColors[i];
                }
                else
                {
                    // 混合染色
                    //targetColor = Color.Lerp(originalColors[i], currentTintTarget, tintBlendRatio);
                    targetColor = slowTintColor;
                }
                
                // 平滑过渡
                targetRenderers[i].color = Color.Lerp(
                    targetRenderers[i].color, 
                    targetColor, 
                    colorFadeSpeed * Time.deltaTime
                );
            }
        }
        
        /// <summary>
        /// 恢复原始颜色
        /// </summary>
        private void RestoreOriginalColors()
        {
            if (targetRenderers == null || originalColors == null) return;
            
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null && i < originalColors.Length)
                {
                    targetRenderers[i].color = originalColors[i];
                }
            }
        }
        
        /// <summary>
        /// 更新覆盖透明度（渐变）
        /// </summary>
        private void UpdateOverlayAlpha()
        {
            if (frostOverlayRenderer == null) return;
            
            // 平滑插值
            float fadeSpeed = 1f / overlayFadeDuration;
            currentOverlayAlpha = Mathf.MoveTowards(currentOverlayAlpha, targetOverlayAlpha, fadeSpeed * Time.deltaTime);
            
            // 应用颜色
            Color c = frostOverlayRenderer.color;
            c.a = currentOverlayAlpha;
            frostOverlayRenderer.color = c;
            
            // 完全透明时隐藏
            if (currentOverlayAlpha <= 0.01f && targetOverlayAlpha <= 0f)
            {
                frostOverlay.SetActive(false);
            }
        }
        
        /// <summary>
        /// 更新呼吸动画（仅冰冻覆盖）
        /// </summary>
        private void UpdateBreathAnimation()
        {
            if (!frostOverlay.activeSelf) return;
            if (frostOverlay == null) return;
            
            breathTimer += Time.deltaTime;
            
            // 正弦波呼吸
            float t = (Mathf.Sin(breathTimer * 2f * Mathf.PI / breathCycleDuration) + 1f) * 0.5f;
            float scale = Mathf.Lerp(breathScaleMin, breathScaleMax, t);
            
            frostOverlay.transform.localScale = Vector3.one * scale;
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