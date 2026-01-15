// ============================================================
// LaserController.cs (重构版 - 支持多激光 + Boss穿透 + 暴击)
// 文件位置: Assets/Scripts/Logic/Player/LaserController.cs
// 用途：激光伤害判定和击退 - 支持 Prism 分裂、Focus 聚能、Boss穿透、暴击
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
    /// 激光控制器（重构版）
    /// 负责：主激光 + 副激光管理、伤害判定、击退效果
    /// 支持：Prism 分裂、Focus 聚能、Boss 穿透伤害、暴击系统
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
        // 运行时配置缓存（从 GameSettings 读取）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float baseDPS = 100f;
        private float tickRate = 0.1f;
        private float baseKnockbackForce = 10f;
        private float maxLaserLength = 20f;
        private float baseLaserWidth = 1.0f;

        private float baseCritRate = 0.1f;

        private float critDamageMultiplier = 2.0f;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float tickTimer = 0f;
        private bool isUltMode = false;
        private float ultDamageMultiplier = 2f;
        private float ultKnockbackMultiplier = 1.5f;
        
        // 技能加成（主激光）
        private float skillDamageMultiplier = 1f;
        private float skillKnockbackMultiplier = 1f;
        private float skillWidthMultiplier = 1f;
        
        // 暴击率加成（技能/事件可修改）
        private float critRateBonus = 0f;
        
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
        
        // 合并检测层（自动计算）
        private LayerMask combinedDetectionLayer;
        
        // Layer 缓存
        private int enemyLayerIndex;
        private int bouncingEnemyLayerIndex;  // 【新增】
        private int bossEyesLayerIndex;
        private int bossPollutionBallLayerIndex;  // 污秽球Layer
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Reflex 反射相关【新增】
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private bool reflexEnabled = false;           // 是否启用反射
        private int reflexLevel = 0;                  // Reflex 技能等级
        private float reflexDamageMultiplier = 0.5f;  // 反射段伤害倍率
        private float reflexLengthBonus = 0f;         // 反射长度加成

        // Crit 暴击相关【新增】
        private int critLevel = 0;                    // Crit 技能等级
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 常量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>副激光宽度倍率（相对主激光）</summary>
        private const float SUB_LASER_WIDTH_RATIO = 0.65f;
        // 激光音效状态
        private bool isLaserAudioStarted = false;
        private LaserHitType frameHighestHitType = LaserHitType.None;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前激光宽度</summary>
        public float CurrentLaserWidth => baseLaserWidth * skillWidthMultiplier;
        
        /// <summary>当前副激光宽度</summary>
        public float CurrentSubLaserWidth => CurrentLaserWidth * SUB_LASER_WIDTH_RATIO;
        
        /// <summary>每 Tick 伤害</summary>
        public float CurrentDamagePerTick => baseDPS * tickRate * skillDamageMultiplier * (isUltMode ? ultDamageMultiplier : 1f);
        
        /// <summary>当前击退力</summary>
        public float CurrentKnockbackForce => baseKnockbackForce * skillKnockbackMultiplier * (isUltMode ? ultKnockbackMultiplier : 1f);
        
        /// <summary>当前暴击率</summary>
        public float CurrentCritRate => Mathf.Clamp01(baseCritRate + critRateBonus);
        
        /// <summary>暴击倍率</summary>
        public float CritMultiplier => critDamageMultiplier;

        /// <summary>副激光数量</summary>
        public int SubLaserCount => subLasers.Count;
        
        /// <summary>反射段伤害倍率</summary>
        public float ReflexDamageMultiplier => reflexDamageMultiplier;

        /// <summary>是否启用反射</summary>
        public bool IsReflexEnabled => reflexEnabled;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Start()
        {
            InitializeFromSettings();
            CacheComponents();
            // 【新增】启动激光循环音效
            StartLaserAudio();
        }
        
        private void OnDestroy()
        {
            ClearAllSubLasers();
            // 【新增】停止激光循环音效
            StopLaserAudio();
        }
        
        private void Update()
        {
            // 【新增】非 Playing 状态时不执行伤害检测
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            {
                // 【新增】游戏暂停/结束时，重置为空射音效
                if (AudioManager.Instance != null && frameHighestHitType != LaserHitType.None)
                {
                    frameHighestHitType = LaserHitType.None;
                    AudioManager.Instance.UpdateLaserHitType(LaserHitType.None);
                }
                return;
            }
            // 【新增】确保激光音效已启动
            if (!isLaserAudioStarted)
            {
                StartLaserAudio();
            }

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
                baseDPS = settings.baseDPS;
                tickRate = settings.tickRate;
                baseKnockbackForce = settings.baseKnockbackForce;
                maxLaserLength = settings.maxLaserLength;
                baseLaserWidth = settings.baseLaserWidth;
                // 从 GameSettings 读取暴击配置
                baseCritRate = settings.baseCritRate;
                critDamageMultiplier = settings.critDamageMultiplier;
            }
            
            // 初始化主激光
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetLaserWidth(CurrentLaserWidth);
                mainLaserBeam.SetMaxLength(maxLaserLength);
            }
            // 传递 LaserPivot 引用给 LaserBeam（关键！）
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
            
            // 合并检测层 (Enemy + BossEyes)
            LayerMask pollutionBallLayer = 1 << LayerMask.NameToLayer("BossPollutionBall");
            combinedDetectionLayer = enemyLayer | bouncingEnemyLayer | pollutionBallLayer;
            
            // 缓存 Layer 索引
            enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            bouncingEnemyLayerIndex = LayerMask.NameToLayer(GameConstants.BOUNCING_ENEMY_LAYER);  // 【新增】
            bossEyesLayerIndex = LayerMask.NameToLayer("EnemyEyes");
            bossPollutionBallLayerIndex = LayerMask.NameToLayer("BossPollutionBall");
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] 检测层初始化 - Enemy: {enemyLayer.value}, BossEyes: {bossEyesLayer.value}");
                Debug.Log($"[LaserController] 检测层初始化 - Enemy: {enemyLayer.value}, BouncingEnemy: {bouncingEnemyLayer.value}, BossEyes: {bossEyesLayer.value}");
                Debug.Log($"[LaserController] 暴击率: {CurrentCritRate:P0}, 暴击倍率: {critDamageMultiplier:P0}");
            }
        }
        
        private void CacheComponents()
        {
            // 自动查找 VFX 颜色同步组件
            if (vfxColorSync == null && mainLaserBeam != null)
            {
                vfxColorSync = mainLaserBeam.GetComponent<LaserVFXColorSync>();
            }
        }
        /// <summary>
        /// 启动激光音效循环
        /// </summary>
        private void StartLaserAudio()
        {
            Debug.Log($"[LaserController] StartLaserAudio 调用, AudioManager.Instance={AudioManager.Instance != null}, isLaserAudioStarted={isLaserAudioStarted}");
            if (AudioManager.Instance != null && !isLaserAudioStarted)
            {
                AudioManager.Instance.StartLaserLoop();
                isLaserAudioStarted = true;
                Debug.Log("[LaserController] 激光音效已启动");
            }
        }
        /// <summary>
        /// 停止激光音效循环
        /// </summary>
        private void StopLaserAudio()
        {
            if (AudioManager.Instance != null && isLaserAudioStarted)
            {
                AudioManager.Instance.StopLaserLoop();
                isLaserAudioStarted = false;
            }
        }
        /// <summary>
        /// 更新激光音效类型
        /// </summary>
        private void UpdateLaserAudioType()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.UpdateLaserHitType(frameHighestHitType);
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 暴击判定
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 判定是否暴击
        /// </summary>
        private bool RollCrit()
        {
            return Random.value < CurrentCritRate;
        }
        
        /// <summary>
        /// 增加暴击率加成
        /// </summary>
        public void AddCritRateBonus(float bonus)
        {
            critRateBonus += bonus;
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] 暴击率加成 +{bonus:P0}, 当前暴击率: {CurrentCritRate:P0}");
            }
        }
        
        /// <summary>
        /// 重置暴击率加成
        /// </summary>
        public void ResetCritRateBonus()
        {
            critRateBonus = 0f;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害判定（支持多激光 + Boss穿透 + 暴击）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void PerformDamageDetection()
        {
            hitEnemies.Clear();
            hitBosses.Clear();
            hitCrates.Clear();  
            // 【新增】重置本帧命中类型
            frameHighestHitType = LaserHitType.None;
            // 1. 主激光伤害检测
            DetectAndDamageEnemiesSegmented(mainLaserBeam,  CurrentDamagePerTick, 1f);
            
            // 2. 副激光伤害检测
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    float subDamage = CurrentDamagePerTick * subLaser.damageMultiplier;
                    DetectAndDamageEnemiesSegmented(subLaser.beam,  subDamage, subLaser.damageMultiplier);
                }
            }
            // 【新增】更新激光音效类型
            UpdateLaserAudioType();
        }
        
        /// <summary>
        /// 分段检测并对敌人造成伤害（支持反射段独立伤害）
        /// </summary>
        private void DetectAndDamageEnemiesSegmented(LaserBeam beam, float baseDamage, float knockbackMultiplier)
        {
            if (beam == null) return;
    
            var segments = beam.GetLaserSegments();
            if (segments == null || segments.Count == 0) return;
    
            float width = beam.GetLaserWidth();
    
            foreach (var segment in segments)
            {
                // 计算该段的实际伤害
                float segmentDamage = baseDamage;
                if (segment.isReflected)
                {
                    // 反射段使用反射伤害倍率
                    segmentDamage = baseDamage * reflexDamageMultiplier;
                }
        
                // 对该段进行伤害检测
                DetectAndDamageInSegment(segment, width, segmentDamage, knockbackMultiplier);
            }
        }
        /// <summary>
        /// 对单个激光段进行伤害检测 (V3.0 重构)
        /// </summary>
        private void DetectAndDamageInSegment(LaserSegment segment, float width, float damage, float knockbackMultiplier)
        {
            // 计算检测盒
            Vector2 segmentCenter = (segment.startPoint + segment.endPoint) / 2f;
            Vector2 segmentDir = segment.Direction;
            float angle = Mathf.Atan2(segmentDir.y, segmentDir.x) * Mathf.Rad2Deg - 90f;
            Vector2 boxSize = new Vector2(width, segment.length);
            
            // 使用合并的检测层
            int hitCount = Physics2D.OverlapBoxNonAlloc(segmentCenter, boxSize, angle, hitBuffer, combinedDetectionLayer);
            
            for (int i = 0; i < hitCount; i++)
            {
                var collider = hitBuffer[i];
                if (collider == null) continue;
                
                int colliderLayer = collider.gameObject.layer;
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // V3.0: 污秽球检测（最高优先级）
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                if (colliderLayer == bossPollutionBallLayerIndex)
                {
                    BossPollutionProjectile ball = collider.GetComponent<BossPollutionProjectile>();
                    if (ball != null && !ball.IsDestroyed)
                    {
                        // 计算推力方向（激光方向）
                        Vector2 pushDir = segmentDir;
                        float pushMagnitude = CurrentKnockbackForce * 2f;  // 推力放大
                        
                        ball.TakeDamage(damage, pushDir * pushMagnitude);
                        
                        if (showDebugInfo)
                        {
                            Debug.Log($"[LaserController] 🟣 命中污秽球！伤害={damage:F0}, 推力={pushMagnitude:F0}");
                        }
                    }
                    continue;  // 污秽球处理完毕
                }
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // Boss 眼睛检测（弱点）
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                if (colliderLayer == bossEyesLayerIndex)
                {
                    BossHealth bossHealth = collider.GetComponentInParent<BossHealth>();
                    if (bossHealth != null && !hitBosses.Contains(bossHealth))
                    {
                        hitBosses.Add(bossHealth);
            
                        bool isCrit = RollCrit();
            
                        // BOSS 易伤加成（Focus Lv3+ = +20%）
                        float bossDamage = damage;
                        if (SkillEffectManager.Instance != null)
                        {
                            float bossBonus = SkillEffectManager.Instance.GetFocusBossDamageBonus();
                            if (bossBonus > 0f)
                            {
                                bossDamage *= (1f + bossBonus);
                    
                                if (showDebugInfo)
                                {
                                    Debug.Log($"[LaserController] BOSS易伤加成: +{bossBonus:P0}, 伤害: {damage:F1} → {bossDamage:F1}");
                                }
                            }
                        }
            
                        // V3.0: 调用眼睛伤害（自动计算连体Buff）
                        bossHealth.TakeCoreDamage(bossDamage, collider.transform.position, isCrit, critDamageMultiplier);
                        
                        // 角力系统
                        BossController bossController = bossHealth.GetComponent<BossController>();
                        if (bossController != null)
                        {
                            int impactLevel = SkillEffectManager.Instance != null 
                                ? SkillEffectManager.Instance.GetImpactLevel() : 0;
                            
                            // 情况1: 蓄力阶段 - 尝试打断
                            if (bossController.IsInChargeTelegraph)
                            {
                                bool canInterrupt = SkillEffectManager.Instance != null
                                    ? SkillEffectManager.Instance.CanInterruptBossCharge(isUltMode)
                                    : (impactLevel >= 5 || isUltMode);
                                
                                if (canInterrupt || isUltMode)
                                {
                                    bossController.InterruptCharge();
                                    
                                    if (showDebugInfo)
                                    {
                                        Debug.Log($"[LaserController] ⚡ 打断Boss蓄力！(Impact Lv.{impactLevel}, ULT={isUltMode})");
                                    }
                                }
                            }
                            
                            // 情况2: Press角力阶段 - 施加推力
                            if (bossController.IsPressing)
                            {
                                float pushMagnitude = bossController.CalculatePushForce(impactLevel, isUltMode);
                                Vector2 pushForce = Vector2.up * pushMagnitude;
                                bossController.ApplyLaserPushForce(pushForce);
                            }
                        }
                    }
                    continue;  // 眼睛处理完毕，不再检测身体
                }
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 宝箱检测
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                TacticalCrate crate = collider.GetComponent<TacticalCrate>();
                if (crate != null)
                {
                    if (!hitCrates.Contains(crate))
                    {
                        hitCrates.Add(crate);
                        
                        bool crateCrit = RollCrit();
                        float finalDamage = crateCrit ? damage * critDamageMultiplier : damage;
            
                        crate.TakeDamage(finalDamage, Vector2.zero, crateCrit);
                        // 【新增】宝箱使用金属音效
                        UpdateFrameHitType(LaserHitType.Metal);
                    }
                    continue;  // 宝箱处理完毕
                }
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // V3.1: Boss 检测（通过眼睛状态判定弱点）
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                if (colliderLayer == enemyLayerIndex)
                {
                    BossController bossController = collider.GetComponentInParent<BossController>();
                    if (bossController != null)
                    {
                        BossHealth bossHealth = bossController.GetComponent<BossHealth>();
                        BossEyeController eyeController = bossController.GetComponentInChildren<BossEyeController>();
                        
                        // 伤害处理
                        if (bossHealth != null && !hitBosses.Contains(bossHealth))
                        {
                            hitBosses.Add(bossHealth);
                            
                            bool isCrit = RollCrit();
                            
                            // BOSS 易伤加成
                            float bossDamage = damage;
                            if (SkillEffectManager.Instance != null)
                            {
                                float bossBonus = SkillEffectManager.Instance.GetFocusBossDamageBonus();
                                if (bossBonus > 0f)
                                {
                                    bossDamage *= (1f + bossBonus);
                                }
                            }
                            
                            // V3.1: 通过眼睛状态判定弱点
                            bool isEyeOpen = eyeController != null && eyeController.IsOpen;
                            
                            if (isEyeOpen)
                            {
                                // 眼睛睁开 → 弱点伤害（高伤害）
                                bossHealth.TakeCoreDamage(bossDamage, collider.transform.position, isCrit, critDamageMultiplier);
                                if (showDebugInfo)
                                {
                                    Debug.Log($"[LaserController] 👁️ 命中Boss弱点（眼睛睁开），伤害: {bossDamage:F1}");
                                }
                            }
                            else
                            {
                                // 眼睛闭合 → 甲壳伤害（80%减伤）
                                bossHealth.TakeBodyDamage(bossDamage, collider.transform.position, isCrit, critDamageMultiplier);
                                if (showDebugInfo)
                                {
                                    Debug.Log($"[LaserController] 🛡️ 命中Boss甲壳（眼睛闭合），伤害: {bossDamage:F1}");
                                }
                            }
                            UpdateFrameHitType(LaserHitType.Burn);
                        }
                        
                        // V3.1: 角力系统（无论眼睛状态都生效）
                        int impactLevel = SkillEffectManager.Instance != null 
                            ? SkillEffectManager.Instance.GetImpactLevel() : 0;
                        // 【新增】角力阶段使用 Metal 音效
                        if (bossController.IsPressing)
                        {
                            UpdateFrameHitType(LaserHitType.Metal);
                        }
                        else
                        {
                            // 非角力阶段
                            UpdateFrameHitType(LaserHitType.Burn);
                        }
                        // 情况1: 蓄力阶段 - 尝试打断
                        if (bossController.IsInChargeTelegraph)
                        {
                            bool canInterrupt = SkillEffectManager.Instance != null
                                ? SkillEffectManager.Instance.CanInterruptBossCharge(isUltMode)
                                : (impactLevel >= 5 || isUltMode);
                            
                            if (canInterrupt || isUltMode)
                            {
                                bossController.InterruptCharge();
                                
                                if (showDebugInfo)
                                {
                                    Debug.Log($"[LaserController] ⚡ 打断Boss蓄力！(Impact Lv.{impactLevel}, ULT={isUltMode})");
                                }
                            }
                        }
                        
                        // 情况2: Press角力阶段 - 施加推力
                        if (bossController.IsPressing)
                        {
                            float pushMagnitude = bossController.CalculatePushForce(impactLevel, isUltMode);
                            Vector2 pushForce = Vector2.up * pushMagnitude;
                            bossController.ApplyLaserPushForce(pushForce);
                        }
                        
                        continue;  // Boss处理完毕，跳过普通敌人检测
                    }
                }
                
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                // 普通敌人检测
                // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                EnemyBlob enemy = collider.GetComponentInParent<EnemyBlob>();
                if (enemy == null || hitEnemies.Contains(enemy)) continue;
                
                hitEnemies.Add(enemy);
                
                bool enemyCrit = RollCrit();
                float finalEnemyDamage = enemyCrit ? damage * critDamageMultiplier : damage;
                
                Vector2 knockbackDir = ((Vector2)enemy.transform.position - (Vector2)segment.startPoint).normalized;
                Vector2 knockbackForce = knockbackDir * CurrentKnockbackForce * knockbackMultiplier;
                
                enemy.TakeDamage(finalEnemyDamage, knockbackForce, enemyCrit);

                UpdateFrameHitType(LaserHitType.Burn);
                // Frost 效果
                ApplyFrostEffect(enemy);
            }
        }

        /// <summary>
        /// 对敌人应用 Frost 减速效果
        /// </summary>
        private void ApplyFrostEffect(EnemyBlob enemy)
        {
            if (SkillEffectManager.Instance == null) return;
    
            float slowPercent, duration;
            SkillEffectManager.Instance.GetFrostParams(out slowPercent, out duration);
    
            if (slowPercent <= 0f) return;
    
            // 应用减速
            enemy.ApplyFrostSlow(slowPercent, duration);
    
            // Lv.5 冰冻检测（基于累计照射时间）
            float freezeThreshold, freezeDuration;
            SkillEffectManager.Instance.GetFrostFreezeParams(out freezeThreshold, out freezeDuration);
    
            if (freezeThreshold > 0f && freezeDuration > 0f)
            {
                // 累加照射时间（每 Tick 调用一次）
                enemy.AddFrostExposureTime(tickRate);
        
                // 检查是否达到冰冻阈值
                if (enemy.GetFrostExposureTime() >= freezeThreshold)
                {
                    enemy.ApplyFrostFreeze(freezeDuration);
                    enemy.ResetFrostExposureTime();
            
                    if (showDebugInfo)
                    {
                        Debug.Log($"[LaserController] ❄️ 敌人冰冻! 照射时间达到 {freezeThreshold}s");
                    }
                }
            }
        }

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
    
            // 【关键】设置副激光的旋转参考节点为自身
            // 这样副激光会使用自己的 Transform（包含角度偏移）来计算激光方向
            beam.SetLaserPivot(subLaserObj.transform);
    
            float subLength = maxLaserLength * lengthMultiplier;
            beam.SetMaxLength(subLength);
            beam.SetLaserWidth(CurrentSubLaserWidth);
            // 【新增】同步反射状态
            if (reflexEnabled)
            {
                beam.SetReflectionEnabled(true);
            }
            // 同步颜色
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

        private void ResetFocusEffect()
        {
            skillDamageMultiplier = 1f;
            SetLaserColor(new Color(0f, 3f, 3f, 1f));
            hasCustomColor = false;
            
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetLaserWidth(CurrentLaserWidth);
            }
        }
        
        /// <summary>
        /// 更新本帧最高优先级的命中类型
        /// 优先级: Metal > Burn > None
        /// </summary>
        private void UpdateFrameHitType(LaserHitType hitType)
        {
            // 只保留最高优先级
            if ((int)hitType > (int)frameHighestHitType)
            {
                frameHighestHitType = hitType;
            }
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
        }

        public void ResetVFXColor()
        {
            if (vfxColorSync != null)
            {
                vfxColorSync.ResetVFXColor();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能加成接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void SetDamageMultiplier(float multiplier)
        {
            skillDamageMultiplier = Mathf.Max(0.1f, multiplier);
        }
        
        public void SetKnockbackMultiplier(float multiplier)
        {
            skillKnockbackMultiplier = Mathf.Max(0f, multiplier);
        }
        
        public void SetWidthMultiplier(float multiplier)
        {
            skillWidthMultiplier = Mathf.Max(0.1f, multiplier);
            UpdateAllLaserWidths();
        }
        
        public void AddDamagePercent(float percent)
        {
            skillDamageMultiplier += percent;
        }
        
        public void AddWidthPercent(float percent)
        {
            skillWidthMultiplier += percent;
            UpdateAllLaserWidths();
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
        // 【新增】从配置读取的技能接口
        // 添加到 LaserController.cs 的技能接口区域
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        #region 配置读取接口
        /// <summary>
        /// 从配置设置 Prism（折射棱镜）效果
        /// </summary>
        /// <param name="level">技能等级</param>
        /// <param name="splitCount">分裂数量</param>
        /// <param name="splitDamageMultiplier">分裂伤害倍率</param>
        /// <param name="splitLength">分裂长度</param>
        public void SetPrismLevelFromConfig(int level, int splitCount, float splitDamageMultiplier, float splitLength)
        {
            ClearAllSubLasers();
            
            if (level <= 0 || splitCount <= 0)
            {
                if (showDebugInfo) Debug.Log("[LaserController] Prism 等级为 0 或分裂数为 0，无副激光");
                return;
            }
            
            // 根据分裂数量计算角度
            float[] angles = CalculatePrismAngles(splitCount);
            
            // 计算长度倍率
            float lengthMultiplier = splitLength / maxLaserLength;
            
            subLaserDamageMultiplier = splitDamageMultiplier;
            subLaserLengthMultiplier = lengthMultiplier;
            
            foreach (float angle in angles)
            {
                CreateSubLaser(angle, splitDamageMultiplier, lengthMultiplier);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] Prism Lv.{level} (配置): 分裂数={splitCount}, 伤害={splitDamageMultiplier:P0}, 长度={splitLength}");
            }
        }

        /// <summary>
        /// 根据分裂数量计算均匀分布的角度
        /// </summary>
        private float[] CalculatePrismAngles(int count)
        {
            // 根据分裂数量生成对称的角度数组
            // 例如：2条 -> [-20, 20]，4条 -> [-30, -15, 15, 30]，6条 -> [-40, -25, -10, 10, 25, 40]
            
            float[] angles = new float[count];
            
            if (count <= 0) return angles;
            
            // 最大角度范围
            float maxAngle = Mathf.Min(15f + count * 5f, 50f); // 根据数量扩展角度范围
            
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
                // 均匀分布
                float step = (maxAngle * 2f) / (count - 1);
                for (int i = 0; i < count; i++)
                {
                    angles[i] = -maxAngle + step * i;
                }
            }
            
            return angles;
        }

        /// <summary>
        /// 从配置设置 Focus（聚能透镜）效果
        /// </summary>
        /// <param name="level">技能等级</param>
        /// <param name="damageMultiplier">伤害倍率</param>
        /// <param name="widthMultiplier">宽度倍率（仅 Lv1 生效）</param>
        /// <param name="laserColor">激光颜色</param>
        public void SetFocusLevelFromConfig(int level, float damageMultiplier, float widthMultiplier)
        {
            if (level <= 0)
            {
                ResetFocusEffect();
                return;
            }
    
            // 应用伤害倍率
            skillDamageMultiplier = damageMultiplier;
    
            // 仅在 Lv1 时应用宽度变化（变细）
            if (level == 1 && widthMultiplier < 1f)
            {
                skillWidthMultiplier *= widthMultiplier;
            }
    
            // 颜色由 SkillEffectManager.UpdateLaserColor() 统一处理
    
            // 更新激光宽度
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetLaserWidth(CurrentLaserWidth);
            }
    
            UpdateAllLaserWidths();
    
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] Focus Lv.{level} (配置): 伤害={damageMultiplier:P0}, 宽度倍率={widthMultiplier:F2}");
            }
        }

        /// <summary>
        /// 从配置设置 Reflex（反射透镜）效果
        /// </summary>
        /// <param name="level">技能等级</param>
        /// <param name="damageMultiplier">反射段伤害倍率</param>
        /// <param name="lengthBonus">激光长度加成</param>
        public void SetReflexLevelFromConfig(int level, float damageMultiplier, float lengthBonus)
        {
            reflexLevel = level;
            
            if (level <= 0)
            {
                // 关闭反射
                reflexEnabled = false;
                reflexDamageMultiplier = 0f;
                reflexLengthBonus = 0f;
                
                if (mainLaserBeam != null)
                {
                    mainLaserBeam.SetReflectionEnabled(false);
                    mainLaserBeam.SetMaxLength(maxLaserLength);
                }
                
                foreach (var subLaser in subLasers)
                {
                    if (subLaser.beam != null)
                    {
                        subLaser.beam.SetReflectionEnabled(false);
                        float subLength = maxLaserLength * subLaser.lengthMultiplier;
                        subLaser.beam.SetMaxLength(subLength);
                    }
                }
                return;
            }
            
            // 启用反射
            reflexEnabled = true;
            reflexDamageMultiplier = damageMultiplier;
            reflexLengthBonus = lengthBonus;
            
            // 计算新的激光长度
            float newLength = maxLaserLength * (1f + lengthBonus);
            
            // 应用到主激光
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetReflectionEnabled(true);
                mainLaserBeam.SetMaxLength(newLength);
            }
            
            // 应用到所有副激光
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    subLaser.beam.SetReflectionEnabled(true);
                    float subLength = newLength * subLaser.lengthMultiplier;
                    subLaser.beam.SetMaxLength(subLength);
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] Reflex Lv.{level} (配置): 反射伤害={damageMultiplier:P0}, 长度加成={lengthBonus:P0}");
            }
        }

        /// <summary>
        /// 从配置设置 Crit（致命暴击）等级
        /// </summary>
        /// <param name="level">技能等级</param>
        /// <param name="critBonus">暴击率加成</param>
        public void SetCritLevelFromConfig(int level, float critBonus)
        {
            critLevel = level;
            critRateBonus = critBonus;
            
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] Crit Lv.{level} (配置): 暴击率加成={critBonus:P0}, 总暴击率={CurrentCritRate:P0}");
            }
        }

        /// <summary>
        /// 设置 VFX 颜色（供 SkillEffectManager 调用）
        /// </summary>
        public void SetVFXColor(Color color)
        {
            // 如果有 LaserVFXColorSync 组件，通过它设置
            var vfxSync = mainLaserBeam?.GetComponent<LaserVFXColorSync>();
            if (vfxSync != null)
            {
                vfxSync.SetVFXColor(color);
            }
            
            // 同步到副激光
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
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 260));
            GUILayout.Label("=== Laser Stats ===");
            GUILayout.Label($"DPS: {baseDPS * skillDamageMultiplier:F1} {(isUltMode ? "(x2 ULT)" : "")}");
            GUILayout.Label($"Damage/Tick: {CurrentDamagePerTick:F1}");
            GUILayout.Label($"Knockback: {CurrentKnockbackForce:F1}");
            GUILayout.Label($"Width: {CurrentLaserWidth:F2}");
            GUILayout.Label($"Sub Lasers: {subLasers.Count}");
            GUILayout.Label($"--- Crit ---");
            GUILayout.Label($"Crit Rate: {CurrentCritRate:P0}");
            GUILayout.Label($"Crit Mult: {critDamageMultiplier:P0}");
            GUILayout.Label($"--- Status ---");
            GUILayout.Label($"Ult Mode: {isUltMode}");
            GUILayout.Label($"Custom Color: {hasCustomColor}");
            GUILayout.EndArea();
        }
#endif
    }
}