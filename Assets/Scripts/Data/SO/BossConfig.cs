// ============================================================
// BossConfig.cs
// 文件位置: Assets/Scripts/Data/SO/BossConfig.cs
// 用途：Boss 行为配置 (ScriptableObject)
// 【重构】分离 Charge(快招) 和 Press(慢招) 配置
// ============================================================

using UnityEngine;

namespace LightVsDecay.Data.SO
{
    /// <summary>
    /// Boss 行为配置
    /// 包含所有状态的时间、参数配置
    /// </summary>
    [CreateAssetMenu(fileName = "BossConfig", menuName = "LightVsDecay/Boss Config", order = 3)]
    public class BossConfig : ScriptableObject
    {
        [Header("血量设置")]
        [Tooltip("Boss 最大血量")]
        public float maxHealth = 10000f;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Spawn (入场) 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ Spawn (入场) ═══")]
        [Tooltip("入场下沉时长")]
        public float spawnDuration = 2.5f;
        
        [Tooltip("战斗锚点Y坐标（屏幕上方）")]
        public float battleAnchorY = 3.0f;
        
        [Tooltip("入场后震动强度")]
        public float spawnShakeIntensity = 0.5f;
        
        [Tooltip("入场后震动时长")]
        public float spawnShakeDuration = 0.5f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Idle (待机) 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ Idle (待机) ═══")]
        [Tooltip("普通Idle时长范围 - 最小")]
        public float idleDurationMin = 3.0f;
        
        [Tooltip("普通Idle时长范围 - 最大")]
        public float idleDurationMax = 5.0f;
        
        [Tooltip("狂暴Idle时长范围 - 最小（血量<30%时）")]
        public float rageIdleDurationMin = 1.5f;
        
        [Tooltip("狂暴Idle时长范围 - 最大（血量<30%时）")]
        public float rageIdleDurationMax = 2.5f;
        
        [Tooltip("触发狂暴的血量百分比")]
        [Range(0f, 1f)]
        public float rageHealthThreshold = 0.3f;
        
        [Tooltip("水平游走速度")]
        public float idleMoveSpeed = 1.5f;
        
        [Tooltip("水平游走范围（屏幕宽度百分比）")]
        [Range(0.5f, 1f)]
        public float idleMoveRangePercent = 0.8f;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Enrage (狂暴状态) 配置【新增】
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ Enrage 狂暴状态 ═══")]
        [Tooltip("狂暴时移速倍率")]
        public float rageMoveSpeedMultiplier = 1.2f;

        [Tooltip("狂暴时污秽球数量")]
        public int ragePollutionBurstCount = 5;

        [Tooltip("狂暴时召唤的Rusher移速加成")]
        public float rageRusherSpeedBonus = 1.25f;

        [Tooltip("狂暴时控制效果（僵直/冰冻）持续时间倍率")]
        [Range(0.25f, 1f)]
        public float rageControlDurationMultiplier = 0.5f;

        [Header("═══ 狂暴触发演出 ═══")]
        [Tooltip("狂暴触发时的屏幕震动强度")]
        public float enrageTriggerShakeIntensity = 0.6f;

        [Tooltip("狂暴触发时的屏幕震动时长")]
        public float enrageTriggerShakeDuration = 0.8f;

        [Tooltip("狂暴触发时的短暂停顿时长")]
        public float enrageTriggerPauseDuration = 0.5f;
        [Tooltip("狂暴常驻红光透明度")]
        [Range(0.1f, 1f)]
        public float enrageRedGlowAlpha = 0.4f;
        [Tooltip("狂暴时BGM播放速度（pitch）")]
        [Range(1.0f, 1.5f)]
        public float rageBGMPitch = 1.15f;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Summon (召唤爪牙) - 被动技能
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ Summon 召唤爪牙 (被动技能) ═══")]
        [Tooltip("召唤技能冷却时间（秒）")]
        public float summonCooldown = 15f;
        
        [Tooltip("狂暴时召唤冷却（秒）")]
        public float summonCooldownRage = 10f;
        
        [Tooltip("召唤动画时长（身体收缩）")]
        public float summonDuration = 1.0f;
        
        [Tooltip("身体震动强度")]
        public float summonShakeIntensity = 0.1f;
        
        [Tooltip("身体震动频率")]
        public float summonShakeFrequency = 30f;
        
