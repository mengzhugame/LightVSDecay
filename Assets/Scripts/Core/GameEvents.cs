using System;
using UnityEngine;

namespace LightVsDecay.Core
{
    /// <summary>
    /// 游戏状态枚举
    /// </summary>
    public enum GameState
    {
        Menu,       // 主菜单
        Playing,    // 游戏中
        Paused,     // 暂停
        Victory,    // 胜利
        Defeat      // 失败
    }
    
    /// <summary>
    /// 游戏全局事件系统
    /// 使用静态事件实现松耦合通信
    /// </summary>
    public static class GameEvents
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 游戏状态事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>游戏状态改变</summary>
        public static event Action<GameState> OnGameStateChanged;
        
        /// <summary>游戏开始</summary>
        public static event Action OnGameStart;
        
        /// <summary>游戏暂停</summary>
        public static event Action OnGamePaused;
        
        /// <summary>游戏恢复</summary>
        public static event Action OnGameResumed;
        
        /// <summary>游戏胜利</summary>
        public static event Action OnGameVictory;
        
        /// <summary>游戏失败</summary>
        public static event Action OnGameDefeat;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 玩家进度事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>经验值变化 (当前经验, 升级所需经验)</summary>
        public static event Action<int, int> OnExpChanged;
        
        /// <summary>等级提升 (新等级)</summary>
        public static event Action<int> OnLevelUp;
        
        /// <summary>金币变化 (当前金币)</summary>
        public static event Action<int> OnCoinChanged;
        
        /// <summary>大招能量变化 (当前能量, 最大能量)</summary>
        public static event Action<int, int> OnUltEnergyChanged;
        
        /// <summary>大招已准备好</summary>
        public static event Action OnUltReady;
        
        /// <summary>大招已使用</summary>
        public static event Action OnUltUsed;
        
        /// <summary>击杀数变化 (击杀数)</summary>
        public static event Action<int> OnKillCountChanged;
        
        /// <summary>连击数变化 (连击数)</summary>
        public static event Action<int> OnComboChanged;
        
        /// <summary>连击重置</summary>
        public static event Action OnComboReset;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 玩家状态事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>护盾血量变化 (当前, 最大)</summary>
        public static event Action<int, int> OnShieldHPChanged;
        
        /// <summary>本体血量变化 (当前, 最大)</summary>
        public static event Action<int, int> OnHullHPChanged;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// 护盾与血量事件（新增）
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>护盾破碎（用于后处理效果）</summary>
        public static event Action OnShieldBroken;

        /// <summary>低血量开始（血量<20%）</summary>
        public static event Action OnLowHealthStart;

        /// <summary>低血量结束（血量>=20%）</summary>
        public static event Action OnLowHealthEnd;

        /// <summary>玩家护盾受伤飘字 (伤害值, 位置)</summary>
        public static event Action<int, Vector3> OnPlayerShieldDamaged;

        /// <summary>玩家血量受伤飘字 (伤害值, 位置)</summary>
        public static event Action<int, Vector3> OnPlayerHealthDamaged;

        /// <summary>玩家护盾恢复飘字 (恢复值, 位置)</summary>
        public static event Action<int, Vector3> OnPlayerShieldRestored;

        /// <summary>玩家血量恢复飘字 (恢复值, 位置)</summary>
        public static event Action<int, Vector3> OnPlayerHealthRestored;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能事件（新增）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>技能应用 (技能类型, 新等级)</summary>
        public static event Action<Data.SO.SkillType, int> OnSkillApplied;

// 在触发方法区域添加：
        public static void TriggerSkillApplied(Data.SO.SkillType type, int newLevel) 
            => OnSkillApplied?.Invoke(type, newLevel);
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 游戏时间事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>游戏时间更新 (当前秒数, 总秒数)</summary>
        public static event Action<float, float> OnGameTimeUpdated;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 敌人事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>敌人死亡 (敌人类型, 位置, 经验值, 金币)</summary>
        public static event Action<Pool.EnemyType, Vector3, int, int> OnEnemyDied;
        /// <summary>经验光点被收集 (经验值)</summary>
        public static event Action<int> OnXPOrbCollected;

        /// <summary>升级选择完成</summary>
        public static event Action OnLevelUpChoiceComplete;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Boss 事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>Boss 血量变化 (血量百分比 0-1)</summary>
        public static event Action<float> OnBossHealthChanged;
        
        /// <summary>Boss 死亡</summary>
        public static event Action OnBossDeath;
        
