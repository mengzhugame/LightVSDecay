// ============================================================
// TechTreeNodeData.cs
// 文件位置: Assets/Scripts/Data/SO/TechTreeNodeData.cs
// 用途：科技树单个节点的 ScriptableObject 配置
// 版本：v2（补全 26 个节点所需的全部效果类型）
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace LightVsDecay.Data.SO
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 枚举定义
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>科技树分支（对应四个方向）</summary>
    public enum TechBranch
    {
        Firepower = 0,   // 🔴 上行 - 火力投射
        Survival  = 1,   // 🟢 下行 - 生存防护
        Resources = 2,   // 🟡 左行 - 资源循环
        Tactics   = 3,   // 🔵 右行 - 战术辅助
    }

    /// <summary>节点类型</summary>
    public enum TechNodeType
    {
        Small     = 0,   // 小节点（可多级升）
        Milestone = 1,   // 大节点（里程碑，通常 maxLevel=1）
    }

    /// <summary>节点效果类型（全 26 节点覆盖）</summary>
    public enum TechEffectType
    {
        // ── 火力 (Firepower) ──────────────────────────
        AttackBonus             = 0,   // 高压电容I：攻击力 flat +N/级
        CritRate                = 1,   // 弱点解析：暴击率 +N%/级
        CritDamage              = 2,   // 透镜校准：暴击伤害 +N%/级
        OverloadLaserWidth      = 3,   // 终极超载：大招激光宽度+伤害 +50%（里程碑）
        AttackBonusPercent      = 4,   // 高压电容II：攻击力百分比 +N%/级（乘算）
        EliteDamageBonus        = 5,   // 过载射击：对精英怪伤害 +N%/级
        PenetrationDecayReduce  = 6,   // 高能穿透：穿透衰减降低 N%/级

        // ── 生存 (Survival) ──────────────────────────
        MaxHP                   = 10,  // 复合装甲I：生命上限 flat +N/级
        ShieldRecoveryRate      = 11,  // 护盾发生器：护盾恢复速度 +N%/级
        CollisionDamageReduce   = 12,  // 动能缓冲：碰撞伤害减免 +N%/级
        ReviveCount             = 13,  // 备用系统：复活次数 +1（里程碑）
        HealOnOverload          = 14,  // 纳米修复：大招回血（里程碑）
        MaxHPPercent            = 15,  // 复合装甲II：生命上限百分比 +N%/级（乘算）
        ShieldBurstOnBreak      = 16,  // 紧急防卫：护盾破碎时震退敌人（里程碑）

        // ── 资源 (Resources) ─────────────────────────
        CoinDropRate            = 20,  // 幸运协议I/II：金币掉落 +N%/级
        OfflineIncome           = 21,  // 离线算力：离线收益 +N%/级
        ShopFreeRefresh         = 22,  // 商会声望：每日免费刷新商店 +1/级
        FirstWaveCoinDouble     = 23,  // 初始启动资金：第1波金币翻倍（里程碑）
        BountyHunter            = 24,  // 赏金猎人：击杀精英/Boss掉落宝箱（里程碑）

        // ── 战术 (Tactics) ───────────────────────────
        ChargeRate              = 30,  // 快速充能：大招充能速度 +N%/级
        KnockbackDistance       = 31,  // 击退增强：击退距离 +N%/级
        KillCooldownReduction   = 32,  // 杀戮回能：击杀减大招冷却 +Ns/级
        SkillRerollCount        = 33,  // 黑客密钥：三选一免费刷新 +1（里程碑）
        OverloadStun            = 34,  // 启动冲击：大招震晕全屏（里程碑）
        BuffDurationBonus       = 35,  // 战术延长：局内增益持续时间 +N%
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // ScriptableObject
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 科技树节点数据（ScriptableObject）
    /// 创建路径：Assets → Create → LightVsDecay → TechTree → Node Data
    /// </summary>
    [CreateAssetMenu(
        fileName = "TechNode_New",
        menuName  = "LightVsDecay/TechTree/Node Data",
        order     = 20)]
    public class TechTreeNodeData : ScriptableObject
    {
        [Header("═══ 基础信息 ═══")]
        [Tooltip("唯一 ID（英文，不可重复）")]
        public string nodeId;

        [Tooltip("显示名称")]
        public string displayName;

        [Tooltip("节点图标（可选）")]
        public Sprite icon;

        [Tooltip("所属分支")]
        public TechBranch branch;

        [Tooltip("节点类型")]
        public TechNodeType nodeType;

        [Header("═══ 效果配置 ═══")]
        [Tooltip("效果类型")]
        public TechEffectType effectType;

        [Tooltip("每级效果数值（含义见 TechEffectType 注释）")]
        public float effectValuePerLevel = 1f;

        [Tooltip("最大等级（里程碑通常为 1）")]
        public int maxLevel = 1;

        [Header("═══ 升级费用 ═══")]
        [Tooltip("Lv0→1 的基础费用（金币）")]
        public int baseCost = 200;

        [Tooltip("指数增长系数（1.05~1.1）")]
        public float growthFactor = 1.07f;

        [Header("═══ 前置条件 ═══")]
        [Tooltip("前置节点 ID 列表（全部 ≥Lv1 才可解锁本节点）")]
        public List<string> prerequisiteNodeIds = new List<string>();
        [Tooltip("前置节点需要达到的等级（默认1，即升一级即可解锁）")]
        public int prerequisiteMinLevel = 1;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>计算升级到下一级所需金币（指数公式）</summary>
        public int GetUpgradeCost(int currentLevel)
        {
            if (currentLevel >= maxLevel) return 0;
            float cost = baseCost * Mathf.Pow(growthFactor, currentLevel);
            return Mathf.Max(1, Mathf.RoundToInt(cost));
        }

        /// <summary>获取当前等级的累计效果值</summary>
        public float GetTotalEffect(int level) => effectValuePerLevel * level;

        /// <summary>构建节点 UI 描述文本（富文本，支持当前等级对比）</summary>
        public string GetDescription(int currentLevel)
        {
            float next = GetTotalEffect(currentLevel + 1);

            string effectStr = effectType switch
            {
                // ── 火力 ──
                TechEffectType.AttackBonus            => $"攻击力 +<color=#FF6666>{next:F0}</color>",
                TechEffectType.CritRate               => $"暴击率 +<color=#FF6666>{next:F1}%</color>",
                TechEffectType.CritDamage             => $"暴击伤害 +<color=#FF6666>{next:F0}%</color>",
                TechEffectType.OverloadLaserWidth     => "<color=#FF6666>大招激光宽度+伤害 各 +50%</color>",
                TechEffectType.AttackBonusPercent     => $"攻击力 +<color=#FF6666>{next:F0}%</color>（百分比）",
                TechEffectType.EliteDamageBonus       => $"对精英怪/Boss伤害 +<color=#FF6666>{next:F0}%</color>",
                TechEffectType.PenetrationDecayReduce => $"穿透伤害衰减降低 <color=#FF6666>{next:F0}%</color>",
                // ── 生存 ──
                TechEffectType.MaxHP                  => $"生命上限 +<color=#66FF66>{next:F0}</color>",
                TechEffectType.ShieldRecoveryRate     => $"护盾恢复速度 +<color=#66FF66>{next:F0}%</color>",
                TechEffectType.CollisionDamageReduce  => $"碰撞伤害减少 <color=#66FF66>{next:F0}%</color>",
                TechEffectType.ReviveCount            => "每局复活次数 +<color=#66FF66>1</color>",
                TechEffectType.HealOnOverload         => "开大招瞬间回复 <color=#66FF66>20%</color> 血量",
                TechEffectType.MaxHPPercent           => $"生命上限 +<color=#66FF66>{next:F0}%</color>（百分比）",
                TechEffectType.ShieldBurstOnBreak     => "护盾破碎时释放<color=#66FF66>脉冲震退</color>周围敌人",
                // ── 资源 ──
                TechEffectType.CoinDropRate           => $"金币掉落 +<color=#FFD700>{next:F0}%</color>",
                TechEffectType.OfflineIncome          => $"离线收益 +<color=#FFD700>{next:F0}%</color>",
                TechEffectType.ShopFreeRefresh        => "每日免费刷新商店 +<color=#FFD700>1</color>",
                TechEffectType.FirstWaveCoinDouble    => "开局第1波金币掉落<color=#FFD700>翻倍</color>",
                TechEffectType.BountyHunter           => "击杀精英/Boss额外掉落<color=#FFD700>资源宝箱</color>",
                // ── 战术 ──
                TechEffectType.ChargeRate             => $"大招充能速度 +<color=#66CCFF>{next:F0}%</color>",
                TechEffectType.KnockbackDistance      => $"击退距离 +<color=#66CCFF>{next:F0}%</color>",
                TechEffectType.KillCooldownReduction  => $"击杀减少大招冷却 +<color=#66CCFF>{next:F2}s</color>",
                TechEffectType.SkillRerollCount       => "三选一免费刷新次数 +<color=#66CCFF>1</color>",
                TechEffectType.OverloadStun           => "开大招震晕全屏敌人 <color=#66CCFF>1.5s</color>",
                TechEffectType.BuffDurationBonus      => $"局内增益持续时间 +<color=#66CCFF>{next:F0}%</color>",
                _                                     => $"+{next}",
            };

            // 附带当前等级数值对比
            if (currentLevel > 0)
            {
                float cur = GetTotalEffect(currentLevel);
                string curStr = effectType switch
                {
                    TechEffectType.CritRate              => $"+{cur:F1}%",
                    TechEffectType.CritDamage            => $"+{cur:F0}%",
                    TechEffectType.CollisionDamageReduce => $"{cur:F0}%",
                    TechEffectType.KillCooldownReduction => $"+{cur:F2}s",
                    TechEffectType.ShieldRecoveryRate    => $"+{cur:F0}%",
                    TechEffectType.CoinDropRate          => $"+{cur:F0}%",
                    TechEffectType.OfflineIncome         => $"+{cur:F0}%",
                    TechEffectType.ChargeRate            => $"+{cur:F0}%",
                    TechEffectType.KnockbackDistance     => $"+{cur:F0}%",
                    TechEffectType.AttackBonusPercent    => $"+{cur:F0}%",
                    TechEffectType.EliteDamageBonus      => $"+{cur:F0}%",
                    TechEffectType.PenetrationDecayReduce=> $"{cur:F0}%",
                    TechEffectType.MaxHPPercent          => $"+{cur:F0}%",
                    TechEffectType.BuffDurationBonus     => $"+{cur:F0}%",
                    _                                    => $"+{cur:F0}",
                };
                effectStr += $"\n<color=#888888>当前：{curStr}</color>";
            }
            return effectStr;
        }
    }
}