        [Tooltip("左侧生成偏移（相对BOSS位置）")]
        public Vector2 summonLeftOffset = new Vector2(-3f, -1f);
        
        [Tooltip("右侧生成偏移（相对BOSS位置）")]
        public Vector2 summonRightOffset = new Vector2(3f, -1f);
        
        [Tooltip("每侧生成的 Rusher 数量")]
        public int summonRusherPerSide = 4;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Pollution (污秽喷吐) - 被动技能
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ Pollution 污秽喷吐 (被动技能) ═══")]
        [Tooltip("Idle 状态下发射间隔（秒）")]
        public float pollutionInterval = 2f;
        [Tooltip("每次发射的污秽球数量")]
        public int pollutionBurstCount = 3;
        [Tooltip("散射角度范围（度）")]
        public float pollutionSpreadAngle = 30f;
        [Tooltip("投射物飞行速度")]
        public float pollutionSpeed = 5f;
        
        [Tooltip("惰性追踪转向速度（度/秒）")]
        public float pollutionTurnSpeed = 90f;
        
        [Tooltip("投射物命中护盾伤害")]
        public int pollutionShieldDamage = 100;
        
        [Tooltip("投射物 Prefab")]
        public GameObject pollutionProjectilePrefab;
        
        [Tooltip("投射物生命周期（秒）")]
        public float pollutionLifetime = 8f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Charge (野蛮冲撞) - 主动技能A【快招】
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ Charge 野蛮冲撞 (快招/反应测试) ═══")]
        [Tooltip("蓄力预警时长（眼睛睁开+红光闪烁）")]
        public float chargeTelegraphDuration = 1.0f;
        
        [Tooltip("蓄力后退距离（像拉弓）")]
        public float chargeWindupDistance = 0.5f;
        
        [Tooltip("冲锋动画时长（Lerp移动）")]
        public float chargeDashDuration = 0.3f;
        
        [Tooltip("冲锋目标Y坐标（塔的位置）")]
        public float chargeTargetY = -10f;
        
        [Tooltip("撞击玩家伤害")]
        public float chargeHitDamage = 300f;
        
        [Tooltip("撞击屏幕震动强度")]
        public float chargeHitShakeIntensity = 0.8f;
        
        [Tooltip("撞击屏幕震动时长")]
        public float chargeHitShakeDuration = 0.3f;
        
        [Tooltip("冲撞后弹回时长")]
        public float chargeBounceBackDuration = 0.5f;
        
        [Tooltip("打断需要的Impact等级（5=仅Lv.5和大招可打断）")]
        public int chargeInterruptRequiredLevel = 5;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Press (重力碾压) - 主动技能B【慢招】
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ Press 重力碾压 (慢招/物理角力) ═══")]
        [Tooltip("Phase 1: 突进时长（瞬移/极速冲到塔前）")]
        public float pressJumpDuration = 0.5f;
        
        [Tooltip("突进目标Y坐标（塔前1米）")]
        public float pressHoverY = -5f;
        
        [Tooltip("Phase 2: 施压时长（眼睛缓慢睁开）")]
        public float pressGlareDuration = 1.5f;
        
        [Tooltip("碾压下压力（Boss向下的力）")]
        public float pressForce = 100f;
        
        [Tooltip("狂暴时碾压力增强倍率")]
        public float pressForceRageMultiplier = 1.2f;
        
        [Tooltip("安全线Y坐标（推回这里算玩家胜利）")]
        public float pressSafeLineY = 3.5f;
        
        [Tooltip("撞击线Y坐标（Boss到达这里算玩家失败）")]
        public float pressHitLineY = -10f;
        
        [Tooltip("角力最大持续时间（防止卡住）")]
        public float pressMaxDuration = 15f;
        
        [Tooltip("被推住后的僵直时长（奖励时间）")]
        public float pressCounterStunDuration = 3.0f;
        
        [Tooltip("角力前召唤小怪数量（0=不召唤）")]
        public int pressSummonCount = 0;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 角力物理 (Press 专用)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 角力物理 (Press专用) ═══")]
        [Tooltip("普通激光对Boss的基础推力")]
        public float baseLaserPushForce = 120f;
        