        /// <summary>Boss 战斗开始</summary>
        public static event Action OnBossFightStart;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件触发方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public static void TriggerGameStateChanged(GameState state) => OnGameStateChanged?.Invoke(state);
        public static void TriggerGameStart() => OnGameStart?.Invoke();
        public static void TriggerGamePaused() => OnGamePaused?.Invoke();
        public static void TriggerGameResumed() => OnGameResumed?.Invoke();
        public static void TriggerGameVictory() => OnGameVictory?.Invoke();
        public static void TriggerGameDefeat() => OnGameDefeat?.Invoke();
        
        public static void TriggerExpChanged(int current, int required) => OnExpChanged?.Invoke(current, required);
        public static void TriggerLevelUp(int newLevel) => OnLevelUp?.Invoke(newLevel);
        public static void TriggerCoinChanged(int coins) => OnCoinChanged?.Invoke(coins);
        public static void TriggerUltEnergyChanged(int current, int max) => OnUltEnergyChanged?.Invoke(current, max);
        public static void TriggerUltReady() => OnUltReady?.Invoke();
        public static void TriggerUltUsed() => OnUltUsed?.Invoke();
        public static void TriggerKillCountChanged(int count) => OnKillCountChanged?.Invoke(count);
        public static void TriggerComboChanged(int combo) => OnComboChanged?.Invoke(combo);
        public static void TriggerComboReset() => OnComboReset?.Invoke();
        
        public static void TriggerShieldHPChanged(int current, int max) => OnShieldHPChanged?.Invoke(current, max);
        public static void TriggerHullHPChanged(int current, int max) => OnHullHPChanged?.Invoke(current, max);
        
        public static void TriggerGameTimeUpdated(float current, float total) => OnGameTimeUpdated?.Invoke(current, total);
        
        public static void TriggerEnemyDied(Pool.EnemyType type, Vector3 pos, int xp, int coin) 
            => OnEnemyDied?.Invoke(type, pos, xp, coin);
        public static void TriggerXPOrbCollected(int xp) => OnXPOrbCollected?.Invoke(xp);
        public static void TriggerLevelUpChoiceComplete() => OnLevelUpChoiceComplete?.Invoke();
        public static void TriggerBossHealthChanged(float percent) => OnBossHealthChanged?.Invoke(percent);
        public static void TriggerBossDeath() => OnBossDeath?.Invoke();
        public static void TriggerBossFightStart() => OnBossFightStart?.Invoke();
        //护盾血量
        public static void TriggerShieldBroken() => OnShieldBroken?.Invoke();
        public static void TriggerLowHealthStart() => OnLowHealthStart?.Invoke();
        public static void TriggerLowHealthEnd() => OnLowHealthEnd?.Invoke();
        public static void TriggerPlayerShieldDamaged(int damage, Vector3 pos) => OnPlayerShieldDamaged?.Invoke(damage, pos);
        public static void TriggerPlayerHealthDamaged(int damage, Vector3 pos) => OnPlayerHealthDamaged?.Invoke(damage, pos);
        public static void TriggerPlayerShieldRestored(int amount, Vector3 pos) => OnPlayerShieldRestored?.Invoke(amount, pos);
        public static void TriggerPlayerHealthRestored(int amount, Vector3 pos) => OnPlayerHealthRestored?.Invoke(amount, pos);
        /// <summary>
        /// 清除所有事件订阅（场景切换时调用）
        /// </summary>
        public static void ClearAllEvents()
        {
            OnGameStateChanged = null;
            OnGameStart = null;
            OnGamePaused = null;
            OnGameResumed = null;
            OnGameVictory = null;
            OnGameDefeat = null;
            
            OnExpChanged = null;
            OnLevelUp = null;
            OnCoinChanged = null;
            OnUltEnergyChanged = null;
            OnUltReady = null;
            OnUltUsed = null;
            OnKillCountChanged = null;
            OnComboChanged = null;
            OnComboReset = null;
            
            OnShieldHPChanged = null;
            OnHullHPChanged = null;
            
            OnGameTimeUpdated = null;
            OnEnemyDied = null;
            OnXPOrbCollected = null;
            OnLevelUpChoiceComplete = null;
            OnSkillApplied = null;
            // Boss 事件清理
            OnBossHealthChanged = null;
            OnBossDeath = null;
            OnBossFightStart = null;
            //护盾和血量
            OnShieldBroken = null;
            OnLowHealthStart = null;
            OnLowHealthEnd = null;
            OnPlayerShieldDamaged = null;
            OnPlayerHealthDamaged = null;
            OnPlayerShieldRestored = null;
            OnPlayerHealthRestored = null;
        }
    }
}