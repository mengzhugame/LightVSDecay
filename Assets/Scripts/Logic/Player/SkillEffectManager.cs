// ============================================================
// SkillEffectManager.cs
// 文件位置: Assets/Scripts/Logic/Player/SkillEffectManager.cs
// 用途：技能效果管理器 - 监听技能选择并应用效果
// 修改：Step 1 - 添加 SkillDatabase 引用，从配置读取参数
// ============================================================

using System.Collections.Generic;
using LightVsDecay.Audio;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Enemy;
using LightVsDecay.Logic.Statistics;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 技能效果管理器
    /// 职责：
    /// - 监听技能选择事件
    /// - 根据技能类型调用对应的效果实现
    /// - 管理 Prism/Focus/Impact/Frost/Power/Wide/Reflex/Crit 等效果
    /// - 【Step 1】从 SkillDatabase 配置读取参数
    /// </summary>
    public class SkillEffectManager : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 组件引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("组件引用")]
        [Tooltip("激光控制器")]
        [SerializeField] private LaserController laserController;
        
        [Tooltip("塔旋转控制器（用于 Adrenaline 加速）")]
        [SerializeField] private TurretController turretController;
        
        [Tooltip("塔生命管理器（用于 Repair 恢复）")]
        [SerializeField] private TurretHealth turretHealth;
        
        [Tooltip("护盾控制器（用于 Repair/Adrenaline 恢复）")]
        [SerializeField] private ShieldController shieldController;
        
        [Tooltip("VFX颜色同步组件（可选，用于 Frost 效果）")]
        [SerializeField] private LaserVFXColorSync vfxColorSync;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】技能数据库引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("技能数据库【新增】")]
        [Tooltip("技能配置数据库（通过 Inspector 拖拽）")]
        [SerializeField] private SkillDatabase skillDatabase;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例访问
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        public static SkillEffectManager Instance { get; private set; }
        /// <summary>
        /// Focus Lv5 死亡爆炸是否启用（供 EnemyBlob 检查）
        /// </summary>
        public bool IsFocusExplosionEnabled => focusLevel >= 5;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 常量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Wide 技能配置（保留旧逻辑作为回退）
        private const float WIDE_WIDTH_PER_LEVEL = 0.25f;
        private const float BASE_LASER_WIDTH = 0.5f;
        // Adrenaline 配置
        private const float ADRENALINE_DURATION = 20f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 技能等级
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private int prismLevel = 0;
        private int focusLevel = 0;
        private int impactLevel = 0;
        private int frostLevel = 0;
        private int powerLevel = 0;
        private int wideLevel = 0;
        private int critLevel = 0;
        private int shatterLevel = 0;  // 【新增】数据破碎等级
        private int chainLevel = 0;  // 连锁反应等级
        private int frostSpreadLevel = 0; // 已在上面合并，只需确保不重复
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 累计加成
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float totalDamageBonus = 0f;   // Power 累计伤害加成
        private float totalWidthBonus = 0f;    // Wide 累计宽度加成
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - Adrenaline
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private bool isAdrenalineActive = false;
        private float adrenalineTimer = 0f;
        private float originalSensitivity = 180f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态 - 缓存的配置数据【新增】
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        // Focus 配置缓存
        private float cachedFocusDamageBonus = 0f;  // 【改名】存储加成值，不是倍率
        private float cachedFocusBossDamageBonus = 0f;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Focus Lv5 爆炸系统（V2.0 修复版 - 切断递归）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private const float EXPLOSION_DAMAGE_SCALE = 3.0f;      // 爆炸伤害 = 面板DPS × 3.0
        private const float EXPLOSION_GLOBAL_COOLDOWN = 0.1f;   // 全局爆炸CD（秒）
        private const float EXPLOSION_TARGET_IMMUNITY = 0.15f;  // 单体免疫时间（秒）
        private float lastExplosionTime = -999f;                // 上次爆炸时间
        private Dictionary<int, float> explosionImmunityTracker = new Dictionary<int, float>(); // 敌人免疫追踪
        // Frost 配置缓存
        private float cachedFrostSlowPercent = 0f;
        private float cachedFrostSlowDuration = 0f;
        private float cachedFrostFreezeThreshold = 0f;
        private float cachedFrostFreezeDuration = 0f;
        
        // Impact 配置缓存
        private float cachedImpactKnockbackMultiplier = 1f;
        // ── Crit ──（新增 critDamageBonus）
        private float cachedCritRateBonus = 0f;
        private float cachedCritDamageBonus = 0f;   // 【新增】
        private bool  cachedCritKnockback = false;  // 【新增】
        // 【新增】Shatter (数据破碎) 配置缓存
        private float cachedShatterDamageBonus = 0f;           // 对受损敌人额外伤害
        private bool cachedShatterExplosionOnKill = false;     // 是否触发漏洞扩散
        private float cachedShatterExplosionRadius = 2f;       // 漏洞扩散爆炸半径
        private const float SHATTER_EXPLOSION_DAMAGE_SCALE = 2.0f;  // 漏洞扩散伤害 = 面板DPS × 2.0
        // Focus 穿透配置缓存【新增】
        private int cachedFocusPenetrationCount = 0;      // 穿透数量（-1=无限）
        private float cachedFocusPenetrationDecay = 0.1f; // 穿透衰减率
        private bool cachedFocusTrueDamageToBoss = false; // 真实伤害标记
        // Chain 配置缓存
        private int cachedChainBounces = 1;
        private float cachedChainRange = 3f;
        private float cachedChainDamageDecay = 0.2f;
        // ── FrostSpread ──（全部新增）
        private float cachedFrostSpreadRadius = 1.5f;
        private float cachedFrostSpreadSlowRatio = 0.5f;
        // 【V4.2 新增】寒霜蔓延 Lv5 扩散冰冻
        private float cachedFrostSpreadFreezeRate;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 简单单例
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        
        private void Start()
        {
            // 自动查找组件
            if (laserController == null)
            {
                laserController = FindObjectOfType<LaserController>();
            }
            
            if (turretController == null)
            {
                turretController = FindObjectOfType<TurretController>();
            }
            
            if (turretHealth == null)
            {
                turretHealth = FindObjectOfType<TurretHealth>();
            }
            
            if (shieldController == null)
            {
                shieldController = FindObjectOfType<ShieldController>();
            }
            
            if (vfxColorSync == null && laserController != null)
            {
                var mainLaserBeam = laserController.GetComponentInChildren<LaserBeam>();
                if (mainLaserBeam != null)
                {
                    vfxColorSync = mainLaserBeam.GetComponent<LaserVFXColorSync>();
                }
            }
            
            // 验证 SkillDatabase
            if (skillDatabase == null)
            {
                GameLogger.LogError("[SkillEffectManager] ❌ SkillDatabase 未设置！技能系统将无法正常工作 波次");
            }
            
            // 订阅事件
            SubscribeEvents();
        }
        
        private void Update()
        {
            // 更新 Adrenaline 计时
            if (isAdrenalineActive)
            {
                adrenalineTimer -= Time.deltaTime;
                if (adrenalineTimer <= 0f)
                {
                    EndAdrenaline();
                }
            }
        }
        
        private void OnDestroy()
        {
            UnsubscribeEvents();
            
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件订阅
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void SubscribeEvents()
        {
            GameEvents.OnSkillApplied += OnSkillApplied;
            GameEvents.OnEnemyDied += OnEnemyDied; // 【新增】监听敌人死亡，处理 Focus Lv5 爆炸
        }
        
        private void UnsubscribeEvents()
        {
            GameEvents.OnSkillApplied -= OnSkillApplied;
            GameEvents.OnEnemyDied -= OnEnemyDied;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能应用回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnSkillApplied(SkillType skillType, int newLevel)
        {
            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillEffectManager] 收到技能应用: {skillType} -> Lv.{newLevel}");
            }
            
            // 获取技能配置
            SkillData skillData = GetSkillData(skillType);
            
            switch (skillType)
            {
                // ========== 主动技能 ==========
                case SkillType.Prism:
                    ApplyPrismEffect(newLevel, skillData);
                    break;
                    
                case SkillType.Focus:
                    ApplyFocusEffect(newLevel, skillData);
                    break;
                    
                case SkillType.Impact:
                    ApplyImpactEffect(newLevel, skillData);
                    break;
                    
                case SkillType.Frost:
                    ApplyFrostEffect(newLevel, skillData);
                    break;
                case SkillType.FrostSpread:   // 【新增】
                    ApplyFrostSpreadEffect(newLevel, skillData);
                    break;  
                case SkillType.Chain:
                    ApplyChainEffect(newLevel, skillData);
                    break;
                    
                // ========== 被动技能 ==========
                case SkillType.Power:
                    ApplyPowerEffect(newLevel, skillData);
                    break;
                    
                case SkillType.Wide:
                    ApplyWideEffect(newLevel, skillData);
                    break;
                case SkillType.Crit:
                    ApplyCritEffect(newLevel, skillData);
                    break;
                case SkillType.Shatter:
                    ApplyShatterEffect(newLevel, skillData);
                    break;
            }
            
            // 更新颜色（考虑 Focus + Frost 组合）
            UpdateLaserColor();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】配置读取辅助方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 从 SkillDatabase 获取技能配置
        /// </summary>
        private SkillData GetSkillData(SkillType type)
        {
            if (skillDatabase == null) return null;
            return skillDatabase.GetData(type);
        }
        
        /// <summary>
        /// 获取指定技能的等级数据
        /// </summary>
        private SkillLevelData GetLevelData(SkillData skillData, int level)
        {
            if (skillData == null || level <= 0) return null;
            return skillData.GetLevelData(level);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 主动技能效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 应用 Prism（折射棱镜）效果
        /// </summary>
        private void ApplyPrismEffect(int level, SkillData skillData)
        {
            prismLevel = level;
    
            if (laserController == null) return;
    
            var levelData = GetLevelData(skillData, level);
            if (levelData == null)
            {
                GameLogger.LogError($"[SkillEffectManager] ❌ Prism Lv.{level} 配置缺失！请检查 SkillDatabase");
                return;
            }
    
            laserController.SetPrismLevelFromConfig(
                level,
                levelData.splitCount,
                levelData.splitDamageMultiplier,
                levelData.splitLength
            );
    
            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillEffectManager] ✓ Prism Lv.{level} - " +
                          $"分裂数:{levelData.splitCount}, 伤害:{levelData.splitDamageMultiplier:P0}, 长度:{levelData.splitLength}");
            }
        }
        
        /// <summary>
        /// 应用 Focus（聚能透镜）效果
        /// </summary>
        private void ApplyFocusEffect(int level, SkillData skillData)
        {
            focusLevel = level;

            var levelData = GetLevelData(skillData, level);
            if (levelData == null)
            {
                GameLogger.LogError($"[SkillEffectManager] ❌ Focus Lv.{level} 配置缺失！请检查 SkillDatabase");
                return;
            }

            // 缓存原有配置数据
            cachedFocusDamageBonus = levelData.damageMultiplier - 1f;
            cachedFocusBossDamageBonus = levelData.bossDamageBonus;

            // 【新增】缓存穿透配置数据
            cachedFocusPenetrationCount = levelData.penetrationCount;
            cachedFocusPenetrationDecay = levelData.penetrationDecay;
            cachedFocusTrueDamageToBoss = levelData.dealsTrueDamageToBoss;

            // 应用到 LaserController
            if (laserController != null)
            {
                // 【新增】设置穿透参数
                laserController.SetFocusPenetrationParams(
                    cachedFocusPenetrationCount,
                    cachedFocusPenetrationDecay,
                    cachedFocusTrueDamageToBoss
                );
            }

            if (showDebugInfo)
            {
                string penetrationInfo = cachedFocusPenetrationCount == -1 
                    ? "无限穿透" 
                    : $"穿透{cachedFocusPenetrationCount}个";
                string trueDamageInfo = cachedFocusTrueDamageToBoss ? ", Boss真实伤害" : "";
        
                GameLogger.Log($"[SkillEffectManager] ✓ Focus Lv.{level} - " +
                          $"伤害:+{cachedFocusDamageBonus:P0}, {penetrationInfo}, " +
                          $"衰减:{cachedFocusPenetrationDecay:P0}{trueDamageInfo}");
            }

            UpdateDamageMultiplier();
        }
        
        /// <summary>
        /// 应用 Impact（冲击模块）效果
        /// 【重构】回归纯物理击退，移除碎冰增伤和处决
        /// </summary>
        private void ApplyImpactEffect(int level, SkillData skillData)
        {
            impactLevel = level;

            var levelData = GetLevelData(skillData, level);
            if (levelData == null)
            {
                GameLogger.LogError($"[SkillEffectManager] ❌ Impact Lv.{level} 配置缺失！请检查 SkillDatabase");
                return;
            }

            cachedImpactKnockbackMultiplier = levelData.knockbackMultiplier;

            if (laserController != null)
            {
                laserController.SetKnockbackMultiplier(cachedImpactKnockbackMultiplier);
            }

            if (showDebugInfo)
            {
                string canPushBoss = (level >= 5) ? "，可推BOSS" : "";
                GameLogger.Log($"[SkillEffectManager] ✓ Impact Lv.{level} - " +
                          $"击退力:{cachedImpactKnockbackMultiplier:F2}x{canPushBoss}");
            }
        }
        
        /// <summary>
        /// 应用 Frost（极寒光束）效果
        /// </summary>
        private void ApplyFrostEffect(int level, SkillData skillData)
        {
            frostLevel = level;
    
            var levelData = GetLevelData(skillData, level);
            if (levelData == null)
            {
                GameLogger.LogError($"[SkillEffectManager] ❌ Frost Lv.{level} 配置缺失！请检查 SkillDatabase");
                return;
            }
    
            cachedFrostSlowPercent = levelData.slowPercent;
            cachedFrostSlowDuration = levelData.slowDuration;
            cachedFrostFreezeThreshold = levelData.freezeThreshold;
            cachedFrostFreezeDuration = levelData.freezeDuration;
    
            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillEffectManager] ✓ Frost Lv.{level} - " +
                          $"减速:{cachedFrostSlowPercent:P0}/{cachedFrostSlowDuration:F1}s, " +
                          $"冰冻阈值:{cachedFrostFreezeThreshold:F1}s");
            }
        }
        /// <summary>获取 Chain 等级</summary>
        public int GetChainLevel() => chainLevel;

        /// <summary>
        /// 获取 Chain 连锁反应参数
        /// </summary>
        public void GetChainParams(out int bounces, out float range, out float decay)
        {
            bounces = cachedChainBounces;
            range = cachedChainRange;
            decay = cachedChainDamageDecay;
        }

        /// <summary>连锁反应是否启用</summary>
        public bool IsChainEnabled => chainLevel > 0;
        /// <summary>
        /// 应用 Chain（连锁反应）效果
        /// </summary>
        private void ApplyChainEffect(int level, SkillData skillData)
        {
            chainLevel = level;

            var levelData = GetLevelData(skillData, level);
            if (levelData == null)
            {
                GameLogger.LogError($"[SkillEffectManager] ❌ Chain Lv.{level} 配置缺失！请检查 SkillDatabase");
                return;
            }

            // 缓存配置数据
            cachedChainBounces = levelData.chainBounces;
            cachedChainRange = levelData.chainRange;
            cachedChainDamageDecay = levelData.chainDamageDecay;

            // 应用到 ChainLightningManager
            if (ChainLightningManager.Instance != null)
            {
                ChainLightningManager.Instance.SetChainParameters(
                    level,
                    cachedChainBounces,
                    cachedChainRange,
                    cachedChainDamageDecay
                );
            }

            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillEffectManager] ✓ Chain Lv.{level} - " +
                          $"跳数:{cachedChainBounces}, 距离:{cachedChainRange}m, 衰减:{cachedChainDamageDecay:P0}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 被动技能效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 应用 Power（功率超频）效果
        /// </summary>
        private void ApplyPowerEffect(int level, SkillData skillData)
        {
            powerLevel = level;
    
            var levelData = GetLevelData(skillData, level);
            if (levelData == null)
            {
                GameLogger.LogError($"[SkillEffectManager] ❌ Power Lv.{level} 配置缺失！请检查 SkillDatabase");
                return;
            }
    
            totalDamageBonus = levelData.damageMultiplier - 1f;
    
            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillEffectManager] ✓ Power Lv.{level} - 伤害加成:{totalDamageBonus:P0}");
            }
    
            UpdateDamageMultiplier();
        }
        
        /// <summary>
        /// 应用 Wide（广域透镜）效果
        /// </summary>
        private void ApplyWideEffect(int level, SkillData skillData)
        {
            wideLevel = level;

            var levelData = GetLevelData(skillData, level);
            if (levelData == null)
            {
                GameLogger.LogError($"[SkillEffectManager] ❌ Wide Lv.{level} 配置缺失！请检查 SkillDatabase");
                return;
            }
    
            totalWidthBonus = levelData.widthMultiplier - 1f;

            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillEffectManager] ✓ Wide Lv.{level} - 宽度加成:{totalWidthBonus:P0}");
            }

            UpdateWidthMultiplier();
        }
        /// <summary>
        /// 更新宽度倍率 - 只计算 Wide 加成
        /// 【已删除】Focus 不再影响宽度
        /// </summary>
        private void UpdateWidthMultiplier()
        {
            if (laserController == null) return;

            // 【修改】Focus 不再影响宽度，基础倍率始终为 1.0
            float baseWidthMultiplier = 1f;

            // Wide 的累加加成
            float wideBonus = totalWidthBonus;

            // 最终宽度倍率 = 基础 * (1 + Wide加成)
            float finalMultiplier = baseWidthMultiplier * (1f + wideBonus);

            laserController.SetWidthMultiplier(finalMultiplier);

            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillEffectManager] 宽度倍率更新: Wide=+{wideBonus:P0}, 最终={finalMultiplier:F2}x");
            }
        }
        /// <summary>
        /// 应用 Crit（致命暴击）效果
        /// Lv1-4：每级 +3% 暴击率 / +15% 暴击伤害
        /// Lv5：+5% 暴击率 / +30% 暴击伤害 / 暴击时附带微弱击退
        /// </summary>
        private void ApplyCritEffect(int level, SkillData skillData)
        {
            critLevel = level;

            var levelData = GetLevelData(skillData, level);
            if (levelData == null)
            {
                GameLogger.LogError($"[SkillEffectManager] ❌ Crit Lv.{level} 配置缺失！请检查 SkillDatabase");
                return;
            }

            cachedCritRateBonus   = levelData.critRateBonus;
            cachedCritDamageBonus = levelData.critDamageBonus;
            cachedCritKnockback   = levelData.critKnockback;

            if (laserController != null)
            {
                laserController.SetCritLevelFromConfig(
                    level,
                    cachedCritRateBonus,
                    cachedCritDamageBonus,
                    cachedCritKnockback);
            }

            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillEffectManager] ✓ Crit Lv.{level} - " +
                          $"暴击率+{cachedCritRateBonus:P0}, " +
                          $"暴击伤害+{cachedCritDamageBonus:P0}, " +
                          $"击退={cachedCritKnockback}");
            }
        }
        /// <summary>
        /// 应用 Shatter（数据破碎）效果
        /// LV1-4: 对受损敌人额外伤害
        /// LV5: 额外伤害 + 漏洞扩散（击杀受损敌人触发AOE）
        /// </summary>
        private void ApplyShatterEffect(int level, SkillData skillData)
        {
            shatterLevel = level;
            
            var levelData = GetLevelData(skillData, level);
            if (levelData == null)
            {
                GameLogger.LogError($"[SkillEffectManager] ❌ Shatter Lv.{level} 配置缺失！请检查 SkillDatabase");
                return;
            }
            
            // 缓存配置数据
            cachedShatterDamageBonus = levelData.shatterDamageBonus;
            cachedShatterExplosionOnKill = levelData.shatterExplosionOnKill;
            cachedShatterExplosionRadius = levelData.shatterExplosionRadius;
            
            if (showDebugInfo)
            {
                string explosionInfo = cachedShatterExplosionOnKill ? "，启用漏洞扩散" : "";
                GameLogger.Log($"[SkillEffectManager] ✓ Shatter Lv.{level} - " +
                          $"受损加伤:{cachedShatterDamageBonus:P0}{explosionInfo}");
            }
        }
        /// <summary>
        /// 应用 FrostSpread（寒霜蔓延）效果
        /// 【V4.2 重构】
        /// Lv1-4：为直接命中的敌人周围产生扩散减速 + VFX
        /// Lv5 质变：扩散也能缓慢累积冰冻（深寒传染）
        /// </summary>
        private void ApplyFrostSpreadEffect(int level, SkillData skillData)
        {
            frostSpreadLevel = level;

            var levelData = GetLevelData(skillData, level);
            if (levelData == null)
            {
                GameLogger.LogError($"[SkillEffectManager] ❌ FrostSpread Lv.{level} 配置缺失！请检查 SkillDatabase");
                return;
            }

            cachedFrostSpreadRadius    = levelData.frostSpreadRadius;
            cachedFrostSpreadSlowRatio = levelData.frostSpreadSlowRatio;
            cachedFrostSpreadFreezeRate = levelData.frostSpreadFreezeRate;

            if (showDebugInfo)
            {
                string lv5Info = (level >= 5 && cachedFrostSpreadFreezeRate > 0f) 
                    ? $"，Lv5深寒传染(冰冻速率:{cachedFrostSpreadFreezeRate:P0})" : "";
                GameLogger.Log($"[SkillEffectManager] ✓ FrostSpread Lv.{level} - " +
                          $"扩散半径:{cachedFrostSpreadRadius}m, " +
                          $"减速比例:{cachedFrostSpreadSlowRatio:P0}{lv5Info}");
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】颜色系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 更新激光颜色（考虑 Focus + Frost 组合）
        /// 只有在有 Focus 或 Frost 技能时才修改颜色，否则保持原始材质颜色
        /// </summary>
        private void UpdateLaserColor()
        {
            if (laserController == null) return;

            bool hasFocus = focusLevel > 0;
            bool hasFrost = frostLevel > 0;

            // 【关键修改】如果没有 Focus 和 Frost，不修改颜色，保持原始材质颜色
            if (!hasFocus && !hasFrost)
            {
                // 重置为原始颜色（如果之前有设置过）
                laserController.ResetLaserColor();
                
                if (vfxColorSync != null)
                {
                    vfxColorSync.ResetVFXColor();
                }
                else
                {
                    laserController.ResetVFXColor();
                }
                
                if (showDebugInfo)
                {
                    GameLogger.Log("[SkillEffectManager] 颜色重置为原始材质颜色");
                }
                return;
            }

            if (skillDatabase == null)
            {
                GameLogger.LogWarning("[SkillEffectManager] SkillDatabase 未设置，无法更新颜色");
                return;
            }

            Color targetColor;

            // 获取技能配置
            var focusData = GetSkillData(SkillType.Focus);
            var frostData = GetSkillData(SkillType.Frost);

            if (hasFocus && hasFrost)
            {
                // 聚能 + 极寒 = 混合紫色
                targetColor = skillDatabase.FocusFrostMixColor;
            }
            else if (hasFocus)
            {
                // 仅聚能 = 从 Focus SkillData 读取颜色
                if (focusData != null && focusData.changeColor)
                {
                    targetColor = focusData.skillColor;
                }
                else
                {
                    return; // Focus 配置不修改颜色，保持原样
                }
            }
            else // hasFrost
            {
                // 仅极寒 = 从 Frost SkillData 读取颜色
                if (frostData != null && frostData.changeColor)
                {
                    targetColor = frostData.skillColor;
                }
                else
                {
                    return; // Frost 配置不修改颜色，保持原样
                }
            }

            // 应用颜色到激光
            laserController.SetLaserColor(targetColor);

            // VFX颜色与激光颜色保持一致
            if (vfxColorSync != null)
            {
                vfxColorSync.SetVFXColor(targetColor);
            }
            else
            {
                laserController.SetVFXColor(targetColor);
            }

            if (showDebugInfo)
            {
                string colorSource = (hasFocus && hasFrost) ? "混合紫色" : 
                    (hasFocus ? "Focus颜色" : "Frost颜色");
                GameLogger.Log($"[SkillEffectManager] 激光颜色更新: {colorSource}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】Focus Lv5 爆炸系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 敌人死亡回调 - 处理 Focus Lv5 爆炸（V2.0 带CD检查）
        /// </summary>
        private void OnEnemyDied(EnemyType type, Vector3 position, int xp, int coin)
        {
            if (focusLevel < 5) return;

            // 【新增】处决击杀不触发爆炸（需要通过其他方式检查，这里暂时保留原逻辑）
            // 注：处决标记在 EnemyBlob 中，但事件不传递该信息
            // 实际排除逻辑将在 LaserController 中处理

            // 【新增】只有 Slime 和 Rusher 才会触发爆炸（其他怪物不会引发连锁）
            if (type != EnemyType.Slime && type != EnemyType.Rusher) return;

            // 全局CD检查（防止连锁爆炸过于密集）
            if (Time.time < lastExplosionTime + EXPLOSION_GLOBAL_COOLDOWN) return;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 倍率更新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateDamageMultiplier()
        {
            if (laserController == null) return;
    
            // 区间 A：基础增伤区（加法叠加）
            // Power 加成（每级 +20%，Lv5 = +100% = 1.0）
            float powerBonus = totalDamageBonus;
    
            // Focus 加成（Lv1=+50%, Lv2=+80%, Lv3=+120%, Lv4=+160%, Lv5=+250%）
            float focusBonus = cachedFocusDamageBonus;
    
            // 最终倍率 = 1 + Power加成 + Focus加成
            float finalMultiplier = 1f + powerBonus + focusBonus;
    
            laserController.SetDamageMultiplier(finalMultiplier);
    
            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillEffectManager] 伤害倍率更新 (加法区): " +
                          $"基础=1.0, Power=+{powerBonus:P0}, Focus=+{focusBonus:P0}, " +
                          $"最终={finalMultiplier:F2}x");
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】Shatter Lv5 漏洞扩散爆炸系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 触发 Shatter Lv5 漏洞扩散爆炸
        /// 由 EnemyBlob.Die() 在击杀受损敌人时调用
        /// </summary>
        /// <param name="position">爆炸位置</param>
        public void TriggerShatterExplosion(Vector3 position)
        {
            // 1. 检查是否启用
            if (!cachedShatterExplosionOnKill || shatterLevel < 5) return;
            
            // 2. 全局CD检查（复用 Focus 爆炸的CD机制）
            if (Time.time < lastExplosionTime + EXPLOSION_GLOBAL_COOLDOWN) return;
            
            // 3. 更新全局CD时间戳
            lastExplosionTime = Time.time;
            
            // 4. 播放漏洞扩散爆炸特效（冰块碎裂效果）
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayShatterExplosion(position);
            }
            
            // 5. 播放爆炸音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayEnemyExplode();
            }
            
            // 6. 计算爆炸伤害（200% 激光面板伤害）
            float panelDPS = laserController != null ? laserController.CurrentPanelDPS : 100f;
            float explosionDamage = panelDPS * SHATTER_EXPLOSION_DAMAGE_SCALE;
            
            // 7. 上报爆炸伤害到 BattleStatistics
            if (BattleStatistics.Instance != null)
            {
                BattleStatistics.Instance.RecordExplosionDamage(explosionDamage);
            }
            
            // 8. 检测范围内的敌人
            int enemyLayer = LayerMask.GetMask(GameConstants.ENEMY_LAYER, "BouncingEnemy");
            Collider2D[] hits = Physics2D.OverlapCircleAll(position, cachedShatterExplosionRadius, enemyLayer);
            
            int actualHitCount = 0;
            
            foreach (var hit in hits)
            {
                EnemyBlob enemy = hit.GetComponentInParent<EnemyBlob>();
                if (enemy == null) continue;
                
                int enemyId = enemy.GetInstanceID();
                
                // 9. 单体免疫检查（复用 Focus 爆炸的免疫机制）
                if (explosionImmunityTracker.TryGetValue(enemyId, out float lastHitTime))
                {
                    if (Time.time < lastHitTime + EXPLOSION_TARGET_IMMUNITY)
                    {
                        continue;
                    }
                }
                
                // 10. 记录该敌人被炸时间
                explosionImmunityTracker[enemyId] = Time.time;
                
                // 11. 造成伤害（标记为爆炸伤害）
                enemy.TakeDamage(explosionDamage, Vector2.zero, false, true);
                actualHitCount++;
            }
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[SkillEffectManager] 💥 Shatter Lv5 漏洞扩散! " +
                          $"位置:{position}, 面板DPS:{panelDPS:F1}, " +
                          $"爆炸伤害:{explosionDamage:F1} (×{SHATTER_EXPLOSION_DAMAGE_SCALE}), " +
                          $"半径:{cachedShatterExplosionRadius}, 命中:{actualHitCount}/{hits.Length}");
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>获取 Frost 等级</summary>
        public int GetFrostLevel() => frostLevel;
        
        /// <summary>获取 Impact 等级</summary>
        public int GetImpactLevel() => impactLevel;

        /// <summary>获取 Crit 等级</summary>
        public int GetCritLevel() => critLevel;
        
        /// <summary>
        /// 获取 Frost 减速参数
        /// </summary>
        public void GetFrostParams(out float slowPercent, out float duration)
        {
            slowPercent = cachedFrostSlowPercent;
            duration = cachedFrostSlowDuration;
        }
        
        /// <summary>
        /// 兼容旧接口
        /// </summary>
        public void GetFrostData(out float slowPercent, out float duration)
        {
            GetFrostParams(out slowPercent, out duration);
        }
        
        /// <summary>
        /// 获取 Frost Lv5 冰冻参数
        /// </summary>
        public void GetFrostFreezeParams(out float threshold, out float duration)
        {
            threshold = cachedFrostFreezeThreshold;
            duration = cachedFrostFreezeDuration;
        }
        /// <summary>寒霜蔓延技能等级</summary>
        public int GetFrostSpreadLevel() => frostSpreadLevel;

        /// <summary>寒霜蔓延是否启用</summary>
        public bool IsFrostSpreadEnabled => frostSpreadLevel > 0;

        /// <summary>
        /// 获取寒霜蔓延扩散参数
        /// </summary>
        public void GetFrostSpreadParams(out float spreadRadius, out float spreadSlowRatio)
        {
            spreadRadius    = cachedFrostSpreadRadius;
            spreadSlowRatio = cachedFrostSpreadSlowRatio;
        }
        /// <summary>
        /// 【V4.2 新增】获取寒霜蔓延 Lv5 扩散冰冻累积速率
        /// </summary>
        public float GetFrostSpreadFreezeRate() => cachedFrostSpreadFreezeRate;
        /// <summary>
        /// Lv.5 Frost 完全冰冻判定（旧接口，基于概率）
        /// </summary>
        public bool TryFrostFreeze()
        {
            if (frostLevel < 5) return false;
            return Random.value < 0.20f; // 20% 概率
        }
        
        /// <summary>
        /// 获取 Focus 对 BOSS 的额外伤害加成
        /// </summary>
        public float GetFocusBossDamageBonus()
        {
            return cachedFocusBossDamageBonus;
        }

        /// <summary>
        /// 判断是否可以打断 BOSS 蓄力
        /// </summary>
        public bool CanInterruptBossCharge()
        {
            return impactLevel >= 5;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Adrenaline 效果（保留旧逻辑）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void EndAdrenaline()
        {
            isAdrenalineActive = false;
            
            if (turretController != null)
            {
                turretController.SetSensitivity(originalSensitivity);
            }
            
            if (showDebugInfo)
            {
                GameLogger.Log("[SkillEffectManager] Adrenaline 效果结束");
            }
        }
        /// <summary>
        /// 获取Wide技能等级
        /// </summary>
        public int GetWideLevel()
        {
            return wideLevel;
        }
        /// <summary>获取 Shatter 等级</summary>
        public int GetShatterLevel() => shatterLevel;
        
        /// <summary>
        /// 获取数据破碎对受损敌人的额外伤害加成
        /// </summary>
        public float GetShatterDamageBonus() => cachedShatterDamageBonus;
        
        /// <summary>
        /// 数据破碎 Lv5 漏洞扩散是否启用
        /// </summary>
        public bool IsShatterExplosionEnabled => cachedShatterExplosionOnKill && shatterLevel >= 5;
        
        /// <summary>
        /// 获取漏洞扩散爆炸半径
        /// </summary>
        public float GetShatterExplosionRadius() => cachedShatterExplosionRadius;
        /// <summary>
        /// 获取 Focus 穿透参数
        /// </summary>
        public void GetFocusPenetrationParams(out int count, out float decay, out bool trueDamage)
        {
            count = cachedFocusPenetrationCount;
            decay = cachedFocusPenetrationDecay;
            trueDamage = cachedFocusTrueDamageToBoss;
        }

        /// <summary>
        /// 获取 Focus 等级
        /// </summary>
        public int GetFocusLevel() => focusLevel;

        /// <summary>
        /// 检查是否启用穿透
        /// </summary>
        public bool IsPenetrationEnabled => focusLevel > 0 && cachedFocusPenetrationCount != 0;

        /// <summary>
        /// 检查是否对Boss造成真实伤害
        /// </summary>
        public bool IsTrueDamageToBossEnabled => cachedFocusTrueDamageToBoss;
        
    }
}