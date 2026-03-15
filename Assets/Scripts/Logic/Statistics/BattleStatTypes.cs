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
        BossCollision,      // Boss 撞击（Charge/Press）
        BossBullet,         // Boss 子弹（污秽球）
        BossFriction,       // Boss 摩擦伤害
        Other
    }
    
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 统计器类
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    /// <summary>
    /// 击杀计数器（按敌人类型）
    /// </summary>
    [Serializable]
    public class KillCounter
    {
        public int slime = 0;
        public int rusher = 0;
        public int tank = 0;
        public int drifter = 0;
        public int elite = 0;
        
        public int Total => slime + rusher + tank + drifter + elite;
        
        public void Reset()
        {
            slime = rusher = tank = drifter = elite = 0;
        }
        
        public void Add(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Slime: slime++; break;
                case EnemyType.Rusher: rusher++; break;
                case EnemyType.Tank: tank++; break;
                case EnemyType.Drifter: drifter++; break;
                case EnemyType.EliteTank:
                case EnemyType.EliteDrifter:
                    elite++; break;
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
    }
}