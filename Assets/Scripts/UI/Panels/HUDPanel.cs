using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using LightVsDecay.Audio;
using LightVsDecay.Logic;
using LightVsDecay.Logic.Enemy;
using LightVsDecay.Logic.Player;
using LightVsDecay.Logic.XP;
using LightVsDecay.Core;

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
        
        [Header("波次进度")]
        [Tooltip("波次进度条（复用原 gameTimerBar）")]
        [SerializeField] private Slider waveProgressBar;  // 改名更清晰
        
        [Tooltip("波次文本（如：波次: 3/12）")]
        [SerializeField] private TextMeshProUGUI waveText;
        
        [Header("连击显示设置")]
        [SerializeField] private float comboFadeDelay = 1.5f;
        [SerializeField] private float comboFadeDuration = 0.3f;
        [SerializeField] private CanvasGroup comboCanvasGroup;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 - 底部区域
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("底部区域 - BottomArea")]
        [Header("玩家血条")]
        [SerializeField] private Image healthBarFill;     // 红色血量条
        [SerializeField] private Image healthBarBuffer;   // 白色缓冲条
        [SerializeField] private TextMeshProUGUI healthText; // 可选：显示 "850/1000"

        [Header("玩家护盾条")]
        [SerializeField] private Image shieldBarFill;     // 青色护盾条
        [SerializeField] private Image shieldBarBuffer;   // 白色缓冲条
        [SerializeField] private TextMeshProUGUI shieldText; // 可选：显示 "350/500"

        [Header("血条/护盾条缓冲设置")]
        [Tooltip("缓冲延迟时间（秒）")]
        [SerializeField] private float playerBufferDelay = 0.35f;
        [Tooltip("缓冲缓动时间（秒）")]
        [SerializeField] private float playerBufferDuration = 0.6f;
        
        [Header("═══ 设置面板 ═══")]
        [SerializeField] private SettingsPanel settingsPanel;
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
        // 玩家血条缓冲效果
        private float healthCurrentPercent = 1f;
        private float healthBufferPercent = 1f;
        private Coroutine healthBufferCoroutine;

// 玩家护盾条缓冲效果
        private float shieldCurrentPercent = 1f;
        private float shieldBufferPercent = 1f;
        private Coroutine shieldBufferCoroutine;       
