using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using LightVsDecay.Logic;
using LightVsDecay.Logic.Enemy;
using LightVsDecay.Logic.Player;
using LightVsDecay.Logic.XP;

namespace LightVsDecay.UI.Panels
{
    /// <summary>
    /// HUD 控制器
    /// 负责 GameScene 所有 HUD UI 元素的更新
    /// </summary>
    public class HUDPanel : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 顶部区域
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("顶部区域 - TopArea")]
        [SerializeField] private TextMeshProUGUI stageNameText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private TextMeshProUGUI coinText;
        
        [Header("经验条")]
        [SerializeField] private GameObject expBarObj;  // 经验条物体（用于隐藏）
        [SerializeField] private Slider expBar;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private RectTransform expBarTarget;

        [Header("Boss血条")]
        [SerializeField] private GameObject bossBloodBarObj;
        [SerializeField] private Image bossBloodFill;   // Fill02 红色血量图
        [SerializeField] private Image bossBloodBuffer; // Fill01 白色缓冲条
        [SerializeField] private TextMeshProUGUI bossNameText;
        [Tooltip("缓冲延迟时间（秒）")]
        [SerializeField] private float bossBufferDelay = 0.35f;
        [Tooltip("缓冲缓动时间（秒）")]
        [SerializeField] private float bossBufferDuration = 0.6f;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 中间区域
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("中间区域 - MidArea")]
        [SerializeField] private TextMeshProUGUI comboCountText;
        [SerializeField] private Slider gameTimerBar;
        
        [Header("连击显示设置")]
        [SerializeField] private float comboFadeDelay = 1.5f;
        [SerializeField] private float comboFadeDuration = 0.3f;
        [SerializeField] private CanvasGroup comboCanvasGroup;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 底部区域
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("底部区域 - BottomArea")]
        [SerializeField] private Image[] heartImages; // 3个红心图标
        [SerializeField] private Image[] shieldImages; // 3个护盾图标
        
        [Header("大招按钮")]
        [SerializeField] private Button skillButton;
        [SerializeField] private Image skillProgressImage; // fillAmount 控制
        
        [Header("大招就绪特效")]
        [SerializeField] private GameObject skillReadyEffect;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 关卡设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("关卡设置")]
        [SerializeField] private string currentStageName = "第一章 - 下水道";
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Coroutine comboFadeCoroutine;
        private bool ultReady = false;
        
