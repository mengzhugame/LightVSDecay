// ============================================================
// SkillEffectManager.cs
// 文件位置: Assets/Scripts/Logic/Player/SkillEffectManager.cs
// 用途：技能效果管理器 - 监听技能选择并应用效果
// ============================================================

using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Data.SO;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 技能效果管理器
    /// 职责：
    /// - 监听技能选择事件
    /// - 根据技能类型调用对应的效果实现
    /// - 管理 Prism/Focus/Impact/Frost/Power/Wide 等效果
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
        [SerializeField] private TowerRotation towerRotation;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = true;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例访问
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public static SkillEffectManager Instance { get; private set; }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        // 当前技能等级缓存
        private int prismLevel = 0;
        private int focusLevel = 0;
        private int impactLevel = 0;
        private int frostLevel = 0;
        private int powerLevel = 0;
        private int wideLevel = 0;
        
        // 被动技能累计加成
        private float totalDamageBonus = 0f;   // Power 累计伤害加成
        private float totalWidthBonus = 0f;    // Wide 累计宽度加成
        
        // Adrenaline buff 状态
        private bool isAdrenalineActive = false;
        private float adrenalineTimer = 0f;
        private const float ADRENALINE_DURATION = 20f;
        
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
            
            if (towerRotation == null)
            {
                towerRotation = FindObjectOfType<TowerRotation>();
            }
            
            // 订阅事件
            SubscribeEvents();
        }
        
        private void OnDestroy()
        {
            UnsubscribeEvents();
            
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        private void Update()
        {
            // 更新 Adrenaline buff
            UpdateAdrenalineBuff();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件订阅
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void SubscribeEvents()
        {
            // 监听技能应用事件
            GameEvents.OnSkillApplied += OnSkillApplied;
        }
        
        private void UnsubscribeEvents()
        {
            GameEvents.OnSkillApplied -= OnSkillApplied;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能应用回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 技能应用事件回调
        /// </summary>
        private void OnSkillApplied(SkillType skillType, int newLevel)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[SkillEffectManager] 收到技能应用: {skillType} -> Lv.{newLevel}");
            }
            
            switch (skillType)
            {
                // ========== 主动技能 ==========
                case SkillType.Prism:
                    ApplyPrismEffect(newLevel);
                    break;
                    
                case SkillType.Focus:
                    ApplyFocusEffect(newLevel);
                    break;
                    
                case SkillType.Impact:
                    ApplyImpactEffect(newLevel);
                    break;
                    
                case SkillType.Frost:
                    ApplyFrostEffect(newLevel);
                    break;
                    
                // ========== 被动技能 ==========
                case SkillType.Power:
                    ApplyPowerEffect(newLevel);
                    break;
                    
                case SkillType.Wide:
                    ApplyWideEffect(newLevel);
                    break;
                    
                // ========== 消耗品 ==========
                case SkillType.Repair:
                    ApplyRepairEffect();
                    break;
                    
                case SkillType.Charge:
                    ApplyChargeEffect();
                    break;
                    
                case SkillType.Adrenaline:
                    ApplyAdrenalineEffect();
                    break;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 主动技能效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 应用 Prism（折射棱镜）效果 - AOE 清怪
        /// </summary>
        private void ApplyPrismEffect(int level)
        {
            prismLevel = level;
            
            if (laserController != null)
            {
                laserController.SetPrismLevel(level);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillEffectManager] ✓ Prism Lv.{level} - 副激光已更新");
            }
        }
        
        /// <summary>
        /// 应用 Focus（聚能透镜）效果 - 单体攻坚
        /// </summary>
        private void ApplyFocusEffect(int level)
        {
            focusLevel = level;
            
            if (laserController != null)
            {
                laserController.SetFocusLevel(level);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillEffectManager] ✓ Focus Lv.{level} - 主激光已强化");
            }
        }
        
        /// <summary>
        /// 应用 Impact（冲击模块）效果 - 控制/防近身
        /// </summary>
        private void ApplyImpactEffect(int level)
        {
            impactLevel = level;
            
            // 计算击退力倍率
            float knockbackMultiplier = GetImpactKnockbackMultiplier(level);
            
            if (laserController != null)
            {
                laserController.SetKnockbackMultiplier(knockbackMultiplier);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillEffectManager] ✓ Impact Lv.{level} - 击退力 x{knockbackMultiplier:F2}");
            }
        }
        
        /// <summary>
        /// 应用 Frost（极寒光束）效果 - 减速辅助
        /// </summary>
        private void ApplyFrostEffect(int level)
        {
            frostLevel = level;
            
            // TODO: 实现减速效果（需要敌人系统支持）
            // 目前先记录等级，后续在 LaserController 的伤害逻辑中处理
            
            // 视觉：变蓝（与 Focus 的红色不叠加，优先 Focus）
            if (focusLevel == 0 && laserController != null)
            {
                Color frostColor = new Color(0.3f, 0.7f, 1f, 1f); // 冰蓝色
                laserController.SetLaserColor(frostColor);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillEffectManager] ✓ Frost Lv.{level} - 减速效果待实现");
            }
        }
        
        /// <summary>
        /// 获取 Impact 击退力倍率
        /// </summary>
        private float GetImpactKnockbackMultiplier(int level)
        {
            switch (level)
            {
                case 1: return 1.30f;
                case 2: return 1.60f;
                case 3: return 2.00f;
                case 4: return 2.50f;
                case 5: return 4.00f; // 可推开 BOSS
                default: return 1.0f;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 被动技能效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 应用 Power（功率超频）效果 - +DPS
        /// 每级增加 +20% 基础 DPS
        /// </summary>
        private void ApplyPowerEffect(int level)
        {
            powerLevel = level;
            
            // 计算总伤害加成（每级 +20%）
            totalDamageBonus = level * 0.20f;
            
            // 应用到激光（叠加到现有倍率上）
            UpdateDamageMultiplier();
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillEffectManager] ✓ Power Lv.{level} - 伤害 +{totalDamageBonus:P0}");
            }
        }
        
        /// <summary>
        /// 应用 Wide（广域透镜）效果 - +激光宽度
        /// 每级增加 +15% 激光宽度
        /// </summary>
        private void ApplyWideEffect(int level)
        {
            wideLevel = level;
            
            // 计算总宽度加成（每级 +15%）
            totalWidthBonus = level * 0.15f;
            
            // 应用到激光
            UpdateWidthMultiplier();
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillEffectManager] ✓ Wide Lv.{level} - 宽度 +{totalWidthBonus:P0}");
            }
        }
        
        /// <summary>
        /// 更新伤害倍率（考虑 Focus + Power + Adrenaline）
        /// </summary>
        private void UpdateDamageMultiplier()
        {
            if (laserController == null) return;
            
            // Focus 的伤害倍率（基础值）
            float focusMultiplier = GetFocusDamageMultiplier(focusLevel);
            
            // Power 的累加加成
            float powerBonus = totalDamageBonus;
            
            // 最终倍率 = Focus基础 + Power加成
            float finalMultiplier = focusMultiplier + powerBonus;
            
            laserController.SetDamageMultiplier(finalMultiplier);
        }
        
        /// <summary>
        /// 更新宽度倍率（考虑 Focus + Wide）
        /// </summary>
        private void UpdateWidthMultiplier()
        {
            if (laserController == null) return;
            
            // Focus 的宽度倍率（基础值，可能是缩小）
            float focusMultiplier = GetFocusWidthMultiplier(focusLevel);
            
            // Wide 的累加加成
            float wideBonus = totalWidthBonus;
            
            // 最终倍率 = Focus基础 * (1 + Wide加成)
            float finalMultiplier = focusMultiplier * (1f + wideBonus);
            
            laserController.SetWidthMultiplier(finalMultiplier);
        }
        
        /// <summary>
        /// 获取 Focus 伤害倍率
        /// </summary>
        private float GetFocusDamageMultiplier(int level)
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
        
        /// <summary>
        /// 获取 Focus 宽度倍率
        /// </summary>
        private float GetFocusWidthMultiplier(int level)
        {
            switch (level)
            {
                case 0: return 1.0f;
                case 1:
                case 2:
                    return 0.80f;
                case 3:
                case 4:
                case 5:
                    return 0.60f;
                default:
                    return 1.0f;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 消耗品效果
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 应用 Repair（紧急抢修）效果
        /// 立刻回复护盾并恢复1点生命
        /// </summary>
        private void ApplyRepairEffect()
        {
            // TODO: 需要 ShieldManager/HealthManager 支持
            // ShieldManager.Instance?.RestoreFullShield();
            // HealthManager.Instance?.RestoreHull(1);
            
            if (showDebugInfo)
            {
                Debug.Log("[SkillEffectManager] ✓ Repair - 护盾+生命恢复（待实现）");
            }
        }
        
        /// <summary>
        /// 应用 Charge（能量过载）效果
        /// 大招能量条 +50%
        /// </summary>
        private void ApplyChargeEffect()
        {
            if (ProgressManager.Instance != null)
            {
                int chargeAmount = ProgressManager.Instance.UltMaxEnergy / 2; // 50%
                ProgressManager.Instance.AddUltEnergy(chargeAmount);
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[SkillEffectManager] ✓ Charge - 大招能量 +50%");
            }
        }
        
        /// <summary>
        /// 应用 Adrenaline（肾上腺素）效果
        /// 恢复1点护盾，20秒内转速+50%、击退力+50%
        /// </summary>
        private void ApplyAdrenalineEffect()
        {
            // 恢复护盾
            // TODO: ShieldManager.Instance?.RestoreShield(1);
            
            // 激活 buff
            isAdrenalineActive = true;
            adrenalineTimer = ADRENALINE_DURATION;
            
            // 应用效果
            ApplyAdrenalineBuffs(true);
            
            if (showDebugInfo)
            {
                Debug.Log($"[SkillEffectManager] ✓ Adrenaline - 20秒增益开始");
            }
        }
        
        /// <summary>
        /// 更新 Adrenaline buff 状态
        /// </summary>
        private void UpdateAdrenalineBuff()
        {
            if (!isAdrenalineActive) return;
            
            adrenalineTimer -= Time.deltaTime;
            
            if (adrenalineTimer <= 0f)
            {
                isAdrenalineActive = false;
                ApplyAdrenalineBuffs(false);
                
                if (showDebugInfo)
                {
                    Debug.Log("[SkillEffectManager] Adrenaline buff 结束");
                }
            }
        }
        
        /// <summary>
        /// 应用/移除 Adrenaline buffs
        /// </summary>
        private void ApplyAdrenalineBuffs(bool active)
        {
            float multiplier = active ? 1.5f : 1.0f;
            
            // 转速 +50%
            if (towerRotation != null)
            {
                towerRotation.SetSpeedMultiplier(multiplier);
            }
            
            // 击退力 +50%（叠加到 Impact 效果上）
            // 这里需要重新计算击退力
            float baseKnockback = GetImpactKnockbackMultiplier(impactLevel);
            float finalKnockback = baseKnockback * multiplier;
            
            if (laserController != null)
            {
                laserController.SetKnockbackMultiplier(finalKnockback);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取当前 Frost 等级（用于敌人受击时判断减速）
        /// </summary>
        public int GetFrostLevel() => frostLevel;
        
        /// <summary>
        /// 获取 Frost 减速数据
        /// </summary>
        public void GetFrostData(out float slowPercent, out float duration)
        {
            switch (frostLevel)
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
                    duration = 1.0f; // Lv.5 有 20% 概率完全冰冻
                    break;
                default:
                    slowPercent = 0f;
                    duration = 0f;
                    break;
            }
        }
        
        /// <summary>
        /// Lv.5 Frost 是否触发完全冰冻（20% 概率）
        /// </summary>
        public bool TryFrostFreeze()
        {
            if (frostLevel >= 5)
            {
                return Random.value < 0.20f; // 20% 概率
            }
            return false;
        }
        
        /// <summary>
        /// 重置所有技能效果（新游戏开始时调用）
        /// </summary>
        public void ResetAllEffects()
        {
            prismLevel = 0;
            focusLevel = 0;
            impactLevel = 0;
            frostLevel = 0;
            powerLevel = 0;
            wideLevel = 0;
            
            totalDamageBonus = 0f;
            totalWidthBonus = 0f;
            
            isAdrenalineActive = false;
            adrenalineTimer = 0f;
            
            if (laserController != null)
            {
                laserController.ClearAllSubLasers();
                laserController.SetDamageMultiplier(1f);
                laserController.SetKnockbackMultiplier(1f);
                laserController.SetWidthMultiplier(1f);
                laserController.SetLaserColor(Color.white);
            }
            
            if (towerRotation != null)
            {
                towerRotation.SetSpeedMultiplier(1f);
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[SkillEffectManager] 所有技能效果已重置");
            }
        }
    }
}