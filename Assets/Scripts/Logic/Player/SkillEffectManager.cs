// ============================================================
// SkillEffectManager.cs
// 文件位置: Assets/Scripts/Logic/Player/SkillEffectManager.cs
// 用途：技能效果管理器 - 监听技能选择并应用效果
// 修改：Step 1 - 添加 SkillDatabase 引用，从配置读取参数
// ============================================================

using LightVsDecay.Audio;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Enemy;

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
        [SerializeField] private bool showDebugInfo = true;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例访问
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public static SkillEffectManager Instance { get; private set; }
        
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
        private int reflexLevel = 0;
        private int critLevel = 0;
        
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
        private bool cachedFocusExplosionOnKill = false;
        private float cachedFocusExplosionDamage = 100f;
        private float cachedFocusExplosionRadius = 2f;
        
        // Frost 配置缓存
        private float cachedFrostSlowPercent = 0f;
        private float cachedFrostSlowDuration = 0f;
        private float cachedFrostFreezeThreshold = 0f;
        private float cachedFrostFreezeDuration = 0f;
        
        // Impact 配置缓存
        private float cachedImpactKnockbackMultiplier = 1f;

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
                Debug.LogWarning("[SkillEffectManager] ⚠️ SkillDatabase 未设置！将使用硬编码参数作为回退");
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
                Debug.Log($"[SkillEffectManager] 收到技能应用: {skillType} -> Lv.{newLevel}");
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
                    
                case SkillType.Reflex:
                    ApplyReflexEffect(newLevel, skillData);
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
            
            // 从配置读取参数
            var levelData = GetLevelData(skillData, level);
            if (levelData != null)
            {
                laserController.SetPrismLevelFromConfig(
                    level,
                    levelData.splitCount,
                    levelData.splitDamageMultiplier,
                    levelData.splitLength
                );
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Prism Lv.{level} (配置) - " +
                              $"分裂数:{levelData.splitCount}, 伤害:{levelData.splitDamageMultiplier:P0}, 长度:{levelData.splitLength}");
                }
            }
            else
            {
                // 回退到硬编码
                laserController.SetPrismLevel(level);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Prism Lv.{level} (硬编码回退)");
                }
            }
        }
        
        /// <summary>
        /// 应用 Focus（聚能透镜）效果
        /// </summary>
        private void ApplyFocusEffect(int level, SkillData skillData)
        {
            focusLevel = level;
            
            var levelData = GetLevelData(skillData, level);
            
            if (levelData != null)
            {
                // 缓存配置数据
                cachedFocusDamageBonus = levelData.damageMultiplier - 1f;
                cachedFocusBossDamageBonus = levelData.bossDamageBonus;
                cachedFocusExplosionOnKill = levelData.explosionOnKill;
                cachedFocusExplosionDamage = levelData.explosionDamage;
                cachedFocusExplosionRadius = levelData.explosionRadius;
                
                // 应用到 LaserController
                if (laserController != null)
                {
                    // 宽度：只有 Lv1 首次选择时减少 50%
                    float widthMultiplier = (level == 1) ? levelData.widthMultiplier : 1f;
                    
                    laserController.SetFocusLevelFromConfig(
                        level,
                        cachedFocusDamageBonus,
                        widthMultiplier
                    );
                }
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Focus Lv.{level} (配置) - " +
                              $"伤害:{cachedFocusDamageBonus:P0}, BOSS加成:{cachedFocusBossDamageBonus:P0}, " +
                              $"爆炸:{cachedFocusExplosionOnKill}");
                }
            }
            else
            {
                // 回退到硬编码
                cachedFocusDamageBonus = GetFocusDamageMultiplierFallback(level) - 1f;
                cachedFocusBossDamageBonus = (level >= 3) ? 0.2f : 0f;
                cachedFocusExplosionOnKill = (level >= 5);
                cachedFocusExplosionDamage = 100f;
                cachedFocusExplosionRadius = 2f;
                
                if (laserController != null)
                {
                    laserController.SetFocusLevel(level);
                }
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Focus Lv.{level} (硬编码回退)");
                }
            }
            
            // 更新伤害倍率
            UpdateDamageMultiplier();
            // 【新增】更新宽度倍率（Focus 会影响基础宽度）
            UpdateWidthMultiplier();
        }
        
        /// <summary>
        /// 应用 Impact（冲击模块）效果
        /// </summary>
        private void ApplyImpactEffect(int level, SkillData skillData)
        {
            impactLevel = level;
    
            var levelData = GetLevelData(skillData, level);
    
            if (levelData != null)
            {
                cachedImpactKnockbackMultiplier = levelData.knockbackMultiplier;
        
                if (laserController != null)
                {
                    laserController.SetKnockbackMultiplier(cachedImpactKnockbackMultiplier);
                }
        
                if (showDebugInfo)
                {
                    string canPushBoss = (level >= 5) ? "，可推BOSS" : "";
                    Debug.Log($"[SkillEffectManager] ✓ Impact Lv.{level} (配置) - 击退力:{cachedImpactKnockbackMultiplier:F2}x{canPushBoss}");
                }
            }
            else
            {
                // 回退到硬编码
                cachedImpactKnockbackMultiplier = GetImpactKnockbackMultiplierFallback(level);
        
                if (laserController != null)
                {
                    laserController.SetKnockbackMultiplier(cachedImpactKnockbackMultiplier);
                }
        
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Impact Lv.{level} (硬编码回退)");
                }
            }
        }
        
        /// <summary>
        /// 应用 Frost（极寒光束）效果
        /// </summary>
        private void ApplyFrostEffect(int level, SkillData skillData)
        {
            frostLevel = level;
            
            var levelData = GetLevelData(skillData, level);
            
            if (levelData != null)
            {
                // 缓存配置数据
                cachedFrostSlowPercent = levelData.slowPercent;
                cachedFrostSlowDuration = levelData.slowDuration;
                cachedFrostFreezeThreshold = levelData.freezeThreshold;
                cachedFrostFreezeDuration = levelData.freezeDuration;
                
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Frost Lv.{level} (配置) - " +
                              $"减速:{cachedFrostSlowPercent:P0}/{cachedFrostSlowDuration:F1}s, " +
                              $"冰冻阈值:{cachedFrostFreezeThreshold:F1}s");
                }
            }
            else
            {
                // 回退到硬编码
                GetFrostDataFallback(level, out cachedFrostSlowPercent, out cachedFrostSlowDuration);
                cachedFrostFreezeThreshold = (level >= 5) ? 1.5f : 0f;
                cachedFrostFreezeDuration = (level >= 5) ? 1.0f : 0f;

                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Frost Lv.{level} (硬编码回退)");
                }
            }
        }
        
        /// <summary>
        /// 应用 Reflex（反射透镜）效果
        /// </summary>
        private void ApplyReflexEffect(int level, SkillData skillData)
        {
            reflexLevel = level;
            
            var levelData = GetLevelData(skillData, level);
            
            if (levelData != null && laserController != null)
            {
                laserController.SetReflexLevelFromConfig(
                    level,
                    levelData.reflexDamageMultiplier,
                    levelData.reflexLengthBonus
                );
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Reflex Lv.{level} (配置) - " +
                              $"反射伤害:{levelData.reflexDamageMultiplier:P0}, 长度加成:{levelData.reflexLengthBonus:P0}");
                }
            }
            else
            {
                // 回退到硬编码
                if (laserController != null)
                {
                    laserController.SetReflexLevel(level);
                }
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Reflex Lv.{level} (硬编码回退)");
                }
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
            
            if (levelData != null)
            {
                // 从配置读取伤害倍率，转换为加成值
                // 配置中 damageMultiplier = 1.2 表示 +20%
                totalDamageBonus = levelData.damageMultiplier - 1f;
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Power Lv.{level} (配置) - 伤害加成:{totalDamageBonus:P0}");
                }
            }
            else
            {
                // 回退：每级 +20%
                totalDamageBonus = level * 0.20f;
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Power Lv.{level} (硬编码回退) - 伤害加成:{totalDamageBonus:P0}");
                }
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
    
            if (levelData != null)
            {
                // 从配置读取宽度倍率（1.4 = +40%）
                // 这个倍率是相对于【当前基础宽度】的加成
                float wideBonus = levelData.widthMultiplier - 1f; // 转换为加成值
                totalWidthBonus = wideBonus;
        
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Wide Lv.{level} (配置) - 宽度加成:{wideBonus:P0}");
                }
            }
            else
            {
                // 回退：每级 +40%
                totalWidthBonus = level * 0.40f;
        
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Wide Lv.{level} (硬编码回退) - 宽度加成:{totalWidthBonus:P0}");
                }
            }
    
            // 更新最终宽度
            UpdateWidthMultiplier();
        }
        /// <summary>
        /// 更新宽度倍率
        /// 计算公式：最终宽度倍率 = Focus宽度倍率 * (1 + Wide加成)
        /// 例如：Focus Lv1 (0.5x) + Wide Lv1 (+40%) = 0.5 * 1.4 = 0.7x
        /// </summary>
        private void UpdateWidthMultiplier()
        {
            if (laserController == null) return;
    
            // Focus 的宽度倍率（可能是 0.5 表示变细）
            float focusWidthMultiplier = (focusLevel >= 1) ? 0.5f : 1f;
    
            // 如果有配置，从 Focus 配置中读取
            var focusData = GetSkillData(SkillType.Focus);
            if (focusData != null && focusLevel >= 1)
            {
                var focusLevelData = GetLevelData(focusData, 1); // 只有 Lv1 会变细
                if (focusLevelData != null && focusLevelData.widthMultiplier < 1f)
                {
                    focusWidthMultiplier = focusLevelData.widthMultiplier;
                }
            }
    
            // Wide 的累加加成
            float wideBonus = totalWidthBonus;
    
            // 最终宽度倍率 = Focus宽度 * (1 + Wide加成)
            float finalMultiplier = focusWidthMultiplier * (1f + wideBonus);
    
            laserController.SetWidthMultiplier(finalMultiplier);
    
            if (showDebugInfo)
            {
                Debug.Log($"[SkillEffectManager] 宽度倍率更新: Focus={focusWidthMultiplier:F2}x, Wide=+{wideBonus:P0}, 最终={finalMultiplier:F2}x");
            }
        }
        /// <summary>
        /// 应用 Crit（致命暴击）效果
        /// </summary>
        private void ApplyCritEffect(int level, SkillData skillData)
        {
            critLevel = level;
            
            var levelData = GetLevelData(skillData, level);
            
            if (levelData != null && laserController != null)
            {
                laserController.SetCritLevelFromConfig(level, levelData.critRateBonus);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[SkillEffectManager] ✓ Crit Lv.{level} (配置) - 暴击率加成:{levelData.critRateBonus:P0}");
                }
            }
            else
            {
                // 回退：每级 +5%
                if (laserController != null)
                {
                    laserController.SetCritLevel(level);
                }
                
                if (showDebugInfo)
                {
                    float critBonus = level * 0.05f;
                    Debug.Log($"[SkillEffectManager] ✓ Crit Lv.{level} (硬编码回退) - 暴击率加成:{critBonus:P0}");
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】颜色系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 更新激光颜色（考虑 Focus + Frost 组合）
        /// </summary>
        private void UpdateLaserColor()
        {
            if (laserController == null) return;
            if (skillDatabase == null)
            {
                Debug.LogWarning("[SkillEffectManager] SkillDatabase 未设置，无法更新颜色");
                return;
            }
    
            Color targetColor = skillDatabase.DefaultLaserColor;
    
            bool hasFocus = focusLevel > 0;
            bool hasFrost = frostLevel > 0;
    
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
            }
            else if (hasFrost)
            {
                // 仅极寒 = 从 Frost SkillData 读取颜色
                if (frostData != null && frostData.changeColor)
                {
                    targetColor = frostData.skillColor;
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
                    (hasFocus ? "Focus红色" : 
                        (hasFrost ? "Frost蓝色" : "默认青色"));
                Debug.Log($"[SkillEffectManager] 颜色更新 - 来源:{colorSource}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】Focus Lv5 爆炸系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 敌人死亡回调 - 处理 Focus Lv5 爆炸
        /// </summary>
        private void OnEnemyDied(EnemyType type, Vector3 position, int xp, int coin)
        {
            // 检查是否启用爆炸
            if (!cachedFocusExplosionOnKill) return;
            if (focusLevel < 5) return;
            
            TriggerFocusExplosion(position);
        }
        
        /// <summary>
        /// 触发 Focus Lv5 聚变爆炸
        /// </summary>
        private void TriggerFocusExplosion(Vector3 position)
        {
            // 【修改】使用对象池播放爆炸特效（替代 Instantiate）
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.PlayEnemyExplosion(position);
            }
            // 【新增】播放爆炸音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayEnemyExplode();
            }

            // 检测范围内的敌人
            int enemyLayer = LayerMask.GetMask(GameConstants.ENEMY_LAYER, "BouncingEnemy");
            Collider2D[] hits = Physics2D.OverlapCircleAll(position, cachedFocusExplosionRadius, enemyLayer);
            
            foreach (var hit in hits)
            {
                EnemyBlob enemy = hit.GetComponentInParent<EnemyBlob>();
                if (enemy != null)
                {
                    // 造成爆炸伤害（不触发连锁爆炸）
                    enemy.TakeDamage(cachedFocusExplosionDamage, Vector2.zero, false);
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillEffectManager] 💥 Focus Lv5 聚变爆炸! 位置:{position}, 伤害:{cachedFocusExplosionDamage}, 半径:{cachedFocusExplosionRadius}, 命中:{hits.Length}");
            }
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
                Debug.Log($"[SkillEffectManager] 伤害倍率更新 (加法区): " +
                          $"基础=1.0, Power=+{powerBonus:P0}, Focus=+{focusBonus:P0}, " +
                          $"最终={finalMultiplier:F2}x");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>获取 Frost 等级</summary>
        public int GetFrostLevel() => frostLevel;
        
        /// <summary>获取 Impact 等级</summary>
        public int GetImpactLevel() => impactLevel;
        
        /// <summary>获取 Reflex 等级</summary>
        public int GetReflexLevel() => reflexLevel;
        
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
        public bool CanInterruptBossCharge(bool isUltMode)
        {
            return impactLevel >= 5 || isUltMode;
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
                Debug.Log("[SkillEffectManager] Adrenaline 效果结束");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 硬编码回退方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float GetFocusDamageMultiplierFallback(int level)
        {
            switch (level)
            {
                case 0: return 1.0f;
                case 1: return 1.50f;
                case 2: return 1.80f;
                case 3: return 2.20f;
                case 4: return 2.60f;
                case 5: return 3.50f;
                default: return 1.0f;
            }
        }
        
        private float GetImpactKnockbackMultiplierFallback(int level)
        {
            switch (level)
            {
                case 1: return 1.50f;
                case 2: return 2.00f;
                case 3: return 2.50f;
                case 4: return 3.00f;
                case 5: return 5.00f;
                default: return 1.0f;
            }
        }

        private void GetFrostDataFallback(int level, out float slowPercent, out float duration)
        {
            switch (level)
            {
                case 1:
                    slowPercent = 0.20f;
                    duration = 0.5f;
                    break;
                case 2:
                    slowPercent = 0.30f;
                    duration = 0.8f;
                    break;
                case 3:
                    slowPercent = 0.40f;
                    duration = 1.0f;
                    break;
                case 4:
                    slowPercent = 0.50f;
                    duration = 1.2f;
                    break;
                case 5:
                    slowPercent = 0.50f;
                    duration = 1.0f;
                    break;
                default:
                    slowPercent = 0f;
                    duration = 0f;
                    break;
            }
        }
    }
}