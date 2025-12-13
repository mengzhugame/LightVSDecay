// ============================================================
// BossConfig.cs
// 文件位置: Assets/Scripts/Data/SO/BossConfig.cs
// 用途：Boss 行为配置 (ScriptableObject)
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
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Spawn (入场) 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ Spawn (入场) ═══")]
        [Tooltip("入场下沉时长")]
        public float spawnDuration = 2.5f;
        
        [Tooltip("战斗锚点Y坐标（屏幕上方1/4处）")]
        public float battleAnchorY = 3.5f;
        
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
        // Summon (召唤) 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ Summon (召唤) ═══")]
        [Tooltip("召唤动画时长")]
        public float summonDuration = 1.5f;
        
        [Tooltip("身体震动强度")]
        public float summonShakeIntensity = 0.1f;
        
        [Tooltip("身体震动频率")]
        public float summonShakeFrequency = 30f;
        
        [Tooltip("召唤小怪数量")]
        public int summonMinionCount = 3;
        // ═══ 新增：Summon 冷却机制 ═══
        [Header("═══ Summon 冷却 (新增) ═══")]
        [Tooltip("召唤技能冷却时间（秒）- Idle结束时检查，优先级最高")]
        public float summonCooldown = 15f;
        
        [Tooltip("钳形攻势：左侧生成偏移（相对BOSS位置）")]
        public Vector2 summonLeftOffset = new Vector2(-3f, -1f);
        
        [Tooltip("钳形攻势：右侧生成偏移（相对BOSS位置）")]
        public Vector2 summonRightOffset = new Vector2(3f, -1f);
        
        [Tooltip("钳形攻势：每侧生成的 Rusher 数量")]
        public int summonRusherPerSide = 2;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ═══ 新增：Pollution 污秽喷吐 ═══
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ Pollution 污秽喷吐 (新增) ═══")]
        [Tooltip("Idle 状态下发射间隔（秒）")]
        public float pollutionInterval = 4f;
        
        [Tooltip("投射物飞行速度")]
        public float pollutionSpeed = 5f;
        
        [Tooltip("惰性追踪转向速度（度/秒）- 越小转弯越大")]
        public float pollutionTurnSpeed = 90f;
        
        [Tooltip("投射物命中护盾伤害")]
        public int pollutionShieldDamage = 100;
        
        [Tooltip("投射物 Prefab（需在Inspector中拖入）")]
        public GameObject pollutionProjectilePrefab;
        
        [Tooltip("投射物生命周期（秒）")]
        public float pollutionLifetime = 8f;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Charge (冲撞) 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        [Header("═══ Charge (冲撞) ═══")]
        [Tooltip("蓄力时长（玩家DPS窗口）")]
        public float chargeTelegraphDuration = 2.0f;
        
        [Tooltip("蓄力后退距离")]
        public float chargeWindupDistance = 0.5f;
        
        [Tooltip("冲撞速度")]
        public float chargeDashSpeed = 15f;
        
        [Tooltip("撞击玩家伤害")]
        public float chargeHitDamage = 300f;
        
        [Tooltip("撞击屏幕震动强度")]
        public float chargeHitShakeIntensity = 0.8f;
        
        [Tooltip("撞击屏幕震动时长")]
        public float chargeHitShakeDuration = 0.3f;
        
        [Tooltip("冲撞后弹回时长")]
        public float chargeBounceBackDuration = 0.5f;
        
        [Header("═══ 角力物理 (Pushback) ═══")]
        [Tooltip("安全线Y坐标（推回这里算玩家胜利）")]
        public float safeLineY = 3.0f;
        
        [Tooltip("撞击线Y坐标（Boss到达这里算玩家失败）")]
        public float hitLineY = -3.0f;
             
        [Tooltip("角力最大持续时间（防止卡住）")]
        public float maxCrushingDuration = 15f;
        
        [Tooltip("被推住后的僵直时长（奖励时间）")]
        public float counterStunDuration = 3.0f;
        
        [Tooltip("普通激光对Boss的基础推力")]
        public float baseLaserPushForce = 80f;
        
        [Tooltip("Impact技能等级的推力倍率 [Lv0, Lv1, Lv2, Lv3, Lv4, Lv5]")]
        public float[] impactPushMultipliers = new float[] { 0.3f, 0.5f, 0.7f, 1.0f, 1.3f, 1.6f };
        
        [Tooltip("大招激光推力倍率")]
        public float ultPushMultiplier = 2.5f;
        
        [Tooltip("冲撞力（Boss向下冲的力）")]
        public float chargeForce = 100f;
        [Tooltip("角力前召唤小怪数量（0=不召唤）")]
        public int crushingSummonCount = 4;
        [Tooltip("蓄力霸体时间（前X秒不可打断）")]
        public float telegraphSuperArmorDuration = 1.0f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Stun (僵直) 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ Stun (僵直) ═══")]
        [Tooltip("僵直时长")]
        public float stunDuration = 2.5f;
        
        [Tooltip("僵直时颜色变暗程度")]
        [Range(0f, 1f)]
        public float stunDarkenAmount = 0.5f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 眼睛 (弱点) 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 眼睛 (弱点) ═══")]
        [Tooltip("闭眼时Y轴缩放")]
        [Range(0f, 0.3f)]
        public float eyeClosedScaleY = 0.1f;
        
        [Tooltip("睁眼时Collider放大倍数")]
        public float eyeOpenColliderScale = 1.5f;
        
        [Tooltip("眼睛开闭动画时长")]
        public float eyeTransitionDuration = 0.2f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // AI 决策配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ AI 决策 ═══")]
        [Tooltip("场面干净阈值（小怪数量）")]
        public int cleanSceneMobThreshold = 3;
        
        [Tooltip("场面混乱阈值（小怪数量）")]
        public int chaoticSceneMobThreshold = 5;
        
        [Tooltip("场面干净时Summon概率")]
        [Range(0f, 1f)]
        public float cleanSceneSummonChance = 0.7f;
        
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
                // 狂暴模式
                return Random.Range(rageIdleDurationMin, rageIdleDurationMax);
            }
            else
            {
                // 普通模式
                return Random.Range(idleDurationMin, idleDurationMax);
            }
        }
        
        /// <summary>
        /// 决定下一个技能（Summon 或 Charge）
        /// </summary>
        /// <param name="currentMobCount">场上小怪数量</param>
        /// <param name="lastSkillWasCharge">上次是否是Charge</param>
        /// <returns>true = Summon, false = Charge</returns>
        public bool ShouldSummon(int currentMobCount, bool lastSkillWasCharge)
        {
            // 防连续：上次是Charge，这次强制Summon
            if (lastSkillWasCharge && currentMobCount < chaoticSceneMobThreshold)
            {
                return true;
            }
            
            // 场面混乱：强制Charge
            if (currentMobCount >= chaoticSceneMobThreshold)
            {
                return false;
            }
            
            // 场面干净：70%概率Summon
            if (currentMobCount < cleanSceneMobThreshold)
            {
                return Random.value < cleanSceneSummonChance;
            }
            
            // 中等场面：50/50
            return Random.value < 0.5f;
        }
    }
}