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
        // 技能加成（主激光）
        private float skillDamageMultiplier = 1f;
        private float skillKnockbackMultiplier = 1f;
        private float skillWidthMultiplier = 1f;
        private float flatDamageBonus = 0f;  // 固定值攻击力加成
        private float skillLengthMultiplier = 1f;  // 长度倍率
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
        // Crit 暴击相关【新增】
        private int critLevel = 0;                    // Crit 技能等级
        // ========== 寒气扩散系统 ==========
        /// <summary>本 Tick 被直接命中的敌人列表（用于寒气扩散）</summary>
        private List<EnemyBlob> directHitEnemiesThisTick = new List<EnemyBlob>();

        /// <summary>扩散检测用的 Collider 数组（避免 GC）</summary>
        private Collider2D[] spreadCheckBuffer = new Collider2D[50];

        /// <summary>已受到扩散减速的敌人 ID（本 Tick 去重）</summary>
        private HashSet<int> spreadAffectedEnemyIds = new HashSet<int>();

// ========== Frost 粒子特效间隔控制 ==========
        /// <summary>每个敌人上次播放粒子的时间</summary>
        private Dictionary<int, float> lastFrostVFXTime = new Dictionary<int, float>();

        /// <summary>粒子播放间隔（秒）</summary>
        private const float FROST_VFX_INTERVAL = 0.3f;

        /// <summary>寒气扩散半径</summary>
        private const float FROST_SPREAD_RADIUS = 1.5f;

        /// <summary>扩散减速比例（扩散效果 = 直接效果 * 此比例）</summary>
        private const float FROST_SPREAD_RATIO = 0.5f;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// Focus 穿透配置【新增】
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private int focusPenetrationCount = 0;        // 穿透数量（-1=无限）
        private float focusPenetrationDecay = 0.1f;   // 穿透衰减率
        private bool focusTrueDamageToBoss = false;   // Boss真实伤害

