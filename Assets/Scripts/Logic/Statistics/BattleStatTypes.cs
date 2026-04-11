// ============================================================
// BattleStatTypes.cs
// 文件位置: Assets/Scripts/Logic/Statistics/BattleStatTypes.cs
// 用途：战斗数据采集系统 - 枚举和数据结构定义
// 版本：V4.0 重构版
// ============================================================

using System;
using System.Collections.Generic;
using LightVsDecay.Core.Pool;
using UnityEngine;
using LightVsDecay.Data.SO;

namespace LightVsDecay.Logic.Statistics
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 枚举定义
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 玩家对敌人的伤害来源
    /// </summary>
    public enum DamageSource
    {
        MainLaser,      // 主激光（直射段）
        SubLaser,       // 副激光（分裂/反射段）
        Explosion,      // 爆炸（Focus Lv5）
        Chain,
        Other
    }
    
    /// <summary>
    /// 玩家受伤来源
    /// </summary>
    public enum PlayerDamageSource
    {
        MobCollision,       // 小怪碰撞
        MobBullet,          // 小怪子弹（炮手 LavaGunner）
        MobExplosionAoE,    // 小怪爆炸 AoE（自爆怪 LavaExploder）
        BossCollision,      // Boss 撞击（Charge/Press）
        BossBullet,         // Boss 子弹（污秽球/熔浆弹）
        BossFriction,       // Boss 摩擦伤害
        BossMeteor,         // Boss 陨石（火山Boss 专用）
        Other
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 统计器类
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 生成计数器（按敌人类型）
    /// </summary>
    [Serializable]
    public class SpawnCounter
    {
        // ─── Ch1 ───
        public int slime = 0;
        public int rusher = 0;
        public int tank = 0;
        public int drifter = 0;
        public int elite = 0;
        // ─── Ch2 ───
        public int lavaExploder = 0;
        public int lavaSplitter = 0;
        public int lavaGunner = 0;
        public int lavaTank = 0;
        public int lavaElite = 0;
        public int lavaPuddle = 0;      // 地形障碍，不计入战斗Total

        // ─── Ch3 ───
        public int frostSlime = 0;
        public int frostTank = 0;
        public int frostCatalyst = 0;
        public int frostcaster = 0;
        public int iceShieldGuard = 0;
        public int frostcasterElite = 0;
        public int frostGunner = 0;
        public int iceWall = 0;             // 地形障碍，不计入战斗 Total

        public int Total => slime + rusher + tank + drifter + elite
                          + lavaExploder + lavaSplitter + lavaGunner + lavaTank + lavaElite
                          + frostSlime + frostTank + frostCatalyst + frostcaster + iceShieldGuard + frostcasterElite + frostGunner;

        public void Reset()
        {
            slime = rusher = tank = drifter = elite = 0;
            lavaExploder = lavaSplitter = lavaGunner = lavaTank = lavaElite = lavaPuddle = 0;
            frostSlime = frostTank = frostCatalyst = frostcaster = iceShieldGuard = frostcasterElite = frostGunner = iceWall = 0;
        }

        public void Add(EnemyType type)
        {
            switch (type)
            {
                // Ch1
                case EnemyType.Slime:        slime++;       break;
                case EnemyType.Rusher:       rusher++;      break;
                case EnemyType.Tank:         tank++;        break;
                case EnemyType.Drifter:      drifter++;     break;
                case EnemyType.EliteTank:
                case EnemyType.EliteDrifter: elite++;       break;
                // Ch2
                case EnemyType.LavaExploder:       lavaExploder++; break;
                case EnemyType.EliteLavaExploder:  lavaElite++;    break;
                case EnemyType.LavaSplitter:       lavaSplitter++; break;
                case EnemyType.EliteLavaSplitter:  lavaElite++;    break;
                case EnemyType.LavaGunner:         lavaGunner++;   break;
                case EnemyType.LavaTank:           lavaTank++;     break;
                case EnemyType.LavaPuddle:         lavaPuddle++;   break;
                // Ch3
                case EnemyType.FrostSlime:          frostSlime++;       break;
                case EnemyType.FrostTank:           frostTank++;        break;
                case EnemyType.FrostCatalyst:       frostCatalyst++;    break;
                case EnemyType.Frostcaster:         frostcaster++;      break;
                case EnemyType.EliteIceShieldGuard: iceShieldGuard++;   break;
                case EnemyType.EliteFrostcaster:    frostcasterElite++; break;
                case EnemyType.FrostGunner:         frostGunner++;      break;
                case EnemyType.IceWall:             iceWall++;          break;
            }
        }
    }

    /// <summary>
    /// 击杀计数器（按敌人类型）
    /// </summary>
    [Serializable]
    public class KillCounter
    {
        // ─── Ch1 ───
        public int slime = 0;
        public int rusher = 0;
        public int tank = 0;
        public int drifter = 0;
        public int elite = 0;
        // ─── Ch2 ───
        public int lavaExploder = 0;
        public int lavaSplitter = 0;
        public int lavaGunner = 0;
        public int lavaTank = 0;
        public int lavaElite = 0;
        // 注：LavaPuddle 不可击杀，不计入KillCounter

        // ─── Ch3 ───
        public int frostSlime = 0;
        public int frostTank = 0;
        public int frostCatalyst = 0;
        public int frostcaster = 0;
        public int iceShieldGuard = 0;
        public int frostcasterElite = 0;
        public int frostGunner = 0;
        // 注：IceWall 不可击杀，不计入 KillCounter

        public int Total => slime + rusher + tank + drifter + elite
                          + lavaExploder + lavaSplitter + lavaGunner + lavaTank + lavaElite
                          + frostSlime + frostTank + frostCatalyst + frostcaster + iceShieldGuard + frostcasterElite + frostGunner;

        public void Reset()
        {
            slime = rusher = tank = drifter = elite = 0;
            lavaExploder = lavaSplitter = lavaGunner = lavaTank = lavaElite = 0;
            frostSlime = frostTank = frostCatalyst = frostcaster = iceShieldGuard = frostcasterElite = frostGunner = 0;
        }

        public void Add(EnemyType type)
        {
            switch (type)
            {
                // Ch1
                case EnemyType.Slime:        slime++;       break;
                case EnemyType.Rusher:       rusher++;      break;
                case EnemyType.Tank:         tank++;        break;
                case EnemyType.Drifter:      drifter++;     break;
                case EnemyType.EliteTank:
                case EnemyType.EliteDrifter: elite++;       break;
                // Ch2
                case EnemyType.LavaExploder:       lavaExploder++; break;
                case EnemyType.EliteLavaExploder:  lavaElite++;    break;
                case EnemyType.LavaSplitter:       lavaSplitter++; break;
                case EnemyType.EliteLavaSplitter:  lavaElite++;    break;
                case EnemyType.LavaGunner:         lavaGunner++;   break;
                case EnemyType.LavaTank:           lavaTank++;     break;
                // Ch3
                case EnemyType.FrostSlime:          frostSlime++;       break;
                case EnemyType.FrostTank:           frostTank++;        break;
                case EnemyType.FrostCatalyst:       frostCatalyst++;    break;
                case EnemyType.Frostcaster:         frostcaster++;      break;
                case EnemyType.EliteIceShieldGuard: iceShieldGuard++;   break;
                case EnemyType.EliteFrostcaster:    frostcasterElite++; break;
                case EnemyType.FrostGunner:         frostGunner++;      break;
            }
        }
    }
    
    /// <summary>
    /// Frost效果统计器
    /// </summary>
    [Serializable]
    public class FrostStats
    {
        public int slowCount = 0;
        public int freezeCount = 0;
        public float slowDuration = 0f;
        
        public void Reset()
        {
            slowCount = freezeCount = 0;
            slowDuration = 0f;
        }
    }
    
    /// <summary>
    /// Boss战统计器
    /// </summary>
    [Serializable]
    public class BossStats
    {
        public int chargeCount = 0;
        public int pressCount = 0;
        public int summonCount = 0;
        public int stunCount = 0;
        public float hpRemaining = 1f;
        public string lastPhase = "None";

        public void Reset()
        {
            chargeCount = pressCount = summonCount = stunCount = 0;
            hpRemaining = 1f;
            lastPhase = "None";
        }
    }

    /// <summary>
    /// Ch3 第三章战斗统计器
    /// </summary>
    [Serializable]
    public class Ch3Stats
    {
        // 冰盾机制
        public int   iceShieldBrokenCount = 0;
        public float iceShieldDmgAbsorbed = 0f;

        // 催化者机制
        public int catalystBurstCount         = 0;
        public int catalystBurstAffectedCount = 0;  // 催化爆发影响的目标数（怪物+冰墙）
        public int iceWallAcceleratedCount    = 0;  // 被催化加速的冰墙数

        // 霜冻施法者机制
        public int frostcasterCastCount      = 0;
        public int frostcasterWallCountTotal = 0;  // 施法者生成冰墙总数

        // 冰墙孵化机制
        public int iceWallHatchCount = 0;           // 冰墙孵化小怪总次数

        // 暴走机制
        public int berserkAppliedCount = 0;         // 怪物暴走状态应用次数

        // 光棱塔冻结
        public int   towerFreezeCount    = 0;
        public float towerFreezeDuration = 0f;

        // 冰刺（绝对零度）
        public int iceSpikeInterceptedCount = 0;
        public int iceSpikeHitCount         = 0;

        public void Reset()
        {
            iceShieldBrokenCount      = 0;
            iceShieldDmgAbsorbed      = 0f;
            catalystBurstCount        = 0;
            catalystBurstAffectedCount = 0;
            iceWallAcceleratedCount   = 0;
            frostcasterCastCount      = 0;
            frostcasterWallCountTotal = 0;
            iceWallHatchCount         = 0;
            berserkAppliedCount       = 0;
            towerFreezeCount          = 0;
            towerFreezeDuration       = 0f;
            iceSpikeInterceptedCount  = 0;
            iceSpikeHitCount          = 0;
        }
    }

    /// <summary>
    /// 极寒之核 Boss 专属统计器
    /// </summary>
    [Serializable]
    public class GlacialBossStats
    {
        public int iceWallBuildCount         = 0;   // 冰墙构建技能释放次数
        public int freezeRayCount            = 0;   // 冰封射线释放次数
        public int chargeCount               = 0;   // 极寒冲撞次数
        public int absoluteZeroCount         = 0;   // 绝对零度施放次数
        public int absoluteZeroInterrupted   = 0;   // 绝对零度被打断次数

        public void Reset()
        {
            iceWallBuildCount = freezeRayCount = chargeCount = absoluteZeroCount = absoluteZeroInterrupted = 0;
        }
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 波次数据结构
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 单波次统计数据 V4.0
    /// </summary>
    [Serializable]
    public class WaveStatData
    {
        // ═══ 基础信息 (5) ═══
        public int wave;
        public string buildType;
        public int playerLevel;
        public string result;           // Win / Loss
        public float timeToClear;
        
        // ═══ 击杀统计 (6) ═══
        public int killSlime;
        public int killRusher;
        public int killTank;
        public int killDrifter;
        public int killElite;
        public int killTotal;
        
        // ═══ 玩家状态 (8) ═══
        public int hpStartHull;
        public int hpStartShield;
        public int hpEndHull;
        public int hpEndShield;
        public float dmgFromMobs;
        public float dmgFromBoss;
        public float playerHPLost;
        public float tankAbsorbedRatio;
        // ═══ 技能与大招 【修改分类】 ═══
        public int overloadCount;       // 【新增】本波次大招释放次数
        // ═══ 伤害输出 (8) ═══
        public float dmgMainLaser;
        public float dmgSubLaser;
        public float dmgExplosion;
        public float dmgChain;          // 【新增】连锁伤害总量
        public float critDamageTotal;
        public int critHitCount;
        public int normalHitCount;
        public float overkillRatio;
        public float dmgDealtTotal;
        
        // ═══ Frost统计 (3) ═══
        public int frostSlowCount;
        public int frostFreezeCount;
        public float frostSlowDuration;
        
        // ═══ 面板快照 (3) ═══
        public float panelDPS;
        public float panelCritRate;
        public float panelLaserWidth;
        
        // ═══ 无人机数据 (12) ═══
        public string droneChoice;
        public string droneRewardType;
        public string droneRewardValue;
        public float droneAccHealth;
        public float droneAccShield;
        public float droneAccDamagePct;
        public float droneAccCritPct;
        public float droneAccLaserWidth;
        public float droneAccLaserLength;
        public int droneCountSupply;
        public int droneCountGacha;
        public int droneCountDeal;
        
        // ═══ Boss战数据 (9) ═══
        public float bossDmgCollision;
        public float bossDmgBullet;
        public float bossPushbackTime;
        public string bossPhase;
        public float bossHPRemaining;
        public int bossChargeCount;
        public int bossPressCount;
        public int bossSummonCount;
        public int bossStunCount;
        
        // ═══ 技能路径 (2) ═══
        public string skillPath;
        public string skillLevels;
        
        // ═══ 其他 (2) ═══
        public float dpsPeak;
        public float enemyTotalHP;
        // ═══ V4.2 新增统计 (5) ═══
        public float effectiveDPS;      // 有效DPS = dmgDealtTotal / timeToClear
        public int playerHitCount;       // 玩家受击次数
        public int expGained;            // 本波获得经验值
        public int goldGained;           // 本波获得金币
        public float timeInDanger;       // 濒死时长（血量<30%的持续秒数）

        // ═══ V4.5 新增：生成统计 + 配置标识 ═══
        public int spawnSlime;
        public int spawnRusher;
        public int spawnTank;
        public int spawnDrifter;
        public int spawnElite;
        public int spawnTotal;
        public string waveConfigId;      // 本局使用的波次配置资源名（区分测试/正式配置）

        // ═══ V4.6 新增：护盾伤害追踪 ═══
        public float shieldDmgFromMobs;  // 小怪对护盾造成的伤害（护盾吸收量）

        // ═══ V4.7 新增：Boss伤害输出 + 波次难度系数 ═══
        public float bossDmgDealt;       // 玩家对Boss造成的总伤害
        public float waveDiffMult;       // 波次难度系数快照

        // ═══ V5.0 Ch2专属统计 ═══

        // Ch2 击杀统计 (5)
        public int killLavaExploder;
        public int killLavaSplitter;
        public int killLavaGunner;
        public int killLavaTank;
        public int killLavaElite;

        // Ch2 生成统计 (6)
        public int spawnLavaExploder;
        public int spawnLavaSplitter;
        public int spawnLavaGunner;
        public int spawnLavaTank;
        public int spawnLavaElite;
        public int spawnLavaPuddle;          // 水坑生成总数（自爆+陨石）

        // 自爆怪行为 (2)
        public int exploderDeathCount;       // 自爆触发次数（含碰撞死亡路径）
        public float exploderAoeDmgToPlayer; // 自爆AoE对玩家伤害总量

        // 分裂怪行为 (2)
        public int splitterDeathCount;       // 分裂触发次数
        public int splitterChildrenSpawned;  // 分裂产生子体总数

        // 炮手行为 (1)
        public float gunnerBulletDmgToPlayer; // 炮手子弹对玩家总伤害

        // 玩家受伤细分 (1)
        public float dmgFromMobExplosion;    // 自爆AoE对玩家伤害（从MobCollision拆出）

        // Boss Ch2专属 (2)
        public int bossAbsorptionCount;      // Boss吸收小怪次数
        public float bossHealTotal;          // Boss回血总量

        // ═══ V5.0 P2 新增 ═══
        public int gunnerShotsFired;         // 炮手开火次数
        public int bossMeteorCount;          // Boss陨石攻击次数（独立于summonCount）

        // ═══ V5.0 P3 新增 ═══
        public int puddlePeakCount;          // 场上熔岩水坑峰值数量

        // ═══ V6.1 Ch2 火球系统 (2) ═══
        public int bossFireballBurstCount;          // 火球喷发触发次数（每次 FireballBurstRoutine 计1）
        public int bossFireballInterceptedCount;    // 被激光拦截击落的火球数量

        // ═══ V6.0 Ch3 击杀统计 (6) + V6.2 FrostGunner (1) ═══
        public int killFrostSlime;
        public int killFrostTank;
        public int killFrostCatalyst;
        public int killFrostcaster;
        public int killIceShieldGuard;
        public int killFrostcasterElite;
        public int killFrostGunner;          // V6.2

        // ═══ V6.0 Ch3 生成统计 (7) + V6.2 FrostGunner (1) ═══
        public int spawnFrostSlime;
        public int spawnFrostTank;
        public int spawnFrostCatalyst;
        public int spawnFrostcaster;
        public int spawnIceShieldGuard;
        public int spawnFrostcasterElite;
        public int spawnFrostGunner;         // V6.2
        public int spawnIceWall;             // 冰墙生成总数（施法者+Boss）

        // ═══ V6.0 冰盾机制 (2) ═══
        public int   iceShieldBrokenCount;   // 本波破碎冰盾数量
        public float iceShieldDmgAbsorbed;   // 冰盾吸收的激光总伤害

        // ═══ V6.0 催化者机制 (1) + V6.1 扩展 (3) ═══
        public int catalystBurstCount;            // 催化者死亡爆发触发次数
        public int catalystBurstAffectedCount;    // 催化爆发命中的怪物总数
        public int iceWallAcceleratedCount;       // 被催化加速孵化的冰墙数

        // ═══ V6.0 霜冻施法者机制 (1) + V6.1 扩展 (1) ═══
        public int frostcasterCastCount;          // 霜冻施法者召唤冰墙次数
        public int frostcasterWallCountTotal;     // 施法者生成冰墙总数

        // ═══ V6.1 孵化机制 (1) ═══
        public int iceWallHatchCount;             // 冰墙孵化小怪总次数

        // ═══ V6.1 暴走机制 (1) ═══
        public int berserkAppliedCount;           // 怪物暴走状态应用次数

        // ═══ V6.0 冰墙峰值 (1) ═══
        public int iceWallPeakCount;         // 场上冰墙峰值数量

        // ═══ V6.0 光棱塔冻结 (2) ═══
        public int   towerFreezeCount;       // 光棱塔被冻结次数
        public float towerFreezeDuration;    // 光棱塔冻结总时长（秒）

        // ═══ V6.0 冰刺 (2) ═══
        public int iceSpikeInterceptedCount; // 被激光拦截的冰刺数量
        public int iceSpikeHitCount;         // 命中光棱塔的冰刺数量

        // ═══ V6.0 极寒之核 Boss (5) ═══
        public int glacialIceWallBuildCount;       // Boss 冰墙构建次数
        public int glacialFreezeRayCount;          // 冰封射线释放次数
        public int glacialChargeCount;             // 极寒冲撞次数
        public int glacialAbsoluteZeroCount;       // 绝对零度施放次数
        public int glacialAbsoluteZeroInterrupted; // 绝对零度被打断次数
    }
}