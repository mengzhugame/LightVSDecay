// ============================================================
// LaserController.cs (重构版 V2.0 - 模块化拆分)
// 文件位置: Assets/Scripts/Logic/Player/LaserController.cs
// 用途：激光控制器主体 - 协调各子模块
// 
// 子模块：
//   - LaserCritSystem.cs (暴击系统)
//   - LaserKnockbackHandler.cs (击退效果)
//   - LaserDamageCalculator.cs (伤害计算)
//   - LaserPenetrationHandler.cs (穿透逻辑)
//   - LaserFrostHandler.cs (寒气/冰冻效果)
//   - LaserAudioHandler.cs (音效管理)
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using LightVsDecay.Core;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Boss;
using LightVsDecay.Logic.Enemy;
using LightVsDecay.Logic.TacticalDrop;
using LightVsDecay.Audio;
using LightVsDecay.Core.Pool;
using LightVsDecay.Logic.Statistics;
using LightVsDecay.UI.FloatingText;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 副激光数据结构
    /// </summary>
    [System.Serializable]
    public class SubLaserData
    {
        public LaserBeam beam;
        public float angle;           // 相对主激光的角度偏移
        public float damageMultiplier; // 伤害倍率（如 0.3 = 30%）
        public float lengthMultiplier; // 长度倍率
    }
    
    /// <summary>
    /// 穿透命中信息（用于按距离排序）
    /// </summary>
    public struct PenetrationHitInfo
    {
        public Collider2D collider;
        public float distance;
        public Vector2 hitPoint;
    
        public PenetrationHitInfo(Collider2D c, float d, Vector2 p)
        {
            collider = c;
            distance = d;
            hitPoint = p;
        }
    }
    
    /// <summary>
    /// 激光控制器（重构版 V2.0）
    /// 负责：主激光 + 副激光管理、协调各子模块
    /// </summary>
    public class LaserController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("游戏设置")]
        [SerializeField] private GameSettings settings;
        
        [Header("组件引用")]
        [Tooltip("主激光（始终存在）")]
        [SerializeField] private LaserBeam mainLaserBeam;
        
        [Tooltip("激光挂载点（LaserPivot - 控制旋转）")]
        [SerializeField] private Transform laserPivot;
        
        [Tooltip("发射点")]
        [SerializeField] private Transform firePoint;
        
        [Tooltip("激光 Prefab（用于生成副激光）")]
        [SerializeField] private GameObject laserBeamPrefab;
        
        [Tooltip("VFX颜色同步组件")]
        [SerializeField] private LaserVFXColorSync vfxColorSync;
        
        [Header("检测设置")]
        [Tooltip("敌人检测层（Enemy Layer - 普通敌人 + Boss护甲）")]
        [SerializeField] private LayerMask enemyLayer;
        [Tooltip("弹跳敌人检测层（BouncingEnemy Layer - Drifter等）")]
        [SerializeField] private LayerMask bouncingEnemyLayer;
        [Tooltip("Boss核心检测层（EnemyEyes Layer）")]
        [SerializeField] private LayerMask bossEyesLayer;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 子模块
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private LaserCritSystem critSystem = new LaserCritSystem();
        private LaserKnockbackHandler knockbackHandler = new LaserKnockbackHandler();
        private LaserDamageCalculator damageCalculator = new LaserDamageCalculator();
        private LaserPenetrationHandler penetrationHandler = new LaserPenetrationHandler();
        private LaserFrostHandler frostHandler = new LaserFrostHandler();
        private LaserAudioHandler audioHandler = new LaserAudioHandler();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时配置缓存（从 GameSettings 读取）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float maxLaserLength = 20f;
        private float baseLaserWidth = 1.0f;
        private float tickRate = 0.1f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float tickTimer = 0f;
        private float skillWidthMultiplier = 1f;
        private float skillLengthMultiplier = 1f;
        
        // 大招倍率（由 OverloadManager 控制）
        private bool isOverloadActive = false;
        private float overloadWidthMultiplier = 1f;
        
        // 副激光管理
        private List<SubLaserData> subLasers = new List<SubLaserData>();
        private float subLaserDamageMultiplier = 0.3f;
        private float subLaserLengthMultiplier = 0.5f;
        
        // 颜色状态
        private Color mainLaserColor = Color.white;
        private bool hasCustomColor = false;
        
        // 伤害检测缓存
        private HashSet<EnemyBlob> hitEnemies = new HashSet<EnemyBlob>();
        private HashSet<BossHealth> hitBosses = new HashSet<BossHealth>();
        private Collider2D[] hitBuffer = new Collider2D[32];
        private HashSet<TacticalCrate> hitCrates = new HashSet<TacticalCrate>();
        
        // 合并检测层
        private LayerMask combinedDetectionLayer;
        
        // Layer 缓存
        private int enemyLayerIndex;
        private int bouncingEnemyLayerIndex;
        private int bossEyesLayerIndex;
        private int bossPollutionBallLayerIndex;
        
        // 连锁反应追踪
        private Dictionary<int, EnemyBlob> lastFrameHitEnemies = new Dictionary<int, EnemyBlob>();
        private Dictionary<int, EnemyBlob> currentFrameHitEnemies = new Dictionary<int, EnemyBlob>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 常量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private const float SUB_LASER_WIDTH_RATIO = 0.65f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性（委托给子模块）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public float CurrentLaserWidth => baseLaserWidth * skillWidthMultiplier * overloadWidthMultiplier;
        public float CurrentSubLaserWidth => CurrentLaserWidth * SUB_LASER_WIDTH_RATIO * overloadWidthMultiplier;
        public float CurrentKnockbackForce => knockbackHandler.CurrentKnockbackForce;
        public float CurrentCritRate => critSystem.CurrentCritRate;
        public float CritMultiplier => critSystem.TotalCritMultiplier;
        public int SubLaserCount => subLasers.Count;
        public float CurrentLaserLength => maxLaserLength * skillLengthMultiplier;
        public float CurrentDamagePerTick => damageCalculator.CurrentDamagePerTick;
        public float CurrentPanelDPS => damageCalculator.CurrentPanelDPS;
        public bool IsOverloadActive => isOverloadActive;
        /// <summary>获取面板 DPS（供 SkillEffectManager 计算爆炸伤害用）</summary>
        public float GetPanelDPS() => damageCalculator.CurrentPanelDPS;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Start()
        {
            InitializeFromSettings();
            CacheComponents();
            audioHandler.StartLaserAudio();
        }
        
        private void OnDestroy()
        {
            ClearAllSubLasers();
            audioHandler.StopLaserAudio();
        }
        
        private void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            {
                audioHandler.ResetToIdle();
                return;
            }
            
            audioHandler.EnsureAudioStarted();

            tickTimer += Time.deltaTime;
    
            if (tickTimer >= tickRate)
            {
                tickTimer = 0f;
                PerformDamageDetection();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void InitializeFromSettings()
        {
            if (settings != null)
            {
                // 初始化子模块
                damageCalculator.Initialize(settings.baseDPS, settings.tickRate, showDebugInfo);
                knockbackHandler.Initialize(settings.baseKnockbackForce, showDebugInfo);
                critSystem.Initialize(settings.baseCritRate, settings.critDamageMultiplier, showDebugInfo);
                
                // 本地缓存
                maxLaserLength = settings.maxLaserLength;
                baseLaserWidth = settings.baseLaserWidth;
                tickRate = settings.tickRate;
            }
            
            // 设置子模块调试模式
            penetrationHandler.SetDebugMode(showDebugInfo);
            frostHandler.SetDebugMode(showDebugInfo);
            audioHandler.SetDebugMode(showDebugInfo);
            
            // 初始化主激光
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetLaserWidth(CurrentLaserWidth);
                mainLaserBeam.SetMaxLength(CurrentLaserLength);
            }
            
            // 传递 LaserPivot 引用给 LaserBeam
            if (mainLaserBeam != null && laserPivot != null)
            {
                mainLaserBeam.SetLaserPivot(laserPivot);
                if (showDebugInfo)
                {
                    Debug.Log($"[LaserController] 已将 LaserPivot 传递给 LaserBeam: {laserPivot.name}");
                }
            }
            else
            {
                Debug.LogError($"[LaserController] 无法传递 LaserPivot! mainLaserBeam={mainLaserBeam != null}, laserPivot={laserPivot != null}");
            }
            
            // 验证 LaserPivot
            if (laserPivot == null && mainLaserBeam != null)
            {
                laserPivot = mainLaserBeam.transform.parent;
                Debug.LogWarning("[LaserController] LaserPivot 未设置，使用 mainLaserBeam 的父物体");
            }
            
            // 验证 FirePoint
            if (firePoint == null && mainLaserBeam != null)
            {
                firePoint = mainLaserBeam.transform;
            }
            
            // 合并检测层
            LayerMask pollutionBallLayer = 1 << LayerMask.NameToLayer("BossPollutionBall");
            combinedDetectionLayer = enemyLayer | bouncingEnemyLayer | pollutionBallLayer;
            
            // 缓存 Layer 索引
            enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            bouncingEnemyLayerIndex = LayerMask.NameToLayer(GameConstants.BOUNCING_ENEMY_LAYER);
            bossEyesLayerIndex = LayerMask.NameToLayer("EnemyEyes");
            bossPollutionBallLayerIndex = LayerMask.NameToLayer("BossPollutionBall");
            
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] 初始化完成 - DPS={damageCalculator.BaseDPS}, 暴击率={critSystem.CurrentCritRate:P0}");
            }
        }
        
        private void CacheComponents()
        {
            if (vfxColorSync == null && mainLaserBeam != null)
            {
                vfxColorSync = mainLaserBeam.GetComponent<LaserVFXColorSync>();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害判定（使用子模块）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void PerformDamageDetection()
        {
            mainLaserBeam?.ForceUpdatePath();
            foreach (var subLaser in subLasers)
                subLaser.beam?.ForceUpdatePath();
            
            hitEnemies.Clear();
            hitBosses.Clear();
            hitCrates.Clear();
            frostHandler.BeginTick();
            audioHandler.ResetFrameHitType();
            
            // 1. 主激光伤害检测
            DetectAndDamageEnemiesSegmented(mainLaserBeam, CurrentDamagePerTick, 1f, true);
            
            // 2. 副激光伤害检测
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    float subDamage = CurrentDamagePerTick * subLaser.damageMultiplier;
                    DetectAndDamageEnemiesSegmented(subLaser.beam, subDamage, subLaser.damageMultiplier, false);
                }
            }
            
            // 3. 应用寒气扩散效果
            ApplyFrostSpread();
            
            // 4. 通知 BossController 结束本tick推力累加
            FinalizeBossPushForce();
            
            // 5. 更新激光音效类型
            audioHandler.UpdateLaserAudioType();
            
            // 6. 处理连锁反应追踪
            ProcessChainLaserTracking();
        }
        
        private void FinalizeBossPushForce()
        {
            foreach (var bossHealth in hitBosses)
            {
                if (bossHealth != null)
                {
                    BossController bossController = bossHealth.GetComponent<BossController>();
                    if (bossController != null && bossController.IsPressing)
                    {
                        bossController.FinalizePushForceThisTick();
                    }
                }
            }
        }
        
        private void DetectAndDamageEnemiesSegmented(LaserBeam beam, float baseDamage, float knockbackMultiplier, bool isMainLaser)
        {
            if (beam == null) return;
    
            var segments = beam.GetLaserSegments();
            if (segments == null || segments.Count == 0) return;
    
            float width = beam.GetLaserWidth();
    
            foreach (var segment in segments)
            {
                DetectAndDamageInSegment(segment, width, baseDamage, knockbackMultiplier, isMainLaser);
            }
        }
        
        private void DetectAndDamageInSegment(LaserSegment segment, float width, float damage, float knockbackMultiplier, bool isMainLaser)
        {
            bool usePenetration = isMainLaser && penetrationHandler.IsEnabled;

            Vector2 segmentDir = segment.Direction;
            Vector2 segmentStart = segment.startPoint;
    
            float detectLength;
            Vector2 detectCenter;
    
            if (usePenetration)
            {
                detectLength = CurrentLaserLength;   // 使用含技能倍率的实际长度
                detectCenter = segmentStart + segmentDir * (detectLength / 2f);
            }
            else
            {
                detectLength = segment.length;
                detectCenter = (segment.startPoint + segment.endPoint) / 2f;
            }
    
            float angle = Mathf.Atan2(segmentDir.y, segmentDir.x) * Mathf.Rad2Deg - 90f;
            Vector2 boxSize = new Vector2(width, detectLength);

            int hitCount = Physics2D.OverlapBoxNonAlloc(detectCenter, boxSize, angle, hitBuffer, combinedDetectionLayer);

            if (usePenetration)
            {
                DetectPenetrationDamage(segment, width, damage, knockbackMultiplier, hitCount);
            }
            else
            {
                DetectNormalDamage(segment, width, damage, knockbackMultiplier, isMainLaser, hitCount);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 穿透伤害检测
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void DetectPenetrationDamage(LaserSegment segment, float width, float baseDamage, float knockbackMultiplier, int hitCount)
        {
            penetrationHandler.ClearHits();
            
            Vector2 segmentDir = segment.Direction;
            Vector2 segmentStart = segment.startPoint;
            HashSet<int> processedIds = new HashSet<int>();
            
            for (int i = 0; i < hitCount; i++)
            {
                var collider = hitBuffer[i];
                if (collider == null) continue;
                
                int colliderLayer = collider.gameObject.layer;
                
                // 污秽球处理（不参与穿透）
                if (colliderLayer == bossPollutionBallLayerIndex)
                {
                    BossPollutionProjectile ball = collider.GetComponent<BossPollutionProjectile>();
                    if (ball != null && !ball.IsDestroyed)
                    {
                        Vector2 pushDir = segmentDir;
                        float pushMagnitude = CurrentKnockbackForce * 2f;
                        ball.TakeDamage(baseDamage, pushDir * pushMagnitude);
                    }
                    continue;
                }
                
                // 宝箱处理（不参与穿透）
                TacticalCrate crate = collider.GetComponent<TacticalCrate>();
                if (crate != null)
                {
                    if (!hitCrates.Contains(crate))
                    {
                        hitCrates.Add(crate);
                        bool crateCrit = critSystem.RollCrit();
                        float finalDamage = critSystem.CalculateCritDamage(baseDamage, crateCrit);
                        crate.TakeDamage(finalDamage, Vector2.zero, crateCrit);
                        audioHandler.UpdateFrameHitType(LaserHitType.Metal);
                    }
                    continue;
                }
                
                float distance = Vector2.Distance(segmentStart, collider.transform.position);
                
                int id = collider.GetInstanceID();
                if (processedIds.Contains(id)) continue;
                processedIds.Add(id);
                
                penetrationHandler.AddHit(collider, distance, collider.transform.position);
            }
            
            if (penetrationHandler.HitCount == 0) return;
            
            penetrationHandler.SortByDistance();
            
            int maxPenetration = penetrationHandler.GetMaxPenetration();
            float currentDamage = baseDamage;
            int penetratedCount = 0;
            
            for (int i = 0; i < penetrationHandler.HitCount && penetratedCount < maxPenetration; i++)
            {
                var hitInfo = penetrationHandler.GetHit(i);
                var collider = hitInfo.collider;
                if (collider == null) continue;
                
                int colliderLayer = collider.gameObject.layer;
                
                // Boss眼睛检测
                if (colliderLayer == bossEyesLayerIndex)
                {
                    ProcessBossEyeHit(collider, segment, currentDamage, ref penetratedCount);
                    currentDamage = penetrationHandler.GetNextDamage(currentDamage);
                    continue;
                }
                
                // Boss身体检测
                if (colliderLayer == enemyLayerIndex)
                {
                    BossController bossController = collider.GetComponentInParent<BossController>();
                    if (bossController != null)
                    {
                        ProcessBossBodyHit(collider, bossController, segment, currentDamage, ref penetratedCount);
                        currentDamage = penetrationHandler.GetNextDamage(currentDamage);
                        continue;
                    }
                }
                
                // 普通敌人检测
                EnemyBlob enemy = collider.GetComponentInParent<EnemyBlob>();
                if (enemy == null || enemy.IsDead) continue;
                
                ProcessEnemyHit(enemy, segment, currentDamage, knockbackMultiplier, true, penetratedCount);
                
                penetratedCount++;
                currentDamage = penetrationHandler.GetNextDamage(currentDamage);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 普通伤害检测
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void DetectNormalDamage(LaserSegment segment, float width, float damage, float knockbackMultiplier, bool isMainLaser, int hitCount)
        {
            Vector2 segmentDir = segment.Direction;
            
            for (int i = 0; i < hitCount; i++)
            {
                var collider = hitBuffer[i];
                if (collider == null) continue;
                
                int colliderLayer = collider.gameObject.layer;
                
                // 污秽球检测
                if (colliderLayer == bossPollutionBallLayerIndex)
                {
                    BossPollutionProjectile ball = collider.GetComponent<BossPollutionProjectile>();
                    if (ball != null && !ball.IsDestroyed)
                    {
                        Vector2 pushDir = segmentDir;
                        float pushMagnitude = CurrentKnockbackForce * 2f;
                        ball.TakeDamage(damage, pushDir * pushMagnitude);
                    }
                    continue;
                }
                
                // Boss眼睛检测
                if (colliderLayer == bossEyesLayerIndex)
                {
                    int temp = 0;
                    ProcessBossEyeHit(collider, segment, damage, ref temp);
                    continue;
                }
                
                // 宝箱检测
                TacticalCrate crate = collider.GetComponent<TacticalCrate>();
                if (crate != null)
                {
                    if (!hitCrates.Contains(crate))
                    {
                        hitCrates.Add(crate);
                        bool crateCrit = critSystem.RollCrit();
                        float finalDamage = critSystem.CalculateCritDamage(damage, crateCrit);
                        crate.TakeDamage(finalDamage, Vector2.zero, crateCrit);
                        audioHandler.UpdateFrameHitType(LaserHitType.Metal);
                    }
                    continue;
                }
                
                // Boss身体检测
                if (colliderLayer == enemyLayerIndex)
                {
                    BossController bossController = collider.GetComponentInParent<BossController>();
                    if (bossController != null)
                    {
                        int temp = 0;
                        ProcessBossBodyHit(collider, bossController, segment, damage, ref temp);
                        continue;
                    }
                }
                
                // 普通敌人检测
                EnemyBlob enemy = collider.GetComponentInParent<EnemyBlob>();
                if (enemy == null) continue;
                
                ProcessEnemyHit(enemy, segment, damage, knockbackMultiplier, isMainLaser, 0);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 目标处理方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ProcessBossEyeHit(Collider2D collider, LaserSegment segment, float damage, ref int penetratedCount)
        {
            BossHealth bossHealth = collider.GetComponentInParent<BossHealth>();
            if (bossHealth == null) return;
            
            if (!hitBosses.Contains(bossHealth))
            {
                hitBosses.Add(bossHealth);
            }

            bool isCrit = critSystem.RollCrit();
            float bossDamage = damage;
            
            if (SkillEffectManager.Instance != null)
            {
                float bossBonus = SkillEffectManager.Instance.GetFocusBossDamageBonus();
                if (bossBonus > 0f)
                {
                    bossDamage *= (1f + bossBonus);
                }
            }
            
            if (penetrationHandler.TrueDamageToBoss)
            {
                bossHealth.TakeTrueCoreDamage(bossDamage, collider.transform.position, isCrit, critSystem.TotalCritMultiplier);
            }
            else
            {
                bossHealth.TakeCoreDamage(bossDamage, collider.transform.position, isCrit, critSystem.TotalCritMultiplier);
            }
            
            // Boss推力处理
            BossController bossController = bossHealth.GetComponent<BossController>();
            if (bossController != null && bossController.IsPressing)
            {
                int impactLevel = SkillEffectManager.Instance != null ? SkillEffectManager.Instance.GetImpactLevel() : 0;
                int wideLevel = SkillEffectManager.Instance != null ? SkillEffectManager.Instance.GetWideLevel() : 0;
                float pushMagnitude = bossController.CalculatePushForce(impactLevel, wideLevel);
                if (pushMagnitude > 0f)
                {
                    bossController.ApplyLaserPushForce(pushMagnitude);
                }
            }
            
            // Frost效果
            ApplyBossFrostEffect(bossController);
            
            audioHandler.UpdateFrameHitType(LaserHitType.Burn);
            penetratedCount++;
        }
        
        private void ProcessBossBodyHit(Collider2D collider, BossController bossController, LaserSegment segment, float damage, ref int penetratedCount)
        {
            BossHealth bossHealth = bossController.GetComponent<BossHealth>();
            BossEyeController eyeController = bossController.GetComponentInChildren<BossEyeController>();
            
            if (bossHealth == null) return;
            
            if (!hitBosses.Contains(bossHealth))
            {
                hitBosses.Add(bossHealth);
            }
            
            bool isCrit = critSystem.RollCrit();
            float bossDamage = damage;
            
            if (SkillEffectManager.Instance != null)
            {
                float bossBonus = SkillEffectManager.Instance.GetFocusBossDamageBonus();
                if (bossBonus > 0f)
                {
                    bossDamage *= (1f + bossBonus);
                }
            }
            
            bool isEyeOpen = eyeController != null && eyeController.IsOpen;
            
            if (penetrationHandler.TrueDamageToBoss)
            {
                if (isEyeOpen)
                {
                    bossHealth.TakeTrueCoreDamage(bossDamage, collider.transform.position, isCrit, critSystem.TotalCritMultiplier);
                }
                else
                {
                    bossHealth.TakeTrueBodyDamage(bossDamage, collider.transform.position, isCrit, critSystem.TotalCritMultiplier);
                }
            }
            else
            {
                if (isEyeOpen)
                {
                    bossHealth.TakeCoreDamage(bossDamage, collider.transform.position, isCrit, critSystem.TotalCritMultiplier);
                }
                else
                {
                    bossHealth.TakeBodyDamage(bossDamage, collider.transform.position, isCrit, critSystem.TotalCritMultiplier);
                }
            }
            
            // 推力处理
            if (bossController.IsPressing)
            {
                int impactLevel = SkillEffectManager.Instance != null ? SkillEffectManager.Instance.GetImpactLevel() : 0;
                int wideLevel = SkillEffectManager.Instance != null ? SkillEffectManager.Instance.GetWideLevel() : 0;
                float pushMagnitude = bossController.CalculatePushForce(impactLevel, wideLevel);
                if (pushMagnitude > 0f)
                {
                    bossController.ApplyLaserPushForce(pushMagnitude);
                }
            }
            
            // Frost效果
            ApplyBossFrostEffect(bossController);
            
            audioHandler.UpdateFrameHitType(LaserHitType.Burn);
            penetratedCount++;
        }
        
        private void ProcessEnemyHit(EnemyBlob enemy, LaserSegment segment, float damage, float knockbackMultiplier, bool isMainLaser, int penetratedCount)
        {
            if (enemy.IsDead) return;
            
            bool enemyCrit = critSystem.RollCrit();
            float baseDamage = critSystem.CalculateCritDamage(damage, enemyCrit);
            
            // 数据破碎加成
            float shatterBonus = 0f;
            if (SkillEffectManager.Instance != null)
            {
                shatterBonus = SkillEffectManager.Instance.GetShatterDamageBonus();
            }
            
            if (shatterBonus > 0f && enemy.IsImpaired)
            {
                baseDamage *= (1f + shatterBonus);
            }

            float finalDamage = baseDamage;
            
            // 击退
            Vector2 knockbackDir = segment.Direction;
            float knockbackMagnitude = CurrentKnockbackForce * knockbackMultiplier;
            if (penetratedCount > 0) knockbackMagnitude *= 0.5f;
            
            DamageSource damageSource = isMainLaser ? DamageSource.MainLaser : DamageSource.SubLaser;
            
            enemy.TakeDamage(finalDamage, knockbackDir * knockbackMagnitude, enemyCrit, false, damageSource, false);

            audioHandler.UpdateFrameHitType(LaserHitType.Burn);

            // Frost效果
            ApplyEnemyFrostEffect(enemy);
            // 【新增】Crit Lv5：暴击时附带微弱击退
            if (enemyCrit && critSystem.IsCritKnockbackEnabled && knockbackMagnitude > 0f)
            {
                Vector2 extraKnockback = knockbackDir * (knockbackMagnitude * (critSystem.CritKnockbackMultiplier - 1f));
                enemy.AddExtraKnockback(extraKnockback);
            }
            // 穿透特效
            if (penetratedCount > 0 && VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayLaserHit(enemy.transform.position);
            }

            RegisterChainHit(enemy, damage, isMainLaser);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Frost 效果（委托给 FrostHandler）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ApplyEnemyFrostEffect(EnemyBlob enemy)
        {
            if (SkillEffectManager.Instance == null) return;
            
            float slowPercent, duration;
            SkillEffectManager.Instance.GetFrostParams(out slowPercent, out duration);
            
            if (slowPercent <= 0f) return;
            
            float freezeThreshold, freezeDuration;
            SkillEffectManager.Instance.GetFrostFreezeParams(out freezeThreshold, out freezeDuration);
            
            frostHandler.ApplyFrostEffect(enemy, slowPercent, duration, tickRate, freezeThreshold, freezeDuration);
        }
        
        private void ApplyBossFrostEffect(BossController bossController)
        {
            if (SkillEffectManager.Instance == null || bossController == null) return;
            
            int frostLevel = SkillEffectManager.Instance.GetFrostLevel();
            if (frostLevel < 1) return;
            
            float slowPercent, duration;
            SkillEffectManager.Instance.GetFrostParams(out slowPercent, out duration);
            
            bool enableFreeze = frostLevel >= 5;
            
            frostHandler.ApplyBossFrostEffect(bossController, slowPercent, duration, tickRate, enableFreeze);
        }
        
        private void ApplyFrostSpread()
        {
            if (SkillEffectManager.Instance == null) return;

            // 只有持有「寒霜蔓延」技能时才触发扩散
            if (!SkillEffectManager.Instance.IsFrostSpreadEnabled) return;

            float slowPercent, slowDuration;
            SkillEffectManager.Instance.GetFrostParams(out slowPercent, out slowDuration);
            if (slowPercent <= 0f) return;

            float spreadRadius, spreadSlowRatio;
            SkillEffectManager.Instance.GetFrostSpreadParams(out spreadRadius, out spreadSlowRatio);

            // 【V4.2 新增】传递 Lv5 扩散冰冻参数
            int frostSpreadLevel = SkillEffectManager.Instance.GetFrostSpreadLevel();
            float spreadFreezeRate = SkillEffectManager.Instance.GetFrostSpreadFreezeRate();
            float freezeThreshold, freezeDuration;
            SkillEffectManager.Instance.GetFrostFreezeParams(out freezeThreshold, out freezeDuration);

            frostHandler.ApplyFrostSpread(slowPercent, slowDuration, spreadRadius, spreadSlowRatio, 
                combinedDetectionLayer, frostSpreadLevel, spreadFreezeRate, 
                freezeThreshold, freezeDuration, tickRate);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 连锁反应
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void RegisterChainHit(EnemyBlob enemy, float damage, bool isMainLaser)
        {
            if (enemy == null || enemy.IsDead) return;
    
            int enemyId = enemy.GetInstanceID();
            currentFrameHitEnemies[enemyId] = enemy;
    
            if (ChainLightningManager.Instance != null && ChainLightningManager.Instance.IsEnabled)
            {
                ChainLightningManager.Instance.RegisterLaserHit(enemy, damage, isMainLaser);
            }
        }

        private void ProcessChainLaserTracking()
        {
            if (ChainLightningManager.Instance == null || !ChainLightningManager.Instance.IsEnabled)
            {
                lastFrameHitEnemies.Clear();
                return;
            }
    
            foreach (var kvp in lastFrameHitEnemies)
            {
                if (!currentFrameHitEnemies.ContainsKey(kvp.Key))
                {
                    ChainLightningManager.Instance.NotifyLaserLeft(kvp.Value);
                }
            }
    
            var temp = lastFrameHitEnemies;
            lastFrameHitEnemies = currentFrameHitEnemies;
            currentFrameHitEnemies = temp;
            currentFrameHitEnemies.Clear();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 副激光管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void CreateSubLaser(float angle, float damageMultiplier, float lengthMultiplier)
        {
            if (laserBeamPrefab == null || laserPivot == null)
            {
                Debug.LogError("[LaserController] Prefab 或 Pivot 未设置！");
                return;
            }
    
            GameObject subLaserObj = Instantiate(laserBeamPrefab, laserPivot);
            subLaserObj.name = $"LaserBeam_Sub_{subLasers.Count}";
            subLaserObj.transform.localPosition = Vector3.zero;
            subLaserObj.transform.localRotation = Quaternion.Euler(0, 0, angle);
    
            LaserBeam beam = subLaserObj.GetComponent<LaserBeam>();
            if (beam == null)
            {
                Debug.LogError($"[LaserController] 副激光 Prefab 缺少 LaserBeam 组件！");
                Destroy(subLaserObj);
                return;
            }
    
            beam.SetLaserPivot(subLaserObj.transform);
    
            float subLength = maxLaserLength * lengthMultiplier;
            beam.SetMaxLength(subLength);
            beam.SetLaserWidth(CurrentSubLaserWidth);
            
            if (hasCustomColor)
            {
                beam.SetColor(mainLaserColor);
            }
    
            subLasers.Add(new SubLaserData
            {
                beam = beam,
                angle = angle,
                damageMultiplier = damageMultiplier,
                lengthMultiplier = lengthMultiplier
            });
    
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] 创建副激光: 角度={angle}°, 伤害倍率={damageMultiplier:P0}, 长度={subLength:F1}");
            }
        }
        
        public void ClearAllSubLasers()
        {
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    Destroy(subLaser.beam.gameObject);
                }
            }
            subLasers.Clear();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 颜色控制
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void SetLaserColor(Color color)
        {
            mainLaserColor = color;
            hasCustomColor = true;
            
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetColor(color);
            }
            
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    subLaser.beam.SetColor(color);
                }
            }
            ChainLightningManager.Instance?.SetChainColor(color);
        }
        
        public void ResetLaserColor()
        {
            hasCustomColor = false;
    
            if (mainLaserBeam != null)
            {
                mainLaserBeam.ResetColor();
            }
    
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    subLaser.beam.ResetColor();
                }
            }
            ChainLightningManager.Instance?.ResetChainColor();
        }
        
        public void ResetVFXColor()
        {
            if (vfxColorSync != null)
            {
                vfxColorSync.ResetVFXColor();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能加成接口（委托给子模块）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void SetDamageMultiplier(float multiplier)
        {
            damageCalculator.SetDamageMultiplier(multiplier);
        }
        
        public void SetKnockbackMultiplier(float multiplier)
        {
            knockbackHandler.SetKnockbackMultiplier(multiplier);
        }
        
        public void SetWidthMultiplier(float multiplier)
        {
            if(showDebugInfo)
                Debug.Log($"[LaserController] SetWidthMultiplier 被调用: {multiplier:F2}x");
            skillWidthMultiplier = Mathf.Max(0.1f, multiplier);
            UpdateAllLaserWidths();
        }
        
        public void AddDamagePercent(float percent)
        {
            damageCalculator.AddDamagePercent(percent);
        }

        public void AddWidthPercent(float percent, float minPercent = 0f)
        {
            float newMultiplier = skillWidthMultiplier + percent;
    
            if (minPercent > 0f && newMultiplier < minPercent)
            {
                newMultiplier = minPercent;
            }
    
            skillWidthMultiplier = newMultiplier;
            UpdateAllLaserWidths();
        }
        
        public void AddDamageFlat(float value)
        {
            damageCalculator.AddDamageFlat(value);
        }

        public void AddLengthPercent(float percent, float minPercent = 0.8f)
        {
            float newMultiplier = skillLengthMultiplier + percent;
            if (newMultiplier < minPercent) newMultiplier = minPercent;
            skillLengthMultiplier = newMultiplier;

            if (mainLaserBeam != null)
                mainLaserBeam.SetMaxLength(CurrentLaserLength);

            // ✅ 新增：同步更新所有副激光长度
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                    subLaser.beam.SetMaxLength(CurrentLaserLength * subLaser.lengthMultiplier);
            }

            if (showDebugInfo)
                Debug.Log($"[LaserController] 激光长度倍率: {skillLengthMultiplier:P0}, 当前长度: {CurrentLaserLength:F1}");
        }
        
        public void ResetDropBonuses()
        {
            damageCalculator.ResetDropBonuses();
            skillLengthMultiplier = 1f;
    
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetMaxLength(CurrentLaserLength);
            }
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                    subLaser.beam.SetMaxLength(CurrentLaserLength * subLaser.lengthMultiplier);
            }
            if (showDebugInfo)
            {
                Debug.Log("[LaserController] 空投加成已重置");
            }
        }
        
        private void UpdateAllLaserWidths()
        {
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetLaserWidth(CurrentLaserWidth);
            }
            
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    subLaser.beam.SetLaserWidth(CurrentSubLaserWidth);
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 暴击接口（委托给 CritSystem）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void AddCritRateBonus(float bonus)
        {
            critSystem.AddCritRateBonus(bonus);
        }
        
        public void ResetCritRateBonus()
        {
            critSystem.ResetCritRateBonus();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置读取接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        #region 配置读取接口
        
        public void SetPrismLevelFromConfig(int level, int splitCount, float splitDamageMultiplier, float splitLength)
        {
            ClearAllSubLasers();
            
            if (level <= 0 || splitCount <= 0)
            {
                if (showDebugInfo) Debug.Log("[LaserController] Prism 等级为 0 或分裂数为 0，无副激光");
                return;
            }
            
            float[] angles = CalculatePrismAngles(splitCount);
            float lengthMultiplier = splitLength / maxLaserLength;
            
            subLaserDamageMultiplier = splitDamageMultiplier;
            subLaserLengthMultiplier = lengthMultiplier;
            
            foreach (float angle in angles)
            {
                CreateSubLaser(angle, splitDamageMultiplier, lengthMultiplier);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] Prism Lv.{level}: 分裂数={splitCount}, 伤害={splitDamageMultiplier:P0}, 长度={splitLength}");
            }
        }

        private float[] CalculatePrismAngles(int count)
        {
            float[] angles = new float[count];
            
            if (count <= 0) return angles;
            
            float maxAngle = Mathf.Min(15f + count * 5f, 50f);
            
            if (count == 1)
            {
                angles[0] = 0f;
            }
            else if (count == 2)
            {
                angles[0] = -maxAngle * 0.6f;
                angles[1] = maxAngle * 0.6f;
            }
            else
            {
                float step = (maxAngle * 2f) / (count - 1);
                for (int i = 0; i < count; i++)
                {
                    angles[i] = -maxAngle + step * i;
                }
            }
            
            return angles;
        }

        public void SetFocusDamageMultiplier(float damageBonus)
        {
            damageCalculator.SetDamageMultiplier(damageBonus);
    
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] Focus伤害设置: +{damageBonus:P0}");
            }
        }
        
        public void SetFocusPenetrationParams(int count, float decay, bool trueDamage)
        {
            penetrationHandler.SetPenetrationParams(count, decay, trueDamage);
        }

        public void SetCritLevelFromConfig(int level, float critRateBonus, float critDamageBonus, bool enableKnockback)
        {
            critSystem.SetCritLevelFromConfig(level, critRateBonus, critDamageBonus, enableKnockback);
        }

        public void SetVFXColor(Color color)
        {
            var vfxSync = mainLaserBeam?.GetComponent<LaserVFXColorSync>();
            if (vfxSync != null)
            {
                vfxSync.SetVFXColor(color);
            }
            
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    var subVfxSync = subLaser.beam.GetComponent<LaserVFXColorSync>();
                    if (subVfxSync != null)
                    {
                        subVfxSync.SetVFXColor(color);
                    }
                }
            }
        }
        
        public void SetOverloadActive(bool active, float damageMultiplier, float widthMultiplier)
        {
            isOverloadActive = active;
            overloadWidthMultiplier = active ? widthMultiplier : 1f;
            damageCalculator.SetOverloadActive(active, damageMultiplier);
    
            UpdateAllLaserWidths();
    
            if (showDebugInfo)
            {
                if (active)
                {
                    Debug.Log($"[LaserController] ⚡ 大招激活！伤害×{damageMultiplier}, 宽度×{widthMultiplier}");
                }
                else
                {
                    Debug.Log("[LaserController] 大招结束，倍率恢复");
                }
            }
        }
        
        #endregion

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDrawGizmosSelected()
        {
            if (firePoint == null) return;
            
            DrawLaserGizmo(firePoint, maxLaserLength, CurrentLaserWidth, Color.green);
            
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    float subLength = maxLaserLength * subLaser.lengthMultiplier;
                    DrawLaserGizmo(subLaser.beam.transform, subLength, CurrentSubLaserWidth, Color.cyan);
                }
            }
        }
        
        private void DrawLaserGizmo(Transform origin, float length, float width, Color color)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
            
            Vector3 center = origin.position + origin.up * (length * 0.5f);
            Vector3 size = new Vector3(width, length, 0.1f);
            
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, origin.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.matrix = oldMatrix;
        }
    }
}
