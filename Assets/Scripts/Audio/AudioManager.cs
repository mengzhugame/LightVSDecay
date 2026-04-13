// ============================================================
// AudioManager.cs
// 文件位置: Assets/Scripts/Audio/AudioManager.cs
// 用途：音频管理器单例，控制BGM和SFX播放
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;

namespace LightVsDecay.Audio
{
    /// <summary>
    /// 激光命中类型（决定播放哪种循环音效）
    /// 优先级: Metal > Burn > None
    /// </summary>
    public enum LaserHitType
    {
        None = 0,   // 未命中（空射）
        Burn = 1,   // 命中软体（灼烧）- 普通怪物/Boss外壳
        Metal = 2   // 命中金属（装甲）- 无人机/Boss眼睛
    }
    
    /// <summary>
    /// 音频管理器
    /// 继承自 AutoPersistentSingleton，自动创建并跨场景持久化
    /// </summary>
    public class AudioManager : AutoPersistentSingleton<AudioManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【关键】自动初始化 - 游戏启动时自动创建
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnGameStart()
        {
            // 调用基类的自动初始化方法
            AutoInitialize();
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 音频开关设置（新增）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private const string PREF_BGM_ENABLED = "BGMEnabled";
        private const string PREF_SFX_ENABLED = "SFXEnabled";

        private bool bgmEnabled = true;
        private bool sfxEnabled = true;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 配置 ═══")]
        [SerializeField] private AudioConfig config;
        
        [Header("═══ 场景名称配置 ═══")]
        [Tooltip("主菜单场景名称")]
        [SerializeField] private string mainMenuSceneName = "MainScene";
        
        [Tooltip("战斗场景名称")]
        [SerializeField] private string battleSceneName = "GameScene";
        
        [Header("═══ 调试 ═══")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 音频源组件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private AudioSource bgmSource;
        private AudioSource sfxSource;
        private AudioSource laserSource;      // 激光循环音效专用
        private AudioSource bossLoopSource;   // Boss 循环音效专用
        private bool battleSfxMuted = false;
        private int battleOverlayMuteCount = 0;
        private readonly List<AudioSource> pausedBattleSources = new List<AudioSource>();
        private float lastEnemyExplodeTime = -1f;
        private const float EnemyExplodeDedupWindow = 0.05f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 音量设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private const string PREF_BGM_VOLUME = "BGMVolume";
        private const string PREF_SFX_VOLUME = "SFXVolume";
        
        private float bgmVolume = 1f;
        private float sfxVolume = 1f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 经验球音效冷却
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float lastXpOrbCollectTime = -1f;
        private float lastCoinCollectTime = -1f;
        
        public float BGMVolume
        {
            get => bgmVolume;
            set
            {
                bgmVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(PREF_BGM_VOLUME, bgmVolume);
                UpdateBGMVolume();
            }
        }
        
        public float SFXVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(PREF_SFX_VOLUME, sfxVolume);
                UpdateLaserVolume();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 激光音效状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private bool isLaserActive = false;
        private LaserHitType currentLaserHitType = LaserHitType.None;
        private AudioClip currentLaserClip = null;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BGM 状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Coroutine bgmFadeCoroutine;
        private AudioClip currentBGM;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期（继承自 AutoPersistentSingleton）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            // 创建音频源
            CreateAudioSources();
            
            // 加载音量设置
            LoadVolumeSettings();
            
            if (showDebugInfo)
            {
                GameLogger.Log("[AudioManager] 单例初始化完成");
            }
        }
        
        private void Start()
        {
            // 首次运行时播放当前场景的BGM
            PlayBGMForCurrentScene();
        }
        
        private void OnEnable()
        {
            // 订阅场景切换事件
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            // 订阅游戏事件
            SubscribeToGameEvents();
        }
        
        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeFromGameEvents();
        }
        
