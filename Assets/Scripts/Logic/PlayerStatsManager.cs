// ============================================================
// PlayerStatsManager.cs
// 文件位置: Assets/Scripts/Logic/PlayerStatsManager.cs
// 用途：玩家属性中枢管理器
//
// 架构说明：
//   所有属性来源（装备系统 + 科技树）统一汇总于此。
//   TurretHealth / LaserController / ShieldController 等
//   战斗组件只从 PlayerStatsManager 读取最终数值。
//   未来接入服务器时，仅需同步此 Manager 的数据。
//
// 数值计算规则：
//   最终值 = 基础值（GameSettings）+ 装备加成 + 科技树加成
//   暴击率 = baseCritRate + equip.critBonus + techTree.CritRate/100
//   暴击伤害倍率 = baseCritDmgMult × (1 + techTree.CritDamage/100)
// ============================================================

using System;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Equipment;
using LightVsDecay.Logic.TechTree;

namespace LightVsDecay.Logic
{
    /// <summary>
    /// 玩家属性中枢管理器（PersistentSingleton）
    /// 挂载到 PersistentManagers 下
    /// </summary>
    public class PlayerStatsManager : PersistentSingleton<PlayerStatsManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("基础配置（来自 GameSettings）")]
        [SerializeField] private GameSettings gameSettings;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>任意属性变化时触发（供属性面板刷新）</summary>
        public static event Action OnStatsChanged;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void Awake()
        {
            base.Awake();
        }

        private void OnEnable()
        {
            // 订阅装备/科技树变化事件，自动通知监听者刷新
            EquipmentManager.OnInventoryChanged      += NotifyChanged;
            EquipmentManager.OnEquipmentSlotChanged  += _ => NotifyChanged();
            TechTreeManager.OnNodeUpgraded           += (_, __) => NotifyChanged();
        }

        private void OnDisable()
        {
            EquipmentManager.OnInventoryChanged     -= NotifyChanged;
            EquipmentManager.OnEquipmentSlotChanged -= _ => NotifyChanged();
            TechTreeManager.OnNodeUpgraded          -= (_, __) => NotifyChanged();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ★ 对外属性接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        // ── 生命值 ────────────────────────────────────────────

        /// <summary>基础最大生命值（来自 GameSettings）</summary>
        public int BaseMaxHP => gameSettings != null ? gameSettings.maxHullHP : 1000;

        /// <summary>装备贡献的生命值加成</summary>
        public int EquipmentHPBonus => EquipStats.hpBonus;

        /// <summary>科技树贡献的生命值加成</summary>
        public int TechTreeHPBonus => TechMgr?.GetMaxHPBonus() ?? 0;

        /// <summary>★ 最终最大生命值（战斗初始化时读取）</summary>
        public int FinalMaxHP => BaseMaxHP + EquipmentHPBonus + TechTreeHPBonus;

        // ── 护盾 ─────────────────────────────────────────────

        /// <summary>基础最大护盾值（来自 GameSettings）</summary>
        public int BaseMaxShield => gameSettings != null ? gameSettings.maxShieldHP : 300;

        /// <summary>装备贡献的护盾值加成</summary>
        public int EquipmentShieldBonus => EquipStats.shieldBonus;

        /// <summary>★ 最终最大护盾值</summary>
        public int FinalMaxShield => BaseMaxShield + EquipmentShieldBonus;

        /// <summary>★ 护盾恢复速度加成百分比（来自科技树，如 10.0 = 10%）</summary>
        public float FinalShieldRecoveryBonus => TechMgr?.GetShieldRecoveryBonus() ?? 0f;

        // ── 攻击力（平台 DPS 加成）────────────────────────────

        /// <summary>基础 DPS（来自 GameSettings）</summary>
        public float BaseDPS => gameSettings != null ? gameSettings.baseDPS : 100f;

        /// <summary>装备贡献的攻击力加成（flat，叠加到 DPS）</summary>
        public int EquipmentAttackBonus => EquipStats.attackBonus;

        /// <summary>科技树贡献的攻击力加成（flat）</summary>
        public float TechTreeAttackBonus => TechMgr?.GetAttackBonus() ?? 0f;

        /// <summary>★ 最终攻击力加成（flat，初始化时注入 LaserDamageCalculator）</summary>
        public float FinalFlatAttackBonus => EquipmentAttackBonus + TechTreeAttackBonus;

        // ── 暴击率 ────────────────────────────────────────────

        /// <summary>基础暴击率（0.1 = 10%）</summary>
        public float BaseCritRate => gameSettings != null ? gameSettings.baseCritRate : 0.1f;

        /// <summary>装备贡献暴击率（decimal，如 0.05 = 5%）</summary>
        public float EquipmentCritRateBonus => EquipStats.critBonus;

        /// <summary>科技树贡献暴击率（decimal，如 0.05 = 5%）</summary>
        public float TechTreeCritRateBonus => (TechMgr?.GetCritRateBonus() ?? 0f) / 100f;

        /// <summary>★ 最终暴击率（decimal，如 0.15 = 15%）</summary>
        public float FinalCritRate => Mathf.Clamp01(BaseCritRate + EquipmentCritRateBonus + TechTreeCritRateBonus);

        // ── 暴击伤害倍率 ──────────────────────────────────────

        /// <summary>基础暴击倍率（2.0 = 200%）</summary>
        public float BaseCritDamageMultiplier => gameSettings != null ? gameSettings.critDamageMultiplier : 2.0f;

        /// <summary>科技树贡献暴击伤害加成（如 15.0 = +15%）</summary>
        public float TechTreeCritDamageBonus => TechMgr?.GetCritDamageBonus() ?? 0f;

        /// <summary>★ 最终暴击伤害倍率（如 2.0 × 1.15 = 2.3）</summary>
        public float FinalCritDamageMultiplier => BaseCritDamageMultiplier * (1f + TechTreeCritDamageBonus / 100f);

        // ── 充能效率 ──────────────────────────────────────────

        /// <summary>装备充能加成（decimal）</summary>
        public float EquipmentChargeBonus => EquipStats.chargeBonus;

        /// <summary>科技树充能速度加成（decimal）</summary>
        public float TechTreeChargeBonus => (TechMgr?.GetChargeRateBonus() ?? 0f) / 100f;

        /// <summary>★ 最终充能加成（decimal）</summary>
        public float FinalChargeBonus => EquipmentChargeBonus + TechTreeChargeBonus;

        // ── 其他战斗属性 ──────────────────────────────────────

        /// <summary>★ 碰撞伤害减免百分比（如 4.0 = 减免 4%）</summary>
        public float FinalCollisionDamageReduction => TechMgr?.GetCollisionDamageReduction() ?? 0f;

        /// <summary>★ 击退距离加成百分比</summary>
        public float FinalKnockbackBonus => TechMgr?.GetKnockbackBonus() ?? 0f;

        /// <summary>★ 击杀减少大招冷却（秒）</summary>
        public float FinalKillCooldownReduction => TechMgr?.GetKillCooldownReduction() ?? 0f;

        /// <summary>★ 金币掉落率加成百分比</summary>
        public float FinalCoinDropRateBonus => TechMgr?.GetCoinDropRateBonus() ?? 0f;

        // ── 里程碑特殊效果 ────────────────────────────────────

        /// <summary>★ 每局复活次数</summary>
        public int FinalReviveCount => TechMgr?.GetReviveCount() ?? 0;

        /// <summary>★ 是否解锁大招回血</summary>
        public bool HasHealOnOverload => TechMgr?.HasHealOnOverload() ?? false;

        /// <summary>★ 是否解锁大招震晕</summary>
        public bool HasOverloadStun => TechMgr?.HasOverloadStun() ?? false;

        /// <summary>★ 大招激光宽度/伤害加成百分比（0 或 50）</summary>
        public float OverloadSurgeBonus => TechMgr?.GetOverloadSurgeBonus() ?? 0f;

        /// <summary>★ 三选一技能免费刷新次数</summary>
        public int FinalSkillRerollCount => TechMgr?.GetSkillRerollCount() ?? 0;

        /// <summary>★ 开局第1波金币翻倍</summary>
        public bool HasFirstWaveCoinDouble => TechMgr?.HasFirstWaveCoinDouble() ?? false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内部辅助
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private TechTreeManager  TechMgr => TechTreeManager.Instance;

        private Data.SO.EquipmentStats EquipStats
        {
            get
            {
                if (EquipmentManager.Instance == null) return new Data.SO.EquipmentStats();
                return EquipmentManager.Instance.GetTotalStats();
            }
        }

        private void NotifyChanged()
        {
            OnStatsChanged?.Invoke();

            if (showDebugInfo)
                GameLogger.Log($"[PlayerStatsManager] 属性更新 → HP:{FinalMaxHP} " +
                          $"ATK+{FinalFlatAttackBonus} CritR:{FinalCritRate * 100:F1}% " +
                          $"CritD:{FinalCritDamageMultiplier:F2}x Shield:{FinalMaxShield}");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [ContextMenu("Debug: Print Final Stats")]
        public void DebugPrintStats()
        {
            GameLogger.Log($"[PlayerStats] ===== 最终属性 =====\n" +
                      $"HP:      {BaseMaxHP} + {EquipmentHPBonus}(装) + {TechTreeHPBonus}(科) = {FinalMaxHP}\n" +
                      $"Shield:  {BaseMaxShield} + {EquipmentShieldBonus}(装) = {FinalMaxShield}\n" +
                      $"ATK:     flat +{FinalFlatAttackBonus}(装{EquipmentAttackBonus}+科{TechTreeAttackBonus})\n" +
                      $"CritR:   {BaseCritRate * 100:F1}% + {EquipmentCritRateBonus * 100:F1}%(装) + {TechTreeCritRateBonus * 100:F1}%(科) = {FinalCritRate * 100:F1}%\n" +
                      $"CritD:   {BaseCritDamageMultiplier:F2}x × (1+{TechTreeCritDamageBonus:F0}%) = {FinalCritDamageMultiplier:F2}x\n" +
                      $"Charge:  +{FinalChargeBonus * 100:F1}%  Knockback: +{FinalKnockbackBonus:F0}%\n" +
                      $"Revive:  {FinalReviveCount}  SkillReroll: {FinalSkillRerollCount}\n" +
                      $"HealOverload: {HasHealOnOverload}  Stun: {HasOverloadStun}");
        }
    }
}