// Boss血条缓冲效果
        private float bossCurrentHP = 1f;
        private float bossBufferHP = 1f;
        private float bossWaveProgress = 0f;
        private Coroutine bossBufferCoroutine;
        private BossHealth cachedBossHealth;  // 缓存 BossHealth 引用
        private bool isBufferWaiting = false;   // 正在等待0.5秒延迟
        private bool isBufferChasing = false;   // 正在追赶中
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
            RefreshWaveProgressFromRuntime();
        }

        private void Update()
        {
            RefreshWaveProgressFromRuntime();
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
            // 波次进度（初始化为第1波）
            UpdateWaveProgressDisplay(0f, 1, 1);
            
            // 玩家血量
            healthCurrentPercent = 1f;
            healthBufferPercent = 1f;
            shieldCurrentPercent = 1f;
            shieldBufferPercent = 1f;

            if (healthBarFill != null) healthBarFill.fillAmount = 1f;
            if (healthBarBuffer != null) healthBarBuffer.fillAmount = 1f;
            if (shieldBarFill != null) shieldBarFill.fillAmount = 1f;
            if (shieldBarBuffer != null) shieldBarBuffer.fillAmount = 1f;

            // ═══ 新增：初始化 Boss 血条状态 ═══
            bossCurrentHP = 1f;
            bossBufferHP = 1f;
            bossWaveProgress = 0f;
            isBufferWaiting = false;
            isBufferChasing = false;
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
            Core.GameEvents.OnComboChanged += OnComboChanged;
            Core.GameEvents.OnComboReset += OnComboReset;
            
            // 玩家状态事件
            Core.GameEvents.OnShieldHPChanged += OnShieldHPChanged;
            Core.GameEvents.OnHullHPChanged += OnHullHPChanged;
            
            // 波次进度事件
            Core.GameEvents.OnWaveProgressUpdated += OnWaveProgressUpdated;
            Core.GameEvents.OnWaveStart += OnWaveStart;
            Core.GameEvents.OnWaveComplete += OnWaveComplete;
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
            Core.GameEvents.OnComboChanged -= OnComboChanged;
            Core.GameEvents.OnComboReset -= OnComboReset;
            
            Core.GameEvents.OnShieldHPChanged -= OnShieldHPChanged;
            Core.GameEvents.OnHullHPChanged -= OnHullHPChanged;
            
            Core.GameEvents.OnWaveProgressUpdated -= OnWaveProgressUpdated;
            Core.GameEvents.OnWaveStart -= OnWaveStart;
            Core.GameEvents.OnWaveComplete -= OnWaveComplete;
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

            Camera gameCamera = Camera.main;
            if (gameCamera == null)
            {
                return target.position;
            }

            Canvas parentCanvas = target.GetComponentInParent<Canvas>();
            Camera uiCamera = null;
            if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = parentCanvas.worldCamera;
            }

            Vector3 uiWorldCenter = target.TransformPoint(target.rect.center);
            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, uiWorldCenter);
            screenPos.z = Mathf.Abs(gameCamera.transform.position.z - transform.position.z);

            return gameCamera.ScreenToWorldPoint(screenPos);
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnExpChanged(int current, int required)
        {
            GameLogger.Log($"[HUDPanel] ★ OnExpChanged 收到! {current}/{required}, expBar={expBar != null}");
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
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 波次 UI 更新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>更新波次进度显示</summary>
        private void UpdateWaveProgressDisplay(float progress, int currentWave, int totalWaves)
        {
            // 更新进度条（显示已完成的波次比例）
            progress = Mathf.Clamp01(progress);
            currentWave = Mathf.Max(currentWave, 1);
            totalWaves = Mathf.Max(totalWaves, 1);

            if (waveProgressBar != null)
            {
                // 进度 = (当前波次 - 1) / 总波次
                // 例如：第3波时进度为 2/12 = 16.7%
                waveProgressBar.value = Mathf.Clamp01(progress);
            }
            
            // 更新波次文本
            if (waveText != null)
            {
                waveText.text = $"WAVE: {currentWave}/{totalWaves}";
            }
        }
        
        /// <summary>波次进度更新回调</summary>
        /// <summary>鏍规嵁褰撳墠娉㈡鍜屾尝鍐呰繘搴﹀埛鏂版€绘尝娆¤繘搴︽潯</summary>
        private void RefreshWaveProgressFromRuntime()
        {
            WaveManager waveManager = WaveManager.Instance;
            if (waveManager == null)
            {
                return;
            }

            int totalWaves = Mathf.Max(waveManager.TotalWaves, 1);
            int currentWave = waveManager.CurrentWaveNumber;

            if (currentWave <= 0)
            {
                UpdateWaveProgressDisplay(0f, 1, totalWaves);
                return;
            }

            currentWave = Mathf.Clamp(currentWave, 1, totalWaves);

            float intraWaveProgress = 0f;
            switch (waveManager.CurrentState)
            {
                case WaveState.Spawning:
                case WaveState.Battle:
                    intraWaveProgress = Mathf.Clamp01(waveManager.WaveProgress);
                    break;
                case WaveState.Complete:
                case WaveState.Victory:
                    intraWaveProgress = 1f;
                    break;
                case WaveState.BossFight:
                    intraWaveProgress = Mathf.Clamp01(bossWaveProgress);
                    break;
            }

            float overallProgress = ((currentWave - 1) + intraWaveProgress) / totalWaves;
            UpdateWaveProgressDisplay(overallProgress, currentWave, totalWaves);
        }

        private void OnWaveProgressUpdated(int currentWave, int totalWaves)
        {
            RefreshWaveProgressFromRuntime();
        }
        
        /// <summary>波次开始回调</summary>
        private void OnWaveStart(int currentWave, int totalWaves)
        {
            RefreshWaveProgressFromRuntime();
            
            // TODO: 可以在这里显示波次开始的提示动画
            // 例如：闪烁波次文本、播放音效等
        }
        
        /// <summary>波次完成回调</summary>
        private void OnWaveComplete(int completedWave, int totalWaves)
        {
            // 波次完成时，进度条填满到当前波次
            RefreshWaveProgressFromRuntime();
            
            // TODO: 可以在这里显示波次完成的庆祝动画
            // 例如：进度条闪光、播放音效等
        }

// ═══ Boss 战斗开始 ═══
        private void OnBossFightStart()
        {
            // 查找并缓存 BossHealth
            cachedBossHealth = FindObjectOfType<BossHealth>();
            bossWaveProgress = 0f;
            RefreshWaveProgressFromRuntime();
    
            int maxHealth = cachedBossHealth != null ? (int)cachedBossHealth.MaxHealth : 50000;
            ShowBossHealthBar("THE CORRUPTOR", maxHealth);
        }

// ═══ Boss 血量变化（参数只有百分比）═══
        private void OnBossHealthChanged(float healthPercent)
        {
            // 从缓存的 BossHealth 获取当前血量
            bossWaveProgress = Mathf.Clamp01(1f - healthPercent);
            RefreshWaveProgressFromRuntime();
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
            bossWaveProgress = 1f;
            RefreshWaveProgressFromRuntime();
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
            bool isHealing = normalizedHP > bossCurrentHP;

            if (isHealing)
            {
                // 回血：buffer（红色底）立即跳到新值，solid（实体HP）慢追上去
                // 视觉效果：先亮红底 → 再填实，表现出血条"被灌入"的饱胀感
                bossCurrentHP = normalizedHP;
                if (bossBloodBuffer != null)
                    bossBloodBuffer.fillAmount = normalizedHP;  // buffer 立即跳

                // solid 慢追：用 BossHealCoroutine 从当前值补到目标
                if (bossBufferCoroutine != null)
                    StopCoroutine(bossBufferCoroutine);
                bossBufferCoroutine = StartCoroutine(BossHealFillCoroutine(normalizedHP));
            }
            else
            {
                // 掉血：solid 先降，buffer 延迟慢追（原有逻辑不变）
                bossCurrentHP = normalizedHP;
                UpdateBossHealthDisplay(normalizedHP);

                if (!isBufferWaiting && !isBufferChasing)
                {
                    if (bossBufferCoroutine != null)
                        StopCoroutine(bossBufferCoroutine);
                    bossBufferCoroutine = StartCoroutine(BossBufferCoroutine());
                }
            }
        }

        /// <summary>回血时：solid fill 从当前值慢速追上 buffer（已跳至目标）</summary>
        private IEnumerator BossHealFillCoroutine(float targetHP)
        {
            isBufferChasing = true;
            float chaseSpeed = 1f / bossBufferDuration;

            while (bossBloodFill != null &&
                   Mathf.Abs(bossBloodFill.fillAmount - targetHP) > 0.001f)
            {
                float maxMove = chaseSpeed * Time.deltaTime;
                bossBloodFill.fillAmount = Mathf.MoveTowards(bossBloodFill.fillAmount, targetHP, maxMove);
                yield return null;
            }

            if (bossBloodFill != null)
                bossBloodFill.fillAmount = targetHP;

            isBufferChasing = false;
            bossBufferCoroutine = null;
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
            float normalizedHP = max > 0 ? (float)current / max : 0f;
    
            // 红色条瞬间更新
            healthCurrentPercent = normalizedHP;
    
            if (healthBarFill != null)
            {
                healthBarFill.fillAmount = normalizedHP;
            }
    
            // 更新文本（可选）
            if (healthText != null)
            {
                healthText.text = $"{current}";
            }
    
            // 白色缓冲条延迟追赶
            if (healthBufferCoroutine != null)
            {
                StopCoroutine(healthBufferCoroutine);
            }
            healthBufferCoroutine = StartCoroutine(HealthBufferCoroutine());
        }
        /// <summary>血条缓冲动画协程</summary>
        private IEnumerator HealthBufferCoroutine()
        {
            yield return new WaitForSeconds(playerBufferDelay);
    
            float startBuffer = healthBufferPercent;
            float targetBuffer = healthCurrentPercent;
            float elapsed = 0f;
    
            while (elapsed < playerBufferDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / playerBufferDuration);
        
                // EaseOutCubic 缓动
                float easeT = 1f - Mathf.Pow(1f - t, 3f);
        
                healthBufferPercent = Mathf.Lerp(startBuffer, targetBuffer, easeT);
        
                if (healthBarBuffer != null)
                {
                    healthBarBuffer.fillAmount = healthBufferPercent;
                }
        
                yield return null;
            }
    
            // 确保最终值精确
            healthBufferPercent = targetBuffer;
            if (healthBarBuffer != null)
            {
                healthBarBuffer.fillAmount = healthBufferPercent;
            }
        }
        /// <summary>更新护盾血量</summary>
        private void UpdateShieldHP(int current, int max)
        {
            float normalizedHP = max > 0 ? (float)current / max : 0f;
    
            // 青色条瞬间更新
            shieldCurrentPercent = normalizedHP;
    
            if (shieldBarFill != null)
            {
                shieldBarFill.fillAmount = normalizedHP;
            }
    
            // 更新文本（可选）
            if (shieldText != null)
            {
                shieldText.text = $"{current}";
            }
    
            // 白色缓冲条延迟追赶
            if (shieldBufferCoroutine != null)
            {
                StopCoroutine(shieldBufferCoroutine);
            }
            shieldBufferCoroutine = StartCoroutine(ShieldBufferCoroutine());
        }
        /// <summary>护盾条缓冲动画协程</summary>
        private IEnumerator ShieldBufferCoroutine()
        {
            yield return new WaitForSeconds(playerBufferDelay);
    
            float startBuffer = shieldBufferPercent;
            float targetBuffer = shieldCurrentPercent;
            float elapsed = 0f;
    
            while (elapsed < playerBufferDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / playerBufferDuration);
        
                // EaseOutCubic 缓动
                float easeT = 1f - Mathf.Pow(1f - t, 3f);
        
                shieldBufferPercent = Mathf.Lerp(startBuffer, targetBuffer, easeT);
        
                if (shieldBarBuffer != null)
                {
                    shieldBarBuffer.fillAmount = shieldBufferPercent;
                }
        
                yield return null;
            }
    
            // 确保最终值精确
            shieldBufferPercent = targetBuffer;
            if (shieldBarBuffer != null)
            {
                shieldBarBuffer.fillAmount = shieldBufferPercent;
            }
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
            isBufferWaiting = false;
            isBufferChasing = false;
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
            // 标记：正在等待延迟
            isBufferWaiting = true;
    
            // 等待0.5秒延迟
            yield return new WaitForSeconds(bossBufferDelay);
    
            // 等待结束，开始追赶
            isBufferWaiting = false;
            isBufferChasing = true;
    
            // 持续追赶，直到缓冲条追上当前血量
            while (Mathf.Abs(bossBufferHP - bossCurrentHP) > 0.001f)
            {
                // 每帧读取最新的目标值（可能在追赶过程中血量继续下降）
                float targetBuffer = bossCurrentHP;
        
                // 计算本帧移动距离（使用固定速度）
                float chaseSpeed = 1f / bossBufferDuration; // 每秒追赶的百分比
                float maxMove = chaseSpeed * Time.deltaTime;
        
                // 向目标移动
                if (bossBufferHP > targetBuffer)
                {
                    bossBufferHP = Mathf.Max(targetBuffer, bossBufferHP - maxMove);
                }
                else
                {
                    bossBufferHP = Mathf.Min(targetBuffer, bossBufferHP + maxMove);
                }
        
                // 更新UI
                if (bossBloodBuffer != null)
                {
                    bossBloodBuffer.fillAmount = bossBufferHP;
                }
        
                yield return null;
            }
    
            // 确保最终值精确
            bossBufferHP = bossCurrentHP;
            if (bossBloodBuffer != null)
            {
                bossBloodBuffer.fillAmount = bossBufferHP;
            }
    
            // 追赶结束
            isBufferChasing = false;
            bossBufferCoroutine = null;
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
            GameLogger.Log("[HUDPanel] 点击暂停按钮");
    
            // 播放按钮音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButtonClick();
            }
    
            // 打开设置面板（显示底部按钮区域）
            if (settingsPanel != null)
            {
                settingsPanel.Show(true);
            }
            else
            {
                GameLogger.LogWarning("[HUDPanel] settingsPanel 未设置！");
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
