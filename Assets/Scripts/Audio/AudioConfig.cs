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
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Light vs Decay/Audio Config")]
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
        
        [Tooltip("怪物音效音量")]
        [Range(0f, 1f)]
        public float enemyDefaultVolume = 0.6f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 玩家音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 玩家音效 ═══")]
        [Tooltip("护盾破碎")]
        public AudioClip shieldBreak;
        
        [Tooltip("低血量警告（心跳）")]
        public AudioClip lowHealthWarning;
        
        [Tooltip("玩家音效音量")]
        [Range(0f, 1f)]
        public float playerDefaultVolume = 0.7f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Boss 音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ Boss 音效 ═══")]
        [Tooltip("Boss 入场")]
        public AudioClip bossEntrance;
        
        [Tooltip("野蛮冲撞预警")]
        public AudioClip bossChargeWarning;
        
        [Tooltip("重力碾压预警")]
        public AudioClip bossPressWarning;
        
        [Tooltip("重力碾压过程（持续）")]
        public AudioClip bossPressing;
        
        [Tooltip("喷吐发射")]
        public AudioClip bossSpit;
        
        [Tooltip("Boss 召唤小怪")]
        public AudioClip bossSummon;
        
        [Tooltip("Boss 音效音量")]
        [Range(0f, 1f)]
        public float bossDefaultVolume = 0.8f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 空投音效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("═══ 空投音效 ═══")]
        [Tooltip("无人机空投落地")]
        public AudioClip airdropLand;
        
        [Tooltip("箱子破碎")]
        public AudioClip crateBreak;
        
        [Tooltip("空投音效音量")]
        [Range(0f, 1f)]
        public float airdropDefaultVolume = 0.6f;
    }
}