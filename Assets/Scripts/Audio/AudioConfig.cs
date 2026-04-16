// ============================================================
// AudioConfig.cs
// 文件位置: Assets/Scripts/Audio/AudioConfig.cs
// 用途：音效配置 ScriptableObject
// ============================================================

using UnityEngine;

namespace LightVsDecay.Audio
{
    /// <summary>
    /// 音效配置数据
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "LightVsDecay/Audio Config")]
    public class AudioConfig : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 背景音乐
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 背景音乐 (BGM) ═══")]
        [Tooltip("主场景背景音乐")]
        public AudioClip mainMenuBGM;
        
        [Tooltip("战斗场景背景音乐")]
        public AudioClip battleBGM;
        
        [Tooltip("Boss战背景音乐（可选，如果没有则继续播放战斗BGM）")]
        public AudioClip bossBGM;
        
        [Tooltip("BGM 默认音量")]
        [Range(0f, 1f)]
        public float bgmDefaultVolume = 0.5f;
        
        [Tooltip("BGM 淡入淡出时间")]
        public float bgmFadeDuration = 1.0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // UI 音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ UI 音效 ═══")]
        [Tooltip("按钮点击")]
        public AudioClip buttonClick;
        
        [Tooltip("升级音效")]
        public AudioClip levelUp;
        
        [Tooltip("技能选择卡牌")]
        public AudioClip skillCardSelect;
        
        [Tooltip("胜利结算")]
        public AudioClip victoryJingle;
        
        [Tooltip("失败结算")]
        public AudioClip defeatJingle;
        
        [Tooltip("UI 音效默认音量")]
        [Range(0f, 1f)]
        public float uiDefaultVolume = 0.7f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 激光循环音效（三种状态）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 激光循环音效 ═══")]
        [Tooltip("激光空射（未命中任何物体，纯电流声）")]
        public AudioClip laserIdle;
        
        [Tooltip("激光灼烧（命中黑油/软体怪/Boss眼睛）")]
        public AudioClip laserBurn;
        
        [Tooltip("激光金属（命中Tank/Boss装甲）")]
        public AudioClip laserMetal;
        
        [Tooltip("激光循环音效音量")]
        [Range(0f, 1f)]
        public float laserLoopVolume = 0.4f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 怪物音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 怪物音效 ═══")]
        [Tooltip("怪物自爆（撞塔）")]
        public AudioClip enemyExplode;
        
        [Tooltip("怪物死亡冒烟")]
        public AudioClip enemyDeath;
        
        [Tooltip("怪物冰冻")]
        public AudioClip enemyFreeze;

        [Tooltip("分裂怪死亡爆开音效（可不配置）")]
        public AudioClip enemySplit;

        [Tooltip("炸弹怪/精英炸弹怪死亡爆炸音效（LavaExploder/EliteLavaExploder）")]
        public AudioClip grenadeExplosion;

        [Tooltip("激光击破投射物爆炸音效（通用，适用于熔岩炮弹、Boss污秽球、冰刺、陨石）")]
        public AudioClip projectileExplode;

        [Tooltip("熔浆液生成音效（LavaPuddle 生成时播放）")]
        public AudioClip lavaPuddleSpawn;

        [Tooltip("熔岩炮手喷吐火球音效")]
        public AudioClip gunnerSpit;

        [Tooltip("火球飞行音效（挂在 LavaProjectile 上的 AudioSource 使用）")]
        public AudioClip fireballFlight;

        [Tooltip("极寒催化者死亡冷气释放特效音效")]
        public AudioClip catalystBurst;

        [Tooltip("冰墙生成落地音效（FrostcasterAI 召唤冰墙时播放）")]
        public AudioClip iceWallSpawn;

        [Tooltip("冰盾破裂音效（Frost_EliteTank 冰盾 HP 归零时播放）")]
        public AudioClip iceShieldBreak;

        [Tooltip("怪物音效音量")]
        [Range(0f, 1f)]
        public float enemyDefaultVolume = 0.6f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 玩家音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 玩家音效 ═══")]
        [Tooltip("护盾破碎")]
        public AudioClip shieldBreak;
        [Tooltip("护盾受击")]
        public AudioClip shieldHit;  // 【新增】

        [Tooltip("光棱塔受击")]
        public AudioClip towerHit;
        [Tooltip("光棱塔被冰冻音效（Boss 冰封技能命中时播放）")]
        public AudioClip towerFreeze;
        [Tooltip("光棱塔解冻音效（冻结解除时播放）")]
        public AudioClip towerUnfreeze;
        [Tooltip("低血量警告（心跳）")]
        public AudioClip lowHealthWarning;

        [Tooltip("玩家音效音量")]
        [Range(0f, 1f)]
        public float playerDefaultVolume = 0.7f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Boss 音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ Boss 音效 ═══")]
        [Tooltip("Boss入场咆哮音效")]
        public AudioClip bossRoar;
        
        [Tooltip("野蛮冲撞预警")]
        public AudioClip bossChargeWarning;
        
        [Tooltip("Boss破空音效（冲锋/突进）")]
        public AudioClip bossDash;
        
        [Tooltip("Boss重力碾压过程（循环）")]
        public AudioClip bossPressing;
        
        [Tooltip("Boss喷吐污秽球")]
        public AudioClip bossSpit;
        
        [Tooltip("Boss召唤小怪")]
        public AudioClip bossSummon;

        [Tooltip("BOSS来袭警告音效（波次出现 Boss 时的 UI 警报声）")]
        public AudioClip bossWarning;

        [Tooltip("熔岩BOSS吸收融合音效（每次吸收 LavaSlime 回血时播放，与 VolcanoBossController.sfxAbsorbSlime 共享同一音效）")]
        public AudioClip bossAbsorb;

        [Tooltip("Boss 死亡爆炸音效（子弹时间开始、爆炸特效播放的同一帧触发）")]
        public AudioClip bossDeath;

        [Tooltip("Boss 音效音量")]
        [Range(0f, 1f)]
        public float bossDefaultVolume = 0.7f;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 经验球音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("═══ 经验球音效 ═══")]
        [Tooltip("经验球收集（飞入经验栏）")]
        public AudioClip xpOrbCollect;

        [Tooltip("经验球收集音量")]
        [Range(0f, 1f)]
        public float xpOrbVolume = 0.5f;

        [Tooltip("经验球收集音效冷却时间（秒）")]
        public float xpOrbCollectCooldown = 0.1f;

        [Tooltip("金币收集（飞入金币栏）")]
        public AudioClip coinCollect;

        [Tooltip("金币收集音量")]
        [Range(0f, 1f)]
        public float coinCollectVolume = 0.45f;

        [Tooltip("金币收集音效冷却时间（秒）")]
        public float coinCollectCooldown = 0.04f;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 空投音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        [Tooltip("无人机入场飞行音效")]
        public AudioClip droneEnter;
        [Tooltip("无人机落地音效")]
        public AudioClip droneLand;
        [Tooltip("箱子破碎")]
        public AudioClip droneBoxBreak;
        
        [Tooltip("无人机音效音量")]
        [Range(0f, 1f)]
        public float droneVolume = 0.6f;
    }
}