// 穿透检测缓存
        private List<PenetrationHitInfo> penetrationHits = new List<PenetrationHitInfo>(16);
        private RaycastHit2D[] penetrationRayHits = new RaycastHit2D[32];
        // ========== 连锁反应追踪 ==========
        /// <summary>上一帧被激光命中的敌人</summary>
        private Dictionary<int, EnemyBlob> lastFrameHitEnemies = new Dictionary<int, EnemyBlob>();

        /// <summary>当前帧被激光命中的敌人</summary>
        private Dictionary<int, EnemyBlob> currentFrameHitEnemies = new Dictionary<int, EnemyBlob>();
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
        /// <summary>当前击退力</summary>
        public float CurrentKnockbackForce => baseKnockbackForce * skillKnockbackMultiplier ;
        
        /// <summary>当前暴击率</summary>
        public float CurrentCritRate => Mathf.Clamp01(baseCritRate + critRateBonus);
        /// <summary>暴击倍率</summary>
        public float CritMultiplier => critDamageMultiplier;

        /// <summary>副激光数量</summary>
        public int SubLaserCount => subLasers.Count;
        /// <summary>当前激光长度</summary>
        public float CurrentLaserLength => maxLaserLength * skillLengthMultiplier;

        /// <summary>每 Tick 伤害（新公式）</summary>
        public float CurrentDamagePerTick => (baseDPS + flatDamageBonus) * tickRate * skillDamageMultiplier;

        /// <summary>当前面板DPS（新公式，用于爆炸伤害计算）</summary>
        public float CurrentPanelDPS => (baseDPS + flatDamageBonus) * skillDamageMultiplier;
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
                mainLaserBeam.SetMaxLength(CurrentLaserLength);
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
            if(showDebugInfo)
                Debug.Log($"[LaserController] StartLaserAudio 调用, AudioManager.Instance={AudioManager.Instance != null}, isLaserAudioStarted={isLaserAudioStarted}");
            if (AudioManager.Instance != null && !isLaserAudioStarted)
            {
                AudioManager.Instance.StartLaserLoop();
                isLaserAudioStarted = true;
                if(showDebugInfo)
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
            directHitEnemiesThisTick.Clear();  // 【新增】清空本 Tick 直接命中列表
            spreadAffectedEnemyIds.Clear();     // 【新增】清空扩散去重集合
            // 【新增】重置本帧命中类型
            frameHighestHitType = LaserHitType.None;
            // 1. 主激光伤害检测
            DetectAndDamageEnemiesSegmented(mainLaserBeam,  CurrentDamagePerTick, 1f,true);
            
            // 2. 副激光伤害检测
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    float subDamage = CurrentDamagePerTick * subLaser.damageMultiplier;
                    DetectAndDamageEnemiesSegmented(subLaser.beam,  subDamage, subLaser.damageMultiplier,false);
                }
            }
            // 【新增】3. 应用寒气扩散效果
            ApplyFrostSpread();
            // 【新增】通知 BossController 结束本tick推力累加
            FinalizeBossPushForce();
            // 【新增】更新激光音效类型
            UpdateLaserAudioType();
            // 【新增】处理连锁反应：通知离开的敌人
            ProcessChainLaserTracking();
        }
        /// <summary>
        /// 【新增】通知所有被命中的Boss结束推力累加
        /// </summary>
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
        /// <summary>
        /// 分段检测并对敌人造成伤害（支持反射段独立伤害）
        /// </summary>
        private void DetectAndDamageEnemiesSegmented(LaserBeam beam, float baseDamage, float knockbackMultiplier, bool isMainLaser)
        {
            if (beam == null) return;
    
            var segments = beam.GetLaserSegments();
            if (segments == null || segments.Count == 0) return;
    
            float width = beam.GetLaserWidth();
    
            foreach (var segment in segments)
            {
                float segmentDamage = baseDamage;
                // 【修改】传入激光类型信息
                bool segmentIsMainLaser = isMainLaser;
                // 对该段进行伤害检测
                DetectAndDamageInSegment(segment, width, segmentDamage, knockbackMultiplier,segmentIsMainLaser);
            }
        }
        /// <summary>
        /// 对单个激光段进行伤害检测 (V4.0 - 支持穿透)
        /// </summary>
        private void DetectAndDamageInSegment(LaserSegment segment, float width, float damage, float knockbackMultiplier, bool isMainLaser)
        {
            // 计算检测盒
            Vector2 segmentCenter = (segment.startPoint + segment.endPoint) / 2f;
            Vector2 segmentDir = segment.Direction;
            float angle = Mathf.Atan2(segmentDir.y, segmentDir.x) * Mathf.Rad2Deg - 90f;
            Vector2 boxSize = new Vector2(width, segment.length);
    
            // 使用合并的检测层
            int hitCount = Physics2D.OverlapBoxNonAlloc(segmentCenter, boxSize, angle, hitBuffer, combinedDetectionLayer);
    
            // 【新增】判断是否启用穿透（只有主激光非反射段才有穿透）
            bool usePenetration = isMainLaser  && focusPenetrationCount != 0;
    
            if (usePenetration)
            {
                // 【穿透模式】收集所有命中目标，按距离排序后处理
                DetectPenetrationDamage(segment, width, damage, knockbackMultiplier, hitCount);
            }
            else
            {
                // 【普通模式】原有逻辑
                DetectNormalDamage(segment, width, damage, knockbackMultiplier, isMainLaser, hitCount);
            }
        }
        /// <summary>
        /// 穿透伤害检测（Focus技能核心）V4.0
        /// 视觉效果：激光停在第一个敌人
        /// 伤害判定：按距离排序，依次对敌人造成衰减伤害
        /// </summary>
        private void DetectPenetrationDamage(LaserSegment segment, float width, float baseDamage, float knockbackMultiplier, int hitCount)
        {
            penetrationHits.Clear();
            
            Vector2 segmentDir = segment.Direction;
            Vector2 segmentStart = segment.startPoint;
            
            // 收集所有命中的目标并计算距离
            HashSet<int> processedIds = new HashSet<int>();
            
            for (int i = 0; i < hitCount; i++)
            {
                var collider = hitBuffer[i];
                if (collider == null) continue;
                
                int colliderLayer = collider.gameObject.layer;
                
                // ━━━ 污秽球处理（不参与穿透，直接处理）━━━
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
                
                // ━━━ 宝箱处理（不参与穿透）━━━
                TacticalCrate crate = collider.GetComponent<TacticalCrate>();
                if (crate != null)
                {
                    if (!hitCrates.Contains(crate))
                    {
                        hitCrates.Add(crate);
                        bool crateCrit = RollCrit();
                        float finalDamage = crateCrit ? baseDamage * critDamageMultiplier : baseDamage;
                        crate.TakeDamage(finalDamage, Vector2.zero, crateCrit);
                        UpdateFrameHitType(LaserHitType.Metal);
                    }
                    continue;
                }
                
                // 计算到激光起点的距离
                float distance = Vector2.Distance(segmentStart, collider.transform.position);
                
                // 去重（同一目标只记录一次）
                int id = collider.GetInstanceID();
                if (processedIds.Contains(id)) continue;
                processedIds.Add(id);
                
                penetrationHits.Add(new PenetrationHitInfo(collider, distance, collider.transform.position));
            }
            
            // 如果没有命中任何目标，返回
            if (penetrationHits.Count == 0) return;
            
            // 按距离排序（近到远）
            penetrationHits.Sort((a, b) => a.distance.CompareTo(b.distance));
            
            // 计算最大穿透数量
            int maxPenetration = focusPenetrationCount == -1 
                ? penetrationHits.Count  // 无限穿透
                : Mathf.Min(focusPenetrationCount, penetrationHits.Count);
            
            // 对每个目标应用衰减伤害
            float currentDamage = baseDamage;  // 第一个目标使用基础伤害（已含Focus加成）
            int penetratedCount = 0;
            
            for (int i = 0; i < penetrationHits.Count && penetratedCount < maxPenetration; i++)
            {
                var hitInfo = penetrationHits[i];
                var collider = hitInfo.collider;
                if (collider == null) continue;
                
                int colliderLayer = collider.gameObject.layer;
                
                // ━━━ Boss眼睛检测 ━━━
                if (colliderLayer == bossEyesLayerIndex)
                {
                    BossHealth bossHealth = collider.GetComponentInParent<BossHealth>();
                    if (bossHealth != null)
                    {
                        if (!hitBosses.Contains(bossHealth))
                        {
                            hitBosses.Add(bossHealth);
                        }

                        bool isCrit = RollCrit();
                        float bossDamage = currentDamage;
                        
                        // Boss易伤加成
                        if (SkillEffectManager.Instance != null)
                        {
                            float bossBonus = SkillEffectManager.Instance.GetFocusBossDamageBonus();
                            if (bossBonus > 0f)
                            {
                                bossDamage *= (1f + bossBonus);
                            }
                        }
                        
                        // 【新增】真实伤害判定
                        if (focusTrueDamageToBoss)
                        {
                            bossHealth.TakeTrueCoreDamage(bossDamage, collider.transform.position, isCrit, critDamageMultiplier);
                        }
                        else
                        {
                            bossHealth.TakeCoreDamage(bossDamage, collider.transform.position, isCrit, critDamageMultiplier);
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
                        // 【新增】Frost减速和冰冻累积（眼睛命中） Unstoppable
                        if (SkillEffectManager.Instance != null)
                        {
                            int frostLevel = SkillEffectManager.Instance.GetFrostLevel();
                            if (frostLevel >= 1)
                            {
                                float slowPercent, duration;
                                SkillEffectManager.Instance.GetFrostParams(out slowPercent, out duration);
                                
                                // 应用减速
                                bossController.ApplyFrostSlow(slowPercent, duration);
                                
                                // 播放 Frost 粒子特效
                                PlayBossFrostVFX(bossController);
                                
                                // LV5 冰冻累积
                                if (frostLevel >= 5)
                                {
                                    bossController.AddFrostExposureTime(tickRate);
                                }
                            }
                        }
                        UpdateFrameHitType(LaserHitType.Burn);
                        
                        // Boss眼睛也消耗穿透次数
                        penetratedCount++;
                        currentDamage *= (1f - focusPenetrationDecay);
                        
                        if (showDebugInfo)
                        {
                            Debug.Log($"[LaserController] 🔫 穿透#{penetratedCount}: Boss眼睛, 伤害={bossDamage:F1}, 真伤={focusTrueDamageToBoss}");
                        }
                    }
                    continue;
                }
                
                // ━━━ Boss身体检测（通过Enemy层）━━━
                if (colliderLayer == enemyLayerIndex)
                {
                    BossController bossController = collider.GetComponentInParent<BossController>();
                    if (bossController != null)
                    {
                        BossHealth bossHealth = bossController.GetComponent<BossHealth>();
                        BossEyeController eyeController = bossController.GetComponentInChildren<BossEyeController>();
                        
                        if (bossHealth != null)
                        {
                            if (!hitBosses.Contains(bossHealth))
                            {
                                hitBosses.Add(bossHealth);
                            }
                            
                            bool isCrit = RollCrit();
                            float bossDamage = currentDamage;
                            
                            // Boss易伤加成
                            if (SkillEffectManager.Instance != null)
                            {
                                float bossBonus = SkillEffectManager.Instance.GetFocusBossDamageBonus();
                                if (bossBonus > 0f)
                                {
                                    bossDamage *= (1f + bossBonus);
                                }
                            }
                            
                            bool isEyeOpen = eyeController != null && eyeController.IsOpen;
                            
                            // 【新增】真实伤害判定
                            if (focusTrueDamageToBoss)
                            {
                                if (isEyeOpen)
                                {
                                    bossHealth.TakeTrueCoreDamage(bossDamage, collider.transform.position, isCrit, critDamageMultiplier);
                                }
                                else
                                {
                                    bossHealth.TakeTrueBodyDamage(bossDamage, collider.transform.position, isCrit, critDamageMultiplier);
                                }
                            }
                            else
                            {
                                if (isEyeOpen)
                                {
                                    bossHealth.TakeCoreDamage(bossDamage, collider.transform.position, isCrit, critDamageMultiplier);
                                }
                                else
                                {
                                    bossHealth.TakeBodyDamage(bossDamage, collider.transform.position, isCrit, critDamageMultiplier);
                                }
                            }
                            
                            // Boss推力
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
                            
                            // Frost减速和冰冻累积
                            if (SkillEffectManager.Instance != null)
                            {
                                int frostLevel = SkillEffectManager.Instance.GetFrostLevel();
                                if (frostLevel >= 1)
                                {
                                    float slowPercent, duration;
                                    SkillEffectManager.Instance.GetFrostParams(out slowPercent, out duration);
                                    
                                    // 应用减速
                                    bossController.ApplyFrostSlow(slowPercent, duration);
                                    
                                    // 【新增】播放 Frost 粒子特效
                                    PlayBossFrostVFX(bossController);
                                    
                                    // 【新增】LV5 冰冻累积
                                    if (frostLevel >= 5)
                                    {
                                        bossController.AddFrostExposureTime(tickRate);
                                    }
                                }
                            }
                            
                            UpdateFrameHitType(LaserHitType.Burn);
                            
                            // Boss身体也消耗穿透次数，但可以继续穿透到后方敌人
                            penetratedCount++;
                            currentDamage *= (1f - focusPenetrationDecay);
                            
                            if (showDebugInfo)
                            {
                                string eyeState = isEyeOpen ? "睁眼" : "闭眼";
                                Debug.Log($"[LaserController] 🔫 穿透#{penetratedCount}: Boss身体({eyeState}), 伤害={bossDamage:F1}, 真伤={focusTrueDamageToBoss}");
                            }
                        }
                        continue;  // Boss处理完毕，继续检测后方敌人
                    }
                }
                
                // ━━━ 普通敌人检测 ━━━
                EnemyBlob enemy = collider.GetComponentInParent<EnemyBlob>();
                if (enemy == null || enemy.IsDead) continue;
                
                // 判定暴击
                bool enemyCrit = RollCrit();
                float finalDamage = enemyCrit ? currentDamage * critDamageMultiplier : currentDamage;
                
                // 数据破碎加成
                float shatterBonus = 0f;
                if (SkillEffectManager.Instance != null)
                {
                    shatterBonus = SkillEffectManager.Instance.GetShatterDamageBonus();
                }
                
                if (shatterBonus > 0f && enemy.IsImpaired)
                {
                    finalDamage *= (1f + shatterBonus);
                }
                
                // 碎冰系统
                float shatterMultiplier = 1f;
                bool enableExecution = false;
                bool isShatter = false;
                bool isExecution = false;
                
                if (SkillEffectManager.Instance != null)
                {
                    SkillEffectManager.Instance.GetShatterParams(out shatterMultiplier, out enableExecution);
                }
                
                if (shatterMultiplier > 1f && enemy.IsControlled)
                {
                    isShatter = true;
                    if (enableExecution && enemy.IsFullyFrozen && !enemy.IsEliteOrBoss)
                    {
                        isExecution = true;
                    }
                }
                
                // 计算最终伤害
                float enemyFinalDamage;
                if (isExecution)
                {
                    enemyFinalDamage = enemy.MaxHealth * 100f;
                    enemy.MarkAsExecuted();
                }
                else if (isShatter)
                {
                    enemyFinalDamage = finalDamage * shatterMultiplier;
                }
                else
                {
                    enemyFinalDamage = finalDamage;
                }
                
                // 击退（穿透目标击退减半）
                Vector2 knockbackDir = segment.Direction;
                float knockbackMagnitude = CurrentKnockbackForce * knockbackMultiplier;
                if (penetratedCount > 0) knockbackMagnitude *= 0.5f;
                
                DamageSource damageSource = DamageSource.MainLaser;
                
                // 造成伤害
                if (isExecution)
                {
                    enemy.TakeDamage(enemyFinalDamage, knockbackDir * knockbackMagnitude, enemyCrit, false, damageSource, false);
                    if (FloatingTextManager.Instance != null)
                    {
                        FloatingTextManager.Instance.ShowExecution(enemy.transform.position);
                    }
                }
                else
                {
                    enemy.TakeDamage(enemyFinalDamage, knockbackDir * knockbackMagnitude, enemyCrit, false, damageSource, isShatter);
                }

                UpdateFrameHitType(LaserHitType.Burn);
                
                // Frost效果
                ApplyFrostEffect(enemy);
                // 【新增】注册连锁反应命中
                RegisterChainHit(enemy, currentDamage, true); 
                
                // 更新穿透计数
                penetratedCount++;
                
                if (showDebugInfo)
                {
                    Debug.Log($"[LaserController] 🔫 穿透#{penetratedCount}: {enemy.name}, 距离={hitInfo.distance:F2}, 伤害={enemyFinalDamage:F1}");
                }
                
                // 计算下一个目标的伤害（应用衰减）
                currentDamage *= (1f - focusPenetrationDecay);
            }
            
            if (showDebugInfo && penetrationHits.Count > maxPenetration)
            {
                Debug.Log($"[LaserController] ⚠️ 穿透上限! 检测到={penetrationHits.Count}, 实际穿透={penetratedCount}/{maxPenetration}");
            }
        }

        /// <summary>
        /// 普通伤害检测（原有逻辑，用于非穿透情况）
        /// </summary>
        private void DetectNormalDamage(LaserSegment segment, float width, float damage, float knockbackMultiplier, bool isMainLaser, int hitCount)
        {
            Vector2 segmentDir = segment.Direction;
            
            for (int i = 0; i < hitCount; i++)
            {
                var collider = hitBuffer[i];
                if (collider == null) continue;
                
                int colliderLayer = collider.gameObject.layer;
                
                // ━━━ 污秽球检测 ━━━
                if (colliderLayer == bossPollutionBallLayerIndex)
                {
                    BossPollutionProjectile ball = collider.GetComponent<BossPollutionProjectile>();
                    if (ball != null && !ball.IsDestroyed)
                    {
                        Vector2 pushDir = segmentDir;
                        float pushMagnitude = CurrentKnockbackForce * 2f;
                        ball.TakeDamage(damage, pushDir * pushMagnitude);
                        
                        if (showDebugInfo)
                        {
                            Debug.Log($"[LaserController] 🟣 命中污秽球！伤害={damage:F0}, 推力={pushMagnitude:F0}");
                        }
                    }
                    continue;
                }
                
                // ━━━ Boss眼睛检测 ━━━
                if (colliderLayer == bossEyesLayerIndex)
                {
                    BossHealth bossHealth = collider.GetComponentInParent<BossHealth>();
                    if (bossHealth != null)
                    {
                        if (!hitBosses.Contains(bossHealth))
                        {
                            hitBosses.Add(bossHealth);
                        }

                        bool isCrit = RollCrit();
                        float bossDamage = damage;
                        
                        if (SkillEffectManager.Instance != null)
                        {
                            float bossBonus = SkillEffectManager.Instance.GetFocusBossDamageBonus();
                            if (bossBonus > 0f)
                            {
                                bossDamage *= (1f + bossBonus);
                            }
                        }
                        
                        bossHealth.TakeCoreDamage(bossDamage, collider.transform.position, isCrit, critDamageMultiplier);
                        
                        // 推力处理
                        if (isMainLaser)
                        {
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
                        }
                        
                        UpdateFrameHitType(LaserHitType.Burn);
                    }
                    continue;
                }
                
                // ━━━ 宝箱检测 ━━━
                TacticalCrate crate = collider.GetComponent<TacticalCrate>();
                if (crate != null)
                {
                    if (!hitCrates.Contains(crate))
                    {
                        hitCrates.Add(crate);
                        bool crateCrit = RollCrit();
                        float finalDamage = crateCrit ? damage * critDamageMultiplier : damage;
                        crate.TakeDamage(finalDamage, Vector2.zero, crateCrit);
                        UpdateFrameHitType(LaserHitType.Metal);
                    }
                    continue;
                }
                
                // ━━━ Boss身体检测 ━━━
                if (colliderLayer == enemyLayerIndex)
                {
                    BossController bossController = collider.GetComponentInParent<BossController>();
                    if (bossController != null)
                    {
                        BossHealth bossHealth = bossController.GetComponent<BossHealth>();
                        BossEyeController eyeController = bossController.GetComponentInChildren<BossEyeController>();
                        
                        if (bossHealth != null)
                        {
                            if (!hitBosses.Contains(bossHealth))
                            {
                                hitBosses.Add(bossHealth);
                            }
                            
                            bool isCrit = RollCrit();
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
                            
                            if (isEyeOpen)
                            {
                                bossHealth.TakeCoreDamage(bossDamage, collider.transform.position, isCrit, critDamageMultiplier);
                            }
                            else
                            {
                                bossHealth.TakeBodyDamage(bossDamage, collider.transform.position, isCrit, critDamageMultiplier);
                            }
                            
                            // Frost减速和冰冻累积
                            if (SkillEffectManager.Instance != null)
                            {
                                int frostLevel = SkillEffectManager.Instance.GetFrostLevel();
                                if (frostLevel >= 1)
                                {
                                    float slowPercent, duration;
                                    SkillEffectManager.Instance.GetFrostParams(out slowPercent, out duration);
                                    
                                    // 应用减速
                                    bossController.ApplyFrostSlow(slowPercent, duration);
                                    
                                    // 【新增】播放 Frost 粒子特效
                                    PlayBossFrostVFX(bossController);
                                    
                                    // 【新增】LV5 冰冻累积
                                    if (frostLevel >= 5)
                                    {
                                        bossController.AddFrostExposureTime(tickRate);
                                    }
                                }
                            }
                            
                            // 推力处理
                            if (isMainLaser  && bossController.IsPressing)
                            {
                                int impactLevel = SkillEffectManager.Instance != null ? SkillEffectManager.Instance.GetImpactLevel() : 0;
                                int wideLevel = SkillEffectManager.Instance != null ? SkillEffectManager.Instance.GetWideLevel() : 0;
                                float pushMagnitude = bossController.CalculatePushForce(impactLevel, wideLevel);
                                if (pushMagnitude > 0f)
                                {
                                    bossController.ApplyLaserPushForce(pushMagnitude);
                                }
                            }
                            // 【新增】Frost减速和冰冻累积（眼睛命中）
                            if (SkillEffectManager.Instance != null)
                            {
                                int frostLevel = SkillEffectManager.Instance.GetFrostLevel();
                                if (frostLevel >= 1)
                                {
                                    float slowPercent, duration;
                                    SkillEffectManager.Instance.GetFrostParams(out slowPercent, out duration);
                                
                                    // 应用减速
                                    bossController.ApplyFrostSlow(slowPercent, duration);
                                
                                    // 播放 Frost 粒子特效
                                    PlayBossFrostVFX(bossController);
                                
                                    // LV5 冰冻累积
                                    if (frostLevel >= 5)
                                    {
                                        bossController.AddFrostExposureTime(tickRate);
                                    }
                                }
                            }
                            UpdateFrameHitType(LaserHitType.Burn);
                        }
                        continue;
                    }
                }
                
                // ━━━ 普通敌人检测 ━━━
                EnemyBlob enemy = collider.GetComponentInParent<EnemyBlob>();
                if (enemy == null) continue;
                
                bool enemyCrit = RollCrit();
                float baseDamage = enemyCrit ? damage * critDamageMultiplier : damage;
                
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
                
                // 碎冰系统
                float shatterMultiplier = 1f;
                bool enableExecution = false;
                bool isShatter = false;
                bool isExecution = false;
                
                if (SkillEffectManager.Instance != null)
                {
                    SkillEffectManager.Instance.GetShatterParams(out shatterMultiplier, out enableExecution);
                }
                
                if (shatterMultiplier > 1f && enemy.IsControlled)
                {
                    isShatter = true;
                    if (enableExecution && enemy.IsFullyFrozen && !enemy.IsEliteOrBoss)
                    {
                        isExecution = true;
                    }
                }
                
                float finalEnemyDamage;
                if (isExecution)
                {
                    finalEnemyDamage = enemy.MaxHealth * 100f;
                    enemy.MarkAsExecuted();
                }
                else if (isShatter)
                {
                    finalEnemyDamage = baseDamage * shatterMultiplier;
                }
                else
                {
                    finalEnemyDamage = baseDamage;
                }
                
                Vector2 knockbackDir = segment.Direction;
                float knockbackMagnitude = CurrentKnockbackForce * knockbackMultiplier;
                
                DamageSource damageSource = isMainLaser ? DamageSource.MainLaser : DamageSource.SubLaser;
                
                if (isExecution)
                {
                    enemy.TakeDamage(finalEnemyDamage, knockbackDir * knockbackMagnitude, enemyCrit, false, damageSource, false);
                    if (FloatingTextManager.Instance != null)
                    {
                        FloatingTextManager.Instance.ShowExecution(enemy.transform.position);
                    }
                }
                else
                {
                    enemy.TakeDamage(finalEnemyDamage, knockbackDir * knockbackMagnitude, enemyCrit, false, damageSource, isShatter);
                }

                UpdateFrameHitType(LaserHitType.Burn);
                ApplyFrostEffect(enemy);
                // 【新增】注册连锁反应命中
                RegisterChainHit(enemy, damage, isMainLaser);
            }
        }

        /// <summary>
        /// 对敌人应用 Frost 减速效果（直接命中）
        /// </summary>
        private void ApplyFrostEffect(EnemyBlob enemy)
        {
            if (SkillEffectManager.Instance == null) return;
    
            float slowPercent, duration;
            SkillEffectManager.Instance.GetFrostParams(out slowPercent, out duration);
    
            if (slowPercent <= 0f) return;
    
            // 应用减速
            enemy.ApplyFrostSlow(slowPercent, duration);
    
            // 【新增】记录到直接命中列表（用于寒气扩散）
            if (!directHitEnemiesThisTick.Contains(enemy))
            {
                directHitEnemiesThisTick.Add(enemy);
            }
    
            // 【新增】播放 Frost 粒子特效（带间隔限制）
            PlayFrostVFX(enemy);
    
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
        /// <summary>
        /// 播放 Frost 粒子特效（带间隔限制）
        /// </summary>
        private void PlayFrostVFX(EnemyBlob enemy)
        {
            if (VFXPoolManager.Instance == null) return;
    
            int enemyId = enemy.GetInstanceID();
            float currentTime = Time.time;
    
            // 检查间隔
            if (lastFrostVFXTime.TryGetValue(enemyId, out float lastTime))
            {
                if (currentTime - lastTime < FROST_VFX_INTERVAL)
                {
                    return; // 还在冷却中
                }
            }
    
            // 播放粒子
            VFXPoolManager.Instance.PlayFrostHit(enemy.transform.position);
            lastFrostVFXTime[enemyId] = currentTime;
        }
        /// <summary>
        /// 播放 Boss Frost 粒子特效（带间隔限制）
        /// </summary>
        private void PlayBossFrostVFX(BossController bossController)
        {
            if (VFXPoolManager.Instance == null) return;
            if (bossController == null) return;
    
            int bossId = bossController.GetInstanceID();
            float currentTime = Time.time;
    
            // 检查间隔
            if (lastFrostVFXTime.TryGetValue(bossId, out float lastTime))
            {
                if (currentTime - lastTime < FROST_VFX_INTERVAL)
                {
                    return; // 还在冷却中
                }
            }
    
            // 播放粒子
            VFXPoolManager.Instance.PlayFrostHit(bossController.transform.position);
            lastFrostVFXTime[bossId] = currentTime;
        }
        /// <summary>
        /// 应用寒气扩散效果（每 Tick 结束时调用）
        /// </summary>
        private void ApplyFrostSpread()
        {
            if (SkillEffectManager.Instance == null) return;
            if (directHitEnemiesThisTick.Count == 0) return;
    
            float slowPercent, duration;
            SkillEffectManager.Instance.GetFrostParams(out slowPercent, out duration);
    
            if (slowPercent <= 0f) return;
    
            // 计算扩散减速效果（直接效果的 50%）
            float spreadSlowPercent = slowPercent * FROST_SPREAD_RATIO;
    
            // 遍历所有直接命中的敌人
            foreach (var sourceEnemy in directHitEnemiesThisTick)
            {
                if (sourceEnemy == null) continue;
        
                // 检测扩散范围内的敌人
                int hitCount = Physics2D.OverlapCircleNonAlloc(
                    sourceEnemy.transform.position,
                    FROST_SPREAD_RADIUS,
                    spreadCheckBuffer,
                    combinedDetectionLayer
                );
        
                for (int i = 0; i < hitCount; i++)
                {
                    Collider2D col = spreadCheckBuffer[i];
                    if (col == null) continue;
            
                    EnemyBlob targetEnemy = col.GetComponentInParent<EnemyBlob>();
                    if (targetEnemy == null) continue;
            
                    // 跳过自己
                    if (targetEnemy == sourceEnemy) continue;
            
                    // 跳过已被直接命中的敌人
                    if (directHitEnemiesThisTick.Contains(targetEnemy)) continue;
            
                    int targetId = targetEnemy.GetInstanceID();
            
                    // 去重：本 Tick 只受一次扩散效果
                    if (spreadAffectedEnemyIds.Contains(targetId)) continue;
                    spreadAffectedEnemyIds.Add(targetId);
            
                    // 应用扩散减速（不触发冰冻累积，不播放粒子）
                    targetEnemy.ApplyFrostSlow(spreadSlowPercent, duration);
                }
            }
    
            if (showDebugInfo && spreadAffectedEnemyIds.Count > 0)
            {
                Debug.Log($"[LaserController] ❄️ 寒气扩散影响了 {spreadAffectedEnemyIds.Count} 个敌人");
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
        /// <summary>
        /// 重置激光颜色为原始材质颜色
        /// </summary>
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
            if(showDebugInfo)
                Debug.Log($"[LaserController] SetWidthMultiplier 被调用: {multiplier:F2}x\n{UnityEngine.StackTraceUtility.ExtractStackTrace()}");
            skillWidthMultiplier = Mathf.Max(0.1f, multiplier);
            UpdateAllLaserWidths();
        }
        
        public void AddDamagePercent(float percent)
        {
            skillDamageMultiplier += percent;
        }

        public void AddWidthPercent(float percent, float minPercent = 0f)
        {
            float newMultiplier = skillWidthMultiplier + percent;
    
            // 下限保护（仅当 minPercent > 0 时生效）
            if (minPercent > 0f && newMultiplier < minPercent)
            {
                newMultiplier = minPercent;
            }
    
            skillWidthMultiplier = newMultiplier;
            UpdateAllLaserWidths();
        }
        /// <summary>
        /// 添加固定值攻击力
        /// </summary>
        public void AddDamageFlat(float value)
        {
            flatDamageBonus += value;
    
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] 固定攻击力 +{value}, 总加成: {flatDamageBonus}, 当前DPS: {CurrentPanelDPS:F0}");
            }
        }

        /// <summary>
        /// 添加激光长度（带下限保护）
        /// </summary>
        public void AddLengthPercent(float percent, float minPercent = 0.8f)
        {
            float newMultiplier = skillLengthMultiplier + percent;
    
            // 下限保护
            if (newMultiplier < minPercent)
            {
                newMultiplier = minPercent;
            }
    
            skillLengthMultiplier = newMultiplier;
    
            // 更新主激光长度
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetMaxLength(CurrentLaserLength);
            }
    
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] 激光长度倍率: {skillLengthMultiplier:P0}, 当前长度: {CurrentLaserLength:F1}");
            }
        }
        /// <summary>
        /// 重置所有空投加成（游戏开始时调用）
        /// </summary>
        public void ResetDropBonuses()
        {
            flatDamageBonus = 0f;
            skillLengthMultiplier = 1f;
            // 注意：skillDamageMultiplier 和 skillWidthMultiplier 由技能系统控制，不在这里重置
    
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetMaxLength(CurrentLaserLength);
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
        /// 设置 Focus 穿透参数（由 SkillEffectManager 调用）
        /// </summary>
        public void SetFocusPenetrationParams(int count, float decay, bool trueDamage)
        {
            focusPenetrationCount = count;
            focusPenetrationDecay = decay;
            focusTrueDamageToBoss = trueDamage;
    
            if (showDebugInfo)
            {
                string penetrationInfo = count == -1 ? "无限" : count.ToString();
                Debug.Log($"[LaserController] Focus穿透设置: 数量={penetrationInfo}, 衰减={decay:P0}, Boss真伤={trueDamage}");
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
        /// <summary>
        /// 注册连锁反应命中
        /// </summary>
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

        /// <summary>
        /// 处理连锁追踪：通知离开的敌人，交换帧缓存
        /// </summary>
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