// ============================================================
// FloatingTextType.cs
// 文件位置: Assets/Scripts/UI/FloatingText/FloatingTextType.cs
// 用途：飘字类型枚举定义
// ============================================================

namespace LightVsDecay.UI.FloatingText
{
    /// <summary>
    /// 飘字类型枚举
    /// </summary>
    public enum FloatingTextType
    {
        /// <summary>普通伤害 - 白色小字，快速消失</summary>
        Normal,
        
        /// <summary>暴击伤害 - 红色大字，弹跳动画，爆炸图标</summary>
        Crit,
        
        /// <summary>状态文本 - 黄色，如 STUN!, BLOCK</summary>
        Status,
        
        /// <summary>Boss护甲伤害 - 银灰色小字，盾牌图标</summary>
        BossShield,
        
        /// <summary>Boss核心伤害 - 红色大字，眼睛图标（可选）</summary>
        BossCore,
        
        // ═══ 【新增】玩家受击飘字类型 ═══
        
        /// <summary>玩家血量受伤 - 红色</summary>
        PlayerHealthDamage,
        
        /// <summary>玩家护盾受伤 - 青色</summary>
        PlayerShieldDamage,
        
        /// <summary>玩家血量恢复 - 绿色</summary>
        PlayerHealthRestore,
        
        /// <summary>玩家护盾恢复 - 青色+</summary>
        PlayerShieldRestore,
        // ═══ 【新增】无人机奖励飘字类型 ═══

        /// <summary>补给无人机奖励 - 蓝绿色</summary>
        DroneSupply,

        /// <summary>问号无人机奖励 - 根据结果变色</summary>
        DroneGacha,

        /// <summary>契约无人机奖励 - 代价+收益双行</summary>
        DroneDeal,
        // ═══ 【新增】碎冰伤害飘字类型 ═══
        
        /// <summary>碎冰伤害 - 亮蓝色，比普通稍大</summary>
        Shatter,
        
        /// <summary>碎冰暴击 - 冰蓝色+红描边，大号</summary>
        ShatterCrit,
        
        /// <summary>处决 - 特殊文字 "EXECUTE!"</summary>
        Execution,

        /// <summary>连锁闪电每跳伤害 - 电弧黄，小字快闪</summary>
        Chain,

        /// <summary>爆炸/AoE 伤害 - 烈焰橙，中字扩散</summary>
        Explosion
    }
}