// Boss血条缓冲效果
        private float bossCurrentHP = 1f;
        private float bossBufferHP = 1f;
        private Coroutine bossBufferCoroutine;
        private BossHealth cachedBossHealth;  // 缓存 BossHealth 引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 初始化UI状态
            InitializeUI();
        }
        
        private void Start()
        {
            // 订阅事件
            SubscribeEvents();
            
            // 设置按钮回调
            SetupButtons();
            RegisterExpBarTarget();
        }
        
        private void OnDestroy()
        {
            // 取消订阅
            UnsubscribeEvents();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void InitializeUI()
        {
            // 关卡名称
            if (stageNameText != null)
            {
                stageNameText.text = currentStageName;
            }
            
            // 金币
            UpdateCoinDisplay(0);
            
            // 经验
            UpdateExpDisplay(0, 10);
            UpdateLevelDisplay(1);
            
            // 连击（初始隐藏）
            if (comboCanvasGroup != null)
            {
                comboCanvasGroup.alpha = 0f;
            }
            
            // 游戏计时器
            UpdateGameTimer(0f, 300f);
            
            // 玩家血量
            UpdateHullHP(3, 3);
            UpdateShieldHP(3, 3);
            
            // 大招
            UpdateUltEnergy(0, 100);
            SetUltReady(false);
            
            // ═══ 新增：初始化 Boss 血条状态 ═══
            bossCurrentHP = 1f;
            bossBufferHP = 1f;
            // Boss血条（初始隐藏）
            if (bossBloodBarObj != null)
            {
                bossBloodBarObj.SetActive(false);
            }
        }
        
        private void SubscribeEvents()
        {
            // 进度事件
            Core.GameEvents.OnExpChanged += OnExpChanged;
            Core.GameEvents.OnLevelUp += OnLevelUp;
            Core.GameEvents.OnCoinChanged += OnCoinChanged;
            Core.GameEvents.OnUltEnergyChanged += OnUltEnergyChanged;
            Core.GameEvents.OnUltReady += OnUltReady;
            Core.GameEvents.OnUltUsed += OnUltUsed;
            Core.GameEvents.OnComboChanged += OnComboChanged;
            Core.GameEvents.OnComboReset += OnComboReset;
            
            // 玩家状态事件
            Core.GameEvents.OnShieldHPChanged += OnShieldHPChanged;
            Core.GameEvents.OnHullHPChanged += OnHullHPChanged;
            
            // 游戏时间事件
            Core.GameEvents.OnGameTimeUpdated += OnGameTimeUpdated;
            // ═══ 新增：Boss 事件 ═══
            Core.GameEvents.OnBossHealthChanged += OnBossHealthChanged;
            Core.GameEvents.OnBossFightStart += OnBossFightStart;
            Core.GameEvents.OnBossDeath += OnBossDeath;
        }
        
        private void UnsubscribeEvents()
        {
            Core.GameEvents.OnExpChanged -= OnExpChanged;
            Core.GameEvents.OnLevelUp -= OnLevelUp;
            Core.GameEvents.OnCoinChanged -= OnCoinChanged;
            Core.GameEvents.OnUltEnergyChanged -= OnUltEnergyChanged;
            Core.GameEvents.OnUltReady -= OnUltReady;
            Core.GameEvents.OnUltUsed -= OnUltUsed;
            Core.GameEvents.OnComboChanged -= OnComboChanged;
            Core.GameEvents.OnComboReset -= OnComboReset;
            
            Core.GameEvents.OnShieldHPChanged -= OnShieldHPChanged;
            Core.GameEvents.OnHullHPChanged -= OnHullHPChanged;
            
            Core.GameEvents.OnGameTimeUpdated -= OnGameTimeUpdated;
            // ═══ 新增：Boss 事件取消订阅 ═══
            Core.GameEvents.OnBossHealthChanged -= OnBossHealthChanged;
            Core.GameEvents.OnBossFightStart -= OnBossFightStart;
            Core.GameEvents.OnBossDeath -= OnBossDeath;
        }
        
        private void SetupButtons()
        {
            // 暂停按钮（暂时不实现功能）
            if (pauseButton != null)
            {
                pauseButton.onClick.AddListener(OnPauseButtonClicked);
            }
            
            // 大招按钮
            if (skillButton != null)
            {
                skillButton.onClick.AddListener(OnSkillButtonClicked);
            }
        }
        /// <summary>
        /// 注册经验条世界坐标位置获取器
        /// </summary>
        private void RegisterExpBarTarget()
        {
            if (XPOrbSpawner.Instance != null)
            {
                XPOrbSpawner.Instance.SetTargetPositionGetter(GetExpBarWorldPosition);
            }
            else
            {
                // 延迟注册（等待 XPOrbSpawner 初始化）
                StartCoroutine(DelayedRegisterExpBarTarget());
            }
        }
        private IEnumerator DelayedRegisterExpBarTarget()
        {
            yield return null; // 等待一帧
    
            if (XPOrbSpawner.Instance != null)
            {
                XPOrbSpawner.Instance.SetTargetPositionGetter(GetExpBarWorldPosition);
            }
        }
        /// <summary>
        /// 获取经验条的世界坐标位置
        /// Screen Space - Overlay 模式下，将 UI 屏幕坐标转换为世界坐标
        /// </summary>
        private Vector3 GetExpBarWorldPosition()
        {
            RectTransform target = expBarTarget != null ? expBarTarget : (expBar != null ? expBar.GetComponent<RectTransform>() : null);
    
            if (target == null)
            {
                // 默认返回屏幕顶部中央
                return Camera.main.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f, Screen.height * 0.95f, 10f));
            }
    
            // 获取 UI 元素的屏幕坐标
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
    
            // 计算中心点（Screen Space - Overlay 模式下 corners 就是屏幕坐标）
            Vector3 screenPos = (corners[0] + corners[2]) * 0.5f;
    
            // 转换为世界坐标（z 值设为相机前方一定距离）
            if (Camera.main != null)
            {
                screenPos.z = 10f; // 距离相机的深度
                return Camera.main.ScreenToWorldPoint(screenPos);
            }
    
            return screenPos;
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnExpChanged(int current, int required)
        {
            UpdateExpDisplay(current, required);
        }
        
        private void OnLevelUp(int newLevel)
        {
            UpdateLevelDisplay(newLevel);
            // TODO: 播放升级特效
        }
        
        private void OnCoinChanged(int coins)
        {
            UpdateCoinDisplay(coins);
        }
        
        private void OnUltEnergyChanged(int current, int max)
        {
            UpdateUltEnergy(current, max);
        }
        
        private void OnUltReady()
        {
            SetUltReady(true);
        }
        
        private void OnUltUsed()
        {
            SetUltReady(false);
        }
        
        private void OnComboChanged(int combo)
        {
            UpdateComboDisplay(combo);
        }
        
        private void OnComboReset()
        {
            HideCombo();
        }
        
        private void OnShieldHPChanged(int current, int max)
        {
            UpdateShieldHP(current, max);
        }
        
        private void OnHullHPChanged(int current, int max)
        {
            UpdateHullHP(current, max);
        }
        
        private void OnGameTimeUpdated(float current, float total)
        {
            UpdateGameTimer(current, total);
        }
