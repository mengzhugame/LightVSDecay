// ============================================================
// LaserController.cs (重构版 - 支持多激光 + Boss穿透 + 暴击)
// 文件位置: Assets/Scripts/Logic/Player/LaserController.cs
// 用途：激光伤害判定和击退 - 支持 Prism 分裂、Focus 聚能、Boss穿透、暴击
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Boss;
using LightVsDecay.Logic.Enemy;

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
        
        [Tooltip("Boss核心检测层（EnemyEyes Layer）")]
        [SerializeField] private LayerMask bossEyesLayer;
        
        [Header("暴击设置")]
        [Tooltip("基础暴击率（0-1）")]
        [SerializeField] private float baseCritRate = 0.1f; // 10%
        
        [Tooltip("暴击伤害倍率")]
        [SerializeField] private float critDamageMultiplier = 2.0f; // 200%
        
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
        
        // 合并检测层（自动计算）
        private LayerMask combinedDetectionLayer;
        
        // Layer 缓存
        private int enemyLayerIndex;
        private int bossEyesLayerIndex;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 常量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>副激光宽度倍率（相对主激光）</summary>
        private const float SUB_LASER_WIDTH_RATIO = 0.65f;
        
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
        
        /// <summary>大招伤害倍率</summary>
        public float UltDamageMultiplier => isUltMode ? ultDamageMultiplier : 1f;
        
        /// <summary>大招击退倍率</summary>
        public float UltKnockbackMultiplier => isUltMode ? (ultKnockbackMultiplier * 2f) : 1f;
        
        /// <summary>副激光数量</summary>
        public int SubLaserCount => subLasers.Count;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Start()
        {
            InitializeFromSettings();
            CacheComponents();
            SubscribeEvents();
        }
        
        private void OnDestroy()
        {
            UnsubscribeEvents();
            ClearAllSubLasers();
        }
        
        private void Update()
        {
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
            }
            
            // 初始化主激光
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetLaserWidth(CurrentLaserWidth);
                mainLaserBeam.SetMaxLength(maxLaserLength);
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
            combinedDetectionLayer = enemyLayer | bossEyesLayer;
            
            // 缓存 Layer 索引
            enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            bossEyesLayerIndex = LayerMask.NameToLayer("EnemyEyes");
            
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] 检测层初始化 - Enemy: {enemyLayer.value}, BossEyes: {bossEyesLayer.value}");
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
        
        private void SubscribeEvents()
        {
            GameEvents.OnUltReady += OnUltReady;
            GameEvents.OnUltUsed += OnUltUsed;
        }
        
        private void UnsubscribeEvents()
        {
            GameEvents.OnUltReady -= OnUltReady;
            GameEvents.OnUltUsed -= OnUltUsed;
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
            
            // 1. 主激光伤害检测
            DetectAndDamageEnemies(mainLaserBeam, firePoint, CurrentDamagePerTick, 1f);
            
            // 2. 副激光伤害检测
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    float subDamage = CurrentDamagePerTick * subLaser.damageMultiplier;
                    DetectAndDamageEnemies(subLaser.beam, subLaser.beam.transform, subDamage, subLaser.damageMultiplier);
                }
            }
        }
        
        /// <summary>
        /// 检测并对敌人/Boss造成伤害（支持穿透 + 暴击）
        /// 使用 Layer 检测，不使用 Tag
        /// </summary>
        private void DetectAndDamageEnemies(LaserBeam beam, Transform origin, float damage, float knockbackMultiplier)
        {
            if (beam == null || origin == null) return;
            
            float length = beam.GetMaxLength();
            float width = beam.GetLaserWidth();
            
            Vector2 boxCenter = (Vector2)origin.position + (Vector2)origin.up * (length * 0.5f);
            Vector2 boxSize = new Vector2(width, length);
            float angle = origin.eulerAngles.z;
            
            // 使用合并的检测层（Enemy + BossEyes）
            int hitCount = Physics2D.OverlapBoxNonAlloc(boxCenter, boxSize, angle, hitBuffer, combinedDetectionLayer);
            
            for (int i = 0; i < hitCount; i++)
            {
                var collider = hitBuffer[i];
                if (collider == null) continue;
                
                int colliderLayer = collider.gameObject.layer;
                
                // ═══════════════════════════════════════════════════
                // Boss 核心检测（EnemyEyes Layer）
                // 只有 Boss 眼睛在这个层有 Collider
                // 【重构】实现完整的角力物理系统
                // ═══════════════════════════════════════════════════
                if (colliderLayer == bossEyesLayerIndex)
                {
                    BossHealth bossHealth = collider.GetComponentInParent<BossHealth>();
                    if (bossHealth != null && !hitBosses.Contains(bossHealth))
                    {
                        // 判定暴击
                        bool isCrit = RollCrit();
                        
                        // 核心伤害 - 200% 弱点伤害，可叠加暴击
                        bossHealth.TakeCoreDamage(damage, collider.transform.position, isCrit, critDamageMultiplier);
                        
                        // ══════════════════════════════════════════════
                        // 【角力系统】对 Boss 施加推力 + 打断判定
                        // ══════════════════════════════════════════════
                        BossController bossController = bossHealth.GetComponent<BossController>();
                        if (bossController != null)
                        {
                            // 获取技能等级和大招状态
                            int impactLevel = SkillEffectManager.Instance != null 
                                ? SkillEffectManager.Instance.GetImpactLevel() 
                                : 0;
                            
                            // === 情况1: 蓄力阶段 - 尝试打断 ===
                            if (bossController.IsInTelegraphPhase)
                            {
                                // 检查是否可以打断（Impact Lv.4+ 或 大招）
                                bool canInterrupt = SkillEffectManager.Instance != null
                                    ? SkillEffectManager.Instance.CanInterruptBossCharge(isUltMode)
                                    : (impactLevel >= 4 || isUltMode);
                                
                                if (canInterrupt)
                                {
                                    bossController.InterruptCharge();
                                    
                                    if (showDebugInfo)
                                    {
                                        Debug.Log("[LaserController] 🛑 打断 BOSS 蓄力！");
                                    }
                                }
                            }
                            // 【新增】霸体状态提示
                            else if (bossController.IsSuperArmor)
                            {
                                // 霸体中，无法打断，但可以输出伤害
                                if (showDebugInfo)
                                {
                                    Debug.Log("[LaserController] 🛡️ BOSS 霸体中，无法打断！");
                                }
                            }
                            // === 情况2: 冲撞阶段 - 施加推力（角力核心） ===
                            else if (bossController.IsCharging)
                            {
                                // 计算推力
                                float pushMagnitude = bossController.CalculatePushForce(impactLevel, isUltMode);
                                
                                // 推力方向（向上，与冲撞方向相反）
                                Vector2 pushDirection = Vector2.up;
                                Vector2 pushForce = pushDirection * pushMagnitude;
                                
                                // 应用推力
                                bossController.ApplyLaserPushForce(pushForce);
                                
                                if (showDebugInfo)
                                {
                                    Debug.Log($"[LaserController] ⚡ 对冲撞中的 BOSS 施加推力: {pushMagnitude:F2} (Impact Lv.{impactLevel}, Ult: {isUltMode})");
                                }
                            }
                        }
                        
                        hitBosses.Add(bossHealth);
                        
                        if (showDebugInfo)
                        {
                            string critStr = isCrit ? " [暴击!]" : "";
                            Debug.Log($"[LaserController] Boss 核心命中{critStr}! 基础伤害: {damage:F1}");
                        }
                    }
                    continue;
                }
                
                // ═══════════════════════════════════════════════════
                // Enemy Layer 检测（普通敌人 + Boss护甲）
                // ═══════════════════════════════════════════════════
                if (colliderLayer == enemyLayerIndex)
                {
                    // 先检查是否是 Boss 护甲
                    BossHealth bossHealth = collider.GetComponent<BossHealth>();
                    if (bossHealth != null && !hitBosses.Contains(bossHealth))
                    {
                        // 判定暴击
                        bool isCrit = RollCrit();
                        
                        // 护甲伤害 - 30% 伤害，可叠加暴击
                        bossHealth.TakeArmorDamage(damage, collider.transform.position, isCrit, critDamageMultiplier);
                        hitBosses.Add(bossHealth);
                        
                        if (showDebugInfo)
                        {
                            string critStr = isCrit ? " [暴击!]" : "";
                            Debug.Log($"[LaserController] Boss 护甲命中{critStr}! 基础伤害: {damage:F1}");
                        }
                        continue;
                    }
                    
                    // 普通敌人检测
                    EnemyBlob enemy = collider.GetComponentInParent<EnemyBlob>();
                    if (enemy == null || hitEnemies.Contains(enemy)) continue;
                    
                    hitEnemies.Add(enemy);
                    
                    // 判定暴击
                    bool enemyCrit = RollCrit();
                    float finalDamage = enemyCrit ? damage * critDamageMultiplier : damage;
                    
                    Vector2 knockbackDir = (enemy.transform.position - origin.position).normalized;
                    Vector2 knockbackForce = knockbackDir * CurrentKnockbackForce * knockbackMultiplier;
                    
                    enemy.TakeDamage(finalDamage, knockbackForce, enemyCrit);
                    
                    // 应用 Frost 减速效果
                    ApplyFrostEffect(enemy);
                }
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
    
            // Lv.5 有 20% 概率完全冰冻
            if (SkillEffectManager.Instance.TryFrostFreeze())
            {
                enemy.ApplyFrostFreeze(1.0f);
            }
            else
            {
                enemy.ApplyFrostSlow(slowPercent, duration);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Prism 效果（副激光管理）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置 Prism 分裂效果
        /// </summary>
        public void SetPrismLevel(int level)
        {
            ClearAllSubLasers();
            
            if (level <= 0)
            {
                if (showDebugInfo) Debug.Log("[LaserController] Prism 等级为 0，无副激光");
                return;
            }
            
            float[] angles = GetPrismAngles(level);
            float damageMultiplier = GetPrismDamageMultiplier(level);
            float lengthMultiplier = GetPrismLengthMultiplier(level);
            
            subLaserDamageMultiplier = damageMultiplier;
            subLaserLengthMultiplier = lengthMultiplier;
            
            foreach (float angle in angles)
            {
                CreateSubLaser(angle, damageMultiplier, lengthMultiplier);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] Prism Lv.{level}: 副激光数量={angles.Length}");
            }
        }
        
        private float[] GetPrismAngles(int level)
        {
            switch (level)
            {
                case 1: return new float[] { -20f, 20f };
                case 2: return new float[] { -30f, -15f, 15f, 30f };
                case 3: return new float[] { -40f, -20f, 20f, 40f };
                case 4: return new float[] { -45f, -30f, -15f, 15f, 30f, 45f };
                case 5: return new float[] { -50f, -35f, -20f, 20f, 35f, 50f };
                default: return new float[0];
            }
        }
        
        private float GetPrismDamageMultiplier(int level)
        {
            switch (level)
            {
                case 1: return 0.30f;
                case 2: return 0.35f;
                case 3: return 0.40f;
                case 4: return 0.45f;
                case 5: return 0.60f;
                default: return 0.30f;
            }
        }
        
        private float GetPrismLengthMultiplier(int level)
        {
            switch (level)
            {
                case 1: return 10f / maxLaserLength;
                case 2: return 12f / maxLaserLength;
                case 3: return 14f / maxLaserLength;
                case 4: return 16f / maxLaserLength;
                case 5: return 18f / maxLaserLength;
                default: return 10f / maxLaserLength;
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
                Destroy(subLaserObj);
                return;
            }
            
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
        // Focus 效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public void SetFocusLevel(int level, Color skillColor)
        {
            if (level <= 0)
            {
                ResetFocusEffect();
                return;
            }
            
            float damageMultiplier = GetFocusDamageMultiplier(level);
            skillDamageMultiplier = damageMultiplier;
            SetLaserColor(skillColor);
            
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetLaserWidth(CurrentLaserWidth);
            }
        }
        
        public void SetFocusLevel(int level)
        {
            Color defaultFocusColor = new Color(3f, 0.3f, 0.2f, 1f);
            SetFocusLevel(level, defaultFocusColor);
        }
        
        private float GetFocusDamageMultiplier(int level)
        {
            switch (level)
            {
                case 1: return 1.50f;
                case 2: return 1.80f;
                case 3: return 2.20f;
                case 4: return 2.60f;
                case 5: return 3.50f;
                default: return 1.0f;
            }
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
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 大招模式
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnUltReady() { }
        private void OnUltUsed() { }
        
        public void ActivateUlt()
        {
            isUltMode = true;
            if (showDebugInfo) Debug.Log("[LaserController] 大招激活！");
        }
        
        public void DeactivateUlt()
        {
            isUltMode = false;
            if (showDebugInfo) Debug.Log("[LaserController] 大招结束");
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
        
        public void SetVFXColor(Color color)
        {
            if (vfxColorSync != null)
            {
                vfxColorSync.SetVFXColor(color);
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