        [Tooltip("Impact技能等级的推力倍率 [Lv0, Lv1, Lv2, Lv3, Lv4, Lv5]")]
        public float[] impactPushMultipliers = new float[] { 0.4f, 0.85f, 1.0f, 1.3f, 1.6f, 2.5f };
        [Tooltip("Wide技能等级的基础推力倍率 [Lv0, Lv1, Lv2, Lv3, Lv4, Lv5]")]
        public float[] widePushMultipliers = new float[] { 0.4f, 0.52f, 0.64f, 0.76f, 0.88f, 1.0f };
        [Tooltip("总推力上限（防止多激光叠加过强）")]
        public float maxTotalPushForce = 300f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 角力摩擦伤害 (Press 专用) 【新增】
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 角力摩擦伤害 ═══")]
        [Tooltip("Boss 压在护盾上时的每秒伤害")]
        public float frictionDamagePerSecond = 50f;

        [Tooltip("最大角力持续时间（超时后 Boss 强制撤退）")]
        public float maxClashDuration = 6f;

        [Tooltip("摩擦伤害触发的 Y 坐标阈值（低于此值开始造成伤害）")]
        public float frictionTriggerY = -8.5f;

        [Tooltip("Boss 疲劳撤退后的僵直时长（奖励输出时间）")]
        public float exhaustedStunDuration = 2.5f;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Stun (僵直) 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ Stun (僵直) ═══")]
        [Tooltip("Charge被打断后的僵直时长")]
        public float chargeInterruptStunDuration = 3.0f;
        
        [Tooltip("僵直时颜色变暗程度")]
        [Range(0f, 1f)]
        public float stunDarkenAmount = 0.5f;
        
        [Tooltip("僵直结束后是否飞回战斗锚点（false=直接Idle）")]
        public bool stunReturnToAnchor = true;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】冰冻系统 (Frost)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 冰冻系统 (Frost) ═══")]
        [Tooltip("Boss 冰冻触发的累计照射时间阈值（秒）")]
        public float bossFrostFreezeThreshold = 1.0f;

        [Tooltip("Boss 冰冻基础持续时间（秒）")]
        public float bossFrostFreezeDuration = 2.0f;

        [Tooltip("冰冻结束后回到的状态（true=Idle，false=尝试恢复之前状态）")]
        public bool frozenReturnToIdle = true;
        [Header("═══ Boss减速效果 ═══")]
        [Tooltip("Boss受到减速效果的削弱系数（0.5 = 减速效果只有50%）")]
        [Range(0f, 1f)]
        public float bossSlowEffectMultiplier = 0.5f;

        [Tooltip("Boss减速视觉染色")]
        public Color bossSlowTintColor = new Color(0.7f, 0.85f, 1f, 1f);
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】霸体系统 (Unstoppable)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 霸体系统 (Unstoppable) ═══")]
        [Tooltip("霸体持续时间（秒）")]
        public float unstoppableDuration = 6.0f;

        [Tooltip("霸体期间 Press 推力效果倍率（0.3 = 30%效果）")]
        [Range(0f, 1f)]
        public float unstoppablePushMultiplier = 0.3f;

        [Tooltip("控制递减 - 第2次控制的效果倍率（0.5 = 50%时长）")]
        [Range(0f, 1f)]
        public float controlDiminish2nd = 0.5f;

        [Tooltip("控制递减 - 第3次控制的效果倍率（0.25 = 25%时长）")]
        [Range(0f, 1f)]
        public float controlDiminish3rd = 0.25f;

        [Tooltip("触发霸体需要的连续控制次数")]
        public int unstoppableTriggerCount = 3;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 甲壳机制
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        [Header("═══ 甲壳机制 ═══")]
        [Tooltip("闭眼时减伤比例（0.8 = 80%减伤，只造成20%伤害）")]
        [Range(0f, 0.95f)]
        public float armorDamageReduction = 0.8f;

        [Tooltip("被动眨眼持续时间（秒）")]
        public float blinkDuration = 0.75f;

        [Header("═══ V3.0 连体Buff ═══")]
        [Tooltip("连体Buff最大叠层（每只Rusher +1层）")]
        public int linkedBuffMaxStacks = 5;

        [Tooltip("每层减伤加成（0.1 = +10%减伤）")]
        [Range(0f, 0.2f)]
        public float linkedBuffDamageReductionPerStack = 0.1f;