// ═══ Boss 战斗开始 ═══
        private void OnBossFightStart()
        {
            // 查找并缓存 BossHealth
            cachedBossHealth = FindObjectOfType<BossHealth>();
    
            int maxHealth = cachedBossHealth != null ? (int)cachedBossHealth.MaxHealth : 50000;
            ShowBossHealthBar("THE CORRUPTOR", maxHealth);
        }

// ═══ Boss 血量变化（参数只有百分比）═══
        private void OnBossHealthChanged(float healthPercent)
        {
            // 从缓存的 BossHealth 获取当前血量
            int currentHealth = 0;
            if (cachedBossHealth != null)
            {
                currentHealth = (int)cachedBossHealth.CurrentHealth;
            }
    
            UpdateBossHealthWithValue(healthPercent, currentHealth);
        }

// ═══ Boss 死亡 ═══
        private void OnBossDeath()
        {
            //HideBossHealthBar();
            cachedBossHealth = null;
            // 启动死亡处理协程
            StartCoroutine(BossDeathSequence());
        }
        /// <summary>Boss 死亡序列 - 确保血条归零动画完成</summary>
        private IEnumerator BossDeathSequence()
        {
            // 强制触发最后一次缓冲动画（血量归零）
            bossCurrentHP = 0f;
            UpdateBossHealthDisplay(0f);
    
            if (bossBufferCoroutine != null)
            {
                StopCoroutine(bossBufferCoroutine);
            }
            bossBufferCoroutine = StartCoroutine(BossBufferCoroutine());
    
            // 等待缓冲动画完成
            yield return new WaitForSeconds(bossBufferDelay + bossBufferDuration + 0.1f);
    
            // 清理缓存
            cachedBossHealth = null;
    
            // 注意：不隐藏血条，让结算面板显示
            // 如果需要隐藏，取消注释下面这行
            // HideBossHealthBar();
        }
        /// <summary>更新Boss血量（带缓冲效果）</summary>
        private void UpdateBossHealthWithValue(float normalizedHP, int currentHealth)
        {
            // 红色条瞬间减少
            bossCurrentHP = normalizedHP;
            UpdateBossHealthDisplay(normalizedHP);

            // 白色缓冲条延迟追赶
            if (bossBufferCoroutine != null)
            {
                StopCoroutine(bossBufferCoroutine);
            }
            bossBufferCoroutine = StartCoroutine(BossBufferCoroutine());
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // UI 更新方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>更新金币显示</summary>
        private void UpdateCoinDisplay(int coins)
        {
            if (coinText != null)
            {
                coinText.text = coins.ToString();
            }
        }
        
        /// <summary>更新经验条</summary>
        private void UpdateExpDisplay(int current, int required)
        {
            if (expBar != null)
            {
                expBar.value = required > 0 ? (float)current / required : 0f;
            }
        }
        
        /// <summary>更新等级显示</summary>
        private void UpdateLevelDisplay(int level)
        {
            if (levelText != null)
            {
                levelText.text = $"Lv.{level}";
            }
        }
        
        /// <summary>更新游戏计时器</summary>
        private void UpdateGameTimer(float current, float total)
        {
            if (gameTimerBar != null)
            {
                gameTimerBar.value = total > 0 ? current / total : 0f;
            }
        }
        
        /// <summary>更新连击显示</summary>
        private void UpdateComboDisplay(int combo)
        {
            if (combo <= 0) return;
            
            if (comboCountText != null)
            {
                comboCountText.text = $"<size=124>{combo}</size><size=60>x</size>";
            }
            
            // 显示连击
            ShowCombo();
            
            // 重置淡出计时
            if (comboFadeCoroutine != null)
            {
                StopCoroutine(comboFadeCoroutine);
            }
            comboFadeCoroutine = StartCoroutine(ComboFadeCoroutine());
        }
        
        /// <summary>显示连击</summary>
        private void ShowCombo()
        {
            if (comboCanvasGroup != null)
            {
                comboCanvasGroup.alpha = 1f;
            }
        }
        
        /// <summary>隐藏连击</summary>
        private void HideCombo()
        {
            if (comboFadeCoroutine != null)
            {
                StopCoroutine(comboFadeCoroutine);
            }
            
            if (comboCanvasGroup != null)
            {
                comboCanvasGroup.alpha = 0f;
            }
        }
        
        /// <summary>连击淡出协程</summary>
        private IEnumerator ComboFadeCoroutine()
        {
            yield return new WaitForSeconds(comboFadeDelay);
            
            float elapsed = 0f;
            while (elapsed < comboFadeDuration)
            {
                elapsed += Time.deltaTime;
                if (comboCanvasGroup != null)
                {
                    comboCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / comboFadeDuration);
                }
                yield return null;
            }
            
            if (comboCanvasGroup != null)
            {
                comboCanvasGroup.alpha = 0f;
            }
        }
        
        /// <summary>更新本体血量（红心）</summary>
        private void UpdateHullHP(int current, int max)
        {
            if (heartImages == null) return;
            
            for (int i = 0; i < heartImages.Length; i++)
            {
                if (heartImages[i] != null)
                {
                    heartImages[i].gameObject.SetActive(i < current);
                }
            }
        }
        
        /// <summary>更新护盾血量</summary>
        private void UpdateShieldHP(int current, int max)
        {
            if (shieldImages == null) return;
            
            for (int i = 0; i < shieldImages.Length; i++)
            {
                if (shieldImages[i] != null)
                {
                    shieldImages[i].gameObject.SetActive(i < current);
                }
            }
        }
        
        /// <summary>更新大招能量</summary>
        private void UpdateUltEnergy(int current, int max)
        {
            if (skillProgressImage != null)
            {
                skillProgressImage.fillAmount = max > 0 ? (float)current / max : 0f;
            }
        }
        
        /// <summary>设置大招就绪状态</summary>
        private void SetUltReady(bool ready)
        {
            ultReady = ready;
            
            // 显示/隐藏就绪特效
            if (skillReadyEffect != null)
            {
                skillReadyEffect.SetActive(ready);
            }
            
            // 可以改变按钮颜色或添加动画
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Boss 血条（带缓冲效果）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>显示Boss血条，隐藏经验条</summary>
        public void ShowBossHealthBar(string bossName, int maxHealth)
        {
            // ═══ 隐藏经验条 ═══
            if (expBarObj != null)
            {
                expBarObj.SetActive(false);
            }
    
            // ═══ 禁用经验球生成 ═══
            if (XPOrbSpawner.Instance != null)
            {
                XPOrbSpawner.Instance.SetSpawningEnabled(false);
            }
    
            // ═══ 显示Boss血条 ═══
            if (bossBloodBarObj != null)
            {
                bossBloodBarObj.SetActive(true);
            }

            if (bossNameText != null)
            {
                bossNameText.text = bossName;
            }

            bossCurrentHP = 1f;
            bossBufferHP = 1f;

            UpdateBossHealthDisplay(1f);

            // 同步缓冲条
            if (bossBloodBuffer != null)
            {
                bossBloodBuffer.fillAmount = 1f;
            }
        }

        
        /// <summary>隐藏Boss血条</summary>
        public void HideBossHealthBar()
        {
            if (bossBloodBarObj != null)
            {
                bossBloodBarObj.SetActive(false);
            }
        }
        
        /// <summary>更新Boss血量（带缓冲效果）</summary>
        public void UpdateBossHealth(float currentHP, float maxHP)
        {
            float normalizedHP = maxHP > 0 ? currentHP / maxHP : 0f;
            
            // 红色条瞬间减少
            bossCurrentHP = normalizedHP;
            
            UpdateBossHealthDisplay(normalizedHP);
            
            // 白色缓冲条延迟追赶
            if (bossBufferCoroutine != null)
            {
                StopCoroutine(bossBufferCoroutine);
            }
            bossBufferCoroutine = StartCoroutine(BossBufferCoroutine());
        }
        
        private void UpdateBossHealthDisplay(float normalized)
        {
            if (bossBloodFill != null)
            {
                bossBloodFill.fillAmount = normalized;
            }
        }
        
        /// <summary>【优化】Boss血条缓冲动画 - 0.5秒延迟 + 0.3秒缓动</summary>
        private IEnumerator BossBufferCoroutine()
        {
            // 使用配置的延迟
            yield return new WaitForSeconds(bossBufferDelay);

            float startBuffer = bossBufferHP;
            float targetBuffer = bossCurrentHP;
            float elapsed = 0f;

            while (elapsed < bossBufferDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / bossBufferDuration);
        
                // EaseOutCubic 缓动 - 比 EaseOutQuad 更丝滑
                float easeT = 1f - Mathf.Pow(1f - t, 3f);
        
                bossBufferHP = Mathf.Lerp(startBuffer, targetBuffer, easeT);
        
                if (bossBloodBuffer != null)
                {
                    bossBloodBuffer.fillAmount = bossBufferHP;
                }
        
                yield return null;
            }

            // 确保最终值精确
            bossBufferHP = targetBuffer;
            if (bossBloodBuffer != null)
            {
                bossBloodBuffer.fillAmount = bossBufferHP;
            }
        }
        /// <summary>【新增】更新Boss血量百分比（带缓冲效果）</summary>
        public void UpdateBossHealthPercent(float normalizedHP, int currentHealth)
        {
            bossCurrentHP = normalizedHP;
            UpdateBossHealthDisplay(normalizedHP);

            // 白色缓冲条延迟追赶
            if (bossBufferCoroutine != null)
            {
                StopCoroutine(bossBufferCoroutine);
            }
            bossBufferCoroutine = StartCoroutine(BossBufferCoroutine());
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnPauseButtonClicked()
        {
            // 暂停功能暂不实现
            Debug.Log("[HUDController] 暂停按钮点击（功能暂未实现）");
        }
        
        private void OnSkillButtonClicked()
        {
            if (!ultReady)
            {
                Debug.Log("[HUDController] 大招尚未准备好");
                return;
            }
            
            // 通过 PlayerProgressManager 使用大招
            if (ProgressManager.Instance != null)
            {
                if (ProgressManager.Instance.UseUlt())
                {
                    // 触发激光控制器的大招
                    var laserController = FindObjectOfType<LaserController>();
                    if (laserController != null)
                    {
                        laserController.ActivateUlt();
                    }
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>设置关卡名称</summary>
        public void SetStageName(string name)
        {
            currentStageName = name;
            if (stageNameText != null)
            {
                stageNameText.text = name;
            }
        }
    }
}