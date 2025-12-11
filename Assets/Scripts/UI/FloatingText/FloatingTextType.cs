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
        BossCore
    }
}