        protected override void OnSingletonDestroy()
        {
            // 清理
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void CreateAudioSources()
        {
            // 先尝试获取已有的 AudioSource（预制体上可能已经存在）
            AudioSource[] existingSources = GetComponents<AudioSource>();
    
            if (existingSources.Length >= 4)
            {
                // 预制体上已有足够的 AudioSource，直接复用
                bgmSource = existingSources[0];
                sfxSource = existingSources[1];
                laserSource = existingSources[2];
                bossLoopSource = existingSources[3];
        
                if (showDebugInfo)
                {
                    GameLogger.Log("[AudioManager] 复用预制体上已有的 AudioSource");
                }
            }
            else
            {
                // 预制体上没有或不够，创建新的
                bgmSource = gameObject.AddComponent<AudioSource>();
                sfxSource = gameObject.AddComponent<AudioSource>();
                laserSource = gameObject.AddComponent<AudioSource>();
                bossLoopSource = gameObject.AddComponent<AudioSource>();
        
                if (showDebugInfo)
                {
                    GameLogger.Log("[AudioManager] 创建新的 AudioSource");
                }
            }
    
            // 配置 BGM 音频源
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.priority = 0;
    
            // 配置 SFX 音频源
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.priority = 128;
    
            // 配置激光循环音频源
            laserSource.loop = true;
            laserSource.playOnAwake = false;
            laserSource.priority = 64;
    
            // 配置 Boss 循环音频源
            bossLoopSource.loop = true;
            bossLoopSource.playOnAwake = false;
            bossLoopSource.priority = 32;
        }
        
        private void LoadVolumeSettings()
        {
            bgmVolume = PlayerPrefs.GetFloat(PREF_BGM_VOLUME, 1f);
            sfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOLUME, 1f);
            UpdateBGMVolume();
            UpdateLaserVolume();
            // 加载开关状态
            bgmEnabled = PlayerPrefs.GetInt(PREF_BGM_ENABLED, 1) == 1;
            sfxEnabled = PlayerPrefs.GetInt(PREF_SFX_ENABLED, 1) == 1;
        }
        
        private void UpdateBGMVolume()
        {
            if (bgmSource != null && config != null)
            {
                bgmSource.volume = bgmVolume * config.bgmDefaultVolume;
            }
        }
        
