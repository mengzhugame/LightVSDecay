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
        Burn = 1,   // 命中软体（灼烧）- 黑油/小怪/Boss眼睛
        Metal = 2   // 命中金属（装甲）- Tank/Boss装甲
    }
    
    /// <summary>
    /// 音频管理器
    /// 单例模式，跨场景持久化
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private static AudioManager _instance;
        public static AudioManager Instance => _instance;
        
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
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 单例设置
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 创建音频源
            CreateAudioSources();
            
            // 加载音量设置
            LoadVolumeSettings();
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
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void CreateAudioSources()
        {
            // BGM 音频源
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.priority = 0; // 最高优先级
            
            // SFX 音频源
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.priority = 128;
            
            // 激光循环音频源
            laserSource = gameObject.AddComponent<AudioSource>();
            laserSource.loop = true;
            laserSource.playOnAwake = false;
            laserSource.priority = 64;
            
            // Boss 循环音频源
            bossLoopSource = gameObject.AddComponent<AudioSource>();
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
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 场景切换处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (config == null) return;
            
            // 停止循环音效
            StopLaserLoop();
            StopBossLoop();
            
            // 根据场景播放对应 BGM
            if (scene.name == mainMenuSceneName)
            {
                PlayBGM(config.mainMenuBGM);
            }
            else if (scene.name == battleSceneName)
            {
                PlayBGM(config.battleBGM);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[AudioManager] 场景加载: {scene.name}");
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
            if (clip == null || clip == currentBGM) return;
            
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
            if (clip == null || sfxSource == null) return;
            
            sfxSource.PlayOneShot(clip, sfxVolume * volumeMultiplier);
            
            if (showDebugInfo)
            {
                Debug.Log($"[AudioManager] 播放音效: {clip.name}");
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
            if (config == null) return;
            
            isLaserActive = true;
            currentLaserHitType = LaserHitType.None;
            
            // 播放空射音效
            SwitchLaserClip(config.laserIdle);
            
            if (showDebugInfo)
            {
                Debug.Log("[AudioManager] 激光循环开始（空射）");
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
            
            if (laserSource.isPlaying)
            {
                laserSource.Stop();
                
                if (showDebugInfo)
                {
                    Debug.Log("[AudioManager] 激光循环停止");
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
                Debug.Log($"[AudioManager] 激光音效切换: {hitType}");
            }
        }
        
        /// <summary>
        /// 立即切换激光音效片段
        /// </summary>
        private void SwitchLaserClip(AudioClip clip)
        {
            if (clip == null || clip == currentLaserClip) return;
            
            currentLaserClip = clip;
            
            // 立即切换（保持播放位置的随机性，避免每次都从头播放）
            laserSource.clip = clip;
            laserSource.volume = sfxVolume * config.laserLoopVolume;
            laserSource.time = Random.Range(0f, clip.length * 0.5f); // 随机起始位置
            laserSource.Play();
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
                PlaySFX(config.enemyExplode, config.enemyDefaultVolume);
            }
        }
        
        /// <summary>
        /// 播放怪物死亡音效
        /// </summary>
        public void PlayEnemyDeath()
        {
            if (config != null)
            {
                PlaySFX(config.enemyDeath, config.enemyDefaultVolume);
            }
        }
        
        /// <summary>
        /// 播放怪物冰冻音效
        /// </summary>
        public void PlayEnemyFreeze()
        {
            if (config != null)
            {
                PlaySFX(config.enemyFreeze, config.enemyDefaultVolume);
            }
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
                PlaySFX(config.shieldBreak, config.playerDefaultVolume);
            }
        }
        
        /// <summary>
        /// 播放低血量警告音效
        /// </summary>
        public void PlayLowHealthWarning()
        {
            if (config != null)
            {
                PlaySFX(config.lowHealthWarning, config.playerDefaultVolume);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Boss 音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放 Boss 入场音效
        /// </summary>
        public void PlayBossEntrance()
        {
            if (config != null)
            {
                PlaySFX(config.bossEntrance, config.bossDefaultVolume);
            }
        }
        
        /// <summary>
        /// 播放野蛮冲撞预警音效
        /// </summary>
        public void PlayBossChargeWarning()
        {
            if (config != null)
            {
                PlaySFX(config.bossChargeWarning, config.bossDefaultVolume);
            }
        }
        
        /// <summary>
        /// 播放重力碾压预警音效
        /// </summary>
        public void PlayBossPressWarning()
        {
            if (config != null)
            {
                PlaySFX(config.bossPressWarning, config.bossDefaultVolume);
            }
        }
        
        /// <summary>
        /// 开始重力碾压过程循环音效
        /// </summary>
        public void StartBossPressLoop()
        {
            if (config == null || config.bossPressing == null) return;
            if (bossLoopSource.isPlaying) return;
            
            bossLoopSource.clip = config.bossPressing;
            bossLoopSource.volume = sfxVolume * config.bossDefaultVolume;
            bossLoopSource.Play();
        }
        
        /// <summary>
        /// 停止 Boss 循环音效
        /// </summary>
        public void StopBossLoop()
        {
            if (bossLoopSource.isPlaying)
            {
                bossLoopSource.Stop();
            }
        }
        
        /// <summary>
        /// 播放喷吐发射音效
        /// </summary>
        public void PlayBossSpit()
        {
            if (config != null)
            {
                PlaySFX(config.bossSpit, config.bossDefaultVolume);
            }
        }
        
        /// <summary>
        /// 播放 Boss 召唤小怪音效
        /// </summary>
        public void PlayBossSummon()
        {
            if (config != null)
            {
                PlaySFX(config.bossSummon, config.bossDefaultVolume);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 空投音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放无人机入场音效
        /// </summary>
        public void PlayDroneEnter()
        {
            if (config != null)
            {
                PlaySFX(config.droneEnter, config.droneVolume);
            }
        }

        /// <summary>
        /// 播放无人机落地音效
        /// </summary>
        public void PlayDroneLand()
        {
            if (config != null)
            {
                PlaySFX(config.droneLand, config.droneVolume);
            }
        }
        
        /// <summary>
        /// 播放箱子破碎音效
        /// </summary>
        public void PlayCrateBreak()
        {
            if (config != null)
            {
                PlaySFX(config.droneBoxBreak, config.droneVolume);
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
    
            // 冷却检查
            float cooldown = config.xpOrbCollectCooldown > 0 ? config.xpOrbCollectCooldown : 0.1f;
            if (Time.time - lastXpOrbCollectTime < cooldown) return;
    
            lastXpOrbCollectTime = Time.time;
            PlaySFX(config.xpOrbCollect, config.xpOrbVolume);
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 游戏事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnLevelUp(int level)
        {
            if (config != null)
            {
                PlaySFX(config.levelUp, config.uiDefaultVolume);
            }
        }
        
        private void OnGameVictory()
        {
            // 停止循环音效
            StopLaserLoop();
            StopBossLoop();
            
            if (config != null)
            {
                PlaySFX(config.victoryJingle, config.uiDefaultVolume);
            }
        }
        
        private void OnGameDefeat()
        {
            // 停止循环音效
            StopLaserLoop();
            StopBossLoop();
            
            if (config != null)
            {
                PlaySFX(config.defeatJingle, config.uiDefaultVolume);
            }
        }
        
        private void OnEnemyDied(EnemyType type, Vector3 pos, int xp, int coin)
        {
            // 敌人死亡时播放死亡音效
            // 注意：自爆音效在 EnemyBlob.Explode() 中单独调用
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
            PlayBossEntrance();
            
            // 可选：切换到 Boss 战 BGM
            if (config != null && config.bossBGM != null)
            {
                PlayBGM(config.bossBGM);
            }
        }
    }
}