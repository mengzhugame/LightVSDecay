// ============================================================
// TechTreeManager.cs
// 文件位置: Assets/Scripts/Logic/TechTree/TechTreeManager.cs
// 版本：v2（26节点完整版）
//
// 解锁时机：
//   通关第1章普通模式后，科技树解锁。
//   初始状态下4条分支的根节点无前置，直接可升级。
//   首次打开面板时，新手引导高亮4个根节点引导玩家加点。
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Data.Runtime;
using LightVsDecay.Data.SO;

namespace LightVsDecay.Logic.TechTree
{
    public enum TechUpgradeResult
    {
        Success,
        AlreadyMaxLevel,
        PrerequisiteNotMet,
        InsufficientGold,
        NodeNotFound,
        TechTreeLocked,
    }

    public class TechTreeManager : PersistentSingleton<TechTreeManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("配置")]
        [SerializeField] private TechTreeDatabase database;

        [Header("解锁条件（通关第几章普通难度后解锁科技树）")]
        [SerializeField] private int unlockAfterChapter = 1;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool forceUnlocked  = false; // 编辑器调试用

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>节点升级成功（nodeId, newLevel）</summary>
        public static event Action<string, int> OnNodeUpgraded;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private Dictionary<string, int> _nodeLevels = new Dictionary<string, int>();
        private TechTreeSaveData _saveData;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public TechTreeDatabase Database => database;

        /// <summary>科技树是否已对玩家解锁</summary>
        public bool IsTechTreeUnlocked
        {
            get
            {
                if (forceUnlocked) return true;
                // ★ 统一读取 SystemUnlockManager 存档标记（不再依赖通关记录）
                return SystemUnlockManager.Instance != null
                       && SystemUnlockManager.Instance.IsTechTreeUnlocked;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void Awake()
        {
            base.Awake();
            LoadFromSave();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 查询接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public int  GetNodeLevel(string nodeId) { _nodeLevels.TryGetValue(nodeId, out int lv); return lv; }
        public bool IsUnlocked(string nodeId)   => GetNodeLevel(nodeId) >= 1;

        public bool CanUpgrade(string nodeId)
        {
            if (!IsTechTreeUnlocked) return false;
            var data = database?.GetById(nodeId);
            if (data == null) return false;
            if (GetNodeLevel(nodeId) >= data.maxLevel) return false;
            return IsPrerequisiteMet(data);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 升级接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public TechUpgradeResult TryUpgradeNode(string nodeId)
        {
            if (!IsTechTreeUnlocked) return TechUpgradeResult.TechTreeLocked;

            var data = database?.GetById(nodeId);
            if (data == null) return TechUpgradeResult.NodeNotFound;

            int lv = GetNodeLevel(nodeId);
            if (lv >= data.maxLevel)     return TechUpgradeResult.AlreadyMaxLevel;
            if (!IsPrerequisiteMet(data)) return TechUpgradeResult.PrerequisiteNotMet;

            int cost = data.GetUpgradeCost(lv);
            if (ProgressManager.Instance == null || ProgressManager.Instance.GoldCoins < cost)
                return TechUpgradeResult.InsufficientGold;

            ProgressManager.Instance.ConsumeGoldCoins(cost);
            _nodeLevels[nodeId] = lv + 1;
            Save();

            int newLv = _nodeLevels[nodeId];
            if (showDebugInfo)
                Debug.Log($"[TechTree] {data.displayName} Lv.{newLv}  (-{cost} 金币)");

            OnNodeUpgraded?.Invoke(nodeId, newLv);
            return TechUpgradeResult.Success;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ★ 全局加成查询（供 PlayerStatsManager 调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        // ── 🔴 火力 ─────────────────────────────────────────

        /// <summary>攻击力 flat 加成（高压电容I）</summary>
        public float GetAttackBonus()            => SumEffect(TechEffectType.AttackBonus);

        /// <summary>攻击力百分比加成（高压电容II，如 5.0 = +5%）</summary>
        public float GetAttackBonusPercent()     => SumEffect(TechEffectType.AttackBonusPercent);

        /// <summary>暴击率加成（弱点解析，如 5.0 = +5%）</summary>
        public float GetCritRateBonus()          => SumEffect(TechEffectType.CritRate);

        /// <summary>暴击伤害加成（透镜校准，如 15.0 = +15%）</summary>
        public float GetCritDamageBonus()        => SumEffect(TechEffectType.CritDamage);

        /// <summary>对精英/Boss伤害加成（过载射击，如 9.0 = +9%）</summary>
        public float GetEliteDamageBonus()       => SumEffect(TechEffectType.EliteDamageBonus);

        /// <summary>穿透伤害衰减降低（高能穿透，如 30.0 = 衰减-30%）</summary>
        public float GetPenetrationDecayReduce() => SumEffect(TechEffectType.PenetrationDecayReduce);

        /// <summary>大招激光宽度+伤害加成（终极超载，0 or 50）</summary>
        public float GetOverloadSurgeBonus()     => SumEffect(TechEffectType.OverloadLaserWidth);

        // ── 🟢 生存 ─────────────────────────────────────────

        /// <summary>生命上限 flat 加成（复合装甲I）</summary>
        public int   GetMaxHPBonus()             => Mathf.RoundToInt(SumEffect(TechEffectType.MaxHP));

        /// <summary>生命上限百分比加成（复合装甲II，如 5.0 = +5%）</summary>
        public float GetMaxHPPercent()           => SumEffect(TechEffectType.MaxHPPercent);

        /// <summary>护盾恢复速度加成（护盾发生器，如 20.0 = +20%）</summary>
        public float GetShieldRecoveryBonus()    => SumEffect(TechEffectType.ShieldRecoveryRate);

        /// <summary>碰撞伤害减免（动能缓冲，如 10.0 = -10%）</summary>
        public float GetCollisionDamageReduction() => SumEffect(TechEffectType.CollisionDamageReduce);

        /// <summary>每局复活次数（备用系统）</summary>
        public int   GetReviveCount()            => Mathf.RoundToInt(SumEffect(TechEffectType.ReviveCount));

        /// <summary>大招回血（纳米修复）</summary>
        public bool  HasHealOnOverload()         => SumEffect(TechEffectType.HealOnOverload) >= 1f;

        /// <summary>护盾破碎释放脉冲（紧急防卫）</summary>
        public bool  HasShieldBurstOnBreak()     => SumEffect(TechEffectType.ShieldBurstOnBreak) >= 1f;

        // ── 🟡 资源 ─────────────────────────────────────────

        /// <summary>金币掉落率加成（幸运协议I+II合计，如 20.0 = +20%）</summary>
        public float GetCoinDropRateBonus()      => SumEffect(TechEffectType.CoinDropRate);

        /// <summary>离线收益加成（离线算力，如 30.0 = +30%）</summary>
        public float GetOfflineIncomeBonus()     => SumEffect(TechEffectType.OfflineIncome);

        /// <summary>每日免费刷新商店次数（商会声望）</summary>
        public int   GetShopFreeRefreshCount()   => Mathf.RoundToInt(SumEffect(TechEffectType.ShopFreeRefresh));

        /// <summary>第1波金币翻倍（初始启动资金）</summary>
        public bool  HasFirstWaveCoinDouble()    => SumEffect(TechEffectType.FirstWaveCoinDouble) >= 1f;

        /// <summary>赏金猎人（精英/Boss额外宝箱）</summary>
        public bool  HasBountyHunter()           => SumEffect(TechEffectType.BountyHunter) >= 1f;

        // ── 🔵 战术 ─────────────────────────────────────────

        /// <summary>大招充能速度加成（快速充能，如 20.0 = +20%）</summary>
        public float GetChargeRateBonus()        => SumEffect(TechEffectType.ChargeRate);

        /// <summary>击退距离加成（击退增强，如 30.0 = +30%）</summary>
        public float GetKnockbackBonus()         => SumEffect(TechEffectType.KnockbackDistance);

        /// <summary>击杀减大招冷却（杀戮回能，单位秒）</summary>
        public float GetKillCooldownReduction()  => SumEffect(TechEffectType.KillCooldownReduction);

        /// <summary>局内增益持续时间加成（战术延长，如 20.0 = +20%）</summary>
        public float GetBuffDurationBonus()      => SumEffect(TechEffectType.BuffDurationBonus);

        /// <summary>三选一技能免费刷新次数（黑客密钥）</summary>
        public int   GetSkillRerollCount()       => Mathf.RoundToInt(SumEffect(TechEffectType.SkillRerollCount));

        /// <summary>大招震晕全屏（启动冲击）</summary>
        public bool  HasOverloadStun()           => SumEffect(TechEffectType.OverloadStun) >= 1f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内部辅助
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private float SumEffect(TechEffectType effectType)
        {
            if (database == null) return 0f;
            float total = 0f;
            foreach (var node in database.AllNodes)
            {
                if (node == null || node.effectType != effectType) continue;
                int lv = GetNodeLevel(node.nodeId);
                if (lv > 0) total += node.GetTotalEffect(lv);
            }
            return total;
        }

        private bool IsPrerequisiteMet(TechTreeNodeData data)
        {
            if (data.prerequisiteNodeIds == null || data.prerequisiteNodeIds.Count == 0) return true;
            foreach (var id in data.prerequisiteNodeIds)
                if (GetNodeLevel(id) < data.prerequisiteMinLevel) // ★ 用配置值替代硬编码的 1
                    return false;
            return true;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 存档
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Save()
        {
            _saveData.nodeLevels.Clear();
            foreach (var kv in _nodeLevels)
                _saveData.nodeLevels.Add(new TechNodeLevelEntry(kv.Key, kv.Value));
            _saveData.Save();
        }

        private void LoadFromSave()
        {
            _saveData = TechTreeSaveData.Load();
            _nodeLevels.Clear();
            foreach (var e in _saveData.nodeLevels)
                if (!string.IsNullOrEmpty(e.nodeId)) _nodeLevels[e.nodeId] = e.level;

            if (showDebugInfo)
                Debug.Log($"[TechTree] 读档完成，已有节点: {_nodeLevels.Count} 个");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试工具
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [ContextMenu("Debug: Print All Bonuses")]
        public void DebugPrintBonuses()
        {
            Debug.Log(
                $"[TechTree] ==== 全部加成汇总 ====\n" +
                $"[火力] ATK flat+{GetAttackBonus()} pct+{GetAttackBonusPercent()}% " +
                $"CritR+{GetCritRateBonus()}% CritD+{GetCritDamageBonus()}% " +
                $"Elite+{GetEliteDamageBonus()}% Penetrate-{GetPenetrationDecayReduce()}% " +
                $"Overload+{GetOverloadSurgeBonus()}%\n" +
                $"[生存] HP+{GetMaxHPBonus()} HP+{GetMaxHPPercent()}% " +
                $"Shield+{GetShieldRecoveryBonus()}% Collision-{GetCollisionDamageReduction()}% " +
                $"Revive={GetReviveCount()} HealOverload={HasHealOnOverload()} Burst={HasShieldBurstOnBreak()}\n" +
                $"[资源] Coin+{GetCoinDropRateBonus()}% Offline+{GetOfflineIncomeBonus()}% " +
                $"Shop+{GetShopFreeRefreshCount()} FirstWave={HasFirstWaveCoinDouble()} Bounty={HasBountyHunter()}\n" +
                $"[战术] Charge+{GetChargeRateBonus()}% Knockback+{GetKnockbackBonus()}% " +
                $"KillCD+{GetKillCooldownReduction()}s Buff+{GetBuffDurationBonus()}% " +
                $"Reroll={GetSkillRerollCount()} Stun={HasOverloadStun()}"
            );
        }

        [ContextMenu("Debug: Force Unlock Tech Tree")]
        public void DebugForceUnlock() { forceUnlocked = true; }

        [ContextMenu("Debug: Reset All Nodes")]
        public void DebugResetAll() { TechTreeSaveData.Reset(); LoadFromSave(); }
    }
}