        private void UpdateLaserVolume()
        {
            if (laserSource != null && config != null)
            {
                laserSource.volume = sfxVolume * config.laserLoopVolume;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件订阅
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void SubscribeToGameEvents()
        {
            GameEvents.OnLevelUp += OnLevelUp;
            GameEvents.OnGameVictory += OnGameVictory;
            GameEvents.OnGameDefeat += OnGameDefeat;
            GameEvents.OnEnemyDied += OnEnemyDied;
            GameEvents.OnShieldBroken += OnShieldBroken;
            GameEvents.OnLowHealthStart += OnLowHealthStart;
            GameEvents.OnBossFightStart += OnBossFightStart;
            GameEvents.OnGameResumed += OnGameResumed;
            GameEvents.OnLevelUpChoiceComplete += OnLevelUpChoiceComplete;
        }
        
        private void UnsubscribeFromGameEvents()
        {
            GameEvents.OnLevelUp -= OnLevelUp;
            GameEvents.OnGameVictory -= OnGameVictory;
            GameEvents.OnGameDefeat -= OnGameDefeat;
            GameEvents.OnEnemyDied -= OnEnemyDied;
            GameEvents.OnShieldBroken -= OnShieldBroken;
            GameEvents.OnLowHealthStart -= OnLowHealthStart;
            GameEvents.OnBossFightStart -= OnBossFightStart;
            GameEvents.OnGameResumed -= OnGameResumed;
            GameEvents.OnLevelUpChoiceComplete -= OnLevelUpChoiceComplete;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 场景切换处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (config == null) return;

            ExitBattleResultMode();
            ResetBGMPitch();
    
            // 停止循环音效
            StopLaserLoop();
            StopBossLoop();
    
            // 根据场景播放对应 BGM
            if (scene.name == mainMenuSceneName)
            {
                PlayBGM(config.mainMenuBGM);
        
                if (showDebugInfo)
                {
                    GameLogger.Log("[AudioManager] 切换到主菜单，播放主菜单BGM");
                }
            }
            else if (scene.name == battleSceneName)
            {
                // 【修复】重新订阅游戏事件（因为 GameEvents.ClearAllEvents() 会清除订阅）
                ResubscribeToGameEvents();
        
                PlayBGM(config.battleBGM);
        
                if (showDebugInfo)
                {
                    GameLogger.Log("[AudioManager] 切换到战斗场景，播放战斗BGM，重新订阅事件");
                }
            }
    
            if (showDebugInfo)
            {
                GameLogger.Log($"[AudioManager] 场景加载: {scene.name}");
            }
        }
        /// <summary>
        /// 重新订阅游戏事件（场景切换后调用）
        /// </summary>
        private void ResubscribeToGameEvents()
        {
            // 先取消订阅（防止重复订阅）
            UnsubscribeFromGameEvents();
            // 重新订阅
            SubscribeToGameEvents();
    
            if (showDebugInfo)
            {
                GameLogger.Log("[AudioManager] 游戏事件已重新订阅");
            }
        }
        /// <summary>
        /// 根据当前场景播放对应的BGM（首次启动时调用）
        /// </summary>
        private void PlayBGMForCurrentScene()
        {
            if (config == null)
            {
                GameLogger.LogWarning("[AudioManager] AudioConfig 未配置！");
                return;
            }
            
            string currentSceneName = SceneManager.GetActiveScene().name;
            // 【新增】调试日志 - 检查音频文件
            if (showDebugInfo || config.mainMenuBGM == null || config.battleBGM == null)
            {
                GameLogger.Log($"[AudioManager] AudioConfig 检查:");
                GameLogger.Log($"  - mainMenuBGM: {(config.mainMenuBGM != null ? config.mainMenuBGM.name : "❌ 未配置")}");
                GameLogger.Log($"  - battleBGM: {(config.battleBGM != null ? config.battleBGM.name : "❌ 未配置")}");
            }
            if (currentSceneName == mainMenuSceneName)
            {
                if (config.mainMenuBGM == null)
                {
                    GameLogger.LogError("[AudioManager] ❌ mainMenuBGM 为空！请在 AudioConfig 中配置主界面BGM");
                    return;
                }
                PlayBGM(config.mainMenuBGM);
            }
            else if (currentSceneName == battleSceneName)
            {
                if (config.battleBGM == null)
                {
                    GameLogger.LogError("[AudioManager] ❌ battleBGM 为空！请在 AudioConfig 中配置战斗BGM");
                    return;
                }
                PlayBGM(config.battleBGM);
            }
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[AudioManager] Start - 当前场景: {currentSceneName}, 播放对应BGM");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // BGM 控制
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放背景音乐（带淡入淡出）
        /// </summary>
        public void PlayBGM(AudioClip clip)
        {
            if (clip == null)
            {
                if (showDebugInfo)
                {
                    GameLogger.LogWarning("[AudioManager] 尝试播放空的BGM片段");
                }
                return;
            }
            
            // 如果是同一首BGM，不重复播放
            if (clip == currentBGM && bgmSource.isPlaying)
            {
                return;
            }
            
            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
            }
            
            bgmFadeCoroutine = StartCoroutine(FadeBGMCoroutine(clip));
        }
        
        private IEnumerator FadeBGMCoroutine(AudioClip newClip)
        {
            float fadeDuration = config != null ? config.bgmFadeDuration : 1f;
            float targetVolume = bgmVolume * (config != null ? config.bgmDefaultVolume : 0.5f);
            
            // 淡出当前 BGM
            if (bgmSource.isPlaying)
            {
                float startVolume = bgmSource.volume;
                float elapsed = 0f;
                
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeDuration);
                    yield return null;
                }
                
                bgmSource.Stop();
            }
            
            // 切换并淡入新 BGM
            currentBGM = newClip;
            bgmSource.clip = newClip;
            bgmSource.volume = 0f;
            bgmSource.Play();
            
            float fadeInElapsed = 0f;
            while (fadeInElapsed < fadeDuration)
            {
                fadeInElapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(0f, targetVolume, fadeInElapsed / fadeDuration);
                yield return null;
            }
            
            bgmSource.volume = targetVolume;
            bgmFadeCoroutine = null;
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[AudioManager] BGM 切换完成: {newClip.name}");
            }
        }
        
        /// <summary>
        /// 停止背景音乐
        /// </summary>
        public void StopBGM()
        {
            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }
            
            bgmSource.Stop();
            currentBGM = null;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // SFX 播放 - 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放一次性音效
        /// </summary>
        public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
        {
            if (!sfxEnabled) return;  // 【新增】音效关闭时不播放
            if (clip == null || sfxSource == null) return;

            PrepareSfxSource();
            
            sfxSource.PlayOneShot(clip, sfxVolume * volumeMultiplier);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[AudioManager] 播放音效: {clip.name}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // UI 音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放按钮点击音效
        /// </summary>
        public void PlayButtonClick()
        {
            if (config != null)
            {
                PlaySFX(config.buttonClick, config.uiDefaultVolume);
            }
        }
        
        /// <summary>
        /// 播放技能选择音效
        /// </summary>
        public void PlaySkillCardSelect()
        {
            if (config != null)
            {
                PlaySFX(config.skillCardSelect, config.uiDefaultVolume);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 激光循环音效（三种状态切换）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 开始激光音效（默认空射）
        /// 由 LaserController 在激光激活时调用
        /// </summary>
        public void StartLaserLoop()
        {
            if(showDebugInfo)
                GameLogger.Log($"[AudioManager] StartLaserLoop 被调用");

            if (battleSfxMuted)
                return;
    
            if (config == null)
            {
                GameLogger.LogError("[AudioManager] ❌ config 为空！");
                return;
            }
    
            if (config.laserIdle == null)
            {
                GameLogger.LogError("[AudioManager] ❌ laserIdle 音效未配置！");
                return;
            }
            
            isLaserActive = true;
            currentLaserHitType = LaserHitType.None;
            
            // 播放空射音效
            SwitchLaserClip(config.laserIdle);
            
            if (showDebugInfo)
            {
                GameLogger.Log("[AudioManager] 激光循环开始（空射）");
            }
        }
        
        /// <summary>
        /// 停止激光音效
        /// 由 LaserController 在激光停止时调用
        /// </summary>
        public void StopLaserLoop()
        {
            isLaserActive = false;
            currentLaserHitType = LaserHitType.None;
            currentLaserClip = null;
            
            if (laserSource != null && laserSource.isPlaying)
            {
                laserSource.Stop();
                
                if (showDebugInfo)
                {
                    GameLogger.Log("[AudioManager] 激光循环停止");
                }
            }
        }
        
        /// <summary>
        /// 更新激光命中类型（每帧由 LaserController 调用）
        /// 优先级: Metal > Burn > None
        /// </summary>
        /// <param name="hitType">本帧的最高优先级命中类型</param>
        public void UpdateLaserHitType(LaserHitType hitType)
        {
            if (!isLaserActive || config == null) return;
            
            // 只有类型变化时才切换音效
            if (hitType == currentLaserHitType) return;
            
            currentLaserHitType = hitType;
            
            // 根据类型切换音效
            AudioClip targetClip = null;
            switch (hitType)
            {
                case LaserHitType.Metal:
                    targetClip = config.laserMetal;
                    break;
                case LaserHitType.Burn:
                    targetClip = config.laserBurn;
                    break;
                case LaserHitType.None:
                default:
                    targetClip = config.laserIdle;
                    break;
            }
            
            SwitchLaserClip(targetClip);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[AudioManager] 激光音效切换: {hitType}");
            }
        }
        
        /// <summary>
        /// 立即切换激光音效片段
        /// 【修复】即使是相同音效，如果没有在播放，也要重新播放
        /// </summary>
        private void SwitchLaserClip(AudioClip clip)
        {
            if (clip == null) return;
    
            // 【修复】检查是否是相同的音效
            if (clip == currentLaserClip)
            {
                // 即使是相同音效，如果没有在播放，也要重新播放
                if (laserSource != null && !laserSource.isPlaying)
                {
                    laserSource.Play();
            
                    if (showDebugInfo)
                    {
                        GameLogger.Log($"[AudioManager] 重新播放相同音效: {clip.name}");
                    }
                }
                return;
            }
    
            currentLaserClip = clip;
    
            // 立即切换（保持播放位置的随机性，避免每次都从头播放）
            laserSource.clip = clip;
            laserSource.volume = sfxVolume * config.laserLoopVolume;
            laserSource.time = Random.Range(0f, clip.length * 0.5f); // 随机起始位置
            laserSource.Play();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Boss 循环音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 开始 Boss 摩擦循环音效
        /// </summary>
        // public void StartBossFrictionLoop()
        // {
        //     if (config == null || config.bossFrictionLoop == null) return;
        //     
        //     bossLoopSource.clip = config.bossFrictionLoop;
        //     bossLoopSource.volume = sfxVolume * config.bossDefaultVolume;
        //     bossLoopSource.Play();
        // }
        
        /// <summary>
        /// 停止 Boss 循环音效
        /// </summary>
        public void StopBossLoop()
        {
            if (bossLoopSource != null && bossLoopSource.isPlaying)
            {
                bossLoopSource.Stop();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 怪物音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放怪物自爆音效
        /// </summary>
        public void PlayEnemyExplode()
        {
            if (config != null)
            {
                if (Time.unscaledTime - lastEnemyExplodeTime < EnemyExplodeDedupWindow)
                {
                    return;
                }

                lastEnemyExplodeTime = Time.unscaledTime;
                PlayBattleSFX(config.enemyExplode, config.enemyDefaultVolume);
            }
        }
        
        /// <summary>
        /// 播放怪物死亡音效
        /// </summary>
        public void PlayEnemyDeath()
        {
            if (config != null)
            {
                PlayBattleSFX(config.enemyDeath, config.enemyDefaultVolume);
            }
        }
        
        /// <summary>
        /// 播放分裂怪死亡爆开音效（可不配置，留空则静默）
        /// </summary>
        public void PlayEnemySplit()
        {
            if (config != null && config.enemySplit != null)
                PlayBattleSFX(config.enemySplit, config.enemyDefaultVolume);
        }

        /// <summary>
        /// 播放怪物冰冻音效
        /// </summary>
        public void PlayEnemyFreeze()
        {
            if (config != null)
            {
                PlayBattleSFX(config.enemyFreeze, config.enemyDefaultVolume);
            }
        }

        /// <summary>
        /// 播放激光击破投射物爆炸音效（通用）
        /// </summary>
        public void PlayProjectileExplode()
        {
            PlayEnemyExplode();
        }

        /// <summary>
        /// 播放炸弹怪死亡爆炸音效（LavaExploder / EliteLavaExploder）
        /// </summary>
        public void PlayGrenadeExplosion()
        {
            PlayEnemyExplode();
        }

        /// <summary>
        /// 播放熔浆液生成音效（LavaPuddle 生成时调用）
        /// </summary>
        public void PlayLavaPuddleSpawn()
        {
            if (config != null && config.lavaPuddleSpawn != null)
                PlayBattleSFX(config.lavaPuddleSpawn, config.enemyDefaultVolume);
        }

        /// <summary>
        /// 播放熔岩炮手喷吐音效
        /// </summary>
        public void PlayGunnerSpit()
        {
            if (config != null && config.gunnerSpit != null)
                PlayBattleSFX(config.gunnerSpit, config.enemyDefaultVolume);
        }

        /// <summary>
        /// 播放极寒催化者死亡冷气释放特效音效
        /// </summary>
        public void PlayCatalystBurst()
        {
            if (config != null && config.catalystBurst != null)
                PlayBattleSFX(config.catalystBurst, config.enemyDefaultVolume);
        }

        /// <summary>
        /// 播放冰墙生成落地音效
        /// </summary>
        public void PlayIceWallSpawn()
        {
            if (config != null && config.iceWallSpawn != null)
                PlayBattleSFX(config.iceWallSpawn, config.enemyDefaultVolume);
        }

        /// <summary>
        /// 播放冰盾破裂音效（Frost_EliteTank 冰盾归零时）
        /// </summary>
        public void PlayIceShieldBreak()
        {
            if (config != null && config.iceShieldBreak != null)
                PlayBattleSFX(config.iceShieldBreak, config.enemyDefaultVolume);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 玩家音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放护盾破碎音效
        /// </summary>
        public void PlayShieldBreak()
        {
            if (config != null)
            {
                PlayBattleSFX(config.shieldBreak, config.playerDefaultVolume);
            }
        }
        
        /// <summary>
        /// 播放护盾受击音效
        /// </summary>
        public void PlayShieldHit()
        {
            if (config != null && config.shieldHit != null)
            {
                PlayBattleSFX(config.shieldHit, config.playerDefaultVolume);
            }
        }
        
        /// <summary>
        /// 播放光棱塔受击音效
        /// </summary>
        public void PlayTowerHit()
        {
            if (config != null && config.towerHit != null)
            {
                PlayBattleSFX(config.towerHit, config.playerDefaultVolume);
            }
        }

        /// <summary>
        /// 播放光棱塔被冰冻音效（Boss 冰封技能命中时调用）
        /// </summary>
        public void PlayTowerFreeze()
        {
            if (config != null && config.towerFreeze != null)
                PlayBattleSFX(config.towerFreeze, config.playerDefaultVolume);
        }

        /// <summary>
        /// 播放光棱塔解冻音效（冻结解除时调用）
        /// </summary>
        public void PlayTowerUnfreeze()
        {
            if (config != null && config.towerUnfreeze != null)
                PlayBattleSFX(config.towerUnfreeze, config.playerDefaultVolume);
        }

        /// <summary>
        /// 播放 BOSS 来袭警告音效（由 BossWarningUI 在警告横幅显示时调用）
        /// </summary>
        public void PlayBossWarning()
        {
            if (config != null && config.bossWarning != null)
                PlayBattleSFX(config.bossWarning, config.bossDefaultVolume);
        }

        /// <summary>
        /// 播放低血量警告音效
        /// </summary>
        public void PlayLowHealthWarning()
        {
            if (config != null)
            {
                PlayBattleSFX(config.lowHealthWarning, config.playerDefaultVolume);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Boss 音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放 Boss 入场咆哮音效
        /// </summary>
        public void PlayBossRoar()
        {
            if (config != null)
            {
                PlayBattleSFX(config.bossRoar, config.bossDefaultVolume);
            }
        }

        
        /// <summary>
        /// 播放 Boss 死亡音效
        /// </summary>
        public void PlayBossDeath()
        {
            // if (config != null)
            // {
            //     PlaySFX(config.bossDeath, config.bossDefaultVolume);
            // }
        }
        
        /// <summary>
        /// 播放 Boss 破空音效（冲锋/突进）
        /// </summary>
        public void PlayBossDash()
        {
            if (config != null)
            {
                PlayBattleSFX(config.bossDash, config.bossDefaultVolume);
            }
        }
        /// <summary>
        /// 播放 Boss 预警音效
        /// </summary>
        public void PlayBossChargeWarning()
        {
            if (config != null)
            {
                PlayBattleSFX(config.bossChargeWarning, config.bossDefaultVolume);
            }
        }
        /// <summary>
        /// 播放 Boss 召唤小怪
        /// </summary>
        public void PlayBossSummon()
        {
            if (config != null)
            {
                PlayBattleSFX(config.bossSummon, config.bossDefaultVolume);
            }
        }
        /// <summary>
        /// 播放 Boss 喷吐污秽
        /// </summary>
        public void PlayBossSpit()
        {
            if (config != null)
            {
                PlayBattleSFX(config.bossSpit, config.bossDefaultVolume);
            }
        }
        
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 无人机音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        /// <summary>
        /// 播放无人机入场音效
        /// </summary>
        public void PlayDroneEnter()
        {
            if (config != null)
            {
                PlayBattleSFX(config.droneEnter, config.droneVolume);
            }
        }
        /// <summary>
        /// 播放无人机落地音效
        /// </summary>
        public void PlayDroneLand()
        {
            if (config != null)
            {
                PlayBattleSFX(config.droneLand, config.droneVolume);
            }
        }
        
        /// <summary>
        /// 播放无人机爆炸音效
        /// </summary>
        public void PlayDroneExplode()
        {
            if (config != null)
            {
                PlayBattleSFX(config.droneBoxBreak, config.droneVolume);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 经验球音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放经验球收集音效（带冷却）
        /// </summary>
        public void PlayXpOrbCollect()
        {
            if (config == null || config.xpOrbCollect == null) return;
            if (battleSfxMuted) return;
    
            float cooldown = config != null ? config.xpOrbCollectCooldown : 0.1f;
            if (Time.time - lastXpOrbCollectTime < cooldown) return;
    
            lastXpOrbCollectTime = Time.time;
            PlaySFX(config.xpOrbCollect, config.xpOrbVolume);
        }

        /// <summary>
        /// Plays the battle coin collect sound when a coin reaches the HUD.
        /// </summary>
        public void PlayCoinCollect()
        {
            if (config == null || config.coinCollect == null) return;
            if (battleSfxMuted) return;

            float cooldown = config.coinCollectCooldown;
            if (Time.time - lastCoinCollectTime < cooldown) return;

            lastCoinCollectTime = Time.time;
            PlaySFX(config.coinCollect, config.coinCollectVolume);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 游戏事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnLevelUp(int level)
        {
            // 【新增】停止激光循环音效（弹出三选一界面）
            StopLaserLoop();
            
            if (config != null)
            {
                PlaySFX(config.levelUp, config.uiDefaultVolume);
            }
        }
        
        private void OnGameVictory()
        {
            EnterBattleResultMode();
            
            if (config != null)
            {
                PlaySFX(config.victoryJingle, config.uiDefaultVolume);
            }
        }
        
        private void OnGameDefeat()
        {
            EnterBattleResultMode();
            
            if (config != null)
            {
                PlaySFX(config.defeatJingle, config.uiDefaultVolume);
            }
        }
        
        private void OnEnemyDied(EnemyType type, Vector3 pos, int xp, int coin)
        {
            // 敌人死亡时播放死亡音效
            // 注意：自爆音效在 EnemyBlob.Explode() 中单独调用
            if (type == EnemyType.LavaExploder || type == EnemyType.EliteLavaExploder)
                return;

            PlayEnemyDeath();
        }
        
        private void OnShieldBroken()
        {
            PlayShieldBreak();
        }
        
        private void OnLowHealthStart()
        {
            PlayLowHealthWarning();
        }
        
        private void OnBossFightStart()
        {
            // 切换到 Boss 战 BGM（警告音效由 BossWarningUI 更早播放）
            if (config != null && config.bossBGM != null)
            {
                PlayBGM(config.bossBGM);
            }
        }
        /// <summary>
        /// 游戏从暂停恢复时，重新启动激光音效
        /// </summary>
        private void OnGameResumed()
        {
            if (battleSfxMuted) return;

            // 恢复激光音效
            StartLaserLoop();
    
            if (showDebugInfo)
            {
                GameLogger.Log("[AudioManager] 游戏恢复，重启激光音效");
            }
        }

        /// <summary>
        /// 三选一选择完成后，重新启动激光音效
        /// </summary>
        private void OnLevelUpChoiceComplete()
        {
            if (battleSfxMuted) return;

            // 恢复激光音效
            StartLaserLoop();
    
            if (showDebugInfo)
            {
                GameLogger.Log("[AudioManager] 升级选择完成，重启激光音效");
            }
        }
        /// <summary>
        /// BGM 开关（开启时使用配置默认音量，关闭时静音）
        /// </summary>
        public bool BGMEnabled
        {
            get => bgmEnabled;
            set
            {
                bgmEnabled = value;
                PlayerPrefs.SetInt(PREF_BGM_ENABLED, value ? 1 : 0);
        
                // 更新实际音量
                if (bgmSource != null && config != null)
                {
                    bgmSource.volume = value ? config.bgmDefaultVolume : 0f;
                }
            }
        }

        /// <summary>
        /// SFX 开关（开启时使用配置默认音量，关闭时静音）
        /// </summary>
        public bool SFXEnabled
        {
            get => sfxEnabled;
            set
            {
                sfxEnabled = value;
                PlayerPrefs.SetInt(PREF_SFX_ENABLED, value ? 1 : 0);
        
                // 更新激光音量
                if (laserSource != null && config != null)
                {
                    laserSource.volume = value ? config.laserLoopVolume : 0f;
                }
            }
        }
        /// <summary>
        /// 设置BGM播放速度（pitch）
        /// </summary>
        /// <param name="pitch">播放速度（1.0=正常, >1=加速）</param>
        public void SetBGMPitch(float pitch)
        {
            if (bgmSource != null)
            {
                bgmSource.pitch = Mathf.Clamp(pitch, 0.5f, 2.0f);
        
                if (showDebugInfo)
                {
                    GameLogger.Log($"[AudioManager] BGM Pitch 设置为: {pitch:F2}");
                }
            }
        }

        /// <summary>
        /// 重置BGM播放速度为正常
        /// </summary>
        public void ResetBGMPitch()
        {
            SetBGMPitch(1.0f);
        }

        private void PrepareSfxSource()
        {
            if (sfxSource == null) return;

            sfxSource.pitch = 1f;
            sfxSource.panStereo = 0f;
            sfxSource.loop = false;
            sfxSource.mute = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.dopplerLevel = 0f;
        }

        private void PlayBattleSFX(AudioClip clip, float volumeMultiplier)
        {
            if (battleSfxMuted) return;
            PlaySFX(clip, volumeMultiplier);
        }

        public void EnterBattleResultMode()
        {
            battleSfxMuted = true;
            StopLaserLoop();
            StopBossLoop();
            StopSceneBattleAudioSources();
        }

        public void ExitBattleResultMode()
        {
            battleSfxMuted = false;
        }

        /// <summary>
        /// 战斗中弹出覆盖界面时暂停战斗音效。
        /// 使用计数器避免多个界面叠加时被提前恢复。
        /// </summary>
        public void PauseBattleAudioForOverlay()
        {
            battleOverlayMuteCount++;
            if (battleOverlayMuteCount > 1)
                return;

            PauseTrackedBattleAudioSource(laserSource);
            PauseTrackedBattleAudioSource(bossLoopSource);
            PauseSceneBattleAudioSources();
        }

        /// <summary>
        /// 战斗中关闭覆盖界面后恢复战斗音效。
        /// </summary>
        public void ResumeBattleAudioForOverlay()
        {
            if (battleOverlayMuteCount <= 0)
                return;

            battleOverlayMuteCount--;
            if (battleOverlayMuteCount > 0)
                return;

            for (int i = pausedBattleSources.Count - 1; i >= 0; i--)
            {
                AudioSource source = pausedBattleSources[i];
                if (source == null)
                    continue;

                source.UnPause();
            }

            pausedBattleSources.Clear();
        }

        private void StopSceneBattleAudioSources()
        {
            AudioSource[] allSources = FindObjectsOfType<AudioSource>(true);
            foreach (AudioSource source in allSources)
            {
                if (source == null) continue;
                if (source == bgmSource || source == sfxSource || source == laserSource || source == bossLoopSource)
                    continue;

                source.Stop();
            }
        }

        private void PauseSceneBattleAudioSources()
        {
            AudioSource[] allSources = FindObjectsOfType<AudioSource>(true);
            foreach (AudioSource source in allSources)
            {
                if (source == null)
                    continue;

                if (source == bgmSource || source == sfxSource)
                    continue;

                PauseTrackedBattleAudioSource(source);
            }
        }

        private void PauseTrackedBattleAudioSource(AudioSource source)
        {
            if (source == null || !source.isPlaying || pausedBattleSources.Contains(source))
                return;

            source.Pause();
            pausedBattleSources.Add(source);
        }
    }
}