        [Tooltip("每层Charge速度加成（0.1 = +10%速度）")]
        [Range(0f, 0.2f)]
        public float linkedBuffChargeSpeedPerStack = 0.1f;

        [Header("═══ V3.0 Charge频率打断 ═══")]
        [Tooltip("触发频率打断的受击次数阈值")]
        public int chargeHitCountThreshold = 30;

        [Header("═══ V3.0 Press过载机制 ═══")]
        [Tooltip("过载检测时间窗口（秒）")]
        public float pressOverloadWindow = 1.5f;

        [Tooltip("过载伤害阈值（窗口内累计伤害达到此值触发撤退）")]
        public float pressOverloadDamageThreshold = 2000f;

        [Header("═══ V3.0 短僵直 ═══")]
        [Tooltip("短僵直时长（毒球反弹、过载撤退）")]
        public float shortStunDuration = 1.5f;

        [Header("═══ V3.0 污秽球物理 ═══")]
        [Tooltip("污秽球血量")]
        public float pollutionBallHP = 200f;

        [Tooltip("污秽球质量（影响被推动的难度）")]
        public float pollutionBallMass = 3f;

        [Tooltip("反弹伤害（Boss最大HP的百分比）")]
        [Range(0.01f, 0.2f)]
        public float pollutionBossDamagePercent = 0.05f;

        [Tooltip("最大同时存在的污秽球数量")]
        public int pollutionMaxCount = 3;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 便捷方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取Idle时长（根据血量百分比）
        /// </summary>
        public float GetIdleDuration(float healthPercent)
        {
            if (healthPercent <= rageHealthThreshold)
            {
                return Random.Range(rageIdleDurationMin, rageIdleDurationMax);
            }
            else
            {
                return Random.Range(idleDurationMin, idleDurationMax);
            }
        }
        
        /// <summary>
        /// 获取召唤冷却（根据血量百分比）
        /// </summary>
        public float GetSummonCooldown(float healthPercent)
        {
            if (healthPercent <= rageHealthThreshold)
            {
                return summonCooldownRage;
            }
            return summonCooldown;
        }
        
        /// <summary>
        /// 获取碾压力（根据血量百分比，狂暴增强）
        /// </summary>
        public float GetPressForce(float healthPercent)
        {
            if (healthPercent <= rageHealthThreshold)
            {
                return pressForce * pressForceRageMultiplier;
            }
            return pressForce;
        }
        
        /// <summary>
        /// 获取推力倍率
        /// </summary>
        public float GetPushMultiplier(int impactLevel)
        {
            if (impactLevel >= 0 && impactLevel < impactPushMultipliers.Length)
            {
                return impactPushMultipliers[impactLevel];
            }
            return impactPushMultipliers[0];
        }
        /// <summary>
        /// 获取Wide技能的推力倍率
        /// </summary>
        public float GetWidePushMultiplier(int wideLevel)
        {
            if (wideLevel >= 0 && wideLevel < widePushMultipliers.Length)
            {
                return widePushMultipliers[wideLevel];
            }
            return widePushMultipliers[0];
        }
        /// <summary>
        /// 获取移动速度（根据狂暴状态）
        /// </summary>
        public float GetMoveSpeed(float healthPercent)
        {
            if (healthPercent <= rageHealthThreshold)
            {
                return idleMoveSpeed * rageMoveSpeedMultiplier;
            }
            return idleMoveSpeed;
        }

        /// <summary>
        /// 获取污秽球数量（根据狂暴状态）
        /// </summary>
        public int GetPollutionBurstCount(float healthPercent)
        {
            if (healthPercent <= rageHealthThreshold)
            {
                return ragePollutionBurstCount;
            }
            return pollutionBurstCount;
        }

        /// <summary>
        /// 获取召唤Rusher的移速加成（根据狂暴状态）
        /// </summary>
        public float GetRusherSpeedBonus(float healthPercent)
        {
            if (healthPercent <= rageHealthThreshold)
            {
                return rageRusherSpeedBonus;
            }
            return 1f;
        }

        /// <summary>
        /// 获取控制效果持续时间倍率（狂暴时减半）
        /// </summary>
        public float GetControlDurationMultiplier(float healthPercent)
        {
            if (healthPercent <= rageHealthThreshold)
            {
                return rageControlDurationMultiplier;
            }
            return 1f;
        }
    }
}